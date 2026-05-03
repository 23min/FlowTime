# Engine Semantics Catalog

This catalog describes the series and warnings emitted by the engine semantics layer. It is the authoritative reference for `/state` and `/state_window` outputs and should be kept in sync with `docs/schemas/time-travel-state.schema.json`.

## Node Series Semantics (State Window)

| Series Key | Unit | Origin | Aggregation | Gating / Notes |
| --- | --- | --- | --- | --- |
| `arrivals` | count | explicit | sum | Input series; per-bin arrivals observed. |
| `served` | count | explicit | sum | Input series; per-bin completions. |
| `errors` | count | explicit | sum | Input series; per-bin errors. |
| `attempts` | count | explicit or derived | sum | May be derived from served + failures if attempts missing. |
| `failures` | count | explicit | sum | Input series; per-bin failed attempts. |
| `exhaustedFailures` | count | explicit | sum | Input series; failures after all retries. |
| `retryEcho` | count | explicit or derived | sum | Retry echo volume (used to populate retry load). |
| `retryBudgetRemaining` | count | explicit | sum | Remaining retry budget. |
| `queue` | count | explicit or derived | sum | Queue depth; origin tagged explicitly. |
| `capacity` | count | explicit | sum | Per-bin service capacity. |
| `parallelism` | count | explicit | sum | Instances/workers; scalar or series. |
| `processingTimeMsSum` | ms | explicit | sum | Total processing time across served items. |
| `servedCount` | count | explicit | sum | Number of served items used for service time. |
| `utilization` | percent | derived | unknown | Derived as served / effectiveCapacity (per bin). |
| `latencyMinutes` | min | derived | avg | Queue latency; requires queue + served + open gate. |
| `serviceTimeMs` | ms | derived | avg | Requires processingTimeMsSum + servedCount. |
| `throughputRatio` | percent | derived | unknown | Derived as served / arrivals (per bin). |
| `flowLatencyMs` | ms | derived | avg | Derived path latency for completions. |

Notes:
- `origin` for `queue` is tagged as `explicit` or `derived` depending on the bundle.
- Derived series emit warnings if required inputs are missing; values return null instead of silent fallbacks.

## Edge Series Semantics (State Window)

| Series Key | Unit | Origin | Aggregation | Gating / Notes |
| --- | --- | --- | --- | --- |
| `flowVolume` | count | explicit | sum | Throughput edge volume (served flow). |
| `attemptsVolume` | count | explicit or derived | sum | Effort edge volume (attempt load). |
| `failuresVolume` | count | explicit or derived | sum | Terminal edge volume (failures). |
| `retryVolume` | count | derived | sum | Retry load apportioned onto throughput edges when not explicit. |
| `retryRate` | percent | derived | unknown | Ratio of failures to attempts on dependency edges. |

Notes:
- Edge series are explicit when they exist in the run bundle; retry-derived edges are marked `derived`.
- Edge series metadata is emitted per edge in `/state_window`.

## Snapshot Metrics (State)

`/state` includes per-node snapshots that correspond to the latest bin. These reuse the same semantics as the window series above, but as single values in `metrics` and `derived` sections.

## Warning Families (Non-Exhaustive)

Warnings are engine-owned and should be treated as authoritative. Common families include:

- **Edge behavior violations**
  - `edge_behavior_violation_lag` (edge applies lag; model transit explicitly)

- **Flow conservation / mismatch**
  - `queue_depth_mismatch`
  - `edge_flow_mismatch_incoming`
  - `edge_flow_mismatch_outgoing`
  - `edge_class_mismatch`

- **Missing inputs for derived metrics**
  - `missing_processing_time_series`
  - `missing_served_count_series`
  - `missing_capacity_series`

- **Class coverage gaps**
  - `class_series_missing_*` (served/errors/outflow/loss)

- **Non-negative / bounds checks**
  - `arrivals_negative`, `served_negative`, `errors_negative`, `queue_negative`, etc.
  - `served_exceeds_arrivals`, `served_exceeds_capacity`

This list is intentionally representative. New warnings must be documented here when added.
