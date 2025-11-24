# contracts.md (shared by FlowTime & FlowTime-Sim)

**Status:** v1.0 (artifact-first, domain-neutral)  
**Applies to:** FlowTime (engine) and FlowTime-Sim (simulator)  
**Scope:** Canonical artifacts and API surfaces that read/write them. Streaming is not implemented and is intentionally omitted here.

## Vocabulary
- **entity** — the thing that moves (job/request/item)  
- **component** — processing point (node)  
- **connection** — directed link between components  
- **class** — segment/category of entities (engine today emits a single `DEFAULT` class)  
- **measure (per bin)** — flows like `arrivals`, `served`, `errors` (units: `entities/bin`)  
- **state level (per bin)** — a level at bin boundary (e.g., `backlog`, `queueDepth`) (units: `entities`)  
- **grid** — `{ bins, binSize, binUnit }`, time is UTC, alignment is left (bin index `t` maps to bin start)

## Versioning & compatibility
- `schemaVersion` governs artifact shapes. Current: **1** (required in `run.json`, `manifest.json`, `series/index.json`).
- Readers should accept `{ current, current-1 }` where practical.
- Breaking changes bump `schemaVersion`; additive fields must be tolerated.
- Engine vs Sim differences are signaled via the `source` field and series naming (`measure@component[@class]`).

## Unified Model Artifact
Both Engine and Sim consume the same model artifact shape (`docs/schemas/model.schema.yaml`):

```yaml
schemaVersion: 1
metadata:
  id: "hello"
  title: "Hello World Flow"
spec:
  grid:
    bins: 8
    binSize: 1
    binUnit: hours
  nodes:
    - id: demand
      kind: const
      values: [10,10,10,10,10,10,10,10]
    - id: served
      kind: expr
      expr: "demand * 0.8"
  outputs:
    - series: served
      as: served.csv
  rng:            # optional; used by Sim/orchestration only
    kind: pcg32
    seed: 123
```

Behavior:
- Engine ignores `rng` and is deterministic by spec; Sim/orchestrated runs honor `rng` (and may require a seed when templates declare `rng`, see RunOrchestrationService).
- Model artifacts are stored under `model/model.yaml` alongside `metadata.json`/`provenance.json` in run outputs.

## Artifact layout (per run)

```
run_<timestamp>_<slug>/
├── spec.yaml            # submitted model (verbatim, normalized line endings)
├── model/
│   ├── model.yaml       # canonicalized model
│   ├── metadata.json    # template/title/version, mode, schema, hashes
│   └── provenance.json  # optional (when provided)
├── run.json             # summary, warnings, series listing
├── manifest.json        # hashes, rng, provenance hash
├── series/
│   ├── index.json
│   └── *.csv            # measure@component[@class].csv
└── aggregates/          # created as a placeholder; table may be absent today
```

Run identifiers are opaque. Engine generates `run_<utc timestamp>_<8-char slug>` (or `run_deterministic_<hash>` when deterministic IDs are requested). Do not parse runIds for semantics.

## File contracts

### run.json
- Schema: `docs/schemas/run.schema.json`
- Fields: `schemaVersion`, `runId`, `engineVersion`, `source` (`engine`/`sim`), `grid { bins, binSize, binUnit, timezone, align }`, `modelHash` (optional), `scenarioHash`, `createdUtc`, `warnings[]`, `series[] { id, path, unit }`.
- Warnings include invariant/analyzer findings; the engine writes them here, not to stdout.

### manifest.json
- Schema: `docs/schemas/manifest.schema.json`
- Fields: `schemaVersion`, `scenarioHash`, optional `modelHash`, `rng { kind, seed }`, `seriesHashes { seriesId -> sha256 }`, `eventCount`, `createdUtc`, optional provenance reference.
- Hash values cover CSV file bytes.

### series/index.json
- Schema: `docs/schemas/series-index.schema.json`
- Fields: `schemaVersion`, `grid { bins, binSize, binUnit, timezone }`, `series[] { id, kind, path, unit, componentId, class, points, hash }`, `formats.aggregatesTable { path, dimensions, measures }`.
- `seriesId` format: `measure@componentId[@class]`; allowed chars `[A-Za-z0-9._\\-@]`.
- Aggregates table path is advertised but not emitted yet; consumers must tolerate its absence.

### Per-series CSV
- Shape: `t,value` with `t` starting at 0, LF newlines, InvariantCulture floats.

## Units & semantics
- Flows (`entities/bin`): `arrivals`, `served`, `errors`, `capacity` (rate).
- Levels (`entities`): `backlog`, `queueDepth`.
- Latency (`minutes`):
  - Engine: `latency = (backlog / max(1e-9, served)) * binSize (in minutes)`.
  - Retry/edge overlays are derived; edge fact tables are not emitted yet (EdgeTimeBin is future work).
- Classes: single `DEFAULT` class today; the schema carries `class` for forward compatibility.

## Service/API surfaces (engine)
- `POST /v1/runs` → create a run, write artifacts.
- `GET /v1/runs/{runId}/graph` → compiled DAG and semantics.
- `GET /v1/runs/{runId}/state` → bin snapshot.
- `GET /v1/runs/{runId}/state_window` → window of series (nodes/edges derived).
- `GET /v1/runs/{runId}/metrics` → aggregates/KPIs over a window.
- `GET /v1/runs/{runId}/index` → `series/index.json`.
- `GET /v1/runs/{runId}/series/{seriesId}` → CSV stream (URL-decode `seriesId`).

Sim Service exposes template/catalog endpoints for model generation and uses the same artifact schema for runs; it does not add separate run formats.

## Validation & CI gates
- JSON Schemas in `docs/schemas/`: `run.schema.json`, `manifest.schema.json`, `series-index.schema.json`, `model.schema.yaml`, `template.schema.json`, `telemetry-manifest.schema.json`.
- CI/tests:
  - Artifact schema validation to catch drift.
  - Determinism checks: same spec + seed ⇒ identical CSV hashes (`seriesHashes`).
  - Invariant/analyzer checks during artifact writing (warnings recorded in `run.json`); template analyzers run in Sim CLI and orchestration.
  - Configuration precedence tests for `FLOWTIME_DATA_DIR` (API and CLI).

## Out of scope / future (not implemented)
- Streaming contracts.
- Edge fact tables (EdgeTimeBin) and per-edge series.
- Catalog APIs or catalog.v1 schema (no catalog endpoints are shipped).

