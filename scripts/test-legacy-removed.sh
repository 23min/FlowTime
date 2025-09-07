#!/bin/bash

echo "Testing that legacy endpoints are removed..."

# Start the service in background
echo "Starting FlowTime-Sim service on port 8082..."
cd /workspaces/flowtime/flowtime-sim-vnext
dotnet run --project src/FlowTime.Sim.Service --urls http://localhost:8082 &
SERVICE_PID=$!

# Wait for service to start
echo "Waiting for service to start..."
sleep 5

echo -e "\n=== Testing legacy endpoints (should return 404) ==="

echo "Testing /sim/run (should be 404):"
curl -s -w "HTTP Status: %{http_code}\n" -X POST \
  -H "Content-Type: text/plain" \
  -d "test" \
  http://localhost:8082/sim/run

echo -e "\nTesting /sim/scenarios (should be 404):"
curl -s -w "HTTP Status: %{http_code}\n" http://localhost:8082/sim/scenarios

echo -e "\nTesting /sim/catalogs (should be 404):"
curl -s -w "HTTP Status: %{http_code}\n" http://localhost:8082/sim/catalogs

echo -e "\nTesting /sim/overlay (should be 404):"
curl -s -w "HTTP Status: %{http_code}\n" http://localhost:8082/sim/overlay

# Clean up
echo -e "\nStopping service..."
kill $SERVICE_PID 2>/dev/null

echo "Legacy endpoint testing complete!"
