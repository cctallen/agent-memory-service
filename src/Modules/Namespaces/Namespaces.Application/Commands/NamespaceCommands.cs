using AgentMemoryService.Shared.Core;
using AgentMemoryService.Shared.Infrastructure;
using AgentMemoryService.Namespaces.Core.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AgentMemoryService.Namespaces.Application.Commands;

// --- Create Namespace ---
public sealed record CreateNamespaceCommand(string Slug, string DisplayName, string? Description, bool IsPublic, Guid OwnerId)
    : IRequest<Result<NamespaceResponse>>;

public sealed record NamespaceResponse(Guid Id, string Slug, string DisplayName, string? Description, bool IsPublic, int MemberCount, DateTime CreatedAt);

public sealed class CreateNamespaceHandler : IRequestHandler<CreateNamespaceCommand, Result<NamespaceResponse>>
{
    private readonly AppDbContext _db;

    public CreateNamespaceHandler(AppDbContext db) => _db = db;

    public async Task<Result<NamespaceResponse>> Handle(CreateNamespaceCommand command, CancellationToken ct)
    {
        var exists = await _db.Namespaces.AnyAsync(n => n.Slug == command.Slug.ToLowerInvariant(), ct);
        if (exists)
            return Result<NamespaceResponse>.Failure(Error.Conflict("Namespace.SlugTaken", $"Slug '{command.Slug}' is already taken"));

        var ns = MemoryNamespace.Create(command.Slug, command.DisplayName, command.OwnerId, command.Description, command.IsPublic);
        _db.Namespaces.Add(ns);
        await _db.SaveChangesAsync(ct);

        return Result<NamespaceResponse>.Success(ns.ToResponse());
    }
}

// --- Delete Namespace ---
public sealed record DeleteNamespaceCommand(Guid NamespaceId, Guid RequesterId) : IRequest<Result<bool>>;

public sealed class DeleteNamespaceHandler : IRequestHandler<DeleteNamespaceCommand, Result<bool>>
{
    private readonly AppDbContext _db;

    public DeleteNamespaceHandler(AppDbContext db) => _db = db;

    public async Task<Result<bool>> Handle(DeleteNamespaceCommand command, CancellationToken ct)
    {
        var ns = await _db.Namespaces.FindAsync([command.NamespaceId], ct);
        if (ns is null) return Result<bool>.Failure(Error.NotFound("Namespace.NotFound", "Namespace not found"));
        if (ns.OwnerId != command.RequesterId) return Result<bool>.Failure(Error.Forbidden("Namespace.Forbidden", "Only the owner can delete a namespace"));

        _db.Namespaces.Remove(ns);
        await _db.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}

public static class NamespaceMappings
{
    public static NamespaceResponse ToResponse(this MemoryNamespace ns) =>
        new(ns.Id, ns.Slug, ns.DisplayName, ns.Description, ns.IsPublic, ns.Members.Count, ns.CreatedAt);
}
