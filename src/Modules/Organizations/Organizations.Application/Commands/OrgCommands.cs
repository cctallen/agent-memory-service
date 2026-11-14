using AgentMemoryService.Organizations.Core.Entities;
using AgentMemoryService.Shared.Core;
using AgentMemoryService.Shared.Infrastructure;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AgentMemoryService.Organizations.Application.Commands;

// ─── Shared response ──────────────────────────────────────────────────────────
public sealed record OrgResponse(
    Guid Id,
    string Slug,
    string DisplayName,
    string? Description,
    Guid OwnerId,
    int MemberCount,
    DateTime CreatedAt);

public static class OrgMappings
{
    public static OrgResponse ToResponse(this Organization o) =>
        new(o.Id, o.Slug, o.DisplayName, o.Description, o.OwnerId, o.Members.Count, o.CreatedAt);
}

// ─── Create Organization ──────────────────────────────────────────────────────
public sealed record CreateOrgCommand(string Slug, string DisplayName, string? Description, Guid RequesterId)
    : IRequest<Result<OrgResponse>>;

public sealed class CreateOrgCommandValidator : AbstractValidator<CreateOrgCommand>
{
    public CreateOrgCommandValidator()
    {
        RuleFor(x => x.Slug)
            .NotEmpty().WithMessage("Slug is required")
            .MaximumLength(60).WithMessage("Slug must be 60 characters or fewer")
            .Matches("^[a-z0-9-]+$").WithMessage("Slug may only contain lowercase letters, numbers, and hyphens");
        RuleFor(x => x.DisplayName)
            .NotEmpty().WithMessage("Display name is required")
            .MaximumLength(100);
    }
}

public sealed class CreateOrgHandler : IRequestHandler<CreateOrgCommand, Result<OrgResponse>>
{
    private readonly AppDbContext _db;

    public CreateOrgHandler(AppDbContext db) => _db = db;

    public async Task<Result<OrgResponse>> Handle(CreateOrgCommand command, CancellationToken ct)
    {
        var exists = await _db.Organizations.AnyAsync(o => o.Slug == command.Slug.ToLowerInvariant(), ct);
        if (exists)
            return Result<OrgResponse>.Failure(Error.Conflict("Org.SlugTaken", $"Slug '{command.Slug}' is already taken"));

        var org = Organization.Create(command.Slug, command.DisplayName, command.RequesterId, command.Description);
        // Owner is automatically a member with Owner role
        org.AddMember(command.RequesterId, OrgRole.Owner);
        _db.Organizations.Add(org);
        await _db.SaveChangesAsync(ct);

        return Result<OrgResponse>.Success(org.ToResponse());
    }
}

// ─── Update Organization ──────────────────────────────────────────────────────
public sealed record UpdateOrgCommand(Guid OrgId, Guid RequesterId, string DisplayName, string? Description)
    : IRequest<Result<OrgResponse>>;

public sealed class UpdateOrgHandler : IRequestHandler<UpdateOrgCommand, Result<OrgResponse>>
{
    private readonly AppDbContext _db;

    public UpdateOrgHandler(AppDbContext db) => _db = db;

    public async Task<Result<OrgResponse>> Handle(UpdateOrgCommand command, CancellationToken ct)
    {
        var org = await _db.Organizations.Include(o => o.Members)
            .FirstOrDefaultAsync(o => o.Id == command.OrgId, ct);
        if (org is null)
            return Result<OrgResponse>.Failure(Error.NotFound("Org.NotFound", "Organization not found"));
        if (!org.IsAdminOrOwner(command.RequesterId))
            return Result<OrgResponse>.Failure(Error.Forbidden("Org.Forbidden", "Admin access required"));

        org.Update(command.DisplayName, command.Description);
        await _db.SaveChangesAsync(ct);
        return Result<OrgResponse>.Success(org.ToResponse());
    }
}

// ─── Delete Organization ──────────────────────────────────────────────────────
public sealed record DeleteOrgCommand(Guid OrgId, Guid RequesterId) : IRequest<Result<bool>>;

public sealed class DeleteOrgHandler : IRequestHandler<DeleteOrgCommand, Result<bool>>
{
    private readonly AppDbContext _db;

