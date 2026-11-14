using AgentMemoryService.Shared.Core;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Identity;
using AgentMemoryService.Users.Core.Entities;

namespace AgentMemoryService.Users.Application.Commands;

// ─────────────────────────────────────────────────────────────
// Shared response types
// ─────────────────────────────────────────────────────────────
public sealed record AuthTokenResponse(string AccessToken, string RefreshToken, DateTime ExpiresAt);

// ─────────────────────────────────────────────────────────────
// Register
// ─────────────────────────────────────────────────────────────
public sealed record RegisterCommand(string Email, string Password, string FirstName, string LastName)
    : IRequest<Result<RegisterResponse>>;

public sealed record RegisterResponse(Guid UserId, string Email, string FullName);

public sealed class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Invalid email format")
            .MaximumLength(255);

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8).WithMessage("Password must be at least 8 characters")
            .Matches("[A-Z]").WithMessage("Password must contain an uppercase letter")
            .Matches("[a-z]").WithMessage("Password must contain a lowercase letter")
            .Matches("[0-9]").WithMessage("Password must contain a number");

        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required")
            .MaximumLength(100);

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required")
            .MaximumLength(100);
    }
}

public sealed class RegisterHandler : IRequestHandler<RegisterCommand, Result<RegisterResponse>>
{
    private readonly UserManager<ApplicationUser> _userManager;

    public RegisterHandler(UserManager<ApplicationUser> userManager) => _userManager = userManager;

    public async Task<Result<RegisterResponse>> Handle(RegisterCommand command, CancellationToken ct)
    {
        var existing = await _userManager.FindByEmailAsync(command.Email);
        if (existing is not null)
            return Result<RegisterResponse>.Failure(
                Error.Conflict("User.DuplicateEmail", "Email already registered"));

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = command.Email,
            UserName = command.Email,
            FirstName = command.FirstName,
            LastName = command.LastName
        };

        var identityResult = await _userManager.CreateAsync(user, command.Password);
        if (!identityResult.Succeeded)
        {
            var errors = string.Join("; ", identityResult.Errors.Select(e => e.Description));
            return Result<RegisterResponse>.Failure(
                Error.Validation("User.CreateFailed", errors));
        }

        return Result<RegisterResponse>.Success(
            new RegisterResponse(user.Id, user.Email!, user.FullName));
    }
}

// ─────────────────────────────────────────────────────────────
// Login
// ─────────────────────────────────────────────────────────────
public sealed record LoginCommand(string Email, string Password)
    : IRequest<Result<AuthTokenResponse>>;

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Invalid email format");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required");
    }
}

// ─────────────────────────────────────────────────────────────
// Assign Role
// ─────────────────────────────────────────────────────────────
public sealed record AssignRoleCommand(Guid TargetUserId, Guid RequesterId, string Role)
    : IRequest<Result<bool>>;

public sealed class AssignRoleCommandValidator : AbstractValidator<AssignRoleCommand>
{
    public AssignRoleCommandValidator()
    {
        RuleFor(x => x.Role).NotEmpty().WithMessage("Role is required");
    }
}

public sealed class AssignRoleHandler : IRequestHandler<AssignRoleCommand, Result<bool>>
{
    private readonly UserManager<ApplicationUser> _userManager;

    public AssignRoleHandler(UserManager<ApplicationUser> userManager) => _userManager = userManager;

    public async Task<Result<bool>> Handle(AssignRoleCommand command, CancellationToken ct)
    {
        if (command.TargetUserId == command.RequesterId)
            return Result<bool>.Failure(Error.Validation("Role.SelfModify", "Cannot modify your own role"));

        var user = await _userManager.FindByIdAsync(command.TargetUserId.ToString());
        if (user is null)
            return Result<bool>.Failure(Error.NotFound("User.NotFound", "User not found"));

        if (await _userManager.IsInRoleAsync(user, command.Role))
            return Result<bool>.Success(true); // already has the role — idempotent

        var result = await _userManager.AddToRoleAsync(user, command.Role);
        if (!result.Succeeded)
            return Result<bool>.Failure(Error.Validation("Role.AssignFailed",
                string.Join("; ", result.Errors.Select(e => e.Description))));

        return Result<bool>.Success(true);
    }
}

// ─────────────────────────────────────────────────────────────
// Remove Role
// ─────────────────────────────────────────────────────────────
public sealed record RemoveRoleCommand(Guid TargetUserId, Guid RequesterId, string Role)
    : IRequest<Result<bool>>;

public sealed class RemoveRoleHandler : IRequestHandler<RemoveRoleCommand, Result<bool>>
{
    private readonly UserManager<ApplicationUser> _userManager;

    public RemoveRoleHandler(UserManager<ApplicationUser> userManager) => _userManager = userManager;

    public async Task<Result<bool>> Handle(RemoveRoleCommand command, CancellationToken ct)
    {
        if (command.TargetUserId == command.RequesterId)
            return Result<bool>.Failure(Error.Validation("Role.SelfModify", "Cannot modify your own role"));

        var user = await _userManager.FindByIdAsync(command.TargetUserId.ToString());
        if (user is null)
            return Result<bool>.Failure(Error.NotFound("User.NotFound", "User not found"));

        if (!await _userManager.IsInRoleAsync(user, command.Role))
            return Result<bool>.Success(true); // already doesn't have the role — idempotent

        var result = await _userManager.RemoveFromRoleAsync(user, command.Role);
        if (!result.Succeeded)
            return Result<bool>.Failure(Error.Validation("Role.RemoveFailed",
                string.Join("; ", result.Errors.Select(e => e.Description))));

        return Result<bool>.Success(true);
    }
}

// ─────────────────────────────────────────────────────────────
// Refresh Token
// ─────────────────────────────────────────────────────────────
public sealed record RefreshTokenCommand(string RefreshToken)
    : IRequest<Result<AuthTokenResponse>>;

public sealed class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty().WithMessage("Refresh token is required");
    }
}
