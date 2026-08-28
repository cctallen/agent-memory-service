using AgentMemoryService.Namespaces.Application.Commands;
using AgentMemoryService.Namespaces.Application.Queries;
using AgentMemoryService.Shared.Core;
using AgentMemoryService.Shared.Infrastructure;
using MediatR;
using System.Security.Claims;

namespace AgentMemoryService.Api.Endpoints;

public static class NamespaceEndpoints
{
    public static void MapNamespaceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/namespaces").WithTags("Namespaces").RequireAuthorization();

        group.MapGet("/", async (ISender mediator, ClaimsPrincipal user, int page = 1, int pageSize = 20, CancellationToken ct = default) =>
        {
            var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await mediator.Send(new GetNamespacesQuery(userId, page, pageSize), ct);
            return result.ToHttpResult();
        });

        group.MapGet("/{slug}", async (string slug, ISender mediator, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await mediator.Send(new GetNamespaceBySlugQuery(slug, userId), ct);
            return result.ToHttpResult();
        });

        group.MapPost("/", async (CreateNamespaceCommand req, ISender mediator, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var command = req with { OwnerId = userId };
            var result = await mediator.Send(command, ct);
            return result.ToHttpResult(ns => Results.Created($"/api/namespaces/{ns.Slug}", ns));
        });

        group.MapDelete("/{id:guid}", async (Guid id, ISender mediator, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await mediator.Send(new DeleteNamespaceCommand(id, userId), ct);
            return result.ToHttpResult(_ => Results.NoContent());
        });
    }
}
