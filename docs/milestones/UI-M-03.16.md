# UI-M-03.16 — Run Orchestration Page (Skeleton)

**Status:** 📋 Planned  
**Dependencies:** ✅ M-03.04 (Run Orchestration APIs), ✅ M-03.02.01 (Simulation Run Orchestration), ✅ UI-M-03.11 (Artifacts Page Restoration), ✅ UI-M-03.12 (Simulate → Gold Run Integration), ✅ UI-M-03.14 (Time-Travel Nav & Route Skeleton), ✅ UI-M-03.15 (Gold Data Access Service)  
**Target:** Replace the placeholder Time-Travel “Run Orchestration” page with a functional skeleton that can kick off simulation or telemetry runs through `/v1/runs`, surface progress/logs, and hand off the resulting `runId` to Artifacts/Time-Travel pages—without polishing the final UX.

---

## Overview

Operators currently jump between CLI commands and the Simulate tab to orchestrate gold bundles. M-03.04 exposed a unified `/v1/runs` API, and UI-M-03.12 wired Simulate to produce canonical runs. This milestone brings those orchestration controls into the Time-Travel workspace so the entire capture→bundle→open loop is visible from the nav group introduced in UI-M-03.14. The page focuses on the core flow (select template, choose mode, provide telemetry bindings or simulation parameters, monitor status, and reveal the resulting `runId`) while deferring advanced UX polish to later milestones.

### Strategic Context
- Motivation: Provide a dedicated orchestration surface under Time-Travel so operators don’t rely on Simulate placeholders or CLI scripts for telemetry replays.
- Impact: Streamlines the feedback loop—new runs appear in Artifacts immediately and can be opened in dashboard/topology views once those milestones land.
- Dependencies: Requires the REST client/service added in UI-M-03.15 and the orchestration endpoints from M-03.04.

---

## Scope

### In Scope ✅
1. Replace `Pages/TimeTravel/RunOrchestration.razor` placeholder with a form-driven experience that invokes `/v1/runs`.
2. Support both simulation and telemetry orchestration (template selector, mode toggle, minimal parameter fields, telemetry capture directory + bindings).
3. Stream orchestration status/log messages and surface final outcome (success, warnings, failure) with CTAs to open the run.
4. Refresh Artifacts (via existing service) after a successful run so the new bundle is immediately discoverable.
5. Document usage in the `.http` samples and milestone tracking log.

### Out of Scope ❌
- ❌ Full UX polish (stepper layouts, fancy progress visuals, validation tooltips, etc.).
- ❌ Advanced parameter editors (keep to simple inputs/JSON blobs; no dynamic form builder).
- ❌ Telemetry capture automation beyond invoking the existing API (no file upload, no CLI integration).
- ❌ Role-based security or multi-user coordination controls.

### Future Work
- UI-M-03.20/03.21 will wire the new `runId` directly into dashboard/topology flows.
- A follow-on polish milestone can revisit orchestration UX (preset scenarios, saved configurations, telemetry manifest inspection).

---

## Requirements

### Functional Requirements

#### FR1: Template & Mode Selection
- Description: Allow users to choose a template and orchestration mode (simulation vs telemetry) before execution.
- Acceptance Criteria:
  - [ ] Template dropdown/loaders sourced from existing catalog service (`IFlowTimeSimService` / template APIs).
  - [ ] Mode toggle defaults to simulation; telemetry mode reveals capture directory + bindings inputs.
  - [ ] Basic validation ensures required fields are populated (`templateId`, capture directory for telemetry, etc.).

#### FR2: Parameter & Binding Inputs
- Description: Provide minimal input controls for parameters/bindings and show how they map to `/v1/runs`.
- Acceptance Criteria:
  - [ ] Simulation mode exposes JSON or key/value textarea for parameters (string stored as raw JSON to pass through).
  - [ ] Telemetry mode exposes capture directory field and bindings (key=value list) with inline help.
  - [ ] Inputs serialize into `RunCreateRequestDto` appropriately (parameters -> dictionary, telemetry.bindings -> dictionary).

