# SVC-M1 Release Notes

**FlowTime API Artifact Endpoints**  
**Release Date**: September 3, 2025  
**Milestone**: SVC-M1  

## 🎯 What's New

### Artifact Serving API

FlowTime API can now serve previously generated run artifacts, enabling external clients and the UI to access completed run data without re-running models.

#### New Endpoints

* **`GET /runs/{runId}/index`**  
  Returns the series index (metadata) for a completed run
  
* **`GET /runs/{runId}/series/{seriesId}`**  
  Streams CSV data for a specific series from a completed run

#### Key Features

* **File System Integration**: Reads from the same directory structure that CLI creates
* **Configurable Storage**: `ArtifactsDirectory` setting (defaults to "out")  
* **Robust Error Handling**: Proper 404s for missing runs/series, detailed error messages
* **Standard Content Types**: JSON for indexes, CSV for series data
* **SYN-M0 Integration**: Leverages existing file adapters for consistent data access

## 🔧 Technical Implementation

### Dependencies
* Added `FlowTime.Adapters.Synthetic` reference to API project
* Reuses `FileSeriesReader` and `RunArtifactAdapter` classes

### Configuration
```json
{
  "ArtifactsDirectory": "out"  // Configurable output directory
}
```

### Example Usage
```bash
# Get series metadata
curl "http://localhost:8080/runs/hello/run_TIMESTAMP_HASH/index"

# Download specific series
curl "http://localhost:8080/runs/hello/run_TIMESTAMP_HASH/series/served@SERVED@DEFAULT"
```

## ✅ Testing

### New Test Coverage
* **ArtifactEndpointTests**: 4 new integration tests
* **Total Test Suite**: 33 tests passing (29 existing + 4 new)

### Test Scenarios
* Non-existent runs return 404
* Valid runs return complete series index  
* Valid series return proper CSV format
* Non-existent series return 404

## 🚀 What This Enables

### For UI Development (UI-M2)
* UI can now read real artifact data instead of simulation-only mode
* Enables true end-to-end workflows: CLI → API → UI

### For Integration
* External tools can access FlowTime run results programmatically
* Supports building dashboards and analysis tools on top of FlowTime

### For Development
* API-first architecture maintained
* Consistent data access patterns across CLI and API

## 🔄 Backward Compatibility

* **Fully backward compatible** - no breaking changes to existing endpoints
* Existing `/run`, `/graph`, `/healthz` endpoints unchanged
* All existing tests continue to pass

## 📋 Known Limitations

* No run discovery endpoint yet (planned for SVC-M2)
* No caching of frequently accessed artifacts
* Requires file system access to artifact directory

## 🎉 Milestone Status

**SVC-M1: COMPLETED** ✅

### Dependencies Satisfied
* ✅ **M0**: Core engine functionality  
* ✅ **SVC-M0**: Base API with /run, /graph endpoints
* ✅ **SYN-M0**: File adapters for artifact reading

### Next Milestones Unlocked
* **UI-M2**: Enhanced UI with real artifact data
* **SVC-M2**: Run discovery and listing endpoints

---

**Contributors**: GitHub Copilot  
**Test Results**: 33/33 tests passing  
**Documentation**: `/docs/milestones/SVC-M1.md`
