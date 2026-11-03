using AgentMemoryService.Shared.Core;
using Pgvector;

namespace AgentMemoryService.Memories.Core.Entities;

public sealed class Memory : AuditableEntity
{
    public Guid Id { get; private set; }
    public Guid NamespaceId { get; private set; }
    public Guid? AgentId { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public string Content { get; private set; } = string.Empty;
    public Vector? Embedding { get; private set; }
    public string? Source { get; private set; }
    public Dictionary<string, string> Metadata { get; private set; } = [];
    public MemoryType Type { get; private set; }
    public float? RelevanceScore { get; private set; }
    public DateTime? ExpiresAt { get; private set; }
    public bool IsActive { get; private set; } = true;
    public MemoryVisibility Visibility { get; private set; } = MemoryVisibility.Public;

    private Memory() { }

    public static Memory Create(
        Guid namespaceId,
        Guid createdByUserId,
        string content,
        MemoryType type = MemoryType.Fact,
        Guid? agentId = null,
        string? source = null,
        Dictionary<string, string>? metadata = null,
        DateTime? expiresAt = null,
        MemoryVisibility visibility = MemoryVisibility.Public) =>
        new()
        {
            Id = Guid.NewGuid(),
            NamespaceId = namespaceId,
            CreatedByUserId = createdByUserId,
            Content = content,
            Type = type,
            AgentId = agentId,
            Source = source,
            Metadata = metadata ?? [],
            ExpiresAt = expiresAt,
            Visibility = visibility
        };

    public void SetEmbedding(float[] embedding) => Embedding = new Vector(embedding);

    public void Deactivate() => IsActive = false;
}

public enum MemoryVisibility
{
    Public = 0,
    Private = 1
}

public enum MemoryType
{
    Fact,
    Preference,
    Instruction,
    Context,
    Observation,
    Summary
}
