using AgentMemoryService.Shared.Core;
using AgentMemoryService.Shared.Infrastructure;
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

    public GetNamespacesHandler(AppDbContext db) => _db = db;

    public async Task<Result<PagedResult<NamespaceResponse>>> Handle(GetNamespacesQuery query, CancellationToken ct)
    {
        var q = _db.Namespaces
            .Include(n => n.Members)
            .Where(n => n.OwnerId == query.UserId || n.IsPublic || n.Members.Any(m => m.UserId == query.UserId));

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderBy(n => n.DisplayName)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(n => n.ToResponse())
            .ToListAsync(ct);

        return Result<PagedResult<NamespaceResponse>>.Success(new PagedResult<NamespaceResponse>(items, total, query.Page, query.PageSize));
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
