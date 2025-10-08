Here’s a **Gold-first Time-Travel Architecture Plan** that rewrites (and tightens) your current plan to reflect the “Gold owns the facts; Engine stays pure” approach—**while still supporting sim-only models**. It’s structured so you can lift sections directly into milestone tickets.

---

# FlowTime Gold-First Time-Travel Architecture Plan

**Date:** October 8, 2025
**Goal:** Ship a working, topology-aware time-travel demo in **5 incremental steps**, optimized for **Gold snapshots** but fully supporting **model-only** runs.
**Constraint:** Each step is TDD-complete and shippable in ~1 day.

## Design Principles (unchanged + clarified)

1. **Incremental:** Every priority level works end-to-end.
2. **KISS:** Minimal additions, maximum leverage of existing PMF/expr engine.
3. **Series-first (Engine Purity):** Engine evaluates `const|expr|pmf` DAG only; **no telemetry logic** inside.
4. **Two-repo coordination:** `flowtime-sim` emits models; **Gold Adapter** generates models from Gold.
5. **Fail-fast validation:** Strict schema checks at all boundaries; clear error surfaces.
6. **UTC discipline:** Absolute timestamps, fixed bins, **end-exclusive** windows.
7. **Grid ergonomics:** `binUnit` + `binSize` are SoT; **engine exposes `binMinutes`** everywhere.

   * `binMinutes = binSize × toMinutes(binUnit)` (integer).
   * Clients do `ts = window.start + (idx × binMinutes)` and Little’s Law math safely.
8. **Gold-first posture:** ADX Materialized Views compute **descriptive, stable, non-parametric** facts once. Engine computes **stateful / counterfactual** behaviors.
9. **Dual-mode:** One API supports both **Gold snapshot** and **Model-only** runs. Same outputs, same UI.

---

# Architecture Overview

**Ingress (two paths)**

* **Gold snapshot mode** → *GoldToModel Adapter* queries **NodeTimeBin/EdgeTimeBin** for requested window; generates **model.yaml** with `const *_gold` series + topology semantics.
* **Model-only mode** → engine receives **model.yaml** from `flowtime-sim` or hand-authored.

**Core Engine**

* Parse model → **TimeGrid** (with `startUtc`, `binMinutes`) → build **Series DAG** → evaluate → write **artifacts**.
* Engine computes only deterministic spreadsheet-like expressions (`MIN/MAX/SHIFT/...`) and PMFs (optional RNG).

**API**

* `/v1/runs` (create run from model or gold request)
* `/v1/graph` (topology + grid + series DAG)
* `/v1/state?ts=` (single-bin snapshot by UTC)
* `/v1/state_window?start=&end=` (dense window slice; derived metrics)
* `/v1/metrics?start=&end=` (SLA aggregates for flows)

**UI**

* Graph view with colored nodes (SLA/utilization), scrubber for time-travel, sparklines.

**Rationale**

* Keep **engine pure** → easy to test and evolve.
* Treat **Gold MV** as truth for “what-was” → fast, cheap, reproducible.
* **Snapshot Runs** freeze moving MVs into immutable artifacts for consistent time travel across teams.

---

# Data Contracts & Schemas (no code)

## 1) Gold Tables (source of truth)

### 1.1 NodeTimeBin (MV base = 5m recommended)

**Grain:** `(flow, node, ts)` where `ts` is **bin start UTC**.
**Columns (required):**

* `ts: datetime` — bin start, UTC
* `node: string` — logical node key (matches Catalog)
* `flow: string` — class or “*”
* `arrivals: long` — count in bin
* `served: long` — count in bin
* `errors: long` — count in bin

**Columns (recommended):**

* `queue_depth: real` — avg depth in bin or snapshot-based estimator
* `oldest_age_s: real` — max age observed
* `capacity_proxy: real` — avg observed capacity indicator
* `replicas: real` — avg replica count
* `latency_p50_ms: real`, `latency_p95_ms: real` — from exits
* `dlq_count: long`, `stuck_flag: bool`

