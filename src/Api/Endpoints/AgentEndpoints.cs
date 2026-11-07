using AgentMemoryService.Agents.Application.Commands;
using AgentMemoryService.Agents.Application.Queries;
using AgentMemoryService.Shared.Core;
using AgentMemoryService.Shared.Infrastructure;
using MediatR;
using System.Security.Claims;

namespace AgentMemoryService.Api.Endpoints;

public static class AgentEndpoints
{
    public static void MapAgentEndpoints(this IEndpointRouteBuilder app)
    {
        // ── Top-level agent management ────────────────────────────────
        var agents = app.MapGroup("/api/agents")
            .WithTags("Agents")
            .RequireAuthorization();

        // GET /api/agents — all agents accessible to the requester
        agents.MapGet("/", async (ISender mediator, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await mediator.Send(new GetAllAgentsQuery(userId), ct);
            return result.ToHttpResult();
        });

        // POST /api/agents — create a new agent (optionally add to a namespace)
        agents.MapPost("/", async (CreateAgentCommand req, ISender mediator, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var command = req with { RequesterId = userId };
            var result = await mediator.Send(command, ct);
            return result.ToHttpResult(a => Results.Created($"/api/agents/{a.Id}", a));
        });

        // DELETE /api/agents/{id}
        agents.MapDelete("/{id:guid}", async (Guid id, ISender mediator, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await mediator.Send(new DeleteAgentCommand(id, userId), ct);
            return result.ToHttpResult(_ => Results.NoContent());
        });

        // POST /api/agents/{id}/rotate-key
        agents.MapPost("/{id:guid}/rotate-key", async (Guid id, ISender mediator, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await mediator.Send(new RotateAgentKeyCommand(id, userId), ct);
            return result.ToHttpResult(key => Results.Ok(new { ApiKey = key }));
        });

        // POST /api/agents/{id}/namespaces/{namespaceId} — add agent to a namespace
        agents.MapPost("/{id:guid}/namespaces/{namespaceId:guid}", async (Guid id, Guid namespaceId, ISender mediator, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await mediator.Send(new AddAgentToNamespaceCommand(id, namespaceId, userId), ct);
            return result.ToHttpResult(a => Results.Ok(a));
        });

        // DELETE /api/agents/{id}/namespaces/{namespaceId} — remove agent from a namespace
        agents.MapDelete("/{id:guid}/namespaces/{namespaceId:guid}", async (Guid id, Guid namespaceId, ISender mediator, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await mediator.Send(new RemoveAgentFromNamespaceCommand(id, namespaceId, userId), ct);
            return result.ToHttpResult(_ => Results.NoContent());
        });

        // ── Namespace-scoped agent queries (kept for backwards compat) ─
        var nsAgents = app.MapGroup("/api/namespaces/{namespaceId:guid}/agents")
            .WithTags("Agents")
            .RequireAuthorization();

        // GET /api/namespaces/{namespaceId}/agents
        nsAgents.MapGet("/", async (Guid namespaceId, ISender mediator, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await mediator.Send(new GetAgentsQuery(namespaceId, userId), ct);
            return result.ToHttpResult();
        });

        // POST /api/namespaces/{namespaceId}/agents — create + immediately add to namespace
        nsAgents.MapPost("/", async (Guid namespaceId, CreateAgentCommand req, ISender mediator, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var command = req with { NamespaceId = namespaceId, RequesterId = userId };
            var result = await mediator.Send(command, ct);
            return result.ToHttpResult(a => Results.Created($"/api/namespaces/{namespaceId}/agents/{a.Id}", a));
        });
    }
}
