#!/bin/bash

echo "=== Verifying Legacy Variables Are Ignored ==="
echo ""

# Clean up previous tests
rm -rf /tmp/legacy-test /tmp/should-be-ignored

echo "1. Setting ONLY legacy environment variables (should NOT be used)"
unset FLOWTIME_SIM_DATA_DIR  # Make sure primary variable is not set
export FLOWTIME_SIM_RUNS_ROOT="/tmp/should-be-ignored/runs"
export FLOWTIME_SIM_CATALOGS_ROOT="/tmp/should-be-ignored/catalogs"

echo "   FLOWTIME_SIM_DATA_DIR: $FLOWTIME_SIM_DATA_DIR (unset)"
echo "   FLOWTIME_SIM_RUNS_ROOT: $FLOWTIME_SIM_RUNS_ROOT (should be ignored)"
echo "   FLOWTIME_SIM_CATALOGS_ROOT: $FLOWTIME_SIM_CATALOGS_ROOT (should be ignored)"

echo ""
echo "2. Starting service - should use defaults, NOT legacy variables"
cd /workspaces/flowtime-sim-vnext

timeout 15s dotnet run --project src/FlowTime.Sim.Service &
SERVICE_PID=$!
sleep 5

echo "3. Making API call..."
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
echo "4. Verification Results:"

if [ -d "/tmp/should-be-ignored" ]; then
    echo "   ❌ FAILED: Legacy directory created at /tmp/should-be-ignored"
    ls -la /tmp/should-be-ignored/
else
    echo "   ✅ PASSED: Legacy directories NOT created"
fi

if [ -d "./data/runs" ]; then
    echo "   ✅ PASSED: Using default location ./data/runs"
    echo "   Latest run:"
    ls -la ./data/runs/ | tail -2
else
    echo "   ❓ Default location ./data/runs not found - check service working directory"
fi

echo ""
echo "✅ Legacy variable test complete!"
echo "   Legacy variables should be completely ignored"
echo "   System should use defaults when FLOWTIME_SIM_DATA_DIR is not set"
