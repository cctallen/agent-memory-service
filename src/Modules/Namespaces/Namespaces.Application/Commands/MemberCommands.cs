using AgentMemoryService.Namespaces.Core.Entities;
using AgentMemoryService.Shared.Core;
using AgentMemoryService.Shared.Infrastructure;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AgentMemoryService.Namespaces.Application.Commands;

// ─────────────────────────────────────────────────────────────
// Add Member
// ─────────────────────────────────────────────────────────────
public sealed record AddMemberCommand(Guid NamespaceId, Guid RequesterId, Guid UserId, NamespaceRole Role)
    : IRequest<Result<bool>>;

public sealed class AddMemberCommandValidator : AbstractValidator<AddMemberCommand>
{
    public AddMemberCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().WithMessage("UserId is required");
        RuleFor(x => x.Role).IsInEnum().WithMessage("Invalid role");
    }
}

public sealed class AddMemberHandler : IRequestHandler<AddMemberCommand, Result<bool>>
{
    private readonly AppDbContext _db;

    public AddMemberHandler(AppDbContext db) => _db = db;

    public async Task<Result<bool>> Handle(AddMemberCommand command, CancellationToken ct)
    {
        var ns = await _db.Namespaces
            .Include(n => n.Members)
            .FirstOrDefaultAsync(n => n.Id == command.NamespaceId, ct);

        if (ns is null)
            return Result<bool>.Failure(Error.NotFound("Namespace.NotFound", "Namespace not found"));

        if (ns.OwnerId != command.RequesterId && !ns.Members.Any(m => m.UserId == command.RequesterId && m.Role >= NamespaceRole.Admin))
            return Result<bool>.Failure(Error.Forbidden("Namespace.Forbidden", "Admin access required to manage members"));

        if (command.UserId == ns.OwnerId)
            return Result<bool>.Failure(Error.Conflict("Namespace.OwnerConflict", "Cannot add the owner as a member"));

        ns.AddMember(command.UserId, command.Role);
        await _db.SaveChangesAsync(ct);

        return Result<bool>.Success(true);
    }
}

// ─────────────────────────────────────────────────────────────
// Remove Member
// ─────────────────────────────────────────────────────────────
public sealed record RemoveMemberCommand(Guid NamespaceId, Guid RequesterId, Guid UserId)
    : IRequest<Result<bool>>;

public sealed class RemoveMemberHandler : IRequestHandler<RemoveMemberCommand, Result<bool>>
{
    private readonly AppDbContext _db;

    public RemoveMemberHandler(AppDbContext db) => _db = db;

    public async Task<Result<bool>> Handle(RemoveMemberCommand command, CancellationToken ct)
    {
        var ns = await _db.Namespaces
            .Include(n => n.Members)
            .FirstOrDefaultAsync(n => n.Id == command.NamespaceId, ct);

        if (ns is null)
            return Result<bool>.Failure(Error.NotFound("Namespace.NotFound", "Namespace not found"));

        // Owner can remove anyone; admin can remove readers/writers; members can remove themselves
        var isOwner = ns.OwnerId == command.RequesterId;
        var isAdmin = ns.Members.Any(m => m.UserId == command.RequesterId && m.Role >= NamespaceRole.Admin);
        var isSelf = command.RequesterId == command.UserId;

        if (!isOwner && !isAdmin && !isSelf)
            return Result<bool>.Failure(Error.Forbidden("Namespace.Forbidden", "Insufficient permissions to remove this member"));

        var removed = ns.RemoveMember(command.UserId);
        if (!removed)
            return Result<bool>.Failure(Error.NotFound("Member.NotFound", "Member not found in this namespace"));

        await _db.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}

// ─────────────────────────────────────────────────────────────
// Update Member Role
// ─────────────────────────────────────────────────────────────
public sealed record UpdateMemberRoleCommand(Guid NamespaceId, Guid RequesterId, Guid UserId, NamespaceRole Role)
    : IRequest<Result<bool>>;

public sealed class UpdateMemberRoleHandler : IRequestHandler<UpdateMemberRoleCommand, Result<bool>>
{
    private readonly AppDbContext _db;

    public UpdateMemberRoleHandler(AppDbContext db) => _db = db;

    public async Task<Result<bool>> Handle(UpdateMemberRoleCommand command, CancellationToken ct)
    {
        var ns = await _db.Namespaces
            .Include(n => n.Members)
            .FirstOrDefaultAsync(n => n.Id == command.NamespaceId, ct);

        if (ns is null)
            return Result<bool>.Failure(Error.NotFound("Namespace.NotFound", "Namespace not found"));

        if (ns.OwnerId != command.RequesterId && !ns.Members.Any(m => m.UserId == command.RequesterId && m.Role >= NamespaceRole.Admin))
            return Result<bool>.Failure(Error.Forbidden("Namespace.Forbidden", "Admin access required to change roles"));

        var updated = ns.UpdateMemberRole(command.UserId, command.Role);
        if (!updated)
            return Result<bool>.Failure(Error.NotFound("Member.NotFound", "Member not found in this namespace"));

        await _db.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}
