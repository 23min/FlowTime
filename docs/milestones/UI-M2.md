# UI-M2 — Mode Toggle & UX Enhancement

> **Target Project:** FlowTime UI  
> **Prerequisites:** UI-M1 ✅, SVC-M1 ✅, SYN-M0 ✅  
> **Status:** COMPLETED ✅

---

## Goal

Enhance the Template Runner UI with reliable mode switching between SIM and API modes, improved user experience for disabled states, robust component state management, AND implement real API integration with FlowTime-Sim services using artifact-first architecture. This milestone combined UX improvements with full API integration.

## What We Accomplished

- **Real API Integration:** Replaced mock services with actual FlowTime-Sim API calls following artifact-first patterns
- **Reliable Mode Switching:** Fixed mode toggle persistence and component refresh issues
- **Enhanced UX:** Added clear disabled state feedback with tooltips and explanatory alerts  
- **Component State Management:** Implemented proper subscription patterns and component remounting
- **Artifact-First Architecture:** Implemented canonical run artifact reading (`series/index.json`, CSV streaming)
- **Repository Organization:** Cleaned up development files and improved script organization
- **Integration Testing:** Updated and verified API integration test scripts
- **Test Coverage:** Expanded from 33 to 35 tests with critical API endpoint coverage

## Why This Approach

- **User Experience First:** Address immediate UX pain points blocking effective usage
- **Foundation for Integration:** Establish reliable mode switching before real API work
- **Component Architecture:** Validate service layer patterns with mock implementations
- **Developer Experience:** Organize repository and scripts for efficient development

---

## Architecture

```
UI Template Runner → HTTP Services → FlowTime-Sim APIs → Simulation Engine → Live Results
```

### Key Principles
- **Service Layer Abstraction:** Maintain clean interfaces while replacing implementations
- **Error Resilience:** Handle network failures and API errors gracefully
- **Real-Time Updates:** Provide live feedback during simulation execution
- **Configuration Driven:** Support multiple environments (dev, staging, prod)

---

## Functional Requirements

### FR-UI-M2-1: Real Template Service Integration ✅
- Replace `TemplateServiceImplementations` mock with HTTP-based implementation
- Connect to FlowTime-Sim `/sim/scenarios` endpoints  
- Handle template schema loading and validation
- Support template search and categorization

### FR-UI-M2-2: Real Catalog Service Integration ✅
- Replace catalog mock service with real API calls
- Connect to FlowTime-Sim catalog endpoints
- Load actual system catalogs with real metadata
- Handle catalog availability and status information

### FR-UI-M2-3: Live Simulation Execution (Artifact-First) ✅
- Replace mock simulation service with real FlowTime-Sim API calls
- Execute simulations via `POST /sim/run` → `{ simRunId }`
- Read results from canonical run artifacts (`runs/<runId>/...`)
- Parse `series/index.json` to discover available series
- Stream individual CSVs via `/sim/runs/{id}/series/{seriesId}`
- **Remove dependency on custom metadata fields** from UI-M1 mock

### FR-UI-M2-4: Mode Toggle & UX Enhancement ✅
- Implement reliable mode switching between SIM and API modes
- Add enhanced UX with clear disabled state feedback and tooltips
- Fix component state management with proper subscription patterns
- Provide visual feedback during mode transitions

### FR-UI-M2-5: Enhanced Error Handling ✅
- Network timeout and retry logic
- API error response handling (404 for missing artifacts)
- User-friendly error messages
- Graceful fallbacks when APIs unavailable

### FR-UI-M2-6: Repository & Test Organization ✅
- Organize development files in scripts/ directory
- Update integration test scripts for current API endpoints
- Expand test coverage for critical API endpoints
- Clean up unused dependencies and improve project structure

---

## Technical Implementation

### Service Layer Architecture
```csharp
// Maintain existing interfaces from UI-M1
public interface ITemplateService { ... }
public interface ICatalogService { ... }
public interface IFlowTimeSimService { ... }

// New HTTP implementations
public class HttpTemplateService : ITemplateService { ... }
public class HttpCatalogService : ICatalogService { ... }
public class HttpFlowTimeSimService : IFlowTimeSimService { ... }
```

