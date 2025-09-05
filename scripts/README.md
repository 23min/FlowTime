# Development Scripts

This directory contains development and testing scripts for FlowTime.

## Integration Test Scripts

- `test-api-integration.sh` - Tests the FlowTime API endpoints (`/run`, `/runs/{id}/index`)
- `test-download.sh` - Tests artifact download functionality via series endpoints

## Test YAML Files

- `test-api.yaml` - Simple FlowTime engine model for API testing (may have encoding issues, use inline YAML instead)
- `test-ui-yaml.yaml` - UI-specific test model  
- `test.yaml` - FlowTime-Sim compatible model for simulation testing

## Usage

Make scripts executable:
```bash
chmod +x scripts/*.sh
```

Run integration tests:
```bash
# Test API endpoints (handles starting/stopping API automatically)
./scripts/test-api-integration.sh

# Test download functionality (requires API to be running)
./scripts/test-download.sh
```

Test individual models with inline YAML (recommended):
```bash
# Engine API - simple model
curl -X POST http://localhost:8080/run -H "Content-Type: text/plain" -d 'grid:
  bins: 3
  binMinutes: 60
nodes:
  - id: demand
    kind: const
    values: [10, 20, 30]
  - id: served
    kind: expr
    expr: "demand * 0.8"'

# FlowTime-Sim (if running on port 5279)
curl -X POST http://localhost:5279/sim/run -H "Content-Type: text/plain" -d @scripts/test.yaml
```

## Current Status

✅ `test-api-integration.sh` - Working, tests `/run` and `/runs/{id}/index` endpoints  
✅ `test-download.sh` - Working, tests series download endpoints  
⚠️ YAML files may have encoding issues, prefer inline YAML for testing
