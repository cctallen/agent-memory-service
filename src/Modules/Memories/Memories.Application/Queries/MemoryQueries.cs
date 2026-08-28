using AgentMemoryService.Shared.Core;
using AgentMemoryService.Shared.Infrastructure;
using AgentMemoryService.Memories.Application.Commands;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AgentMemoryService.Memories.Application.Queries;

public sealed record GetMemoriesQuery(Guid NamespaceId, Guid RequesterId, int Page = 1, int PageSize = 20,
    string? TypeFilter = null, Guid? AgentId = null)
    : IRequest<Result<PagedResult<MemoryResponse>>>;

public sealed record PagedResult<T>(List<T> Items, int Total, int Page, int PageSize);

public sealed class GetMemoriesHandler : IRequestHandler<GetMemoriesQuery, Result<PagedResult<MemoryResponse>>>
{
    private readonly AppDbContext _db;

    public GetMemoriesHandler(AppDbContext db) => _db = db;

    public async Task<Result<PagedResult<MemoryResponse>>> Handle(GetMemoriesQuery query, CancellationToken ct)
    {
        var ns = await _db.Namespaces.Include(n => n.Members).FirstOrDefaultAsync(n => n.Id == query.NamespaceId, ct);
        if (ns is null) return Result<PagedResult<MemoryResponse>>.Failure(Error.NotFound("Namespace.NotFound", "Namespace not found"));
        if (!ns.HasAccess(query.RequesterId))
            return Result<PagedResult<MemoryResponse>>.Failure(Error.Forbidden("Memory.Forbidden", "Access denied"));

        var q = _db.Memories.Where(m => m.NamespaceId == query.NamespaceId);

        if (query.AgentId.HasValue)
            q = q.Where(m => m.AgentId == query.AgentId);

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(m => m.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        return Result<PagedResult<MemoryResponse>>.Success(
            new PagedResult<MemoryResponse>(items.Select(m => m.ToResponse()).ToList(), total, query.Page, query.PageSize));
    }
}

public sealed record SearchMemoriesQuery(Guid NamespaceId, Guid RequesterId, string Query, int TopK = 10)
    : IRequest<Result<List<MemoryResponse>>>;

public sealed class SearchMemoriesHandler : IRequestHandler<SearchMemoriesQuery, Result<List<MemoryResponse>>>
{
    private readonly AppDbContext _db;

    public SearchMemoriesHandler(AppDbContext db) => _db = db;

    public async Task<Result<List<MemoryResponse>>> Handle(SearchMemoriesQuery query, CancellationToken ct)
    {
        var ns = await _db.Namespaces.Include(n => n.Members).FirstOrDefaultAsync(n => n.Id == query.NamespaceId, ct);
        if (ns is null) return Result<List<MemoryResponse>>.Failure(Error.NotFound("Namespace.NotFound", "Namespace not found"));
        if (!ns.HasAccess(query.RequesterId))
            return Result<List<MemoryResponse>>.Failure(Error.Forbidden("Memory.Forbidden", "Access denied"));

        // Text search fallback (vector search added when embedding service wired)
        var results = await _db.Memories
            .Where(m => m.NamespaceId == query.NamespaceId && EF.Functions.ILike(m.Content, $"%{query.Query}%"))
            .OrderByDescending(m => m.CreatedAt)
            .Take(query.TopK)
            .ToListAsync(ct);

        return Result<List<MemoryResponse>>.Success(results.Select(m => m.ToResponse()).ToList());
    }
}