### Configuration System
```json
{
  "FlowTimeSimApi": {
    "BaseUrl": "http://localhost:5279",
    "Timeout": "00:05:00",
    "RetryPolicy": {
      "MaxRetries": 3,
      "BackoffMultiplier": 2
    }
  }
}
```

### Real-Time Updates
- Background polling service for simulation status
- SignalR integration for live updates (optional)
- Progress indicators and cancellation tokens
- Status persistence across page refreshes

---

## Required FlowTime-Sim Endpoints

### Simulation Execution (Per Integration Spec)
```http
POST /sim/run
Body: {
  "grid": { "bins": 288, "binMinutes": 5 },
  "rng": { "seed": 12345 },
  "components": ["COMP_A"],
  "measures": ["arrivals","served"],
  "arrivals": { "kind": "rate", "ratePerBin": 1.2 },
  "served": { "kind": "fractionOf", "of": "arrivals", "fraction": 0.85 },
  "catalogId": null,
  "templateId": "hello",
  "notes": "UI-M2 integration"
}
Response: { "simRunId": "sim_2025-09-04T10-22-01Z_ab12cd34" }
```

### Artifact Reading (Canonical)
```http
GET /sim/runs/{id}/index
Response: series/index.json (application/json)

GET /sim/runs/{id}/series/{seriesId}  
Response: CSV stream (t,value)
```

### Template Management
```http
GET /api/templates
GET /api/templates/{id}
GET /api/templates/{id}/schema
```

### Catalog Management
```http
GET /api/catalogs
GET /api/catalogs/{id}
```

---

## New Code/Files

```
ui/FlowTime.UI/Services/Http/
  FlowTimeSimApiClient.cs
  SimResultsService.cs
  
ui/FlowTime.UI/Services/
  FeatureFlagService.cs (enhanced)
  
ui/FlowTime.UI/Components/
  TemplateRunner.razor (enhanced)
  TemplateGallery.razor (enhanced)
  CatalogPicker.razor (enhanced)
  SimulationResults.razor (enhanced)
  
src/FlowTime.API/Services/
  RunArtifactWriter.cs (shared)
  
tests/FlowTime.Api.Tests/
  ArtifactEndpointTests.cs (enhanced)
  
scripts/
  README.md
  test-api-integration.sh
  test-api.yaml
  test-download.sh
  test-ui-yaml.yaml
  test.yaml
```

## Critical Changes Required from UI-M1

### 1. **Remove Custom Metadata Dependencies**
The UI-M1 mock returns custom metadata fields that violate the artifact-first principle:
```csharp
// REMOVE: Custom metadata fields from UI-M1 mock
["stats.totalDemand"] = GetMockStatistic("totalDemand", request),
["stats.avgThroughput"] = GetMockStatistic("avgThroughput", request),
["timeSeries.demand.min"] = GetMockStatistic("demandMin", request),
// ... etc
```

### 2. **Implement Artifact-First Result Reading**
Replace custom metadata with canonical artifact reading:
```csharp
// NEW: Read from canonical artifacts
var indexResponse = await httpClient.GetAsync($"/sim/runs/{runId}/index");
var seriesIndex = await indexResponse.Content.ReadFromJsonAsync<SeriesIndex>();

foreach (var series in seriesIndex.Series)
{
    var csvResponse = await httpClient.GetAsync($"/sim/runs/{runId}/series/{series.Id}");
    var csvData = await csvResponse.Content.ReadAsStringAsync();
    // Parse CSV and create visualization
}
```

### 3. **Update SimulationResults Component**
The results display must read from `series/index.json` rather than custom metadata:
- Parse series from canonical index
- Load individual CSV streams for visualization
- Calculate statistics from actual series data
- No hardcoded assumptions about available series


---

### Acceptance Criteria

### Mode Toggle & UX Enhancement ✅
- [x] Mode switching between SIM and API modes works reliably
- [x] Mode toggle persists across browser sessions
- [x] Components properly refresh when mode changes
- [x] RUN button shows clear disabled state with helpful tooltips
- [x] User feedback for disabled states with explanatory alerts

### Real API Integration ✅
- [x] Replace mock services with real FlowTime-Sim API calls
- [x] FlowTimeSimApiClient for artifact-first communication
- [x] SimResultsService for loading CSV series data from artifacts
- [x] TemplateService updated to get scenarios from /sim/scenarios
- [x] FlowTimeSimService calls /sim/run following integration spec
- [x] SimulationResults component loads real data from series index

