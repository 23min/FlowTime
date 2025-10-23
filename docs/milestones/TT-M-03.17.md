# TT-M-03.17 — Explicit Telemetry Generation

**Status:** ✅ Completed (manual verification + tests updated)  
**Dependencies:** ✅ UI-M-03.16 (Run Orchestration Page), ✅ M-03.04 (/v1/runs API), ✅ M-03.02 (Simulation orchestration)  
**Owners:** Time-Travel Platform / UI Core  
**Target Outcome:** Add a separate endpoint to generate telemetry bundles from existing simulation runs, surface minimal availability metadata, and keep `/v1/runs` free of auto-generation.

---

## Overview

Telemetry replay requires a capture bundle. TT‑M‑03.17 introduces an explicit, operator‑initiated telemetry generation step exposed via a new API. Operators trigger telemetry generation from the run detail view; the UI then enables telemetry replay once the bundle is available. No auto‑generation occurs within run creation.

### Goals
- Make telemetry generation explicit and separable from run creation.
- Provide a minimal telemetry availability summary in run detail/list responses (available, generatedAt, warningCount, optional sourceRunId).
- Keep paths internal; UI does not surface directories.
- CLI parity via a thin wrapper that calls the same endpoint.

### Non Goals
- Auto‑generation inside `/v1/runs`.
- RNG/PMF seed management (moved to TT‑M‑03.19).
- UI exposure of filesystem paths.

---

## Scope

### In Scope ✅
1. **New endpoint**: `POST /v1/telemetry/captures` (from existing simulation run)
2. **Service**: capture from canonical run, write `manifest.json` and `autocapture.json` (templateId, captureKey, sourceRunId, generatedAtUtc, parametersHash)
3. **Run metadata**: telemetry summary in detail/list responses (available, generatedAtUtc, warningCount, optional sourceRunId)
4. **UI**: run detail actions — Generate telemetry; Replay from model; Replay from telemetry (when available)
5. **Docs & tests**: capture guide, roadmap updates; endpoint + UI tests

### Out of Scope ❌
- Auto‑generation inside `/v1/runs`.
- RNG/PMF seed management.
- Telemetry bundle cleanup/retention policies.

---

## Functional Requirements

1. **FR1 — Explicit trigger**: Generation only via `POST /v1/telemetry/captures`.
2. **FR2 — Preconditions**: v1 accepts `source: { type: "run", runId }`.
3. **FR3 — Idempotency**: 409 on existing bundle unless `overwrite=true`.
4. **FR4 — Provenance summary**: Run detail/list include telemetry availability summary (no filesystem paths).
5. **FR5 — Failure handling**: Structured errors on invalid source/outputs.

### Non-Functional Requirements
- **NFR1 — Observability:** Log generation lifecycle events with template/capture identifiers.
- **NFR2 — Performance:** Avoid unnecessary work; reuse bundles via 409/overwrite.
- **NFR3 — Testability:** Endpoint + UI tests; temp directories for isolation.
- **NFR4 — Security:** Operate only under configured roots (`TelemetryRoot`, `RunsRoot`).

---

## Implementation Plan (High Level)

1. Endpoint + service to capture from an existing run.
2. Run metadata summary fields for telemetry availability.
3. UI actions in run detail (generate / replay selection).
4. Telemetry bundles default to each run's `model/telemetry/`; shared libraries become optional configuration.
5. Placeholder `gold/` directory renamed to `aggregates/` across docs/contracts.
6. Docs updated (guide + roadmap).
7. Tests for endpoint + UI provenance.

### Deliverables
- ✅ `POST /v1/telemetry/captures` + service implementation.
- ✅ Run metadata telemetry summary (available, generatedAtUtc, warningCount, sourceRunId).
- ✅ Run detail UI with generation action and replay gating.
- ✅ Telemetry generation defaults to run‑scoped `model/telemetry/`; `TelemetryRoot` used only for optional shared libraries.
- ✅ Docs/contracts updated to refer to `aggregates/` instead of `gold/` placeholders (archives may still reference `gold`).
- ✅ Updated docs (capture guide, roadmap) and manual verification notes.
- ✅ UI tests pass with availability chip and actions wired (no replay viewer yet by design).

---

## Progress

- ✅ Backend endpoint + service merged with contract/golden updates.
- ✅ Run summary now includes telemetry availability metadata (available, generatedAtUtc, warningCount, sourceRunId).
- ✅ Run detail surfaces telemetry availability with generate/replay actions.
- ✅ Telemetry capture defaults to run‑scoped storage; shared bundles optional via TelemetryRoot.
- ✅ Docs updated; manual verification completed; screenshots to be refreshed in a later doc polish PR.

---

## TDD Approach

For each workstream, add tests before implementation:

1. **Backend helper tests** — Start with failing unit tests covering capture detection, creation, reuse, and failure.
2. **API integration tests** — Add failing tests under `FlowTime.Api.Tests/RunOrchestrationTests` validating new response fields and filesystem outcomes.
3. **UI model tests** — Extend the `RunOrchestration*` test suite to expect the new statuses/messages.

Each test should fail prior to implementation, then pass once the feature code is in place. Record RED/GREEN transitions in the milestone tracker.

---

## Test Plan

### Automated
- **Unit tests:** `RunOrchestrationServiceAutoCaptureTests` (new) verifying helper logic.
- **Integration:** `/v1/runs` telemetry scenario tests (capture missing, capture reuse, capture failure).
- **UI tests:** Extend existing component/service tests to assert status messaging flows.

### Manual Verification
1. Open run detail for a simulation run → telemetry unavailable.
2. Click “Generate telemetry” → endpoint returns success; availability turns true with timestamp.
3. Click “Replay from telemetry” → run created successfully.
4. Retry generation without overwrite → 409; with overwrite → success.

---

## Risks & Mitigations
- **Long-running work:** Keep capture logs visible; don’t block `/v1/runs` requests.
- **Filesystem errors:** Validate roots early; return actionable errors.
- **Template misconfig:** If `captureKey` missing, allow explicit directory override (UI now auto-generates run-scoped capture keys).
- **Path churn:** Moving bundles under runs requires doc/test updates; track via new deliverables above.

---

## Open Questions
1. Should `/v1/telemetry/captures` support `source: { type:"simulate", templateId, parameters }` later? (Out of scope.)
2. Do we expose warning details in the summary or only counts? (Proposed: counts only.)
3. Retention policy for generated bundles? (Deferred.)
4. Should we provide a CLI helper to promote run-local bundles into a shared telemetry library? (Deferred.)

---

## References
- `docs/operations/telemetry-capture-guide.md`
- `docs/milestones/UI-M-03.16.md`
- `docs/architecture/time-travel/telemetry-generation-explicit.md`
