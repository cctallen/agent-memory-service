using AgentMemoryService.Namespaces.Application.Commands;
using AgentMemoryService.Namespaces.Core.Entities;
using AgentMemoryService.Shared.Infrastructure;
using AgentMemoryService.Users.Core.Entities;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AgentMemoryService.Api.Endpoints;

public static class NamespaceMemberEndpoints
{
    public static void MapNamespaceMemberEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/namespaces/{namespaceId:guid}/members")
            .WithTags("Namespace Members")
            .RequireAuthorization();

        // GET /api/namespaces/{namespaceId}/members
        group.MapGet("/", async (Guid namespaceId, ISender mediator,
            AppDbContext db,
            UserManager<ApplicationUser> userManager,
            ClaimsPrincipal user, CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var ns = await db.Namespaces
                .Include(n => n.Members)
                .AsNoTracking()
                .FirstOrDefaultAsync(n => n.Id == namespaceId, ct);

            if (ns is null) return Results.NotFound();
            if (!ns.HasAccess(userId)) return Results.Forbid();

            // Batch-load user details for all members
            var memberUserIds = ns.Members.Select(m => m.UserId).ToList();
            var users = await userManager.Users
                .AsNoTracking()
                .Where(u => memberUserIds.Contains(u.Id))
                .Select(u => new { u.Id, u.Email, u.FirstName, u.LastName })
                .ToDictionaryAsync(u => u.Id, ct);

            var members = ns.Members.Select(m =>
            {
                var u = users.GetValueOrDefault(m.UserId);
                return new
                {
                    m.UserId,
                    Email = u?.Email ?? "",
                    FullName = ((u?.FirstName ?? "") + " " + (u?.LastName ?? "")).Trim(),
                    m.Role,
                    RoleName = m.Role.ToString(),
                    m.JoinedAt
                };
            });

            return Results.Ok(members);
        });

        // POST /api/namespaces/{namespaceId}/members
        group.MapPost("/", async (Guid namespaceId, AddMemberRequest req, ISender mediator, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await mediator.Send(new AddMemberCommand(namespaceId, userId, req.UserId, req.Role), ct);
            return result.ToHttpResult(_ => Results.NoContent());
        });

        // PUT /api/namespaces/{namespaceId}/members/{userId}
        group.MapPut("/{memberId:guid}", async (Guid namespaceId, Guid memberId, UpdateMemberRoleRequest req, ISender mediator, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await mediator.Send(new UpdateMemberRoleCommand(namespaceId, userId, memberId, req.Role), ct);
            return result.ToHttpResult(_ => Results.NoContent());
        });

        // DELETE /api/namespaces/{namespaceId}/members/{userId}
        group.MapDelete("/{memberId:guid}", async (Guid namespaceId, Guid memberId, ISender mediator, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await mediator.Send(new RemoveMemberCommand(namespaceId, userId, memberId), ct);
            return result.ToHttpResult(_ => Results.NoContent());
        });
    }
}

public sealed record AddMemberRequest(Guid UserId, NamespaceRole Role);
public sealed record UpdateMemberRoleRequest(NamespaceRole Role);