**Provenance (always):**

* `schema_version: string`
* `adapter_version: string`
* `source_window_start: datetime`, `source_window_end: datetime`
* `extraction_ts: datetime`
* `known_data_gaps: string` (JSON)

**Rationale:** UI and engine can consume a single, tidy, dense table for “what-was.” Provenance enables audit.

### 1.2 EdgeTimeBin (optional)

**Grain:** `(flow, from_node, to_node, ts)`
**Columns:** `routed_count`, `hop_latency_p95_ms`, `errors`, provenance as above.
**Rationale:** Validate and visualize routing; trend QA.

### 1.3 FlowInstanceIndex (optional for drill-through)

Per-entity path summaries: `entity_id, flow, start_ts, end_ts, status, path_hash, hop_count, duration_ms`.
**Rationale:** Deep analysis and “stuck” entity lists without recomputation.

### 1.4 Catalog_Nodes

**Columns:** `node_id, flow, kind{service|queue|router|external}, group, ui_x, ui_y`
**Rationale:** Single source for topology layout & kinds.

---

## 2) Engine Model Schema (Target; both modes)

*(Based on your latest schema; deltas emphasize Gold mapping. No code—shape only.)*

### 2.1 Top level

```yaml
schemaVersion: 1
modelFormat: "1.1"        # optional but recommended
window:
  start: "YYYY-MM-DDThh:mm:ssZ"  # bin 0 timestamp (UTC)
  timezone: "UTC"
grid:
  bins: <int>             # H
  binSize: <int>          # magnitude
  binUnit: "minutes|hours|days|weeks"
# engine derives:
# grid.binMinutes: int
```

**Rationale:** Makes bin math simple; required for time travel UI.

### 2.2 Topology + Classes

```yaml
classes: ["*"]            # placeholder; multi-class later
topology:
  nodes:
    - id: "OrderService"
      kind: "service"     # service|queue|router|external
      group: "Orders"
      ui: { x: 120, y: 260 }
      semantics:
        arrivals: "orders_arrivals_*"      # maps to series id
        served:   "orders_served_*"
        capacity: "orders_capacity_*"      # proxy or modeled
        errors:   "orders_errors_*"
        queue:    null                     # for service (queues only)
        sla_min:  5.0
    - id: "OrderQueue"
      kind: "queue"
      group: "Orders"
      ui: { x: 340, y: 260 }
      semantics:
        arrivals: "queue_inflow_*"
        served:   "queue_outflow_*"
        queue:    "queue_backlog_*"        # queue nodes require ‘queue’
        oldest_age_s: "queue_oldest_age_*" # optional
  edges:
    - { id: "e1", from: "OrderService:out", to: "OrderQueue:in" }
```

> `*_gold` vs modeled ids: in **Gold mode**, semantics point to `*_gold` `const` series. In **Model mode**, they point to modeled series.

### 2.3 Series Nodes

* `const` — arrays of H numbers (Gold columns or manual).
* `expr` — formulas using `+, -, *, /, MIN, MAX, ABS, SQRT, POW, SHIFT(series, n)`
* `pmf` — `{ values[], probabilities[] }` with optional `rng`.

**Stateful queue pattern (expr only):**

* `Q[t] = MAX(0, SHIFT(Q,1) + inflow[t] − capacity[t])`
* `served[t] = MIN(SHIFT(Q,1) + inflow[t], capacity[t])`

**Rationale:** No new node kinds needed; keeps engine surface area stable.

---

## 3) Gold Snapshot Request (ingress schema)

### 3.1 POST /v1/runs (Gold mode)

```json
{
  "runKind": "gold-snapshot",
  "window": {
    "start": "2025-10-07T00:00:00Z",
    "bins": 288,
    "binSize": 5,
    "binUnit": "minutes"
  },
  "selection": {
    "flows": ["*"],
    "nodes": ["Ingress", "Queue.A"]
  },
  "topology": { "...": "optional override or from Catalog_Nodes" },
  "provenance": {
    "request_id": "uuid",
    "purpose": "investigation|demo|report"
  }
}
```

