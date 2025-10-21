# TT-M-03.17 — Telemetry Auto-Capture Orchestration

**Status:** 📋 Proposed  
**Dependencies:** ✅ UI-M-03.16 (Run Orchestration Page), ✅ M-03.04 (/v1/runs API), ✅ M-03.02 (Simulation orchestration)  
**Owners:** Time-Travel Platform / UI Core  
**Target Outcome:** End-to-end "simulate → capture → replay" loop that can be initiated from the existing Run Orchestration page without requiring manual telemetry bundle creation.

---

## Overview

UI-M-03.16 delivered the Run Orchestration surface, but telemetry replays still depend on a capture bundle pre-existing under the API's `TelemetryRoot`. TT-M-03.17 fills that gap by automating the capture generation flow whenever a request targets a template that exposes `metadata.captureKey` but lacks a corresponding bundle. The milestone is intentionally narrow: extend the orchestration backend, surface the auto-capture state in the UI, and document the behaviour so operators have a predictable loop.

### Goals
- Remove the manual "seed capture bundle" prerequisite for templates that advertise a capture key.
- Keep orchestration idempotent: if a bundle already exists, reuse it without re-running simulation.
- Preserve the UI contract: operators still select template + mode + bindings; the extra work happens server-side.
- Maintain test-first discipline (unit + integration) so the new workflow is protected from regressions.

### Non Goals
- Building a general-purpose telemetry generation UI. The existing page only gains status messaging.
- Supporting stochastic templates whose models rely on RNG/PMF. Such templates remain blocked and must surface a clear warning.
- Changing template metadata format beyond the already adopted `metadata.captureKey`.

---

## Scope

### In Scope ✅
1. **Backend orchestration changes**
   - Extend `RunOrchestrationService` (and related endpoints) to detect missing capture bundles when `captureKey` is provided.
   - Automatically run a simulation (if needed), then execute telemetry capture into `TelemetryRoot/<captureKey>`.
   - Cache the capture result so subsequent telemetry runs reuse the bundle without recreating it.
2. **API contract tweaks**
   - Add an optional flag in the orchestration request (`options.autoCapture` default `true`) to control the behaviour.
   - Emit progress metadata in the response (e.g., `captureGenerated`, `captureWarnings`).
3. **UI updates**
   - Display in-progress messaging while auto-capture runs (e.g., "Generating telemetry bundle...").
   - Report success or warning states if capture creation was skipped or blocked.
4. **Documentation & operational notes**
   - Update `telemetry-capture-guide.md` and the roadmap to reflect the new automated loop.
5. **Automated tests**
   - Unit tests around orchestration state machine.
   - Integration tests hitting `/v1/runs` covering the new paths (capture exists, capture generated, capture blocked).

### Out of Scope ❌
- UI-driven configuration of capture destinations or custom parameter overrides.
- Auto-generation of captures for templates marked stochastic (RNG or PMF nodes).
- Telemetry bundle cleanup or eviction policies.

---

## Functional Requirements

1. **FR1 — Auto-capture trigger**  
   When a telemetry run is requested, the backend must determine whether the associated capture bundle exists. If not, it generates it automatically before proceeding with the telemetry replay.

2. **FR2 — Idempotency**  
   Repeated telemetry runs for the same template/capture key should not regenerate the bundle unless explicitly forced.

3. **FR3 — Status transparency**  
   API responses and UI notifications must indicate whether a bundle was generated, reused, or skipped (with reason).

4. **FR4 — Failure handling**  
   If auto-capture fails (e.g., stochastic template), return a structured error (`code`, `message`, `details`) and ensure the UI surfaces it clearly.

5. **FR5 — Opt-out**  
   Operators should be able to set `options.autoCapture = false` for advanced scenarios (e.g., manual bundle injection) without altering existing contracts.

### Non-Functional Requirements
- **NFR1 — Observability:** Log autocapture lifecycle events (start, reuse, success, failure) with template/capture identifiers.
- **NFR2 — Performance:** Auto-capture must reuse deterministic runs where possible to avoid unnecessary rework; long-running steps should stream progress to logs.
- **NFR3 — Testability:** Provide deterministic integration tests by seeding templates and verifying filesystem results under temporary directories.
- **NFR4 — Security:** Do not loosen filesystem sandboxing; only operate under configured roots (`TelemetryRoot`, `RunsRoot`).

---

## Implementation Plan (High Level)

1. **Backend orchestration enhancement**
   - Inject telemetry bundle resolver into `RunOrchestrationService`.
   - Add helper that checks for existing bundle and either reuses or creates it (simulation + capture).
   - Update API endpoint (`RunOrchestrationEndpoints`) to include `autoCapture` flag and propagate capture status in the response DTO.
2. **File-system helpers**
   - Normalize capture paths to `TelemetryRoot/<captureKey>`.
   - Write simple metadata (e.g., `bundle.json`) tracking creation timestamp and source runId for auditing.
3. **UI adjustments**
   - Extend `RunOrchestrationModels` to reflect new response fields.
   - Show progress indicator while capture is in-flight; present alerts for skip/failure states.
4. **Documentation & roadmap**
   - Update architecture docs to note the automated loop.
   - Add operational guidance (how to disable auto-capture, known limitations).
5. **Testing**
   - Unit tests for new helper functions.
   - Integration tests in `FlowTime.Api.Tests` simulating capture generation and reuse scenarios.

### Deliverables
- Updated backend service + endpoint.
- Enhanced UI run orchestration flow (progress + messaging).
- Revised docs (milestone tracker, telemetry capture guide, roadmap).
- Automated tests demonstrating auto-capture success, reuse, and failure handling.

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
1. Kick off telemetry run for a template without an existing bundle; observe auto-capture progress and ensure the run completes.
2. Repeat the same telemetry run; confirm capture is reused (no long-running regeneration).
3. Attempt telemetry run for a stochastic template; ensure UI surfaces a clear error.
4. Toggle `autoCapture = false` via dev tools and verify that the API rejects missing bundles (documented behaviour).

---

## Risks & Mitigations
- **Long-running simulations:** Mitigate via progress logging and UI messaging.
- **Filesystem errors:** Validate permissions and root paths early; fail fast with actionable error details.
- **Template misconfiguration:** If capture key is missing, fall back to the current behaviour (require manual bundle) and inform the user.

---

## Open Questions
1. Should auto-capture results be cached beyond the filesystem (e.g., database metadata)? (Default: filesystem metadata only.)
2. Do we need throttling to prevent concurrent auto-capture for the same template? (Likely yes; guard with simple lock.)
3. What retention policy, if any, should be applied to generated canonical runs used only for capture? (Documented as follow-up if needed.)

---

## References
- `docs/operations/telemetry-capture-guide.md`
- `docs/milestones/UI-M-03.16.md`
- `src/FlowTime.Generator/TelemetryCapture.cs`
- `src/FlowTime.Generator/Orchestration/RunOrchestrationService.cs`
