using AgentMemoryService.Shared.Core;
using AgentMemoryService.Shared.Infrastructure;
using AgentMemoryService.Agents.Core.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AgentMemoryService.Agents.Application.Commands;

public sealed record CreateAgentCommand(Guid NamespaceId, string Name, string? Description, Guid RequesterId)
    : IRequest<Result<AgentResponse>>;

public sealed record AgentResponse(Guid Id, Guid NamespaceId, string Name, string? Description, string ApiKey, DateTime CreatedAt);

public sealed class CreateAgentHandler : IRequestHandler<CreateAgentCommand, Result<AgentResponse>>
{
    private readonly AppDbContext _db;

    public CreateAgentHandler(AppDbContext db) => _db = db;

    public async Task<Result<AgentResponse>> Handle(CreateAgentCommand command, CancellationToken ct)
    {
        var ns = await _db.Namespaces.Include(n => n.Members).FirstOrDefaultAsync(n => n.Id == command.NamespaceId, ct);
        if (ns is null) return Result<AgentResponse>.Failure(Error.NotFound("Namespace.NotFound", "Namespace not found"));
        if (!ns.CanWrite(command.RequesterId)) return Result<AgentResponse>.Failure(Error.Forbidden("Agent.Forbidden", "Writer access required"));

        var agent = Agent.Create(command.NamespaceId, command.Name, command.Description);
        _db.Agents.Add(agent);
        await _db.SaveChangesAsync(ct);

        return Result<AgentResponse>.Success(agent.ToResponse());
    }
}

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

public sealed record DeleteAgentCommand(Guid AgentId, Guid RequesterId) : IRequest<Result<bool>>;

public sealed class DeleteAgentHandler : IRequestHandler<DeleteAgentCommand, Result<bool>>
{
    private readonly AppDbContext _db;

    public DeleteAgentHandler(AppDbContext db) => _db = db;

    public async Task<Result<bool>> Handle(DeleteAgentCommand command, CancellationToken ct)
    {
        var agent = await _db.Agents
            .FirstOrDefaultAsync(a => a.Id == command.AgentId, ct);
        if (agent is null) return Result<bool>.Failure(Error.NotFound("Agent.NotFound", "Agent not found"));

        var ns = await _db.Namespaces.Include(n => n.Members)
            .FirstOrDefaultAsync(n => n.Id == agent.NamespaceId, ct);
        if (ns is null || !ns.CanWrite(command.RequesterId))
            return Result<bool>.Failure(Error.Forbidden("Agent.Forbidden", "Writer access required"));

        _db.Agents.Remove(agent);
        await _db.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}

public static class AgentMappings
{
    public static AgentResponse ToResponse(this Agent a) =>
        new(a.Id, a.NamespaceId, a.Name, a.Description, a.ApiKey, a.CreatedAt);
}
