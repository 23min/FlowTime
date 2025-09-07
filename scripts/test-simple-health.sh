#!/bin/bash

echo "=== FlowTime-Sim Enhanced Simple Health Testing ==="

cd /workspaces/flowtime/flowtime-sim-vnext

# Start service
echo "Starting FlowTime-Sim service..."
dotnet run --project src/FlowTime.Sim.Service --urls http://localhost:8081 > simple-health-test.log 2>&1 &
SERVICE_PID=$!

# Wait for startup
echo "Waiting for service to start..."
sleep 5

# Check if service is running
if ! curl -s http://localhost:8081/healthz > /dev/null; then
    echo "❌ Service failed to start"
    kill $SERVICE_PID 2>/dev/null
    exit 1
fi

echo "✅ Service started successfully"

echo -e "\n=== Testing Enhanced Simple Health Endpoints ==="

echo "1. Legacy /healthz (basic response):"
curl -s http://localhost:8081/healthz | jq .

echo -e "\n2. Legacy /healthz with detailed parameter (enhanced but simple):"
curl -s "http://localhost:8081/healthz?detailed" | jq .

echo -e "\n3. V1 /v1/healthz (service info):"
curl -s http://localhost:8081/v1/healthz | jq .

echo -e "\n4. V1 /v1/healthz with detailed parameter (enhanced but simple):"
curl -s "http://localhost:8081/v1/healthz?detailed" | jq .

echo -e "\n5. Testing response times:"
echo "Basic health response time:"
time curl -s http://localhost:8081/healthz > /dev/null

echo "Enhanced health response time:"
time curl -s "http://localhost:8081/healthz?detailed" > /dev/null

# Stop service
echo -e "\n=== Stopping service ==="
kill $SERVICE_PID
wait $SERVICE_PID 2>/dev/null

echo -e "\n=== Service Request Logs ==="
if [ -f simple-health-test.log ]; then
    grep "HTTP" simple-health-test.log | head -10
fi

# Cleanup
rm -f simple-health-test.log

echo -e "\n🎉 Enhanced simple health testing completed!"
