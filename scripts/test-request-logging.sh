#!/bin/bash

echo "=== Testing FlowTime-Sim Request Logging ==="

cd /workspaces/flowtime-sim-vnext

# Start service and capture logs
echo "Starting FlowTime-Sim service with request logging..."
dotnet run --project src/FlowTime.Sim.Service --urls http://localhost:8081 > service.log 2>&1 &
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

# Make test requests to generate logs
echo -e "\n=== Making test requests to observe logging ==="

echo "1. Testing GET /healthz"
curl -s http://localhost:8081/healthz > /dev/null

echo "2. Testing GET /v1/healthz"  
curl -s http://localhost:8081/v1/healthz > /dev/null

echo "3. Testing GET /v1/sim/scenarios"
curl -s http://localhost:8081/v1/sim/scenarios > /dev/null

echo "4. Testing GET /v1/sim/catalogs"
curl -s http://localhost:8081/v1/sim/catalogs > /dev/null

echo "5. Testing 404 endpoint"
curl -s http://localhost:8081/nonexistent > /dev/null

# Give time for logs to be written
sleep 2

# Stop service
echo -e "\n=== Stopping service ==="
kill $SERVICE_PID
wait $SERVICE_PID 2>/dev/null

# Show the request logs
echo -e "\n=== Request Logs (should show one line per request) ==="
if [ -f service.log ]; then
    # Look for our request logging lines (they should contain timing info)
    grep -E "(GET|POST|PUT|DELETE)" service.log | grep -E "[0-9]+ ms" || echo "No request logs found in expected format"
    
    echo -e "\n=== Full Service Log (for debugging) ==="
    cat service.log
else
    echo "❌ No service log file found"
fi

# Cleanup
rm -f service.log

echo -e "\n🎉 Request logging test completed!"
