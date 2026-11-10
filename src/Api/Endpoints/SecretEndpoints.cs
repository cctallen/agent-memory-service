using AgentMemoryService.Secrets.Application.Commands;
using AgentMemoryService.Secrets.Application.Queries;
using AgentMemoryService.Shared.Infrastructure;
using MediatR;
using System.Security.Claims;

namespace AgentMemoryService.Api.Endpoints;

/// <summary>Request body for reveal-by-name.</summary>
public sealed record RevealByNameRequest(string Name);

public static class SecretEndpoints
{
    public static void MapSecretEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/namespaces/{namespaceId:guid}/secrets")
            .WithTags("Secrets")
            .RequireAuthorization();

        // List all secrets in namespace (metadata only — no values)
        group.MapGet("/", async (Guid namespaceId, ISender mediator, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var isAgent = user.IsInRole("Agent");
            var result = await mediator.Send(new GetSecretsQuery(namespaceId, userId, IsAgent: isAgent), ct);
            return result.ToHttpResult();
        });

        // Create a new secret
        group.MapPost("/", async (Guid namespaceId, CreateSecretCommand req, ISender mediator, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var isAgent = user.IsInRole("Agent");
            var command = req with { NamespaceId = namespaceId, RequesterId = userId, IsAgent = isAgent };
            var result = await mediator.Send(command, ct);
            return result.ToHttpResult(s => Results.Created($"/api/namespaces/{namespaceId}/secrets/{s.Id}", s));
        });

        // Update a secret (rotate value or rename)
        group.MapPut("/{id:guid}", async (Guid namespaceId, Guid id, UpdateSecretCommand req, ISender mediator, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var isAgent = user.IsInRole("Agent");
            var command = req with { SecretId = id, NamespaceId = namespaceId, RequesterId = userId, IsAgent = isAgent };
            var result = await mediator.Send(command, ct);
            return result.ToHttpResult();
        });

        // Reveal a secret by name — agent-friendly lookup without needing the UUID
        group.MapPost("/reveal-by-name", async (Guid namespaceId, RevealByNameRequest req, ISender mediator, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var userId = user.IsInRole("Agent") ? Guid.Empty : Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var isAgent = user.IsInRole("Agent");
            var result = await mediator.Send(new RevealSecretByNameCommand(req.Name, namespaceId, userId, IsAgent: isAgent), ct);
            return result.ToHttpResult();
        });

        // Reveal (decrypt) a secret value — explicit action to avoid accidental exposure in list
        group.MapPost("/{id:guid}/reveal", async (Guid namespaceId, Guid id, ISender mediator, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var isAgent = user.IsInRole("Agent");
            var result = await mediator.Send(new RevealSecretCommand(id, namespaceId, userId, IsAgent: isAgent), ct);
            return result.ToHttpResult();
        });

        // Delete a secret
        group.MapDelete("/{id:guid}", async (Guid namespaceId, Guid id, ISender mediator, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var isAgent = user.IsInRole("Agent");
            var result = await mediator.Send(new DeleteSecretCommand(id, namespaceId, userId, IsAgent: isAgent), ct);
            return result.ToHttpResult(_ => Results.NoContent());
        });
    }
}
