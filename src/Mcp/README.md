# MindVault MCP Server

A [Model Context Protocol](https://modelcontextprotocol.io/) server that gives Claude Desktop (and any other MCP host) access to [MindVault](https://github.com/cctallen/agent-memory-service) — persistent, cross-agent memory and secrets.

## Tools

| Tool | Description |
|---|---|
| `remember` | Save a memory (fact, preference, instruction, context, or summary) |
| `search_memories` | Semantic search across all memories in the namespace |
| `recall_memories` | List the most recent memories |
| `forget_memory` | Delete a memory by ID |
| `list_secrets` | List secret names (no values exposed) |
| `reveal_secret` | Decrypt and return a secret value by name |

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download) (or use a pre-built binary — see below)
- A running MindVault instance
- An agent API key + namespace ID from the MindVault admin

## Build

```bash
# From repo root — builds a self-contained single-file binary
dotnet publish src/Mcp/AgentMemoryService.Mcp/AgentMemoryService.Mcp.csproj \
  -c Release \
  -r osx-arm64 \        # or osx-x64, win-x64, linux-x64
  --self-contained \
  -o ./publish/mcp
```

The output is `./publish/mcp/mindvault-mcp` (no runtime required).

## Claude Desktop setup

Edit `~/Library/Application Support/Claude/claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "mindvault": {
      "command": "/path/to/mindvault-mcp",
      "env": {
        "MINDVAULT_URL": "http://your-server:5228",
        "MINDVAULT_API_KEY": "ams_your_agent_api_key",
        "MINDVAULT_NAMESPACE_ID": "your-namespace-uuid"
      }
    }
  }
}
```

Restart Claude Desktop. You'll see a 🔌 icon confirming the MCP server is connected.

## Cursor / VS Code setup

Add to `.cursor/mcp.json` or your VS Code MCP config:

```json
{
  "servers": {
    "mindvault": {
      "type": "stdio",
      "command": "/path/to/mindvault-mcp",
      "env": {
        "MINDVAULT_URL": "http://your-server:5228",
        "MINDVAULT_API_KEY": "ams_your_agent_api_key",
        "MINDVAULT_NAMESPACE_ID": "your-namespace-uuid"
      }
    }
  }
}
```

## Environment variables

| Variable | Required | Default | Description |
|---|---|---|---|
| `MINDVAULT_API_KEY` | ✅ | — | Agent API key (starts with `ams_`) |
| `MINDVAULT_NAMESPACE_ID` | ✅ | — | Namespace UUID to read/write |
| `MINDVAULT_URL` | ❌ | `http://3.129.106.237:5228` | Base URL of your MindVault API |
