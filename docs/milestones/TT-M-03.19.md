# TT-M-03.19 — Seeded RNG & Provenance (Deterministic Runs)

**Status:** 🛠️ In Progress  
**Dependencies:** ✅ TT‑M‑03.17 (Explicit Telemetry Generation), ✅ TT‑M‑03.18 (Phase 1 Closeout)  
**Owners:** Time‑Travel Platform / UI Core  
**Target Outcome:** Make all simulation runs deterministic by default with explicit RNG seeding, surface seed in run metadata, and record replay provenance in telemetry bundles (`autocapture.json`) via `rngSeed`, `parametersHash`, and `scenarioHash`. No backward compatibility required (existing `data/` will be discarded and regenerated).

---

## Overview

To enable reproducible time‑travel and consistent demos/tests, we introduce a first‑class RNG configuration across API, UI, and generator. Simulation runs will be deterministic with a stable default seed (123), operators can override the seed, and telemetry bundles will carry sufficient provenance (seed + parameter hash) for replay auditing. Templates that declare an `rng` block must provide a seed; otherwise runs fail fast.

This milestone is metadata/contract focused; it does not change engine evaluation semantics for PMF nodes (which remain expected‑value deterministic for now).

---

## Goals

- Deterministic runs by default (no random seed generation). Default seed = `123` when not specified.
- Explicit RNG in API requests/responses: `rng: { kind: 'pcg32', seed: int }`.
- Enforce seed presence when template declares an RNG block; otherwise default is applied.
- Surface seed in run list/detail responses (no filesystem paths).
- Extend telemetry `autocapture.json` with `rngSeed`, `parametersHash` (stable JSON hash), and `scenarioHash` (existing writer hash).
- Keep the hashing process independent from the execution seed (use an internal, non‑surfaced salt only if required; never show this value).

### Non‑Goals

- Enabling stochastic sampling in the engine (PMF nodes stay expected‑value deterministic).
- Backward compatibility or data migration (we explicitly discard existing `data/`).

---

## Scope

1. API contracts (server + UI DTOs)
   - Add `rng` to run creation requests: `{ kind: 'pcg32', seed: int }`.
   - Echo seed in run responses under metadata (and optionally summaries).
   - Validate `rng.kind == 'pcg32'`; reject unsupported kinds.
2. Deterministic default and validation
   - Replace any random seed fallback with constant `123`.
   - If template contains an `rng` block and request omits `rng.seed`, return `400 Bad Request` with actionable message.
3. Generator and provenance
   - Ensure `RunArtifactWriter` writes `rng.seed` into provenance.
   - `TelemetryGenerationService` writes `rngSeed`, `parametersHash`, and `scenarioHash` into `model/telemetry/autocapture.json`.
   - `parametersHash` = SHA‑256 over normalized JSON of `{ templateId, templateVersion, parameters (sorted), executionSeed }`.
   - `scenarioHash` = existing writer hash (retain as is).
4. UI
   - Run Orchestration (simulation): add optional “RNG Seed” input (default 123).
   - Run Detail: display seed read‑only in the summary; keep directories hidden.
5. Tests & Docs
   - Update API/UI tests to cover seed plumbing and capture metadata.
   - Update capture guide and roadmap references.

---

## Functional Requirements

1. FR1 — API request: `POST /v1/runs`
   - Accepts `rng: { kind: 'pcg32', seed: int }` (optional unless template declares RNG).
   - Rejects unsupported RNG kinds with `400`.
2. FR2 — Determinism
   - If `rng` omitted and template has no RNG requirement, use seed `123`.
   - If template has `rng` and `seed` absent, `400 Bad Request` (“template declares RNG; provide rng.seed”).
3. FR3 — API response
   - Run detail and list echo seed under metadata; no filesystem paths.
4. FR4 — Telemetry provenance
   - `model/telemetry/autocapture.json` contains: `rngSeed`, `parametersHash`, `scenarioHash`.
   - Hashing does not reuse the execution seed; if a salted hash is used, the salt is internal and never surfaced.
5. FR5 — UI
   - Orchestration page includes “RNG Seed” field (numeric) with default `123`.
   - Run detail shows a small “Seed: <n>” line in the summary when present.

### Non‑Functional Requirements

- NFR1 — Observability: Log template id and seed at run creation; log capture provenance write.
- NFR2 — Testability: API/UI/generator tests validating seed propagation and metadata.
- NFR3 — Security: No path leakage; internal salts not exposed.
- NFR4 — Performance: Hashing must be fast; no significant overhead.

---

## Implementation Plan

