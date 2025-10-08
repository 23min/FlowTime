# Gold-First KISS Implementation Roadmap

**Branch:** gold-first-kiss  
**Base Commit:** 08a7fddc  
**Status:** Ready to Implement  

---

## Executive Summary

This document defines the concrete implementation plan for FlowTime time-travel capability using the KISS (Keep It Simple, Stupid) architecture. Based on 6 validated architectural decisions, we have a clear path to deliver core time-travel capability.

**Key Success Metrics:**
- ✅ Core time-travel working (window, topology, /state APIs)
- ✅ Synthetic gold telemetry generation
- ✅ Template system operational
- ✅ Demo-ready with example systems

---

## Milestone Overview

| Milestone | Description | Dependencies |
|-----------|-------------|--------------|
| M3.0 | Foundation + Fixtures | None |
| M3.1 | Time-Travel APIs | M3.0 |
| M3.2 | TelemetryLoader + Templates | M3.0 |
| M3.3 | Validation + Polish | M3.1, M3.2 |

---

## M3.0: Foundation + Fixtures

### Goal
Extend model schema and engine to support window, topology, file sources, and explicit initial conditions. Create synthetic gold telemetry fixtures for testing.

### Deliverables

**Schema Extensions:**
1. Window section in model schema (start, timezone)
2. Topology section (nodes with kind/semantics, edges)
3. File source support for const nodes (`source: "file://path"`)
4. Initial condition enforcement for self-referencing SHIFT

**Engine Changes:**
1. TimeGrid.StartTimeUtc field (DateTime?)
2. Window and Topology models (TopologyDefinition, TopologyNode, SemanticMapping)
3. FileSourceResolver (path resolution, CSV reading)
4. ModelParser extensions (parse window, topology)
5. ModelValidator extensions (topology validation)

**Fixtures:**
1. Order System (service + queue)
2. Microservices (3 services, 2 queues)
3. HTTP Service (stateless, no queue)

### Acceptance Criteria

**AC1: Window Parsing**
```yaml
# Model with window parses successfully
window:
  start: "2025-10-07T00:00:00Z"
  timezone: "UTC"

grid:
  bins: 288
  binSize: 5
  binUnit: "minutes"
```

**AC2: Topology Parsing**
```yaml
topology:
  nodes:
    - id: "OrderService"
      kind: "service"
      semantics:
        arrivals: "orders_arrivals"
        served: "orders_served"
        capacity: null  # Optional
  edges:
    - from: "OrderService:out"
      to: "OrderQueue:in"
```

**AC3: File Source Loading**
```yaml
nodes:
  - id: orders_arrivals
    kind: const
    source: "file://fixtures/order-system/arrivals.csv"
```

**AC4: Initial Condition Enforcement**
```yaml
# Valid (has initial):
- id: queue_depth
  kind: expr
  expr: "MAX(0, SHIFT(queue_depth, 1) + inflow - outflow)"
  initial: 0

# Invalid (missing initial) → Parse Error
```

**AC5: TimeGrid with Start Time**
```csharp
var grid = new TimeGrid(...) { StartTimeUtc = startTime };
var binTimestamp = grid.GetBinStartUtc(binIndex);
Assert.Equal("2025-10-07T02:30:00Z", binTimestamp);
```

### Test Coverage

**Unit Tests (20 tests):**
- Window parsing (valid, invalid timezone, missing start)
- Topology parsing (valid, missing required fields, invalid references)
- File source resolution (relative, absolute, not found)
- Initial condition validation (missing, scalar, reference)
- TimeGrid timestamp computation

**Integration Tests (5 tests):**
- End-to-end: model with window + topology → artifacts
- File sources load correctly from fixture directory
- Self-referencing SHIFT evaluates with initial condition
- Backward compatibility: M2.10 model still works

**Golden Tests (2 tests):**
- Fixed model with window/topology → consistent artifacts
- Fixture regression: order-system fixture → known output