### Template Integration ✅
- [x] Template mode switching works between mock (API) and SIM catalogs
- [x] Template gallery refreshes when mode changes
- [x] Template loading shows proper loading states
- [x] Template selection is preserved appropriately during mode changes

### Catalog Integration ✅
- [x] Catalog picker supports both API and SIM modes
- [x] Catalog loading shows proper loading states  
- [x] Catalog selection clears when switching modes
- [x] Mode changes trigger catalog refresh automatically

### Error Handling & UX ✅
- [x] Clear tooltips explain why RUN button is disabled
- [x] Alert messages guide users when selections are incomplete
- [x] Mode switching provides visual feedback
- [x] Component remounting handles stale state properly
- [x] API series endpoint returns 404 for missing index.json (not 500)

### Development Integration ✅
- [x] Integration test scripts working with current API
- [x] Repository organized with scripts in proper directory
- [x] Documentation updated for current endpoints and usage
- [x] Build and test tasks properly configured
- [x] Comprehensive scripts/README.md with usage instructions

### Artifact-First Architecture ✅
- [x] UI reads simulation results from canonical run artifacts (`runs/<runId>/...`)
- [x] Results loaded via `series/index.json` discovery pattern
- [x] Individual series streamed via `/runs/{id}/series/{seriesId}` 
- [x] **No dependency on custom metadata fields** from UI-M1 mock
- [x] Simulation execution via `POST /sim/run` → `{ simRunId }` pattern
- [x] Compliance with `schemaVersion: 1` artifact contract

### Test Coverage Enhancement ✅
- [x] Expanded test coverage from 33 to 35 tests (all passing)
- [x] Added critical API endpoint tests for artifact handling
- [x] Test for missing index.json returning 404 (not 500)
- [x] Test for partial series name matching functionality
- [x] Removed unused Moq dependency from FlowTime.UI.Tests

---

## Testing Strategy

### Integration Testing
- End-to-end testing with running FlowTime-Sim service
- Template loading and parameter form generation
- Complete simulation workflow execution
- Error scenario testing (network failures, invalid data)

### Performance Testing
- API response time measurement
- UI responsiveness under load
- Memory usage with real data sets
- Background polling impact assessment

### User Acceptance Testing
- Complete user workflows with real data
- Error handling from user perspective
- Performance from user experience standpoint
- Cross-browser compatibility testing

---

## Dependencies

### FlowTime-Sim Service Integration Spec Compliance
- **Critical**: Current UI-M1 mock violates sim-integration-spec.md
- **Required**: Mock must write canonical run artifacts (`runs/<runId>/...`)
- **Required**: UI must read from `series/index.json` and stream CSVs
- **Architecture**: Artifact-first pattern, not custom JSON blobs

### FlowTime-Sim Service Requirements
- SIM-SVC-M2: Template management endpoints
- SIM-CAT-M2: Catalog management endpoints  
- Running FlowTime-Sim service for integration testing
- Compliance with canonical artifact format (`schemaVersion: 1`)

### Infrastructure
- Network connectivity to FlowTime-Sim service
- Configuration management for different environments
- File system access for artifact reading (if using file-based adapters)
- Authentication/authorization if required

---

## Success Metrics

### Functional Success
- 100% of UI-M1 mock functionality replaced with real API calls
- Zero breaking changes to UI-M1 user experience
- All simulation workflows execute with real backend
- Error scenarios handled better than mock implementation

### Performance Success
- API calls complete within 5 seconds for normal operations
- UI remains responsive during all API operations
- Simulation status updates within 2 seconds of backend changes
- Memory usage remains stable over extended usage

### User Experience Success
- No regression in UI-M1 user experience
- Enhanced feedback during simulation execution
- Clear error messages guide users to resolution
- Seamless transition from UI-M1 mock to real implementation

---

## Future Extensions (Post UI-M2)

- **Caching Strategy:** Local caching of templates and catalogs
- **Offline Support:** Queue operations when offline
- **Advanced Progress:** Detailed simulation progress indicators
- **Result Visualization:** Enhanced charts and analysis tools
- **Collaboration Features:** Share simulations and results
- **Performance Optimization:** Request batching and optimization
