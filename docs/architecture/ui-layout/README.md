# FT-UI-LAYOUT — Pluggable Layout Motors (Client/Server Hybrid)

**Status:** Draft (proposed epic/spec)

## Problem Statement

Topology layout is currently tightly coupled to the UI rendering and the "shape" of the data we fetch.
As a result:

- Layout changes are hard to iterate on ("rewrite JS", "rewrite Blazor", etc.).
- Experiments (new layout algorithms, different routing strategies, or server-side layout) create large, risky diffs.
- Performance work (input/paint/data lane separation) is harder because layout recompute is entangled with render payload rebuilds.

We want to **split layout from rendering** so we can plug in different "layout motors" (client-side, server-side, or hybrid) without rewriting the rest of the topology stack.

## Goals

1. **Decouple concerns:** Make layout a replaceable subsystem with a clear contract.
2. **Enable multiple motors:** Support at least:
   - "UI local" layout (JS or .NET in WASM),
   - "Server precomputed" layout returned with state data,
   - and a hybrid mode (server seeds + client refinement).
3. **Cacheability:** Make layout results cacheable by signature.
4. **Performance-friendly:** Align with the UI perf architecture (scene vs overlay; input/paint/data lanes).
5. **Determinism & reproducibility:** Layout results should be traceable to a specific input signature and algorithm version.

## Non-Goals

- Designing the final "best" DAG layout algorithm.
- Rewriting the full topology renderer.
- Guaranteeing stable pixel-perfect layout across browsers.

## Glossary

- **Layout input:** The minimal graph/metadata needed to compute positions and routing.
- **Layout result:** Node positions, sizes, edge routes, and any auxiliary indices needed to render/hit-test.
- **Layout motor:** A component that implements the layout contract.
- **Scene payload:** Mostly-static geometry derived from the layout result.
- **Overlay payload:** Fast-changing per-bin metrics/highlights.

## Key Design Principle

The UI should treat layout like a *pure function* (modulo a chosen algorithm and version):

$$
\texttt{LayoutResult} = f(\texttt{LayoutInput}, \texttt{LayoutOptions}, \texttt{AlgorithmId})
$$

…and should be able to cache or request it based on a stable signature.

## Proposed Contracts

### 1) `LayoutInput`

`LayoutInput` should avoid per-bin metrics. It describes *structure and static metadata*:

- Graph: node ids, edge endpoints.
- Optional grouping hints: class lanes, subsystem boundaries, swimlanes.
- Optional fixed anchors: pinned nodes, root selection.
- Node sizing inputs (if variable): label lengths, icon type, whether the node has queue/buffer.

**No per-bin data.** Per-bin metrics belong to overlays.

### 2) `LayoutOptions`

Options that influence geometry:

- Orientation: LR/TB.
- Spacing: node separation, rank separation.
- Routing: polyline vs orthogonal; bundling on/off.
- Collapsing: hide "trivial" nodes; collapse subgraphs.
- Stability: incremental / keep previous positions where possible.

### 3) `LayoutResult`

Minimum outputs required to render:

- Node rectangles in *world coordinates*: `(x, y, width, height)`.
- Edge routes: polyline points or segments, in world coords.
- Graph bounds.
- Optional indexes:
  - a spatial index for hit-testing edges,
  - rank/layer ids,
  - bend-point caches.

  ## World coordinates vs viewport (client/server)

  When layout is computed server-side, it does **not** need (and should not depend on) the client viewport size.

  - **Layout outputs are in world coordinates.** The server returns node rectangles and edge routes in a stable coordinate system (e.g., logical pixels in an arbitrary world-space).
  - **The UI owns the viewport transform.** Pan/zoom is just a transform from world → screen:

  $$
  	exttt{screenPoint} = T_{pan,zoom} (\texttt{worldPoint})
  $$

  - **`graph bounds` exist to fit-to-view.** The UI uses `LayoutResult` bounds to compute an initial camera transform (fit, center, padding). After that, the user’s camera state is purely client-side.

  ### When viewport matters

  There are two legitimate cases where the current viewport influences *what* gets rendered, but not the underlying topology:

  1. **Level-of-detail (LOD)**: the UI can choose to omit labels, hide minor edges, or simplify stroke effects when zoomed out. This should not require recomputing layout.
  2. **Text measurement / sizing**: exact label sizes can differ by font and platform. If station sizing depends on measured text widths, prefer a **hybrid** approach:
    - server computes structure (ranks/order/routing) using conservative size estimates,
    - UI measures labels and optionally runs a small refinement pass to resolve collisions.

  If we ever introduce a mode where layout is intentionally viewport-dependent (e.g., produce a cropped layout for a minimap or a specific LOD geometry), that dependency must be explicit in `LayoutOptions` and therefore part of `layoutOptionsSignature`.

### 4) Signatures and versioning

To make results cacheable and debuggable:

- `layoutInputSignature`: hash of structure + sizing inputs.
- `layoutOptionsSignature`: hash of options.
- `layoutAlgorithmId`: stable string (`"dagre@1"`, `"elk@0.9"`, `"ft-sim-layout@2"`, etc.).
- `layoutAlgorithmVersion`: optional semantic version or git sha.

The final cache key can be:

$$
\texttt{layoutKey} = H(\texttt{layoutInputSignature}, \texttt{layoutOptionsSignature}, \texttt{layoutAlgorithmId})
$$

## Layout Motor Interfaces (Conceptual)

### UI-side

The UI should depend on an abstraction:

- `ILayoutMotor.Compute(LayoutInput, LayoutOptions) -> LayoutResult`

…but the *implementation* can be:

- JS module (fast iteration, integrates well with Canvas).
- .NET service in WASM (share code with engine, easier testing).
- Remote (HTTP) motor.

### Server-side

Add a server capability:

- `POST /layout` (or extend state endpoints to include layout results).

The server returns a `LayoutResult` plus signatures.

## Data Flow Options

### Option A — Client-computed layout (baseline)

- UI fetches topology structure.
- UI runs layout motor locally.
- UI builds scene payload and renders.

Pros: no server work; fast experimentation.
Cons: client CPU/GC; harder to keep deterministic across devices; large graphs can stall.

### Option B — Server-precomputed layout (preferred for large graphs)

- Server computes layout once per run + filter signature.
- UI fetches `LayoutResult` together with graph or state.
- UI renders scene from layout result.

Pros: removes layout CPU from UI; consistent across clients; cacheable.
Cons: server cost; need versioning; must handle UI size differences.

### Option C — Hybrid

- Server provides "seed" layout (ranks/layers + coarse positions).
- UI refines for current viewport, label measurement, or minor constraints.

Pros: stable + responsive; accommodates UI-specific sizing.
Cons: more complexity.

## Relationship to UI Performance Work

This epic is a structural enabler for `docs/architecture/ui-perf/README.md`.

- Layout becomes part of the **scene build** lane.
- Per-bin changes do not trigger layout.
- Layout results become a stable scene input that the renderer can cache.

## Open Questions

- Where do node sizes come from (server-estimated vs client-measured)?
- How are filters (classes on/off, collapse options) represented in `LayoutInputSignature`?
- Should server layout be persisted on disk per run? (likely yes, keyed by signature)
- What is the long-term "blessed" layout algorithm?

## Milestone Candidates

1. Introduce `LayoutInput`/`LayoutResult` schema (internal) and signatures.
2. Add a "no-op" layout motor wrapper around current layout approach.
3. Implement server layout endpoint or server-included layout in `/state_window`.
4. Add UI caching and a simple layout motor picker (feature flag).
5. Add tests: signature stability, layout cache correctness, and UI integration smoke.
