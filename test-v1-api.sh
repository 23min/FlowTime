#!/bin/bash

echo "Testing FlowTime-Sim v1 API..."

# Start the service in background
echo "Starting FlowTime-Sim service on port 8081..."
cd /workspaces/flowtime/flowtime-sim-vnext
dotnet run --project src/FlowTime.Sim.Service --urls http://localhost:8081 &
SERVICE_PID=$!

# Wait for service to start
echo "Waiting for service to start..."
sleep 5

# Test endpoints
echo -e "\n=== Testing legacy health endpoint ==="
curl -s http://localhost:8081/healthz | jq .

echo -e "\n=== Testing v1 health endpoint ==="
curl -s http://localhost:8081/v1/healthz | jq .

echo -e "\n=== Testing v1 sim run endpoint ==="
curl -s -X POST \
  -H "Content-Type: text/plain" \
  -d "$(cat examples/m0.const.sim.yaml)" \
  http://localhost:8081/v1/sim/run | jq .

echo -e "\n=== Testing v1 scenarios endpoint ==="
curl -s http://localhost:8081/v1/sim/scenarios | jq .

echo -e "\n=== Testing v1 catalogs endpoint ==="
curl -s http://localhost:8081/v1/sim/catalogs | jq .

# Clean up
echo -e "\nStopping service..."
kill $SERVICE_PID 2>/dev/null

echo "API testing complete!"
