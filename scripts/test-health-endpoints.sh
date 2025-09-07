#!/bin/bash

echo "=== FlowTime-Sim Health Check Testing ==="

cd /workspaces/flowtime/flowtime-sim-vnext

# Start service
echo "Starting FlowTime-Sim service..."
dotnet run --project src/FlowTime.Sim.Service --urls http://localhost:8081 > health-test.log 2>&1 &
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

echo -e "\n=== Testing Health Endpoints ==="

echo "1. Legacy /healthz (basic response):"
curl -s http://localhost:8081/healthz | jq .

echo -e "\n2. Legacy /healthz with detailed parameter:"
curl -s "http://localhost:8081/healthz?detailed" | jq .

echo -e "\n3. V1 /v1/healthz (enhanced service info):"
curl -s http://localhost:8081/v1/healthz | jq .

echo -e "\n4. V1 /v1/healthz with detailed parameter:"
curl -s "http://localhost:8081/v1/healthz?detailed" | jq .

echo -e "\n5. Testing HTTP status codes:"
echo "Basic health status:"
curl -s -w "HTTP Status: %{http_code}\n" http://localhost:8081/healthz -o /dev/null

echo "Detailed health status:"
curl -s -w "HTTP Status: %{http_code}\n" "http://localhost:8081/v1/healthz?detailed" -o /dev/null

# Stop service
echo -e "\n=== Stopping service ==="
kill $SERVICE_PID
wait $SERVICE_PID 2>/dev/null

echo -e "\n=== Service Request Logs ==="
if [ -f health-test.log ]; then
    grep "HTTP" health-test.log | head -10
fi

# Cleanup
rm -f health-test.log

echo -e "\n🎉 Health check testing completed!"
