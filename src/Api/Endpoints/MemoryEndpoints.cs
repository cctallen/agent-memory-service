using AgentMemoryService.Memories.Application.Commands;
using AgentMemoryService.Memories.Application.Queries;
using AgentMemoryService.Shared.Core;
using AgentMemoryService.Shared.Infrastructure;
using MediatR;
using System.Security.Claims;

namespace AgentMemoryService.Api.Endpoints;

public static class MemoryEndpoints
{
    public static void MapMemoryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/namespaces/{namespaceId:guid}/memories")
            .WithTags("Memories")
            .RequireAuthorization();

        group.MapGet("/", async (Guid namespaceId, ISender mediator, ClaimsPrincipal user,
            int page = 1, int pageSize = 20, Guid? agentId = null, CancellationToken ct = default) =>
        {
            var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var isAgent = user.IsInRole("Agent");
            var result = await mediator.Send(new GetMemoriesQuery(namespaceId, userId, page, pageSize, AgentId: agentId, IsAgent: isAgent), ct);
            return result.ToHttpResult();
        });

        group.MapGet("/search", async (Guid namespaceId, string q, ISender mediator, ClaimsPrincipal user,
            int topK = 10, CancellationToken ct = default) =>
        {
            var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var isAgent = user.IsInRole("Agent");
            var result = await mediator.Send(new SearchMemoriesQuery(namespaceId, userId, q, topK, IsAgent: isAgent), ct);
            return result.ToHttpResult();
        });

        group.MapPost("/", async (Guid namespaceId, AddMemoryCommand req, ISender mediator, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var isAgent = user.IsInRole("Agent");
            var command = req with { NamespaceId = namespaceId, RequesterId = userId, IsAgent = isAgent };
            var result = await mediator.Send(command, ct);
            return result.ToHttpResult(m => Results.Created($"/api/namespaces/{namespaceId}/memories/{m.Id}", m));
        });

        group.MapDelete("/{id:guid}", async (Guid namespaceId, Guid id, ISender mediator, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await mediator.Send(new DeleteMemoryCommand(id, userId), ct);
            return result.ToHttpResult(_ => Results.NoContent());
        });
    }
}
