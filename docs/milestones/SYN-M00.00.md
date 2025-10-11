# SYN‑M-0 — Synthetic Adapter (Local Synthetic “Gold” Ingest)

**Goal**
Run FlowTime end‑to‑end entirely offline using small, fixed synthetic datasets (NDJSON/Parquet) that mimic the normalized “Gold event” schema. No Azure/App Insights deps. This also gives CI a stable dataset for golden tests.

## Scope

* Add a **Synthetic Adapter** that reads **events** from local files and exposes them to the existing stitching/metrics pipeline.
* Ship **two tiny sample datasets** in‑repo (≤30 MB total).
* Provide a **CLI entrypoint** to run ingest + stitch + export metrics.
* Add **tests** that assert known counts and percentiles.

Out of scope (later milestones): real telemetry adapters, streaming, large datasets, UI.

---

## Data Contract (authoritative for this adapter)

### Event record (row)

| Column       | Type                    | Notes                                   |
| ------------ | ----------------------- | --------------------------------------- |
| `entity_id`  | string (non‑empty)      | Correlation key (e.g., parcel\_id)      |
| `event_type` | string (non‑empty)      | Canonical milestone name                |
| `ts`         | timestamp (UTC)         | Event time (not ingest time)            |
| `attrs`      | JSON / map\<string,any> | Flat‑ish dims used for grouping/filters |

**Requirements**

* `ts` must be UTC ISO‑8601 in NDJSON and logical TIMESTAMP(UTC) in Parquet.
* `attrs` keys must be **lower\_snake\_case**; values: string | number | boolean.
* No PII.
* Multiple events per `(entity_id, event_type)` are allowed (re‑entries, waves).

### File formats

* **NDJSON**: one JSON object per line with exactly these fields.
* **Parquet**: schema

  ```
  entity_id: BYTE_ARRAY (UTF8)
  event_type: BYTE_ARRAY (UTF8)
  ts: INT64 (TIMESTAMP_MILLIS, UTC)
  attrs: BYTE_ARRAY (UTF8 JSON)  // or map<binary, binary> if supported end-to-end
  ```

---

## Adapter design

### Interfaces (C#)

```csharp
// Core DTO
public sealed record EventRecord(
    string EntityId,
    string EventType,
    DateTime TsUtc,
    IReadOnlyDictionary<string, object?> Attrs);

// Abstraction for event sources
public interface IEventSource
{
    IAsyncEnumerable<EventRecord> ReadAsync(string path, CancellationToken ct = default);
}

// Implementations
public sealed class NdjsonEventSource : IEventSource { /* streaming reader */ }
public sealed class ParquetEventSource : IEventSource { /* Parquet.Net reader */ }
```

**Notes**

* Use streaming APIs to avoid loading entire files into memory.
* Validate and coerce `ts` to `DateTimeKind.Utc`; reject non‑UTC.
* For `attrs`, keep numbers as `double` and integers as `long` where possible; strings otherwise.

### Wiring into FlowTime

* Add an **ingest** command in the CLI that selects the event source by `--format` and path:

  ```
  flowtime ingest \
    --format parquet \
    --events-path ./data/samples/weekday-10k/events.parquet \
    --out ./out/edges.csv
  ```
* Internally: `IEventSource.ReadAsync` → existing **edge stitching** (from/to pairing per registry) → metrics/export.

### Edge stitching expectations (unchanged)

* The adapter **does not** stitch; it only yields events.
* Stitching pairs `(from_event → to_event)` in timestamp order **per entity\_id**.
* If duplicates exist, pair the **k‑th** `from` with the **k‑th** `to` that is **≥ from.ts\`**.
* If unmatched `from` or `to` remain, log counters; do not fail the run.

---

## CLI (M-0)

Add/extend a top‑level `ingest` verb:

```
flowtime ingest
  --format (ndjson|parquet)    default: parquet
  --events-path <path>         file or folder (glob ok)
  --edges-config <path>        defaults to built-in edges.yaml
  --out <path>                 edges CSV or Parquet (optional)
  --metrics-out <path>         summary metrics JSON/CSV (optional)
  --validate-only              runs schema & invariants checks
```

Behavior:

* Reads events (streaming), stitches edges, computes basic metrics (count, P50/P95/P99 duration per edge × key dims), writes outputs.
* Exit code non‑zero if schema invalid or required dims missing for any configured edge.

---

## Samples shipped in‑repo

```
data/
  samples/
    weekday-10k/
      events.parquet
      MANIFEST.json
    black-friday-10k/
      events.parquet
      MANIFEST.json
