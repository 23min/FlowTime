# contracts.md (shared by FlowTime & FlowTime-Sim)

> **📋 Charter Alignment**: This artifact-first contract specification directly supports the [FlowTime-Engine Charter](../flowtime-engine-charter.md) paradigm. The "artifacts are source of truth" principle here implements the charter's artifacts-centric workflow: [Models] → [Runs] → [Artifacts] → [Learn].

> **Status:** v1.0 (artifact-first, domain-neutral)
> **Applies to:** FlowTime (engine) and FlowTime-Sim (simulator)
> **Scope:** Canonical **artifacts**, **service/API** surfaces, **streaming**, and **catalog**. Readers and UIs must rely on these contracts. Services are stateless after a run completes—artifacts are the source of truth.
>
> **Charter Context**: This document defines the artifact contracts that enable the M2.7 KISS Registry and support charter workflow integration across the Engine+Sim ecosystem.

## Vocabulary (domain-neutral)

- **entity** — the thing that moves (job/request/item)
- **component** — processing point (node)
- **connection** — directed link between components
- **class** — segment/category of entities
- **measure (per bin)** — flows like `arrivals`, `served`, `errors`
- **state level (per bin)** — a level at bin boundary (e.g., `backlog`, `queue_depth`)
- **grid** — `{ binMinutes, bins }`, time is UTC, alignment is **left** (bin index `t` maps to bin start)

## Versioning & compatibility

- **schemaVersion** governs artifact shapes. Current: **1** (REQUIRED in all artifact JSON: run.json, manifest.json, series/index.json).
- Readers should accept `{ current, current-1 }` schema versions.
- Breaking changes bump schemaVersion (e.g., adding `queue_depth` in Sim v2).
- Keep **engine** vs **sim** differences explicit via the `source` field and series naming (see Units & semantics).

## Unified Model Artifact Format {#model-artifact}

Both FlowTime (Engine) and FlowTime-Sim use a unified Model artifact structure that wraps the model definition:

### Model Artifact Structure
```yaml
kind: Model
schemaVersion: 1
metadata:
  title: "Manufacturing Line Model"
  created: "2024-09-20T10:00:00Z"
  description: "Production flow with capacity constraints"  # optional
  tags: ["production", "manufacturing"]                    # optional
spec:
  # Standard model definition (same YAML both engines consume)
  grid: { bins: 2160, binMinutes: 60 }
  nodes:
    - id: demand
      kind: const
      values: [10, 10, 10, 10]
    - id: served
      kind: expr
      expr: "demand * 0.8"
  rng:                    # optional - ignored by Engine, used by Sim
    kind: pcg32
    seed: 12345
  outputs:
    - series: served
      as: served.csv
```

### Engine vs Simulator Behavior
- **FlowTime (Engine)**: Always deterministic; ignores `rng`/`seed` fields in `spec`
- **FlowTime-Sim**: Uses `rng`/`seed` from `spec` for reproducible synthetic data generation
- **Model Sharing**: Same Model artifact works for both engines - only execution behavior differs
- **Artifacts**: Both produce compatible run artifacts (same JSON schemas, CSV formats)

## Determinism rules (for hashing, parity, CI) {#hashing}

- Normalize model/spec text before hashing:

  - use LF `\n` line endings
  - trim trailing whitespace per line
  - collapse consecutive blank lines
  - ignore YAML key ordering
- CSV writers:

  - LF newlines
  - culture-invariant floats (e.g., `G17` with `InvariantCulture`)
  - no thousands separators
- Re-running the same spec + seed must yield:

  - identical per-series CSV bytes and SHA-256 hashes
  - identical `run.json`, `manifest.json`, `series/index.json` payloads, except `runId` and timestamps (unless deterministic run id is set)

---

### Schema Files

| Artifact | Schema |
|----------|--------|
| run.json | [schemas/run.schema.json](schemas/run.schema.json) |
| manifest.json | [schemas/manifest.schema.json](schemas/manifest.schema.json) |
| series/index.json | [schemas/series-index.schema.json](schemas/series-index.schema.json) |

`modelHash` MAY appear in `run.json` / `manifest.json` (engine internal aid). Tools must rely on `scenarioHash` for identity.

## Artifact contracts

### Run identifier (runId)

