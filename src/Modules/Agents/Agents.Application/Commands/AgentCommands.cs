using AgentMemoryService.Shared.Core;
using AgentMemoryService.Shared.Infrastructure;
using AgentMemoryService.Agents.Core.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AgentMemoryService.Agents.Application.Commands;

// ─────────────────────────────────────────────────────────────
// Shared response
// ─────────────────────────────────────────────────────────────
public sealed record AgentNamespaceEntry(Guid NamespaceId, AgentPermissions Permissions);
public sealed record AgentResponse(Guid Id, string Name, string? Description, string ApiKey, DateTime CreatedAt, List<Guid> NamespaceIds, List<AgentNamespaceEntry> NamespacePermissions);

public static class AgentMappings
{
    public static AgentResponse ToResponse(this Agent a) =>
        new(a.Id, a.Name, a.Description, a.ApiKey, a.CreatedAt,
            a.Namespaces.Select(n => n.NamespaceId).ToList(),
            a.Namespaces.Select(n => new AgentNamespaceEntry(n.NamespaceId, n.Permissions)).ToList());
}

// ─────────────────────────────────────────────────────────────
// Create Agent (global — not tied to a namespace at creation)
// ─────────────────────────────────────────────────────────────
public sealed record CreateAgentCommand(string Name, string? Description, Guid RequesterId, Guid? NamespaceId = null)
    : IRequest<Result<AgentResponse>>;

public sealed class CreateAgentHandler : IRequestHandler<CreateAgentCommand, Result<AgentResponse>>
{
    private readonly AppDbContext _db;

    public CreateAgentHandler(AppDbContext db) => _db = db;

    public async Task<Result<AgentResponse>> Handle(CreateAgentCommand command, CancellationToken ct)
    {
        // If a namespace is provided, verify write access
        if (command.NamespaceId.HasValue)
        {
            var ns = await _db.Namespaces.Include(n => n.Members)
                .FirstOrDefaultAsync(n => n.Id == command.NamespaceId.Value, ct);
            if (ns is null) return Result<AgentResponse>.Failure(Error.NotFound("Namespace.NotFound", "Namespace not found"));
            if (!ns.CanWrite(command.RequesterId)) return Result<AgentResponse>.Failure(Error.Forbidden("Agent.Forbidden", "Writer access required"));
        }

        var agent = Agent.Create(command.Name, command.Description);

        if (command.NamespaceId.HasValue)
            agent.AddNamespace(command.NamespaceId.Value);

        _db.Agents.Add(agent);
        await _db.SaveChangesAsync(ct);

        return Result<AgentResponse>.Success(agent.ToResponse());
    }
}

// ─────────────────────────────────────────────────────────────
// Add Agent to Namespace
// ─────────────────────────────────────────────────────────────
public sealed record AddAgentToNamespaceCommand(Guid AgentId, Guid NamespaceId, Guid RequesterId, AgentPermissions Permissions = AgentPermissions.ReadWrite)
    : IRequest<Result<AgentResponse>>;

public sealed class AddAgentToNamespaceHandler : IRequestHandler<AddAgentToNamespaceCommand, Result<AgentResponse>>
{
    private readonly AppDbContext _db;

    public AddAgentToNamespaceHandler(AppDbContext db) => _db = db;

    public async Task<Result<AgentResponse>> Handle(AddAgentToNamespaceCommand command, CancellationToken ct)
    {
        var ns = await _db.Namespaces.Include(n => n.Members)
            .FirstOrDefaultAsync(n => n.Id == command.NamespaceId, ct);
        if (ns is null) return Result<AgentResponse>.Failure(Error.NotFound("Namespace.NotFound", "Namespace not found"));
        if (!ns.CanWrite(command.RequesterId)) return Result<AgentResponse>.Failure(Error.Forbidden("Agent.Forbidden", "Writer access required"));

        var agent = await _db.Agents.Include(a => a.Namespaces)
            .FirstOrDefaultAsync(a => a.Id == command.AgentId, ct);
        if (agent is null) return Result<AgentResponse>.Failure(Error.NotFound("Agent.NotFound", "Agent not found"));

        agent.AddNamespace(command.NamespaceId, command.Permissions);
        await _db.SaveChangesAsync(ct);

        return Result<AgentResponse>.Success(agent.ToResponse());
    }
}

// ─────────────────────────────────────────────────────────────
// Update Agent Namespace Permissions
// ─────────────────────────────────────────────────────────────
public sealed record UpdateAgentPermissionsCommand(Guid AgentId, Guid NamespaceId, Guid RequesterId, AgentPermissions Permissions)
    : IRequest<Result<AgentResponse>>;

public sealed class UpdateAgentPermissionsHandler : IRequestHandler<UpdateAgentPermissionsCommand, Result<AgentResponse>>
{
    private readonly AppDbContext _db;

    public UpdateAgentPermissionsHandler(AppDbContext db) => _db = db;

