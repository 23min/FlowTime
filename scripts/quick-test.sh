#!/bin/bash
echo "Testing FlowTime.Sim API..."

cd /workspaces/flowtime-sim-vnext

echo "Starting service..."
dotnet run --project src/FlowTime.Sim.Service &
SERVICE_PID=$!

sleep 3

echo "Making API call with valid spec..."
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
  id: testNode")

echo "Response: $RESPONSE"

echo ""
echo "Checking for data directory..."
ls -la src/FlowTime.Sim.Service/ | grep -E "(data|runs)"

echo ""
echo "Looking for any recently created directories..."
find src/FlowTime.Sim.Service -type d -newer quick-test.sh 2>/dev/null

kill $SERVICE_PID
wait $SERVICE_PID 2>/dev/null

echo "Done."
