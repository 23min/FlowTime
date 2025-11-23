# SIM-M2 — Contracts Parity (Artifacts Alignment)

## Status
COMPLETED (branch `feature/docs-m2/roadmap-updates`) — pending tag.

## Goal
Deliver a stable per-run artifact pack: dual JSON (`run.json`, `manifest.json`), `series/index.json`, and per-series CSVs with deterministic hashing & integrity (tamper) tests; defer Parquet Gold table, event enrichment, and service/API work to later milestones.

## Scope
- Run directory: `runs/<runId>/` (replaces earlier draft `out/<runId>/`).
  - `run.json` — summary (schemaVersion, runId, grid, scenarioHash, rng) + (future) warnings; mirrors `manifest.json` for now.
  - `manifest.json` — integrity: per-series hashes (`seriesHashes`), eventCount (0 if events omitted), createdUtc.
  - `series/index.json` — enumeration (id, kind, unit, path, points, hash, optional formats.goldTable placeholder unused this milestone).
  - `series/*.csv` — canonical per-series time series (`t,value`).
- Removed legacy `metadata.json` and single `gold.csv` canonical role.
- Hashing: raw file bytes SHA-256 with `sha256:` prefix; CSV LF newlines, invariant formatting.

## Non-Goals

## Run Directory Example
```
runs/sim_2025-09-01T18-30-12Z_a1b2c3d4/
  run.json
  manifest.json
  series/
    arrivals@COMP_A.csv
    served@COMP_A.csv
  series/index.json
  [events.ndjson]
```

## JSON Sketches (Illustrative)
`run.json`:
```json
{
  "schemaVersion": 1,
  "runId": "run_20250901T183012Z_a1b2c3d4",
  "scenarioHash": "sha256:...",
  "grid": { "bins": 4, "binMinutes": 60 },
  "series": [ { "id": "arrivals", "path": "arrivals.csv" }, { "id": "gold", "path": "gold.csv" } ],
  "events": { "schemaVersion": 0, "fieldsReserved": ["entityType","routeId","stepId","componentId","correlationId"] }
}
```
`manifest.json`:
```json
{
  "schemaVersion": 1,
  "modelHash": "sha256:...",
  "scenarioHash": "sha256:...",
  "seed": 12345,
  "rng": "pcg",
  "seriesHashes": { "arrivals": "sha256:...", "gold": "sha256:..." },
  "eventCount": 0,
  "generatedAtUtc": "2025-09-01T18:30:12Z"
}
```
`index.json`:
```json
{
  "series": [
    { "id": "arrivals", "path": "arrivals.csv", "hash": "sha256:...", "points": 4, "kind": "const", "units": null },
    { "id": "gold", "path": "gold.csv", "hash": "sha256:...", "points": 4, "kind": "served", "units": null }
  ]
}
```

## Acceptance Criteria

## Open Questions

## Plan (Slices)
1. Hashing & normalization + tests.
2. Manifest writer (replace metadata.json) + tests.
3. run.json + index.json + arrivals.csv emission.
4. JSON Schema & validation tests.
5. Docs update & finalize deprecation note.

## Determinism Notes

## Future Parity Hook
Add cross-repo parity tests once engine M1.5 artifacts land.
