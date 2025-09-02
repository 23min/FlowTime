# Release: SIM-M2 — Artifact Parity & Series Index

Status: DRAFT (pending tag `sim-m2`)

## Summary
SIM-M2 establishes a stable artifact layout for FlowTime-Sim runs: dual JSON (`run.json`, `manifest.json`), `series/index.json`, and per-series CSV files with deterministic hashing and integrity (tamper) tests. Legacy `metadata.json` and single-file `gold.csv` usage are deprecated in favor of per-series discovery. Parquet Gold table, event enrichment, and service/API endpoints are deferred.

## Highlights
- Dual-write JSON artifacts (currently identical content) allow future semantic separation (summary vs integrity) without breaking consumers.
- Per-series CSV layout under `runs/<runId>/series/` with canonical discovery via `series/index.json`.
- SHA-256 hashing for each series (`sha256:<64hex>`), stored in `manifest.json`; tamper test ensures integrity.
- Standardized `runId` format: `sim_YYYY-MM-DDTHH-mm-ssZ_<8slug>`.
- Optional `events.ndjson` file (may be omitted without failing acceptance).
- Documentation updates: roadmap SIM-M2 scope narrowed; README & testing strategy aligned; `metadata-manifest.md` marked LEGACY.

## Breaking / Behavioral Changes
| Area | Change | Action Required |
|------|--------|-----------------|
| Output Layout | New `runs/<runId>/` structure with per-series CSVs | Update any tooling expecting `gold.csv` root file |
| Manifest | `metadata.json` removed (replaced by `run.json` + `manifest.json`) | Shift consumers to new dual JSON structure |
| runId | New underscore-based pattern | Adjust regex in any downstream validations |

## Migration from SIM-M1
1. Replace references to `metadata.json` with `run.json` / `manifest.json`.
2. Update ingestion scripts to read `series/index.json` and iterate per-series CSVs.
3. Remove assumptions about a single `gold.csv` file.
4. Update runId validation patterns to the new underscore format.

## Artifact Pack Example
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
`series/index.json` entry (illustrative):
```json
{
  "id": "arrivals@COMP_A",
  "kind": "flow",
  "path": "series/arrivals@COMP_A.csv",
  "unit": "entities/bin",
  "componentId": "COMP_A",
  "class": "DEFAULT",
  "points": 288,
  "hash": "sha256:..."
}
```

## Determinism & Integrity
- Identical spec + seed ⇒ identical per-series CSV bytes (& hashes) across runs.
- Tamper test ensures any mutation to a series file invalidates stored hash.
- `run.json` mirrors `manifest.json` during this milestone; divergence reserved for future semantics.

## Deprecations
- `metadata.json` (SIM-M1) — superseded; legacy doc retained with LEGACY banner.
- Single aggregated `gold.csv` as canonical consumer target — superseded by per-series CSVs.

## Testing & Quality
- Integrity & determinism tests added (series hashing, tamper detection, runId pattern).
- All existing tests updated/removed to eliminate references to deprecated artifacts.

## Known Limitations
- Parquet Gold table not yet emitted (placeholder kept in contracts). 
- Events file not required; enrichment deferred.
- No service/API endpoints (HTTP) in this milestone.

## Next (Preview)
- SIM-SVC-M2: minimal HTTP service & streaming groundwork.
- Parquet Gold writer & formats disclosure.
- Divergent `run.json` vs `manifest.json` semantics (summary vs integrity-only).

## Tagging Checklist
- [ ] Merge milestone branch into `main`.
- [ ] Verify docs (README, ROADMAP, contracts, testing) reference new layout exclusively.
- [ ] Tag `sim-m2`.
- [ ] Announce deprecation timeline for removing legacy docs.

---
(End of SIM-M2 draft release notes)
