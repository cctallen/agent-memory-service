using AgentMemoryService.Agents.Application.Commands;
using AgentMemoryService.Shared.Core;
using AgentMemoryService.Shared.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AgentMemoryService.Agents.Application.Queries;

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

        var agents = await _db.Agents
            .AsNoTracking()
            .Where(a => a.NamespaceId == request.NamespaceId && a.IsActive)
            .OrderBy(a => a.CreatedAt)
            .ToListAsync(ct);

        return Result<List<AgentResponse>>.Success(agents.Select(a => a.ToResponse()).ToList());
    }
}
