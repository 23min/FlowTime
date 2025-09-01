# Artifact & Contract Specification (M1 Draft)

Status: Draft (to be finalized at Contracts Parity milestone M1).

## Goals
Provide deterministic, self-describing run artifacts enabling:
- CLI ↔ API ↔ UI parity
- Reproducibility & integrity checks
- Forward-compatible schema evolution

## Directory Layout
```
runs/<runId>/
  run.json          # high-level summary & series listing
  manifest.json     # integrity + hashes + warnings
  index.json        # lightweight series discovery (UI friendly)
  series/
    <seriesId>.csv  # t,value (LF line endings, invariant floats)
```

## run.json (Summary)
```jsonc
{
  "schemaVersion": 1,
  "runId": "r-2025-09-01T12-00-00Z",
  "engineVersion": "0.1.0",
  "grid": { "bins": 288, "binMinutes": 5 },
  "modelHash": "sha256:...",          // normalized model YAML
  "scenarioHash": "sha256:...",       // model + overlay/template expansion (== modelHash if none)
  "exprDialect": "v1",                // expression semantics version
  "rngSeed": 42,
  "createdUtc": "2025-09-01T12:00:01Z",
  "series": [
    { "id": "demand",  "path": "series/demand.csv",  "kind": "flow",    "unit": "jobs/bin" },
    { "id": "served",  "path": "series/served.csv",  "kind": "flow" },
    { "id": "backlog", "path": "series/backlog.csv", "kind": "stock",   "unit": "jobs" }
  ]
}
```

## manifest.json (Integrity & Warnings)
```jsonc
{
  "schemaVersion": 1,
  "modelHash": "sha256:...",
  "scenarioHash": "sha256:...",
  "seriesHashes": { "demand": "sha256:...", "served": "sha256:..." },
  "warnings": ["pmf-normalized:demand", "latency:division-by-zero:served@t=17" ],
  "notes": ""
}
```

## index.json (Discovery)
```jsonc
{
  "schemaVersion": 1,
  "grid": { "bins": 288, "binMinutes": 5 },
  "series": [
    { "id": "demand",  "path": "series/demand.csv" },
    { "id": "served",  "path": "series/served.csv" }
  ]
}
```

## CSV Format
```
t,value
0,10
1,12.5
```
Rules:
- `t` = 0..bins-1 (integer)
- `value` = double formatted with invariant culture and a stable format specifier (e.g., G17 or trimmed fixed pattern)
- LF (`\n`) endings only

## Hashing
- Algorithm: SHA-256, lowercase hex, prefixed with `sha256:`.
- `modelHash`: canonicalized YAML (strip comments, normalize line endings, deterministic key ordering for objects if templated expansion is introduced).
- `scenarioHash`: hash of canonical model plus overlay/template expansions.
- `seriesHashes[seriesId]`: hash of raw CSV bytes (including header & newline) or body-only (choose and document; recommended: full file for simplicity).

## Determinism Requirements
- Same model + seed ⇒ identical modelHash, scenarioHash, seriesHashes.
- Reordering YAML keys must not change modelHash.
- Floating output differences across OS/CPU not permitted (format control + no locale influence).

## Warning Codes (Initial)
| Code | Meaning |
|------|---------|
| pmf-normalized:<nodeId> | PMF probabilities adjusted to sum 1.0 |
| latency:division-by-zero:<seriesId>@t=<n> | Latency computed with served=0 (value set to 0) |
| expr:deprecated-func:<name> | Deprecated function used (future) |

## JSON Schema Evolution
- Start at schemaVersion=1.
- Additive fields only until a major semantic change; bump version then.
- Backward compatibility: older consumers ignore unknown fields.

## Validation Workflow
1. Engine writes series CSVs.
2. Compute per-series hashes.
3. Write manifest.json (hashes + warnings).
4. Write run.json (summary) and index.json (discovery) referencing existing paths.
5. Test suite validates schemas + recomputes hashes.

## CLI Flags (Planned)
- `--no-manifest`: suppress manifest.json only (still emits run.json, index.json, CSVs) for ultra-fast ephemeral runs.
- `--seed <n>`: explicit RNG seed for deterministic procedural generators.

## Integrity Check (Future CLI)
`flowtime verify runs/<runId>` → recompute hashes & report mismatches.

## Open Questions
- Hash scope: include CSV header line? (Current: yes.)
- runId format: time-based vs hash-based vs incremental? (Current draft: timestamp + random suffix; deterministic mode may hash model+seed.)

---
End of draft.
