# EdgeTimeBin Architecture

## 1. Summary

This document proposes **EdgeTimeBin** as a first‑class quantitative primitive for FlowTime, layered on top of the existing node‑centric time‑travel architecture and the **Classes as Flows** epic.

Today, FlowTime:

- Models **nodes** with time‑binned metrics (arrivals, served, errors, queueDepth, capacity, flowLatencyMs, retry semantics).
- Represents edges in topology and uses **node‑derived** heuristics to color/size edges.
- Supports retry governance, DLQ, and terminal edges.

EdgeTimeBin elevates edges from purely structural topology elements to **quantitative fact carriers**:

```text
(edgeId, timeBin, [classId]) → { flowVolume, retries, errors, ... }
```

EdgeTimeBins:

- Make **routing** a measured quantity, not an assumption.
- Disambiguate multi‑parent and multi‑child situations.
- Make retry loops, DLQ flows, and terminal behavior explicit per edge.
- Enable stronger conservation and debugging.
- Unlock path‑level and process‑mining‑style analytics.
- Provide a truthful basis for edge overlays in the UI.

This epic is larger than the classes epic and depends on it:

- Classes define **what** is flowing (flows).
- EdgeTimeBin defines **where flows go** (edges), optionally broken down by class.

---

## 2. Motivation

FlowTime aims to be a **flow‑centric digital twin**, not just a node‑local metrics system.

Nodes give you:

- Utilization.
- Queue dynamics.
- Node‑local contributions to latency.
- Retry/DLQ volumes per node.

But node‑only data cannot answer:

- How much of node B’s arrivals came from which parent (multi‑parent).
- How a node’s served volume is distributed across multiple children over time.
- The volume and timing of flows along retries / DLQ edges.
- Empirical routing matrices (`p(edge | node, class, time)`).
- Path frequencies and toxic routes.

EdgeTimeBin fills this gap by:

- Attaching time‑binned **flow metrics to edges**.
- Keeping them consistent with node metrics via conservation.
- Providing per‑class breakdowns where available.

This aligns with:

- The whitepaper’s vision of flow‑centric modeling.
- Time‑travel’s focus on gold time‑binned state.
- The Loop: simulation and telemetry runs must share the same edge‑aware contract.

---

## 3. Definition

An **EdgeTimeBin** is a time‑binned metric record for a single edge:

```json
{
  "edgeId": "process-to-out",
  "timeBin": "2025-11-23T10:00:00Z",
  "flowVolume": 133,
  "byClass": {
    "Order": 118,
    "Refund": 15
  },
  "retries": 5,
  "errors": 2
}
```

Analogy:

- Node fact table (already present):
  - `(nodeId, timeBin, [classId]) → { arrivals, served, errors, queueDepth, ... }`
- Edge fact table (proposed):
  - `(edgeId, timeBin, [classId]) → { flowVolume, retries, errors, ... }`

EdgeTimeBins are **optional but highly recommended**:

- When present, they drive edge overlays and path analytics.
- When absent, the system falls back to node‑derived heuristics with explicit fidelity warnings.

### 3.1 Semantics and Conservation

- Edges are **semantics-free carriers of metrics**. All behavior (capacity, schedule, routing decisions, retries) remains owned by nodes (including `serviceWithBuffer`), not by edges.
- For each node $u$, class $k$, bin $t$, and outgoing edge $e = (u \to v)$, the edge flow $\text{edgeFlow}_{e,k}(t)$ is defined as the portion of $\text{served}_{u,k}(t)$ that is routed to $v$ in that bin.
- Conservation at the node/edge boundary is expressed as:

  $$
  \sum_{e \in \text{Out}(u)} \text{edgeFlow}_{e,k}(t)
    = \text{served}_{u,k}(t)
      - \text{loss}_{u,k}(t)
      - \text{routedToNull}_{u,k}(t)
  $$

  for each node $u$, class $k$, and bin $t$, where `loss` and `routedToNull` represent modeled drops or routing to terminal sinks.

- For bins where a `serviceWithBuffer` node has its schedule gate $G_t = 0$, outgoing edge flows from that node are zero: edges respect node‑level schedules and capacities; they do not re‑interpret or override them.

---

## 4. Scope

### 4.1 In Scope

