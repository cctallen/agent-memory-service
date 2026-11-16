using AgentMemoryService.Shared.Core;
using AgentMemoryService.Shared.Infrastructure;
using AgentMemoryService.Shared.Infrastructure.Caching;
using AgentMemoryService.Namespaces.Application.Commands;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AgentMemoryService.Namespaces.Application.Queries;

public sealed record GetNamespacesQuery(Guid UserId, int Page = 1, int PageSize = 20)
    : IRequest<Result<PagedResult<NamespaceResponse>>>;

public sealed record PagedResult<T>(List<T> Items, int Total, int Page, int PageSize);

public sealed class GetNamespacesHandler : IRequestHandler<GetNamespacesQuery, Result<PagedResult<NamespaceResponse>>>
{
    private readonly AppDbContext _db;
    private readonly IMemoryCacheService _cache;

    public GetNamespacesHandler(AppDbContext db, IMemoryCacheService cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<Result<PagedResult<NamespaceResponse>>> Handle(GetNamespacesQuery query, CancellationToken ct)
    {
        var cacheKey = $"namespaces:user:{query.UserId}:page:{query.Page}:{query.PageSize}";
        var cached = await _cache.GetOrCreateAsync(cacheKey, async (innerCt) =>
        {
            // Resolve the orgs this user belongs to
            var userOrgIds = await _db.OrganizationMembers
                .Where(m => m.UserId == query.UserId)
                .Select(m => m.OrganizationId)
                .ToListAsync(innerCt);

            var q = _db.Namespaces
                .Include(n => n.Members)
                .Where(n =>
                    n.OwnerId == query.UserId ||
                    n.Members.Any(m => m.UserId == query.UserId) ||
                    (n.IsPublic && n.OrganizationId != null && userOrgIds.Contains(n.OrganizationId!.Value)));

            var total = await q.CountAsync(innerCt);
            var items = await q
                .OrderBy(n => n.DisplayName)
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .Select(n => n.ToResponse())
                .ToListAsync(innerCt);

            return new PagedResult<NamespaceResponse>(items, total, query.Page, query.PageSize);
        }, expiry: TimeSpan.FromMinutes(2), ct: ct);

        return Result<PagedResult<NamespaceResponse>>.Success(cached!);
    }
}

public sealed record GetNamespaceBySlugQuery(string Slug, Guid RequesterId) : IRequest<Result<NamespaceResponse>>;

public sealed class GetNamespaceBySlugHandler : IRequestHandler<GetNamespaceBySlugQuery, Result<NamespaceResponse>>
{
    private readonly AppDbContext _db;

    public GetNamespaceBySlugHandler(AppDbContext db) => _db = db;

    public async Task<Result<NamespaceResponse>> Handle(GetNamespaceBySlugQuery query, CancellationToken ct)
    {
        var ns = await _db.Namespaces
            .Include(n => n.Members)
            .FirstOrDefaultAsync(n => n.Slug == query.Slug, ct);

        if (ns is null) return Result<NamespaceResponse>.Failure(Error.NotFound("Namespace.NotFound", "Namespace not found"));

        // Explicit member or owner — always allowed
        if (ns.OwnerId == query.RequesterId || ns.Members.Any(m => m.UserId == query.RequesterId))
            return Result<NamespaceResponse>.Success(ns.ToResponse());

        // Public namespace — only allowed if requester belongs to the same org
        if (ns.IsPublic && ns.OrganizationId.HasValue)
        {
            var inOrg = await _db.OrganizationMembers
                .AnyAsync(m => m.OrganizationId == ns.OrganizationId.Value && m.UserId == query.RequesterId, ct);
            if (inOrg) return Result<NamespaceResponse>.Success(ns.ToResponse());
        }

        return Result<NamespaceResponse>.Failure(Error.Forbidden("Namespace.Forbidden", "Access denied"));
    }
}