### Files to Create/Modify

**Core:**
- `src/FlowTime.Core/TimeGrid.cs` (add StartTimeUtc)
- `src/FlowTime.Core/Models/Window.cs` (NEW)
- `src/FlowTime.Core/Models/Topology.cs` (NEW)
- `src/FlowTime.Core/Models/ModelParser.cs` (extend)
- `src/FlowTime.Core/Models/ModelValidator.cs` (extend)
- `src/FlowTime.Core/FileSourceResolver.cs` (NEW)
- `src/FlowTime.Core/Evaluator.cs` (handle initial conditions)

**Fixtures:**
- `fixtures/order-system/model.yaml`
- `fixtures/order-system/arrivals.csv`
- `fixtures/order-system/served.csv`
- `fixtures/order-system/queue_depth.csv`
- `fixtures/microservices/...` (similar structure)
- `fixtures/http-service/...` (stateless service)

**Tests:**
- `tests/FlowTime.Core.Tests/TimeGridTests.cs` (extend)
- `tests/FlowTime.Core.Tests/WindowParserTests.cs` (NEW)
- `tests/FlowTime.Core.Tests/TopologyParserTests.cs` (NEW)
- `tests/FlowTime.Core.Tests/FileSourceTests.cs` (NEW)
- `tests/FlowTime.Core.Tests/InitialConditionTests.cs` (NEW)

### Risks and Mitigation

| Risk | Mitigation |
|------|------------|
| File path security (directory traversal) | Validate no "..", restrict to model directory |
| CSV parsing edge cases | Use CsvHelper library, test CRLF/LF |
| Initial condition type confusion | Clear error messages, examples in docs |
| Backward compatibility breaks | Add tests for M2.10 models |

---

## M3.1: Time-Travel APIs

### Goal
Implement /state and /state_window endpoints for bin-level querying with derived metrics. Enable UI time-travel integration.

### Deliverables

**API Endpoints:**
1. GET /v1/runs/{runId}/state?binIndex={idx}
2. GET /v1/runs/{runId}/state_window?startBin={idx}&endBin={idx}
3. Response includes window, bin timestamps, node values

