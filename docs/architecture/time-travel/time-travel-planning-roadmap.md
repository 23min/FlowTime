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

## Design Principles

- **Keep It Simple, Stupid (KISS):** Favor explicit files and declarative templates over adapters or inference so each milestone stays reviewable and low risk (see `time-travel-architecture-ch1-overview.md`).
- **Telemetry Is Just File Input:** Treat historical telemetry exactly like synthetic fixtures; the engine never needs to know the origin of a series (`time-travel-architecture-ch1-overview.md` §1.2).
- **Synthetic-First, Self-Hosted:** Ship curated fixture systems before wiring real ADX data so APIs and UI can integrate without external dependencies (`time-travel-planning-decisions.md` Q5).
- **UTC-Anchored Grid:** Every run is anchored to an absolute start timestamp with fixed bin size/unit to eliminate timezone drift (`time-travel-architecture-ch2-data-contracts.md`).
- **Deterministic Single Mode:** One evaluation path powers both simulation and telemetry playback, keeping derived metrics predictable (`time-travel-architecture-ch1-overview.md`).
- **Mode-Based Validation:** Telemetry runs surface warnings, simulation runs fail fast on errors—simplicity with clear operator feedback (`time-travel-planning-decisions.md` Q3).
- **Incremental Delivery:** Milestones M-03.00 → M-03.02 → M-03.02.01 → M-03.03 are sequenced so each step is independently demoable and testable before layering the next capability (`time-travel-architecture-ch5-implementation-roadmap.md`).

### Service Time Contract (TT‑M‑03.29)

TT‑M‑03.29 expands the `service` semantics with optional `processingTimeMsSum` (per-bin milliseconds of active work) and `servedCount`. When both are provided, the engine derives:

```
serviceTimeMs = processingTimeMsSum / max(1, servedCount)
```

The derived metric ships in `/state` and `/state_window`, giving the UI a stable basis for the “Service Time” color mode and inspector sparkline. Every gallery template (incident workflow, IT microservices, all supply-chain variants, manufacturing, network reliability, and transportation demos) now emits those series so operators can see latency and service time without editing YAML. The UI currently uses static thresholds (green ≤ 400 ms, yellow ≤ 700 ms, red beyond) until quantile-based tuning arrives in TT‑M‑03.30.

### Flow Latency (Source → Node)

- Definition: cumulative queue latency (Little’s Law per queue) plus service time along the dominant upstream path from a source to the current node. No fan-in averaging/blending.
- Computation: pick the highest-volume predecessor per bin; `flowLatencyMs = upstreamFlowLatencyMs + (queueLatencyMinutes * 60000 or serviceTimeMs)`.
- Exposure: `/state` and `/state_window` include `flowLatencyMs` per node; null when inputs are missing. UI shows flow latency sparkline/metric; dashboard can surface flow latency KPI.
- Telemetry: works for simulation and real telemetry as long as queue depth/served and processingTimeMsSum/servedCount are present; otherwise emits info-level warnings.

---

## Milestone Overview

| Milestone | Description | Dependencies |
|-----------|-------------|--------------|
| M-03.00 | Foundation + Fixtures | None |
| M-03.01 | Time-Travel APIs | M-03.00 |
| M-03.02 | TelemetryLoader + Templates | M-03.00 |
| M-03.02.01 | Simulation Run Orchestration | M-03.00, M-03.01, M-03.02 |
| M-03.03 | Validation + Polish | M-03.01, M-03.02, M-03.02.01 |

---

## M-03.00: Foundation + Fixtures

### Goal
Extend model schema and engine to support window, topology, file sources, and explicit initial conditions. Create synthetic gold telemetry fixtures for testing.

### Why This Matters
- Anchors every run to real-world time so `/state` can translate bin indices into UTC timestamps (`time-travel-architecture-ch2-data-contracts.md`).
- Establishes topology semantics and fixtures that unblock API and UI development without waiting on production telemetry (`time-travel-planning-decisions.md` Q5).
- Forces explicit initial conditions so stateful expressions behave deterministically across simulation and telemetry runs (`time-travel-architecture-ch3-components.md`).

