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
        var group = app.MapGroup("/api/namespaces/{namespaceId:guid}/agents")
            .WithTags("Agents")
            .RequireAuthorization();

        group.MapGet("/", async (Guid namespaceId, ISender mediator, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await mediator.Send(new GetAgentsQuery(namespaceId, userId), ct);
            return result.ToHttpResult();
        });

        group.MapPost("/", async (Guid namespaceId, CreateAgentCommand req, ISender mediator, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var command = req with { NamespaceId = namespaceId, RequesterId = userId };
            var result = await mediator.Send(command, ct);
            return result.ToHttpResult(a => Results.Created($"/api/namespaces/{namespaceId}/agents/{a.Id}", a));
        });

        group.MapPost("/{id:guid}/rotate-key", async (Guid namespaceId, Guid id, ISender mediator, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await mediator.Send(new RotateAgentKeyCommand(id, userId), ct);
            return result.ToHttpResult(key => Results.Ok(new { ApiKey = key }));
        });

        group.MapDelete("/{id:guid}", async (Guid namespaceId, Guid id, ISender mediator, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await mediator.Send(new DeleteAgentCommand(id, userId), ct);
            return result.ToHttpResult(_ => Results.NoContent());
        });
    }
}
