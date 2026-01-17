---
name: TimePro Agenda Skills
description: Persona-based skills for managing TimePro timesheets using the MCP tools.
version: 2.0.0
---

# TimePro Agenda Skills

This skill set provides specialized workflows for different user personas to manage their timesheets effectively using the SSW TimePro MCP server.

## Personas

### 1. The Developer (`dev`)
**Focus:** Code activity, Git commits, minimal manual entry.
**Style:** `oneDayOneProject`, `keepItSimple`

**Workflow:**
1.  **Get Suggestions:** Call `SuggestTimesheets` to see what's already logged and what's recommended.
2.  **Accept Suggestions:** Use `AcceptSuggestedTimesheet(id, notes?)` for days with suggestions.
3.  **Manual Entry:** Use `CreateTimesheet` for days without suggestions.
4.  **Confirm:** Execute pending confirmations.

**Properties:**
-   `role`: "dev"
-   `complexity`: "simple"

### 2. The Consultant (`consultant`)
**Focus:** Validating against CRM bookings, client work, multiple projects.
**Style:** `jugglingProjects`, `billableFirst`

**Workflow:**
1.  **Get Suggestions:** Call `SuggestTimesheets` - includes CRM bookings automatically.
2.  **Check Rates:** Use `GetClientRate` to verify billable rates for clients.
3.  **Accept or Create:** Accept suggestions with notes, or create timesheets manually.
4.  **Confirm:** Execute pending confirmations.

**Properties:**
-   `role`: "consultant"
-   `complexity`: "high"
-   `crmIntegration`: "critical"

### 3. The Administrator (`admin`)
**Focus:** Oversight, leave management, consistency.
**Style:** `completeSchedule`, `compliant`

**Workflow:**
1.  **Pattern Analysis:** Use `AnalyzeWorkPatterns` to ensure consistency.
2.  **Get Suggestions:** Use `SuggestTimesheets` to view the full week's status.
3.  **Leave Management:** Use `GetLeaveUrl` for any non-working days.

**Properties:**
-   `role`: "admin"
-   `compliance`: "strict"
-   `reporting`: "detailed"

## Usage

To use these skills, referencing the specific persona properties can tailor the helper agent's behavior:

-   **Development Mode:** "Act as the Developer persona. Keep it simple, one project per day."
-   **Consulting Mode:** "Act as the Consultant persona. Ensure billable rates are correct."
-   **Admin Mode:** "Act as the Admin persona. Verify 8-hour days and check for gaps."
