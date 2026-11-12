using AgentMemoryService.Shared.Core;
using AgentMemoryService.Users.Core.Entities;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace AgentMemoryService.Users.Application.Commands;

// ─────────────────────────────────────────────────────────────
// Shared response type (also used by queries)
// ─────────────────────────────────────────────────────────────
public sealed record UserDetailResponse(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string FullName,
    DateTime CreatedAt,
    bool IsActive);

// ─────────────────────────────────────────────────────────────
// Update User
// ─────────────────────────────────────────────────────────────
public sealed record UpdateUserCommand(Guid Id, Guid RequesterId, string FirstName, string LastName, string Email)
    : IRequest<Result<UserDetailResponse>>;

public sealed class UpdateUserCommandValidator : AbstractValidator<UpdateUserCommand>
{
    public UpdateUserCommandValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required")
            .MaximumLength(100);

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required")
            .MaximumLength(100);

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Invalid email format");
    }
}

public sealed class UpdateUserHandler : IRequestHandler<UpdateUserCommand, Result<UserDetailResponse>>
{
    private readonly UserManager<ApplicationUser> _userManager;

    public UpdateUserHandler(UserManager<ApplicationUser> userManager) => _userManager = userManager;

    public async Task<Result<UserDetailResponse>> Handle(UpdateUserCommand command, CancellationToken ct)
    {
        var user = await _userManager.FindByIdAsync(command.Id.ToString());
        if (user is null)
            return Result<UserDetailResponse>.Failure(
                Error.NotFound("User.NotFound", $"User '{command.Id}' not found"));

        user.FirstName = command.FirstName;
        user.LastName = command.LastName;
        user.Email = command.Email;
        user.UserName = command.Email;
        user.NormalizedEmail = command.Email.ToUpperInvariant();
        user.NormalizedUserName = command.Email.ToUpperInvariant();

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            return Result<UserDetailResponse>.Failure(
                Error.Validation("User.UpdateFailed", errors));
        }

        return Result<UserDetailResponse>.Success(
            new UserDetailResponse(user.Id, user.Email!, user.FirstName, user.LastName, user.FullName, user.CreatedAt, user.IsActive));
    }
}

// ─────────────────────────────────────────────────────────────
// Delete User (soft delete)
// ─────────────────────────────────────────────────────────────
public sealed record DeleteUserCommand(Guid Id, Guid RequesterId)
    : IRequest<Result<bool>>;

public sealed class DeleteUserHandler : IRequestHandler<DeleteUserCommand, Result<bool>>
{
    private readonly UserManager<ApplicationUser> _userManager;

    public DeleteUserHandler(UserManager<ApplicationUser> userManager) => _userManager = userManager;

    public async Task<Result<bool>> Handle(DeleteUserCommand command, CancellationToken ct)
    {
        if (command.Id == command.RequesterId)
            return Result<bool>.Failure(
                Error.Forbidden("User.CannotDeleteSelf", "You cannot delete your own account"));

        var user = await _userManager.FindByIdAsync(command.Id.ToString());
        if (user is null)
            return Result<bool>.Failure(
                Error.NotFound("User.NotFound", $"User '{command.Id}' not found"));

        user.IsActive = false;
        user.RefreshToken = null;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            return Result<bool>.Failure(
                Error.Validation("User.DeleteFailed", errors));
        }

        return Result<bool>.Success(true);
    }
}

// ─────────────────────────────────────────────────────────────
// Admin Reset Password
// ─────────────────────────────────────────────────────────────
public sealed record AdminResetPasswordCommand(Guid UserId, string NewPassword)
    : IRequest<Result<bool>>;

public sealed class AdminResetPasswordCommandValidator : AbstractValidator<AdminResetPasswordCommand>
{
    public AdminResetPasswordCommandValidator()
    {
        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("Password is required")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters")
            .Matches("[A-Z]").WithMessage("Password must contain an uppercase letter")
            .Matches("[a-z]").WithMessage("Password must contain a lowercase letter")
            .Matches("[0-9]").WithMessage("Password must contain a number");
    }
}

public sealed class AdminResetPasswordHandler : IRequestHandler<AdminResetPasswordCommand, Result<bool>>
{
    private readonly UserManager<ApplicationUser> _userManager;

    public AdminResetPasswordHandler(UserManager<ApplicationUser> userManager) => _userManager = userManager;

    public async Task<Result<bool>> Handle(AdminResetPasswordCommand command, CancellationToken ct)
    {
        var user = await _userManager.FindByIdAsync(command.UserId.ToString());
        if (user is null)
            return Result<bool>.Failure(
                Error.NotFound("User.NotFound", $"User '{command.UserId}' not found"));

        var removeResult = await _userManager.RemovePasswordAsync(user);
        if (!removeResult.Succeeded)
        {
            var errors = string.Join("; ", removeResult.Errors.Select(e => e.Description));
            return Result<bool>.Failure(
                Error.Validation("User.PasswordResetFailed", errors));
        }

        var addResult = await _userManager.AddPasswordAsync(user, command.NewPassword);
        if (!addResult.Succeeded)
        {
            var errors = string.Join("; ", addResult.Errors.Select(e => e.Description));
            return Result<bool>.Failure(
                Error.Validation("User.PasswordResetFailed", errors));
        }

        return Result<bool>.Success(true);
    }
}
