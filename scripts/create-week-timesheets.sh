#!/bin/bash
# Create timesheets for a full week using MCP dry-run workflow
# Usage: ./create-week-timesheets.sh [confirm]
# Add "confirm" argument to actually execute the confirmations

set -e

CONFIRM_MODE=${1:-""}
EMPLOYEE_ID="JEK"

# Colors for output
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

echo -e "${BLUE}=== Weekly Timesheet Creation (Dry-Run Mode) ===${NC}"
echo ""

# Get git commits for description generation
echo -e "${YELLOW}Scanning git history for SSW.Rewards...${NC}"
MONDAY_COMMITS=$(mcp-cli ssw-timepro/scan_local_repository '{"repositoryPath":"/Users/jk/Developer/git/SSW.Rewards.Mobile","days":14,"author":"jernej"}' 2>&1 | jq -r '.dailyActivity[] | select(.date == "2026-01-12") | .commits[].subject' | head -5 | tr '\n' ', ' | sed 's/,$//')
FRIDAY_COMMITS=$(mcp-cli ssw-timepro/scan_local_repository '{"repositoryPath":"/Users/jk/Developer/git/SSW.Rewards.Mobile","days":14,"author":"jernej"}' 2>&1 | jq -r '.dailyActivity[] | select(.date == "2026-01-16") | .commits[].subject' | head -5 | tr '\n' ', ' | sed 's/,$//')

echo "Monday commits: ${MONDAY_COMMITS:0:100}..."
echo "Friday commits: ${FRIDAY_COMMITS:0:100}..."
echo ""

# Array to store confirmation IDs
declare -a CONFIRMATION_IDS

# Monday - SSW.Rewards
echo -e "${GREEN}Creating Monday (Jan 12) - SSW.Rewards...${NC}"
RESULT=$(mcp-cli ssw-timepro/create_timesheet "{
  \"employeeId\": \"$EMPLOYEE_ID\",
  \"clientId\": \"SSW\",
  \"projectId\": \"4BPT0L\",
  \"date\": \"2026-01-12\",
  \"startTime\": \"09:00\",
  \"endTime\": \"18:00\",
  \"categoryId\": \"PROD\",
  \"locationId\": \"SSW\",
  \"notes\": \"${MONDAY_COMMITS:0:200}\",
  \"timeLess\": 1,
  \"billableId\": \"W\",
  \"sellPrice\": 0,
  \"dryRun\": true
}" 2>&1)
ID=$(echo "$RESULT" | jq -r '.confirmationId')
echo "$RESULT" | jq -r '"\(.preview)"'
CONFIRMATION_IDS+=("$ID")

# Tuesday - ASF Audits
echo -e "${GREEN}Creating Tuesday (Jan 13) - ASF Audits...${NC}"
RESULT=$(mcp-cli ssw-timepro/create_timesheet "{
  \"employeeId\": \"$EMPLOYEE_ID\",
  \"clientId\": \"LR8R0L\",
  \"projectId\": \"ASF\",
  \"date\": \"2026-01-13\",
  \"startTime\": \"09:00\",
  \"endTime\": \"17:00\",
  \"categoryId\": \"AUDIT\",
  \"locationId\": \"SSW\",
  \"notes\": \"ASF Architecture Audit\",
  \"timeLess\": 0,
  \"billableId\": \"BILLABLE\",
  \"sellPrice\": 325,
  \"dryRun\": true
}" 2>&1)
ID=$(echo "$RESULT" | jq -r '.confirmationId')
echo "$RESULT" | jq -r '"\(.preview)"'
CONFIRMATION_IDS+=("$ID")