schemas/
  gold_event.schema.json
```

**MANIFEST.json**

```json
{
  "gold_schema_version": "1.0",
  "scenario_id": "weekday",
  "seed": 123,
  "generator": "flow-sim v0.1",
  "generator_commit": "<tag-or-sha>",
  "row_count": 10000,
  "time_range_utc": ["2025-08-22T00:00:00Z","2025-08-23T00:00:00Z"]
}
```

**Size budget**: keep both samples ≤30 MB total (prefer Parquet + snappy).

---

## Validation & errors

### Schema validation (adapter)

* Ensure required columns exist with correct types.
* Ensure `ts` is UTC and monotonic **within a single entity’s event sequence** (non‑decreasing).
* Ensure `event_type` is one of the **known milestones** (loaded from `edges.yaml`’s node set).
* Ensure `attrs` contains **all dims** required by any edge that will reference them (e.g., `hub_id` for `ArrivedHub→SortedHub`). Missing dims → error unless a default is provided.

### Invariant checks (warn, don’t fail by default)

* “DeliveryAttempted” must not precede “OutForDelivery” for the same `entity_id`.
* “Delivered” must not precede “DeliveryAttempted”.
* Edge duration must be ≥ 0.
  Emit counters and a small report:

```
validation_report.json
{
  "missing_dims": { "edge:lastmile.success": 17 },
  "unmatched_from": { "edge:hub.sort": 3 },
  "unordered_events": 12
}
```

---

## Metrics (minimum set for SYN‑M-0)

* Per edge key (from/to pair) and **global** (no grouping for M-0):

  * count, throughput (events/hour)
  * duration: p50, p90, p95, p99
* Write to `--metrics-out` (CSV or JSON).
* (Optional) One dimension split to prove grouping works, e.g., `product`.

---

## Testing (CI)

* **Unit tests**:

  * NDJSON & Parquet readers parse minimal examples; timezone coercion works; `attrs` parsing stable.
* **Golden test (integration)**:

  * Load `weekday-10k` → stitch → assert:

    * total stitched edges count equals expected (document the value),
    * `origin.sort` p95 within a tolerance window (±10%),
    * no negative durations,
    * ≤X unmatched edges.
* **Validate‑only test**:

  * Run `flowtime ingest --validate-only` on both sample datasets; expect exit code 0 and a clean `validation_report.json`.

---

## Implementation notes

* **Parquet**: favor **Parquet.Net**; if JSON maps are awkward, store `attrs` as JSON string (UTF8) and parse lazily.
* **Streaming**: use `IAsyncEnumerable` + backpressure (buffered channel) so large files don’t blow memory.
* **Time**: normalize all timestamps to `DateTimeKind.Utc` immediately; reject local times.
* **Perf**: batch stitching by entity using a `Dictionary<string, List<EventRecord>>` with periodic spill if memory is tight (M-0 datasets are small; simple list is fine).
* **Logs**: summarize counts; write a compact `run_manifest.json` with timings and adapter version.
* **Determinism**: not applicable to adapter; determinism is a property of the generator. Preserve input order where provided.

---

## Documentation

* **README section**: “Run FlowTime with local synthetic data”

  ```
  dotnet run --project src/FlowTime.Cli -- ingest \
    --format parquet \
    --events-path ./data/samples/weekday-10k/events.parquet \
    --metrics-out ./out/metrics.json
  ```
* **Schema doc**: `schemas/gold_event.schema.json` plus a short Markdown explaining each field and common dims used in edges.

---

## Acceptance criteria

1. FlowTime can read **NDJSON and Parquet** synthetic event files via `ingest` CLI and complete stitching + metrics without Azure.
2. Two datasets (`weekday-10k`, `black-friday-10k`) are included and pass `--validate-only`.
3. CI runs a golden test on `weekday-10k` and asserts known counts/percentiles.
4. Adapter errors are clear (schema/dim problems point to offending rows/columns).
5. Documentation shows a 60‑second quickstart end‑to‑end.

---

## Nice‑to‑have (if time allows)

* `--events-path` accepts a **folder or glob** to concatenate multiple parts.
* Output **edges** as Parquet too (`--out-format parquet`) for speed.
* Small **profiling counters** (rows/s, parse errors, GC allocations) printed at the end.

---

With SYN‑M-0 landed, anyone can: clone repo → run one command → see stitched edges and metrics. Later, a separate **flow‑sim** project can publish bigger datasets that the same adapter consumes without any FlowTime changes.
