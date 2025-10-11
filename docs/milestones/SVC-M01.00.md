# SVC-M-1 — Artifact Endpoints

**Goal**
Enable the API to serve previously generated run artifacts, allowing UI and external clients to access series data and metadata from completed runs.

## Scope

* Add **artifact serving endpoints** that read from the file system where CLI writes artifacts
* Provide **GET /runs/{runId}/index** to return the series index for a completed run
* Provide **GET /runs/{runId}/series/{seriesId}** to stream CSV data for specific series
* Leverage existing **SYN-M-0 file adapters** for consistent artifact reading
* Maintain **API-first architecture** with proper error handling and content types

## Functional Requirements

### API Endpoints

* `GET /runs/{runId}/index` → returns the contents of `runs/{runId}/series/index.json`
* `GET /runs/{runId}/series/{seriesId}` → streams CSV content from `runs/{runId}/series/{seriesId}.csv`

### Error Handling

* Returns **404 Not Found** for non-existent runs or series
* Returns **500 Internal Server Error** with details for file system issues
* Proper HTTP status codes and JSON error responses

### Configuration

* **ArtifactsDirectory** configuration setting (defaults to "out")
* Reads from the same directory structure that CLI writes to
* Supports nested run directories (e.g., `out/hello/run_TIMESTAMP_HASH`)

## Implementation Details

### Dependencies Added

* Added reference to `FlowTime.Adapters.Synthetic` in `FlowTime.API.csproj`
* Leverages existing `FileSeriesReader` and `RunArtifactAdapter` classes

### Code Changes

* **Program.cs**: Added artifact endpoints before `app.Run()`
  * Configurable artifacts directory via `ArtifactsDirectory` setting
  * Uses `FileSeriesReader` for consistent file access
  * Converts `Series` objects to CSV format for streaming
  * Full exception handling with proper HTTP status codes

### Data Flow

1. Client requests `GET /runs/{runId}/index`
2. API constructs path: `{ArtifactsDirectory}/{runId}`
3. Uses `RunArtifactAdapter.GetIndexAsync()` to read `series/index.json`
4. Returns JSON response with series metadata

1. Client requests `GET /runs/{runId}/series/{seriesId}`
2. API constructs path: `{ArtifactsDirectory}/{runId}`
3. Uses `FileSeriesReader.ReadSeriesAsync()` to read CSV data
4. Converts `Series.ToArray()` to CSV format
5. Returns CSV response with `text/csv` content type

## Testing

### Unit Tests

* **ArtifactEndpointTests.cs**: Comprehensive test coverage
  * `GET_Runs_NonExistentRun_Returns_NotFound`: 404 for missing runs
  * `GET_Runs_ExistingRun_Returns_Index`: Valid JSON index response
  * `GET_Runs_ExistingSeries_Returns_CSV`: Valid CSV series response
  * `GET_Runs_NonExistentSeries_Returns_NotFound`: 404 for missing series

### Integration Testing

* Tests use `WebApplicationFactory` with configurable `ArtifactsDirectory`
* Creates temporary test artifacts to verify end-to-end functionality
* Validates proper content types and response formats

## Acceptance Criteria

✅ **API Completeness**: GET /runs/{runId}/index returns complete series index
✅ **CSV Streaming**: GET /runs/{runId}/series/{seriesId} returns valid CSV data
✅ **Error Handling**: Proper 404 responses for non-existent resources
✅ **Content Types**: Correct MIME types (application/json, text/csv)
✅ **File System Integration**: Reads from same structure CLI creates
✅ **Test Coverage**: All endpoints covered by unit and integration tests

## Usage Example

```bash
curl -s "http://localhost:8080/runs/hello/run_20250903T201653Z_7c81c6e2/index" | jq .

# Get series data
curl -s "http://localhost:8080/runs/hello/run_20250903T201653Z_7c81c6e2/series/served@SERVED@DEFAULT"
```

## What's Next

* **UI-M-2**: UI can now switch from simulation mode to reading real artifact data
* **SVC-M-2**: Add run listing endpoints (`GET /runs`) for discovery
* **Cache optimization**: Consider caching frequently accessed artifacts
* **Streaming optimization**: For large series, consider streaming line-by-line

---

**Status**: ✅ **COMPLETED**
**Milestone**: SVC-M-1 (Service/API Artifact Endpoints)
**Dependencies**: SYN-M-0 (File Adapters), SVC-M-0 (Base API)
**Test Count**: 4 new tests, 33 total tests passing
