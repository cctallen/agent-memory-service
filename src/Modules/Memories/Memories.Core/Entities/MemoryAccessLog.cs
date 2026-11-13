namespace AgentMemoryService.Memories.Core.Entities;

public sealed class MemoryAccessLog
{
    public Guid Id { get; private set; }
    public Guid NamespaceId { get; private set; }
    public Guid? MemoryId { get; private set; }   // null = list/search operation
    public Guid AgentId { get; private set; }
    public string AgentName { get; private set; } = string.Empty;
    public AccessOperation Operation { get; private set; }
    public string? Query { get; private set; }     // populated for Search operations
    public int RecordCount { get; private set; }   // how many memories returned
    public DateTime AccessedAt { get; private set; }

    private MemoryAccessLog() { }

    public static MemoryAccessLog Create(
        Guid namespaceId,
        Guid agentId,
        string agentName,
        AccessOperation operation,
        Guid? memoryId = null,
        string? query = null,
        int recordCount = 1) =>
        new()
        {
            Id = Guid.NewGuid(),
            NamespaceId = namespaceId,
            MemoryId = memoryId,
            AgentId = agentId,
            AgentName = agentName,
            Operation = operation,
            Query = query,
            RecordCount = recordCount,
            AccessedAt = DateTime.UtcNow,
        };
}

public enum AccessOperation
{
    Get = 0,
    List = 1,
    Search = 2,
}
