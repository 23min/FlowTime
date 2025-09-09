#!/bin/bash

echo "Testing FlowTime.Sim with FLOWTIME_SIM_DATA_DIR..."

# Set the data directory
export FLOWTIME_SIM_DATA_DIR="/tmp/flowtime-sim-full-test"

echo "Using data directory: $FLOWTIME_SIM_DATA_DIR"

# Create test catalogs
mkdir -p "$FLOWTIME_SIM_DATA_DIR/catalogs"
cat > "$FLOWTIME_SIM_DATA_DIR/catalogs/test-catalog.yaml" << 'EOF'
version: 1
metadata:
  id: "test-catalog"
  title: "Test Catalog"
  description: "Test catalog for single data directory"
components:
  - id: TEST_NODE
    label: "Test Node"
    description: "Test processing node"
connections: []
classes: 
  - "DEFAULT"
layoutHints:
  rankDir: LR
EOF

echo "Created test catalog at: $FLOWTIME_SIM_DATA_DIR/catalogs/test-catalog.yaml"

# Start the service in background
cd /workspaces/flowtime-sim-vnext
dotnet run --project src/FlowTime.Sim.Service &
SERVICE_PID=$!

# Wait for service to be ready
echo "Waiting for service to start..."
sleep 5

echo "Testing /catalogs endpoint..."
curl -s http://localhost:8081/catalogs | jq .

echo "Testing /catalogs/test-catalog endpoint..."
curl -s http://localhost:8081/catalogs/test-catalog | jq .

echo "Testing simulation run..."
curl -s -X POST http://localhost:8081/run \
  -H "Content-Type: text/plain" \
  -d "version: 1
metadata:
  scenario: test
routes:
  - id: r1
    from: TEST_NODE
    to: TEST_NODE
workload:
  arrival:
    pattern: constant
    rate: 1.0
  runtime: 1s"

echo ""
echo "Checking directory structure:"
find "$FLOWTIME_SIM_DATA_DIR" -type d | sort

# Cleanup
echo "Shutting down service..."
kill $SERVICE_PID 2>/dev/null

echo "Done."
