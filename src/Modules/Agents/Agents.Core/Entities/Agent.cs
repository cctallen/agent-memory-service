using AgentMemoryService.Shared.Core;

namespace AgentMemoryService.Agents.Core.Entities;

public sealed class Agent : AuditableEntity
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string ApiKey { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;

    private readonly List<AgentNamespaceMembership> _namespaces = [];
    public IReadOnlyCollection<AgentNamespaceMembership> Namespaces => _namespaces.AsReadOnly();

    private Agent() { }

    public static Agent Create(string name, string? description = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            ApiKey = GenerateApiKey()
        };

    public void AddNamespace(Guid namespaceId)
    {
        if (_namespaces.Any(n => n.NamespaceId == namespaceId)) return;
        _namespaces.Add(new AgentNamespaceMembership(Id, namespaceId));
    }

    public bool RemoveNamespace(Guid namespaceId)
    {
        var m = _namespaces.FirstOrDefault(n => n.NamespaceId == namespaceId);
        if (m is null) return false;
        _namespaces.Remove(m);
        return true;
    }

    public void RotateApiKey() => ApiKey = GenerateApiKey();

    public void Deactivate() => IsActive = false;

    private static string GenerateApiKey() =>
        $"ams_{Convert.ToBase64String(Guid.NewGuid().ToByteArray()).Replace("=", "").Replace("+", "").Replace("/", "")}";
}

public sealed class AgentNamespaceMembership
{
    public Guid AgentId { get; private set; }
    public Guid NamespaceId { get; private set; }
    public DateTime JoinedAt { get; private set; }

    private AgentNamespaceMembership() { }

    public AgentNamespaceMembership(Guid agentId, Guid namespaceId)
    {
        AgentId = agentId;
        NamespaceId = namespaceId;
        JoinedAt = DateTime.UtcNow;
    }
}
