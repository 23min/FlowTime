# contracts.md (shared by FlowTime & FlowTime-Sim)

> **Status:** v1.0 (artifact-first, domain-neutral)
> **Applies to:** FlowTime (engine) and FlowTime-Sim (simulator)
> **Scope:** Canonical **artifacts**, **service/API** surfaces, **streaming**, and **catalog**. Readers and UIs must rely on these contracts. Services are stateless after a run completes—artifacts are the source of truth.

## Vocabulary (domain-neutral)

* **entity** — the thing that moves (job/request/item)
* **component** — processing point (node)
* **connection** — directed link between components
* **class** — segment/category of entities
* **measure (per bin)** — flows like `arrivals`, `served`, `errors`
* **state level (per bin)** — a level at bin boundary (e.g., `backlog`, `queue_depth`)
* **grid** — `{ binMinutes, bins }`, time is UTC, alignment is **left** (bin index `t` maps to bin start)

## Versioning & compatibility

* **schemaVersion** governs artifact shapes. Current: **1**.
* Readers should accept `{ current, current-1 }` schema versions.
* Breaking changes bump schemaVersion (e.g., adding `queue_depth` in Sim v2).
* Keep **engine** vs **sim** differences explicit via the `source` field and series naming (see Units & semantics).

## Determinism rules (for hashing, parity, CI)

* Normalize model/spec text before hashing:

  * use LF `\n` line endings
  * trim trailing whitespace per line
  * collapse consecutive blank lines
  * ignore YAML key ordering
* CSV writers:

  * LF newlines
  * culture-invariant floats (e.g., `G17` with `InvariantCulture`)
  * no thousands separators
* Re-running the same spec + seed must yield:

  * identical per-series CSV bytes and SHA-256 hashes
  * identical `run.json`, `manifest.json`, `series/index.json` payloads, except `runId` and timestamps (unless deterministic run id is set)

---

## Artifact contracts

### Directory layout (per run)

```
runs/<runId>/
  run.json
  manifest.json
  series/
    <seriesId>.csv
  gold/
    node_time_bin.parquet       # analytics table (CSV optional via flag)
  events.ndjson                 # optional; used by streaming and audits
```

> **Per-series CSVs** are the UI/adapters’ canonical inputs. The Parquet **gold** table is for analytics. Services must stream the same files over HTTP.

