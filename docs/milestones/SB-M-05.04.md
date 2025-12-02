# SB-M-05.04 — Deterministic Run Orchestration

**Status:** 📝 Planned  
**Epic:** SIM/Engine Boundary Purification (`docs/architecture/sim-engine-boundary/`)  
**Depends on:** SB‑M‑05.03 (queue/DLQ DSL parity)  
**Goal:** Make template-driven run generation deterministic and idempotent so SIM/orchestration can reuse existing bundles (or prompt before overwriting) while producing engine-ready artifacts.

---

## Overview

Today, FlowTime.UI/CLI call the engine API with a template ID + parameters and the engine instantiates `RunOrchestrationService`, which regenerates models/bundles every time—even if the inputs are identical. This bloats the `data/runs` directory, complicates comparisons, and keeps the SIM/Engine boundary blurry. SB‑M‑05.04 implements deterministic orchestration under the SIM umbrella:

1. **Deterministic bundle hashing:** The orchestration tier computes a stable hash based on template ID, version, parameters, RNG seed, and telemetry bindings. Generated bundles are named using this hash (e.g., `run_<templateId>_<hash>`).
2. **Idempotent runs:** Before generating artifacts, orchestration checks if the bundle already exists. If so, CLI/UI callers are prompted to reuse it or overwrite. Overwrites regenerate artifacts deterministically (no hidden drift).
3. **Engine purity:** Engine APIs accept only canonical bundles/run manifests produced by the orchestration tier. No template compilation happens inside the engine host.

---

## Functional Requirements

### FR1 — Hashing & Bundle Identity
- Define a hashing scheme for template inputs (`templateId`, `metadata.version`, parameter bag, RNG seed, telemetry bindings, and class definitions). Use a stable algorithm (e.g., SHA256) and encode it into the run ID.
- Persist hash metadata in `provenance.json` so downstream consumers know which inputs produced the bundle.
- Expose the computed hash/run ID in orchestration responses so UI/CLI can display “Run `run_<hash>` (existing/new).”

### FR2 — Orchestration Service Behavior
- Extend the SIM orchestration endpoint (`POST /orchestration/runs`) to:
  - Compute the deterministic hash.
  - Check the bundle store (local `data/runs/` or configured artifact root) for an existing run with the same hash.
  - If the bundle exists:
    - Optionally short-circuit and return the existing manifest.
    - Or, when the caller requests overwrite, regenerate artifacts in-place (ensuring atomic replacement).
  - If the bundle is missing, generate it (template expansion, analyzers, telemetry bundle) and place it under the hashed run directory.
- Provide a `force` flag so automation/CI can overwrite without interactive prompts.

### FR3 — Engine API Simplification
- Update `FlowTime.API` run endpoints to accept only canonical bundles (path or upload). Remove any template-ID handling to enforce the new boundary.
- Adjust CLI/UI to call orchestration first, then hand the resulting bundle ID/path to the engine run submission endpoint.

### FR4 — UX/CLI Updates
- UI “Run Template” flow:
  - Display the deterministic run ID upfront.
  - If the run already exists, prompt the user: “Re-use existing bundle / Re-generate (overwrite).”
  - Provide a “View existing run” shortcut when reusing.
- CLI:
  - `flow-sim generate` gains options `--reuse` (default) and `--force-overwrite`.
  - Output explicitly states whether the run was reused or regenerated.

### FR5 — Docs & Tooling
- Update `docs/templates/template-authoring.md` and `docs/operations` to describe deterministic bundle behavior and how to clear bundles if needed.
- Document the hashing scheme and overwrite semantics in the SIM/engine boundary architecture note.

---

## Phases & Deliverables

1. **Phase 1 — Hashing & Metadata**
   - Implement hash computation + provenance wiring.
   - Add unit tests verifying identical inputs yield identical hashes while parameter changes produce different run IDs.
2. **Phase 2 — Orchestration Workflow**
   - Extend orchestration endpoints/service to reuse/overwrite bundles.
   - Wire CLI/UI to prompt for reuse vs overwrite.
   - Add integration tests covering reuse, forced overwrite, and error cases (partial bundles).
3. **Phase 3 — Engine Boundary & Docs**
   - Remove template orchestration from engine API.
   - Update docs, release notes, and tracking.
   - `dotnet build` + `dotnet test --nologo` before handoff.

---

## Test Plan

- **Unit tests:** Hash builder tests, orchestration service checks (existing vs new bundle), CLI prompt logic.
- **Integration tests:** Orchestration endpoint end-to-end (simulate UI calling twice with same inputs).
- **Regression:** Ensure router/template bundle tests remain deterministic and engine runs still execute normally.
- **Manual:** Run a representative template twice, verify the same run ID and no duplicate bundles unless overwrite requested.

---

## Tracking

- Create `docs/milestones/tracking/SB-M-05.04-tracking.md` when implementation starts. 
