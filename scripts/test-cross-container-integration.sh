#!/bin/bash
# FlowTime-Sim Cross-Container Integration Test
# Tests various ways FlowTime-Sim can connect to FlowTime API

set -e  # Exit on error

echo "🧪 FlowTime-Sim Cross-Container Integration Test"
echo "================================================"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Test results tracking
TESTS_PASSED=0
TESTS_FAILED=0
TESTS_TOTAL=0

# Helper functions
run_test() {
    local test_name="$1"
    local test_command="$2"
    TESTS_TOTAL=$((TESTS_TOTAL + 1))
    
    echo ""
    echo -e "${BLUE}🔧 Test $TESTS_TOTAL: $test_name${NC}"
    echo "Command: $test_command"
    
    if eval "$test_command"; then
        echo -e "${GREEN}✅ PASSED: $test_name${NC}"
        TESTS_PASSED=$((TESTS_PASSED + 1))
        return 0
    else
        echo -e "${RED}❌ FAILED: $test_name${NC}"
        TESTS_FAILED=$((TESTS_FAILED + 1))
        return 1
    fi
}

check_api_health() {
    local url="$1"
    local timeout="${2:-5}"
    echo "🔍 Checking API health at $url (timeout: ${timeout}s)"
    
    if timeout $timeout curl -s "$url/healthz" > /dev/null 2>&1; then
        echo -e "${GREEN}✅ API is healthy at $url${NC}"
        return 0
    else
        echo -e "${RED}❌ API not accessible at $url${NC}"
        return 1
    fi
}

# Change to correct directory
cd /workspaces/flowtime-sim-vnext

echo ""
echo "📍 Environment Information"
echo "=========================="
echo "Working directory: $(pwd)"
echo "Docker network: flowtime-dev"
echo "Container name: flowtime-sim"
echo ""
echo "🌍 Environment Variables:"
echo "FLOWTIME_API_BASEURL: ${FLOWTIME_API_BASEURL:-'(not set)'}"
echo ""

# Build the CLI first
echo "🔨 Building FlowTime-Sim CLI..."
if ! dotnet build src/FlowTime.Sim.Cli --nologo -v quiet; then
    echo -e "${RED}❌ Failed to build CLI${NC}"
    exit 1
fi
echo -e "${GREEN}✅ CLI build successful${NC}"

echo ""
echo "🏥 API Health Checks"
echo "===================="

# Check if FlowTime API is running on different endpoints
check_api_health "http://localhost:8080" 3 || echo -e "${YELLOW}⚠️  FlowTime API not running on localhost:8080${NC}"
check_api_health "http://flowtime-api:8080" 3 || echo -e "${YELLOW}⚠️  FlowTime API not running on flowtime-api:8080${NC}"

echo ""
echo "🧪 CLI Connection Tests"
echo "======================="

# Test 1: Default behavior (should now use FLOWTIME_API_BASEURL)
run_test "CLI with environment variable default (sim mode)" \
    "timeout 10 dotnet run --project src/FlowTime.Sim.Cli -- --model examples/m0.const.sim.yaml --mode sim --out out/test-env-default --verbose 2>/dev/null"

# Test 2: Connectivity test - localhost should fail in containers
echo ""
echo -e "${BLUE}🔧 Test 2: Container networking validation${NC}"
if curl -s http://localhost:8080/healthz > /dev/null 2>&1; then
    echo -e "${RED}❌ Unexpected: localhost:8080 is accessible from container${NC}"
    TESTS_FAILED=$((TESTS_FAILED + 1))
else
    echo -e "${GREEN}✅ Expected: localhost:8080 not accessible from container${NC}"
    TESTS_PASSED=$((TESTS_PASSED + 1))
fi
TESTS_TOTAL=$((TESTS_TOTAL + 1))

# Test 3: Connectivity test - container name should work  
echo ""
echo -e "${BLUE}🔧 Test 3: Container name connectivity${NC}"
if curl -s http://flowtime-api:8080/healthz > /dev/null 2>&1; then
    echo -e "${GREEN}✅ Container name flowtime-api:8080 is accessible${NC}"
    TESTS_PASSED=$((TESTS_PASSED + 1))
else
    echo -e "${RED}❌ Container name flowtime-api:8080 not accessible${NC}"
    TESTS_FAILED=$((TESTS_FAILED + 1))
fi
TESTS_TOTAL=$((TESTS_TOTAL + 1))

# Test 4: Environment variable precedence validation
echo ""
echo -e "${BLUE}🔧 Test 4: Environment variable usage validation${NC}"
DEFAULT_URL=$(dotnet run --project src/FlowTime.Sim.Cli -- --help 2>/dev/null | grep "Default FlowTime URL" | cut -d: -f2- | xargs)
if [[ "$DEFAULT_URL" == *"flowtime-api:8080"* ]]; then
    echo -e "${GREEN}✅ Environment variable FLOWTIME_API_BASEURL is being used as default${NC}"
    TESTS_PASSED=$((TESTS_PASSED + 1))
