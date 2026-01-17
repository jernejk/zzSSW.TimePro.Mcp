---
name: TimePro Timesheet Management
description: AI-driven timesheet management using SSW TimePro MCP tools.
version: 3.1.0
---

# TimePro Timesheet Management

This skill enables AI-driven timesheet management. The MCP tools provide raw data, and the AI makes decisions about what timesheets to create.

## Quick Start

```
recommend_week()   # See what needs to be done for the week
recommend_day()    # Get details for a specific day
```

## Available MCP Tools

### Recommendation Tools
| Tool | Purpose |
|------|---------|
| `recommend_day` | Get recommendation for a single day |
| `recommend_week` | Get recommendations for the entire week |

### Creating Timesheets
| Tool | When to Use |
|------|-------------|
| `accept_suggested_timesheet` | When `suggestedTimesheetId` exists in response |
| `create_timesheet` | When no suggestion exists (use `recentProjects`) |

### Other Tools
| Tool | Purpose |
|------|---------|
| `search_clients` | Find clients by name |
| `get_projects_for_client` | Get projects for a client |
| `get_client_rate` | Get billable rate for a client |
| `scan_local_repository` | Get git commits from a local repo |
| `confirm_operation` | Execute a dry-run operation |

## Understanding the Response

When you call `recommend_day` or `recommend_week`, you get:

### `existing`
Confirmed timesheets already saved. Count these hours as done.

### `suggested` ⭐ IMPORTANT
Pre-filled templates from TimePro. Each has a `suggestedTimesheetId`.

**When `suggested` exists, you MUST use `accept_suggested_timesheet(suggestedTimesheetId)`**

Why:
- These are generated from historical patterns
- They impact TimePro reports correctly
- The `suggestedTimesheetId` is a template reference that gets converted to a real timesheet

### `crmBookings`
Calendar appointments from CRM. Shows what client work was booked.

### `recentProjects`
Fallback - use only when no `suggested` exists.

## Writing Good Notes

### DO ✅
- Write a **brief summary** of what was accomplished
- Focus on outcomes and deliverables
- Use natural language: "Implemented notification UX improvements"
- Keep it concise: 1-2 sentences max

### DON'T ❌
- Don't include timestamps (e.g., "11:55", "12:43")
- Don't list every single commit message
- Don't use git commit prefixes (💄, ✨, 🐛)
- Don't include PR numbers unless specifically relevant

### Examples

**Bad:**
```
- 11:55 💄 Update SendNotification UI - Improve placeholder text
- 12:11 💄 Improve UX for achievement selection
- 12:43 💄 Enhance target autocomplete width
```

**Good:**
```
Improved SendNotification UI - enhanced placeholder text, achievement selection UX, and autocomplete layout
```

**Even Better (if user provides context):**
```
SSW.Rewards: Notification improvements for v3.7 release
```

## AI Decision Logic

For each day with `hoursNeeded > 0`:

```
IF suggested array has items:
    → accept_suggested_timesheet(suggestedTimesheetId, notes?, location?)

ELSE IF crmBookings exist:
    → create_timesheet(...) using booking's clientId/projectId

ELSE IF recentProjects exist:
    → create_timesheet(...) using top recentProject

ELSE:
    → Ask user which project
```

## Weekly Timesheet Generation

When generating timesheets for an entire week, use **load balancing** to distribute work fairly.

### The Problem
Git commits don't always reflect the day work was done:
- Code review happens the day after writing
- PRs get merged the next day
- Some days have many commits, others have none
- Work spans multiple days but commits land on one day

### Load Balancing Strategy

1. **Scan the full week first** - Get all commits for Mon-Fri before assigning
2. **Identify project clusters** - Group commits by project across the week
3. **Distribute evenly** - If Project X has commits on Tue-Thu but not Mon, and user worked on it all week, spread the work

**Example:**
```
Git commits show:
- Monday: 0 commits (SSW.Rewards)
- Tuesday: 15 commits (SSW.Rewards)
- Wednesday: 2 commits (SSW.Rewards)

Better timesheet allocation:
- Monday: "SSW.Rewards: Started notification feature work"
- Tuesday: "SSW.Rewards: Implemented notification UI components"
- Wednesday: "SSW.Rewards: Bug fixes and code review"
```

### Implementation Steps

1. Call `recommend_week()` to see all days
2. Call `scan_local_repository(days: 7)` to get all commits
3. Group commits by project
4. For days with 0 commits but same project as adjacent days:
   - Attribute work to that project
   - Use notes like "Continued [project] work" or "Code review and planning"
5. For days with many commits:
   - Summarize the key changes, don't list everything

### Ask User When Unclear
If the work pattern is ambiguous (multiple projects, gaps in activity), ask:
> "I see commits for SSW.Rewards on Tuesday and Thursday, but none on Monday/Wednesday. Did you work on SSW.Rewards all week, or were those days different projects?"

## Creating Timesheets

### Option 1: From Suggestion (Preferred)
When `suggested` array has items:
```
accept_suggested_timesheet(
    suggestedTimesheetId: 217510,
    notes: "Brief work summary",
    location: "Home"
)
```

### Option 2: Manual Creation
When no suggestion exists:
```
create_timesheet(
    employeeId: "JEK",
    clientId: "SSW",
    projectId: "4BPT0L",
    date: "2026-01-13",
    startTime: "09:00",
    endTime: "18:00",
    categoryId: "DEV",
    locationId: "Home",
    notes: "Brief work summary",
    timeLess: 1,
    billableId: "W",
    dryRun: true
)
```

Then confirm with:
```
confirm_operation(confirmationId)
```

## Standard Values

| Field | Value | Notes |
|-------|-------|-------|
| `startTime` | `09:00` | Standard start |
| `endTime` | `18:00` | For 8 net hours |
| `timeLess` | `1` | 1 hour lunch break |
| `categoryId` | `DEV` | Development work |
| `locationId` | `Home` | Remote work |
| `billableId` | `W` | Internal (non-billable) |

### For Billable Client Work
```
get_client_rate(employeeId, clientId)
→ use response.rate as sellPrice
→ set billableId: "BILLABLE"
```

## Example Workflow

**User:** "Create my timesheets for this week"

**AI Steps:**
1. `recommend_week()` → see all days
2. `scan_local_repository(days: 7)` → get all commits for context
3. Analyze commit patterns across the week
4. For each day with `hoursNeeded > 0`:
   - Has `suggested`? → `accept_suggested_timesheet(id, notes)`
   - No suggestion? → `create_timesheet(...)` with summarized notes
5. Apply load balancing if commits are uneven
6. `confirm_operation(id)` for each pending confirmation

## Edge Cases

| Scenario | Action |
|----------|--------|
| Weekend | Skip (returns `isWeekend: true`) |
| Leave day | Don't create timesheet |
| Multiple projects | Ask user how to split |
| No suggestions, no recent projects | Ask user which project |
| Day with 0 commits but same project as neighbors | Attribute to that project |
| Day with 20+ commits | Summarize key themes, don't list all |
