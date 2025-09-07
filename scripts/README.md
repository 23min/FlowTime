# Scripts Directory

This directory contains development and testing scripts for FlowTime-Sim.

## Testing Scripts

### Health Check Testing
- **`test-health-endpoints.sh`** - Comprehensive health endpoint testing
- **`test-simple-health.sh`** - Simple health response validation

### API Testing  
- **`test-v1-api.sh`** - V1 API functional testing
- **`validate-api-migration.sh`** - V1 API migration validation
- **`test-api.sh`** - General API testing
- **`quick-test.sh`** - Quick development testing

### Configuration Testing
- **`test-clean-config.sh`** - Clean configuration testing (no legacy support)
- **`test-config.sh`** - Configuration validation
- **`test-config.ps1`** - PowerShell configuration tests
- **`test-single-data-dir.sh`** - Single data directory testing
- **`test-single-data-final.sh`** - Final data directory validation

### Legacy Testing
- **`test-legacy-removed.sh`** - Verify legacy endpoints are removed
- **`test-legacy-ignored.sh`** - Test legacy configuration handling

### Feature Testing
- **`test-request-logging.sh`** - Request logging validation
- **`test-catalogs.sh`** - Catalog management testing

## Test Data Files

- **`simple-test.yaml`** - Simple YAML payload for basic API testing
- **`test-api.http`** - VS Code REST Client file for interactive testing

## Debug Files

- **`debug-events.ndjson`** - Debug output files for development
- **`my-debug-events.ndjson`** - Additional debug data

## Usage

All scripts should be run from the repository root:

```bash
# Example: Test health endpoints
./scripts/test-health-endpoints.sh

# Example: Validate API migration
./scripts/validate-api-migration.sh

# Example: Test clean configuration
./scripts/test-clean-config.sh

# Example: Quick smoke test
./scripts/quick-test.sh
```

## Development Notes

- Scripts assume the service can be started on `localhost:8081`
- All scripts include cleanup procedures
- Use `jq` for JSON formatting (installed in dev container)
- Most scripts create temporary test files and clean them up automatically
- Some scripts may require manual service startup - check individual script documentation
