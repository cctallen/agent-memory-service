using AgentMemoryService.Shared.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace AgentMemoryService.Api.Auth;

/// <summary>
/// Allows agents to authenticate via X-Api-Key header instead of JWT.
/// The agent identity is resolved to a ClaimsPrincipal with the agent's
/// namespace ID and agent ID as claims.
/// </summary>
public sealed class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly AppDbContext _db;
    public const string SchemeName = "ApiKey";
    public const string HeaderName = "X-Api-Key";

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        AppDbContext db)
        : base(options, logger, encoder)
    {
        _db = db;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var apiKeyValues))
            return AuthenticateResult.NoResult();

        var apiKey = apiKeyValues.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(apiKey))
            return AuthenticateResult.NoResult();

        var agent = await _db.Agents
            .Include(a => a.Namespaces)
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.ApiKey == apiKey, Context.RequestAborted);

        if (agent is null)
            return AuthenticateResult.Fail("Invalid API key");

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, agent.Id.ToString()),
            new(ClaimTypes.Name, agent.Name),
            new("agent_id", agent.Id.ToString()),
            new(ClaimTypes.Role, "Agent"),
        };

        // Add a claim for each namespace the agent belongs to
        foreach (var membership in agent.Namespaces)
            claims.Add(new Claim("namespace_id", membership.NamespaceId.ToString()));

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return AuthenticateResult.Success(ticket);
    }
}
