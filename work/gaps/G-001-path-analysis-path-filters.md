---
id: G-001
title: Path Analysis / Path Filters
status: open
---

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
- `work/epics/completed/edge-time-bin/` (inputs)
- `work/epics/anomaly-detection/` (pathologies)
- `work/epics/completed/ai/` (MCP consumption)

### Immediate implications
- MCP should remain **pass-through** for edge data in M-08.05.
- Any path filters or summary helpers should be **server-side** (authoritative),
  and should live in a follow-up epic or milestone.

---