**Adapter Output → model.yaml (conceptual):**

* `window/grid` from request;
* `nodes:` one `const` series per selected Gold column, per (node, flow):
  `Ingress_arrivals_gold`, `Ingress_served_gold`, `QueueA_queue_depth_gold`, etc.
* `topology.semantics` wired to `*_gold`.
* (Optional) `expr` nodes for derived helpers (utilization, LL latency for queues when needed).

**Rationale:** Engine receives the **same model shape** as sim-only runs.

### 3.2 POST /v1/runs (Model mode)

* Body is **your model.yaml/json** (as in your original plan).
* `runKind` may be `"model"` or omitted.

---

# Priority Roadmap (Gold-first)

| Pri    | Name                     | Duration | Deliverable                                          | Validates                         |
| ------ | ------------------------ | -------- | ---------------------------------------------------- | --------------------------------- |
| **P0** | Gold Snapshot Foundation | 1 day    | Gold→Model adapter + Run snapshotting + `binMinutes` | Gold-first ingress + immutability |
| **P1** | Time Travel APIs         | 1 day    | `/state` & `/state_window` (dense, aligned)          | Scrub time over frozen Run        |
| **P2** | SLA & Utilization        | 1 day    | Derived metrics, coloring, `/metrics`                | SLA dashboards & node coloring    |
| **P3** | Routing & Conservation   | 1 day    | Routing via expr + residual checks                   | Flow correctness & QA             |
| **P4** | Demo (Gold + Sim)        | 1 day    | End-to-end scenario (baseline Gold; optional sim)    | Ship-ready                        |

**Total:** 5 days, with a working system at each checkpoint.

---

# P0 — Gold Snapshot Foundation

## P0.1: Contracts & Validation

* **Gold slice** must be **dense** (every bin present). If gaps exist, adapter **zero-fills** by joining against the requested `Grid(start, end, binMinutes)`.
* **UTC** only; **end-exclusive** window `[start, end)`.
* **Topology** from Catalog_Nodes by default; request may override.
* **Semantics** must resolve to series ids present in `nodes[]`.
* **Engine response** always includes `grid.binMinutes` and `window.start/end`.

**Decision point rationale:**

* Freezing a Gold slice as a **Run** avoids moving-target MVs, enabling consistent time-travel and reproducible investigations.

## P0.2: `/v1/runs` (create)

**Input:** Gold snapshot request (above) or full model.
**Output (common):**

```json
{
  "runId": "run_20251008T102500Z_abcd",
  "status": "completed",
  "mode": "gold" | "model",
  "grid": { "bins": 288, "binSize": 5, "binUnit": "minutes", "binMinutes": 5 },
  "window": { "start": "2025-10-07T00:00:00Z", "end": "2025-10-07T24:00:00Z", "timezone": "UTC" },
  "topology": { "nodes": [...], "edges": [...] },
  "provenance": { "source_window_start": "...", "source_window_end": "...", "adapter_version": "..." }
}
```

**Success criteria**

* Gold request produces a model with `*_gold` const series of length `bins`.
* Engine artifacts written; `/v1/graph` returns `grid.binMinutes` and `window`.

**Tests**

* Accept Gold requests with/without topology override.
* Reject non-UTC, misaligned windows, or unresolved semantics.
* Back-compat: model-only body still works.

---

# P1 — Time Travel APIs

## P1.1: `/v1/graph`

Returns:

* `grid` with `binMinutes`;
* `window` `{ start, end, timezone }`;
* `topology` `{ nodes(kind, ui, semantics), edges }`;
* `series` DAG (for debug).

**Rationale:** UI can render graph and place scrubber with no additional queries.

## P1.2: `/v1/state?ts=...`

**Rules:**

* `ts` must be **bin start** within `[window.start, window.end)`.
* Alignment tolerance: 1s; return canonical bin start.

**Response (conceptual):**

