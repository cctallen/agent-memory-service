using AgentMemoryService.Shared.Core;
using AgentMemoryService.Shared.Infrastructure;
using AgentMemoryService.Shared.Infrastructure.Caching;
using AgentMemoryService.Memories.Core.Entities;
using AgentMemoryService.Namespaces.Core.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AgentMemoryService.Memories.Application.Commands;

// --- Add Memory ---
public sealed record AddMemoryCommand(
    Guid NamespaceId,
    Guid RequesterId,
    string Content,
    MemoryType Type = MemoryType.Fact,
    Guid? AgentId = null,
    string? Source = null,
    Dictionary<string, string>? Metadata = null,
    DateTime? ExpiresAt = null,
    MemoryVisibility Visibility = MemoryVisibility.Public,
    bool IsAgent = false)
    : IRequest<Result<MemoryResponse>>;

public sealed record MemoryResponse(
    Guid Id, Guid NamespaceId, string Content, MemoryType Type,
    Guid? AgentId, string? Source, Dictionary<string, string> Metadata,
    DateTime CreatedAt, DateTime? ExpiresAt, MemoryVisibility Visibility);

public sealed class AddMemoryHandler : IRequestHandler<AddMemoryCommand, Result<MemoryResponse>>
{
    private readonly AppDbContext _db;
    private readonly IMemoryCacheService _cache;

    public AddMemoryHandler(AppDbContext db, IMemoryCacheService cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<Result<MemoryResponse>> Handle(AddMemoryCommand command, CancellationToken ct)
    {
        var ns = await _db.Namespaces.Include(n => n.Members).FirstOrDefaultAsync(n => n.Id == command.NamespaceId, ct);
        if (ns is null) return Result<MemoryResponse>.Failure(Error.NotFound("Namespace.NotFound", "Namespace not found"));
        // Agents are pre-scoped to their namespace via API key — skip member check
        if (!command.IsAgent && !ns.CanWrite(command.RequesterId))
            return Result<MemoryResponse>.Failure(Error.Forbidden("Memory.Forbidden", "Write access required"));

        var memory = Memory.Create(
            command.NamespaceId, command.RequesterId, command.Content,
            command.Type, command.AgentId, command.Source, command.Metadata, command.ExpiresAt, command.Visibility);

        _db.Memories.Add(memory);
        await _db.SaveChangesAsync(ct);

        await _cache.RemoveByPrefixAsync($"memories:{command.NamespaceId}", ct);

        return Result<MemoryResponse>.Success(memory.ToResponse());
    }
}

// --- Delete Memory ---
public sealed record DeleteMemoryCommand(Guid MemoryId, Guid RequesterId, bool IsAgent = false) : IRequest<Result<bool>>;

public sealed class DeleteMemoryHandler : IRequestHandler<DeleteMemoryCommand, Result<bool>>
{
    private readonly AppDbContext _db;
    private readonly IMemoryCacheService _cache;

    public DeleteMemoryHandler(AppDbContext db, IMemoryCacheService cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<Result<bool>> Handle(DeleteMemoryCommand command, CancellationToken ct)
    {
        var memory = await _db.Memories.FindAsync([command.MemoryId], ct);
        if (memory is null) return Result<bool>.Failure(Error.NotFound("Memory.NotFound", "Memory not found"));

        // Allow deletion if: (1) requester created it, (2) record has no owner (orphaned),
        // (3) requester is namespace owner/admin, or (4) request is from an agent in this namespace
        var isOwner = memory.CreatedByUserId == command.RequesterId;
        var isOrphaned = memory.CreatedByUserId == null;

        if (!isOwner && !isOrphaned && !command.IsAgent)
        {
            var ns = await _db.Namespaces.Include(n => n.Members)
                .FirstOrDefaultAsync(n => n.Id == memory.NamespaceId, ct);
            var isNamespaceAdmin = ns is not null &&
                (ns.OwnerId == command.RequesterId ||
                 ns.Members.Any(m => m.UserId == command.RequesterId && m.Role >= NamespaceRole.Admin));
            if (!isNamespaceAdmin)
                return Result<bool>.Failure(Error.Forbidden("Memory.Forbidden", "Cannot delete another user's memory"));
        }

        memory.Deactivate();
        await _db.SaveChangesAsync(ct);

        await _cache.RemoveByPrefixAsync($"memories:{memory.NamespaceId}", ct);

        return Result<bool>.Success(true);
    }
}

public static class MemoryMappings
{
    public static MemoryResponse ToResponse(this Memory m) =>
        new(m.Id, m.NamespaceId, m.Content, m.Type, m.AgentId, m.Source, m.Metadata, m.CreatedAt, m.ExpiresAt, m.Visibility);
}