    public DeleteOrgHandler(AppDbContext db) => _db = db;

    public async Task<Result<bool>> Handle(DeleteOrgCommand command, CancellationToken ct)
    {
        var org = await _db.Organizations.Include(o => o.Members)
            .FirstOrDefaultAsync(o => o.Id == command.OrgId, ct);
        if (org is null)
            return Result<bool>.Failure(Error.NotFound("Org.NotFound", "Organization not found"));
        if (org.OwnerId != command.RequesterId)
            return Result<bool>.Failure(Error.Forbidden("Org.Forbidden", "Only the owner can delete an organization"));

        // Detach all namespaces from this org before deleting
        var namespaces = await _db.Namespaces
            .Where(n => n.OrganizationId == command.OrgId)
            .ToListAsync(ct);
        foreach (var ns in namespaces)
            ns.DetachFromOrg();

        _db.Organizations.Remove(org);
        await _db.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}

// ─── Add Member ───────────────────────────────────────────────────────────────
public sealed record AddOrgMemberCommand(Guid OrgId, Guid RequesterId, Guid UserId, OrgRole Role)
    : IRequest<Result<bool>>;

public sealed class AddOrgMemberHandler : IRequestHandler<AddOrgMemberCommand, Result<bool>>
{
    private readonly AppDbContext _db;

    public AddOrgMemberHandler(AppDbContext db) => _db = db;

    public async Task<Result<bool>> Handle(AddOrgMemberCommand command, CancellationToken ct)
    {
        var org = await _db.Organizations.Include(o => o.Members)
            .FirstOrDefaultAsync(o => o.Id == command.OrgId, ct);
        if (org is null)
            return Result<bool>.Failure(Error.NotFound("Org.NotFound", "Organization not found"));
        if (!org.IsAdminOrOwner(command.RequesterId))
            return Result<bool>.Failure(Error.Forbidden("Org.Forbidden", "Admin access required to manage members"));
        if (command.Role == OrgRole.Owner)
            return Result<bool>.Failure(Error.Validation("Org.InvalidRole", "Cannot assign Owner role; transfer ownership instead"));

        org.AddMember(command.UserId, command.Role);
        await _db.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}

// ─── Remove Member ────────────────────────────────────────────────────────────
public sealed record RemoveOrgMemberCommand(Guid OrgId, Guid RequesterId, Guid UserId)
    : IRequest<Result<bool>>;

public sealed class RemoveOrgMemberHandler : IRequestHandler<RemoveOrgMemberCommand, Result<bool>>
{
    private readonly AppDbContext _db;

    public RemoveOrgMemberHandler(AppDbContext db) => _db = db;

    public async Task<Result<bool>> Handle(RemoveOrgMemberCommand command, CancellationToken ct)
    {
        var org = await _db.Organizations.Include(o => o.Members)
            .FirstOrDefaultAsync(o => o.Id == command.OrgId, ct);
        if (org is null)
            return Result<bool>.Failure(Error.NotFound("Org.NotFound", "Organization not found"));
        if (command.UserId == org.OwnerId)
            return Result<bool>.Failure(Error.Validation("Org.OwnerRemoval", "Cannot remove the owner from an organization"));

        var isSelf = command.RequesterId == command.UserId;
        if (!isSelf && !org.IsAdminOrOwner(command.RequesterId))
            return Result<bool>.Failure(Error.Forbidden("Org.Forbidden", "Admin access required to remove members"));

        var removed = org.RemoveMember(command.UserId);
        if (!removed)
            return Result<bool>.Failure(Error.NotFound("Member.NotFound", "Member not found in this organization"));

        await _db.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}

// ─── Update Member Role ───────────────────────────────────────────────────────
public sealed record UpdateOrgMemberRoleCommand(Guid OrgId, Guid RequesterId, Guid UserId, OrgRole Role)
    : IRequest<Result<bool>>;

public sealed class UpdateOrgMemberRoleHandler : IRequestHandler<UpdateOrgMemberRoleCommand, Result<bool>>
{
    private readonly AppDbContext _db;

    public UpdateOrgMemberRoleHandler(AppDbContext db) => _db = db;

