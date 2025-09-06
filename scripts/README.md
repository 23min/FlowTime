# FlowTime.Sim Testing Scripts

This directory contains various scripts and test files for FlowTime.Sim development and testing.

## API Testing Scripts

### `quick-test.sh`
Simple API test script that starts the service and makes a basic API call to verify functionality.

### `test-api.sh` 
Comprehensive API test script that:
- Builds the project
- Starts the service
- Tests multiple API endpoints
- Validates responses
- Cleans up

### `test-api.http`
VS Code REST Client file for interactive API testing. Contains HTTP requests for:
- Health checks
- `/sim/run` endpoint testing
- Various payload examples

### `test-catalogs.sh`
Tests the catalog-related API endpoints:
- Lists available catalogs
- Retrieves specific catalog details

## Configuration Testing Scripts

### `test-config.sh`
Tests various configuration scenarios including:
- Environment variable overrides
- CLI parameter handling
- File output location configuration

### `test-config.ps1`
PowerShell version of configuration tests for cross-platform validation.

### `test-single-data-dir.sh`
Tests the single data directory configuration (`FLOWTIME_SIM_DATA_DIR`):
- Creates test catalogs
- Verifies directory structure
- Tests both CLI and Service modes

### `test-single-data-final.sh`
Final validation test for the single data directory implementation.

### `test-legacy-ignored.sh`
Validates that legacy environment variables (`FLOWTIME_SIM_RUNS_ROOT`, `FLOWTIME_SIM_CATALOGS_ROOT`) are properly ignored when the new `FLOWTIME_SIM_DATA_DIR` approach is used.

### `test-clean-config.sh`
Tests configuration behavior in clean environments without legacy variables.

## Test Data Files

### `simple-test.yaml`
Simple YAML payload for basic API testing.

## Debug Files

### `debug-events.ndjson`, `my-debug-events.ndjson`
Debug output files containing event data for debugging and development purposes.

## Usage

Most scripts can be run directly from the scripts directory:

```bash
cd scripts
./quick-test.sh
./test-api.sh
# etc.
```

Note: Some scripts may require the service to be started manually or may start/stop the service themselves. Check individual script documentation for specific requirements.
