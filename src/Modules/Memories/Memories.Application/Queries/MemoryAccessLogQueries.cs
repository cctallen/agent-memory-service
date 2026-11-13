using AgentMemoryService.Memories.Core.Entities;
using AgentMemoryService.Shared.Core;
using AgentMemoryService.Shared.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AgentMemoryService.Memories.Application.Queries;

// --- Get Access Logs for a namespace ---
public sealed record GetMemoryAccessLogsQuery(
    Guid NamespaceId,
    Guid RequesterId,
    int Page = 1,
    int PageSize = 50,
    Guid? AgentId = null,
    Guid? MemoryId = null,
    AccessOperation? Operation = null,
    DateTime? Since = null,
    DateTime? Until = null)
    : IRequest<Result<PagedResult<MemoryAccessLogResponse>>>;

public sealed record MemoryAccessLogResponse(
    Guid Id,
    Guid NamespaceId,
    Guid? MemoryId,
    Guid AgentId,
    string AgentName,
    string Operation,
    string? Query,
    int RecordCount,
    DateTime AccessedAt);

public sealed class GetMemoryAccessLogsHandler
    : IRequestHandler<GetMemoryAccessLogsQuery, Result<PagedResult<MemoryAccessLogResponse>>>
{
    private readonly AppDbContext _db;

    public GetMemoryAccessLogsHandler(AppDbContext db) => _db = db;

    public async Task<Result<PagedResult<MemoryAccessLogResponse>>> Handle(
        GetMemoryAccessLogsQuery query, CancellationToken ct)
    {
        var ns = await _db.Namespaces.Include(n => n.Members)
            .FirstOrDefaultAsync(n => n.Id == query.NamespaceId, ct);
        if (ns is null)
            return Result<PagedResult<MemoryAccessLogResponse>>.Failure(
                Error.NotFound("Namespace.NotFound", "Namespace not found"));
        if (!ns.HasAccess(query.RequesterId))
            return Result<PagedResult<MemoryAccessLogResponse>>.Failure(
                Error.Forbidden("AccessLog.Forbidden", "Access denied"));

        var q = _db.MemoryAccessLogs.Where(l => l.NamespaceId == query.NamespaceId);

        if (query.AgentId.HasValue)
            q = q.Where(l => l.AgentId == query.AgentId.Value);
        if (query.MemoryId.HasValue)
            q = q.Where(l => l.MemoryId == query.MemoryId.Value);
        if (query.Operation.HasValue)
            q = q.Where(l => l.Operation == query.Operation.Value);
        if (query.Since.HasValue)
            q = q.Where(l => l.AccessedAt >= query.Since.Value);
        if (query.Until.HasValue)
            q = q.Where(l => l.AccessedAt <= query.Until.Value);

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(l => l.AccessedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        var responses = items.Select(l => new MemoryAccessLogResponse(
            l.Id, l.NamespaceId, l.MemoryId, l.AgentId, l.AgentName,
            l.Operation.ToString(), l.Query, l.RecordCount, l.AccessedAt)).ToList();

        return Result<PagedResult<MemoryAccessLogResponse>>.Success(
            new PagedResult<MemoryAccessLogResponse>(responses, total, query.Page, query.PageSize));
    }
}
