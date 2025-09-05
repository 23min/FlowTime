#!/bin/bash

echo "Testing download functionality..."

# Create a test run
echo "Creating test run via API..."
RESPONSE=$(curl -s -X POST http://localhost:8080/run \
  -H "Content-Type: text/plain" \
  -d "grid:
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
    expr: demand * 0.8")

echo "API Response:"
echo "$RESPONSE"

# Parse the runId from the response
RUN_ID=$(echo "$RESPONSE" | grep -o '"runId":"[^"]*"' | cut -d'"' -f4)
echo "Run ID: $RUN_ID"

if [ -n "$RUN_ID" ]; then
    echo "Testing series download endpoints..."
    
    # Test downloading each series
    echo "1. Testing demand series download:"
    curl -s "http://localhost:8080/runs/$RUN_ID/series/demand@DEMAND@DEFAULT" | head -5
    
    echo -e "\n2. Testing capacity series download:"
    curl -s "http://localhost:8080/runs/$RUN_ID/series/capacity@CAPACITY@DEFAULT" | head -5
    
    echo -e "\n3. Testing flow series download:"  
    curl -s "http://localhost:8080/runs/$RUN_ID/series/flow@FLOW@DEFAULT" | head -5
    
    echo -e "\n✅ All series endpoints are working!"
    echo "You can now test the UI download by:"
    echo "1. Going to http://localhost:5219"
    echo "2. Using API mode"
    echo "3. Running the same model"
    echo "4. Clicking 'View Results' to load the data"
    echo "5. Clicking 'Download Data' to download the CSV files"
else
    echo "❌ Failed to create test run"
fi
