# UI-M-03.15 — Gold Data Access Service (REST)

**Status:** ✅ Complete  
**Dependencies:** ✅ M-03.01 (Time-Travel APIs), ✅ UI-M-03.10 (UI Baseline & Build Health), ✅ UI-M-03.11 (Artifacts Page Restoration), ✅ UI-M-03.12 (Simulate → Gold Run Integration), ✅ UI-M-03.14 (Time-Travel Nav & Route Skeleton)  
**Target:** Deliver a reusable UI service that calls the Time-Travel REST endpoints (`/v1/runs/{runId}/state`, `/state_window`, `/index`, `/series/{seriesId}`) and shapes the responses for upcoming dashboard, topology, and scrubber milestones—no direct filesystem access from the UI.

---

## Overview

M-03.01 shipped REST APIs that expose gold bundle data to clients. UI-M-03.15 wires the FlowTime UI into those endpoints so future milestones can build visuals on top of real data without touching disk. We will extend the existing `FlowTimeApiClient` (or introduce a focused companion) to fetch state snapshots, window slices, and series metadata, and we will wrap those calls in a Time-Travel data service that downstream components can consume. The contract mirrors `docs/schemas/time-travel-state.schema.json`, ensuring a smooth hand-off when the backend evolves.

### Strategic Context
- Motivation: Keep the UI sandboxed from the file system and validate the REST pipeline before investing in visualization work.
- Impact: Centralizes Time-Travel data access, error handling, and range translation so dashboard/topology milestones can focus on UX.
- Dependencies: Artifacts and Simulate flows already surface `runId` context, and the Run orchestration APIs can produce new gold bundles for testing.

---

## Scope

### In Scope ✅
1. Define UI-facing contracts (interfaces + DTOs) for state snapshot/window responses, series index metadata, and streamed series data.
2. Extend the FlowTime API client layer with strongly typed methods for `/state`, `/state_window`, `/index`, and `/series/{seriesId}`.
3. Implement a `TimeTravelDataService` (working name) that wraps the REST client, normalizes errors, and exposes helpers for requesting specific bin ranges.
4. Document how Time-Travel pages should call the service (expected query parameters, handling of warnings, etc.).

### Out of Scope ❌
- ❌ Rendering dashboard/topology visuals (UI-M-03.20+).
- ❌ Direct filesystem reads from the UI.
- ❌ New backend endpoints (if additional data such as SLA metrics requires API work, coordinate with the backend roadmap first).

### Future Work
- UI-M-03.16 will invoke this service while adding orchestration playback controls.
- UI-M-03.20 and later milestones will bind the service to visual components.
- Backend follow-ups may add dedicated metrics/graph endpoints; the service should be ready to adopt them.

---

## Requirements

### Functional Requirements

#### FR1: Client Contracts
- Description: Introduce an interface (e.g., `ITimeTravelDataClient`) plus DTOs that map directly to the REST responses.
- Acceptance Criteria:
  - [x] Methods: `GetStateAsync(runId, binIndex)`, `GetStateWindowAsync(runId, range)`, `GetSeriesIndexAsync(runId)`, `GetSeriesAsync(runId, seriesId)` (stream or parsed values).
  - [x] DTOs expose node identifiers, metrics, derived metrics, warnings, and telemetry metadata exactly as defined in `docs/schemas/time-travel-state.schema.json`.
  - [x] Contracts live under `FlowTime.UI.Services` (or a dedicated namespace) and are registered for DI.

#### FR2: REST Integration
- Description: Extend `FlowTimeApiClient` (or add a dedicated client) to call the Time-Travel endpoints.
- Acceptance Criteria:
  - [x] `/v1/runs/{runId}/state?binIndex=` returns a `StateSnapshotResponse` model.
  - [x] `/v1/runs/{runId}/state_window?startBin=&endBin=` returns a `StateWindowResponse` model.
  - [x] `/v1/runs/{runId}/index` returns the existing `SeriesIndex` contract; `/series/{seriesId}` streams CSV data (ensure `HttpCompletionOption.ResponseHeadersRead` and proper disposal).
  - [x] Non-success HTTP statuses surface meaningful error messages (error code, status, and text) to the caller.

