# Milestone: SIM-SVC-M2 — Minimal Simulation Service/API (Pre-Charter)

> ⚠️ **CHARTER VIOLATION**: This milestone describes FlowTime-Sim as a "simulation engine" that generates telemetry, violating Charter v1.0.
> Per Charter v1.0, FlowTime-Sim is a "modeling front-end" that creates model artifacts but **never computes telemetry**.
> This milestone is **SUPERSEDED** by charter-compliant SIM-M2.6 (Model Authoring) and SIM-M2.7 (Registry Integration).

Status: Complete (Pre-Charter, now deprecated)

## Goal (Charter Violation)
**OUTDATED**: This goal violates charter by describing telemetry generation. Charter-compliant model authoring services replace this functionality.

## Motivation
- Decouple front-end (UI) from local CLI execution.
- Provide a stable HTTP contract for triggering and reading simulation outputs.
- Lay groundwork for future streaming & Parquet support without changing core artifact layout.

## Out of Scope (Deferred)
- Streaming incremental series/event delivery (future streaming milestone).
- Parquet emission (SIM-M3 or dedicated GOLD milestone).
- Authentication / authorization (will add AAD/JWT integration later).
- Advanced scenario catalogs and version negotiation (basic static listing only, or deferred entirely if not needed for acceptance).
- Overlay parameter validation beyond shallow merge.

## Success Criteria / Acceptance
- POST /sim/run accepts a valid spec and returns HTTP 200 with `{ simRunId }`.
- Artifacts written under `<RUNS_ROOT>/runs/<simRunId>/` mirror CLI output (byte-identical per-series CSV + identical hashes for same spec+seed).
- GET /sim/runs/{id}/index returns the `series/index.json` content for a completed run (200) or 404 if not found.
- GET /sim/runs/{id}/series/{seriesId}` streams the CSV file with `text/csv` content-type (404 if not found).
- Service is stateless: no in-memory retention required after artifacts are written.
- Determinism kept: repeating POST with identical spec (including seed) produces new run directory with identical per-series hashes (different runId & timestamps only).
- CORS enabled (permissive) for local development.
- Invariant culture numeric formatting.

## API Surface (Planned Minimum)
| Method | Path | Description | Status |
|--------|------|-------------|--------|
| POST | /sim/run | Submit simulation spec YAML; returns `{ simRunId }` | Implemented (initial) |
| GET | /sim/runs/{id}/index | Retrieve JSON index for a run | Implemented |
| GET | /sim/runs/{id}/series/{seriesId} | Retrieve a specific series CSV | Implemented |
| GET | /sim/scenarios | List available scenarios (IDs + brief metadata) | Implemented |
| POST | /sim/overlay | Create run from base run + overlay patch | Implemented |

Future (not required for acceptance): scenario hash introspection endpoint, health expansions, metrics.

## Data Model Notes
- `simRunId` format unchanged (`sim_YYYY-MM-DDTHH-mm-ssZ_<slug>`).
- Scenario hash = model hash of original submitted YAML (same as CLI) stored in manifest/run.json.
- RNG, seed, and schemaVersion persisted identically via `RunArtifactsWriter`.

## Environment Configuration
| Variable | Purpose | Default |
|----------|---------|---------|
| FLOWTIME_SIM_RUNS_ROOT | Directory root where `runs/` folder is created | Current working directory |
| FLOWTIME_SIM_WARN_LEGACY | Re-enable legacy spec warnings during spec migration | (unset) silent |

## Error Handling
| Case | Response |
|------|----------|
| Empty body | 400 `{ error: "Empty body" }` |
| YAML parse error | 400 `{ error: "YAML parse failed", detail }` |
| Spec validation failure | 400 `{ error: "Spec validation failed", errors: [...] }` |
| Not found (index/series) | 404 `{ error: "Not found" }` (planned) |
| Client abort (cancellation) | 499 (client closed request) |
| Unhandled exception | 500 ProblemDetails |

## Determinism Guarantees (Carried Forward)
Unchanged from SIM-M2: given identical spec + seed, per-series CSV content & hashes are identical across runs; only runId & timestamps differ.

## Implementation Plan
1. Bootstrap service project & wire to core + artifact writer. (DONE)
2. Implement POST /sim/run (YAML parse → validate → generate arrivals → write artifacts → return ID). (DONE initial)
3. Add GET /sim/runs/{id}/index (read file & return JSON) with 404 handling.
4. Add GET /sim/runs/{id}/series/{seriesId} (stream file; validate path traversal avoidance).
5. Introduce minimal scenarios registry (optional if needed for UI) and GET /sim/scenarios.
6. Implement POST /sim/overlay (load base manifest + apply shallow overlay to spec, regenerate). Optional for milestone if time permits.
7. Integration tests: POST then fetch index & a series; hash parity with CLI; negative 400/404 cases. (DONE)
8. Documentation updates (contracts.md, testing.md). (DONE)
9. Final polish: refine logging, review CORS defaults, draft release notes. (IN PROGRESS)

## Testing Strategy Additions
- New test fixture spins up WebApplicationFactory for service endpoints.
- Round-trip: create run via API; verify presence & shape of artifact files & JSON schema validation (reuse existing schemas if accessible, or copy minimal checks).
- Parity: run same spec via CLI and API, compare series file SHA-256 hashes.

## Security Considerations
- YAML submission surface: limit to simulation spec structure; currently ignoring attempts at large payloads beyond letting Kestrel size limits handle. Future: size cap + yaml safe loader adjustments.
- Path traversal: sanitize `id` and `seriesId` (no `..`, no path separators) before file access.
- CORS: wide open for local dev; must restrict origin + methods in future production milestone.

## Open Questions / Follow-Ups
- Idempotency (dedupe identical spec submissions) — deferred.
- Deep vs shallow overlay extension for future spec complexity — next milestone to define.
- Scenario registry externalization (resource file or dynamic source) — future.
- Streaming alignment and events parity when SSE milestone lands.

## Exit Criteria Checklist
- [x] POST /sim/run deterministic and returns 200 + ID.
- [x] CLI vs API parity test green.
- [x] GET index + series endpoints implemented with 404 handling & path safety.
- [x] Basic integration + negative tests passing.
- [x] Overlay endpoint + spec.yaml persistence.
- [x] Scenario registry endpoint.
- [x] Docs updated (contracts.md & testing.md).
- [x] Release notes draft.

---
(Milestone closed. Further work will track under next service milestone.)
