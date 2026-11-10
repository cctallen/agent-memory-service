using AgentMemoryService.Users.Application.Queries;
using AgentMemoryService.Shared.Infrastructure;
using MediatR;

namespace AgentMemoryService.Api.Endpoints;

public static class UserEndpoints
{
    public static void MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/users")
            .WithTags("Users")
            .RequireAuthorization();

        // GET /api/users/lookup?email=... — resolve email to user ID
        group.MapGet("/lookup", async (string email, ISender mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new FindUserByEmailQuery(email), ct);
            return result.ToHttpResult();
        })
        .WithSummary("Look up a user by email address");
    }
}