### Deliverables

**Schema Extensions:**
1. Window section in model schema (`startTimeUtc`, timezone)
2. Topology section (nodes with semantics → `file:` URIs, edges)
3. File-based telemetry wiring via topology semantics
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
- Backward compatibility: M-02.10 model still works

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
| Backward compatibility breaks | Add tests for M-02.10 models |

---

## M-03.01: Time-Travel APIs

### Goal
Implement /state and /state_window endpoints for bin-level querying with derived metrics. Enable UI time-travel integration.

### Why This Matters
- Provides the UI with per-bin snapshots and slices so time-travel scrubbing is possible without additional data services (`time-travel-architecture-ch4-data-flows.md`).
- Computes utilization and latency centrally, ensuring consistent business logic for both telemetry replay and simulation comparisons (`time-travel-planning-decisions.md` Q1/Q5).
- Establishes the node-coloring contract that communicates saturation and SLA breaches directly from backend to visualization (`time-travel-architecture-ch4-data-flows.md`).

### Deliverables

**API Endpoints:**
1. GET /v1/runs/{runId}/state?binIndex={idx}
2. GET /v1/runs/{runId}/state_window?startBin={idx}&endBin={idx}
3. Response includes window, bin timestamps, node values

**Artifact Persistence:**
1. Engine `/v1/run` execution writes canonical bundles (`model/model.yaml`, `metadata.json`, `provenance.json`) alongside series artifacts so `/state` consumers can rely on SIM-style storage.
2. Metadata builder normalises hashes/template identifiers and records canonical storage paths for schema validation.
3. Wildcard (`*`, `prefix/*`) output expansion ensures specs don’t need exhaustive series lists before time-travel APIs are called.

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

**AC5: Canonical Run Artifacts**
```
runs/<runId>/
  model/
    model.yaml
    metadata.json
    provenance.json
  series/
    index.json
    *.csv
```
- `/v1/run` emits the canonical structure above, including the SHA-256 `modelHash` shared across `metadata.json`, `run.json`, and `manifest.json`.
- Wildcard output expansion populates the `series/` directory without manually enumerating every node in the spec.
- Schema validation for `/state`/`/state_window` responses passes against `docs/schemas/time-travel-state.schema.json`.

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
- M-03.00 (requires window and topology in model)
- Fixtures from M-03.00 (for integration testing)

### Risks and Mitigation

| Risk | Mitigation |
|------|------------|
| API response too large (288 bins × 50 nodes) | Pagination for state_window, limit node count |
| Timestamp computation bugs | Thorough unit tests, validate against manual calculation |
| Coloring threshold debates | Make configurable in topology (sla_min per node) |

---

## M-03.02: TelemetryLoader + Templates

### Goal
Deliver a telemetry capture pipeline and FlowTime-Sim orchestration flow that replays deterministic run outputs as canonical bundles consumable by `/state` without manual edits.

### Why This Matters
- Unlocks telemetry-first runs by emitting the canonical artifact layout required by the `/state` APIs shipped in M-03.01 (`docs/releases/M-03.01.md`).
- Moves template authoring from ad-hoc YAML edits to validated, reviewable assets with provenance tracking (`time-travel-planning-decisions.md` Q2).
- Harmonises synthetic and telemetry workflows so demos and regression tests rely on the same pipeline (`time-travel-planning-decisions.md` Q5).
- Provides a bootstrap path until live telemetry (ADX/catalog loaders) is available, after which the capture tooling can remain as a regression harness while the ingestion pipeline swaps to real sources without contract changes.

### Deliverables

**Telemetry Capture Pipeline:**
1. Capture CLI (`flowtime telemetry capture`) that reads canonical run outputs and normalises telemetry bundles.
2. Optional gap injection (zero-fill, NaN) with warning capture for telemetry-mode validation.
3. Manifest builder aligning run metadata with canonical telemetry manifest structure.
4. Documentation outlining how captured bundles map to future ADX manifests.

