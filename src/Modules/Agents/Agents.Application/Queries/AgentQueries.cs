using AgentMemoryService.Agents.Application.Commands;
using AgentMemoryService.Shared.Core;
using AgentMemoryService.Shared.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AgentMemoryService.Agents.Application.Queries;

// ─────────────────────────────────────────────────────────────
// Get agents for a namespace
// ─────────────────────────────────────────────────────────────
public sealed record GetAgentsQuery(Guid NamespaceId, Guid RequesterId) : IRequest<Result<List<AgentResponse>>>;

public sealed class GetAgentsHandler : IRequestHandler<GetAgentsQuery, Result<List<AgentResponse>>>
{
    private readonly AppDbContext _db;

    public GetAgentsHandler(AppDbContext db) => _db = db;

    public async Task<Result<List<AgentResponse>>> Handle(GetAgentsQuery request, CancellationToken ct)
    {
        var ns = await _db.Namespaces
            .Include(n => n.Members)
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == request.NamespaceId, ct);

        if (ns is null)
            return Result<List<AgentResponse>>.Failure(Error.NotFound("Namespace.NotFound", "Namespace not found"));

        if (!ns.HasAccess(request.RequesterId))
            return Result<List<AgentResponse>>.Failure(Error.Forbidden("Agent.Forbidden", "Read access required"));

        var agentIds = await _db.AgentNamespaces
            .Where(m => m.NamespaceId == request.NamespaceId)
            .Select(m => m.AgentId)
            .ToListAsync(ct);

        var agents = await _db.Agents
            .Include(a => a.Namespaces)
            .AsNoTracking()
            .Where(a => agentIds.Contains(a.Id))
            .OrderBy(a => a.CreatedAt)
            .ToListAsync(ct);

        return Result<List<AgentResponse>>.Success(agents.Select(a => a.ToResponse()).ToList());
    }
}

// ─────────────────────────────────────────────────────────────
// Get all agents the requester has access to (cross-namespace)
// ─────────────────────────────────────────────────────────────
public sealed record GetAllAgentsQuery(Guid RequesterId) : IRequest<Result<List<AgentResponse>>>;

public sealed class GetAllAgentsHandler : IRequestHandler<GetAllAgentsQuery, Result<List<AgentResponse>>>
{
    private readonly AppDbContext _db;

    public GetAllAgentsHandler(AppDbContext db) => _db = db;

    public async Task<Result<List<AgentResponse>>> Handle(GetAllAgentsQuery request, CancellationToken ct)
    {
        // Namespaces the requester owns or is a member of
        var accessibleNamespaceIds = await _db.Namespaces
            .Include(n => n.Members)
            .AsNoTracking()
            .Where(n => n.OwnerId == request.RequesterId || n.Members.Any(m => m.UserId == request.RequesterId))
            .Select(n => n.Id)
            .ToListAsync(ct);

        var agentIds = await _db.AgentNamespaces
            .Where(m => accessibleNamespaceIds.Contains(m.NamespaceId))
            .Select(m => m.AgentId)
            .Distinct()
            .ToListAsync(ct);

        var agents = await _db.Agents
            .Include(a => a.Namespaces)
            .AsNoTracking()
            .Where(a => agentIds.Contains(a.Id))
            .OrderBy(a => a.CreatedAt)
            .ToListAsync(ct);

        return Result<List<AgentResponse>>.Success(agents.Select(a => a.ToResponse()).ToList());
    }
}
