# Milestone: SIM-SVC-M2 — Minimal Simulation Service/API (Artifact-Centric)

Status: DRAFT

## Goal
Expose the simulation engine as a stateless HTTP service that produces the same on-disk artifact pack (run.json, manifest.json, series/index.json, per-series CSVs, optional events.ndjson) as the CLI, enabling UI / other services to request new simulation runs via simple HTTP calls.

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
| GET | /sim/runs/{id}/index | Retrieve JSON index for a run | Pending |
| GET | /sim/runs/{id}/series/{seriesId} | Retrieve a specific series CSV | Pending |
| GET | /sim/scenarios | List available scenarios (IDs + brief metadata) | Pending / Maybe |
| POST | /sim/overlay | Create run from base run + overlay patch | Pending |

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
7. Integration tests: POST then fetch index & a series; assert hash consistency with CLI for same YAML.
8. Documentation updates (contracts.md references to service endpoints; testing.md add service tests section).
9. Final polish: logging (request/response summary), 404 responses, tighten CORS note, milestone release notes.

## Testing Strategy Additions
- New test fixture spins up WebApplicationFactory for service endpoints.
- Round-trip: create run via API; verify presence & shape of artifact files & JSON schema validation (reuse existing schemas if accessible, or copy minimal checks).
- Parity: run same spec via CLI and API, compare series file SHA-256 hashes.

## Security Considerations
- YAML submission surface: limit to simulation spec structure; currently ignoring attempts at large payloads beyond letting Kestrel size limits handle. Future: size cap + yaml safe loader adjustments.
- Path traversal: sanitize `id` and `seriesId` (no `..`, no path separators) before file access.
- CORS: wide open for local dev; must restrict origin + methods in future production milestone.

## Open Questions
- Do we require idempotency (dedupe identical spec submissions)? (Deferred—currently each POST yields a fresh run directory.)
- Overlay semantics depth (shallow vs deep merge) for complex future spec additions.
- Scenario registry format (YAML in repo vs embedded resources).

## Exit Criteria Checklist
- [ ] POST /sim/run deterministic and returns 200 + ID.
- [ ] CLI vs API parity test green.
- [ ] GET index + series endpoints implemented with 404 handling & path safety.
- [ ] Basic integration test project added and passing in CI.
- [ ] Milestone docs updated (this file + roadmap cross-ref).
- [ ] Contracts doc mentions service endpoints.
- [ ] Release notes (future SIM-SVC-M2) drafted.

---
(End of SIM-SVC-M2 draft milestone notes)
