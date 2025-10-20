# UI-M-03.12 Implementation Tracking

**Milestone:** UI-M-03.12 — Simulate → Gold Run Integration  
**Started:** 2025-10-17  
**Status:** 🚧 In Progress  
**Branch:** `feature/time-travel-ui-m3`  
**Assignee:** [TBD]

---

## Progress Log

### 2025-10-17 — Tracking Setup
- Reviewed milestone spec, contracts reference, and telemetry capture guide.
- Drafted UI task breakdown for adapter, simulate page, and diagnostics.
- Next: begin adapter request/response modelling (Phase 1).

### 2025-10-17 — Simulate Orchestration UI (stashed work)
- Added orchestration API client targeting `/v1/runs`.
- Rebuilt `/simulate` page with start form, log viewer, and completion summary (validation staged).
- Wired navigation actions for Time-Travel and Artifacts; logs currently synthetic.
- Validation: `dotnet build src/FlowTime.UI/FlowTime.UI.csproj` (tests deferred to later phases).

---

## Current Status

### Overall Progress
- [x] Phase 1: Orchestrator Adapter (3/3 tasks) — tests pending
- [x] Phase 2: Simulate UI Integration (3/3 tasks) — tests pending
- [ ] Phase 3: Diagnostics & Refresh (0/2 tasks)

### Test Status
- Unit Tests: Not yet implemented (UI wiring only)
- Integration Tests: Pending backend availability
- Manual / E2E: TODO once backend simulation mode accessible

---

## Phase 1: Orchestrator Adapter

Goal: File-backed adapter to execute local pipeline and stream logs to UI.

Tasks completed:
- Request/Result models plus default handling (tests TODO).
- Adapter execution with synthetic log streaming + cancel hook (tests TODO).
- Output validation utility parsing `run.json` and manifest presence (tests TODO).

Next: add unit tests (validation, log streaming, output checks).

---

## Phase 2: Simulate UI Integration

Goal: Start form → logs → success card with actions.

Tasks completed:
- Start form validation and disabled states.
- Log viewer + status indicators (synthetic events).
- Completion summary with CTA buttons.

Next: hook into real backend logs once available and cover with UI tests.

---

## Phase 3: Diagnostics & Refresh

Goal: Resilient error paths and post-run discoverability.

Planned tasks:
- Error/partial handling with last log lines and diagnostics.
- Artifacts page refresh integration post-run.

Status: not started (awaiting backend merge + telemetry).

---

## Dependencies & Notes
- Blocked on backend milestone **M-03.02.01** until merged/deployed; staging ready.
- Once backend lands, re-test simulate page against live `/v1/runs` simulation mode and adjust logs.
- CLI/docs updates from backend branch now available via `time-travel-m3` merge.

---

## Next Actions
1. Verify backend deployment; re-run simulate flow end-to-end.
2. Add unit tests for adapter and UI state handling.
3. Implement Phase 3 diagnostics/refresh tasks.

## References
- `docs/milestones/UI-M-03.12.md`
- `docs/operations/telemetry-capture-guide.md`
- `src/FlowTime.UI/Pages/Simulate.razor`
- `src/FlowTime.UI/Services/FlowTimeApiClient.cs`
