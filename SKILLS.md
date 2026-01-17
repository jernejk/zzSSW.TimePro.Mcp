# SSW TimePro MCP Skills

> AI-assisted timesheet management for SSW TimePro via Model Context Protocol (MCP).

This document describes the capabilities available to AI assistants (GitHub Copilot, Claude, etc.) when using the SSW TimePro MCP server.

## Quick Start

```bash
# Install the MCP server
dotnet tool install -g SSW.TimePro.Mcp

# Configure your AI assistant (see mcp-settings/README.md)
```

## Available Tools

### Timesheet Query Tools

| Tool | Description |
|------|-------------|
| `get_timesheets_by_days` | Fetch timesheets for X days from today. Use `takeDays=7` for a week. |
| `get_timesheets_by_date_range` | Fetch timesheets for a specific date range. Defaults to current week. |
| `get_suggested_timesheets` | Fetch auto-generated timesheet suggestions for a date. |
| `get_timesheet_by_id` | Get details of a specific timesheet. |

### Timesheet Management Tools

| Tool | Description |
|------|-------------|
| `get_timesheet_reference_data` | Get categories, locations, billable types before creating a timesheet. |
| `search_clients` | Search for clients by name. Returns client ID for timesheet creation. |
| `get_projects_for_client` | Get available projects for a specific client. |
| `create_timesheet` | Create a new timesheet entry (dry-run by default). |
| `update_timesheet` | Update an existing timesheet entry. |
| `delete_timesheet` | Delete a timesheet entry (dry-run by default). |
| `accept_suggested_timesheet` | Accept/convert a suggested timesheet to a real entry. |
| `delete_suggested_timesheet` | Reject/delete a suggested timesheet. |

### Confirmation Tools (Dry-Run System)

| Tool | Description |
|------|-------------|
| `list_pending_confirmations` | List all pending dry-run operations waiting for confirmation. |
| `get_confirmation` | Get full details of a pending confirmation. |
| `confirm_operation` | Execute a pending confirmation (requires passphrase if configured). |
| `cancel_confirmation` | Cancel a pending confirmation without executing. |
| `cleanup_confirmations` | Remove expired and old confirmations. |

### Agenda & Planning Tools

| Tool | Description |
|------|-------------|
| `generate_weekly_agenda` | Generate a weekly agenda from CRM, suggestions, existing timesheets, and git. |
| `export_agenda_to_markdown` | Export agenda to Markdown for review. |
| `analyze_work_patterns` | Analyze historical timesheet patterns (frequent clients, typical hours). |
| `create_timesheets_from_agenda` | Bulk create timesheets from an agenda (skips days with existing entries). |
| `get_leave_url` | Get the URL for submitting leave requests. |

### Git Scanning Tools

| Tool | Description |
|------|-------------|
| `scan_local_repository` | Scan a local git repo for commits. Great for finding work activity. |
| `scan_local_repositories` | Scan multiple repos at once. |
| `scan_github` | Scan GitHub for a user's commit activity via API. |
| `discover_repositories` | Find git repositories in a directory (up to 2 levels deep). |

### CRM Tools

| Tool | Description |
|------|-------------|
| `get_crm_bookings` | Fetch CRM bookings/appointments (SSW production only). |

---

## Common Workflows

### 1. Weekly Timesheet Catch-Up

> "Create timesheets for this week based on my git activity"

**Workflow:**
1. `scan_local_repository` - Find commits in your project repos
2. `generate_weekly_agenda` - Combine git activity with existing data
3. `export_agenda_to_markdown` - Review the generated agenda
4. `create_timesheets_from_agenda` - Create dry-run confirmations
5. `list_pending_confirmations` - Review what will be created
6. `confirm_operation` - Execute each confirmation

### 2. Single Day Timesheet

> "Create a timesheet for Monday on SSW.Rewards project"

**Workflow:**
1. `search_clients` - Find client ID (e.g., "SSW")
2. `get_projects_for_client` - Find project ID (e.g., "4BPT0L")
3. `get_timesheet_reference_data` - Get category/location IDs
4. `create_timesheet` - Create with `dryRun=true`
5. `confirm_operation` - Execute after review

### 3. Multi-Project Week

> "Monday SSW.Rewards, Tue-Thu ASF Audits, Friday SSW.Rewards"

