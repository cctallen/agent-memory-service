using AgentMemoryService.Users.Application.Commands;
using AgentMemoryService.Users.Application.Queries;
using AgentMemoryService.Shared.Infrastructure;
using AgentMemoryService.Users.Core.Entities;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AgentMemoryService.Api.Endpoints;

public static class UserEndpoints
{
    public static void MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/users")
            .WithTags("Users")
            .RequireAuthorization("Admin");

        // GET /api/users — list all registered users (includes roles)
        group.MapGet("/", async (UserManager<ApplicationUser> userManager, CancellationToken ct) =>
        {
            var users = await userManager.Users
                .AsNoTracking()
                .Where(u => u.IsActive)
                .OrderBy(u => u.Email)
                .ToListAsync(ct);

            var result = new List<object>();
            foreach (var u in users)
            {
                var roles = await userManager.GetRolesAsync(u);
                result.Add(new
                {
                    u.Id,
                    u.Email,
                    u.FirstName,
                    u.LastName,
                    FullName = (u.FirstName + " " + u.LastName).Trim(),
                    u.CreatedAt,
                    Roles = roles.ToList()
                });
            }

            return Results.Ok(result);
        })
        .WithSummary("List all registered users");

        // GET /api/users/lookup?email=... — resolve email to user ID
        group.MapGet("/lookup", async (string email, ISender mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new FindUserByEmailQuery(email), ct);
            return result.ToHttpResult();
        })
        .WithSummary("Look up a user by email address");

        // GET /api/users/{id} — get a single user by ID
        group.MapGet("/{id:guid}", async (Guid id, ISender mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetUserByIdQuery(id), ct);
            return result.ToHttpResult();
        })
        .WithSummary("Get a user by ID");

        // PUT /api/users/{id} — update user profile
        group.MapPut("/{id:guid}", async (Guid id, UpdateUserRequest req, ISender mediator, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var requesterId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await mediator.Send(new UpdateUserCommand(id, requesterId, req.FirstName, req.LastName, req.Email), ct);
            return result.ToHttpResult();
        })
        .WithSummary("Update a user's profile");

        // DELETE /api/users/{id} — soft-delete a user
        group.MapDelete("/{id:guid}", async (Guid id, ISender mediator, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var requesterId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await mediator.Send(new DeleteUserCommand(id, requesterId), ct);
            return result.ToHttpResult(_ => Results.NoContent());
        })
        .WithSummary("Deactivate (soft-delete) a user");

        // POST /api/users/{id}/reset-password — admin password reset
        group.MapPost("/{id:guid}/reset-password", async (Guid id, ResetPasswordRequest req, ISender mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new AdminResetPasswordCommand(id, req.NewPassword), ct);
            return result.ToHttpResult(_ => Results.NoContent());
        })
        .WithSummary("Admin reset a user's password");

        // POST /api/users/{id}/roles — assign a role
        group.MapPost("/{id:guid}/roles", async (Guid id, AssignRoleRequest req, ISender mediator, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var requesterId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await mediator.Send(new AssignRoleCommand(id, requesterId, req.Role), ct);
            return result.ToHttpResult(_ => Results.NoContent());
        })
        .WithSummary("Assign a role to a user");

        // DELETE /api/users/{id}/roles/{role} — remove a role
        group.MapDelete("/{id:guid}/roles/{role}", async (Guid id, string role, ISender mediator, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var requesterId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await mediator.Send(new RemoveRoleCommand(id, requesterId, role), ct);
            return result.ToHttpResult(_ => Results.NoContent());
        })
        .WithSummary("Remove a role from a user");
    }
}

// Request body records
public sealed record UpdateUserRequest(string FirstName, string LastName, string Email);
public sealed record ResetPasswordRequest(string NewPassword);
public sealed record AssignRoleRequest(string Role);