```json
{
  "runId": "...",
  "mode": "gold" | "model",
  "grid": { "binMinutes": 5, ... },
  "window": { ... },
  "bin": { "index": 42, "startUtc": "...", "endUtc": "..." },
  "nodes": {
    "OrderService": {
      "kind": "service",
      "observed": { "arrivals": 150, "served": 145, "errors": 2, "capacity_proxy": 200 },
      "modeled":  { /* present only if scenario/model series exist */ },
      "utilization": 0.725,    // derived once here
      "color": "yellow"        // rule-based for UI ergonomics
    },
    "OrderQueue": {
      "kind": "queue",
      "observed": { "arrivals": 145, "served": 140, "queue": 8, "oldest_age_s": 120 },
      "latency_min": 0.057     // derive via LL only when queue+served present (or use observed percentile if you prefer)
    }
  }
}
```

**Rationale:** Single-bin payload standardized for UI coloring and quick details; no UI math duplication.

## P1.3: `/v1/state_window?start=...&end=...`

* **Range semantics:** `[start, end)`; both must be bin starts.
* **Returns arrays** (timestamps + per-metric vectors) **per topology node** for the slice.

**Success criteria**

* Accurate index mapping and bin alignment validation.
* Dense arrays of expected length `(end-start)/binMinutes`.

**Tests**

* Misaligned timestamps → `400` with `nearestBinStart`.
* Out-of-window → `400`.
* Mode field correctly set.

---

# P2 — SLA & Utilization

## P2.1: Derived metrics (standardized)

* **Utilization:** `served / max(ε, capacity_proxy)` when both present.
* **Latency (queues only):**

  * If **observed percentile** exists and you want “what-was” purity → use `latency_p50_ms`/`p95_ms` from Gold in `observed`.
  * If **modeled** view needed (counterfactual) → provide `latency_min = queue / max(ε, served) * binMinutes` in `modeled`.

**Rationale:** Keep “observed” vs “modeled” explicit; avoid conflating heuristics with facts.

## P2.2: Coloring rules (suggested defaults, included in API for UI ergonomics)

* **SLA color (queues):**

  * green: `latency <= sla_min`
  * yellow: `latency <= 1.5 × sla_min`
  * red: otherwise
* **Utilization color (services):**

  * green: `< 0.7`
  * yellow: `0.7–0.9`
  * red: `>= 0.9`

**Rationale:** Having canonical bands in API keeps UI thin and consistent.

## P2.3: `/v1/metrics?start=&end=`

Aggregates per flow and per node (queues) across the slice:

```json
{
  "flows": {
    "Orders": {
      "bins_total": 24,
      "bins_meeting_sla": 23,
      "sla_pct": 95.8,
      "worst_latency_min": 6.2,
      "avg_latency_min": 1.4,
      "total_errors": 12
    }
  }
}
```

**Rules:**

* For flows, take worst (max) `latency_min` across queue nodes; `sla_pct` computed from bins meeting SLA.
* Include `grid` and window bounds in response.

**Success criteria**

* Aggregates match hand-checks on fixture data.
* Works for both Gold-only runs and model-only runs.

---

# P3 — Routing & Conservation

## P3.1: Routing via expressions (no new node kind)

Examples:

* `to_A = lb_arrivals * 0.6`
* `to_B = lb_arrivals * 0.4`
  Optional lints: warn if sibling splits from same parent don’t sum to ~1.0.

**Rationale:** Keep engine surface area small; policy experimentation remains expressions.

## P3.2: Conservation checks (queues)

Compute residual per bin:
`residual[t] = arrivals[t] − served[t] − (Q[t] − Q[t−1])`
Expose in `/state` (per node) and log warnings in `run.json` when `|residual| > tolerance`.

**Rationale:** Data quality and model wiring QA; useful even in pure Gold mode.

---

# P4 — Demo (Gold baseline + optional sim overlay)

## P4.1: Demo dataset

* Window: 1 day of 5-minute bins (288 bins).
* Nodes: `Ingress (service) → Queue.A (queue) → Billing (service)`
* Gold contains a demand spike causing saturation and queue build-up.