    public async Task<Result<bool>> Handle(UpdateOrgMemberRoleCommand command, CancellationToken ct)
    {
        var org = await _db.Organizations.Include(o => o.Members)
            .FirstOrDefaultAsync(o => o.Id == command.OrgId, ct);
        if (org is null)
            return Result<bool>.Failure(Error.NotFound("Org.NotFound", "Organization not found"));
        if (!org.IsAdminOrOwner(command.RequesterId))
            return Result<bool>.Failure(Error.Forbidden("Org.Forbidden", "Admin access required to change roles"));
        if (command.Role == OrgRole.Owner)
            return Result<bool>.Failure(Error.Validation("Org.InvalidRole", "Cannot assign Owner role; transfer ownership instead"));
        if (command.UserId == org.OwnerId)
            return Result<bool>.Failure(Error.Validation("Org.OwnerRole", "Cannot change the owner's role"));

        var updated = org.UpdateMemberRole(command.UserId, command.Role);
        if (!updated)
            return Result<bool>.Failure(Error.NotFound("Member.NotFound", "Member not found in this organization"));

        await _db.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}

// ─── Attach Namespace to Org ──────────────────────────────────────────────────
public sealed record AttachNamespaceToOrgCommand(Guid OrgId, Guid NamespaceId, Guid RequesterId)
    : IRequest<Result<bool>>;

public sealed class AttachNamespaceToOrgHandler : IRequestHandler<AttachNamespaceToOrgCommand, Result<bool>>
{
    private readonly AppDbContext _db;

    public AttachNamespaceToOrgHandler(AppDbContext db) => _db = db;

    public async Task<Result<bool>> Handle(AttachNamespaceToOrgCommand command, CancellationToken ct)
    {
        var org = await _db.Organizations.Include(o => o.Members)
            .FirstOrDefaultAsync(o => o.Id == command.OrgId, ct);
        if (org is null)
            return Result<bool>.Failure(Error.NotFound("Org.NotFound", "Organization not found"));
        if (!org.IsAdminOrOwner(command.RequesterId))
            return Result<bool>.Failure(Error.Forbidden("Org.Forbidden", "Admin access required"));

        var ns = await _db.Namespaces
            .FirstOrDefaultAsync(n => n.Id == command.NamespaceId, ct);
        if (ns is null)
            return Result<bool>.Failure(Error.NotFound("Namespace.NotFound", "Namespace not found"));
        if (ns.OwnerId != command.RequesterId && !org.IsAdminOrOwner(command.RequesterId))
            return Result<bool>.Failure(Error.Forbidden("Namespace.Forbidden", "Must own the namespace to attach it"));

        ns.AttachToOrg(command.OrgId);
        await _db.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}

// ─── Detach Namespace from Org ────────────────────────────────────────────────
public sealed record DetachNamespaceFromOrgCommand(Guid OrgId, Guid NamespaceId, Guid RequesterId)
    : IRequest<Result<bool>>;

public sealed class DetachNamespaceFromOrgHandler : IRequestHandler<DetachNamespaceFromOrgCommand, Result<bool>>
{
    private readonly AppDbContext _db;

    public DetachNamespaceFromOrgHandler(AppDbContext db) => _db = db;

    public async Task<Result<bool>> Handle(DetachNamespaceFromOrgCommand command, CancellationToken ct)
    {
        var org = await _db.Organizations.Include(o => o.Members)
            .FirstOrDefaultAsync(o => o.Id == command.OrgId, ct);
        if (org is null)
            return Result<bool>.Failure(Error.NotFound("Org.NotFound", "Organization not found"));
        if (!org.IsAdminOrOwner(command.RequesterId))
            return Result<bool>.Failure(Error.Forbidden("Org.Forbidden", "Admin access required"));

        var ns = await _db.Namespaces
            .FirstOrDefaultAsync(n => n.Id == command.NamespaceId && n.OrganizationId == command.OrgId, ct);
        if (ns is null)
            return Result<bool>.Failure(Error.NotFound("Namespace.NotFound", "Namespace not found in this organization"));

        ns.DetachFromOrg();
        await _db.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}
