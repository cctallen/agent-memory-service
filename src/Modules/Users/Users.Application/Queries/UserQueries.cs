using AgentMemoryService.Shared.Core;
using AgentMemoryService.Users.Core.Entities;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AgentMemoryService.Users.Application.Queries;

public sealed record UserSummary(Guid Id, string Email, string FullName);

// ─────────────────────────────────────────────────────────────
// Find user by email
// ─────────────────────────────────────────────────────────────
public sealed record FindUserByEmailQuery(string Email) : IRequest<Result<UserSummary>>;

public sealed class FindUserByEmailHandler : IRequestHandler<FindUserByEmailQuery, Result<UserSummary>>
{
    private readonly UserManager<ApplicationUser> _userManager;

    public FindUserByEmailHandler(UserManager<ApplicationUser> userManager)
        => _userManager = userManager;

    public async Task<Result<UserSummary>> Handle(FindUserByEmailQuery query, CancellationToken ct)
    {
        var user = await _userManager.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.NormalizedEmail == query.Email.ToUpperInvariant(), ct);

        if (user is null)
            return Result<UserSummary>.Failure(Error.NotFound("User.NotFound", $"No user found with email '{query.Email}'"));

        return Result<UserSummary>.Success(new UserSummary(user.Id, user.Email!, user.FullName));
    }
}
