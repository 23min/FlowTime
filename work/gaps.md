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
- `work/epics/completed/edge-time-bin/` (inputs)
- `work/epics/anomaly-detection/` (pathologies)
- `work/epics/completed/ai/` (MCP consumption)

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

## Dependency Constraint Enforcement (Deferred M-10.03)

### What was planned
M-10.03 scoped MCP-side pattern enforcement: a dependency pattern selector routing user intent to Option A or Option B, rejecting unsupported patterns (feedback loops, retries), and promoting engine warnings to hard errors during MCP model generation.

### Why deferred
1. The engine review (2026-03) found that `ConstraintAllocator` has **zero callers** in the evaluation pipeline — constraints are declared in models but silently ignored at runtime. MCP-side enforcement alone doesn't fix this.
2. The sequenced plan recommends wiring `ConstraintAllocator` into `Graph.Evaluate()` (Phase 3.5) before MCP enforcement adds value.
3. Near-term priority is correctness bugs and analytical primitives (Phases 0–3 of the sequenced plan).

### When to revisit
After Phase 3.5 (runtime constraint enforcement) is complete. At that point, M-10.03 should be re-scoped to include both runtime enforcement and MCP guardrails.

### Reference
- Spec: `work/epics/E-12-dependency-constraints/M-10.03-dependency-mcp-pattern-enforcement.md`
- Review: `docs/architecture/reviews/review-sequenced-plan-2026-03.md` (Phase 3.5, Phase 4.1)

---

## dag-map Layout Quality (Svelte UI)

### Wiggly trunk in dag-map bezier layout
The main trunk path in dag-map's `layoutMetro` wobbles vertically when branches push the Y-positions around. Visible on the FlowTime "Transportation Network" model (12 nodes). The trunk should be a straight horizontal line with branches diverging above/below.

### No class differentiation from FlowTime API
The FlowTime `/v1/runs/{id}/graph` endpoint returns node `kind` (service, queue, dlq) but not flow classes. dag-map's layout engine uses `cls` to assign route colors and lane spread. Without meaningful classes, all nodes land on one route.

### Possible fixes
- dag-map: improve trunk stability in layoutMetro (prioritize trunk straightness)
- FlowTime API: expose class-to-node mapping so dag-map can assign routes
- dag-map: add heatmap mode so coloring comes from metrics, not static classes

---

## dag-map Features Needed for Svelte UI M5+

- ~~**Heatmap mode**: per-node/edge metric coloring~~ **Done** (dag-map `metrics`/`edgeMetrics`/`colorScales`)
- **Click/tap events**: callback with node ID (M5 blocker for inspector). `data-id` attributes exist; need event delegation in Svelte wrapper or library-level callback.
- **Hover tooltips**: on stations (M5 blocker for inspector)
- **Selected node highlighting**: visual state (M5 blocker for inspector)
- **Node shape differentiation**: custom shapes per node kind (service=rect, queue=diamond, dlq=triangle). Possible via `renderNode` callback.

See dag-map ROADMAP.md “Planned” sections.

---

## Svelte UI: SVG Performance at Scale

The “all dependencies” non-operational view has ~60-80 nodes and 200+ edges. SVG should handle this (est. ~600 DOM elements), but hasn't been tested. If it struggles:
- Try DOM-based metric updates (setAttribute) instead of full SVG re-render
- Consider canvas hybrid (dag-map for layout, canvas for rendering) as last resort
- Semantic zoom (dot → station → card at zoom levels) could reduce element count

---

## Client-Side Route Derivation for layoutFlow

FlowTime classes are metric dimensions, not graph routes. To use dag-map's `layoutFlow`, we need routes: `{ id, cls, nodes: [nodeIds] }`. The API provides `ByClass` on edges/nodes in state data, but no path-level query.

**Workaround:** Trace edges with non-zero `ByClass[classId].flowVolume` per class to derive approximate routes. Not authoritative — a proper Path Analysis API is needed for production.

**Status:** Not attempted yet. Needs experimentation.

---

## Router Convergence Guard (Deferred from Phase 1)

### What was planned
Add a max-iteration limit and convergence detection to `RouterAwareGraphEvaluator`, which currently performs a single re-evaluation pass.

### Why deferred
The single-pass design is correct for static router weights. A convergence guard is only needed if dynamic/expression-based router weights are introduced (e.g., "route to least-utilized path"). No current or planned models require dynamic routing. FlowTime operates on aggregated time series per bin — routing fractions are either observed (telemetry) or assumed (static weights).

### When to revisit
When dynamic routing is designed as a feature. At that point, add iteration with convergence detection and a max-iteration safety limit.

### Reference
- `src/FlowTime.Core/Routing/RouterAwareGraphEvaluator.cs`
- Phase 1 spec: `work/milestones/m-ec-p1-engineering-foundation.md`

---

## Parallelism `object?` Typing (Deferred from Phase 1)

### What was planned
Replace `NodeSemantics.Parallelism` (`object?`) with a proper discriminated union type. The loose typing exists because YAML deserialization can produce a string (file URI or node reference), a numeric scalar, or a double array.

### Why deferred
The change touches 21 files across Core, Contracts, Sim, API, and UI — a cross-cutting refactor with high risk for a foundation milestone. CUE (https://cuelang.org/) was noted as a potential future approach for model schema validation with native union type support.

### When to revisit
Addressed by E-16 m-E16-01 (Compiled Semantic References). See D-2026-04-03-007. Parallelism becomes a typed reference resolved at compile time. Close this gap after m-E16-01 completes.

### Reference
- `src/FlowTime.Core/Models/NodeSemantics.cs` (line 21)
- `src/FlowTime.Core/DataSources/SemanticLoader.cs` (ResolveParallelism method)
- Phase 1 spec: `work/milestones/m-ec-p1-engineering-foundation.md`

---

## Open Questions

- Should path filters be part of the time-travel API or a separate analysis endpoint?
- What thresholds/semantics define a “path” in time-binned data?
- Should derived path outputs be stored in run artifacts or computed on demand?
- How should node kind (service/queue/dlq) map to visual differentiation in dag-map?
- Should the Svelte UI support both operational and full graph views? What layout handles 80+ nodes?
