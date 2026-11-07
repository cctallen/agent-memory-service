namespace AgentMemoryService.Mcp.Core;

/// <summary>Carries namespace ID through DI to tool classes.</summary>
public sealed record MindVaultConfig(string NamespaceId);