- Extending state and telemetry schema to include EdgeTimeBins.
- Engine support for emitting **per‑edge** metrics for simulation runs.
- Time‑travel `/state` and `/state_window` support for edge data.
- Soft conservation analyzers tying node and edge metrics together.
- UI edge overlays that use EdgeTimeBins when available.
- TelemetryLoader contract extensions for per‑edge aggregates where real systems can support them.

### 4.2 Out of Scope

- Full process‑mining algorithms (complex path discovery, conformance checking).
- Mandatory per‑edge telemetry ingestion from all domains.
- Re‑implementing core retry logic to be edge‑native in one step.
- Large‑scale persistence/query changes; the initial implementation stays within the existing run artifact layout.

### 4.3 Non-Goals (v1)

- **No edge-level behavior**
  - Edges do not own capacity, queues, schedules, delays, or filters.
  - If behavior is needed between two nodes, model it as an explicit node (often a `serviceWithBuffer`) with edges on either side.
- **No new time model**
  - Edge metrics are recorded on the same fixed time grid as node metrics; there are no per-edge event queues or DES-style timing.
- **No path-level aggregation (with a bounded exception)**
  - This epic focuses on per-edge, per-bin, per-class flows.
  - Full path-level analytics (multi-edge routes, conformance checks, toxic route analysis) remain deferred to the **Anomaly & Pathology Detection** epic (`work/epics/anomaly-detection/`).
- **Exception:** A near-term milestone (M-07.05) introduces a *derived sink/path latency* series using edge flows and node delays. This is a single derived metric, not a full path-mining system.

---

## 5. Architecture Overview

### 5.1 State Schema Extensions

The time‑travel state schema gains a new `edges` section alongside `nodes`.

Per bin:

```jsonc
{
  "timeBin": "2025-11-23T10:00:00Z",
  "nodes": {
    "process": {
      "arrivals": 120,
      "served": 118,
      "errors": 2,
      "queueDepth": 5,
      "byClass": {
        "Order": { "arrivals": 95, "served": 94, "errors": 1 },
        "Refund": { "arrivals": 25, "served": 24, "errors": 1 }
      }
    },
    "outQueue": {
      "arrivals": 118,
      "queueDepth": 118
    }
  },
  "edges": {
    "process-to-out": {
      "flowVolume": 118,
      "errors": 0,
      "retries": 0,
      "byClass": {
        "Order": 94,
        "Refund": 24
      }
    },
    "retry-loop": {
      "flowVolume": 5,
      "retries": 5,
      "errors": 0
    }
  }
}
```

Notes:

- `nodes` remains backward compatible.
- `edges` is additive; consumers can ignore it if they don’t support EdgeTimeBin.
- `byClass` under edges aligns with **classes as flows** so you can do per‑flow edge overlays.

### 5.2 Canonical Run Artifacts

Run artifacts remain in the same **canonical layout** described in `run-provenance.md` and time‑travel docs:

```text
run_<timestamp>/
  model/
    model.yaml
    metadata.json
    provenance.json
  series/
    index.json
    node_*.csv        # existing node series
    edge_*.csv        # NEW: edge series
  run.json
  manifest.json
```

- `series/index.json` is extended to include:
  - Edge series identifiers.
  - Hashes for edge CSVs.
- Edge CSVs are in the same shape as node CSVs but with `edgeId` instead of `nodeId`, and metrics appropriate to edges (e.g., `flowVolume`, `errors`, `retries`).

This **preserves** the single‑registry principle (Engine owns all artifacts) and integrates with existing provenance design.

---

## 6. Conservation and Validation

EdgeTimeBins enable stronger, multi‑level conservation checks.

### 6.1 Node–Edge Conservation

For each node `N` and bin `t`:

- Node arrivals should match the sum of incoming edge flows:

  ```text
  arrivals_N(t) ≈ Σ_{e in In(N)} flowVolume_e(t)
  ```

- Node served should match the sum of outgoing edge flows:

  ```text
  served_N(t) ≈ Σ_{e in Out(N)} flowVolume_e(t)
  ```

Per class:

```text
arrivalsByClass_N(c, t) ≈ Σ_{e in In(N)} flowByClass_e(c, t)
servedByClass_N(c, t) ≈ Σ_{e in Out(N)} flowByClass_e(c, t)
```

These become **soft validators**:

- Initial epic: log discrepancies; surface warnings in `/v1/runs` and `/state`.
- Later: make them stricter as telemetry quality improves.

### 6.4 Analyzer & UX Hooks (Edge-Level)

