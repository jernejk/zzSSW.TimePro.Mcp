# Test Scenario: Weekly Timesheet Creation

## Scenario
Create timesheets for the week of 12-16 Jan 2026 based on:
- Monday & Friday: SSW.Rewards work (get descriptions from git)
- Tue-Thu: Northwind Audits (from CRM bookings)

## Test Commands

### 1. Get Suggestions for the Week
```bash
mcp-cli ssw-timepro/suggest_timesheets '{"employeeId":"JEK","startDate":"2026-01-12","endDate":"2026-01-16"}'
```

**Expected Response:**
- Shows existing timesheets (if any)
- Shows suggested timesheets with `suggestedTimesheetId`
- Shows CRM bookings for days without suggestions
- Clear `action` per day: `none`, `choose`, or `manual`

### 2. Accept a Suggested Timesheet (with notes)
```bash
mcp-cli ssw-timepro/accept_suggested_timesheet '{"suggestedTimesheetId":217510,"notes":"Northwind Architecture Audit"}'
```

### 3. Get Client Rate (for manual timesheets)
```bash
mcp-cli ssw-timepro/get_client_rate '{"employeeId":"JEK","clientId":"LR8R0L"}'
```

### 4. Create Manual Timesheet (dry-run)
```bash
mcp-cli ssw-timepro/create_timesheet '{
  "employeeId": "JEK",
  "clientId": "SSW",
  "projectId": "4BPT0L",
  "date": "2026-01-12",
  "startTime": "09:00",
  "endTime": "18:00",
  "categoryId": "PROD",
  "locationId": "SSW",
  "notes": "SSW.Rewards development",
  "timeLess": 1,
  "billableId": "W",
  "sellPrice": 0,
  "dryRun": true
}'
```

### 5. Confirm Operation
```bash
mcp-cli ssw-timepro/confirm_operation '{"confirmationId":"<ID_FROM_STEP_4>"}'
```

## Copilot CLI Test

Run this prompt with Copilot:
```bash
copilot -p "Using the ssw-timepro MCP tools, help me create timesheets for Jan 12-16, 2026.
First check what suggestions are available using suggest_timesheets.
For days with suggestions, accept them using accept_suggested_timesheet.
For days without, create timesheets manually." --model claude-sonnet-4
```

## Expected Output
- `suggest_timesheets` shows 5 working days with status
- Days with suggestions: accept using `accept_suggested_timesheet`
- Days without: create using `create_timesheet` with `dryRun=true`
- Confirm pending operations
