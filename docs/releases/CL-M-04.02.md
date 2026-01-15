# CL-M-04.02 — Engine & State Aggregation for Classes

## Summary
- Added class-aware aggregation across engine/state: `/state` and `/state_window` now emit `byClass` blocks plus `classCoverage` metadata for compatibility with class-enabled runs.
- Telemetry artifacts are class-aware: telemetry CSVs include `classId`, manifests list `classes` and `classCoverage`, and run/series metadata mirror these fields for downstream consumers (UI/CLI readiness).
- Contracts/schemas updated (`time-travel-state`, telemetry manifest) with class structures; adapters and validators ingest class-aware artifacts.

## Tests
- `dotnet test --nologo` (perf suite skipped by design; `Test_PMF_Normalization_Performance` marked skip).
- Targeted: `TelemetryCaptureTests`, `TelemetryBundleBuilderTests`, `StateEndpointTests`, CLI telemetry workflows.

## Notes
- Legacy artifacts remain unchanged; new artifacts include `classId`/`classCoverage` fields and continue to parse in legacy mode (wildcard `*` bucket).
- Performance test skips are expected for the dev environment; no functional regressions observed.