## P4.2: Demo run(s)

* **Run A (Gold snapshot):** semantics point to `*_gold`.
* **Run B (Model overlay, optional):** duplicate model; add `expr` that doubles capacity; derive `modeled` latency & compare visually.

**Success criteria**

* `/state` at spike bin shows red queue node; `/state_window` shows build-up & drain.
* `/metrics` reflects SLA dip during spike window.
* UI scrubs smoothly and colors update predictably.

---

# Why each part exists (decision rationales)

* **Gold MVs (NodeTimeBin/EdgeTimeBin):** cheap, accurate, re-usable “what-was” facts; keeps engine small and transparent.
* **Run snapshotting:** prevents demo drift and analytical “heisenbugs” from late arrivals; enables caching and access control.
* **Topology-aware API:** makes time travel **graph-native**; avoids re-implementing mapping logic or KQL in UI.
* **`binMinutes` everywhere:** one scalar to kill unit bugs and float drift across clients; makes LL and rates trivial.
* **Observed vs Modeled separation:** keeps truth vs hypothesis clear; enables immediate what-if without schema churn.
* **Expressions for queues/routing:** preserves a single engine model; fewer node kinds, easier to test and reason about.
* **Conservation residuals:** built-in data QA and wiring correctness check; invaluable during onboarding of new flows.

---

# Milestone Checklists (copy/paste to issues)

## P0 — Gold Snapshot Foundation

* [ ] Define **Gold selection schema** for `/v1/runs (gold-snapshot)` (flows, nodes, window).
* [ ] Adapter: produce **dense vectors** from Gold: one `const *_gold` series per metric.
* [ ] Build model.yaml with `window/grid/topology/nodes`; wire semantics to `*_gold`.
* [ ] Engine: ensure all responses include `grid.binMinutes`.
* [ ] `/v1/graph`: return `grid`, `window`, `topology`, `series`.
* [ ] TDD: window UTC & alignment; unresolved semantics; dense bin length; `binMinutes` presence.

## P1 — Time Travel APIs

* [ ] `/v1/state`: bin alignment validation; canonical bin start in response.
* [ ] `/v1/state_window`: `[start,end)`; arrays length `(end-start)/binMinutes`.
* [ ] Return **observed** for Gold; **modeled** only when present.
* [ ] TDD: misaligned timestamps, out-of-window, dense arrays, mode flag.

## P2 — SLA & Utilization

* [ ] Derive `utilization` when `served` and `capacity_proxy` exist.
* [ ] Queue `latency_min`: use **observed** percentile if desired (what-was) **or** LL for modeled (what-if).
* [ ] Color bands in API (SLA & utilization).
* [ ] `/v1/metrics`: SLA aggregates per flow; include `grid` and window.
* [ ] TDD: aggregates; coloring thresholds; queues-only latency derivation.

## P3 — Routing & Conservation

* [ ] Expression patterns for splits; optional ratio lint warning.
* [ ] Conservation residuals exposed + tolerance logging.
* [ ] TDD: residual math over sequences; routing sum warnings.

## P4 — Demo

* [ ] Prepare demo Gold slice with a clear spike.
* [ ] Run snapshot A (Gold).
* [ ] Optional overlay run B (modeled capacity ↑).
* [ ] Validate `/state` (spike) and `/state_window` (recovery), `/metrics` (SLA dip).
* [ ] Demo README with example requests.

---

# Dual-Mode Support (Gold vs Model)

**Same API / UI.** Differences only in ingress and which semantics are wired.

| Aspect           | Gold Snapshot                            | Model-only                             |      |               |
| ---------------- | ---------------------------------------- | -------------------------------------- | ---- | ------------- |
| Ingress          | `/v1/runs` (gold-snapshot) + adapter     | `/v1/runs` with model.yaml             |      |               |
| Series           | `const *_gold` from Gold                 | `const                                 | expr | pmf` authored |
| Observed/Modeled | **observed** populated; modeled optional | **modeled** populated; observed absent |      |               |
| Time travel      | Frozen Run artifacts                     | Same                                   |      |               |
| Metrics          | From observed (or modeled if chosen)     | From modeled                           |      |               |

