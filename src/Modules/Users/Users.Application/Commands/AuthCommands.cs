using AgentMemoryService.Shared.Core;
using MediatR;
using Microsoft.AspNetCore.Identity;
using AgentMemoryService.Users.Core.Entities;

namespace AgentMemoryService.Users.Application.Commands;

// --- Register ---
public sealed record RegisterCommand(string Email, string Password, string FirstName, string LastName)
    : IRequest<Result<RegisterResponse>>;

public sealed record RegisterResponse(Guid UserId, string Email, string FullName);

public sealed class RegisterHandler : IRequestHandler<RegisterCommand, Result<RegisterResponse>>
{
    private readonly UserManager<ApplicationUser> _userManager;

    public RegisterHandler(UserManager<ApplicationUser> userManager) => _userManager = userManager;

    public async Task<Result<RegisterResponse>> Handle(RegisterCommand command, CancellationToken ct)
    {
        var existing = await _userManager.FindByEmailAsync(command.Email);
        if (existing is not null)
            return Result<RegisterResponse>.Failure(Error.Conflict("User.DuplicateEmail", "Email already registered"));

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = command.Email,
            UserName = command.Email,
            FirstName = command.FirstName,
            LastName = command.LastName
        };

        var result = await _userManager.CreateAsync(user, command.Password);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            return Result<RegisterResponse>.Failure(Error.Validation("User.CreateFailed", errors));
        }

        return Result<RegisterResponse>.Success(new RegisterResponse(user.Id, user.Email!, user.FullName));
    }
}

// --- Login ---
public sealed record LoginCommand(string Email, string Password) : IRequest<Result<LoginResponse>>;

public sealed record LoginResponse(string AccessToken, string RefreshToken, DateTime ExpiresAt);
