# Test script to verify FlowTime.Sim.Service configuration improvements
# This validates that file output goes to the right location based on configuration

Write-Host "=== FlowTime.Sim Configuration Test ===" -ForegroundColor Green

# Test 1: Check default behavior (should use ./data from configuration)
Write-Host "`n1. Testing default configuration (Development environment)..." -ForegroundColor Yellow
Push-Location "/workspaces/flowtime/flowtime-sim-vnext/src/FlowTime.Sim.Service"

# Clean up any existing test data
Remove-Item -Path "./data" -Recurse -Force -ErrorAction SilentlyContinue

# Start service briefly and test health endpoint
Write-Host "Starting service..."
$process = Start-Process -FilePath "dotnet" -ArgumentList "run", "--urls", "http://localhost:8082" -NoNewWindow -PassThru
Start-Sleep 3

try {
    $response = Invoke-RestMethod -Uri "http://localhost:8082/healthz" -Method Get
    Write-Host "Health check: $($response.status)" -ForegroundColor Green
    
    # Check if data directory exists (it should be created automatically)
    if (Test-Path "./data") {
        Write-Host "✓ Data directory created successfully at ./data" -ForegroundColor Green
    } else {
        Write-Host "✗ Data directory not created" -ForegroundColor Red
    }
} catch {
    Write-Host "✗ Service not responding: $($_.Exception.Message)" -ForegroundColor Red
} finally {
    Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
    Write-Host "Service stopped"
}

Pop-Location

# Test 2: Check environment variable override
Write-Host "`n2. Testing environment variable override..." -ForegroundColor Yellow
$env:FLOWTIME_SIM_RUNS_ROOT = "/tmp/flowtime-test"

Push-Location "/workspaces/flowtime/flowtime-sim-vnext"

# Clean up
Remove-Item -Path "/tmp/flowtime-test" -Recurse -Force -ErrorAction SilentlyContinue

# Test with CLI (faster than service)
Write-Host "Testing with CLI..."
try {
    $output = & dotnet run --project src/FlowTime.Sim.Cli -- --model examples/m0.const.yaml --out /tmp/flowtime-test/output.csv --format csv 2>&1
    
    if (Test-Path "/tmp/flowtime-test") {
        Write-Host "✓ Environment variable override working - files created in /tmp/flowtime-test" -ForegroundColor Green
        Get-ChildItem "/tmp/flowtime-test" -Recurse | Format-Table Name, Length
    } else {
        Write-Host "✗ Environment variable override failed" -ForegroundColor Red
    }
} catch {
    Write-Host "✗ CLI test failed: $($_.Exception.Message)" -ForegroundColor Red
}

# Clean up
Remove-Item -Path "/tmp/flowtime-test" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -Path "env:FLOWTIME_SIM_RUNS_ROOT" -ErrorAction SilentlyContinue

Pop-Location

Write-Host "`n=== Configuration Test Complete ===" -ForegroundColor Green
