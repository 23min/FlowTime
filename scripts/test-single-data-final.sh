#!/bin/bash

echo "=== Testing FlowTime.Sim Single Data Directory ==="
echo ""

# Clean up any previous test directories
rm -rf /tmp/single-data-test

echo "1. Testing with FLOWTIME_SIM_DATA_DIR environment variable"
export FLOWTIME_SIM_DATA_DIR="/tmp/single-data-test"
echo "   Data directory: $FLOWTIME_SIM_DATA_DIR"

# Create some test catalogs in the expected location
mkdir -p "$FLOWTIME_SIM_DATA_DIR/catalogs"
cp /workspaces/flowtime/flowtime-sim-vnext/catalogs/*.yaml "$FLOWTIME_SIM_DATA_DIR/catalogs/"

echo "   Created catalogs in: $FLOWTIME_SIM_DATA_DIR/catalogs/"
ls "$FLOWTIME_SIM_DATA_DIR/catalogs/"

echo ""
echo "2. Testing directory structure creation"
cd /workspaces/flowtime/flowtime-sim-vnext

# Test the ServiceHelpers directly by running a quick CLI command
echo "   Testing CLI with new data directory..."
echo "version: 1
metadata:
  scenario: test
routes:
  - id: r1
    from: nodeA
    to: nodeA
workload:
  arrival:
    pattern: constant
    rate: 1.0
  runtime: 1s" > /tmp/test-spec.yaml

# Run the CLI which should create files in the new location
dotnet run --project src/FlowTime.Sim.Cli -- --spec /tmp/test-spec.yaml --out /tmp/single-data-test/runs/cli-test --format csv 2>/dev/null

echo ""
echo "3. Verifying directory structure:"
find /tmp/single-data-test -type d | sort
echo ""
echo "4. Verifying file creation:"
find /tmp/single-data-test -name "*.yaml" -o -name "*.json" -o -name "*.csv" | head -5

echo ""
echo "✅ Single data directory test complete!"
echo "   Base directory: /tmp/single-data-test"
echo "   Runs directory: /tmp/single-data-test/runs/"
echo "   Catalogs directory: /tmp/single-data-test/catalogs/"
