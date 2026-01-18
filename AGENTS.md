# AI Agent Guidelines for SSW.TimePro.Mcp

This document provides guidelines for AI agents working with the SSW TimePro MCP server.

## Project Overview

SSW.TimePro.Mcp is a Model Context Protocol (MCP) server that provides AI-assisted timesheet management for SSW TimePro. It enables AI assistants to:

- Get timesheet recommendations based on CRM, git activity, and historical patterns
- Create, update, and manage timesheets with dry-run safety
- Scan git repositories (local, GitHub, Azure DevOps) for work activity
- Fetch CRM appointments and bookings

## Architecture

- **Framework**: .NET 10, MCP Server (stdio transport)
- **Key Services**:
  - `TimeProService` - API client for TimePro endpoints
  - `ConfirmationService` - Manages dry-run confirmations for write operations
  - `GitScanningService` - Scans local and remote git repositories
  - `GitHubService` - GitHub API integration
  - `LocalGitService` - Local git repository scanning

## Available MCP Tools

### Recommendations
- `recommend_day` - Single day recommendation with existing timesheets, suggestions, CRM bookings
- `recommend_week` - Week overview with hours summary

### Timesheets
- `get_timesheets` - Unified fetching by ID, date, or date range
- `create_timesheet` - Create new (dry-run by default)
- `update_timesheet` - Update existing
- `delete_timesheet` - Delete (dry-run by default)

### Clients & Projects
- `search_clients` - Search by name
- `get_projects_for_client` - List projects for a client
- `get_client_rate` - Get billable rate

### Git Scanning
- `scan_local_repositories` - Scan local git repos
- `scan_remote_commits` - Scan GitHub or Azure DevOps

### CRM
- `get_crm_bookings` - Fetch appointments (SSW production only)

### Confirmation
- `confirm_operation` - Execute pending dry-run operations

## API Testing

### Local Development Environment

The local TimePro API runs at `https://api.local-sswtimepro.com:7107/`.

### Required Headers

All API requests require these headers:
```
x-timepro-api-key: <API_KEY>
x-timepro-api-name: SSW-TimePro-MCP
x-timepro-tenant-id: <TENANT_ID>
```

### Example curl Commands

#### Get Timesheets
```bash
curl -k --request GET \
  --url 'https://api.local-sswtimepro.com:7107/api/Timesheets/GetTimesheetListViewModel?employeeID=JEK&date=2026-01-16' \
  --header 'x-timepro-api-key: <API_KEY>' \
  --header 'x-timepro-api-name: SSW-TimePro-MCP' \
  --header 'x-timepro-tenant-id: ssw'
```

#### Get Recent Projects
```bash
curl -k --request GET \
  --url 'https://api.local-sswtimepro.com:7107/api/Projects/GetRecentProjects?empId=JEK' \
  --header 'x-timepro-api-key: <API_KEY>' \
  --header 'x-timepro-api-name: SSW-TimePro-MCP' \
  --header 'x-timepro-tenant-id: ssw'
```

#### Get Employee Settings
```bash
curl -k --request GET \
  --url 'https://api.local-sswtimepro.com:7107/api/employees/getSettingsDetails' \
  --header 'x-timepro-api-key: <API_KEY>' \
  --header 'x-timepro-api-name: SSW-TimePro-MCP' \
  --header 'x-timepro-tenant-id: ssw'
```

#### Get CRM Appointments
```bash
curl -k --request GET \
  --url 'https://api.local-sswtimepro.com:7107/Crm/Appointments?employeeID=JEK&start=1736899200&end=1737417600' \
  --header 'x-timepro-api-key: <API_KEY>' \
  --header 'x-timepro-api-name: SSW-TimePro-MCP' \
  --header 'x-timepro-tenant-id: ssw'
```

Note: CRM endpoints use Unix epoch timestamps for `start` and `end` parameters.

#### Get Azure DevOps Commits
```bash
curl -k --request GET \
  --url 'https://api.local-sswtimepro.com:7107/api/DevOps/GetAzureSubResult?empId=JEK&startDate=2026-01-13&endDate=2026-01-17' \
  --header 'x-timepro-api-key: <API_KEY>' \
  --header 'x-timepro-api-name: SSW-TimePro-MCP' \
  --header 'x-timepro-tenant-id: ssw'
```

