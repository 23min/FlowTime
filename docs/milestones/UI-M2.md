# UI-M2 — Real API Integration

> **Target Project:** FlowTime UI  
> **Prerequisites:** UI-M1 ✅, SVC-M1 ✅, SYN-M0 ✅  
> **FlowTime-Sim Dependencies:** SIM-SVC-M2, SIM-CAT-M2  

---

## Goal

Replace mock services with real FlowTime-Sim API integration to make the Template Runner fully functional with live simulation execution. Transform the UI-M1 prototype into a production-ready interface that communicates with actual backend services.

## Why This Approach

- **Production Readiness:** Move from mock data to real simulation engine
- **End-to-End Validation:** Test complete integration between UI and backend services
- **Real User Value:** Enable actual simulation workflows with live results
- **Architecture Validation:** Prove the service layer design with real API calls
- **Performance Testing:** Understand real-world latency and error scenarios

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

### FR-UI-M2-1: Real Template Service Integration
- Replace `TemplateServiceImplementations` mock with HTTP-based implementation
- Connect to FlowTime-Sim `/templates` endpoints
- Handle template schema loading and validation
- Support template search and categorization

### FR-UI-M2-2: Real Catalog Service Integration  
- Replace catalog mock service with real API calls
- Connect to FlowTime-Sim catalog endpoints
- Load actual system catalogs with real metadata
- Handle catalog availability and status information

### FR-UI-M2-3: Live Simulation Execution (Artifact-First)
- Replace mock simulation service with real FlowTime-Sim API calls
- Execute simulations via `POST /sim/run` → `{ simRunId }`
- Read results from canonical run artifacts (`runs/<runId>/...`)
- Parse `series/index.json` to discover available series
- Stream individual CSVs via `/sim/runs/{id}/series/{seriesId}`
- **Remove dependency on custom metadata fields** from UI-M1 mock

### FR-UI-M2-4: Real-Time Status Tracking
- Implement polling for simulation progress
- Display live status updates during execution
- Handle long-running simulation scenarios
- Provide cancellation capabilities

### FR-UI-M2-5: Enhanced Error Handling
- Network timeout and retry logic
- API error response handling
- User-friendly error messages
- Offline/connectivity detection

### FR-UI-M2-6: Configuration Management
- Environment-specific API endpoints
- Authentication/authorization support
- Configurable timeouts and retry policies
- Development vs production settings

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
    "BaseUrl": "http://localhost:8081",
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
  HttpTemplateService.cs
  HttpCatalogService.cs
  HttpFlowTimeSimService.cs
  ApiClientFactory.cs
  
ui/FlowTime.UI/Configuration/
  FlowTimeSimApiOptions.cs
  ApiConfiguration.cs
  
ui/FlowTime.UI/Models/Api/
  ApiResponse.cs
  ApiError.cs
  SimulationStatusDto.cs
  
ui/FlowTime.UI/Services/Background/
  SimulationPollingService.cs
  
ui/FlowTime.UI/Extensions/
  ServiceCollectionExtensions.cs
  
tests/FlowTime.UI.Tests/Services/
  HttpTemplateServiceTests.cs
  HttpCatalogServiceTests.cs
    HttpFlowTimeSimServiceTests.cs
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

### Artifact-First Integration ✅
- [ ] UI reads simulation results from canonical run artifacts (`runs/<runId>/...`)
- [ ] Results loaded via `series/index.json` discovery pattern
- [ ] Individual series streamed via `/runs/{id}/series/{seriesId}` 
- [ ] **No dependency on custom metadata fields** from UI-M1 mock
- [ ] Simulation execution via `POST /sim/run` → `{ simRunId }` pattern

### Template Integration ✅
- [ ] Real templates loaded from FlowTime-Sim API
- [ ] Template search and filtering works with live data
- [ ] Parameter schemas loaded dynamically from backend
- [ ] Template availability reflects actual service status

### Catalog Integration ✅
- [ ] System catalogs loaded from real API endpoints
- [ ] Catalog metadata displays actual system information
- [ ] Catalog availability and health status shown
- [ ] Dropdown properly displays real catalog options

### Real-Time Features ✅
- [ ] Simulation progress tracked with live updates
- [ ] Status changes reflected in UI immediately
- [ ] Long-running simulations handled gracefully
- [ ] Users can cancel running simulations

### Schema Compliance ✅
- [ ] All artifacts follow `schemaVersion: 1` contract
- [ ] `run.json` includes required fields: `source`, `engineVersion`, `grid`, `series[]`
- [ ] `manifest.json` includes `rng`, `seriesHashes`, `eventCount`
- [ ] `series/index.json` includes canonical series metadata
- [ ] Series IDs follow `measure@componentId[@class]` convention
- [ ] Units properly specified: flows=`entities/bin`, levels=`entities`, latency=`minutes`

### Error Handling ✅
- [ ] Network failures handled with appropriate user feedback
- [ ] API errors displayed with actionable messages
- [ ] Retry logic works for transient failures
- [ ] Offline scenarios handled gracefully

### Performance ✅
- [ ] API calls complete within acceptable timeframes
- [ ] UI remains responsive during API operations
- [ ] Background polling doesn't impact user experience
- [ ] Memory usage remains stable with real data

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
