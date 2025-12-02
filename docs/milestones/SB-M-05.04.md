# SB-M-05.04 — Deterministic Run Orchestration

**Status:** 📝 Planned  
**Epic:** SIM/Engine Boundary Purification (`docs/architecture/sim-engine-boundary/`)  
**Depends on:** SB‑M‑05.03 (queue/DLQ implicit DSL)  
**Goal:** Make template-driven runs idempotent by hashing inputs to deterministic bundle IDs, enabling bundle reuse/overwrite prompts, and completing the SIM/engine separation.

---

## Overview

Engine runs currently regenerate bundles every time a template is executed, even when inputs (template ID, parameters, RNG seed, telemetry bindings) haven’t changed. SB‑M‑05.04 moves hashing/packaging into the SIM/orchestration tier so runs become deterministic artifacts that can be reused or overwritten intentionally. This also advances the SIM/engine boundary epic: orchestration handles template compilation/analyzers; the engine consumes canonical bundles only.

---

## Functional Requirements

### FR1 — Hashing & Provenance
- Compute a stable hash from template ID/version, parameter bag, RNG seed, and telemetry bindings. Use that hash when naming run bundles (e.g., `run_<templateId>_<hash>`).
- Persist the hash + input metadata into `provenance.json` for traceability.
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

### FR4 — UI/CLI Updates
- UI “Run Template” flow:
  - Show the deterministic run ID/hash before execution.
  - If the bundle exists, prompt “reuse existing / regenerate”.
  - Provide a “View existing run” shortcut when reuse is chosen.
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

## Tracking

- `docs/milestones/tracking/SB-M-05.04-tracking.md`
