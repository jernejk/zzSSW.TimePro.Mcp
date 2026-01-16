#!/bin/bash
# Quick test script using mcp-cli to verify all read endpoints are working
# Run from the project root directory

set -e

PROJECT_DIR="/Users/jk/Developer/git/SSW.TimePro.Mcp"
cd "$PROJECT_DIR"

echo "============================================="
echo "SSW TimePro MCP Server - Endpoint Tests"
echo "============================================="
echo ""

# Build first
echo "🔨 Building project..."
dotnet build --verbosity quiet

echo ""
echo "📋 Available tools:"
mcp-cli -d

echo ""
echo "============================================="
echo "Testing Read Endpoints"
echo "============================================="

echo ""
echo "1️⃣  GetTimesheetsByDays (3 days)"
mcp-cli ssw-timepro/get_timesheets_by_days '{"employeeId": "JEK", "takeDays": 3}'

echo ""
echo "2️⃣  GetTimesheetsByDateRange (current week)"
mcp-cli ssw-timepro/get_timesheets_by_date_range '{"employeeId": "JEK"}'

echo ""
echo "3️⃣  GetSuggestedTimesheets (today)"
mcp-cli ssw-timepro/get_suggested_timesheets '{"employeeId": "JEK"}'

echo ""
echo "4️⃣  SearchClients (SSW)"
mcp-cli ssw-timepro/search_clients '{"employeeId": "JEK", "searchText": "SSW"}'

echo ""
echo "5️⃣  GetCrmBookings (current week)"
mcp-cli ssw-timepro/get_crm_bookings '{"employeeId": "JEK"}'

echo ""
echo "6️⃣  GetTimesheetReferenceData"
mcp-cli ssw-timepro/get_timesheet_reference_data '{"employeeId": "JEK"}' | head -100
echo "... (truncated)"

echo ""
echo "============================================="
echo "Testing Dry Run Operations"
echo "============================================="

echo ""
echo "7️⃣  DeleteTimesheet (dry run)"
mcp-cli ssw-timepro/delete_timesheet '{"timesheetId": 12345, "dryRun": true}'

echo ""
echo "8️⃣  DeleteSuggestedTimesheet (dry run)"
mcp-cli ssw-timepro/delete_suggested_timesheet '{"suggestedTimesheetId": 12345, "dryRun": true}'

echo ""
echo "============================================="
echo "✅ All tests completed!"
echo "============================================="
