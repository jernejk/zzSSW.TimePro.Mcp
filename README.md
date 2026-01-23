# Unofficial SSW.TimePro.Mcp

[![.NET 10](https://img.shields.io/badge/.NET-10-512BD4)](https://dotnet.microsoft.com/download/dotnet/10.0)
[![MCP](https://img.shields.io/badge/MCP-Model%20Context%20Protocol-blue)](https://modelcontextprotocol.io/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

A unofficial .NET 10 Model Context Protocol (MCP) server for SSW TimePro. This server enables AI assistants (GitHub Copilot, Claude, Cursor, etc.) to manage timesheets through natural language.

## Features

- **Timesheet Recommendations** - Get AI-powered suggestions based on CRM bookings, recent projects, and git activity
- **Flexible Timesheet Fetching** - Query by date, date range, or specific ID
- **Full CRUD Operations** - Create, update, and delete timesheets with dry-run safety
- **Git Activity Scanning** - Scan local repos or remote sources (GitHub, Azure DevOps)
- **CRM Integration** - Fetch appointments and bookings from SSW CRM
- **Production Safety** - Built-in protections prevent accidental writes to production

## Quick Start

### Install as a .NET Tool (Recommended)

```bash
# Install globally
dotnet tool install -g SSW.TimePro.Mcp

# Verify installation
ssw-timepro-mcp --version
```

### Build from Source

```bash
git clone https://github.com/jernejk/SSW.TimePro.Mcp.git
cd SSW.TimePro.Mcp
dotnet build
```

## Configuration

### Environment Variables

| Variable | Description | Required |
|----------|-------------|----------|
| `TIMEPRO_API_KEY` | API authentication key | Yes |
| `TIMEPRO_TENANT_ID` | Tenant ID (e.g., `ssw`) | Yes |
| `TIMEPRO_BASE_URL` | API base URL | No (defaults to production) |
| `TIMEPRO_DEFAULT_EMPLOYEE_ID` | Default employee ID for tools | No |
| `TIMEPRO_CONFIRM_PHRASE` | Passphrase for write confirmations | No |
| `GITHUB_TOKEN` | GitHub API token for scanning | No |
| `GITHUB_USERNAME` | GitHub username for commit lookup | No |

### Environments

| Environment | Base URL |
|-------------|----------|
| Local | `https://api.local-sswtimepro.com:7107/` |
| Staging | `https://api.staging-sswtimepro.com/` |
| Production | `https://api.sswtimepro.com/` |

## IDE Integration

### VS Code with GitHub Copilot

Add to your VS Code settings (`.vscode/mcp.json` or user settings):

```json
{
  "mcp": {
    "servers": {
      "ssw-timepro": {
        "command": "ssw-timepro-mcp",
        "env": {
          "TIMEPRO_TENANT_ID": "ssw",
          "TIMEPRO_API_KEY": "your-api-key",
          "TIMEPRO_DEFAULT_EMPLOYEE_ID": "JEK",
          "TIMEPRO_BASE_URL": "https://api.local-sswtimepro.com:7107/",
          "GITHUB_USERNAME": "your-github-username",
          "GITHUB_TOKEN": "your-github-token"
        }
      }
    }
  }
}
```

### Claude Desktop

Add to your Claude Desktop config (`~/Library/Application Support/Claude/claude_desktop_config.json` on macOS):

```json
{
  "mcpServers": {
    "ssw-timepro": {
      "command": "ssw-timepro-mcp",
      "env": {
        "TIMEPRO_TENANT_ID": "ssw",
        "TIMEPRO_API_KEY": "your-api-key",
        "TIMEPRO_DEFAULT_EMPLOYEE_ID": "JEK",
        "TIMEPRO_BASE_URL": "https://api.local-sswtimepro.com:7107/",
        "GITHUB_USERNAME": "your-github-username",
        "GITHUB_TOKEN": "your-github-token"
      }
    }
  }
}
```

### Cursor

Add to your Cursor MCP settings:

```json
{
  "mcpServers": {
    "ssw-timepro": {
      "command": "ssw-timepro-mcp",
      "env": {
        "TIMEPRO_TENANT_ID": "ssw",
        "TIMEPRO_API_KEY": "your-api-key",
        "TIMEPRO_DEFAULT_EMPLOYEE_ID": "JEK",
        "GITHUB_USERNAME": "your-github-username",
        "GITHUB_TOKEN": "your-github-token"
      }
    }
  }
}
```

### Using dotnet run (Development)

If running from source instead of the installed tool:

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
        "DOTNET_ENVIRONMENT": "Development",
        "TIMEPRO_TENANT_ID": "ssw",
        "TIMEPRO_API_KEY": "your-api-key",
        "TIMEPRO_DEFAULT_EMPLOYEE_ID": "JEK",
        "TIMEPRO_BASE_URL": "https://api.local-sswtimepro.com:7107/",
        "GITHUB_USERNAME": "your-github-username",
        "GITHUB_TOKEN": "your-github-token"
      }
    }
  }
}
```

## Testing with mcp-cli

[mcp-cli](https://github.com/chrishayuk/mcp-cli) is useful for testing MCP tools directly from the command line.

### Setup

1. Create `mcp_servers.json` in your project:

```json
{
  "mcpServers": {
    "ssw-timepro": {
      "command": "ssw-timepro-mcp",
      "env": {
        "TIMEPRO_TENANT_ID": "ssw",
        "TIMEPRO_API_KEY": "your-api-key",
        "TIMEPRO_DEFAULT_EMPLOYEE_ID": "JEK",
        "TIMEPRO_BASE_URL": "https://api.local-sswtimepro.com:7107/",
        "GITHUB_USERNAME": "your-github-username",
        "GITHUB_TOKEN": "your-github-token"
      }
    }
  }
}
```

2. Run tools:

```bash
# List available tools
mcp-cli tools -s ssw-timepro