### SSL Certificate Issues

For local development (`local-*` hostnames), SSL certificate validation is bypassed in the MCP server. When using curl, add `-k` or `--insecure` flag.

## MCP Tool Testing with mcp-cli

### Configuration

Create `mcp_servers.json` in your project root:
```json
{
  "mcpServers": {
    "ssw-timepro": {
      "command": "ssw-timepro-mcp",
      "env": {
        "TIMEPRO_TENANT_ID": "ssw",
        "TIMEPRO_API_KEY": "<API_KEY>",
        "TIMEPRO_DEFAULT_EMPLOYEE_ID": "JEK",
        "TIMEPRO_BASE_URL": "https://api.local-sswtimepro.com:7107/",
        "GITHUB_USERNAME": "jernejk",
        "GITHUB_TOKEN": "<GITHUB_TOKEN>"
      }
    }
  }
}
```

### Running Tools

```bash
# List available tools
mcp-cli tools -s ssw-timepro

# Get daily recommendation
mcp-cli call-tool ssw-timepro recommend_day '{"date": "2026-01-17"}'

# Get weekly recommendation
mcp-cli call-tool ssw-timepro recommend_week '{"startDate": "2026-01-13"}'

# Fetch timesheets
mcp-cli call-tool ssw-timepro get_timesheets '{"employeeId": "JEK", "date": "2026-01-13"}'

# Search clients
mcp-cli call-tool ssw-timepro search_clients '{"employeeId": "JEK", "searchText": "SSW"}'

# Scan local repos
mcp-cli call-tool ssw-timepro scan_local_repositories '{"repositoryPaths": "/path/to/repo", "days": 7}'

# Scan GitHub
mcp-cli call-tool ssw-timepro scan_remote_commits '{"username": "jernejk", "days": 7}'

# Scan Azure DevOps
mcp-cli call-tool ssw-timepro scan_remote_commits '{"username": "JEK", "source": "azuredevops", "days": 7}'
```

## Environment Variables

| Variable | Description | Example |
|----------|-------------|---------|
| `TIMEPRO_BASE_URL` | API base URL | `https://api.local-sswtimepro.com:7107/` |
| `TIMEPRO_TENANT_ID` | Tenant identifier | `ssw` |
| `TIMEPRO_API_KEY` | API authentication key | `<key>` |
| `TIMEPRO_DEFAULT_EMPLOYEE_ID` | Default employee for tools | `JEK` |
| `TIMEPRO_CONFIRM_PHRASE` | Passphrase for write confirmations | (optional) |
| `GITHUB_TOKEN` | GitHub API token | (optional) |
| `GITHUB_USERNAME` | GitHub username for commit lookup | (optional) |

## Recommendation Priority

The recommendation tools follow this priority order:

1. **Existing Timesheets** (highest) - Already logged entries
2. **Suggested Timesheets** - Auto-generated from CRM/appointments
3. **CRM Bookings** - Calendar appointments
4. **Recent Projects** (fallback) - Based on historical usage

Within suggested timesheets, billable client work is prioritized over internal work.

## Work Day Hours

- Standard work day: 9:00 - 18:00 (9 hour span)
- 1 hour lunch break is subtracted
- Actual work hours: 8 hours per day
- Expected weekly hours: 40 hours (5 working days x 8 hours)

Employee settings are fetched from `/api/employees/getSettingsDetails` which returns:
- `StartTime`: e.g., "09:00:00"
- `EndTime`: e.g., "18:00:00"
- `TimeLessMinutes`: e.g., 60 (lunch break in minutes)

## Safety Features

### Dry-Run by Default
- `create_timesheet` and `delete_timesheet` use dry-run mode by default
- Returns a `confirmationId` that must be explicitly confirmed
- Use `confirm_operation` to execute

### Confirmation Passphrase
- Set `TIMEPRO_CONFIRM_PHRASE` to require a passphrase for confirmations
- Adds extra safety for write operations
