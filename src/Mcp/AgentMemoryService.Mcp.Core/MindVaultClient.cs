using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgentMemoryService.Mcp.Core;

/// <summary>
/// Typed HTTP client for the MindVault API.
/// Authenticates as an agent via X-Api-Key (set on the HttpClient by DI).
/// </summary>
public sealed class MindVaultClient
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public MindVaultClient(HttpClient http) => _http = http;

    // ── Memories ─────────────────────────────────────────────

    public async Task<PagedResult<MemoryDto>> GetMemoriesAsync(
        string namespaceId, int pageSize = 20, CancellationToken ct = default)
    {
        var res = await _http.GetAsync(
            $"/api/namespaces/{namespaceId}/memories?pageSize={pageSize}", ct);
        await EnsureSuccessAsync(res);
        return (await res.Content.ReadFromJsonAsync<PagedResult<MemoryDto>>(_json, ct))!;
    }

    public async Task<List<MemoryDto>> SearchMemoriesAsync(
        string namespaceId, string query, int topK = 10, CancellationToken ct = default)
    {
        var res = await _http.GetAsync(
            $"/api/namespaces/{namespaceId}/memories/search?q={Uri.EscapeDataString(query)}&topK={topK}", ct);
        await EnsureSuccessAsync(res);
        return (await res.Content.ReadFromJsonAsync<List<MemoryDto>>(_json, ct))!;
    }

    public async Task<MemoryDto> AddMemoryAsync(
        string namespaceId, AddMemoryRequest req, CancellationToken ct = default)
    {
        var body = new StringContent(JsonSerializer.Serialize(req, _json), Encoding.UTF8, "application/json");
        var res = await _http.PostAsync($"/api/namespaces/{namespaceId}/memories", body, ct);
        await EnsureSuccessAsync(res);
        return (await res.Content.ReadFromJsonAsync<MemoryDto>(_json, ct))!;
    }

    public async Task DeleteMemoryAsync(
        string namespaceId, string memoryId, CancellationToken ct = default)
    {
        var res = await _http.DeleteAsync($"/api/namespaces/{namespaceId}/memories/{memoryId}", ct);
        await EnsureSuccessAsync(res);
    }

    // ── Secrets ──────────────────────────────────────────────

    public async Task<List<SecretDto>> GetSecretsAsync(
        string namespaceId, CancellationToken ct = default)
    {
        var res = await _http.GetAsync($"/api/namespaces/{namespaceId}/secrets", ct);
        await EnsureSuccessAsync(res);
        return (await res.Content.ReadFromJsonAsync<List<SecretDto>>(_json, ct))!;
    }

    public async Task<SecretRevealDto> RevealSecretAsync(
        string namespaceId, string secretId, CancellationToken ct = default)
    {
        var res = await _http.PostAsync(
            $"/api/namespaces/{namespaceId}/secrets/{secretId}/reveal",
            new StringContent("{}", Encoding.UTF8, "application/json"), ct);
        await EnsureSuccessAsync(res);
        return (await res.Content.ReadFromJsonAsync<SecretRevealDto>(_json, ct))!;
    }

    // ── Helpers ───────────────────────────────────────────────

    private static async Task EnsureSuccessAsync(HttpResponseMessage res)
    {
        if (!res.IsSuccessStatusCode)
        {
            var body = await res.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"API {(int)res.StatusCode}: {body}");
        }
    }
}

// ── DTOs ──────────────────────────────────────────────────────

public sealed record PagedResult<T>(List<T> Items, int Total, int Page, int PageSize);

public sealed record MemoryDto(
    string Id,
    string NamespaceId,
    string Content,
    int Type,
    string? AgentId,
    string? Source,
    float? RelevanceScore,
    DateTime CreatedAt,
    DateTime? ExpiresAt);

public sealed record SecretDto(
    string Id,
    string NamespaceId,
    string Name,
    string? Description,
    DateTime CreatedAt);

public sealed record SecretRevealDto(
    string Id,
    string Name,
    string? Description,
    string Value);

public sealed record AddMemoryRequest(
    string Content,
    int Type = 0,
    int Visibility = 0,
    string? Source = null,
    DateTime? ExpiresAt = null);

public static class MemoryType
{
    public const int Fact = 0;
    public const int Preference = 1;
    public const int Instruction = 4;
    public const int Context = 3;
    public const int Summary = 5;

    public static int Parse(string? value) => value?.ToLowerInvariant() switch
    {
        "preference" => Preference,
        "instruction" => Instruction,
        "context" => Context,
        "summary" => Summary,
        _ => Fact,
    };

    public static string Label(int type) => type switch
    {
        Preference => "Preference",
        Instruction => "Instruction",
        Context => "Context",
        Summary => "Summary",
        _ => "Fact",
    };
}
