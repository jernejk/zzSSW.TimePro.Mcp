#!/bin/bash
# Test script for timesheet creation workflow
# Uses mcp-cli to test the SSW.TimePro.Mcp server

set -e

echo "=== TimePro MCP Timesheet Workflow Test ==="
echo ""

# Configuration
EMPLOYEE_ID="JEK"
START_DATE="2026-01-12"
END_DATE="2026-01-16"
REWARDS_REPO="/Users/jk/Developer/git/SSW.Rewards.Mobile"

echo "1. Generating weekly agenda for $START_DATE to $END_DATE..."
mcp-cli ssw-timepro/generate_weekly_agenda "{\"employeeId\":\"$EMPLOYEE_ID\",\"startDate\":\"$START_DATE\",\"endDate\":\"$END_DATE\"}" 2>&1 | jq -r '.days[] | "\(.date) (\(.dayOfWeek)): \(.status) - \(.totalHours)h - \(.items[0].source // "no source")"'

echo ""
echo "2. Scanning SSW.Rewards git repository for commits..."
mcp-cli ssw-timepro/scan_local_repository "{\"repositoryPath\":\"$REWARDS_REPO\",\"days\":14,\"author\":\"jernej\"}" 2>&1 | jq -r '.dailyActivity[] | select(.date >= "2026-01-12" and .date <= "2026-01-16") | "\(.date): \(.totalCommits) commits"'

echo ""
echo "3. Getting existing timesheets..."
mcp-cli ssw-timepro/get_timesheets_by_date_range "{\"employeeId\":\"$EMPLOYEE_ID\",\"startDate\":\"$START_DATE\",\"endDate\":\"$END_DATE\"}" 2>&1 | jq -r '.timesheets[] | "\(.date | split("T")[0]): \(.project) - \(.totalTime)h"'

echo ""
echo "4. Listing any pending confirmations..."
mcp-cli ssw-timepro/list_pending_confirmations '{"status":"Pending"}' 2>&1 | jq -r '.confirmations[] | "\(.id): \(.description) (\(.status))"'

echo ""
echo "=== Test Complete ==="