else
    echo -e "${RED}❌ Environment variable not being used as default. Got: $DEFAULT_URL${NC}"
    TESTS_FAILED=$((TESTS_FAILED + 1))
fi
TESTS_TOTAL=$((TESTS_TOTAL + 1))

# Test 5: API Version configuration  
echo ""
echo -e "${BLUE}🔧 Test 5: API Version configuration${NC}"
DEFAULT_API_VERSION=$(dotnet run --project src/FlowTime.Sim.Cli -- --help 2>/dev/null | grep "Default API Version" | cut -d: -f2- | xargs)
if [[ "$DEFAULT_API_VERSION" == *"v1"* ]]; then
    echo -e "${GREEN}✅ API Version defaults to v1${NC}"
    TESTS_PASSED=$((TESTS_PASSED + 1))
else
    echo -e "${RED}❌ API Version not set correctly. Got: $DEFAULT_API_VERSION${NC}"
    TESTS_FAILED=$((TESTS_FAILED + 1))
fi
TESTS_TOTAL=$((TESTS_TOTAL + 1))

# Test 6: API endpoint connectivity (v1)
echo ""
echo -e "${BLUE}🔧 Test 6: FlowTime API v1 endpoint connectivity${NC}"
if curl -s -X POST http://flowtime-api:8080/v1/run -H "Content-Type: text/plain" -d "test" > /dev/null 2>&1; then
    echo -e "${GREEN}✅ FlowTime API v1/run endpoint is accessible${NC}"
    TESTS_PASSED=$((TESTS_PASSED + 1))
else
    echo -e "${RED}❌ FlowTime API v1/run endpoint not accessible${NC}"
    TESTS_FAILED=$((TESTS_FAILED + 1))
fi
TESTS_TOTAL=$((TESTS_TOTAL + 1))

# Test 7: Test simulation mode (no API needed)
run_test "CLI simulation mode (no API required)" \
    "timeout 10 dotnet run --project src/FlowTime.Sim.Cli -- --model examples/m0.const.sim.yaml --mode sim --out out/test-sim-only --verbose 2>/dev/null"

# Test 6: Check help message shows correct default
echo ""
echo -e "${BLUE}🔧 Additional Test: Help message default URL${NC}"
HELP_OUTPUT=$(dotnet run --project src/FlowTime.Sim.Cli -- --help 2>/dev/null)
echo "Help output:"
echo "$HELP_OUTPUT"
if echo "$HELP_OUTPUT" | grep -q "flowtime-api:8080"; then
    echo -e "${GREEN}✅ Help shows container URL as default${NC}"
    TESTS_PASSED=$((TESTS_PASSED + 1))
else
    echo -e "${RED}❌ Help does not show container URL as default${NC}"
    TESTS_FAILED=$((TESTS_FAILED + 1))
fi
TESTS_TOTAL=$((TESTS_TOTAL + 1))

echo ""
echo "🔍 Generated Artifacts Check"
echo "============================"

# Check if any output directories were created
if ls out/test-* > /dev/null 2>&1; then
    echo -e "${GREEN}✅ Output directories created:${NC}"
    ls -la out/test-* 2>/dev/null | head -10
    
    # Check for CSV files
    if find out/test-* -name "*.csv" -type f > /dev/null 2>&1; then
        echo -e "${GREEN}✅ CSV files generated${NC}"
        find out/test-* -name "*.csv" -type f | head -5
    else
        echo -e "${YELLOW}⚠️  No CSV files found${NC}"
    fi
else
    echo -e "${YELLOW}⚠️  No output directories found${NC}"
fi

echo ""
echo "📊 Test Results Summary"
echo "======================="
echo -e "Total tests: ${BLUE}$TESTS_TOTAL${NC}"
echo -e "Passed: ${GREEN}$TESTS_PASSED${NC}"
echo -e "Failed: ${RED}$TESTS_FAILED${NC}"

if [ $TESTS_FAILED -eq 0 ]; then
    echo ""
    echo -e "${GREEN}🎉 All tests passed! Cross-container connectivity is working.${NC}"
    exit 0
else
    echo ""
    echo -e "${RED}❌ Some tests failed. Check the output above for details.${NC}"
    echo ""
    echo "🔧 Troubleshooting Tips:"
    echo "- Ensure FlowTime API is running in flowtime-api container"
    echo "- Verify both containers are on the same Docker network (flowtime-dev)"
    echo "- Check that FLOWTIME_API_BASEURL environment variable is set correctly"
    echo "- For localhost tests, ensure FlowTime API is running on host port 8080"
    exit 1
fi