#### FR3: Range & Error Handling Helpers
- Description: Provide utilities so UI layers can request valid ranges and react to API warnings.
- Acceptance Criteria:
  - [x] Service validates bin ranges (start ≤ end, within total bins) before making REST calls.
  - [x] When the backend returns warnings (e.g., mode validation), the service surfaces them without losing context.
  - [x] Timestamps are preserved as `DateTimeOffset` for downstream scrubber support.

### Non-Functional Requirements
- **NFR1: Schema Alignment** — DTOs match the schema/documented contracts so backend changes are easy to track.
- **NFR2: Observability** — Log REST failures with run id, endpoint, status code, and correlation hints to simplify troubleshooting.
- **NFR3: Responsiveness** — All calls are asynchronous; streaming endpoints must avoid buffering large payloads on the UI thread.

---

## Implementation Plan

### Phase 1: Contracts & Wiring
- Goal: Establish the service interface, DTOs, and DI registration.
- Tasks:
  1. Mirror `StateSnapshotResponse` / `StateWindowResponse` models in the UI project.
  2. Define request helpers (bin range struct, optional timestamp conversion).
  3. Register the new client/service in the UI dependency container.
- Success Criteria:
  - [x] Contracts compile and are discoverable by Razor pages/components.

### Phase 2: REST Client Implementation
- Goal: Implement the HTTP calls and error handling.
- Tasks:
  1. Add methods to `FlowTimeApiClient` for `/state`, `/state_window`, `/index`, `/series`.
  2. Deserialize JSON responses with `System.Text.Json` options consistent with existing clients.
  3. Normalize error payloads (status + error code/message) into a single `ApiCallResult` path.
- Success Criteria:
  - [x] Unit tests cover success and error scenarios (mocked `HttpMessageHandler`).

### Phase 3: Service & Validation
- Goal: Wrap the client in a Time-Travel data service and validate manually.
- Tasks:
  1. Implement `TimeTravelDataService` that orchestrates range validation and aggregates warnings.
  2. Add a small internal-only diagnostic hook (e.g., debug button) to ensure the service works end-to-end.
  3. Update the tracking doc with findings and sample payloads.
- Success Criteria:
  - [x] Manual smoke test fetches a run’s window and index through the service without touching disk.

---

## Test Plan

- **Unit Tests**
  - Validate deserialization of `StateSnapshotResponse` / `StateWindowResponse` JSON.
  - Ensure error handling propagates 404/422/500 responses with clear messages.
  - Verify range validation rejects invalid inputs before calling REST.
- **Manual / Integration**
  - Use a sample run (via Simulate or CLI) and confirm `/state_window` results flow into the service.
  - Download a series CSV and confirm the service returns the expected stream or parsed values.

---

## References

- `docs/architecture/time-travel/ui-m3-roadmap.md`
- `docs/schemas/time-travel-state.schema.json`
- `src/FlowTime.API/Program.cs` (`/v1/runs/{runId}/state`, `/state_window`, `/index`, `/series`)
- `src/FlowTime.API/Services/StateQueryService.cs`
- `docs/operations/telemetry-capture-guide.md`

---

## Completion Notes

- REST DTOs, client extensions, and `TimeTravelDataService` implemented under `src/FlowTime.UI/Services/`.  
- Added DTO aliasing so generated models with `grid.start` surface timestamps consistently (`src/FlowTime.Contracts/Dtos/ModelDtos.cs`).  
- Updated simulation model builder to propagate window start into grid metadata (`src/FlowTime.Sim.Core/Templates/SimModelBuilder.cs`).  
- Automated coverage via `FlowTime.UI.Tests` and `FlowTime.Sim.Tests`. Manual verification performed with `src/FlowTime.API/FlowTime.API.http` (simulation run 3b → `/state`, `/state_window`).  
- Commands executed: `dotnet build`, `dotnet test tests/FlowTime.UI.Tests/FlowTime.UI.Tests.csproj --no-build`, `dotnet test tests/FlowTime.Sim.Tests/FlowTime.Sim.Tests.csproj --no-build`.
