# Epic: Path Analysis & Subgraph Queries

**ID:** E-13

## 1. Summary

This epic defines **path analysis** as a first‑class capability in FlowTime: answering end‑to‑end journey questions across one or more routes in the DAG, over time, using FlowTime’s per‑bin node and edge artifacts.

Path analysis is built on EdgeTimeBin, Classes‑as‑Flows, and the stable post-E-16 fact surfaces exposed by the server. It is consumed by both the UI and MCP server. It is not a replacement for edge overlays; it is a higher‑order analysis layer.

---

## 2. Motivation

Edge time bins enable truth‑based edge overlays and conservation checks, but they do not answer questions like:

- “For the **Order** flow, what was the dominant end‑to‑end path from `Orders.Create → Billing.Settle` at 10:00–14:00, and where did it bottleneck?”
- “What fraction of volume went via path A vs path B during a surge window?”
- “Which path contributed most to backlog‑hours and SLA misses?”
- “If we reroute 15% around `Auth.Validate`, what happens to end‑to‑end latency on the Order journey?”

Path analysis introduces formal query semantics and derived outputs so these questions are answerable in a consistent, explainable way.

---

## 3. Definitions

### 3.1 Path

A **path** is a sequence of nodes/edges in the DAG. Path queries can be:

- **Explicit**: caller supplies a sequence of edges or nodes.
- **Set‑based**: “all paths from A to B,” optionally constrained (e.g., must pass through X).
- **Policy‑based**: “top K paths by volume” within a window.

### 3.2 Path Filters vs Path Analysis

- **Path Filters** are *subgraph extraction*: return only the nodes/edges that participate in a selected path or set of paths.
- **Path Analysis** includes *metrics and attribution* (dominant paths, bottlenecks, path pain, latency estimates).

Path filters are a subset of path analysis but still require defined semantics (thresholds, class handling, missing edge data behavior).

---

## 4. Scope

### In Scope

- **Path query object** definition for explicit, set‑based, and policy‑based paths.
- **Derived path metrics** that are honest and explainable using existing per‑bin artifacts.
- **Subgraph responses** suitable for UI overlays and MCP consumption.
- **Provenance metadata** for derived path metrics (origin/aggregation).

### Out of Scope (v1)

- Full process‑mining algorithms or conformance checking.
- Probabilistic end‑to‑end latency distributions.
- Cross‑run path inference at scale.

---

## 5. Path Metrics (v1)

These metrics are designed to be **derived without pretending**:

### A. Volume Split

- Per‑path volume can be computed using edge time bins.
- For explicit paths, **path flow** can be approximated by the tightest edge (min‑cut) or by the entry edge, with explicit provenance.

### B. Bottleneck Attribution

Per bin, define a binding score for each node in the path:

- `binding = 1(Q[t-1] > 0)` OR `utilization[t] ≈ 1`
- or `shortfall = max(0, arrivals[t] - capacity[t])`

Path bottleneck per bin = argmax(binding/shortfall) among nodes on the path.

### C. Path “Pain”

Sum backlog‑hours along the path:

```
pathPain = Σ_nodes Σ_t Q_node[t] * Δt
```

This is interpretable and ties directly to incident analysis.

### D. End‑to‑End Latency Estimate (v1)

A pragmatic latency estimate per bin:

```
W_path[t] ≈ Σ_i W_i[t]
W_i[t] ≈ Q_i[t] / max(ε, served_i[t]) * bin_minutes
```

This is directional and explainable. v2+ can introduce delay kernels / convolution across edges.

---

## 6. API & Data Contracts

Path analysis requires a **server‑side contract** so clients don’t compute their own semantics:

- Path query input (`from`, `to`, `classId`, constraints, window).
- Path analysis output (path list, metrics, subgraph).
- Clear provenance metadata for derived metrics.

This should be exposed as a **dedicated analysis endpoint** (preferred), not overloaded onto `/state_window`.

---

## 7. UI Integration

Path analysis enables a distinctive UI mode:

- Highlight chosen path(s).
- Edge width = flow.
- Node color = bottleneck score / SLA risk.
- Scrubber shows dominant path changes over time.

---

## 8. MCP Integration

MCP should consume **server‑provided path outputs** (authoritative), not compute path metrics itself.

---

## 9. Dependencies

- Stable post-E-16 server-provided state/graph facts and contracts
- EdgeTimeBin (edge series + quality + warnings)
- Classes as Flows (per-class edge series)
- Derived sink/path latency (v1 signal for end-to-end latency)
- Resumed Phase 3 p3c + p3b for richer diagnostics and what-if path work

---

## 10. Roadmap / Milestones (TBD)

This epic will be broken into milestones once the query contract and minimal metrics are approved.

Suggested phases:
1. Path query contract + subgraph responses on stable post-E-16 fact surfaces.
2. Volume split + bottleneck attribution (v1) once edge facts are authoritative.
3. Path pain + latency estimate (v1), then richer comparison/what-if path work after p3c + p3b.
4. UI + MCP integration; overlay-aware comparisons can layer on later.

---

## 11. Open Questions

- How should path volume be defined (min‑cut vs entry edge vs explicit normalization)?
- How to handle missing edge data or approximate edge quality?
- Where should path outputs be stored (derived on demand vs persisted in run artifacts)?
- What is the minimal API footprint that still enables UI/MCP consumption?

