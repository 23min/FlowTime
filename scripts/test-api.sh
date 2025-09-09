#!/bin/bash
# Test script for FlowTime.Sim API

echo "=== FlowTime.Sim API Test ==="

# Change to the correct directory
cd /workspaces/flowtime-sim-vnext

echo "1. Checking current directory structure..."
echo "Current directory: $(pwd)"
echo "Service directory: $(ls -la src/FlowTime.Sim.Service/)"

echo ""
echo "2. Building the project..."
dotnet build src/FlowTime.Sim.Service

echo ""
echo "3. Starting service in background..."
dotnet run --project src/FlowTime.Sim.Service &
SERVICE_PID=$!
echo "Service PID: $SERVICE_PID"

# Wait for service to start
echo "4. Waiting for service to start..."
sleep 5

echo ""
echo "5. Testing health endpoint..."
curl -s http://localhost:8081/healthz | jq . 2>/dev/null || curl -s http://localhost:8081/healthz

echo ""
echo "6. Testing simulation run..."
echo "Sending simulation request..."

# Create a simple simulation spec
cat > test-sim.yaml << 'EOF'
schemaVersion: 1
grid:
  bins: 3
  binMinutes: 60
arrivals:
  kind: const
  values: [10, 20, 15]
EOF

echo "Simulation spec:"
cat test-sim.yaml

echo ""
echo "Making API call..."
RESPONSE=$(curl -s -X POST http://localhost:8081/sim/run \
  -H "Content-Type: text/plain" \
  -d @test-sim.yaml)

echo "Response: $RESPONSE"

echo ""
echo "7. Checking for created directories..."
echo "Looking for data directory in service folder:"
ls -la src/FlowTime.Sim.Service/ | grep -E "(data|runs)"

echo ""
echo "Looking for any new directories:"
find src/FlowTime.Sim.Service -type d -name "*" -newer test-sim.yaml 2>/dev/null | head -10

echo ""
echo "8. Stopping service..."
kill $SERVICE_PID 2>/dev/null
wait $SERVICE_PID 2>/dev/null

# Clean up
rm -f test-sim.yaml

echo ""
echo "=== Test Complete ==="
