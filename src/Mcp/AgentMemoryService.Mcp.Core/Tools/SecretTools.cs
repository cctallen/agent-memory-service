using System.ComponentModel;
using ModelContextProtocol.Server;

namespace AgentMemoryService.Mcp.Core.Tools;

[McpServerToolType]
public sealed class SecretTools
{
    private readonly MindVaultClient _client;
    private readonly MindVaultConfig _config;

    public SecretTools(MindVaultClient client, MindVaultConfig config)
    {
        _client = client;
        _config = config;
    }

    [McpServerTool]
    [Description(
        "List all secrets stored in MindVault for this namespace. " +
        "Returns names and descriptions only — values are never included in the list.")]
    public async Task<string> ListSecrets(CancellationToken ct = default)
    {
        var secrets = await _client.GetSecretsAsync(_config.NamespaceId, ct);

        if (secrets.Count == 0)
            return "No secrets found in this namespace.";

        return string.Join("\n", secrets.Select(s =>
            $"[{s.Id[..8]}] {s.Name}{(s.Description is not null ? $" — {s.Description}" : "")}"));
    }

    [McpServerTool]
    [Description(
        "Reveal (decrypt) a secret value from MindVault by its name. " +
        "Only call this when you actually need to use the value — do not reveal secrets unnecessarily.")]
    public async Task<string> RevealSecret(
        [Description("The exact name of the secret to reveal (e.g. DATABASE_URL).")] string name,
        CancellationToken ct = default)
    {
        // Look up by name first
        var secrets = await _client.GetSecretsAsync(_config.NamespaceId, ct);
        var match = secrets.FirstOrDefault(s =>
            s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        if (match is null)
            return $"No secret named '{name}' found. Use list_secrets to see available secrets.";

        var revealed = await _client.RevealSecretAsync(_config.NamespaceId, match.Id, ct);
        return revealed.Value;
    }
}