### run.json (summary, series listing, reserved event fields)

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
  ],
  "events": {
    "schemaVersion": 0,
    "fieldsReserved": [
      "entityType","eventType","componentId","connectionId",
      "class","simTime","wallTime","correlationId","attrs"
    ]
  }
}
```

* `source` is `"engine"` for FlowTime and `"sim"` for FlowTime-Sim.
* `warnings` records normalizations, coerced values (e.g., PMF renorm, divide-by-zero latency → 0), etc.

### manifest.json (determinism & integrity)

```json
{
  "schemaVersion": 1,
  "scenarioHash": "sha256:…",
  "rng": { "kind": "pcg32", "seed": 12345 },
  "seriesHashes": {
    "arrivals@COMP_A": "sha256:…",
    "served@COMP_A":   "sha256:…"
  },
  "eventCount": 0,
  "createdUtc": "2025-09-01T18:30:12Z"
}
```

* Hash keys are **seriesId** and the value is the SHA-256 of the CSV **file bytes**.

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

* **seriesId format:** `measure@componentId[@class]`
* Allowed chars in `seriesId`: `[A-Za-z0-9._\-@]`; if others are needed, URL-encode in the API path and use a filename-safe transformation for disk.
* `kind` values: `"flow" | "state" | "derived"`.
* Units:

  * flows: **`entities/bin`** (arrivals, served, errors, capacity-as-rate)
  * levels: **`entities`** (backlog, queue\_depth)
  * derived latency: **`minutes`** (see Semantics)

### Per-series CSV (canonical UI input)

```
t,value    # t = 0..(bins-1), LF newlines, InvariantCulture floats
```

### Gold table (analytics, dense rows policy in v1)

* Path: `gold/node_time_bin.parquet` (CSV optional via `--csv`).
* Shape:

  ```
  time_bin (UTC start), component_id, class, arrivals, served, errors
  ```

### Events (optional file, used by streaming & audits)

* Path: `events.ndjson`
* Each line is an object with reserved keys:

```json
{
  "entityId":"E-10023",
  "entityType":"entity",
  "eventType":"enter|exit|error",
  "componentId":"COMP_A",
  "connectionId":"COMP_A->COMP_B",
  "class":"DEFAULT",
  "simTime":"2025-09-01T08:00:00Z",
  "wallTime":"2025-09-01T08:00:00Z",
  "correlationId":"E-10023",
  "attrs":{"attempt":1}
}
```

---

## Units & semantics (engine vs sim)

* **Flows (`entities/bin`)** — `arrivals`, `served`, `errors`, `capacity` (rate)
* **Levels (`entities`)**

  * engine: `backlog` (pending level)
  * sim (schemaVersion 2): `queue_depth`
* **Latency (`minutes`)**

  * engine: `latency = (backlog / max(1e-9, served)) * binMinutes` (flow-through)
  * sim v2: `latency_est_minutes = (queue_depth / max(1e-9, capacity)) * binMinutes` (capacity-based estimate)
* Name these differently in UI to avoid confusion (e.g., “Observed latency” vs “Capacity-based latency estimate”).

---

## Service/API surfaces

> Services are **artifact views** and **run starters**. They must not keep run state in memory after completion.

### FlowTime (engine)

* `POST /run` → `{ runId }` and writes artifacts under `runs/<runId>/…`
* `GET /graph` → compiled DAG (nodes, edges)
* `GET /runs/{runId}/index` → `series/index.json`
* `GET /runs/{runId}/series/{seriesId}` → stream CSV; `seriesId` is URL-decoded
* `POST /compare` (optional/minimal) → deltas/KPIs on common series

### FlowTime-Sim (simulator)

* `POST /sim/run` → `{ simRunId }` (writes under `runs/<simRunId>/…`)
* `GET /sim/runs/{id}/index` → `series/index.json`
* `GET /sim/runs/{id}/series/{seriesId}` → CSV/Parquet passthrough
* `GET /sim/scenarios` → list presets + knobs (domain-neutral)
* `POST /sim/overlay` → `{ baseRunId, overlaySpec }` → new `simRunId`
* **Catalog endpoints:**

  * `GET /sim/catalogs`, `GET /sim/catalogs/{id}`, `POST /sim/catalogs/validate`

### Optional proxy (single origin)

* FlowTime may expose `/sim/*` as a transparent proxy to Sim and write artifacts under the same `runs/` root (or a namespaced root). Parity with direct Sim endpoints is required.

---

## Streaming contract (Sim → UI / adapters)

* Transport: Server-Sent Events (SSE) or chunked NDJSON
* Frames:

  * event: same shape as `events.ndjson`
  * watermark:

    ```json
    { "type":"watermark", "runId":"…", "binIndex":123, "simTime":"2025-09-01T08:00:00Z" }
    ```
  * heartbeat (optional): `{ "type":"heartbeat", "ts":"…" }`
  * end: `{ "type":"end" }`
* Behavior:

  * order-independent within a bin; snap to left-aligned grid
  * optional `?resume=<binIndex>` for replay continuity
  * **parity rule:** file snapshot at “end” must equal accumulated stream for the same `runId`/seed

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

* `components[].id` is the stable join key to `series[].componentId` and `gold.component_id`.
* A catalog can be passed inline to `POST /sim/run` or referenced by `catalogId`.

---

## Validation & CI gates

* JSON Schema files (place in `docs/schemas/`):

  * `run.schema.json`, `manifest.schema.json`, `series-index.schema.json`
  * optionally `streaming.schema.json`, `catalog.v1.schema.json`
* Tests:

  * schema validation of emitted JSON (fail on drift)
  * determinism: same seed ⇒ identical file hashes (`seriesHashes`)
  * mass balance checks where applicable (e.g., conservation with backlog enabled)
* Doc hash guard (optional):

  * compute SHA-256 of `docs/contracts.md` in CI; fail if it diverges from expected (or compare across repos in integration CI)

---

## Deprecations

* Legacy single-file `gold.csv` is deprecated. Prefer **per-series CSVs** and the Parquet **gold** table.
* Replace any domain-colored reserved keys (e.g., `routeId`, `stepId`) with **domain-neutral** ones (`connectionId`).
