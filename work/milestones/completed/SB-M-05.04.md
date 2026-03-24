# SB-M-05.04 — Deterministic Run Orchestration

**Status:** ✅ Complete  
**Epic:** SIM/Engine Boundary Purification (`work/epics/sim-engine-boundary/`)  
**Depends on:** SB‑M‑05.03 (queue/DLQ implicit DSL)  
**Goal:** Make template-driven runs idempotent by hashing inputs to deterministic bundle IDs, enabling bundle reuse/overwrite prompts, and completing the SIM/engine separation.

---

## Overview

Engine runs currently regenerate bundles every time a template is executed, even when inputs (template ID, parameters, RNG seed, telemetry bindings) haven’t changed. SB‑M‑05.04 moves hashing/packaging into the SIM/orchestration tier so runs become deterministic artifacts that can be reused or overwritten intentionally. This also advances the SIM/engine boundary epic: orchestration handles template compilation/analyzers; the engine consumes canonical bundles only.

---

## Outcome

- FlowTime-Sim now owns deterministic run orchestration end-to-end. `RunOrchestrationService`, the FlowTime.Sim Service endpoint (`/api/v1/orchestration/runs`), FlowTime.Cli (`flowtime telemetry run`), and the UI all compute a reuse hash and default to returning existing bundles unless callers opt into `overwriteExisting=true`.
- FlowTime.API `/v1/runs` accepts canonical bundles via `RunImportRequest` (path or archive) and reuses `RunOrchestrationContractMapper` metadata so UI dashboards, CLI output, and API consumers share a consistent envelope.
- Docs/tooling describe the new flow: `docs/operations/telemetry-capture-guide.md` now highlights the SIM-first orchestration, and `docs/development/ui-debug-mode.md` captures the debug commands + Chrome DevTools steps used during SB‑M‑05.04 validation.
- Release summary, remaining risks, and verification details live in `docs/releases/SB-M-05.04.md`.

---

## Functional Requirements

### FR1 — Hashing & Provenance
- Compute a stable hash from template ID/version, parameter bag, RNG seed, and telemetry bindings. Use that hash when naming run bundles (e.g., `run_<templateId>_<hash>`).
- Persist the hash + input metadata into `provenance.json`, `run.json`, and the manifest provenance stub for traceability.
- Expose the hash/run ID in orchestration responses so UI/CLI can display “existing vs new run.”

### FR2 — Orchestration Workflow
- Enhance the SIM orchestration service (`FlowTime.Sim.Service` or CLI) so `generate/run`:
  - Computes the deterministic hash.
  - Checks the bundle store (`data/runs/<hash>` or configured path) to see if the run already exists.
  - Returns existing bundle metadata by default, and allows callers to force a regeneration/overwrite.
- Provide a REST endpoint (e.g., `POST /orchestration/runs`) that handles hashing, reuse prompt, and artifact creation.

### FR3 — Engine API Simplification
- Update engine run endpoints to accept only canonical bundles (path or upload). Remove template ID/parameter handling from `FlowTime.API`.
- Ensure UI/CLI flows call orchestration first, then hand the resulting bundle ID/path to the engine submission endpoint.
- `/v1/runs` now accepts `RunImportRequest` (either `bundlePath` or `bundleArchiveBase64`) and responds with the same metadata envelope used across UI/CLI, keeping the engine API read-only with respect to templates.

### FR4 — UI/CLI Updates
- UI “Run Template” flow:
  - Default to deterministic reuse (`deterministicRunId=true`) when the form inputs (template selection, parameters, telemetry bindings, RNG seed) match a prior submission.
  - If orchestration reports `WasReused = true`, surface a banner/toast with options to open the existing run or regenerate (which sets `overwriteExisting=true` for the next submission).
  - Track form deltas locally so the UI knows when a new deterministic hash will be emitted (parameters changed, template version shifted, RNG seed updated) and hides reuse messaging in that case.
  - Provide explicit run controls (Reuse / Regenerate / Fresh run) so power users can opt out of reuse.
- CLI (`flow-sim generate` / `flow-sim run`) gains options `--reuse` (default) and `--force-overwrite`.

### FR5 — Docs & Tracking
- Update authoring/operations docs to describe deterministic bundle behavior, hashing scheme, and the orchestration vs engine boundary.
- Add release note with the new flow; update milestone tracker as work progresses.

---

## Phases & Deliverables

1. **Phase 1 — Hashing & Schema Updates**
   - RED tests for hashing/provenance metadata.
   - Implement hash builder + provenance changes.
2. **Phase 2 — Orchestration & Engine Boundary**
   - Extend orchestration service + CLI; add reuse/overwrite prompts.
   - Update engine API to accept only bundles; adjust tests/clients.
3. **Phase 3 — UI/CLI, Docs & Release**
   - Wire the UI prompt flow; expose “reuse vs overwrite” to users.
   - `dotnet build`, `dotnet test --nologo`, manual verification, release note.

---

## Test Plan

- Unit tests for hash computation and provenance stamping.
- Orchestration service integration tests (existing bundle vs new bundle).
- Engine API regression tests ensuring it accepts only bundles (no template IDs).
- UI/CLI tests verifying the prompt flow.
- Manual verification: run a template twice, confirm the same bundle ID is reused unless overwritten.

---

## Verification

- `dotnet build`
- `dotnet test --nologo` (expected skips: `FlowTime.Tests.Performance.M2PerformanceTests.*`, FlowTime.Sim example smoke tests)

---

## Tracking

- `work/milestones/tracking/SB-M-05.04-tracking.md`