# Get recommendations for today
mcp-cli call-tool ssw-timepro recommend_day '{}'

# Get weekly overview
mcp-cli call-tool ssw-timepro recommend_week '{"startDate": "2026-01-13"}'

# Fetch timesheets
mcp-cli call-tool ssw-timepro get_timesheets '{"employeeId": "JEK", "date": "2026-01-13"}'

# Search for clients
mcp-cli call-tool ssw-timepro search_clients '{"employeeId": "JEK", "searchText": "SSW"}'

# Scan local git repos
mcp-cli call-tool ssw-timepro scan_local_repositories '{"repositoryPaths": "/path/to/repo1,/path/to/repo2", "days": 7}'

# Scan GitHub
mcp-cli call-tool ssw-timepro scan_remote_commits '{"username": "jernejk", "days": 7}'

# Scan Azure DevOps
mcp-cli call-tool ssw-timepro scan_remote_commits '{"username": "JEK", "source": "azuredevops", "days": 7}'
```

## Available MCP Tools

### Recommendations

| Tool | Description |
|------|-------------|
| `recommend_day` | Get timesheet recommendation for a single day |
| `recommend_week` | Get timesheet recommendations for an entire week |

### Timesheet Operations

| Tool | Description |
|------|-------------|
| `get_timesheets` | Fetch timesheets by ID, date, or date range |
| `create_timesheet` | Create a new timesheet (dry-run by default) |
| `update_timesheet` | Update an existing timesheet |
| `delete_timesheet` | Delete a timesheet (dry-run by default) |

### Client & Project Lookup

| Tool | Description |
|------|-------------|
| `search_clients` | Search for clients by name |
| `get_projects_for_client` | Get available projects for a client |
| `get_client_rate` | Get employee's billable rate for a client |

### Git Activity

| Tool | Description |
|------|-------------|
| `scan_local_repositories` | Scan multiple local git repos for commits |
| `scan_remote_commits` | Scan GitHub or Azure DevOps for commits |

### CRM & Appointments

| Tool | Description |
|------|-------------|
| `get_crm_bookings` | Fetch CRM appointments (SSW production only) |

### Confirmation

| Tool | Description |
|------|-------------|
| `confirm_operation` | Execute a pending dry-run confirmation |

## Safety Features

### Dry-Run by Default

Write operations (`create_timesheet`, `delete_timesheet`) use dry-run mode by default. This creates a confirmation that must be explicitly executed:

```
1. create_timesheet(...) → Returns confirmationId
2. confirm_operation(confirmationId) → Executes the operation
```

### Confirmation Passphrase

Set `TIMEPRO_CONFIRM_PHRASE` to require a passphrase for confirmations:

```bash
export TIMEPRO_CONFIRM_PHRASE="let's do it!"
```

Then: `confirm_operation(confirmationId, passphrase="let's do it!")`

## Example Prompts

### For GitHub Copilot / Cursor

```
Create timesheets for this week:
- Monday: SSW.Rewards (client SSW, project 4BPT0L), 9am-6pm
- Tuesday-Thursday: Northwind client, audit work, 9am-5pm
- Friday: SSW internal work

Use dry-run mode and show me what will be created.
```

### For Claude

```
What did I work on last week? Check my git commits and timesheets.

Then recommend what I should log for this week based on my recent activity.
```

## Development

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [mcp-cli](https://github.com/chrishayuk/mcp-cli) (for testing)
- SSW TimePro API access

### Running Tests

```bash
dotnet test
```

### Project Structure

```
src/SSW.TimePro.Mcp.Server/
├── Configuration/     # Settings classes
├── Models/           # DTOs and request/response models
├── Services/         # Business logic
│   ├── Confirmation/ # Dry-run confirmation handling
│   └── Git/          # Git scanning services
├── Tools/            # MCP tool implementations
└── Program.cs        # Host configuration
```

### Secrets Management

For development, use .NET User Secrets:

```bash
cd src/SSW.TimePro.Mcp.Server
dotnet user-secrets set "TimePro:ApiKey" "YOUR_API_KEY"
dotnet user-secrets set "TimePro:TenantId" "ssw"
dotnet user-secrets set "GitHub:Token" "YOUR_GITHUB_TOKEN"
```

Or use [Infisical](https://infisical.com/):

```bash
./scripts/sync-secrets.sh
```

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Run tests: `dotnet test`
5. Submit a pull request

## License

MIT License - see [LICENSE](LICENSE) for details.

## Related

- [Model Context Protocol](https://modelcontextprotocol.io/) - The protocol specification
- [SSW TimePro](https://sswtimepro.com/) - The timesheet management system
- [mcp-cli](https://github.com/chrishayuk/mcp-cli) - CLI for testing MCP servers
