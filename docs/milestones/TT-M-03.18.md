# TT-M-03.18 — Telemetry Auto-Capture (Seeded RNG Support)

**Status:** 📋 Proposed  
**Dependencies:** ✅ TT-M-03.17 (Deterministic auto-capture baseline), ✅ M-03.04 (/v1/runs API), ✅ M-03.02 (Simulation orchestration)  
**Owners:** Time-Travel Platform / UI Core  
**Target Outcome:** Extend the auto-capture loop to templates that rely on RNG/PMF nodes by capturing and replaying deterministic seeds, so telemetry can be generated and reused without manual intervention.

---

## Overview

TT-M-03.17 enables automatic telemetry generation for deterministic templates. TT-M-03.18 closes the gap for stochastic models: when a template uses RNG/PMF nodes, the orchestration service must select and record a deterministic seed, write provenance alongside the generated telemetry, and guarantee that future replays reuse the exact same data. The UI should surface the seed metadata so operators know telemetry is reproducible.

### Goals
- Automatically select a deterministic RNG seed when generating telemetry for templates with PMF/RNG nodes.
- Persist seed/provenance metadata with both the canonical run and the generated telemetry bundle.
- Ensure subsequent telemetry replays use the stored seed (no drift between auto-generation and replay).
- Provide UI messaging that confirms telemetry was generated from a specific seed, and allow operators to reference it when debugging.

### Non Goals
- Seed management UI beyond basic visibility (no custom seed input in this milestone).
- Advanced telemetry versioning or historical seed management (single active bundle per capture key).
- Telemetry diff tooling or replay comparisons (still future work).

---

## Scope

### In Scope ✅
1. **Seed-aware auto-capture**
   - Extend `RunOrchestrationService` to detect RNG/PMF nodes and choose a deterministic seed (e.g. hash of templateId + timestamp).
   - Pass the seed through simulation execution and telemetry capture so outputs are reproducible.
2. **Provenance metadata**
   - Store seed information in canonical run metadata (`run.json`) and in the telemetry bundle (e.g. `bundle.json`).
   - Include capture timestamp, template version, and simulation runId for auditability.
3. **Replay enforcement**
   - Update telemetry replay path so it reuses the captured data without re-running RNG logic (ensuring deterministic outputs).
   - Introduce validation to prevent executing stochastic replay without stored seeds.
4. **UI enhancements**
   - Display “Generated telemetry with seed XYZ” (or reused) in the orchestration page summary.
   - Offer a copy-to-clipboard action for the seed to streamline debugging.
5. **Documentation**
   - Refresh telemetry capture guide with seeded workflow.
   - Document operational guidance (e.g. clearing/rebuilding seeded bundles).__

### Out of Scope ❌
- Multiple concurrent seeds per capture key (one bundle per template capture key).
- Long-term archival of older seeded bundles.
- Custom user-provided seeds (not until a future milestone).

---

## Functional Requirements

1. **FR1 — Seed detection and selection**  
   When a template with RNG/PMF nodes is auto-generated, the system must compute or retrieve a deterministic seed before executing simulation.

2. **FR2 — Provenance persistence**  
   The chosen seed must be recorded in both the run metadata and the generated telemetry bundle so future replays are reproducible.

3. **FR3 — Replay determinism**  
   Telemetry replays must rely on the stored bundle/seed; the system should not re-run RNG logic unless explicitly forced (outside scope).

4. **FR4 — UI transparency**  
   The orchestration UI must display seed information (generated, reused) and warn when an RNG template cannot be captured (e.g. missing seed metadata).

5. **FR5 — Failure handling**  
   If seed generation or provenance writing fails, return structured errors (`code`, `message`, `details`) and surface them in the UI.

### Non-Functional Requirements
- **NFR1 — Observability:** Log seed selection, reuse, and replay events with template/capture identifiers.
- **NFR2 — Security:** Ensure seeds are safe for logging (no sensitive data) and stored in readable JSON.
- **NFR3 — Testability:** Provide integration tests exercising seeded capture and replay scenarios.
- **NFR4 — Performance:** Avoid redundant capture work; reuse existing bundles when seeds match.

---

## Implementation Plan (High Level)

1. **Seed management layer**
   - Add helper to check template metadata for RNG usage and compute deterministic seed (e.g., GUID hash or configuration-based seed).
   - Provide ability to reuse existing bundle seeds if telemetry already exists.
2. **Orchestration updates**
   - Extend `RunOrchestrationService` to pass seeds into simulation and capture flows when needed.
   - Ensure canonical run metadata captures seed + provenance info.
3. **Telemetry capture adjustments**
   - Update `TelemetryCapture` to record seed in `telemetry-manifest` (or new metadata file).
   - Validate seeds on replay to prevent mismatched telemetry.
4. **UI changes**
   - Extend `RunOrchestrationModels` and Razor page to show seed status (generated/reused) and optional copy button.
   - Present warnings when RNG capture cannot proceed or when metadata is missing.
5. **Docs & roadmap**
   - Update architecture roadmap and telemetry capture guide with seeded workflow diagrams.
   - Document operations (how to clear or regenerate seeded bundles).
6. **Testing**
   - Unit tests: seed helper logic, metadata persistence checks.
   - Integration tests: RNG template auto-capture (generate + reuse) + replay confirmation.
   - UI tests: verify seed messaging/state transitions.

### Deliverables
- Seed-aware orchestration helper.
- Updated telemetry capture + metadata files.
- UI messaging for seeded bundles.
- Revised documentation and automated tests.

---

## TDD Approach

1. **Backend seed helper tests (RED)** — Cover seed generation, reuse, and failure cases.
2. **API integration tests (RED)** — Add scenarios to `RunOrchestrationTests` verifying seeded metadata appears in responses and on disk.
3. **UI tests (RED)** — Extend existing suite to expect new seed fields/messages.
4. Implement helper logic and endpoints until tests pass (GREEN).
5. Refactor and clean up while keeping tests green.
6. Log RED/GREEN phases in the milestone tracker.

---

## Test Plan

### Automated
- Unit: `RunOrchestrationSeedHelperTests`, `TelemetryBundleMetadataTests`.
- Integration: `/v1/runs` seeded telemetry scenarios (generate, reuse, failure).
- UI: `RunOrchestrationPageSeedTests` for seed messaging.

### Manual
1. Run orchestration for PMF template without bundle; confirm seed shown and telemetry generated.
2. Repeat run; ensure capture is reused and seed is identical.
3. Delete metadata/seed file intentionally; confirm error is surfaced.
4. Optional: inspect telemetry bundle `bundle.json` to confirm seed + provenance.

---

## Risks & Mitigations
- **Seed collision or poor randomness:** Use stable deterministic generation (e.g., hashed template + timestamp) or allow configuration override.
- **Metadata drift:** Validate metadata existence before replay; fail fast if missing.
- **Backward compatibility:** Ensure existing deterministic bundles (TT-M-03.17) continue to function.

---

## Open Questions
1. Should seed selection be configurable (e.g., via `options.seed`)? Deferred until demanded.
2. Do we need explicit commands to regenerate seeded telemetry? Possibly a future milestone.
3. Where should seed metadata live (`run.json`, new `bundle.json`)? Proposed: both run metadata and a new `telemetry/bundle.json` for clarity.

---

## References
- docs/milestones/TT-M-03.17.md
- docs/operations/telemetry-capture-guide.md
- src/FlowTime.Generator/TelemetryCapture.cs
- src/FlowTime.Generator/Orchestration/RunOrchestrationService.cs
