# MCP Settings

This folder contains example configuration files for connecting MCP clients to the SSW TimePro MCP server.

## VSCode / GitHub Copilot

Copy `vscode-settings.json` to your VSCode MCP configuration:

**Location**: `~/.vscode/mcp-servers.json` or project-level `.vscode/mcp-servers.json`

```json
{
  "mcpServers": {
    "ssw-timepro": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "/path/to/SSW.TimePro.Mcp/src/SSW.TimePro.Mcp.Server/SSW.TimePro.Mcp.Server.csproj"
      ],
      "env": {
        "DOTNET_ENVIRONMENT": "Development"
      }
    }
  }
}
```

## Claude Desktop

Add to your Claude Desktop configuration:

**macOS**: `~/Library/Application Support/Claude/claude_desktop_config.json`
**Windows**: `%APPDATA%\Claude\claude_desktop_config.json`

```json
{
  "mcpServers": {
    "ssw-timepro": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "/path/to/SSW.TimePro.Mcp/src/SSW.TimePro.Mcp.Server/SSW.TimePro.Mcp.Server.csproj"
      ],
      "env": {
        "DOTNET_ENVIRONMENT": "Production"
      }
    }
  }
}
```

## Production Mode

For production use, compile the project first:

```bash
dotnet publish -c Release
```

Then use the compiled binary directly:

```json
{
  "mcpServers": {
    "ssw-timepro": {
      "command": "/path/to/SSW.TimePro.Mcp/src/SSW.TimePro.Mcp.Server/bin/Release/net10.0/publish/SSW.TimePro.Mcp.Server"
    }
  }
}
```

## Environment Variables

You can also pass configuration via environment variables:

```json
{
  "mcpServers": {
    "ssw-timepro": {
      "command": "dotnet",
      "args": ["run", "--project", "/path/to/project"],
      "env": {
        "DOTNET_ENVIRONMENT": "Production",
        "TimePro__BaseUrl": "https://api.sswtimepro.com/",
        "TimePro__TenantId": "ssw",
        "TimePro__ApiKey": "YOUR_API_KEY"
      }
    }
  }
}
```

Note: Use double underscores (`__`) for nested configuration in environment variables.