#### FR3: Orchestration Execution & Status Streaming
- Description: Invoke `/v1/runs` via `FlowTimeApiClient.CreateRunAsync`, surface logs/status, and handle dry-run/live modes.
- Acceptance Criteria:
  - [ ] “Plan” option triggers dry-run (options.dryRun=true) and displays the returned plan (files, warnings) without writing bundles.
  - [ ] Full run streams status/log entries (reuse `TimeTravelDataService` pattern for result handling) and blocks duplicate submissions.
  - [ ] API failures (4xx/5xx) render friendly messages with the returned `error`/`code`.

#### FR4: Completion Summary & Navigation
- Description: After success, show a summary card with `runId`, warnings count, and CTAs.
- Acceptance Criteria:
  - [ ] Summary includes run metadata (mode, template, created UTC) using the API response.
  - [ ] Provide buttons: “View in Artifacts” (navigates to `/time-travel/artifacts?runId=`), “Open Dashboard” (navigates to `/time-travel/dashboard?runId=`).
  - [ ] Trigger Artifacts refresh (reuse `IRunDiscoveryService`) to ensure new run appears without reload.

#### FR5: Telemetry Warnings & Diagnostics
- Description: Surface telemetry resolution state and manifest warnings.
- Acceptance Criteria:
  - [ ] Display `telemetrySourcesResolved` boolean and list any warnings returned in the response.
  - [ ] Dry-run plan shows planned bindings/files to help debug missing telemetry.

### Non-Functional Requirements
- **NFR1: Async UI** — All API calls must be asynchronous and avoid blocking the UI thread; show loading states during orchestration.
- **NFR2: Observability** — Log orchestration attempts/failures via the existing `NotificationService`/logger to ease troubleshooting.
- **NFR3: Reusability** — Structure components/services so later milestones can reuse pieces (e.g., extract binding editor into a separate component if needed).

---

## Implementation Plan

### Phase 1: Form & Inputs
- Goal: Build the basic form with template selection, mode toggle, parameter/binding fields.
- Tasks:
  1. Fetch template list via existing services.
  2. Introduce simple input models (DTOs for parameters/bindings).
  3. Add validation/error state handling (disable submit until valid).
- Success Criteria:
  - [ ] Form renders with default template selected; required fields validated client-side.

### Phase 2: API Integration & Status
- Goal: Wire up `CreateRunAsync` calls and display status/logs.
- Tasks:
  1. Add orchestration state model (pending/running/succeeded/failed/dry-run).
  2. Implement dry-run vs live run flows.
  3. Persist an in-flight operation snapshot (template, mode, submission time) in local storage/session so returning users see status until completion.
  4. Surface API errors/warnings and disable repeated submissions while running.
- Success Criteria:
  - [ ] Successful run yields metadata summary; dry-run prints plan.
  - [ ] Reloading the page mid-run rehydrates pending status from storage.

### Phase 3: Completion Wiring & Refresh
- Goal: Provide navigation CTAs and refresh Artifacts list.
- Tasks:
  1. Hook summary actions to navigation manager.
  2. Invoke `IRunDiscoveryService` refresh on success.
  3. Update documentation/tracking with validation notes and `.http` samples.
- Success Criteria:
  - [ ] New run appears in Artifacts after orchestration; CTA navigation works.

---

## Test Plan

- **Unit / Component Tests**
  - Validate form serialization (parameters, telemetry bindings) into `RunCreateRequestDto`.
  - Mock `IFlowTimeApiClient` to confirm dry-run vs live-run flows surface correct status messages.
  - Ensure validation prevents submission with missing required fields.
- **Manual**
  - Orchestrate a simulation run via the new page; confirm run appears under Artifacts and `/state` works.
  - Navigate away mid-run and return—ensure the stored status is surfaced until the request finishes.
  - Perform a telemetry dry-run using the sample capture directory to verify plan output.
  - Trigger an error (e.g., invalid template) and confirm error messaging/logging.

---

## References

- `docs/operations/telemetry-capture-guide.md`
- `docs/milestones/UI-M-03.12.md`
- `docs/milestones/UI-M-03.15.md`
- `src/FlowTime.API/Endpoints/RunOrchestrationEndpoints.cs`
- `src/FlowTime.UI/Services/FlowTimeApiClient.cs`