- **FR-ETB-A1 — Edge/Node Conservation Check**
  - For each node, class, and bin, analyzers must verify that the sum of outgoing edge flows $\text{edgeFlow}_{e,k}(t)$ matches $\text{served}_{u,k}(t)$ minus any modeled loss or routing to null, within a configured tolerance.
  - Deviations beyond tolerance should be surfaced as warnings and made available to `/state` and `/v1/runs` so authors can debug modeling or telemetry issues.
- **FR-ETB-A2 — Edge Heat & Policy Context**
  - Analyzer/diagnostic output should make it easy for UIs to pair edge throughput/attempt metrics with nearby node policies (e.g., `serviceWithBuffer` capacity, dispatch schedules), so "cold" or "hot" edges can be explained in terms of upstream/downstream node behavior.

### 6.2 Queue Conservation via Edges

Queue depth updates can be expressed purely in terms of edge flows:

```text
queueDepth_N(t) = queueDepth_N(t-1)
  + Σ_{e in In(N)} flowVolume_e(t)
  - Σ_{e in Out(N)} flowVolume_e(t)
  - errors_N(t)
```

This is more robust than node‑only conservation, especially when arrivals/served are not directly measured in telemetry.

### 6.3 Cut‑Based Conservation

For any subgraph `S` with boundary edges:

- Total inflow across the cut ≈ total outflow plus accumulation plus discard.

This supports:

- Localized debugging: “the leak is on edge E12 at 14:05”.
- Better drift detection as telemetry quality evolves.

---

## 7. Simulation vs Telemetry

### 7.1 Simulation

For synthetic runs:

- The engine already knows, per event:
  - Which edge it traversed.
  - Which class it belongs to.
- EdgeTimeBins for simulation are:

  - Cheap to compute.
  - Deterministic for gold fixtures.
  - Fully aligned with node metrics by design.

Implementation:

- When stepping the simulation, accumulate:
  - `flowVolume[e, t]++` for each traversed edge.
  - `flowByClass[e, c, t]++` when class is known.
  - Retry and error counts per edge.

Synthetic telemetry capture:

- Writes both **node** and **edge** aggregates into bundles.
- These become the **gold reference** for what real telemetry should emulate.

### 7.2 Telemetry

Real systems may not provide per‑edge telemetry for all domains:

- Some environments can derive edges from traces (spans with upstream/downstream services).
- Others only have node metrics.

TelemetryLoader must be **generous** but explicit:

- Where real telemetry **can** provide EdgeTimeBins:
  - Ingest `(edgeId, timeBin, classId)` metrics and pass them as canonical edge CSVs.
- Where it cannot:
  - Leave edge metrics empty or approximate.
  - Attach warnings and data‑quality flags:
    - `missingEdgeFlows`
    - `approxEdgesFromNodes`
    - `partialClassOnEdges`

Engine’s analyzers:

- Use edge metrics when available (exact mode).
- Fall back to node‑derived heuristics with explicit `quality: approx` flags when not.
- Preserve node metrics as the minimum viable contract.

---

## 8. UI & Visualization

EdgeTimeBin supports more truthful and powerful visualizations, building on per‑class node views:

### 8.1 Edge Overlays

When EdgeTimeBins are present:

- **Edge thickness**:
  - Proportional to `flowVolume` or `flowByClass[selectedClass]`.
- **Edge color**:
  - Encodes error rate or retry intensity.
- **Tooltips**:
  - Show per‑edge, per‑class volumes and failure/retry stats.

For class‑filtered views:

- Selected class drives both node and edge overlays.
- Edges that have zero volume for the class can be dimmed.

When EdgeTimeBins are absent:

- UI falls back to current node‑derived overlays:
  - Clear indicator: “edge metrics approximated from node metrics; per‑edge values may be inaccurate.”

### 8.2 Path and Flow Views (Incremental)

Initial epic focus:

- Improve **edge overlays** and **multi‑parent clarity** for flows.
- Provide simple path views using model topology + edge flows.

Future epics (post EdgeTimeBin foundations):

- Per‑class path frequency histograms.
- Path‑based KPIs (latency, error rate).
- “Which flows end in DLQ?” views.
- Comparisons between baseline and scenario runs at path level.

---

## 9. Risks, Gaps, and Interactions

### 9.1 Performance and Cardinality

EdgeTimeBin can significantly increase series count:

