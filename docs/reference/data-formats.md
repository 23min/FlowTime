# Data Formats Reference (Authoritative — Jan 2026)

**Status:** Active reference  
**Scope:** Canonical run artifacts, telemetry bundles, and export formats used by FlowTime Engine and FlowTime.Sim.  
**Note:** This document describes **shipped** formats. If something is future work, it is explicitly labeled as such.

---

## 1) Canonical Run Artifact Layout

Every run produces a deterministic artifact bundle:

```
run_<timestamp>_<slug>/
├── spec.yaml            # submitted model (verbatim, normalized line endings)
├── model/
│   ├── model.yaml       # canonicalized model
│   ├── metadata.json    # template/title/version, mode, hashes
│   └── provenance.json  # optional (when provided)
├── run.json             # summary + warnings + series list
├── manifest.json        # hashes + RNG + provenance
├── series/
│   ├── index.json
│   └── *.csv            # seriesId CSVs
└── aggregates/          # export outputs (CSV/NDJSON/Parquet); may be empty until POST /export
```

### Required JSON files
- `run.json` — `docs/schemas/run.schema.json`
- `manifest.json` — `docs/schemas/manifest.schema.json`
- `series/index.json` — `docs/schemas/series-index.schema.json`

**Schema drift note (current):**
- Engine emits `kind: "edge"` in `series/index.json`, but the schema enum does not yet include `edge`.
- `run.json` may include `inputHash`, which is not yet declared in the run schema.

These are known schema mismatches that should be reconciled in a schema update.

### Required CSV files
Per‑series CSVs live under `series/` and are referenced by `series/index.json`.

**Series CSV format (required):**
```
t,value
0,10.0
1,12.3
2,11.8
```

- `t` is the **bin index** (0‑based).
- `value` is a float (InvariantCulture).

---

## 2) Series IDs and Index Metadata

Series IDs follow the canonical pattern:

```
<measure>@<COMPONENT_ID>@<CLASS_ID>
```

Examples:
- `arrivals_hub@ARRIVALS_HUB@DEFAULT`
- `flow_latency_ms@FLOW_LATENCY_MS@DEFAULT`
- `edge_queue_to_airport_flowVolume@EDGE_QUEUE_TO_AIRPORT_FLOWVOLUME@DEFAULT`

Edge series IDs are normalized to:
```
edge_<edgeId>_<metric>@EDGE_<EDGEID>_<METRIC>@<CLASS_ID>
```

The **index** (`series/index.json`) records:
- `id`
- `kind` (`flow`, `state`, `derived`, or `edge`)
- `path` (relative `series/*.csv`)
- `unit` (e.g., `entities/bin`, `minutes`, `ms`)
- `componentId`
- `class`
- `hash` (sha256)

**Note:** Edge series are emitted into the same `series/` folder and appear in `series/index.json` as `kind: "edge"`.

---

## 3) run.json (summary + warnings)

`run.json` includes:
- `grid { bins, binSize, binUnit, timezone, align }`
- `series[] { id, path, unit }`
- `warnings[]` with codes + optional nodeId/bins/edgeIds
- `classCoverage` (when class data is present)

Schema: `docs/schemas/run.schema.json`

---

## 4) manifest.json (hashes + provenance)

`manifest.json` includes:
- `seriesHashes` (sha256 per seriesId)
- `rng { kind, seed }`
- `scenarioHash`, optional `modelHash`
- `provenance { modelId, templateId, inputHash }` when available
- Optional `classes[]` block

Schema: `docs/schemas/manifest.schema.json`

---

## 5) Edge Series (EdgeTimeBin)

Edge time‑bin metrics are emitted as series with `kind: "edge"`:

**Throughput edges**
- `flowVolume`

**Effort edges**
- `flowVolume`
- `attemptsVolume`
- `failuresVolume`
- `retryVolume` (derived)
- `retryRate` (derived)

All edge series are optional and only emitted when edge metrics exist in the run.

---

## 6) Constraint Series (Option B Dependencies)

Constraint resources are **not nodes**; they are series‑backed resources attached to services.

When present, the engine emits per‑constraint series in state responses:
- `arrivals`, `served`, `errors`, `latencyMinutes`, and derived `shortfall`.

These are returned through `/state` and `/state_window` (not as separate CSV files today).

---

## 7) Telemetry Capture Bundles (v2)

Telemetry capture bundles are defined by:
`docs/schemas/telemetry-manifest.schema.json`

Required fields:
- `schemaVersion: 2`
- `grid { bins, binSize, binUnit }`
- `supportsClassMetrics` (boolean)
- `files[]` entries:
  - `nodeId`
  - `metric` (Arrivals/Served/Errors/ExternalDemand/QueueDepth/Capacity)
  - `path` (CSV)
  - `hash` (sha256)
  - `points` (int)
  - `classId` **required** when `supportsClassMetrics: true`

When `supportsClassMetrics: true`, include:
- `classes[]`
- `classCoverage` (`full`, `partial`, `missing`)

---

## 8) Exports (Aggregates)

Exports are **optional** outputs written under `aggregates/` and served by:
`GET /v1/runs/{runId}/export/{format}`

Supported formats:
- `csv` (aggregates)
- `ndjson`
- `parquet`

**Note:** The aggregates directory is populated on demand via `POST /v1/runs/{runId}/export`.  
The `series/index.json` advertises an `aggregatesTable` path (default `aggregates/node_time_bin.parquet`), but this file may not exist unless an export was generated.

### CSV / NDJSON export schema
These exports use a **flattened “aggregates” format**:

```
time_bin,component_id,measure,value
```

This is a convenience export for BI tools and is **not** the canonical run artifact.

Export file names:
- `aggregates/export.csv`
- `aggregates/export.ndjson`
- `aggregates/export.parquet`

---

## 9) Format Selection (Guidance)

- **Human-authored**: YAML (templates, model specs)
- **Machine-generated**: JSON (run.json, manifest.json, index.json)
- **Time series**: CSV per series (canonical)
- **Aggregates/BI**: CSV/NDJSON/Parquet (exports only)

---

## 10) Out of Scope / Future Formats

These are **not** shipped yet:
- Streaming formats
- Path‑level analytics tables
- Separate edge fact tables outside of per‑series CSVs

---

## References

- `docs/reference/engine-capabilities.md`
- `docs/reference/contracts.md`
- `docs/schemas/run.schema.json`
- `docs/schemas/manifest.schema.json`
- `docs/schemas/series-index.schema.json`
- `docs/schemas/telemetry-manifest.schema.json`