1) Contracts & DTOs
- Server: `FlowTime.Contracts/TimeTravel/RunContracts.cs`
  - Add `RunRngOptions { string Kind = "pcg32"; int Seed; }`.
  - `RunCreateRequest`: new `Rng` property (nullable).
  - `RunCreateResponse.Metadata`: include `Rng` (seed echoed).
  - `RunSummary`: optionally include `Rng` (seed) if helpful for lists.
- UI: `src/FlowTime.UI/Services/FlowTimeApiModels.cs`
  - Mirror `RunRngOptionsDto` and wire through in request/response DTOs.

2) API plumbing
- `src/FlowTime.API/Endpoints/RunOrchestrationEndpoints.cs`
  - Validate `rng.kind` if provided.
  - Enforce seed presence when template has RNG.
  - Default seed to 123 when `rng` is null and no RNG requirement.
  - Pass seed into orchestration → artifact writer.
  - Echo seed in responses (detail + list).

3) Deterministic default
- `src/FlowTime.Core/Artifacts/RunArtifactWriter.cs`
  - Replace random seed fallback with constant `123` (`DefaultSeed`).

4) Telemetry capture metadata
- `src/FlowTime.Generator/TelemetryGenerationService.cs`
  - Extend `TelemetryGenerationMetadata` to include `int? RngSeed`, `string ParametersHash`, `string ScenarioHash`.
  - Compute `parametersHash` from normalized `{ templateId, templateVersion, parameters(sorted), executionSeed }`.
  - Write fields into `autocapture.json` alongside existing metadata.

5) Reader exposure (if needed)
- `src/FlowTime.Core/TimeTravel/RunManifestReader.cs`
  - Ensure seed is available for API responses.

6) UI updates
- `src/FlowTime.UI/Pages/TimeTravel/RunOrchestration.razor`: add “RNG Seed” (default 123) and include in requests.
- `src/FlowTime.UI/Pages/TimeTravel/Artifacts.razor`: show seed in Run Summary if available.

7) Tests
- API integration tests: seed acceptance, response echo, validation error when template RNG requires seed.
- Generator tests: `autocapture.json` contains `rngSeed`, `parametersHash`, `scenarioHash`.
- UI tests: request serialization with seed; seed displayed in run detail.

8) Docs
- Update `docs/operations/telemetry-capture-guide.md` with new fields.
- Update `docs/architecture/time-travel/ui-m3-roadmap.md` seeded loop notes.

---

## Deliverables

- Updated API/DTOs with explicit RNG block and response echo.
- Deterministic default seed (123) in artifact writer.
- Telemetry `autocapture.json` carries `rngSeed`, `parametersHash`, `scenarioHash`.
- UI orchestration seed input and run detail seed display.
- Tests for API/UI/generator.
- Docs updated (capture guide, roadmap).

---

## Test Plan

Automated
- Create simulation run with `rng.seed=777` → response metadata seed = 777; provenance contains `rng.seed=777`.
- Create simulation run without `rng` and template has no RNG → response metadata seed = 123.
- Template declares RNG and request omits seed → `400` with clear error.
- After `POST /v1/telemetry/captures`, `autocapture.json` includes `rngSeed` and valid `parametersHash` + `scenarioHash`.

Manual
1) Create a simulation run with default seed → Open in Artifacts → seed shows as 123.
2) Override seed to 42 → run again → seed shows as 42; telemetry capture includes `rngSeed=42`.
3) Use a template with `rng` block and omit seed → run fails with actionable message.

---

## Acceptance Criteria

- All simulation runs are deterministic (no random seed generation anywhere). Default seed = 123 when not provided and template does not require RNG.
- API accepts `rng` and echoes seed in run responses; UI can display seed.
- Telemetry capture metadata (`autocapture.json`) includes `rngSeed`, `parametersHash`, and `scenarioHash`.
- Templates with RNG require an explicit seed; omission yields a `400` with a clear error.
- No filesystem path leakage in UI or responses; internal salts are never exposed.
- API/UI test suites pass with updated assertions.

---

## Risks & Mitigations

- Contract churn across UI/API: mitigate with coordinated DTO changes and tests.
- Confusion between execution seed and hashing: document clearly; never surface internal salts; keep both `parametersHash` (stable) and `scenarioHash` (writer) for clarity.
- Future stochastic execution: this milestone confines scope to provenance and determinism; sampling changes are a later engine milestone.

---

## References

- docs/milestones/TT-M-03.17.md
- docs/milestones/TT-M-03.18.md
- docs/architecture/time-travel/ui-m3-roadmap.md
- docs/operations/telemetry-capture-guide.md
