using AgentMemoryService.Shared.Core;
using AgentMemoryService.Shared.Infrastructure;
using AgentMemoryService.Users.Application.Commands;
using MediatR;

namespace AgentMemoryService.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/register", async (RegisterCommand command, ISender mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(command, ct);
            return result.ToHttpResult(user => Results.Created($"/api/users/{user.UserId}", user));
        })
        .WithSummary("Register a new user");

        group.MapPost("/login", async (LoginCommand command, ISender mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(command, ct);
            return result.ToHttpResult();
        })
        .WithSummary("Login and receive JWT tokens");

        group.MapPost("/refresh", async (RefreshTokenCommand command, ISender mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(command, ct);
            return result.ToHttpResult();
        })
        .WithSummary("Refresh an access token using a refresh token");
    }
}
