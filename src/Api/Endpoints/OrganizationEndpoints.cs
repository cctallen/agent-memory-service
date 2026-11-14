using AgentMemoryService.Organizations.Application.Commands;
using AgentMemoryService.Organizations.Application.Queries;
using AgentMemoryService.Organizations.Core.Entities;
using AgentMemoryService.Shared.Infrastructure;
using MediatR;
using Microsoft.AspNetCore.Identity;
using AgentMemoryService.Users.Core.Entities;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AgentMemoryService.Api.Endpoints;

public static class OrganizationEndpoints
{
    public static void MapOrganizationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/organizations")
            .WithTags("Organizations")
            .RequireAuthorization();

        // GET /api/organizations
        group.MapGet("/", async (ISender mediator, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var isAdmin = user.IsInRole("Admin");
            var result = await mediator.Send(new GetOrgsQuery(userId, isAdmin), ct);
            return result.ToHttpResult();
        });

        // POST /api/organizations
        group.MapPost("/", async (CreateOrgRequest req, ISender mediator, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await mediator.Send(new CreateOrgCommand(req.Slug, req.DisplayName, req.Description, userId), ct);
            return result.ToHttpResult(o => Results.Created($"/api/organizations/{o.Id}", o));
        });

        // GET /api/organizations/{id}
        group.MapGet("/{id:guid}", async (Guid id, ISender mediator, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var isAdmin = user.IsInRole("Admin");
            var result = await mediator.Send(new GetOrgByIdQuery(id, userId, isAdmin), ct);
            return result.ToHttpResult();
        });

        // PUT /api/organizations/{id}
        group.MapPut("/{id:guid}", async (Guid id, UpdateOrgRequest req, ISender mediator, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await mediator.Send(new UpdateOrgCommand(id, userId, req.DisplayName, req.Description), ct);
            return result.ToHttpResult();
        });

        // DELETE /api/organizations/{id}
        group.MapDelete("/{id:guid}", async (Guid id, ISender mediator, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await mediator.Send(new DeleteOrgCommand(id, userId), ct);
            return result.ToHttpResult(_ => Results.NoContent());
        });

        // GET /api/organizations/{id}/members
        group.MapGet("/{id:guid}/members", async (Guid id, ISender mediator, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var isAdmin = user.IsInRole("Admin");
            var result = await mediator.Send(new GetOrgMembersQuery(id, userId, isAdmin), ct);
            return result.ToHttpResult();
        });

        // POST /api/organizations/{id}/members
        group.MapPost("/{id:guid}/members", async (Guid id, AddOrgMemberRequest req, ISender mediator,
            ClaimsPrincipal user, UserManager<ApplicationUser> userManager, CancellationToken ct) =>
        {
            var requesterId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

            // Resolve email → userId if email provided
            Guid targetUserId = req.UserId;
            if (targetUserId == Guid.Empty && !string.IsNullOrWhiteSpace(req.Email))
            {
                var found = await userManager.FindByEmailAsync(req.Email);
                if (found is null) return Results.NotFound(new { error = "User not found with that email" });
                targetUserId = found.Id;
            }

            var result = await mediator.Send(new AddOrgMemberCommand(id, requesterId, targetUserId, req.Role), ct);
            return result.ToHttpResult(_ => Results.NoContent());
        });

        // PUT /api/organizations/{id}/members/{userId}
        group.MapPut("/{id:guid}/members/{userId:guid}", async (Guid id, Guid userId, UpdateOrgMemberRoleRequest req,
            ISender mediator, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var requesterId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await mediator.Send(new UpdateOrgMemberRoleCommand(id, requesterId, userId, req.Role), ct);
            return result.ToHttpResult(_ => Results.NoContent());
        });

        // DELETE /api/organizations/{id}/members/{userId}
        group.MapDelete("/{id:guid}/members/{userId:guid}", async (Guid id, Guid userId,
            ISender mediator, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var requesterId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await mediator.Send(new RemoveOrgMemberCommand(id, requesterId, userId), ct);
            return result.ToHttpResult(_ => Results.NoContent());
        });

        // GET /api/organizations/{id}/namespaces
        group.MapGet("/{id:guid}/namespaces", async (Guid id, ISender mediator, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var isAdmin = user.IsInRole("Admin");
            var result = await mediator.Send(new GetOrgNamespacesQuery(id, userId, isAdmin), ct);
            return result.ToHttpResult();
        });

        // POST /api/organizations/{id}/namespaces/{namespaceId} — attach namespace to org
        group.MapPost("/{id:guid}/namespaces/{namespaceId:guid}", async (Guid id, Guid namespaceId,
            ISender mediator, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await mediator.Send(new AttachNamespaceToOrgCommand(id, namespaceId, userId), ct);
            return result.ToHttpResult(_ => Results.NoContent());
        });

        // DELETE /api/organizations/{id}/namespaces/{namespaceId} — detach namespace from org
        group.MapDelete("/{id:guid}/namespaces/{namespaceId:guid}", async (Guid id, Guid namespaceId,
            ISender mediator, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await mediator.Send(new DetachNamespaceFromOrgCommand(id, namespaceId, userId), ct);
            return result.ToHttpResult(_ => Results.NoContent());
        });
    }
}

public sealed record CreateOrgRequest(string Slug, string DisplayName, string? Description);
public sealed record UpdateOrgRequest(string DisplayName, string? Description);
public sealed record AddOrgMemberRequest(Guid UserId, string? Email, OrgRole Role);
public sealed record UpdateOrgMemberRoleRequest(OrgRole Role);
