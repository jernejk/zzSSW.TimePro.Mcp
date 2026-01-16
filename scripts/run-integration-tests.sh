#!/bin/bash
# Script to run integration tests against a live TimePro instance

# Load secrets from Infisical if available
PROJECT_ID="f45d9554-1eb2-417b-819a-5954d6f32f58"

if command -v infisical &> /dev/null; then
    echo "Loading secrets from Infisical..."
    export TIMEPRO_API_KEY=$(infisical secrets get "TimePro-SSW-ApiKey" --projectId "$PROJECT_ID" --plain 2>/dev/null)
    export TIMEPRO_BASE_URL="https://api.sswtimepro.com/"
    export TIMEPRO_TENANT_ID="ssw"
fi

# Enable integration tests
export RUN_INTEGRATION_TESTS=true

# Run integration tests
echo "Running integration tests..."
dotnet test --filter "Category=Integration" "$@"
