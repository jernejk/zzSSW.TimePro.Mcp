# SSW.TimePro.Mcp

A .NET 10 Model Context Protocol (MCP) server for SSW TimePro, built with Microsoft Agent Framework. This server provides AI assistants with the ability to fetch, create, and update timesheets.

## Features

- **Fetch Timesheets by Days**: Get timesheets for the last X days from today
- **Fetch Timesheets by Date Range**: Get timesheets for a specific date range (defaults to current week)
- **Suggested Timesheets**: Fetch and manage auto-generated timesheet suggestions
- **CRM Bookings**: Fetch CRM appointments/bookings (SSW production only)
- **Create/Update Timesheets**: Create and update timesheet entries with dry run support
- **Delete Timesheets**: Delete timesheets with dry run support
- **Accept/Reject Suggested Timesheets**: Accept or reject suggested timesheets

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Infisical CLI](https://infisical.com/docs/cli/overview) (for secrets management)
- [mcp-cli](https://github.com/philschmid/mcp-cli) (for testing)
- SSW TimePro API access

## Getting Started

### 1. Clone and Build

```bash
git clone https://github.com/jernejk/SSW.TimePro.Mcp.git
cd SSW.TimePro.Mcp
dotnet build
```

### 2. Configure Secrets

The server uses .NET User Secrets for sensitive configuration. You can either set them manually or use Infisical:

#### Using Infisical (Recommended)

```bash
# List secrets from the project
infisical secrets --projectId f45d9554-1eb2-417b-819a-5954d6f32f58

# Sync secrets to .NET User Secrets
./scripts/sync-secrets.sh
```

#### Manual Configuration

```bash
cd src/SSW.TimePro.Mcp.Server
dotnet user-secrets set "TimePro:ApiKey" "YOUR_API_KEY"
dotnet user-secrets set "TimePro:TenantId" "ssw"
dotnet user-secrets set "TimePro:BaseUrl" "https://api.sswtimepro.com/"
dotnet user-secrets set "GitHub:Token" "YOUR_GITHUB_TOKEN"
```

### 3. Run Tests

```bash
dotnet test
```

### 4. Configure MCP Client

Add the server to your MCP client configuration. See the examples in the `mcp-settings` folder.

## Configuration

### TimePro Settings

| Setting | Description | Default |
|---------|-------------|---------|
| `TimePro:BaseUrl` | TimePro API base URL | `https://api.sswtimepro.com/` |
| `TimePro:TenantId` | Tenant ID (e.g., 'ssw') | `ssw` |
| `TimePro:ApiKey` | API key for authentication | (required) |

### URL Normalization

The server automatically normalizes URLs to use the `api` subdomain:
- `https://ssw.sswtimepro.com/` → `https://api.sswtimepro.com/`
- `https://ssw.local-sswtimepro.com:7107/` → `https://api.local-sswtimepro.com:7107/`

### Environments

| Environment | Base URL |
|-------------|----------|
| Local | `https://api.local-sswtimepro.com:7107/` |
| Staging | `https://api.staging-sswtimepro.com/` |
| Production | `https://api.sswtimepro.com/` |

## Available MCP Tools

### Timesheet Fetching

- **GetTimesheetsByDays**: Fetch timesheets for X days from today
  - `employeeId`: Employee ID (e.g., 'JEK')
  - `takeDays`: Number of days to fetch (default: 7)
  - `skipDays`: Days to skip from today (default: 0)
  - `includeDescription`: Include full notes (default: false)

- **GetTimesheetsByDateRange**: Fetch timesheets for a date range
  - `employeeId`: Employee ID
  - `startDate`: Start date (yyyy-MM-dd, default: current week's Monday)
  - `endDate`: End date (yyyy-MM-dd, default: current week's Sunday)
  - `includeDescription`: Include full notes

- **GetSuggestedTimesheets**: Fetch auto-generated timesheet suggestions
  - `employeeId`: Employee ID
  - `date`: Target date (default: today)
  - `refresh`: Refresh suggestions before fetching

- **GetTimesheetById**: Get a specific timesheet by ID

### CRM Bookings

- **GetCrmBookings**: Fetch CRM appointments (SSW production only)
  - `employeeId`: Employee ID
  - `startDate`: Start date (default: current week's Monday)
  - `endDate`: End date (default: current week's Sunday)

### Timesheet Management

- **GetTimesheetReferenceData**: Get categories, locations, and billable types
- **SearchClients**: Search for clients by name
- **CreateTimesheet**: Create a new timesheet entry (supports dry run)
- **UpdateTimesheet**: Update an existing timesheet (supports dry run)
- **DeleteTimesheet**: Delete a timesheet entry (supports dry run)
- **AcceptSuggestedTimesheet**: Accept a suggested timesheet
- **DeleteSuggestedTimesheet**: Delete/reject a suggested timesheet (supports dry run)

## Architecture

This project follows a light Vertical Slice Architecture (VSA):

```
src/SSW.TimePro.Mcp.Server/
├── Configuration/          # Settings classes
├── Models/                 # DTOs and request/response models
├── Services/               # Business logic (TimeProService)
├── Tools/                  # MCP tool implementations
└── Program.cs              # Host configuration
```

## Testing

### Unit Tests

```bash
# Run all unit tests
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```

### Integration Tests

Integration tests run against a live TimePro instance:

```bash
# Run integration tests
./scripts/run-integration-tests.sh

# Or manually with environment variable
RUN_INTEGRATION_TESTS=true dotnet test --filter "Category=Integration"
```

### MCP-CLI Testing

Quick endpoint testing using [mcp-cli](https://github.com/philschmid/mcp-cli):

```bash
# List all available tools
mcp-cli -d

# Test specific endpoints
mcp-cli ssw-timepro/get_timesheets_by_days '{"employeeId": "JEK", "takeDays": 7}'
mcp-cli ssw-timepro/search_clients '{"employeeId": "JEK", "searchText": "SSW"}'
mcp-cli ssw-timepro/delete_timesheet '{"timesheetId": 123, "dryRun": true}'

# Run all endpoint tests
./scripts/test-mcp-endpoints.sh
```

## Scripts

| Script | Description |
|--------|-------------|
| `scripts/sync-secrets.sh` | Sync secrets from Infisical to .NET User Secrets |
| `scripts/run-integration-tests.sh` | Run integration tests with secrets |
| `scripts/test-mcp-endpoints.sh` | Test all MCP endpoints using mcp-cli |

## Infisical Integration

This project uses Infisical for secrets management. The project slug is `ssw-time-pro-th0-i`.

Available secrets:
- `GITHUB_TOKEN` - GitHub token for commit fetching
- `TimePro-SSW-ApiKey` - SSW tenant API key
- `TimePro-JK_MCP-ApiKey` - JK_MCP tenant API key (for testing)
- `TimeProApiUrl` - Default API URL

## License

MIT License - see [LICENSE](LICENSE) for details.

