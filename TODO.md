# TimePro MCP Server - Project Status

## ✅ Completed

### Core Infrastructure
- [x] **Project Setup**: Initial .NET 10 project structure with MCP integration.
- [x] **Configuration**: Implemented `TimeProSettings` and `GitHubSettings`.
- [x] **Dependency Injection**: Registered services (`TimeProService`, `AgendaService`, `GitScanningService`, etc.) in `Program.cs`.
- [x] **Build Fixes**: Resolved strict type issues in `AgendaService.cs` (DateOnly/DateTime) and model property mismatches (`TimesheetItem`).

### Services
- [x] **TimeProService**:
  - Implemented `GetTimesheetsAsync` (with date-loop fallback for single-day endpoint).
  - Implemented `GetEmployeeSettingsAsync` to fetch user working hours.
  - Implemented `GetAppointmentsAsync` for CRM bookings.
- [x] **LocalGitService**: Implemented local repository scanning (`git log`).
- [x] **GitHubService**: Implemented GitHub user event scanning.
- [x] **AgendaService**:
  - Implemented `GenerateAgendaAsync` to combine Timesheets, CRM, and Git data.
  - Added logic to respect User Settings (e.g. 09:00 - 18:00) for agenda items to avoid accidental overtime in suggestions.
  - Implemented `ExportToMarkdown`.
- [x] **ConfirmationService**: Added support for Dry-Run operations (saving requests to JSON).

### Testing & Validation
- [x] **Integration Tests**: Created `ProductionReplayTests.cs` to test agenda generation against live/mocked data.
- [x] **Live API Verification**: Verified `curl` connectivity to `ssw.sswtimepro.com` and updated service configuration.
- [x] **Models**: Restored missing `WorkPatterns` model.

### Agent Capabilities
- [x] **Skills**: Created `.agents/skills/skills.md` defining "Dev", "Consultant", and "Admin" personas.

---

## 🚧 In Progress / Next Steps

### Features
- [ ] **Azure DevOps Integration**: Implement `IAzureDevOpsService` to scan ADO commits/PRs (similar to GitHubService).
- [ ] **Interactive Git Scanning**: Implement logic for `GitScanSettings.Ask` preference (handling user prompts during generation).
- [ ] **Natural Language Agenda**: Implement features to allow users to describe their day and have it converted to timesheets ("Natural Agenda Building").
- [ ] **Multi-Project Refinement**: Improve `AgendaService` to better split time blocks when multiple projects are detected in a single day (currently groups daily).

### Fixes & Improvements
- [ ] **Fix Production Timesheet Fetch**: Investigate why `GetTimesheetListViewModel` returns empty list in integration tests (likely `d.TimeID` vs `string`/`int` strict JSON deserialization or User-Agent filtering).
- [ ] **App Name Configuration**: Allow configuring `x-timepro-api-name` (currently hardcoded to `JK-TimePro-MCP`) to mimic "Insomnia" or other allowed clients if needed.

### Documentation
- [ ] **README Updates**: Document the new `ProductionReplayTests` and how to run them.