**Template Orchestration (via FlowTime-Sim):**
1. Template schema reference and authoring guidance synced from FlowTime-Sim.
2. Scripts/CLI helpers that call Sim `/templates/{id}/generate` (or CLI) with parameter payloads referencing captured bundles.
3. Parameter mapping docs covering Sim-supported parameter primitives (scalar values and arrays) and provenance expectations.
4. Canonical bundle emission via `RunArtifactWriter` using Sim-generated `model.yaml`.
5. Guidance noting that once ADX ingestion is live, Sim remains the template authority while telemetry bundles may come directly from loaders instead of capture.

**Workflow Integration:**
1. Example templates (order-system, microservices, http-service) wired to consume captured bundles.
2. End-to-end docs + integration tests covering capture → Sim `/generate` → `/state`.
3. Golden bundles for capture output, template instantiation, and `/state` responses to guard regressions.

### Acceptance Criteria

**AC1: Telemetry Capture Execution**
```bash
flowtime telemetry capture \
  --run-dir data/runs/order-system \
  --output data/telemetry/order-system \
  --dry-run
```
*Dry-run lists planned outputs; full run writes CSV files and `manifest.json` with captured run provenance + checksum fields.*

**AC2: Dense Fill & Warning Propagation**
```json
{
  "warnings": [
    {
      "code": "data_gap",
      "nodeId": "OrderService",
      "bins": [12, 13],
      "fill": "zero"
    }
  ],
  "files": [
    { "metric": "arrivals", "path": "OrderService_arrivals.csv", "checksum": "sha256-..." }
  ]
}
```

**AC3: Template Instantiation Workflow**
```bash
curl -X POST http://localhost:8090/api/v1/templates/order-system/generate \
  -H "Content-Type: application/json" \
  -d @params/order-system.dev.json > out/order-system/model.yaml

flowtime artifacts write \
  --model out/order-system/model.yaml \
  --series-root data/telemetry/order-system \
  --out data/models/order-system/telemetry-dev
```
*Produces `model.yaml`, `metadata.json`, and `provenance.json` referencing captured manifest + template parameters.*

**AC4: End-to-End Replay**
```csharp
// Captured bundle + Sim-generated model replay through StateQueryService
var snapshot = await stateQuery.GetStateAsync(runId: "order-system-telemetry", binIndex: 42);
Assert.Equal("telemetry", snapshot.Metadata.Mode);
Assert.Contains("data_gap", snapshot.Warnings.Select(w => w.Code));
```

### Test Coverage

**Unit Tests (~30 tests total):**
- Capture parsing/validation, CSV normalisation, gap injection, manifest writing.
- Parameter binder serialisation for Sim `/templates/{id}/generate`.
- CLI argument binding and configuration overrides for the capture command.

**Integration Tests (≥8 tests):**
- Capture outputs consumed by Sim `/generate` for each example system.
- `/state` replay of captured bundles covering telemetry warnings and provenance hashes.
- Determinism checks ensuring identical checksum output for repeated captures.

**Golden / Contract Tests (≥4 tests):**
- Approved capture bundle (CSV + manifest) for order-system run.
- Approved instantiated template bundle per example.
- Schema validation for `template.schema.json` and `telemetry-manifest.schema.json`.

### Files to Create/Modify

- `src/FlowTime.Generator/TelemetryCapture.cs`
- `src/FlowTime.Generator/Capture/RunArtifactReader.cs`
- `src/FlowTime.Generator/Processing/GapInjector.cs`
- `src/FlowTime.Generator/Artifacts/CaptureManifestWriter.cs`
- `src/FlowTime.CLI/Commands/TelemetryCaptureCommand.cs`
- `scripts/time-travel/run-sim-template.sh` (Sim orchestration helper)