    public async Task<Result<AgentResponse>> Handle(UpdateAgentPermissionsCommand command, CancellationToken ct)
    {
        var ns = await _db.Namespaces.Include(n => n.Members)
            .FirstOrDefaultAsync(n => n.Id == command.NamespaceId, ct);
        if (ns is null) return Result<AgentResponse>.Failure(Error.NotFound("Namespace.NotFound", "Namespace not found"));
        if (!ns.CanWrite(command.RequesterId)) return Result<AgentResponse>.Failure(Error.Forbidden("Agent.Forbidden", "Writer access required"));

        var agent = await _db.Agents.Include(a => a.Namespaces)
            .FirstOrDefaultAsync(a => a.Id == command.AgentId, ct);
        if (agent is null) return Result<AgentResponse>.Failure(Error.NotFound("Agent.NotFound", "Agent not found"));

        var updated = agent.UpdateNamespacePermissions(command.NamespaceId, command.Permissions);
        if (!updated) return Result<AgentResponse>.Failure(Error.NotFound("Agent.NotInNamespace", "Agent is not in this namespace"));

        await _db.SaveChangesAsync(ct);
        return Result<AgentResponse>.Success(agent.ToResponse());
    }
}

// ─────────────────────────────────────────────────────────────
// Remove Agent from Namespace
// ─────────────────────────────────────────────────────────────
public sealed record RemoveAgentFromNamespaceCommand(Guid AgentId, Guid NamespaceId, Guid RequesterId)
    : IRequest<Result<bool>>;

public sealed class RemoveAgentFromNamespaceHandler : IRequestHandler<RemoveAgentFromNamespaceCommand, Result<bool>>
{
    private readonly AppDbContext _db;

    public RemoveAgentFromNamespaceHandler(AppDbContext db) => _db = db;

    public async Task<Result<bool>> Handle(RemoveAgentFromNamespaceCommand command, CancellationToken ct)
    {
        var ns = await _db.Namespaces.Include(n => n.Members)
            .FirstOrDefaultAsync(n => n.Id == command.NamespaceId, ct);
        if (ns is null) return Result<bool>.Failure(Error.NotFound("Namespace.NotFound", "Namespace not found"));
        if (!ns.CanWrite(command.RequesterId)) return Result<bool>.Failure(Error.Forbidden("Agent.Forbidden", "Writer access required"));

        var agent = await _db.Agents.Include(a => a.Namespaces)
            .FirstOrDefaultAsync(a => a.Id == command.AgentId, ct);
        if (agent is null) return Result<bool>.Failure(Error.NotFound("Agent.NotFound", "Agent not found"));

        var removed = agent.RemoveNamespace(command.NamespaceId);
        if (!removed) return Result<bool>.Failure(Error.NotFound("Agent.NotInNamespace", "Agent is not in this namespace"));

        await _db.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}

// ─────────────────────────────────────────────────────────────
// Delete Agent entirely
// ─────────────────────────────────────────────────────────────
public sealed record DeleteAgentCommand(Guid AgentId, Guid RequesterId) : IRequest<Result<bool>>;

public sealed class DeleteAgentHandler : IRequestHandler<DeleteAgentCommand, Result<bool>>
{
    private readonly AppDbContext _db;

    public DeleteAgentHandler(AppDbContext db) => _db = db;

    public async Task<Result<bool>> Handle(DeleteAgentCommand command, CancellationToken ct)
    {
        var agent = await _db.Agents.FirstOrDefaultAsync(a => a.Id == command.AgentId, ct);
        if (agent is null) return Result<bool>.Failure(Error.NotFound("Agent.NotFound", "Agent not found"));

        // Must be owner/writer of at least one of the agent's namespaces
        var namespaceIds = await _db.AgentNamespaces
            .Where(m => m.AgentId == command.AgentId)
            .Select(m => m.NamespaceId)
            .ToListAsync(ct);

        var hasAccess = await _db.Namespaces
            .Include(n => n.Members)
            .Where(n => namespaceIds.Contains(n.Id))
            .AnyAsync(n => n.OwnerId == command.RequesterId ||
                n.Members.Any(m => m.UserId == command.RequesterId && m.Role >= AgentMemoryService.Namespaces.Core.Entities.NamespaceRole.Writer), ct);

        if (!hasAccess)
            return Result<bool>.Failure(Error.Forbidden("Agent.Forbidden", "Writer access required in at least one agent namespace"));

        _db.Agents.Remove(agent);
        await _db.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}

// ─────────────────────────────────────────────────────────────
// Rotate API Key
// ─────────────────────────────────────────────────────────────
public sealed record RotateAgentKeyCommand(Guid AgentId, Guid RequesterId) : IRequest<Result<string>>;

public sealed class RotateAgentKeyHandler : IRequestHandler<RotateAgentKeyCommand, Result<string>>
{
    private readonly AppDbContext _db;

    public RotateAgentKeyHandler(AppDbContext db) => _db = db;

    public async Task<Result<string>> Handle(RotateAgentKeyCommand command, CancellationToken ct)
    {
        var agent = await _db.Agents.FindAsync([command.AgentId], ct);
        if (agent is null) return Result<string>.Failure(Error.NotFound("Agent.NotFound", "Agent not found"));

        agent.RotateApiKey();
        await _db.SaveChangesAsync(ct);
        return Result<string>.Success(agent.ApiKey);
    }
}
