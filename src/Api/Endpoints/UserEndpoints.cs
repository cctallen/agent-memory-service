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
            .RequireAuthorization();

        // GET /api/users — list all registered users
        group.MapGet("/", async (UserManager<ApplicationUser> userManager, CancellationToken ct) =>
        {
            var users = await userManager.Users
                .AsNoTracking()
                .Where(u => u.IsActive)
                .OrderBy(u => u.Email)
                .Select(u => new
                {
                    u.Id,
                    u.Email,
                    u.FirstName,
                    u.LastName,
                    FullName = (u.FirstName + " " + u.LastName).Trim(),
                    u.CreatedAt
                })
                .ToListAsync(ct);

            return Results.Ok(users);
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
    }
}

// Request body records
public sealed record UpdateUserRequest(string FirstName, string LastName, string Email);
public sealed record ResetPasswordRequest(string NewPassword);