**Schemas & Docs:**
- `docs/schemas/template.schema.json` (sync from Sim)
- `docs/schemas/telemetry-manifest.schema.json` (NEW)
- `docs/operations/telemetry-capture-guide.md` (NEW)
- `docs/templates/README.md` (UPDATE with Sim integration workflow)

**Tests:**
- `tests/FlowTime.Generator.Tests/` (unit + integration)
- `tests/FlowTime.Api.Tests/TelemetryIntegrationTests.cs`
- `tests/FlowTime.Api.Tests/Golden/telemetry-capture/*.json`

### Dependencies
- M-03.00 (fixture topology/window conventions)
- M-03.01 (canonical writer + schema validation for `/state`)

---

## M-03.02.01: Simulation Run Orchestration

### Goal
Expose simulation-mode orchestration so `/v1/runs` can generate canonical run bundles without telemetry capture inputs. The milestone refactors the orchestration service to branch on mode while reusing FlowTime.Sim to populate run artifacts (`run.json`, `manifest.json`, `series/index.json`).

### Why This Matters
- UI-M-03.12 requires a REST surface for synthetic runs; currently operators must fall back to the CLI.
- Keeps simulation the default path for model iteration, aligning validation/provenance across API and CLI workflows.
- Provides the simulation foundation needed for M-03.03 validation and observability polish.

### Deliverables

**Orchestration Refactor:**
1. `RunOrchestrationService` handles simulation mode (no telemetry bindings) and writes canonical artifacts.
2. API response mirrors telemetry metadata envelope (warning counts, grid summary, artifact presence flags, `canReplay`).
3. Mode-aware validation: simulation failures are surfaced as errors; telemetry behaviour unchanged.

**CLI/API Alignment:**
1. CLI (`flowtime run --mode simulation`) reuses the shared orchestration pipeline.
2. Integration tests covering simulation API responses and artifact emission.
3. Structured logs/metrics for simulation runs.

**Documentation & Samples:**
1. `.http` walkthrough demonstrating simulation run creation.
2. Operator guidance contrasting simulation vs telemetry paths.
3. Milestone spec + tracking (M-03.02.01) with acceptance criteria and test plan.

### Acceptance Criteria
- `POST /v1/runs` with `mode=simulation` succeeds without telemetry inputs and produces canonical run artifacts.
- Response includes warning count, grid summary, and artifact presence flags enabling `/state` replay.
- CLI simulation runs mirror API behaviour (deterministic ids, warnings, artifact layout).
- Structured logs/metrics emitted for simulation orchestration.

### Dependencies
- M-03.00, M-03.01, M-03.02

---

### Risks and Mitigation

| Risk | Mitigation |
|------|------------|
| Captured telemetry bundles diverge from eventual ADX output shape | Keep capture schema aligned with planned ADX manifests, document mapping assumptions, and plan validation once ADX access exists. |
| Sim `/generate` contract or schema shifts | Track Sim template schema version, add smoke tests against Sim service, and coordinate releases. |
| Capture bundles drift from canonical layout | Reuse canonical writer utilities, validate against schemas, add `/state` integration tests. |
| Warning volume overwhelms users | Categorise warnings with severity + codes, document remediation guidance. |
| Orchestration scripts become brittle | Ship sensible defaults, dry-run mode, and provide troubleshooting docs for capture + Sim integration. |

---

## UI Contract: Time-Travel Visualization APIs

The M-03.x milestones provide the backend surface required for the time-travel UI described in `time-travel-architecture-ch4-data-flows.md`.

### Required UI Views
- **Flow Graph:** Renders the topology using node colors supplied by `/state`.
- **Time Scrubber & Sparklines:** Drive bin selection with `/state` (single-bin) and `/state_window` (range) responses.
- **Node Details:** Display arrivals, served, queue depth, latency, utilization, and errors for the focused bin.
- **Health Banner:** Surface validation warnings (telemetry mode) and errors (simulation mode) inline with the playback experience.

