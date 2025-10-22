# Telemetry Generation — Explicit Step (Working Plan)

Status: Draft for approval  
Scope: Replace auto‑capture paths with an explicit, operator‑initiated telemetry generation step

---

## Context

Problem: We don’t have real telemetry yet. The UI needs a way to “replay from telemetry” in addition to replaying the model.  
Constraint: Telemetry must never be auto‑generated as part of run creation. The generation step must be explicit and intentional (and later removable/hidden once real telemetry arrives).

This document replaces the auto‑capture approach in TT‑M‑03.17 with a clear, separable telemetry generation workflow (new endpoint + explicit UI action), plus provenance so the UI can advertise whether telemetry exists for a given run.

---

## Objectives

- Keep Simulation and Telemetry replay as separate concerns (no hidden auto‑steps).
- Provide a first‑class “Generate telemetry bundle” action that writes a `manifest.json` (and metadata) for replay.
- Surface provenance in the UI so an operator can choose “Replay from model” vs “Replay from telemetry”.
- Make removal easy once real ingestion lands (endpoint/UI isolated, minimal coupling).

### Non‑Goals
- Live ingestion adapters and streaming telemetry (future milestone).
- Complex policy/retention. Simple overwrite semantics only.
- Backwards compatibility for auto‑generation inside `/v1/runs`.

### Terminology
- Simulation run: canonical engine artifacts created from a template + parameters.
- Telemetry bundle: CSVs + `manifest.json` produced by capture; consumed by telemetry replay.
- Capture key: stable directory key under a configured `TelemetryRoot`.

---

## API Design

### 1) Generate Telemetry (new)

`POST /v1/telemetry/captures`

Request (v1: from existing simulation run):

```json
{
  "source": { "type": "run", "runId": "RUN_123" },
  "output": {
    "captureKey": "template-telemetry",  
    "overwrite": false
  }
}
```

- If `captureKey` is supplied and `TelemetryRoot` is configured, writes to `TelemetryRoot/<captureKey>/`.
- (Optional) Alternate: `output.directory` (absolute path) instead of `captureKey`.

Response:

```json
{
  "capture": {
    "captureDirectory": "examples/time-travel/template-telemetry",
    "generated": true,
    "alreadyExists": false,
    "manifestPath": ".../manifest.json",
    "sourceRunId": "RUN_123",
    "warnings": [ { "code": "gap_fill", "message": "...", "nodeId": "...", "bins": [12, 13] } ]
  }
}
```

Errors:
- 404 — source run not found
- 409 — bundle exists and `overwrite = false`
- 400 — invalid request (missing fields)
- 500 — generation failed

Notes:
- Writes lightweight metadata alongside the manifest (e.g., `autocapture.json`): `{ templateId, captureKey, sourceRunId, generatedAtUtc, parametersHash }`.
- Log lifecycle: start, success, failure (templateId, captureKey, runId).

### 2) Run Orchestration (existing)

`POST /v1/runs` remains focused on creating simulation or telemetry runs:
- No auto‑generation path.
- Telemetry replay requires an existing bundle (capture directory + bindings). Missing bundle → `422 Unprocessable Entity` with a helpful message.

---

## Backend Changes

- Add `TelemetryGenerationService`
  - Reads canonical run (via `RunArtifactReader`).
  - Executes capture pipeline (writes CSVs + `manifest.json`).
  - Writes `autocapture.json` metadata (fields above) for provenance.
  - Validates overwrite semantics; returns structured status (generated vs alreadyExists).
- Keep `TelemetryBundleBuilder` unchanged (consumes an existing bundle only).
- Remove auto‑generation logic from `RunOrchestrationService` and `/v1/runs` endpoint: no `autoCapture` flag, no `capture` block in run creation responses.

---

## UI Flows

### Run Orchestration page
- Modes:
  - Simulation — unchanged (template → model → compute/render).
  - Telemetry — requires capture dir + bindings; no generation. If missing, UI should guide the operator to generate telemetry first (see below).
- Remove the “Auto‑generate telemetry bundle” toggle and any capture status tied to `/v1/runs`.

