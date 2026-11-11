using AgentMemoryService.Users.Application.Queries;
using AgentMemoryService.Shared.Infrastructure;
using AgentMemoryService.Users.Core.Entities;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

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
    }
}
