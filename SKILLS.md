# SSW TimePro MCP Skills

> AI-assisted timesheet management for SSW TimePro via Model Context Protocol (MCP).

This document describes the capabilities available to AI assistants (GitHub Copilot, Claude, Cursor, etc.) when using the SSW TimePro MCP server.

## Quick Start

```bash
# Install the MCP server
dotnet tool install -g SSW.TimePro.Mcp

# Configure your AI assistant (see README.md for IDE-specific setup)
```

## Available Tools

### Recommendation Tools

| Tool | Description |
|------|-------------|
| `recommend_day` | Get recommendation for a single day - shows existing timesheets, suggestions, CRM bookings, and recent projects |
| `recommend_week` | Get recommendations for an entire week with summary of hours logged vs needed |

### Timesheet Tools

| Tool | Description |
|------|-------------|
| `get_timesheets` | Unified fetching - by ID, single date, or date range. Defaults to current week. |
| `create_timesheet` | Create a new timesheet entry (dry-run by default) |
| `update_timesheet` | Update an existing timesheet entry |
| `delete_timesheet` | Delete a timesheet entry (dry-run by default) |

### Client & Project Tools

| Tool | Description |
|------|-------------|
| `search_clients` | Search for clients by name. Returns client ID for timesheet creation. |
| `get_projects_for_client` | Get available projects for a specific client. |
| `get_client_rate` | Get employee's billable rate for a specific client. |

### Git Scanning Tools

| Tool | Description |
|------|-------------|
| `scan_local_repositories` | Scan multiple local git repos for commits. Great for finding work activity. |
| `scan_remote_commits` | Scan GitHub or Azure DevOps (via TimePro API) for user's commits. |

### CRM Tools

| Tool | Description |
|------|-------------|
| `get_crm_bookings` | Fetch CRM bookings/appointments (SSW production only). |

### Confirmation Tools

| Tool | Description |
|------|-------------|
| `confirm_operation` | Execute a pending dry-run confirmation. Required for create/delete operations. |

---

## Common Workflows

### 1. Quick Weekly Catch-Up

> "What do I need to log this week?"

**Workflow:**
1. `recommend_week` - See overview of all days, what's logged, what's suggested
2. For days with suggestions: `create_timesheet` based on the recommendation
3. `confirm_operation` - Execute pending confirmations

### 2. Single Day Timesheet

> "Create a timesheet for Monday on SSW.Rewards project"

**Workflow:**
1. `search_clients` - Find client ID (e.g., "SSW")
2. `get_projects_for_client` - Find project ID (e.g., "4BPT0L")
3. `get_client_rate` - Get billable rate if needed
4. `create_timesheet` - Create with `dryRun=true` (default)
5. `confirm_operation` - Execute after review

### 3. Git-Based Timesheet Creation

> "What did I work on last week? Create timesheets based on my commits."

**Workflow:**
1. `scan_local_repositories` or `scan_remote_commits` - Find git activity
2. `recommend_week` - See what's already logged
3. `create_timesheet` for each day based on git activity
4. `confirm_operation` - Execute confirmations

### 4. Azure DevOps Activity

> "Check my Azure DevOps commits for the last 7 days"

**Workflow:**
```
scan_remote_commits(username="JEK", source="azuredevops", days=7)
```

This uses the TimePro API to fetch commits from all connected Azure DevOps subscriptions.

---

## Writing Good Notes

When creating timesheets, the `notes` field should follow these guidelines:

### Do's
- Start with a verb: "Implemented...", "Fixed...", "Reviewed..."
- Be specific about what was done
- Reference PRs, tickets, or features when relevant
- Keep it concise but informative

### Don'ts
- Don't just write the project name
- Don't use vague descriptions like "worked on stuff"
- Don't include sensitive information

### Examples

**Good:**
```
Implemented user authentication flow with OAuth2
Fixed bug in payment processing (PR #234)
Reviewed architecture for new reporting module
```

**Bad:**
```
SSW.Rewards work
Development
Meetings
```

---

## Safety Features

### Dry-Run by Default

Write operations (`create_timesheet`, `delete_timesheet`) use `dryRun=true` by default. This creates a confirmation that must be explicitly executed.

```
1. create_timesheet(...) → Returns confirmationId
2. confirm_operation(confirmationId) → Executes the operation
```

### Confirmation Passphrase

Set `TIMEPRO_CONFIRM_PHRASE` environment variable to require a passphrase:

```bash
export TIMEPRO_CONFIRM_PHRASE="let's do it!"
```

Then use: `confirm_operation(confirmationId, passphrase="let's do it!")`

---

## Configuration

### Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `TIMEPRO_API_KEY` | API authentication key | Required |
| `TIMEPRO_TENANT_ID` | Tenant ID (e.g., `ssw`) | Required |
| `TIMEPRO_BASE_URL` | API base URL | `https://api.sswtimepro.com/` |
| `TIMEPRO_DEFAULT_EMPLOYEE_ID` | Default employee for tools | Not set |
| `TIMEPRO_CONFIRM_PHRASE` | Passphrase for confirmations | Not set |
| `GITHUB_TOKEN` | GitHub API token for scanning | Optional |
| `GITHUB_USERNAME` | GitHub username for commit lookup | Optional |

### Common Reference IDs

These are examples - use `search_clients` and `get_projects_for_client` to find actual values.

| Category | ID | Description |
|----------|-----|-------------|
| Production | `PROD` | Development work |
| Audit | `AUDIT` | Architecture audits |
| Leave | `LEAVE` | Time off |

| Billable Type | ID | Description |
|---------------|-----|-------------|
| Billable | `BILLABLE` or `B` | Client-billable work |
| Internal | `W` | Internal/non-billable work |

| Location | ID | Description |
|----------|-----|-------------|
| Home | `Home` | Remote/work from home (default) |
| Office | `Office` | Office-based work |
| Client Site | `Client-Site` | On-site at client location |

| Category | ID | Description |
|----------|-----|-------------|
| Development | `DEV` | Software development (default) |
| Meeting | `MEETING` | Meetings, calls, consultations |
| Admin | `ADMIN` | Administrative tasks |
| Support | `SUPPORT` | Support and maintenance |

---

## Example Prompts

### For GitHub Copilot / Cursor

```bash
# Quick overview
"Show me what timesheets I need to log this week"

# Create from activity
"Scan my git repos and create timesheets for last week"

# Specific day
"Create a timesheet for yesterday - I worked on SSW.Rewards from 9am to 6pm"
```

### For Claude

```
Check my timesheets for this week and let me know if I'm missing any days.

For missing days, suggest what I should log based on:
1. My recent project history
2. Any CRM bookings
3. Git commits from /Users/me/git

Then create the timesheets in dry-run mode for my review.
```

---

## Troubleshooting

### "Invalid confirmation passphrase"
Check your `TIMEPRO_CONFIRM_PHRASE` environment variable matches what you're providing.

### "Confirmation not found"
Confirmations expire after 24 hours. Create a new one.

### SSL Certificate Errors
For local development with self-signed certs, the MCP server automatically bypasses SSL validation for `local-*` hostnames.

### "Employee ID required"
Either provide `employeeId` parameter or set `TIMEPRO_DEFAULT_EMPLOYEE_ID` in configuration.

---

## Version

- **MCP Server**: 1.0.0
- **Protocol**: Model Context Protocol (stdio transport)
- **Runtime**: .NET 10
