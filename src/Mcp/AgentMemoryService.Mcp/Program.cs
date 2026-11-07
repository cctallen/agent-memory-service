using AgentMemoryService.Mcp.Core;
using AgentMemoryService.Mcp.Core.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

// ── Config from environment ───────────────────────────────────
var apiKey = Environment.GetEnvironmentVariable("MINDVAULT_API_KEY")
    ?? throw new InvalidOperationException("MINDVAULT_API_KEY environment variable is required");

var namespaceId = Environment.GetEnvironmentVariable("MINDVAULT_NAMESPACE_ID")
    ?? throw new InvalidOperationException("MINDVAULT_NAMESPACE_ID environment variable is required");

var baseUrl = Environment.GetEnvironmentVariable("MINDVAULT_URL")
    ?? "http://3.129.106.237:5228";

// ── Host setup ────────────────────────────────────────────────
var builder = Host.CreateApplicationBuilder(args);

// MCP uses stdio — all logs must go to stderr so they don't corrupt the protocol stream
builder.Logging.AddConsole(opts =>
    opts.LogToStandardErrorThreshold = LogLevel.Warning);
builder.Logging.SetMinimumLevel(LogLevel.Warning);

// HTTP client for the MindVault API
builder.Services.AddHttpClient<MindVaultClient>(client =>
{
    client.BaseAddress = new Uri(baseUrl);
    client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Pass namespace ID through DI
builder.Services.AddSingleton(new MindVaultConfig(namespaceId));

// MCP server via stdio
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly(typeof(MemoryTools).Assembly); // tools live in Mcp.Core

await builder.Build().RunAsync();
