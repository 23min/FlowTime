# Release Notes: SIM-SVC-M2 – Minimal Simulation Service/API

Date: 2025-09-03
Status: Released (tag: sim-svc-m2, commit: 7fe761a)

## Overview
SIM-SVC-M2 introduces the first HTTP service layer for the FlowTime simulator. The service exposes the same deterministic artifact pack as the CLI (run.json, manifest.json, series/index.json, per-series CSV, optional events), enabling remote invocation by UI or other services.

Release Tag Notes:
- Annotated tag `sim-svc-m2` created on commit `7fe761a`.
- No code changes after tagging; this document update is documentation-only.

## Highlights
- New ASP.NET Core service (`FlowTime.Sim.Service`) with stateless endpoints.
- Deterministic parity with CLI outputs (per-series CSV hashes) for identical spec + seed.
- Overlay capability: derive a new run from an existing base run with a shallow spec patch (seed/grid/arrivals-rate adjustments).
- Scenario registry endpoint providing a minimal discoverable catalog.
- Persisted original `spec.yaml` for every run to enable reproducible overlays.

## Endpoints
| Method | Path | Description |
|--------|------|-------------|
| POST | /sim/run | Submit YAML spec; returns `{ simRunId }` |
| GET | /sim/runs/{id}/index | Retrieve `series/index.json` (404 if missing) |
| GET | /sim/runs/{id}/series/{seriesId} | Stream a series CSV (404 if missing) |
| GET | /sim/scenarios | List static scenarios (id + brief metadata) |
| POST | /sim/overlay | Create a new run from a base run + shallow overlay patch |
| GET | /healthz | Liveness check |

## Run Artifact Layout
```
<root>/runs/<simRunId>/
  run.json
  manifest.json
  spec.yaml
  events.ndjson        (optional when includeEvents=true)
  series/
    index.json
    arrivals@<COMP>.csv
    served@<COMP>.csv
    errors@<COMP>.csv
```

## Overlay Semantics
- Body: `{ "baseRunId": "...", "overlay": { "seed"?, "grid"?, "arrivals"? } }`
- Shallow merge only; unspecified sections preserved.
- `arrivals.rate` overrides `arrivals.rates` if both supplied.
- Validation errors return `400`; unknown base run returns `404`.

## Determinism
For identical spec + seed: 
- Per-series CSV content and SHA-256 hashes are identical between CLI and Service.
- Only `simRunId` + timestamps differ.

## Error Model (Summary)
| Condition | Status | Body |
|-----------|--------|------|
| Invalid / empty body | 400 | `{ error: "..." }` |
| Spec validation failure | 400 | `{ error: "Spec validation failed", errors:[...] }` |
| Run / series not found | 404 | `{ error: "Not found" }` |
| Overlay base run missing | 404 | `{ error: "Not found" }` |
| Unhandled (unexpected) | 500 | ProblemDetails |

## Environment Variables
| Name | Purpose |
|------|---------|
| FLOWTIME_SIM_RUNS_ROOT | Root directory for `runs/` |
| FLOWTIME_SIM_WARN_LEGACY | Enables legacy spec warnings (optional) |

## Testing Summary
- 43 automated tests (integration + negative) added/updated.
- CLI ↔ Service parity test ensures identical arrivals CSV hash.
- Negative coverage: 400 (invalid ID), 404 (missing run/series/base overlay).

## Internal Notes / Implementation
- Single-process stateless design; all state on disk.
- `RunArtifactsWriter` reused from CLI for artifact parity.
- Simple path safety checks for run and series IDs (disallow traversal chars).
- YAML ingestion input `Content-Type: text/plain`.

## Known Limitations / Deferred
- No streaming or incremental delivery yet.
- No Parquet export.
- No auth / rate limiting.
- Overlay limited to shallow patch.
- Scenario registry static/in-memory.

## Upgrade / Migration
No breaking changes to existing CLI artifacts; this is additive. Existing tooling can point to the service instead of local CLI to generate runs.

## Next Steps (Proposed)
- Add structured logging & correlation IDs.
- Introduce GET variant for overlay diff/preview.
- Parquet / columnar output option (future milestone).
- Scenario versioning & persisted catalog.
- Streaming SSE / WebSocket for progressive series.

---
End of SIM-SVC-M2 release notes.
