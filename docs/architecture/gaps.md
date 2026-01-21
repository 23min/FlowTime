# Architecture Gaps

This document tracks architecture gaps that are surfaced during implementation but not yet captured as epics or milestones.
It is intentionally short, factual, and forward-looking.

---

## Path Analysis / Path Filters

### Why this is a gap
FlowTime now emits edge time bins and derived sink/path latency, but there is no formal architecture for **path analysis**:

- Which paths are used (per class, per time window)?
- How much flow traverses each path?
- What are the dominant or toxic routes?
- Can we extract a subgraph that is “active” for a given class/time window?

Today, clients (UI/MCP) can inspect nodes and edges, but cannot ask for **path-level** answers without custom, client-side logic.

### Path filters vs path analysis

- **Path filters** are a *subgraph extraction* feature:
  - Given a start/end node, class, and time window, return only the nodes/edges that carry flow.
  - This is a query/selection problem and could be exposed as an API option.

- **Path analysis** is broader and deeper:
  - Path discovery, path frequency, ranking, and decomposition.
  - Dominant route identification, path changes over time, and route anomalies.
  - May require additional aggregation and/or derived outputs beyond edge time bins.

Path filters can be built from edge time bins, but they still need **well-defined query semantics**
(e.g., thresholds, class handling, time window behavior) that are not yet specified.

### Relationship to Edge-Time-Bin epic
Edge time bins provide the necessary *inputs* for path analysis, but do not define
how to aggregate or query paths. A formal epic is needed to standardize this.

### Proposed direction
Create a dedicated epic, tentatively **Path Analysis & Subgraph Queries**, to cover:

- Server-side path/subgraph query semantics (path filters).
- Path-level aggregations (counts, shares, dominant routes).
- Optional derived outputs with provenance (for MCP and UI).

This epic should be coordinated with:
- `docs/architecture/edge-time-bin/` (inputs)
- `docs/architecture/anomaly-detection/` (pathologies)
- `docs/architecture/ai/` (MCP consumption)

### Immediate implications
- MCP should remain **pass-through** for edge data in M-08.05.
- Any path filters or summary helpers should be **server-side** (authoritative),
  and should live in a follow-up epic or milestone.

---

## Summary Helpers (Edge/Path Analytics)

There is no API contract for **summary helpers** such as:

- Edge retry ratios (retryVolume / flowVolume).
- Conservation deltas at node boundaries.
- Path or route summaries.

These would be useful for MCP and UI, but require a contract that clearly labels
values as derived. This gap is best addressed alongside path analysis.

---

## Open Questions

- Should path filters be part of the time-travel API or a separate analysis endpoint?
- What thresholds/semantics define a “path” in time-binned data?
- Should derived path outputs be stored in run artifacts or computed on demand?

