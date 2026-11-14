using AgentMemoryService.Shared.Core;

namespace AgentMemoryService.Namespaces.Core.Entities;

public sealed class MemoryNamespace : AuditableEntity
{
    public Guid Id { get; private set; }
    public string Slug { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public Guid OwnerId { get; private set; }
    public bool IsPublic { get; private set; }
    public Guid? OrganizationId { get; private set; }

    private readonly List<NamespaceMember> _members = [];
    public IReadOnlyCollection<NamespaceMember> Members => _members.AsReadOnly();

    private MemoryNamespace() { }

    public static MemoryNamespace Create(string slug, string displayName, Guid ownerId, string? description = null, bool isPublic = false) =>
        new()
        {
            Id = Guid.NewGuid(),
            Slug = slug.ToLowerInvariant(),
            DisplayName = displayName,
            OwnerId = ownerId,
            Description = description,
            IsPublic = isPublic
        };

    public void Update(string displayName, string? description, bool isPublic)
    {
        DisplayName = displayName;
        Description = description;
        IsPublic = isPublic;
    }

    public void AttachToOrg(Guid orgId) => OrganizationId = orgId;
    public void DetachFromOrg() => OrganizationId = null;

    public void AddMember(Guid userId, NamespaceRole role)
    {
        if (_members.Any(m => m.UserId == userId)) return;
        _members.Add(new NamespaceMember(Id, userId, role));
    }

    public bool RemoveMember(Guid userId)
    {
        var member = _members.FirstOrDefault(m => m.UserId == userId);
        if (member is null) return false;
        _members.Remove(member);
        return true;
    }

    public bool UpdateMemberRole(Guid userId, NamespaceRole role)
    {
        var member = _members.FirstOrDefault(m => m.UserId == userId);
        if (member is null) return false;
        member.UpdateRole(role);
        return true;
    }

    public bool HasAccess(Guid userId) =>
        IsPublic || OwnerId == userId || _members.Any(m => m.UserId == userId);

    public bool CanWrite(Guid userId) =>
        OwnerId == userId || _members.Any(m => m.UserId == userId && m.Role >= NamespaceRole.Writer);
}

public sealed class NamespaceMember
{
    public Guid NamespaceId { get; private set; }
    public Guid UserId { get; private set; }
    public NamespaceRole Role { get; private set; }
    public DateTime JoinedAt { get; private set; }

    private NamespaceMember() { }

    public NamespaceMember(Guid namespaceId, Guid userId, NamespaceRole role)
    {
        NamespaceId = namespaceId;
        UserId = userId;
        Role = role;
        JoinedAt = DateTime.UtcNow;
    }

    public void UpdateRole(NamespaceRole role) => Role = role;
}

public enum NamespaceRole { Reader = 1, Writer = 2, Admin = 3 }
