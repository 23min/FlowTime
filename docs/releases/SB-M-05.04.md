# Release SB-M-05.04 — Deterministic Run Orchestration

**Release Date:** 2025-12-09  
**Type:** Milestone delivery (SIM/engine boundary)  
**Key Surfaces:** FlowTime.Sim Service, FlowTime.API, FlowTime.Cli, FlowTime.UI

## Overview

SB‑M‑05.04 finishes the SIM/engine separation for run orchestration. FlowTime-Sim now computes deterministic bundle hashes, stores canonical artifacts under stable IDs, and exposes reuse/overwrite semantics through both the CLI and the new `/api/v1/orchestration/runs` endpoint. FlowTime.API `/v1/runs` became a pure bundle importer; UI/CLI flows must call the SIM service first and then hand the canonical bundle to the engine. The release also captured the debugging workflow used for performance investigations so future milestones can attach Chrome traces and WASM debugging steps.

## Key Changes

1. **Deterministic orchestration + reuse**
   - `RunOrchestrationService` stamps `inputHash` metadata, names bundles `run_<templateId>_<hash>`, and short-circuits to existing bundles unless `overwriteExisting=true`. Provenance (`run.json`, `provenance.json`, manifest) records the deterministic fingerprint.
   - FlowTime.Sim Service hosts `/api/v1/orchestration/runs`, reusing `RunOrchestrationContractMapper` so HTTP responses match CLI/UI data models. CLI (`flowtime telemetry run`) defaults to reuse, adds `--force-overwrite`, and reports whether a run was reused.
2. **Engine import contract + UI alignment**
   - FlowTime.API `/v1/runs` now accepts `RunImportRequest` with `bundlePath` or `bundleArchiveBase64`, copies bundles into the engine data root, and rejects legacy template payloads with `410 Gone`.
   - FlowTime.UI orchestration views switched to `FlowTimeSimApiClient`, leaving the engine API read-only (state listings, telemetry captures). Telemetry capture endpoints/tests now import canonical bundles before running captures, ensuring parity with production flows.
3. **Docs & debugging workflow**
   - `docs/operations/telemetry-capture-guide.md` documents the SIM-first orchestration, deterministic bundle reuse, and the CLI/SIM environment variables required to generate canonical artifacts.
   - `docs/development/ui-debug-mode.md` records the Bash/PowerShell commands for launching FlowTime.UI in Debug mode plus the Chrome DevTools checklist (Shift+Alt+D WASM debugger, Performance panel workflow) used during the performance milestone validations.

## Tests

- `dotnet build`
- `dotnet test --nologo` (expected skips: `FlowTime.Tests.Performance.M2PerformanceTests.*`, FlowTime.Sim example smoke tests)
- Targeted regression: `dotnet test tests/FlowTime.Api.Tests/FlowTime.Api.Tests.csproj --filter TelemetryCaptureEndpointsTests`

## Known Issues / Follow-ups

- FlowTime.Tests performance benchmarks remain skipped in CI; they require a tuned environment and are documented as acceptable skips for all SB‑M‑05 milestones.
- FlowTime.Sim deterministic bundles assume the SIM data root is shared with FlowTime.API (`FLOWTIME_SIM_DATA_DIR`, `ArtifactsDirectory`). When running the CLI outside the dev container, ensure those paths match or bundle reuse will mis-identify existing runs.
- Third-party callers of FlowTime.API `/v1/runs` must migrate to bundle imports; template payloads now receive HTTP 410 with guidance to call FlowTime-Sim. FlowTime.Api client wrappers will be cleaned up in a follow-up once all downstream tooling has switched to bundle imports.

## Verification

- Ran `flowtime telemetry run` twice against the sample templates to confirm deterministic reuse messaging, then forced overwrite to regenerate bundles and inspect `WasReused=false` output.
- Imported the resulting bundles through FlowTime.API `/v1/runs` and executed `/v1/telemetry/captures` to validate the telemetry capture guide instructions.
- Launched FlowTime.UI via the documented Bash/PowerShell scripts, attached Chrome’s WASM debugger (`Shift+Alt+D`), and captured a Performance trace while running the deterministic-run scenario to ensure the instructions remain accurate.
