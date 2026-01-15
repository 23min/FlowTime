# Release CL-M-04.04 — Telemetry Contract & Loop Validation

**Release Date:** 2025-11-27  
**Type:** Milestone delivery (no version bump)  
**Key Fixtures:** `tests/fixtures/templates/loop-parity-template.yaml` (loop parity), `data/run_20251125T130751Z_21597334` / `data/run_20251125T130822Z_91deaced` (class-enabled demos)

## Overview

This release closes the class-aware telemetry loop:

- Manifest schema version 2 introduces `supportsClassMetrics`, `classCoverage`, and per-file `classId` entries; docs and schema tests enforce the new contract while keeping legacy totals-only bundles valid.
- TelemetryLoader/CLI now ingest per-class CSVs, validate conservation, and emit coverage warnings so operators can see mismatches immediately.
- `/v1/telemetry/captures` returns class metadata, stores schema v2 manifests under each run, and surfaces warnings to the UI/CLI.
- A deterministic loop-parity fixture plus `ClassesLoopTests` prove simulation-vs-telemetry parity and ensure missing-class telemetry downgrades coverage gracefully.

## Key Changes

1. **Schema & Docs** – `docs/schemas/telemetry-manifest.schema.json` bumped to schemaVersion 2; `docs/operations/telemetry-capture-guide.md` now illustrates class-aware manifests and producer guidance.
2. **TelemetryLoader & CLI** – Loader aggregates class CSVs, validates totals vs class sums, and the CLI prints coverage summaries/warnings (`TelemetryLoaderByClassTests` keep behavior locked).
3. **Capture Endpoint** – `/v1/telemetry/captures` returns `supportsClassMetrics`, `classCoverage`, and `classes`, while persisting the manifest to `model/telemetry/telemetry-manifest.json` for downstream consumers.
4. **Loop Regression Fixture** – `tests/fixtures/templates/loop-parity-template.yaml` plus `ClassesLoopTests` cover simulation vs telemetry `/state_window` equality and missing-class warnings end-to-end.

## Tests

- `dotnet test tests/FlowTime.Integration.Tests/FlowTime.Integration.Tests.csproj --filter ClassesLoopTests --nologo`
- `dotnet test tests/FlowTime.Api.Tests/FlowTime.Api.Tests.csproj --filter TelemetryCaptureEndpointsTests --nologo`
- `dotnet build`
- `dotnet test --nologo` *(perf benchmark suite remains skipped as planned)*

## Known Issues / Follow-ups

- Existing transportation/supply-chain demo runs still carry informational analyzer warnings (router leakage, backlog conservation); tracked for CL-M-04.05/CL-M-04.06.
- Service-with-buffer work (SB-M-01) will replace the backlog “convolution” modeling hack noted during CL-M-04.03/04.04 testing.
- Perf benchmark skips remain deferred to the epic-level perf sweep.

## Verification Artifacts

- `tests/fixtures/templates/loop-parity-template.yaml`
- `data/run_20251125T130751Z_21597334` (supply-chain classes) and `data/run_20251125T130822Z_91deaced` (transportation classes)