### Backend Support Delivered in M-03.x
- `POST /v1/graph` (existing) parses model metadata and returns topology + semantics aligned to the KISS schema; the UI can reuse this response for graph layout previews.
- `GET /v1/runs/{runId}/state` provides:
  - Window metadata (`start`, `timezone`, `binMinutes`)
  - Per-node semantics (arrivals, served, queue, capacity) plus derived metrics
  - Node coloring and `colorReason` matched to the thresholds defined in M-03.01
- `GET /v1/runs/{runId}/state_window?startBin={s}&endBin={e}` supplies dense series for sparklines and aggregate charts.
- Validation warnings/errors are returned with every response so the UI can annotate issues without polling other services (see M-03.03 deliverables).
- Live ADX ingestion is deferred; telemetry capture + bundling tooling from M-03.02 remain the canonical path until a future milestone prioritises direct adapters.
- Dedicated run-orchestration APIs (`POST /v1/runs`, listings) land across two milestones:
  - M-03.02.01 adds simulation-mode orchestration so synthetic runs can be generated without telemetry inputs.
  - M-03.04 adds the telemetry-mode path and run listings. Until both ship, UI teams rely on CLI workflows or pre-generated fixtures.
  - TT-M-03.17 adds an explicit telemetry generation endpoint (`POST /v1/telemetry/captures`) so operators can create bundles from simulation runs; `/v1/runs` does not auto-generate.

### Integration Checklist
- Respect backend-provided timestamps; compute client timelines as `start + binIndex × binMinutes`.
- Gray nodes indicate missing capacity—display throughput ratio or helper messaging rather than hiding the node.
- Telemetry warnings should present as non-blocking notices; simulation errors must halt playback per mode-based validation rules.
- Initial visuals can use built-in chart components or standard chart libraries; full topology graph layout will be handled by a future milestone (TBD) using an external layout engine/post-processing step rather than bespoke UI code.
- Aggregated metrics endpoints (e.g., `/metrics`) and advanced overlays remain future work (see Post-M-3 section).

---

## M-03.03: Validation + Polish

### Goal
Add post-evaluation validation, observability, and production-ready polish.

### Why This Matters
- Establishes trust signals—warnings vs. errors—so operators know whether telemetry irregularities block investigations (`time-travel-planning-decisions.md` Q3).
- Captures structured metrics and logs that the UI and operations dashboards rely on to explain run health (`time-travel-architecture-ch4-data-flows.md`).
- Documents the full workflow so onboarding engineers can reproduce time-travel flows without tribal knowledge (`time-travel-architecture-ch6-decision-log.md`).

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
- M-03.00, M-03.01, M-03.02 (integrates all)

---

## Post-M-3 (Future Work)

### M-03.04: Capacity Inference + Overlay Scenarios (P1)

**Deliverables:**
1. Capacity inference in API layer (saturation method)
2. Confidence labeling (high/medium/low)
3. Overlay scenario support (Gold baseline + modeled changes)
4. Template overlays (modify semantics at instantiation)

**Not Blocking:** Can ship M-03.00-M-03.03 without this.

### M-04.00: Catalog_Nodes Integration (P2)

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

### M-03.00–M-03.03 (incl. M-03.02.01) Complete When:
- ✅ All 60+ tests passing
- ✅ 3 fixture systems working (order, microservices, HTTP)
- ✅ /state and /state_window returning correct data
- ✅ Synthetic gold generator produces valid CSV
- ✅ Templates instantiate correctly
- ✅ Validation framework operational
- ✅ Documentation complete
- ✅ Performance benchmarks met

### Demo Scenario (End of M-03.03)
1. **Generate synthetic gold telemetry**
   ```bash
   dotnet run --project tools/SyntheticGold -- \
     --simulation examples/order-system.yaml \
     --output fixtures/gold-telemetry/ \
     --bins 288
   ```
   - Produces `fixtures/gold-telemetry/*.csv` plus `manifest.json` (M-03.02 deliverable).
