# UI-M-03.16 Implementation Tracking

**Milestone:** UI-M-03.16 — Run Orchestration Page (Skeleton)  
**Status:** 📋 Planned  
**Branch:** `feature/time-travel-ui-m3`  
**Assignee:** [TBD]

---

## Progress Log

> Add dated entries as work begins (design notes, PRs, manual validation).

### 2025-10-20 — Session Start
- **TDD Plan (pre-implementation)**
  - Tests to introduce (RED first):
    1. `RunOrchestrationRequestBuilderTests` validates simulation vs telemetry serialization into `RunCreateRequestDto` and rejects invalid telemetry capture input.
    2. `RunOrchestrationStateMachineTests` enforces Idle → Planning/Running → Success/Failure transitions and prevents duplicate submissions while active.
    3. `RunSubmissionSnapshotStorageTests` covers persisting/reloading the pending submission payload (template, mode, submittedAt) via the browser storage helper.
  - Implementation tasks (GREEN after tests fail):
    1. Build orchestration page form (template picker, mode toggle, parameter/binding editors) backed by request builder.
    2. Wire API invocation, local-storage snapshot, and result rendering (plan vs run metadata) with notification/error handling.
    3. Trigger `IRunDiscoveryService` refresh, surface CTAs, and finalize documentation/http samples.
- Notes: Manual verification checklist from milestone doc remains pending.

### 2025-10-20 — RED cycle
- Added orchestration unit tests (request builder, state machine, snapshot storage); current run: `dotnet test` fails as expected with NotImplemented placeholders.

### 2025-10-20 — GREEN cycle
- Implemented request builder/state machine/storage helpers plus orchestration UI; `dotnet test` now passes (75 tests) and page scaffolding compiles.

### 2025-10-20 — Enhancement cycle
- **TDD delta**
  - Tests to add: `RunSuccessSnapshotStorageTests` to capture persistence of the last successful run metadata.
  - Implementation: persist and surface the most recent completed run (idle view), add telemetry capture guidance/link.
- **API support**
  - Added `TelemetryRoot` resolution so `/v1/runs` accepts capture keys; updated integration tests + docs.

### 2025-10-21 — Completion notes
- UI orchestration page verified for simulation runs; telemetry flow ready pending provisioned capture bundles.
- Manual telemetry replay remains blocked by future automation of capture generation (out of scope for UI-M-03.16).

---

## Current Status

### Overall Progress
- [x] Form/UI scaffolding implemented
- [x] API integration + status handling completed
- [x] Completion summary & Artifacts refresh verified

### Test Status
- Unit/component tests: ✅ `RunOrchestration*` suite passing
- Manual orchestration runs: simulation verified; telemetry pending capture bundle availability

---

## Risks & Notes
- Template catalog responses may grow; consider pagination/filters if the list becomes unwieldy.
- Telemetry bindings UX is minimal—flag usability issues for future polish milestones.
- Ensure error handling covers both dry-run and live-run paths to avoid confusing operators.

---

## Next Steps
1. Implement the orchestration form and validation.
2. Wire up `CreateRunAsync` (dry-run + live) with status streaming.
3. Refresh Artifacts and verify navigation/summary actions.

---

## References
- docs/milestones/UI-M-03.16.md
- docs/operations/telemetry-capture-guide.md
- docs/milestones/UI-M-03.12.md
