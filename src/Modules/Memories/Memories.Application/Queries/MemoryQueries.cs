using AgentMemoryService.Memories.Core.Entities;
using AgentMemoryService.Shared.Core;
using AgentMemoryService.Shared.Infrastructure;
using AgentMemoryService.Shared.Infrastructure.Caching;
using AgentMemoryService.Memories.Application.Commands;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AgentMemoryService.Memories.Application.Queries;

public sealed record GetMemoriesQuery(Guid NamespaceId, Guid RequesterId, int Page = 1, int PageSize = 20,
    string? TypeFilter = null, Guid? AgentId = null, bool IsAgent = false, DateTime? Since = null)
    : IRequest<Result<PagedResult<MemoryResponse>>>;

public sealed record PagedResult<T>(List<T> Items, int Total, int Page, int PageSize);

public sealed class GetMemoriesHandler : IRequestHandler<GetMemoriesQuery, Result<PagedResult<MemoryResponse>>>
{
    private readonly AppDbContext _db;
    private readonly IMemoryCacheService _cache;

    public GetMemoriesHandler(AppDbContext db, IMemoryCacheService cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<Result<PagedResult<MemoryResponse>>> Handle(GetMemoriesQuery query, CancellationToken ct)
    {
        var ns = await _db.Namespaces.Include(n => n.Members).FirstOrDefaultAsync(n => n.Id == query.NamespaceId, ct);
        if (ns is null) return Result<PagedResult<MemoryResponse>>.Failure(Error.NotFound("Namespace.NotFound", "Namespace not found"));
        // Agents are pre-scoped to their namespace via API key — skip member check
        if (!query.IsAgent && !ns.HasAccess(query.RequesterId))
            return Result<PagedResult<MemoryResponse>>.Failure(Error.Forbidden("Memory.Forbidden", "Access denied"));
        if (query.IsAgent && ns.Id != query.NamespaceId)
            return Result<PagedResult<MemoryResponse>>.Failure(Error.Forbidden("Memory.Forbidden", "Agent not scoped to this namespace"));

        // Skip cache for since-queries — they're cheap and must be fresh
        var cacheKey = query.Since.HasValue
            ? null
            : $"memories:{query.NamespaceId}:page:{query.Page}:{query.PageSize}:agent:{query.AgentId}";

        async Task<PagedResult<MemoryResponse>> fetchFromDb(CancellationToken innerCt)
        {
            var q = _db.Memories.Where(m => m.NamespaceId == query.NamespaceId);

            if (query.Since.HasValue)
                q = q.Where(m => m.CreatedAt > query.Since.Value);

            if (query.AgentId.HasValue)
                q = q.Where(m => m.AgentId == query.AgentId);

            // Agents see only Public memories OR their own Private ones
            if (query.IsAgent)
                q = q.Where(m => m.Visibility == MemoryVisibility.Public
                    || m.AgentId == query.RequesterId
                    || m.CreatedByUserId == query.RequesterId);

            var total = await q.CountAsync(innerCt);
            var items = await q
                .OrderByDescending(m => m.CreatedAt)
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToListAsync(innerCt);

            return new PagedResult<MemoryResponse>(items.Select(m => m.ToResponse()).ToList(), total, query.Page, query.PageSize);
        }

        var cached = cacheKey is null
            ? await fetchFromDb(ct)
            : await _cache.GetOrCreateAsync(cacheKey, fetchFromDb, expiry: TimeSpan.FromMinutes(1), ct: ct);

        return Result<PagedResult<MemoryResponse>>.Success(cached!);
    }
}

public sealed record SearchMemoriesQuery(Guid NamespaceId, Guid RequesterId, string Query, int TopK = 10,
    bool IsAgent = false)
    : IRequest<Result<List<MemoryResponse>>>;

public sealed class SearchMemoriesHandler : IRequestHandler<SearchMemoriesQuery, Result<List<MemoryResponse>>>
{
    private readonly AppDbContext _db;

    public SearchMemoriesHandler(AppDbContext db) => _db = db;

    public async Task<Result<List<MemoryResponse>>> Handle(SearchMemoriesQuery query, CancellationToken ct)
    {
        var ns = await _db.Namespaces.Include(n => n.Members).FirstOrDefaultAsync(n => n.Id == query.NamespaceId, ct);
        if (ns is null) return Result<List<MemoryResponse>>.Failure(Error.NotFound("Namespace.NotFound", "Namespace not found"));
        // Agents are pre-scoped to their namespace via API key — skip member check
        if (!query.IsAgent && !ns.HasAccess(query.RequesterId))
            return Result<List<MemoryResponse>>.Failure(Error.Forbidden("Memory.Forbidden", "Access denied"));

        // Text search fallback (vector search added when embedding service wired)
        var q = _db.Memories
            .Where(m => m.NamespaceId == query.NamespaceId && EF.Functions.ILike(m.Content, $"%{query.Query}%"));

        // Agents see only Public memories OR their own Private ones
        if (query.IsAgent)
            q = q.Where(m => m.Visibility == MemoryVisibility.Public
                || m.AgentId == query.RequesterId
                || m.CreatedByUserId == query.RequesterId);

        var results = await q
            .OrderByDescending(m => m.CreatedAt)
            .Take(query.TopK)
            .ToListAsync(ct);

        return Result<List<MemoryResponse>>.Success(results.Select(m => m.ToResponse()).ToList());
    }
}