- Opaque string; consumers MUST NOT parse for semantics.
- Standard generation format (producer guidance): `<source>_<yyyy-MM-ddTHH-mm-ssZ>_<8-char slug>`
  - Example: `sim_2025-09-01T18-30-12Z_a1b2c3d4`
  - Regex (non-normative aid): `^(engine|sim)_\d{4}-\d{2}-\d{2}T\d{2}-\d{2}-\d{2}Z_[a-z0-9]{8}$`
- Slug: lowercase hex/alphanumeric (collision-resistant enough for CI/local workflows).
- Engine and Simulator MUST avoid introducing meaning into segments (remain opaque for future flexibility).

### Directory layout (per run)

```
runs/<runId>/
  spec.yaml                     # original submitted (or derived) simulation spec (persisted for overlay derivations)
  run.json
  manifest.json
  series/
    <seriesId>.csv
  gold/
    node_time_bin.parquet       # analytics table (CSV optional via flag)
```

> **Per-series CSVs** are the UI/adapters’ canonical inputs. The Parquet **gold** table is for analytics. Services must stream the same files over HTTP.

### run.json (summary, series listing)

```json
{
  "schemaVersion": 1,
  "runId": "sim_2025-09-01T18-30-12Z_a1b2c3d4",
  "engineVersion": "sim-0.1.0",
  "source": "sim",
  "grid": { "bins": 288, "binMinutes": 5, "timezone": "UTC", "align": "left" },
  "modelHash": "sha256:…",            // engine may populate both modelHash and scenarioHash
  "scenarioHash": "sha256:…",
  "createdUtc": "2025-09-01T18:30:12Z",
  "warnings": [],
  "series": [
    { "id":"arrivals@COMP_A", "path":"series/arrivals@COMP_A.csv", "unit":"entities/bin" },
    { "id":"served@COMP_A",   "path":"series/served@COMP_A.csv",   "unit":"entities/bin" }
  ]
}
```

- `source` is "engine" for FlowTime and "sim" for FlowTime-Sim.
- `warnings` records normalizations, coerced values (e.g., PMF renorm, divide-by-zero latency → 0), etc.

### manifest.json (determinism & integrity)

```json
{
  "schemaVersion": 1,
  "scenarioHash": "sha256:…",
  "modelHash": "sha256:…",              // optional, engine internal aid
  "rng": { "kind": "pcg32", "seed": 12345 },
  "seriesHashes": {
    "arrivals@COMP_A": "sha256:…",
    "served@COMP_A":   "sha256:…"
  },
  "createdUtc": "2025-09-01T18:30:12Z"
}
```

- Hash keys are **seriesId** and the value is the SHA-256 of the CSV **file bytes**.

### series/index.json (discovery, units, grid)

```json
{
  "schemaVersion": 1,
  "grid": { "bins": 288, "binMinutes": 5, "timezone": "UTC" },
  "series": [
    {
      "id": "arrivals@COMP_A",
      "kind": "flow",
      "path": "series/arrivals@COMP_A.csv",
      "unit": "entities/bin",
      "componentId": "COMP_A",
      "class": "DEFAULT",
      "points": 288,
      "hash": "sha256:…"
    }
  ],
  "formats": {
    "goldTable": {
      "path": "gold/node_time_bin.parquet",
      "dimensions": ["time_bin", "component_id", "class"],
      "measures": ["arrivals", "served", "errors"]
    }
  }
}
```

- **seriesId format:** `measure@componentId[@class]`
- Allowed chars in `seriesId`: `[A-Za-z0-9._\-@]`; if others are needed, URL-encode in the API path and use a filename-safe transformation for disk.
- `kind` values: `"flow" | "state" | "derived"`.
- Units:

  - flows: **`entities/bin`** (arrivals, served, errors, capacity-as-rate)
  - levels: **`entities`** (backlog, queue_depth)
  - derived latency: **`minutes`** (see Semantics)

### Per-series CSV (canonical UI input)

```
t,value    # t = 0..(bins-1), LF newlines, InvariantCulture floats
```

### Gold table (analytics, dense rows policy in v1)

- Path: `gold/node_time_bin.parquet` (CSV optional via `--csv`).
- Shape:

  ```
  time_bin (UTC start), component_id, class, arrivals, served, errors
  ```

---

## Units & semantics (engine vs sim)

- **Flows (`entities/bin`)** — `arrivals`, `served`, `errors`, `capacity` (rate)
- **Levels (`entities`)**

  - engine: `backlog` (pending level)
  - sim (schemaVersion 2): `queue_depth`
