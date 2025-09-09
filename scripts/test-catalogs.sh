#!/bin/bash

echo "Testing FlowTime.Sim Catalogs API..."
echo "Starting service..."

# Start the service in background
cd /workspaces/flowtime-sim-vnext
dotnet run --project src/FlowTime.Sim.Service &
SERVICE_PID=$!

# Wait for service to be ready
echo "Waiting for service to start..."
sleep 5

# Test catalogs endpoint
echo "Testing /sim/catalogs endpoint..."
curl -s http://localhost:8081/sim/catalogs | jq .

# Test specific catalog
echo "Testing /sim/catalogs/demo-system endpoint..."
curl -s http://localhost:8081/sim/catalogs/demo-system | jq .

# Cleanup
echo "Shutting down service..."
kill $SERVICE_PID 2>/dev/null

echo "Done."
