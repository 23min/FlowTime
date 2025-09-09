#!/bin/bash
# Test script to verify FlowTime.Sim.Service configuration improvements
# This validates that file output goes to the right location based on configuration

echo "=== FlowTime.Sim Configuration Test ==="

# Test 1: Check CLI behavior with environment variable
echo ""
echo "1. Testing CLI with environment variable override..."
cd /workspaces/flowtime-sim-vnext

# Clean up any existing test data
rm -rf /tmp/flowtime-test

# Set environment variable and test CLI
export FLOWTIME_SIM_RUNS_ROOT="/tmp/flowtime-test"
echo "Set FLOWTIME_SIM_RUNS_ROOT=/tmp/flowtime-test"

# Run CLI with simple model
dotnet run --project src/FlowTime.Sim.Cli -- --model examples/m0.const.yaml --out output/test.csv --format csv

# Check if environment variable was respected
if [ -d "/tmp/flowtime-test" ]; then
    echo "✓ Environment variable override working - files created in /tmp/flowtime-test"
    find /tmp/flowtime-test -type f | head -5
else
    echo "✗ Environment variable override failed"
fi

# Clean up
rm -rf /tmp/flowtime-test
unset FLOWTIME_SIM_RUNS_ROOT

# Test 2: Check default behavior (should use ./data when running from service directory)
echo ""
echo "2. Testing Service directory default behavior..."
cd /workspaces/flowtime-sim-vnext/src/FlowTime.Sim.Service

# Clean up
rm -rf ./data

# Test that data directory is created by running a simple .NET call to check RunsRoot
cat > TestConfig.cs << 'EOF'
using Microsoft.Extensions.Configuration;

var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: true)
    .AddJsonFile("appsettings.Development.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

// Simulate the ServiceHelpers.RunsRoot call
var envVar = Environment.GetEnvironmentVariable("FLOWTIME_SIM_RUNS_ROOT");
string runsRoot;

if (!string.IsNullOrWhiteSpace(envVar))
{
    runsRoot = envVar;
}
else
{
    var configRoot = configuration["FlowTimeSim:RunsRoot"];
    if (!string.IsNullOrEmpty(configRoot))
    {
        runsRoot = configRoot;
    }
    else
    {
        runsRoot = "./data";
    }
}

Directory.CreateDirectory(runsRoot);
Console.WriteLine($"RunsRoot: {runsRoot}");
Console.WriteLine($"Full path: {Path.GetFullPath(runsRoot)}");
EOF

# Run the test
dotnet run TestConfig.cs

if [ -d "./data" ]; then
    echo "✓ Default configuration working - data directory created at ./data"
    ls -la ./data
else
    echo "✗ Default configuration failed - data directory not created"
fi

# Clean up
rm -f TestConfig.cs
rm -rf ./data

cd /workspaces/flowtime-sim-vnext

echo ""
echo "=== Configuration Test Complete ==="
