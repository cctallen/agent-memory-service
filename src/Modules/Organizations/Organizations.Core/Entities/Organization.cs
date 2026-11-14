using AgentMemoryService.Shared.Core;

namespace AgentMemoryService.Organizations.Core.Entities;

public enum OrgRole { Member = 1, Admin = 2, Owner = 3 }

public sealed class Organization : AuditableEntity
{
    public Guid Id { get; private set; }
    public string Slug { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public Guid OwnerId { get; private set; }

    private readonly List<OrganizationMember> _members = [];
    public IReadOnlyCollection<OrganizationMember> Members => _members.AsReadOnly();

    private Organization() { }

    public static Organization Create(string slug, string displayName, Guid ownerId, string? description = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            Slug = slug.ToLowerInvariant().Trim(),
            DisplayName = displayName.Trim(),
            OwnerId = ownerId,
            Description = description
        };

    public void Update(string displayName, string? description)
    {
        DisplayName = displayName.Trim();
        Description = description;
    }

    public void AddMember(Guid userId, OrgRole role)
    {
        if (_members.Any(m => m.UserId == userId)) return;
        _members.Add(new OrganizationMember(Id, userId, role));
    }

    public bool RemoveMember(Guid userId)
    {
        var m = _members.FirstOrDefault(x => x.UserId == userId);
        if (m is null) return false;
        _members.Remove(m);
        return true;
    }

    public bool UpdateMemberRole(Guid userId, OrgRole role)
    {
        var m = _members.FirstOrDefault(x => x.UserId == userId);
        if (m is null) return false;
        m.UpdateRole(role);
        return true;
    }

    public bool HasMember(Guid userId) =>
        OwnerId == userId || _members.Any(m => m.UserId == userId);

    public bool IsAdminOrOwner(Guid userId) =>
        OwnerId == userId || _members.Any(m => m.UserId == userId && m.Role >= OrgRole.Admin);
}

public sealed class OrganizationMember
{
    public Guid OrganizationId { get; private set; }
    public Guid UserId { get; private set; }
    public OrgRole Role { get; private set; }
    public DateTime JoinedAt { get; private set; }

    private OrganizationMember() { }

    public OrganizationMember(Guid organizationId, Guid userId, OrgRole role)
    {
        OrganizationId = organizationId;
        UserId = userId;
        Role = role;
        JoinedAt = DateTime.UtcNow;
    }

    public void UpdateRole(OrgRole role) => Role = role;
}
