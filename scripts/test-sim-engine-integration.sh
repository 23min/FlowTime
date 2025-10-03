#!/bin/bash

################################################################################
# FlowTime Sim ↔ Engine Integration Test Suite
################################################################################

# Note: Not using 'set -e' because we want to continue testing even if some tests fail

# Colors
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
NC='\033[0m'

# Configuration
ENGINE_URL="http://localhost:8080"
SIM_URL="http://localhost:8090"
TMP_DIR="/tmp/flowtime-integration-test"
VERBOSE=false
CHAPTER=""

# Parse arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --verbose) VERBOSE=true; shift ;;
        --chapter) CHAPTER="$2"; shift 2 ;;
        *) echo "Usage: $0 [--verbose] [--chapter N]"; exit 1 ;;
    esac
done

# Test counters
PASSED=0
FAILED=0
TOTAL=0

# Helper functions
pass() { echo -e "${GREEN}✅ PASS:${NC} $1"; ((PASSED++)); ((TOTAL++)); }
fail() { echo -e "${RED}❌ FAIL:${NC} $1"; ((FAILED++)); ((TOTAL++)); }

echo "FlowTime Sim ↔ Engine Integration Test Suite"
echo "=============================================="
echo ""

# Create temp directory
mkdir -p "$TMP_DIR"

# Check services
echo "Checking services..."
curl -s "$ENGINE_URL/healthz" > /dev/null || { echo "Engine not running"; exit 1; }
curl -s "$SIM_URL/healthz" > /dev/null || { echo "Sim not running"; exit 1; }
echo "✅ Both services running"
echo ""

################################################################################
# Chapter 1: Basic Workflow
################################################################################

if [[ -z "$CHAPTER" ]] || [[ "$CHAPTER" == "1" ]]; then
    echo "Chapter 1: Basic Workflow"
    echo "-------------------------"
    
    # Test 1.1: Simple generation and execution
    # Use bins=6 to match default demandPattern/capacityPattern array lengths
    curl -s -X POST "$SIM_URL/api/v1/templates/transportation-basic/generate" \
      -H "Content-Type: application/json" \
      -d '{"bins": 6, "binSize": 1, "binUnit": "hours"}' \
      -o "$TMP_DIR/basic.json"
    
    if jq -e '.model' "$TMP_DIR/basic.json" > /dev/null; then
        jq -r '.model' "$TMP_DIR/basic.json" | \
        curl -s -X POST "$ENGINE_URL/v1/run" \
          -H "Content-Type: application/x-yaml" \
          --data-binary @- > "$TMP_DIR/basic-result.json"
        
        # Validate all required response fields
        if jq -e '.grid and .order and .series and .runId and .artifactsPath and .modelHash' "$TMP_DIR/basic-result.json" > /dev/null; then
            pass "Basic workflow: generation → execution (all response fields present)"
        else
            fail "Basic workflow failed (missing response fields)"
        fi
    else
        fail "Model generation failed"
    fi
    
    echo ""
fi

################################################################################
# Chapter 4: Provenance Metadata
################################################################################

if [[ -z "$CHAPTER" ]] || [[ "$CHAPTER" == "4" ]]; then
    echo "Chapter 4: Provenance Metadata"
    echo "-------------------------------"
    
    # Test 4.1: Header-based provenance
    curl -s -X POST "$SIM_URL/api/v1/templates/transportation-basic/generate" \
      -H "Content-Type: application/json" \
      -d '{"bins": 6}' \
      -o "$TMP_DIR/prov.json"
    
    MODEL=$(jq -r '.model' "$TMP_DIR/prov.json")
    PROV=$(jq -c '.provenance' "$TMP_DIR/prov.json")
    
    echo "$MODEL" | curl -s -X POST "$ENGINE_URL/v1/run" \
      -H "Content-Type: application/x-yaml" \
      -H "X-Model-Provenance: $PROV" \
      --data-binary @- > "$TMP_DIR/prov-result.json"
    
    ARTIFACTS=$(jq -r '.artifactsPath' "$TMP_DIR/prov-result.json")
    
    if [[ -f "$ARTIFACTS/provenance.json" ]]; then
        pass "Header-based provenance stored"
    else
        fail "Provenance file not created"
    fi
    
    # Test 4.2: Models without provenance still work
    cat > "$TMP_DIR/no-prov.yaml" <<YAML
schemaVersion: 1
grid:
  bins: 3
  binSize: 1
  binUnit: hours
nodes:
  - id: demand
    kind: const
    values: [10, 20, 30]
YAML
    
    HTTP_CODE=$(curl -s -o /dev/null -w "%{http_code}" \
      -X POST "$ENGINE_URL/v1/run" \
      -H "Content-Type: application/x-yaml" \
      --data-binary @"$TMP_DIR/no-prov.yaml")
    
    if [[ "$HTTP_CODE" == "200" ]]; then
        pass "Models without provenance metadata work (optional feature)"
    else
        fail "Models without provenance should still work"
    fi
    
    # Test 4.3: Old schema (arrivals/route) should be REJECTED
    cat > "$TMP_DIR/old-schema.yaml" <<YAML
schemaVersion: 1
grid:
  bins: 3
  binSize: 1
  binUnit: hours
arrivals:
  kind: const
  values: [10, 20, 30]
route:
  id: TEST
YAML
    
    HTTP_CODE=$(curl -s -o "$TMP_DIR/old-schema-result.json" -w "%{http_code}" \
      -X POST "$ENGINE_URL/v1/run" \
      -H "Content-Type: application/x-yaml" \
      --data-binary @"$TMP_DIR/old-schema.yaml")
    
    if [[ "$HTTP_CODE" == "400" ]] || [[ "$HTTP_CODE" == "422" ]]; then
        pass "Old schema (arrivals/route) correctly REJECTED"
    else
        fail "Old schema should be rejected (got HTTP $HTTP_CODE, expected 400/422)"
    fi
    
    echo ""
fi

################################################################################
# Summary
################################################################################

echo "=============================================="
echo "Test Summary"
echo "=============================================="
echo "Total: $TOTAL"
echo -e "${GREEN}Passed: $PASSED${NC}"

if [[ $FAILED -gt 0 ]]; then
    echo -e "${RED}Failed: $FAILED${NC}"
    exit 1
else
    echo -e "${GREEN}✅ All tests passed!${NC}"
    exit 0
fi
