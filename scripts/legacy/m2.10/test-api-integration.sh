#!/bin/bash

# Check if API is already running
API_RUNNING=false
if curl -s http://localhost:8080/healthz > /dev/null 2>&1; then
    echo "✅ FlowTime API is already running"
    API_RUNNING=true
else
    # Start API in background
    echo "Starting FlowTime API..."
    cd /workspaces/flowtime-vnext
    dotnet run --project src/FlowTime.API --urls http://localhost:8080 > /dev/null 2>&1 &
    API_PID=$!
    
    # Wait for API to start
    echo "Waiting for API to start..."
    for i in {1..20}; do
        if curl -s http://localhost:8080/healthz > /dev/null 2>&1; then
            echo "✅ API started successfully"
            break
        fi
        sleep 1
        if [ $i -eq 20 ]; then
            echo "❌ API failed to start after 20 seconds"
            exit 1
        fi
    done
fi

# Test the API
echo "Testing POST /run endpoint..."
RESPONSE=$(curl -s -X POST http://localhost:8080/run \
  -H "Content-Type: text/plain" \
  -d 'grid:
  bins: 3
  binMinutes: 60
nodes:
  - id: demand
    kind: const
    values: [10, 20, 30]
  - id: capacity  
    kind: const
    values: [25, 25, 25]
  - id: flow
    kind: expr
    expr: "demand * 0.8"')

echo "API Response:"
echo "$RESPONSE"

# Parse the response to get runId and artifactsPath
RUN_ID=$(echo "$RESPONSE" | grep -o '"runId":"[^"]*"' | cut -d'"' -f4)
ARTIFACTS_PATH=$(echo "$RESPONSE" | grep -o '"artifactsPath":"[^"]*"' | cut -d'"' -f4)

echo "Run ID: $RUN_ID"
echo "Artifacts Path: $ARTIFACTS_PATH"

# Check if artifacts were created
if [ -n "$ARTIFACTS_PATH" ] && [ -d "$ARTIFACTS_PATH" ]; then
    echo "✅ Artifacts directory exists: $ARTIFACTS_PATH"
    echo "Contents:"
    ls -la "$ARTIFACTS_PATH"
else
    echo "❌ Artifacts directory not found: $ARTIFACTS_PATH"
fi

# Check if we can retrieve the run via the API
echo "Testing GET /runs/$RUN_ID/index..."
INDEX_RESPONSE=$(curl -s http://localhost:8080/runs/$RUN_ID/index)
echo "Index Response:"
echo "$INDEX_RESPONSE"

# Stop the API (only if we started it)
if [ "$API_RUNNING" = false ] && [ -n "$API_PID" ]; then
    echo "Stopping API..."
    kill $API_PID 2>/dev/null
    wait $API_PID 2>/dev/null
fi

echo "Test completed!"
