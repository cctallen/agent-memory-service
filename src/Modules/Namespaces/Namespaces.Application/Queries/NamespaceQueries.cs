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
            var q = _db.Namespaces
                .Include(n => n.Members)
                .Where(n => n.OwnerId == query.UserId || n.IsPublic || n.Members.Any(m => m.UserId == query.UserId));

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
        if (!ns.IsPublic && !ns.HasAccess(query.RequesterId))
            return Result<NamespaceResponse>.Failure(Error.Forbidden("Namespace.Forbidden", "Access denied"));

        return Result<NamespaceResponse>.Success(ns.ToResponse());
    }
}