- **Latency (`minutes`)**

  - engine: `latency = (backlog / max(1e-9, served)) * binMinutes` (flow-through)
  - sim v2: `latency_est_minutes = (queue_depth / max(1e-9, capacity)) * binMinutes` (capacity-based estimate)
- Name these differently in UI to avoid confusion (e.g., “Observed latency” vs “Capacity-based latency estimate”).


---

## Service/API surfaces

> Services are **artifact views** and **run starters**. They must not keep run state in memory after completion.

### FlowTime (engine)

- `POST /run` → `{ runId }` and writes artifacts under `runs/<runId>/…`
- `GET /graph` → compiled DAG (nodes, edges)
- `GET /runs/{runId}/index` → `series/index.json`
- `GET /runs/{runId}/series/{seriesId}` → stream CSV; `seriesId` is URL-decoded
- `POST /compare` (optional/minimal) → deltas/KPIs on common series

### FlowTime-Sim (simulator)

- `POST /sim/run` → `{ simRunId }` (writes under `runs/<simRunId>/…`)
- `GET /sim/runs/{id}/index` → `series/index.json`
- `GET /sim/runs/{id}/series/{seriesId}` → CSV/Parquet passthrough
- `GET /sim/scenarios` → list presets + knobs (domain-neutral)
- `POST /sim/overlay` → `{ simRunId }` (body: `{ baseRunId, overlay: { seed?, grid?{ bins?, binMinutes? }, arrivals?{ kind?, values?[], rate?, rates?[] } } }`) — performs a shallow patch against the persisted `spec.yaml` of the base run and produces a new derived run. Precedence rules: if `rate` is set `rates` is cleared; if `rates` provided `rate` cleared.
  - Errors:
    - 400 if JSON invalid or `baseRunId` missing / overlay fails validation.
    - 404 if `baseRunId` not found or its `spec.yaml` missing.
  - Security: same ID validation rules as other endpoints (IDs restricted to `[A-Za-z0-9_-]`).
- **Catalog endpoints:**

  - `GET /sim/catalogs`, `GET /sim/catalogs/{id}`, `POST /sim/catalogs/validate`

### Optional proxy (single origin)

- FlowTime may expose `/sim/*` as a transparent proxy to Sim and write artifacts under the same `runs/` root (or a namespaced root). Parity with direct Sim endpoints is required.

---

## Streaming contract (External processors → UI / adapters)

- Transport: Server-Sent Events (SSE) or chunked streaming
- Frames:

  - series data: pre-binned time series values
  - watermark:

    ```json
    { "type":"watermark", "runId":"…", "binIndex":123, "simTime":"2025-09-01T08:00:00Z" }
    ```
  - heartbeat (optional): `{ "type":"heartbeat", "ts":"…" }`
  - end: `{ "type":"end" }`
- Behavior:

  - order-independent within a bin; snap to left-aligned grid
  - optional `?resume=<binIndex>` for replay continuity
  - **parity rule:** file snapshot at “end” must equal accumulated stream for the same `runId`/seed

---

## Catalog.v1 (for diagramming & ID stability)

```yaml
version: 1
components:
  - id: COMP_A
    label: "A"
  - id: COMP_B
    label: "B"
connections:
  - from: COMP_A
  - to: COMP_B
classes: ["DEFAULT"]
layoutHints:
  rankDir: LR
```

- `components[].id` is the stable join key to `series[].componentId` and `gold.component_id`.
- A catalog can be passed inline to `POST /sim/run` or referenced by `catalogId`.

---

## Validation & CI gates

- JSON Schema files (place in `docs/schemas/`):

  - `run.schema.json`, `manifest.schema.json`, `series-index.schema.json`
  - optionally `streaming.schema.json`, `catalog.v1.schema.json`
- Tests:

  - schema validation of emitted JSON (fail on drift)
  - determinism: same seed ⇒ identical file hashes (`seriesHashes`)
  - mass balance checks where applicable (e.g., conservation with backlog enabled)
- Doc hash guard (optional):

  - compute SHA-256 of `docs/contracts.md` in CI; fail if it diverges from expected (or compare across repos in integration CI)

---

## Deprecations

- Legacy single-file `gold.csv` is deprecated. Prefer **per-series CSVs** and the Parquet **gold** table.
- Replace any domain-colored reserved keys (e.g., `routeId`, `stepId`) with **domain-neutral** ones (`connectionId`).