# Wednesday - ASF Audits
echo -e "${GREEN}Creating Wednesday (Jan 14) - ASF Audits...${NC}"
RESULT=$(mcp-cli ssw-timepro/create_timesheet "{
  \"employeeId\": \"$EMPLOYEE_ID\",
  \"clientId\": \"LR8R0L\",
  \"projectId\": \"ASF\",
  \"date\": \"2026-01-14\",
  \"startTime\": \"09:00\",
  \"endTime\": \"17:00\",
  \"categoryId\": \"AUDIT\",
  \"locationId\": \"SSW\",
  \"notes\": \"ASF Architecture Audit - continued\",
  \"timeLess\": 0,
  \"billableId\": \"BILLABLE\",
  \"sellPrice\": 325,
  \"dryRun\": true
}" 2>&1)
ID=$(echo "$RESULT" | jq -r '.confirmationId')
echo "$RESULT" | jq -r '"\(.preview)"'
CONFIRMATION_IDS+=("$ID")

# Thursday - ASF Audits
echo -e "${GREEN}Creating Thursday (Jan 15) - ASF Audits...${NC}"
RESULT=$(mcp-cli ssw-timepro/create_timesheet "{
  \"employeeId\": \"$EMPLOYEE_ID\",
  \"clientId\": \"LR8R0L\",
  \"projectId\": \"ASF\",
  \"date\": \"2026-01-15\",
  \"startTime\": \"09:00\",
  \"endTime\": \"17:00\",
  \"categoryId\": \"AUDIT\",
  \"locationId\": \"SSW\",
  \"notes\": \"ASF Architecture Audit - wrap up\",
  \"timeLess\": 0,
  \"billableId\": \"BILLABLE\",
  \"sellPrice\": 325,
  \"dryRun\": true
}" 2>&1)
ID=$(echo "$RESULT" | jq -r '.confirmationId')
echo "$RESULT" | jq -r '"\(.preview)"'
CONFIRMATION_IDS+=("$ID")

# Friday - SSW.Rewards
echo -e "${GREEN}Creating Friday (Jan 16) - SSW.Rewards...${NC}"
RESULT=$(mcp-cli ssw-timepro/create_timesheet "{
  \"employeeId\": \"$EMPLOYEE_ID\",
  \"clientId\": \"SSW\",
  \"projectId\": \"4BPT0L\",
  \"date\": \"2026-01-16\",
  \"startTime\": \"09:00\",
  \"endTime\": \"18:00\",
  \"categoryId\": \"PROD\",
  \"locationId\": \"SSW\",
  \"notes\": \"${FRIDAY_COMMITS:0:200}\",
  \"timeLess\": 1,
  \"billableId\": \"W\",
  \"sellPrice\": 0,
  \"dryRun\": true
}" 2>&1)
ID=$(echo "$RESULT" | jq -r '.confirmationId')
echo "$RESULT" | jq -r '"\(.preview)"'
CONFIRMATION_IDS+=("$ID")

echo ""
echo -e "${BLUE}=== Summary ===${NC}"
echo "Created ${#CONFIRMATION_IDS[@]} dry-run confirmations:"
for id in "${CONFIRMATION_IDS[@]}"; do
  echo "  - $id"
done

echo ""
echo -e "${YELLOW}Pending confirmations:${NC}"
mcp-cli ssw-timepro/list_pending_confirmations '{"status":"Pending"}' 2>&1 | jq -r '.confirmations[] | "  \(.id): \(.description)"'

if [ "$CONFIRM_MODE" == "confirm" ]; then
  echo ""
  echo -e "${GREEN}=== Confirming all operations ===${NC}"
  for id in "${CONFIRMATION_IDS[@]}"; do
    echo "Confirming $id..."
    mcp-cli ssw-timepro/confirm_operation "{\"confirmationId\":\"$id\"}" 2>&1 | jq -r '"\(.success ? "✓" : "✗") \(.result.message // .message // .error)"'
  done
else
  echo ""
  echo -e "${YELLOW}To confirm these timesheets, run:${NC}"
  echo "  ./create-week-timesheets.sh confirm"
  echo ""
  echo "Or confirm individually:"
  for id in "${CONFIRMATION_IDS[@]}"; do
    echo "  mcp-cli ssw-timepro/confirm_operation '{\"confirmationId\":\"$id\"}'"
  done
fi

echo ""
echo -e "${BLUE}=== Done ===${NC}"
