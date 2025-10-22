# TT-M-03.18 — Replay Selection & Provenance (UI)

**Status:** 📋 Proposed  
**Dependencies:** ✅ TT-M-03.17 (Explicit Telemetry Generation), ✅ M-03.04 (/v1/runs API)  
**Owners:** Time-Travel Platform / UI Core  
**Target Outcome:** Polish the UI around choosing replay mode (model vs telemetry), display telemetry availability and generated‑at/warning count, and streamline the operator flow from Artifacts/Run detail. Seeded RNG support moves to TT‑M‑03.19.

---

## Overview

TT‑M‑03.17 provides an explicit telemetry generation endpoint. TT‑M‑03.18 focuses on the UI: make the decision to replay from the model or telemetry clear and fast, and present telemetry availability succinctly (no filesystem paths). This milestone is UI‑only; RNG/PMF seed support is deferred.

### Goals
- Simple, discoverable replay choice (model vs telemetry).
- Show telemetry availability, generated‑at timestamp, and warning count.
- “Generate telemetry” action available from run detail; enable “Replay from telemetry” only when available.
- Do not show filesystem paths in UI.

### Non Goals
- Seeded RNG/PMF handling (TT‑M‑03.19).
- Telemetry diff tooling (future).
- Surfacing directories/paths.

---

## Scope

### In Scope ✅
1. **Artifacts/Run detail UX** — Present availability, generated‑at, warning count (no paths); add buttons: Generate telemetry, Replay from model, Replay from telemetry.
2. **Gate actions** — Disable Replay from telemetry until availability is true.
3. **Copy** — Tight UI copy to make the choice obvious; hover help to define “replay via telemetry”.
4. **Docs** — Update UI help text and operator guide screenshots.

### Out of Scope ❌
- Seeds, versioning, archival, and user‑provided seeds.

---

## Functional Requirements

1. **FR1 — Replay selection** — Operator can choose model vs telemetry from run detail.
2. **FR2 — Availability display** — Show availability, generated‑at, warning count; no paths.
3. **FR3 — Action gating** — Telemetry replay disabled until available.
4. **FR4 — Failure handling** — Clear messages when generation fails or telemetry missing.

### Non-Functional Requirements
- **NFR1 — Observability:** Log operator actions (generate, replay selection) with run id.
- **NFR2 — Accessibility:** Keyboard navigable buttons, concise labels.
- **NFR3 — Testability:** UI tests to assert gating and copy.

---

## Implementation Plan (High Level)

1. Run detail UI — inject new panel for telemetry availability + actions.
2. Call generation endpoint; refresh availability on success.
3. Gate buttons and copy; add minimal toasts for success/error.
4. Update docs and screenshots.
5. UI tests for gating and copy.

### Deliverables
- Run detail UI for replay selection and telemetry availability.
- Integration with generation endpoint.
- Updated documentation and automated UI tests.

---

## TDD Approach

1. **UI tests (RED)** — Assert gating and copy.
2. Implement UI and wire to endpoint (GREEN).
3. Refactor and keep tests green; update docs.

---

## Test Plan

### Automated
- UI tests: gating + copy; integration test to validate availability refresh after endpoint call.

### Manual
1. Open run detail for a simulation run with no telemetry → replay from telemetry disabled.
2. Generate telemetry → availability updated; warning count shown.
3. Replay from telemetry succeeds; model replay still available.

---

## Risks & Mitigations
- **UI confusion:** Keep copy concise, avoid leaking filesystem details.
- **State drift:** Refresh availability after generation; show clear errors on failure.

---

## Open Questions
1. Where to store `generatedAtUtc` for summary: reuse `autocapture.json` and mirror in run metadata.
2. Do we need rollbacks for overwritten telemetry? (Out of scope.)

---

## References
- docs/milestones/TT-M-03.17.md
- docs/operations/telemetry-capture-guide.md
