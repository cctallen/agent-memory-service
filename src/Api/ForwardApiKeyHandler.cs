using Microsoft.AspNetCore.Http;

namespace AgentMemoryService.Api;

/// <summary>
/// When the MCP server is hosted inside the API process, this handler
/// forwards the X-Api-Key from the inbound MCP request to the outbound
/// self-loopback HTTP calls made by MindVaultClient.
/// In the standalone stdio binary this handler is not registered.
/// </summary>
public sealed class ForwardApiKeyHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _accessor;

    public ForwardApiKeyHandler(IHttpContextAccessor accessor) => _accessor = accessor;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken ct)
    {
        var key = _accessor.HttpContext?.Request.Headers["X-Api-Key"].FirstOrDefault();
        if (!string.IsNullOrEmpty(key))
            request.Headers.TryAddWithoutValidation("X-Api-Key", key);

        return base.SendAsync(request, ct);
    }
}