**Workflow:**
```
Day       | Client   | Project | Category | Hours
----------|----------|---------|----------|-------
Monday    | SSW      | 4BPT0L  | PROD     | 09:00-18:00
Tuesday   | LR8R0L   | ASF     | AUDIT    | 09:00-17:00
Wednesday | LR8R0L   | ASF     | AUDIT    | 09:00-17:00
Thursday  | LR8R0L   | ASF     | AUDIT    | 09:00-17:00
Friday    | SSW      | 4BPT0L  | PROD     | 09:00-18:00
```

Use `create_timesheet` for each day with appropriate parameters.

### 4. Review and Analyze

> "What did I work on last week?"

**Workflow:**
1. `get_timesheets_by_date_range` - Fetch last week's timesheets
2. `analyze_work_patterns` - See patterns and frequent projects
3. `scan_local_repositories` - Compare with git activity

---

## Safety Features

### Dry-Run by Default

All write operations (`create_timesheet`, `delete_timesheet`) use `dryRun=true` by default. This creates a confirmation that must be explicitly executed.

### Production Protection

When configured against production (`api.sswtimepro.com`):
- **Read operations**: Allowed
- **Write operations**: BLOCKED
- **Dry-run confirmations**: Created but cannot be executed

### Confirmation Passphrase

Set `TIMEPRO_CONFIRM_PHRASE` environment variable to require a passphrase for executing confirmations:

```bash
export TIMEPRO_CONFIRM_PHRASE="let's do it!"
```

Then use: `confirm_operation(confirmationId, passphrase="let's do it!")`

---

## Configuration

### Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `TIMEPRO_API_TOKEN` | API authentication token | Required |
| `TIMEPRO_BASE_URL` | API base URL | `https://api.local-sswtimepro.com:7107/` |
| `TIMEPRO_CONFIRM_PHRASE` | Passphrase for confirmations | Not set |
| `GITHUB_TOKEN` | GitHub API token for scanning | Optional |

### Common Client/Project IDs

| Client | Client ID | Common Projects |
|--------|-----------|-----------------|
| SSW (Internal) | `SSW` | `4BPT0L` (SSW.Rewards), etc. |
| ASF Audits | `LR8R0L` | `ASF` |

### Category IDs

| Category | ID | Description |
|----------|-----|-------------|
| Production | `PROD` | Development work |
| Audit | `AUDIT` | Architecture audits |
| Leave | `LEAVE` | Time off |

### Billable Types

| Type | ID | Description |
|------|-----|-------------|
| Billable | `BILLABLE` | Client-billable work |
| Internal | `W` | Internal/non-billable work |

---

## Example Prompts

### For GitHub Copilot CLI

```bash
# Create a week of timesheets
ghcs "Help me create timesheets for Jan 12-16, 2026. Monday & Friday: SSW.Rewards (SSW/4BPT0L, PROD, 09:00-18:00). Tue-Thu: ASF Audits (LR8R0L/ASF, AUDIT, 09:00-17:00). Employee ID is JEK."

# Scan git for activity
ghcs "Scan /Users/me/git/MyProject for commits in the last 7 days by author 'myname'"

# Check pending confirmations
ghcs "List all pending timesheet confirmations"
```

### For Claude Code

```
Create timesheets for this week:
- Employee: JEK
- Monday: SSW.Rewards (client SSW, project 4BPT0L), 9am-6pm, category PROD
- Tuesday-Thursday: ASF Audits (client LR8R0L, project ASF), 9am-5pm, category AUDIT, billable at $325/hr
- Friday: SSW.Rewards again

Use dry-run mode and show me what will be created.
```

---

## Persona-Based Usage

See [.agents/skills/skills.md](.agents/skills/skills.md) for persona-specific workflows:

- **Developer**: Git-focused, minimal manual entry
- **Consultant**: CRM integration, multi-project management
- **Administrator**: Compliance, leave management, oversight

---

## Troubleshooting

### "Write operations blocked"
You're connected to production. Use local environment for testing writes.

### "Invalid confirmation passphrase"
Check your `TIMEPRO_CONFIRM_PHRASE` environment variable.

### "Confirmation not found"
Confirmations expire after 24 hours. Use `list_pending_confirmations` to see active ones.

### SSL Certificate Errors
For local development with self-signed certs, the MCP server automatically bypasses SSL validation for `local-*` hostnames.

---

## Version

- **MCP Server**: 0.1.0
- **Protocol**: Model Context Protocol (stdio transport)
- **Runtime**: .NET 10