### Artifacts / Run Detail view
- Show provenance for the selected run: template, parameters, and telemetry availability.
- Actions:
  - “Generate telemetry for this run” → `POST /v1/telemetry/captures` with `{ source: { type:"run", runId }, output: { captureKey or directory, overwrite? } }`.
  - “Replay from model” → create simulation‑mode run (existing path).
  - “Replay from telemetry” → enabled only when telemetry is available; creates telemetry‑mode run.
- Display the telemetry summary without filesystem paths:
  - Available: Yes/No
  - Generated at: timestamp from metadata
  - Warning count: integer
  - Optional: Source run id
  - Do not surface `captureDirectory` in the UI.

---

## Provenance

- Minimal metadata file (JSON) next to `manifest.json` with fields:
  - `templateId`, `captureKey`, `sourceRunId`, `generatedAtUtc`, `parametersHash`.
- API surfaces a telemetry summary in run metadata so the UI can decide without paths:
  - Proposed addition to `GET /v1/runs/{id}` and listings: `telemetry: { available: bool, generatedAtUtc?: string, warningCount: number, sourceRunId?: string }`.
- UI consumes this summary; it should not display filesystem paths.

---

## Migration & Cleanup

Replace the inlined auto‑generation work with the explicit flow:

- Contracts
  - Remove `RunCreationOptions.autoCapture` and `RunCreateResponse.capture` from `/v1/runs` models.
- API
  - Remove `capture` block construction from `POST /v1/runs` responses.
  - Add `POST /v1/telemetry/captures` with request/response as above.
  - Add non‑path telemetry summary to run listing/detail responses (available, generatedAtUtc, warningCount, optional sourceRunId).
- Generator
  - Delete auto‑generation helpers/locks from `RunOrchestrationService`.
- UI
  - Remove run‑orchestration toggle + capture status rendering.
  - Add “Generate telemetry” action on run detail page; show provenance; enable/disable replay buttons accordingly.
- Tests
  - Update goldens to remove `capture` in run‑creation responses.
  - Add generation endpoint tests: generated, already exists (409), invalid input (400).
  - UI tests: submission builder (no autoCapture), snapshot/provenance serialization.
- Docs
  - Update `telemetry-capture-guide.md` (remove auto‑capture notes; document new endpoint + UI usage).
  - Update roadmap/milestones per below.

---

## Phased Work Breakdown

- 17A — Backend endpoint + service + tests
- 17B — Artifacts/Run detail UI actions + provenance; remove orchestration toggle
- 17C — Docs/roadmap/milestone cleanup; migrate goldens
- 18 — Replay selection UX polish (clear “model vs telemetry” affordances)
- 19 — Seeded RNG support (optional follow‑on once replay flow is stable)

---

## Open Decisions (confirm)

- Source of generation (v1): Only from an existing simulation run identified by `runId`. (Yes)
- Output naming: Default to `template.metadata.captureKey` when present; allow explicit `directory` override. (OK)
- Overwrite default: `false` → 409 on collision; operator can re‑run with `overwrite: true`. (OK)
- CLI parity: Optional thin wrapper that POSTs the same payload (nice‑to‑have). (Yes)
- UI should not surface capture directories; only telemetry availability and minimal facts (generatedAt, warningCount).

---

## Removal Path (when real telemetry arrives)

- Hide the “Generate telemetry” action from UI by feature flag; leave codepath for demos/tests.
- Keep endpoint guarded similarly (or remove entirely) to reduce maintenance.

---

## Acceptance Criteria

- Telemetry generation is explicit and succeeds independently of run creation.
- `/v1/runs` does not generate telemetry; telemetry replay fails fast (422) when bundle is missing.
- Artifacts/Run detail shows clear provenance (no paths) and enables correct actions.
- Tests: endpoint (generate/reuse/errors) + UI serialization; goldens updated.

---

## Manual Verification Checklist

1. Create a simulation run from a template.
2. Open the run detail; verify “Telemetry: not available”.
3. Click “Generate telemetry for this run” → endpoint returns success; provenance shows up.
4. Click “Replay from telemetry” → run is created; UI confirms replay from telemetry.
5. Attempt “Replay from telemetry” without generation → rejected with clear message.

---

## Risks & Mitigations

- Operator confusion between model vs telemetry replay → UI copy and provenance should be explicit.
- Over‑generation on accident → overwrite=false by default; UI prompts when overwriting.
- Future removal complexity → endpoint/UI isolated; clean toggle path documented above.
