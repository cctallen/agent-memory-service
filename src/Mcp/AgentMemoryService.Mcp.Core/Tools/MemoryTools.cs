using System.ComponentModel;
using ModelContextProtocol.Server;

namespace AgentMemoryService.Mcp.Core.Tools;

[McpServerToolType]
public sealed class MemoryTools
{
    private readonly MindVaultClient _client;
    private readonly MindVaultConfig _config;

    public MemoryTools(MindVaultClient client, MindVaultConfig config)
    {
        _client = client;
        _config = config;
    }

    [McpServerTool]
    [Description(
        "Save something to MindVault memory. Use this proactively whenever the user shares a preference, " +
        "makes a decision, gives an instruction, or reveals a fact worth remembering across conversations.")]
    public async Task<string> Remember(
        [Description("The content to remember. Be specific and self-contained — this will be read back without context.")] string content,
        [Description("Memory type: Fact (default), Preference, Instruction, Context, or Summary.")] string type = "Fact",
        [Description("Optional: where this came from, e.g. 'user', 'conversation', 'document'.")] string? source = null,
        CancellationToken ct = default)
    {
        var memory = await _client.AddMemoryAsync(_config.NamespaceId, new AddMemoryRequest(
            Content: content,
            Type: MemoryType.Parse(type),
            Source: source ?? "claude-desktop"), ct);

        return $"Saved ({memory.Id})";
    }

    [McpServerTool]
    [Description(
        "Search MindVault memory semantically. Use this before answering questions about past conversations, " +
        "user preferences, or anything the user may have told you before.")]
    public async Task<string> SearchMemories(
        [Description("What to search for. Use natural language — this is a semantic search.")] string query,
        [Description("Max number of results to return (default 10).")] int topK = 10,
        CancellationToken ct = default)
    {
        var results = await _client.SearchMemoriesAsync(_config.NamespaceId, query, topK, ct);

        if (results.Count == 0)
            return "No relevant memories found.";

        return string.Join("\n", results.Select(m =>
            $"[{m.Id[..8]}] [{MemoryType.Label(m.Type)}] {(m.RelevanceScore.HasValue ? $"(score: {m.RelevanceScore:F2}) " : "")}{m.Content}"));
    }

    [McpServerTool]
    [Description("Retrieve the most recent memories from MindVault.")]
    public async Task<string> RecallMemories(
        [Description("Number of recent memories to return (default 20).")] int limit = 20,
        CancellationToken ct = default)
    {
        var result = await _client.GetMemoriesAsync(_config.NamespaceId, limit, ct);

        if (result.Items.Count == 0)
            return "No memories found.";

        return string.Join("\n", result.Items.Select(m =>
            $"[{m.Id[..8]}] [{MemoryType.Label(m.Type)}] {m.CreatedAt:yyyy-MM-dd} — {m.Content}"));
    }

    [McpServerTool]
    [Description("Delete a specific memory by its ID. Use the full ID returned by remember or search.")]
    public async Task<string> ForgetMemory(
        [Description("The full memory ID to delete.")] string id,
        CancellationToken ct = default)
    {
        await _client.DeleteMemoryAsync(_config.NamespaceId, id, ct);
        return $"Deleted memory {id}";
    }
}
