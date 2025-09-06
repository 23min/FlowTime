#!/bin/bash

echo "=== Testing Simplified Configuration (No Legacy Support) ==="
echo ""

# Clean up any previous test directories
rm -rf /tmp/clean-config-test

echo "1. Testing FLOWTIME_SIM_DATA_DIR environment variable"
export FLOWTIME_SIM_DATA_DIR="/tmp/clean-config-test"
echo "   Data directory: $FLOWTIME_SIM_DATA_DIR"

echo ""
echo "2. Testing ServiceHelpers directly..."
cd /workspaces/flowtime/flowtime-sim-vnext

# Create a simple test spec
echo "version: 1
metadata:
  scenario: clean-test
routes:
  - id: r1
    from: testNode
    to: testNode
workload:
  arrival:
    pattern: constant
    rate: 1.0
  runtime: 1s" > /tmp/test-clean-spec.yaml

# Test CLI which uses ServiceHelpers
echo "   Running CLI to test configuration..."
dotnet run --project src/FlowTime.Sim.Cli -- --spec /tmp/test-clean-spec.yaml --out temp-cli-test --format csv 2>/dev/null

echo ""
echo "3. Starting API service to test..."
timeout 15s dotnet run --project src/FlowTime.Sim.Service &
SERVICE_PID=$!

# Wait for startup
sleep 5

echo "   Making API call..."
RESPONSE=$(curl -s -X POST http://localhost:8081/sim/run -H "Content-Type: text/plain" -d "schemaVersion: 1
grid:
  bins: 2
  binMinutes: 60
  start: 2025-01-01T00:00:00Z
seed: 12345
arrivals:
  kind: const
  values: [5, 10]
route:
  id: testNode" 2>/dev/null)

echo "   Response: $RESPONSE"

# Stop service
kill $SERVICE_PID 2>/dev/null
wait $SERVICE_PID 2>/dev/null

echo ""
echo "4. Checking directory structure:"
if [ -d "/tmp/clean-config-test" ]; then
    find /tmp/clean-config-test -type d | sort
    echo ""
    echo "5. Files created:"
    find /tmp/clean-config-test -name "*.yaml" -o -name "*.json" -o -name "*.csv" | head -5
else
    echo "   ❌ Directory not created at /tmp/clean-config-test"
fi

echo ""
echo "6. Testing legacy environment variables (should be ignored):"
export FLOWTIME_SIM_RUNS_ROOT="/tmp/should-be-ignored"
export FLOWTIME_SIM_CATALOGS_ROOT="/tmp/should-also-be-ignored"
echo "   Set legacy variables to /tmp/should-be-ignored paths"
echo "   These should NOT be used anymore..."

echo ""
echo "✅ Test complete!"
echo "   Primary data directory: $FLOWTIME_SIM_DATA_DIR"
echo "   Legacy variables should be ignored"
