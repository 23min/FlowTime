# SIM-M2 — Contracts Parity (Artifacts Alignment)

## Status
Draft (in progress on branch `feature/contracts-m2/artifacts`).

## Goal
Introduce dual-write structured run artifacts (run.json, manifest.json, index.json, per-series CSV) with deterministic hashing so FlowTime-Sim stays lock-stepped with FlowTime engine milestone M1.5 and unblocks UI + cross-tooling.

## Scope
- Deterministic model normalization + hashing (modelHash, scenarioHash).
- New run directory layout `out/<runId>/`.
- Files:
  - `run.json` — summary + series listing + reserved event placeholders.
  - `manifest.json` — determinism & integrity metadata (seed, rng, seriesHashes, eventCount, generatedAtUtc).
  - `index.json` — quick lookup of series (id, path, hash, points, kind, units?).
  - `arrivals.csv` — arrivals counts per bin (t,value).
  - `gold.csv` — existing aggregate served counts (retained).
- Replace legacy `metadata.json` with `manifest.json` (graceful transition notes).
- Hashing rules: LF endings, trim trailing whitespace, collapse blank lines, ignore YAML key ordering.
- CLI flags: `--no-manifest` (skip new JSON artifacts), future `--deterministic-run-id` (optional).
- Tests: hashing stability, manifest presence, structural field assertions, deterministic repeatability.
- JSON Schemas (phase 2 of milestone; may land after initial writer code): `docs/contracts/*.schema.json`.

## Non-Goals
- No new simulation semantics (routing/backlog/service times still placeholders).
- No event emission beyond current events.ndjson (event enrichment reserved only).
- No streaming or API surface yet.

## Run Directory Example
```
out/run_20250901T183012Z_a1b2c3d4/
  run.json
  manifest.json
  index.json
  arrivals.csv
  gold.csv
  events.ndjson
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
- Running existing sample specs produces new directory with all artifacts.
- Hash stability tests pass (key reorder → same hash; value change → different hash).
- Repeat runs with same seed produce identical seriesHashes & manifest.
- Legacy `metadata.json` removed or soft-deprecated (decision documented here before merge).
- All prior tests remain green.

## Open Questions
- Should `scenarioHash` differ from `modelHash` early? (Decision: keep identical now; diverge later if scenario overlays are added.)
- Keep or remove legacy `metadata.json`? (Leaning remove; add note in CHANGELOG.)
- Include absolute durations or timezone info in run.json? (Defer until backlog/latency.)

## Plan (Slices)
1. Hashing & normalization + tests.
2. Manifest writer (replace metadata.json) + tests.
3. run.json + index.json + arrivals.csv emission.
4. JSON Schema & validation tests.
5. Docs update & finalize deprecation note.

## Determinism Notes
- Use `Utf8JsonWriter` with stable ordering; sort dictionary keys.
- Force `\n` newline in CSV.

## Future Parity Hook
Add cross-repo parity tests once engine M1.5 artifacts land.