**Rationale:** You can ship “what-was” today; flip to scenarios tomorrow without UI or API churn.

---

# Example Shapes (no code)

## `/v1/graph` (both modes)

```json
{
  "runId": "run_...",
  "mode": "gold",
  "grid": { "bins": 288, "binSize": 5, "binUnit": "minutes", "binMinutes": 5 },
  "window": { "start": "2025-10-07T00:00:00Z", "end": "2025-10-08T00:00:00Z", "timezone": "UTC" },
  "topology": {
    "nodes": [
      { "id": "Ingress", "kind": "service", "group": "Orders",
        "ui": { "x": 120, "y": 260 },
        "semantics": { "arrivals": "Ingress_arrivals_gold", "served": "Ingress_served_gold",
                       "capacity": "Ingress_capacity_proxy_gold", "sla_min": 5.0 } },
      { "id": "Queue.A", "kind": "queue", "group": "Orders",
        "ui": { "x": 340, "y": 260 },
        "semantics": { "arrivals": "QA_in_gold", "served": "QA_out_gold", "queue": "QA_depth_gold" } }
    ],
    "edges": [ { "id": "e1", "from": "Ingress:out", "to": "Queue.A:in" } ]
  },
  "series": { "nodes": ["Ingress_arrivals_gold", "Ingress_served_gold", "QA_depth_gold"], "order": ["..."] }
}
```

## `/v1/state?ts=...`

```json
{
  "runId": "run_...",
  "mode": "gold",
  "grid": { "binMinutes": 5 },
  "window": { "start": "...", "end": "...", "timezone": "UTC" },
  "bin": { "index": 50, "startUtc": "2025-10-07T04:10:00Z", "endUtc": "2025-10-07T04:15:00Z" },
  "nodes": {
    "Ingress": {
      "kind": "service",
      "observed": { "arrivals": 300, "served": 150, "errors": 2, "capacity_proxy": 150 },
      "utilization": 1.0,
      "color": "red"
    },
    "Queue.A": {
      "kind": "queue",
      "observed": { "arrivals": 150, "served": 120, "queue": 30 },
      "latency_min": 1.25,               // if LL derived for display; else include observed p50/p95
      "color": "red"
    }
  }
}
```

## `/v1/state_window?start=&end=`

Dense arrays per node with timestamps, plus optional aggregates flag (later).

## `/v1/metrics?start=&end=`

As in your plan—`sla_pct`, `bins_meeting_sla`, `worst_latency_min`, etc., per flow and (optionally) per node.

---

# Risks & Mitigations

* **Bin alignment / UTC edge cases:** enforce end-exclusive `[start,end)`; 1s tolerance; comprehensive tests.
* **Sparse Gold bins:** adapter fills gaps via dense grid; provenance flags `known_data_gaps`.
* **Conflating observed & modeled latencies:** keep them separate in payload; choose explicitly per view.
* **Performance of large windows:** cache Run artifacts; stream slices; consider Parquet artifacts post-P4.

---

# Future (post-P4)

* **Scenario Gold export** (optional): write modeled slices with `scenario_id` to a “View Gold” table for BI.
* **Multi-class flows:** extend `classes` and per-class vectors.
* **Retry/delay kernels, autoscale policies, finite buffers + DLQ:** all as new expression patterns or node kinds without changing API.

---

## TL;DR (what changes from your previous plan)

* **P0 now includes the Gold→Model adapter** and Run snapshotting; engine stays pure.
* We **separate observed vs modeled** fields in API (same shapes, clearer semantics).
* **Latency derivation rules**: queues get LL in modeled; services don’t get latency; can surface observed percentiles from Gold.
* **Conservation residuals** remain; applied to queues across both modes.
* **Everything still supports sim-only**: same endpoints, same topology semantics, just without `*_gold`.

This is ready to break into milestone issues.
