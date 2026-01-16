---
name: TimePro Agenda Skills
description: Persona-based skills for managing TimePro timesheets using the Agenda tools.
version: 1.0.0
---

# TimePro Agenda Skills

This skill set provides specialized workflows for different user personas to manage their timesheets effectively using the SSW TimePro MCP server.

## Personas

### 1. The Developer (`dev`)
**Focus:** Code activity, Git commits, minimal manual entry.
**Style:** `oneDayOneProject`, `keepItSimple`

**Workflow:**
1.  **Scan Git:** Use `GitScanTools` to find activity in local repositories (`SSW.Rewards.Mobile`, `ASF/HubX`, etc.).
2.  **Generate Agenda:** Call `GenerateWeeklyAgenda` with `includeExisting=true`, `includeSuggested=true`, and `localGitPaths` pointing to active repos.
3.  **Review:** Check the markdown export for accuracy.
4.  **Confirm:** Bulk create timesheets based on the generated agenda.

**Properties:**
-   `role`: "dev"
-   `complexity`: "simple"
-   `gitScanning`: "always"

### 2. The Consultant (`consultant`)
**Focus:** Validating against CRM bookings, client work, multiple projects.
**Style:** `jugglingProjects`, `complexAgenda`

**Workflow:**
1.  **Check CRM:** Ensure `includeCrm=true` is set to pull client meetings.
2.  **Analyze Patterns:** Use `AnalyzeWorkPatterns` to identify frequent clients and projects from the last 14 days.
3.  **Generate Agenda:** Combine CRM data with suggested timesheets.
4.  **Fill Gaps:** Manually review "NeedsAttention" days where billable hours < 8.
5.  **Confirm:** Create timesheets after validating client allocations.

**Properties:**
-   `role`: "consultant"
-   `complexity`: "high"
-   `crmIntegration`: "critical"

### 3. The Administrator (`admin`)
**Focus:** Oversight, leave management, team patterns (if applicable), consistency.
**Style:** `completeSchedule`, `microManagerFriendly`

**Workflow:**
1.  **Pattern Analysis:** frequent checks on work patterns (`AnalyzeWorkPatterns`) to ensure consistency.
2.  **Agenda Generation:** Use `GenerateWeeklyAgenda` to view the full week's layout, ensuring no gaps (`completeSchedule`).
3.  **Leave Management:** Use `GetLeaveUrl` for any non-working days.
4.  **Documentation:** `ExportAgendaToMarkdown` for record-keeping before submission.

**Properties:**
-   `role`: "admin"
-   `compliance`: "strict"
-   `reporting`: "detailed"

## Usage

To use these skills, referencing the specific persona properties can tailor the helper agent's behavior:

-   **Development Mode:** "Act as the Developer persona. prioritize git commits and single-project days."
-   **Consulting Mode:** "Act as the Consultant persona. Ensure all CRM bookings are accounted for and projects are correctly split."
-   **Admin Mode:** "Act as the Admin persona. Verify 8-hour days and check for any missing categories."