- Roughly (#edges × #classes × #bins).
- For large graphs and many classes, this can be heavy.

Mitigations:

- Allow **edge selection/filtering** in `/state`:
  - e.g., `/state_edges?edgeIds=...`, or filters by node, kind, or label.
- Support sparse encoding:
  - Only include edges with non‑zero flow in given windows.
- Start with small/medium fixtures and heavily instrumented systems; document limits.

### 9.2 Telemetry Feasibility

Not all domains can provide accurate EdgeTimeBins:

- Some systems have only aggregate per‑service metrics.
- Process‑mining from logs/traces can be complex.

Mitigation:

- Make EdgeTimeBins **optional** in telemetry contracts.
- Classify EdgeTimeBin telemetry sources:
  - Exact: direct per‑edge events or spans.
  - Approximate: inferred from node metrics.
  - Unavailable: no edge data.

UI and analyzers must respect these distinctions and surface fidelity explicitly.

### 9.3 Interaction with Classes Epic

EdgeTimeBin depends on:

- Classes being defined and used as flow types.
- Node metrics already being per‑class where needed.

Risk:

- Implementing EdgeTimeBin before classes is fully wired would force retrofits and ad‑hoc per‑class edge representations.

Mitigation:

- Complete the **Classes as Flows** epic first.
- Define per‑class node metrics and telemetry contract for nodes.
- Then introduce per‑class edge metrics as an extension.

### 9.4 Interaction with Expression Extensions

Expression extensions may later allow:

- Class‑aware routing expressions.
- Label‑aware behaviors.

Risk:

- Tight coupling between expression evaluation and EdgeTimeBin production could complicate optimization and caching.

Mitigation:

- Keep EdgeTimeBin emission as a **post‑fact aggregation**:
  - Engine’s internal state uses whatever routing logic exists.
  - EdgeTimeBins are aggregated from events, not from re‑interpreting expressions outside the simulation.

---

## 10. Incremental Adoption Plan / Milestones

EdgeTimeBin foundations are planned as 7.x engine milestones:

- **M-07.01** - Schema + Artifacts + Simulation (Foundations)
  - Extend state schema and run artifacts for edges.
  - Emit EdgeTimeBins for simulation runs.
  - Add golden fixtures with edge data.

- **M-07.02** - Node-Edge Coherence Analyzers
  - Implement soft conservation checks tying node and edge metrics.
  - Surface discrepancies as warnings, not errors.
  - Add explicit edge quality signals (exact/approx/missing).

- **M-07.03** - UI Edge Overlays (Feature-flagged)
  - Switch overlays to use EdgeTimeBins when available, with fallback indicators.
  - Gradually expand to retries/DLQ and error overlays.

- **M-07.04** - Edge Semantics Contract
  - Define explicit edge series semantics for flow + retry volumes.
  - Align edge quality signals and provenance for UI and API consumers.

- **M-07.05** - Edge-Derived Path Latency
  - Derive sink/path latency from edge flows and node delay contributions.
  - Surface derived latency with provenance metadata.

- **M-07.06** - Transit Node Modeling (Remove Edge Lag)
  - Replace edge lag with explicit transit nodes in templates.
  - Add tests to enforce lag-free first-party templates.

- **Telemetry Ingestion Epic** - Edge telemetry extensions
  - Per-edge telemetry ingestion is handled under `work/epics/telemetry-ingestion/`.
  - Edge quality flags remain the contract for approximate or missing edge data.

- **Future Work** - Path-Level Views
  - Add path analytics and richer per-flow visualizations on top of solid EdgeTimeBin foundations.

---

## 11. Summary

EdgeTimeBin is the **edge counterpart** to node metrics in FlowTime’s time‑travel architecture:

- Nodes + classes: **who is under load, and for what flows?**
- Edges + classes: **how do those flows move, and where do they succeed/fail?**

By implementing EdgeTimeBin as a separate epic after **Classes as Flows**, FlowTime can:

- Preserve backward compatibility.
- Build on a solid class‑aware foundation.
- Introduce powerful edge‑truthful views and path analytics without over‑committing telemetry ingestion requirements for all domains.

The Loop, the whitepaper, and the time‑travel status docs all remain consistent:

- Simulation and telemetry share a single, gold contract.
- EdgeTimeBin is optional but highly leveraged where available.
- Data‑quality and fidelity are surfaced explicitly, not hidden.
