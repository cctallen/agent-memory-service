using AgentMemoryService.Shared.Core;
using AgentMemoryService.Users.Application.Commands;
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

// ─────────────────────────────────────────────────────────────
// Get user by ID
// ─────────────────────────────────────────────────────────────
public sealed record GetUserByIdQuery(Guid Id) : IRequest<Result<UserDetailResponse>>;

public sealed class GetUserByIdHandler : IRequestHandler<GetUserByIdQuery, Result<UserDetailResponse>>
{
    private readonly UserManager<ApplicationUser> _userManager;

    public GetUserByIdHandler(UserManager<ApplicationUser> userManager)
        => _userManager = userManager;

    public async Task<Result<UserDetailResponse>> Handle(GetUserByIdQuery query, CancellationToken ct)
    {
        var user = await _userManager.FindByIdAsync(query.Id.ToString());

        if (user is null)
            return Result<UserDetailResponse>.Failure(
                Error.NotFound("User.NotFound", $"User '{query.Id}' not found"));

        return Result<UserDetailResponse>.Success(
            new UserDetailResponse(user.Id, user.Email!, user.FirstName, user.LastName, user.FullName, user.CreatedAt, user.IsActive));
    }
}
