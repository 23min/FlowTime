# UI-M02.01 Release: API Versioning & Storage Location Enhancement

> **Release Date:** September 8, 2025  
> **Branch:** feature/ui-m2/sim-api-versioning  
> **Status:** COMPLETED ✅
> **Base Release:** UI-M02.00 (September 5, 2025)

---

## Overview

UI-M02.01 provides critical enhancements to the FlowTime API health monitoring and storage location information, completing the API versioning work for the UI-M02 milestone. This patch release ensures full visibility of storage configurations across both FlowTime API and FlowTime-Sim services.

## Key Features

### 🗄️ **Storage Location Information**
- **Enhanced Health Endpoints**: FlowTime API now reports storage locations matching FlowTime-Sim functionality
- **HealthDetails Model**: Proper JSON serialization with camelCase property names
- **Configuration Priority**: Environment variable > Configuration > Default precedence
- **UI Display**: Health pages now show data directory and runs directory for both services

### 🔧 **API Versioning Consistency**
- **V1 Endpoint Standard**: All API tests updated to use `/v1/` endpoints consistently
- **Health Monitoring**: Enhanced health page with endpoint display and v1 API support
- **JSON Serialization**: Proper `JsonPropertyName` attributes for consistent API responses

### 🧹 **Repository Cleanup**
- **Script Organization**: All development scripts consolidated in `scripts/` directory
- **Documentation Cleanup**: Removed duplicate files, maintained only organized versions in subdirectories
- **File Structure**: Clean separation between root configuration and organized documentation

---

## Technical Changes

### Enhanced ServiceInfo Models
```csharp
public record HealthInfo(string Status, HealthDetails? Details = null);
public record HealthDetails(
    [property: JsonPropertyName("dataDirectory")] string? DataDirectory = null,
    [property: JsonPropertyName("runsDirectory")] string? RunsDirectory = null
);
```

### Storage Location Logic
- **GetDataDirectory()**: Resolves data directory using environment variables and configuration
- **GetRunsDirectory()**: Provides runs directory information for artifact storage
- **Configuration Integration**: Added `IConfiguration` dependency to ServiceInfoProvider

### API Response Example
```json
{
  "health": {
    "status": "healthy",
    "details": {
      "dataDirectory": "/workspaces/flowtime-vnext/src/FlowTime.API/data",
      "runsDirectory": "/workspaces/flowtime-vnext/src/FlowTime.API/data"
    }
  }
}
```

---

## Commit History

1. **feat**: implement storage location information in FlowTime API health endpoints (`c20b57a`)
2. **feat(ui)**: enhance health page with endpoint display and v1 API support (`6500599`)
3. **docs**: streamline README.md format with milestone table (`6bd8094`)
4. **feat(ui)**: implement comprehensive health monitoring with API versioning (`89ce07c`)
5. **chore**: clean up repository structure by removing duplicate files (`131eb33`)

---

## Testing Coverage

### Enhanced Test Suite
- **All Tests Passing**: 52/52 tests successful (100% pass rate)
- **V1 Endpoint Coverage**: Updated all API integration tests to use versioned endpoints
- **No Build Warnings**: Clean compilation across all projects
- **Health Endpoint Validation**: Storage location information properly serialized and accessible

### Repository Health
- **Zero Compiler Warnings**: Clean build output across all projects
- **Dependency Integrity**: All package references resolved and up to date
- **Code Quality**: Consistent formatting and structure maintained

---

## Breaking Changes

**None** - This is a backward-compatible enhancement that only adds functionality.

---

## Files Modified

### Core Implementation
- `src/FlowTime.API/Models/ServiceInfo.cs`: Enhanced with HealthInfo and HealthDetails records
- `src/FlowTime.API/Services/ServiceInfoProvider.cs`: Added storage location methods
- Multiple test files: Updated to use V1 endpoints consistently

### Repository Organization
- Moved scripts from root to `scripts/` directory
- Removed duplicate documentation files from `docs/` root
- Maintained organized documentation in subdirectories

---

## Deployment Notes

### Requirements
- No additional runtime dependencies
- Existing configuration patterns supported
- Environment variable override capabilities maintained

### Configuration
Storage location configuration uses this precedence:
1. Environment variables (`FLOWTIME_DATA_DIRECTORY`)
2. Configuration file settings
3. Default application directory

---

## Next Steps

With UI-M02.01 complete, the recommended next milestone is **FlowTime Core M1** (Expression Parser) to implement spreadsheet-style formulas and SHIFT operations, building on the solid foundation established by the UI and API versioning work.

---

## Validation

### Pre-Release Checklist ✅
- [x] All 52 tests passing
- [x] Zero build warnings
- [x] Storage location information visible in health endpoints
- [x] V1 API endpoints working correctly
- [x] Repository structure cleaned and organized
- [x] Documentation updated and organized

### Ready for Merge ✅
Branch `feature/ui-m2/sim-api-versioning` is ready for merge to main branch with confidence.
