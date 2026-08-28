using AgentMemoryService.Shared.Core;

namespace AgentMemoryService.Agents.Core.Entities;

public sealed class Agent : AuditableEntity
{
    public Guid Id { get; private set; }
    public Guid NamespaceId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string ApiKey { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;

    private Agent() { }

    public static Agent Create(Guid namespaceId, string name, string? description = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            NamespaceId = namespaceId,
            Name = name,
            Description = description,
            ApiKey = GenerateApiKey()
        };

    public void RotateApiKey() => ApiKey = GenerateApiKey();

    public void Deactivate() => IsActive = false;

    private static string GenerateApiKey() =>
        $"ams_{Convert.ToBase64String(Guid.NewGuid().ToByteArray()).Replace("=", "").Replace("+", "").Replace("/", "")}";
}
