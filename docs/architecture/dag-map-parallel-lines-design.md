# dag-map: Parallel Lines for Multi-Class Edges

> **Date:** 2026-03-24
> **Status:** Design draft
> **Context:** FlowTime edges carry multiple entity classes simultaneously. The current dag-map library colors each edge by the source node's class, losing class-level flow information. This design proposes parallel colored lines on shared segments — the way real metro maps draw multiple lines on shared track.

---

## Problem

In FlowTime, a single edge (e.g., CentralHub → HubQueue) carries flow for *all* classes simultaneously. The current dag-map approach assigns one color per edge (from the source node's `cls`), which tells you nothing about which entity types flow where.

What we want: when three classes (priority, standard, bulk) all flow through the same edge, that edge should render as three parallel colored lines — like the Central, District, and Circle lines sharing track between South Kensington and Tower Hill on the London Underground map.

## How dag-map Routes Work Today

The layout engine (layout.js Step 2) discovers routes via greedy longest-path decomposition:

1. Find the longest path in the graph → trunk (route 0)
2. Remove those nodes, find the next longest path → branch (route 1)
3. Repeat until all nodes are assigned

Routes share nodes at **interchanges** (nodes with in-degree > 1 or out-degree > 1). When two routes pass through the same interchange, their paths overlap on the shared segments. This already produces a visual similar to parallel lines, but:

- Routes are topology-derived, not class-derived
- Shared segments overlap (same Y) rather than being offset as parallel lines
- A node belongs to exactly one route (the one that "owns" it), even if multiple routes pass through it

## Proposed Design: Consumer-Provided Routes

### API Change

Allow the consumer to provide pre-computed routes instead of letting the layout engine discover them:

```javascript
const layout = layoutMetro(dag, {
  routes: [
    { id: 'priority', nodes: ['Source', 'Hub', 'LineA', 'Airport'], cls: 'priority' },
    { id: 'standard', nodes: ['Source', 'Hub', 'LineA', 'Airport', 'Downtown'], cls: 'standard' },
    { id: 'bulk',     nodes: ['Source', 'Hub', 'LineB', 'Industrial'], cls: 'bulk' },
  ],
  // ... other options
});
```

Each route is a **path through the graph** (ordered node IDs) with an associated class. Routes share nodes freely — a node can appear in many routes.

When `routes` is provided:
- Skip Step 2 (greedy longest-path extraction)
- Use the provided routes directly
- Continue with Step 3 (Y-position assignment) and beyond

When `routes` is omitted:
- Current behavior — auto-discover routes from topology. Zero breaking change.

### Parallel Line Rendering

When multiple routes share a segment (same from→to edge), they should render as parallel lines offset perpendicular to the path:

```
Before (overlapping):          After (parallel):
  A ════════════════ B          A ──────────────── B   (priority, blue)
                                A ──────────────── B   (standard, teal)
                                A ──────────────── B   (bulk, amber)
```

#### Offset Calculation

For a segment from point P to point Q:

1. Compute the perpendicular direction: `perp = normalize(Q - P) rotated 90°`
2. For N routes sharing this segment, offset each by: `perp * (i - (N-1)/2) * lineGap`
3. `lineGap` = ~3-5px at scale 1.0 (theme/option configurable)

For bezier curves, offset each control point by the same perpendicular amount. This produces parallel curves that stay parallel through bends.

For angular/progressive curves, offset each waypoint. The curves will be approximately parallel.

#### Segment Sharing Detection

Two routes "share a segment" if both contain the edge (A, B) as consecutive nodes. Build a map:

```
sharedSegments: Map<"A→B", [routeIdx, routeIdx, ...]>
```

Segments with >1 route get parallel rendering. Segments with exactly 1 route render as today.

### Node Rendering with Parallel Lines

At interchange nodes (where routes converge/diverge), the parallel lines need to merge into/out of the node:

```
    ──── A ════ B ════ C ──── D
    ──── A ════ B ════ C
                       ╘════ E
```

At node C, two lines continue to D and one diverges to E. The lines should smoothly merge/split at the node.

**Approach:** Draw each route's path independently through the full route. At shared segments, offset parallel. At divergence/convergence points, the offset transitions from parallel to centered over a short distance (one "transition segment" of ~15px).

### Station (Node) Shape

With parallel lines, interchange stations become **small pills** perpendicular to the flow — wide enough to span all parallel lines passing through them. The pill width grows with the number of lines:

```
Single route:    ●   (circle or small dot)
Two routes:      ━━  (short pill)
Three routes:    ━━━ (wider pill)
```

This happens naturally if the consumer uses `renderNode` and reads `ctx.routeCount` (new context field: how many routes pass through this node).

## Implementation Plan

### Phase 1: Consumer-provided routes (layout.js)

1. Accept `options.routes` array
2. If provided, skip Step 2 (auto-discovery)
3. Convert provided routes to internal format (same as auto-discovered: `{ nodes, lane, parentRoute, depth }`)
4. Determine parentRoute/depth by analyzing route relationships (shared prefixes, branching points)
5. Continue with Steps 3-6 as normal

**Effort:** ~4 hours. Layout change only. Rendering stays the same but now routes = classes.

### Phase 2: Parallel offset rendering (layout.js Step 5 + render.js)

1. Build `sharedSegments` map from route data
2. For each segment in each route, compute perpendicular offset based on route's position in the sharing group
3. Offset the path points before passing to the routing function (bezier/angular)
4. Add transition segments at merge/split points

**Effort:** ~6-8 hours. This is the hard part — getting smooth transitions at divergence points.

### Phase 3: Context enrichment

1. Add `ctx.routeCount` to renderNode context (how many routes pass through this node)
2. Add `ctx.routeClasses` (which route classes pass through)
3. Add route-level data attributes: `data-route-id`, `data-route-cls`

**Effort:** ~1-2 hours.

## Alternatives Considered

### A: Edges carry class arrays, renderer splits them

```javascript
edges: [['A', 'B', { classes: ['priority', 'standard'] }]]
```

**Rejected:** This puts class-flow information on edges, but the real question is "which classes flow through which path?" — that's route-level, not edge-level. You'd have to annotate every edge, and the layout engine wouldn't know how to group them into coherent visual lines.

### B: Multiple edges between the same nodes

```javascript
edges: [['A', 'B', { cls: 'priority' }], ['A', 'B', { cls: 'standard' }]]
```

**Rejected:** Creates a multigraph. The layout engine assumes a simple graph. The topo sort, route extraction, and path building all assume at most one edge per node pair.

### C: Post-hoc splitting in the renderer only

Keep layout as-is, split edges into parallel lines purely in the renderer based on edge annotations.

**Rejected:** Without layout awareness, the parallel lines would overlap with adjacent nodes and routes. The layout needs to know about parallel lines to allocate space.

## Open Questions

1. **Route ordering:** When 3 routes share a segment, which one goes on top/middle/bottom? Options: alphabetical, by volume (thickest in center), by route depth.
2. **Max parallel lines:** At what point (5? 8? 12 classes?) does this become illegible? Should we cap and group?
3. **Line thickness:** Should parallel lines be thinner than single lines? Probably yes — each line could be 2px instead of 4px, keeping the total visual weight similar.
4. **Color blending at high density:** With many parallel lines, colors become hard to distinguish. Should we switch to a different visual (bundled gradient, for example) above a threshold?
5. **Consumer-provided routes vs. class-annotated edges:** Should the library support both? The consumer-provided route approach is more explicit and flexible. The edge-annotation approach is simpler for consumers but less powerful.

## Example: FlowTime Transit Hub with 3 Classes

```javascript
const routes = [
  {
    id: 'priority',
    cls: 'priority',
    nodes: ['OriginNorth', 'CentralHub', 'HubQueue', 'LineAirport', 'Airport'],
  },
  {
    id: 'standard',
    cls: 'standard',
    nodes: ['OriginNorth', 'CentralHub', 'HubQueue', 'LineDowntown', 'Downtown'],
  },
  {
    id: 'bulk',
    cls: 'bulk',
    nodes: ['OriginSouth', 'CentralHub', 'HubQueue', 'LineIndustrial', 'Industrial'],
  },
];
```

Shared segments:
- `CentralHub → HubQueue`: all 3 routes (3 parallel lines)
- `OriginNorth → CentralHub`: priority + standard (2 parallel lines)

The trunk line would show 3 colors merging at CentralHub, running parallel through HubQueue, then diverging at the routers into separate single-color branches. Exactly like a metro map.