2. **Instantiate the telemetry template**
   ```bash
   dotnet run --project FlowTime.Cli template instantiate \
     --template templates/telemetry/order-system.yaml \
     --param telemetry_dir=fixtures/gold-telemetry \
     --param window_start=2025-10-07T00:00:00Z
   ```
   - Yields `runs/order-system/model.yaml` with topology + provenance (M-03.02).
3. **Execute the run**
   ```bash
   curl -X POST https://localhost:5001/v1/runs \
     -H "Content-Type: application/json" \
     -d @runs/order-system/request.json
   ```
   - Response returns `runId`, warnings (if any), and artifact locations (M-03.03).
4. **Scrub a specific bin**
   ```bash
   curl "https://localhost:5001/v1/runs/${runId}/state?binIndex=42"
   ```
   - UI reads derived metrics + node colors to highlight hotspots (M-03.01).
5. **Show trend window**
   ```bash
   curl "https://localhost:5001/v1/runs/${runId}/state_window?startBin=0&endBin=144"
   ```
   - Supplies sparklines and SLA trends for the visualization (M-03.01).
6. **Surface health signals**
   - Any telemetry gaps appear as warnings (M-03.03 mode-based validation).
   - Structured logs and performance metrics corroborate the UI view.
7. **UI presentation**
   - Topology graph colored by utilization/latency.
   - Time scrubber and detail panel update from API responses.

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
| Backward compatibility issues | Low | Medium | Test M-02.10 models, graceful degradation |
| Security (directory traversal) | Low | High | Path validation, restrict to model directory |

---

## Implementation Sequence

```
M-03.00: Foundation + Fixtures
  ├─ Schema extensions (window, topology)
  ├─ File sources
  ├─ Initial conditions
  └─ Create 3 fixture systems

M-03.01: Time-Travel APIs (depends on M-03.00)
  ├─ /state endpoint
  ├─ /state_window endpoint
  ├─ Derived metrics
  └─ UI can integrate (parallel work)

M-03.02: TelemetryLoader + Templates (depends on M-03.00)
  ├─ CSV loader
  ├─ Template parser
  ├─ Synthetic gold generator
  └─ Example templates

M-03.03: Validation + Polish (depends on M-03.01, M-03.02)
  ├─ Validation framework
  ├─ Structured logging
  ├─ Documentation
  └─ Performance tuning
```

---

## Post-M-03.00 Follow-Ups

1. **Shared expression validation library (Milestone M-03.00.01)**
   - **Goal:** Extract the Engine expression parser/AST and semantic checks into a neutral assembly (`FlowTime.Expressions`), with matching unit tests, so Engine and FlowTime.Sim consume the same validation logic.
   - **Timing:** Execute immediately after M-03.00 deliverables land and before kicking off Engine M-03.01.
   - **Hand-off:** Once the library is published, FlowTime.Sim’s M-3 adoption work will replace its legacy expression checks with the shared package to keep both surfaces aligned.

---

## Deferred (TT‑M‑03.27 Context)

The following items are intentionally deferred from TT‑M‑03.27 — Queues First‑Class (Backlog + Latency; No Retries) to keep scope focused and deliverable. They remain on the roadmap and will be sequenced after TT‑M‑03.27 lands.

- Retries and backoff modeling (dependency attempts, retry rates, effect on throughput)
- Service time S derivation (requires processing_time_sum + served_count or in_service_count)
- Oldest age telemetry and visuals for queues
- Edge overlays (attempt/success/latency heat on edges)
- API fallback to reconstruct queue depth from arrivals/served when telemetry omits depth (requires q0 policy and provenance flag)

Reference: `docs/milestones/TT-M-03.27.md`

---

## Next Actions

1. **Review and Approve:** Validate this roadmap
2. **Create M-03.00 Spec:** Detailed implementation spec
3. **Set Up Environment:** Dev containers, test data
4. **Begin M-03.00:** Start with schema extensions

**Ready to proceed?**

---

**End of Roadmap Document**
