#!/bin/bash
# Script to sync secrets from Infisical to .NET User Secrets

PROJECT_ID="f45d9554-1eb2-417b-819a-5954d6f32f58"
PROJECT_PATH="src/SSW.TimePro.Mcp.Server/SSW.TimePro.Mcp.Server.csproj"

echo "Fetching secrets from Infisical..."

# Get TimePro API Key
API_KEY=$(infisical secrets get "TimePro-SSW-ApiKey" --projectId "$PROJECT_ID" --plain 2>/dev/null)
if [ -n "$API_KEY" ]; then
    dotnet user-secrets set "TimePro:ApiKey" "$API_KEY" --project "$PROJECT_PATH"
    echo "✓ Set TimePro:ApiKey"
else
    echo "⚠ Could not fetch TimePro-SSW-ApiKey"
fi

# Get GitHub Token
GITHUB_TOKEN=$(infisical secrets get "GITHUB_TOKEN" --projectId "$PROJECT_ID" --plain 2>/dev/null)
if [ -n "$GITHUB_TOKEN" ]; then
    dotnet user-secrets set "GitHub:Token" "$GITHUB_TOKEN" --project "$PROJECT_PATH"
    echo "✓ Set GitHub:Token"
else
    echo "⚠ Could not fetch GITHUB_TOKEN"
fi

# Get TimePro API URL (optional, for development)
API_URL=$(infisical secrets get "TimeProApiUrl" --projectId "$PROJECT_ID" --plain 2>/dev/null)
if [ -n "$API_URL" ]; then
    dotnet user-secrets set "TimePro:BaseUrl" "$API_URL" --project "$PROJECT_PATH"
    echo "✓ Set TimePro:BaseUrl"
fi

echo ""
echo "Done! Current secrets:"
dotnet user-secrets list --project "$PROJECT_PATH"