**Derived Metrics:**
1. Utilization: `served / capacity` (if capacity present, else null)
2. Latency (Little's Law): `queue / served × binMinutes`
3. Throughput ratio: `served / arrivals` (proxy when capacity unknown)

**Node Coloring:**
1. Services: Green (<0.7), Yellow (0.7-0.9), Red (≥0.9) based on utilization
2. Queues: Green (latency ≤ SLA), Yellow (≤1.5×SLA), Red (>1.5×SLA)
3. Unknown capacity: Gray (no coloring without utilization)

### Acceptance Criteria

**AC1: /state Single Bin**
```
GET /v1/runs/run_abc123/state?binIndex=42

Response:
{
  "runId": "run_abc123",
  "mode": "simulation",  // From provenance
  "window": {
    "start": "2025-10-07T00:00:00Z",
    "timezone": "UTC"
  },
  "grid": {
    "bins": 288,
    "binSize": 5,
    "binUnit": "minutes",
    "binMinutes": 5
  },
  "bin": {
    "index": 42,
    "startUtc": "2025-10-07T03:30:00Z",
    "endUtc": "2025-10-07T03:35:00Z"
  },
  "nodes": {
    "OrderService": {
      "kind": "service",
      "arrivals": 150,
      "served": 145,
      "errors": 5,
      "capacity": null,
      "utilization": null,  // No capacity
      "color": "gray"
    },
    "OrderQueue": {
      "kind": "queue",
      "arrivals": 145,
      "served": 140,
      "queue": 8,
      "latency_min": 0.286,  // (8/140)*5
      "sla_min": 5.0,
      "color": "green"
    }
  }
}
```

**AC2: /state_window Time Series**
```
GET /v1/runs/run_abc123/state_window?startBin=0&endBin=144

Response:
{
  "runId": "run_abc123",
  "window": {...},
  "grid": {...},
  "slice": {
    "startBin": 0,
    "endBin": 144,
    "bins": 144
  },
  "timestamps": [
    "2025-10-07T00:00:00Z",
    "2025-10-07T00:05:00Z",
    ...
  ],
  "nodes": {
    "OrderService": {
      "kind": "service",
      "series": {
        "arrivals": [120, 125, 130, ...],  // 144 values
        "served": [118, 124, 128, ...],
        "utilization": [null, null, ...]   // No capacity
      }
    },
    "OrderQueue": {
      "kind": "queue",
      "series": {
        "queue": [5, 6, 8, ...],
        "latency_min": [0.042, 0.048, 0.063, ...]
      }
    }
  }
}
```

**AC3: Coloring Logic**
```csharp
// Service with capacity
utilization = 0.85 → "yellow"
utilization = 0.95 → "red"

// Queue with SLA
latency_min = 4.0, sla_min = 5.0 → "green"
latency_min = 7.0, sla_min = 5.0 → "yellow"
latency_min = 10.0, sla_min = 5.0 → "red"

// Missing capacity
capacity = null → "gray"
```

**AC4: Graceful Null Handling**
```csharp
// If capacity is null:
utilization = null  // Not 0, not error

// If queue is 0:
latency_min = 0.0  // Not division by zero

// If served is 0:
latency_min = null  // Can't compute
```

### Test Coverage

**Unit Tests (15 tests):**
- Utilization computation (with/without capacity)
- Latency computation (Little's Law)
- Coloring rules (all thresholds)
- Null handling (missing capacity, zero queue, zero served)
- Timestamp computation from binIndex

**Integration Tests (8 tests):**
- /state endpoint contract (200 OK, correct shape)
- /state_window endpoint contract
- Error cases (invalid runId, invalid binIndex, bin out of range)
- Backward compatibility (run without window → 400 error)

**Golden Tests (2 tests):**
- Fixed run + binIndex → consistent /state response
- Fixed run + window → consistent /state_window response

### Files to Create/Modify

**API:**
- `src/FlowTime.API/Program.cs` (add /state endpoints)
- `src/FlowTime.API/Handlers/StateHandler.cs` (NEW)
- `src/FlowTime.API/Handlers/StateWindowHandler.cs` (NEW)

**Contracts:**
- `src/FlowTime.Contracts/StateResponse.cs` (NEW)
- `src/FlowTime.Contracts/StateWindowResponse.cs` (NEW)
- `src/FlowTime.Contracts/NodeState.cs` (NEW)

**Metrics:**
- `src/FlowTime.Core/Metrics/UtilizationComputer.cs` (NEW)
- `src/FlowTime.Core/Metrics/LatencyComputer.cs` (NEW)
- `src/FlowTime.Core/Metrics/ColoringRules.cs` (NEW)

**Tests:**
- `tests/FlowTime.API.Tests/StateEndpointTests.cs` (NEW)
- `tests/FlowTime.API.Tests/StateWindowEndpointTests.cs` (NEW)
- `tests/FlowTime.Core.Tests/MetricsTests.cs` (NEW)

### Dependencies
- M3.0 (requires window and topology in model)
- Fixtures from M3.0 (for integration testing)

### Risks and Mitigation

| Risk | Mitigation |
|------|------------|
| API response too large (288 bins × 50 nodes) | Pagination for state_window, limit node count |
| Timestamp computation bugs | Thorough unit tests, validate against manual calculation |
| Coloring threshold debates | Make configurable in topology (sla_min per node) |

---

## M3.2: TelemetryLoader + Templates

### Goal
Implement TelemetryLoader to extract from ADX/CSV and Template system to instantiate models.

### Deliverables

**TelemetryLoader:**
1. Load from CSV files (simpler than ADX, use for development)
2. Load from Azure Data Explorer (future production)
3. Dense bin filling (zero-fill with warnings for gaps)
4. Manifest generation (provenance, warnings, checksums)

**Template System:**
1. Template parser (YAML with {{parameter}} substitution)
2. Parameter validation (types, defaults)
3. Template instantiation (resolve parameters → model.yaml)
4. Example templates (order-system, microservices)

**Synthetic Gold Generator:**
1. Tool: FlowTime simulation run → Gold CSV files
2. Converts series artifacts to NodeTimeBin format
3. Adds provenance metadata
4. Creates manifest.json

### Acceptance Criteria

**AC1: Load from CSV**
```csharp
var loader = new TelemetryLoader();
var result = loader.LoadFromCsv(new LoadRequest {
    Directory = "fixtures/gold-telemetry/",
    Window = new Window { Start = "2025-10-07T00:00:00Z", ... },
    Nodes = ["OrderService", "OrderQueue"]
});

Assert.Equal(288, result.Files.Count);
Assert.Contains("OrderService_arrivals.csv", result.Files);
```

**AC2: Dense Fill with Warnings**
```csharp
// Input: Bins [0,1,2,...,11, 14,15,...] (missing 12,13)
// Output: Bins [0,1,2,...,287] (filled with zeros)
// Warnings: [{ type: "data_gap", bins: [12,13] }]
```

**AC3: Template Instantiation**
```yaml
# templates/telemetry/order-system.yaml
parameters:
  - name: telemetry_dir
    type: string
  - name: window_start
    type: timestamp
  - name: q0
    type: number
    default: 0

topology:
  nodes:
    - id: "OrderService"
      semantics:
        arrivals: "{{telemetry_dir}}/OrderService_arrivals.csv"

# Instantiate with: {telemetry_dir: "fixtures/", window_start: "..."}
# Result: arrivals: "fixtures/OrderService_arrivals.csv"
```

**AC4: Synthetic Gold Generation**
```bash
# Generate gold telemetry from simulation
dotnet run --project tools/SyntheticGold -- \
  --simulation examples/order-system.yaml \
  --output fixtures/gold-telemetry/ \
  --bins 288
  
# Output:
# fixtures/gold-telemetry/OrderService_arrivals.csv
# fixtures/gold-telemetry/OrderService_served.csv
# fixtures/gold-telemetry/manifest.json
```

### Test Coverage

**Unit Tests (20 tests):**
- CSV loading (valid, missing file, wrong row count)
- Dense fill (gaps at start, middle, end)
- Template parsing (valid, invalid syntax, missing parameters)
- Parameter substitution (string, number, timestamp)
- Synthetic gold generation (simulation → CSV)

**Integration Tests (5 tests):**
- End-to-end: CSV → Template → Model → Engine → /state
- Multiple nodes loaded correctly
- Warnings propagated to API response

### Files to Create/Modify

**TelemetryLoader:**
- `src/FlowTime.Adapters.Telemetry/` (NEW project)
- `src/FlowTime.Adapters.Telemetry/TelemetryLoader.cs`
- `src/FlowTime.Adapters.Telemetry/CsvReader.cs`
- `src/FlowTime.Adapters.Telemetry/DenseFiller.cs`
- `src/FlowTime.Adapters.Telemetry/ManifestWriter.cs`

**Templates:**
- `src/FlowTime.Templates/` (NEW project)
- `src/FlowTime.Templates/TemplateParser.cs`
- `src/FlowTime.Templates/ParameterResolver.cs`
- `templates/telemetry/order-system.yaml`
- `templates/telemetry/microservices.yaml`

**Synthetic Gold:**
- `tools/SyntheticGold/` (NEW project)
- `tools/SyntheticGold/Program.cs`
- `tools/SyntheticGold/GoldConverter.cs`

**Tests:**
- `tests/FlowTime.Adapters.Telemetry.Tests/`
- `tests/FlowTime.Templates.Tests/`

### Dependencies
- M3.0 (requires file source support)

### Risks and Mitigation

| Risk | Mitigation |
|------|------------|
| Template syntax gets complex | Keep simple (only {{param}} substitution, no logic) |
| CSV format variations | Document format strictly, validate on load |
| Large telemetry files (memory) | Stream CSV row-by-row |

---

## M3.3: Validation + Polish

### Goal
Add post-evaluation validation, observability, and production-ready polish.

### Deliverables

**Validation Framework:**
1. Conservation validator (arrivals - served ≈ Δqueue)
2. Mode-based severity (warnings for telemetry, errors for simulation)
3. Warning collection and reporting in API responses

**Observability:**
1. Structured logging (trace IDs, operation names, timing)
2. Performance logging (evaluation duration, file I/O)
3. Error logging with context

**Documentation:**
1. API reference (all endpoints)
2. Template authoring guide
3. Synthetic gold generation guide
4. Schema reference (window, topology, gold CSV format)

**Performance:**
1. Benchmark: 288-bin model evaluates in <500ms
2. /state response in <50ms
3. /state_window (144 bins) in <200ms

### Acceptance Criteria

**AC1: Conservation Validation**
```csharp
// Telemetry mode (warnings):
POST /v1/runs (mode=telemetry)
Response 200 OK:
{
  "warnings": [{
    "type": "conservation_violation",
    "severity": "medium",
    "bins": [42],
    "message": "arrivals - served != delta_queue",
    "details": { "arrivals": 100, "served": 95, "delta_queue": 3, "expected": 5 }
  }]
}

// Simulation mode (errors):
POST /v1/runs (mode=simulation)
Response 400 Bad Request:
{
  "error": "ValidationFailed",
  "message": "Conservation violation at bin 42"
}
```

**AC2: Structured Logging**
```csharp
logger.LogInformation(
    "Model evaluation completed in {DurationMs}ms for run {RunId}",
    duration,
    runId
);
// Output: {"@t":"2025-10-07T14:30:00Z","@m":"Model evaluation completed in 245ms for run run_abc123","DurationMs":245,"RunId":"run_abc123"}
```

**AC3: Performance Benchmark**
```
Benchmark: 288-bin order-system model
- Parse: 50ms
- Evaluate: 180ms
- Write artifacts: 70ms
- Total: 300ms ✅ (<500ms target)
```

### Test Coverage

**Unit Tests (15 tests):**
- Conservation validation (pass, fail, tolerance)
- Mode detection (telemetry vs simulation)
- Warning collection and formatting

**Integration Tests (5 tests):**
- End-to-end with warnings
- End-to-end with errors (simulation mode)
- Structured logging output validation

**Performance Tests (3 tests):**
- 288-bin evaluation benchmark
- /state response time
- /state_window response time

### Files to Create/Modify

**Validation:**
- `src/FlowTime.Core/Validation/` (NEW namespace)
- `src/FlowTime.Core/Validation/ConservationValidator.cs`
- `src/FlowTime.Core/Validation/ValidationContext.cs`
- `src/FlowTime.Core/Validation/WarningCollector.cs`

**Logging:**
- `src/FlowTime.Core/Logging/StructuredLogger.cs` (NEW)
- `src/FlowTime.API/Program.cs` (add logging middleware)

**Documentation:**
- `docs/api/state-endpoints.md` (NEW)
- `docs/templates/authoring-guide.md` (NEW)
- `docs/schemas/gold-csv-format.md` (NEW)
- `docs/schemas/model-schema-v1.1.md` (UPDATE)

**Tests:**
- `tests/FlowTime.Core.Tests/ValidationTests.cs` (NEW)
- `tests/FlowTime.API.Tests/PerformanceTests.cs` (NEW)

### Dependencies
- M3.0, M3.1, M3.2 (integrates all)

---

## Post-M3 (Future Work)

### M3.4: Capacity Inference + Overlay Scenarios (P1)

**Deliverables:**
1. Capacity inference in API layer (saturation method)
2. Confidence labeling (high/medium/low)
3. Overlay scenario support (Gold baseline + modeled changes)
4. Template overlays (modify semantics at instantiation)

**Not Blocking:** Can ship M3.0-M3.3 without this.

### M4.0: Catalog_Nodes Integration (P2)

**Deliverables:**
1. Query Catalog_Nodes for topology
2. Hybrid mode (template + discovered nodes)
3. Dynamic node listing API

**Use Case:** Large microservice fleets (100+ services).

---

## Testing Strategy

### Unit Test Coverage
- **Target:** 80% code coverage
- **Focus:** Core logic, validation, metrics computation
- **Tools:** xUnit, FluentAssertions

### Integration Test Coverage
- **Target:** All API endpoints, end-to-end flows
- **Focus:** Contract validation, error handling
- **Tools:** WebApplicationFactory, HttpClient

### Golden Test Coverage
- **Target:** Fixed inputs → consistent outputs
- **Focus:** Regression detection, artifact stability
- **Tools:** Snapshot testing, file comparison

### Performance Test Coverage
- **Target:** Meet performance criteria
- **Focus:** Evaluation speed, API latency
- **Tools:** BenchmarkDotNet

---

## Success Criteria

### M3.0-M3.3 Complete When:
- ✅ All 60+ tests passing
- ✅ 3 fixture systems working (order, microservices, HTTP)
- ✅ /state and /state_window returning correct data
- ✅ Synthetic gold generator produces valid CSV
- ✅ Templates instantiate correctly
- ✅ Validation framework operational
- ✅ Documentation complete
- ✅ Performance benchmarks met

### Demo Scenario (End of M3.3):
1. Generate synthetic gold telemetry from simulation
2. Create template for order-system
3. Instantiate template with telemetry
4. POST /v1/runs → create run
5. GET /v1/state?binIndex=42 → see snapshot
6. GET /v1/state_window → see time series
7. UI displays time-travel scrubber

---

## Risk Register

### High Risk
| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| File I/O performance issues | Medium | High | Stream CSVs, cache in memory, benchmark early |
| API response size too large | Medium | Medium | Pagination, node filtering, compression |
| Timestamp arithmetic bugs | Low | High | Extensive unit tests, UTC-only, bin alignment validation |

### Medium Risk
| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| Template complexity creep | Medium | Medium | Keep syntax simple, defer advanced features |
| Validation false positives | Medium | Low | Make thresholds configurable, clear messages |
| CSV format variations | Low | Medium | Strict validation, document format clearly |

### Low Risk
| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| Backward compatibility issues | Low | Medium | Test M2.10 models, graceful degradation |
| Security (directory traversal) | Low | High | Path validation, restrict to model directory |

---

## Implementation Sequence

```
M3.0: Foundation + Fixtures
  ├─ Schema extensions (window, topology)
  ├─ File sources
  ├─ Initial conditions
  └─ Create 3 fixture systems

M3.1: Time-Travel APIs (depends on M3.0)
  ├─ /state endpoint
  ├─ /state_window endpoint
  ├─ Derived metrics
  └─ UI can integrate (parallel work)

M3.2: TelemetryLoader + Templates (depends on M3.0)
  ├─ CSV loader
  ├─ Template parser
  ├─ Synthetic gold generator
  └─ Example templates

M3.3: Validation + Polish (depends on M3.1, M3.2)
  ├─ Validation framework
  ├─ Structured logging
  ├─ Documentation
  └─ Performance tuning
```

---

## Next Actions

1. **Review and Approve:** Validate this roadmap
2. **Create M3.0 Spec:** Detailed implementation spec
3. **Set Up Environment:** Dev containers, test data
4. **Begin M3.0:** Start with schema extensions

**Ready to proceed?**

---

**End of Roadmap Document**
