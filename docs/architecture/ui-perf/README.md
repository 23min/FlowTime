# FT-UI-PERF — Topology Input Latency & Render Loop Architecture

**Status:** Draft (proposed epic/spec)

## Problem Statement

The Time-Travel Topology page exhibits input latency during interactive operations (panning, hover, scrub/playback). Users report:

- Pointer interactions (drag/pan/hover) feel sluggish.
- The UI sometimes appears frozen while events queue, then processes many events quickly.
- Hover requires slow cursor movement to register.

These symptoms are consistent with **main-thread saturation** (long JS tasks, WASM/.NET work, or DOM/SVG paint) which causes pointer events to queue and flush later.

## Goals

1. **Input latency:** Pointer interactions (hover/pan/zoom) remain responsive under large graphs and class-heavy runs.
2. **Render stability:** Avoid re-render storms and eliminate redundant work across JS ↔ .NET ↔ DOM.
3. **Work partitioning:** Separate “scene build” from “paint-only” updates.
4. **Measurability:** Add repeatable profiling and telemetry to verify improvements.

## Non-Goals

- Rewriting the topology visual stack (Canvas + DOM overlay) from scratch.
- Perfect DAG layout or edge-routing aesthetics.
- Broad refactors outside the topology/UI performance surfaces.

## Current Architecture (Observed)

### Rendering stacks

1. **Canvas renderer (JS):** draws nodes/edges/overlays; performs hit-testing and hover/drag scheduling.
2. **DOM node overlay (Blazor):** renders one `<button>` proxy per node for accessibility and focus/selection.
3. **Inspector charts (Blazor + JS):** sparklines/horizon charts update with selection/active bin.

### Key risk pattern

Even if the JS renderer is properly throttled with `requestAnimationFrame`, the UI can still stall if the .NET side rebuilds large payloads or DOM trees at high frequency (e.g., per active bin tick).

### Related architecture notes

Data-loading strategy options are discussed separately in:

- `docs/architecture/ui-dag-loading-options.md`

This performance spec is intentionally *orthogonal* to the loading-mode decision: selective windows, bulk bundles, and WASM/shared-engine approaches all still require strict input/pain/data lane separation to avoid main-thread stalls.

In particular, if a future loading mode introduces larger payloads (bundles), the UI should plan for:

- **Streaming hydration**: progressively ingest data and render skeleton states early.
- **Off-main-thread parsing**: use a Worker for large JSON/bundle parsing to avoid input stalls.
- **Backpressure-aware recompute**: prefer latest-wins updates rather than attempting to process every intermediate bin/event.

## Root Causes / Problem Areas

### A) Main-thread blocking from long tasks

- Full-scene redraws triggered on pointermove (drag) and hover changes.
- Expensive hit-testing per hover sample (edge polyline distance checks).
- Unconditional logging in hot paths (especially when DevTools open).
- Blazor state updates and parameter sets causing large allocations and repeated LINQ projections.

### E) Loading strategy can amplify or reduce risk

- **Selective window loading (default)**: keeps network bounded, but does not prevent UI stalls if the UI recomputes per-node/per-edge derived data every tick.
- **Bulk DAG bundles**: reduce server round-trips, but can increase parse/hydration/memory costs and worsen GC churn unless hydration is streamed and off-main-thread.
- **WASM/shared engine**: avoids evaluator drift, but shifts CPU to the client; treat as tooling/what-if unless benchmarks show UX wins.

#### Practical implication

Choosing a loading strategy should be treated as a *separate axis* from input responsiveness. The primary determinant of perceived smoothness is whether the UI avoids doing large allocations or full scene rebuilds on the hot interaction paths.

### B) Missing separation between **scene build** and **paint**

Topology currently mixes:

- **Scene build**: filtering, graph lookup creation, node/edge DTO materialization, hitbox construction.
- **Paint**: redraw with new pan/zoom transform, highlight, hover, tooltip.

If scene build runs per bin tick / hover / pan, it will saturate WASM and block inputs.

### D) Concrete hot paths (from current code)

The current Topology page and canvas component exhibit a high-frequency update pattern:

- `Topology.razor` updates `selectedBin` via timeline input and playback.
- Each bin change calls `BuildNodeSparklines(...)` and `UpdateActiveMetrics(...)`, then triggers `StateHasChanged()`.
- `TopologyCanvasBase.OnParametersSet()` rebuilds `CanvasRenderRequest` on every parameter set, including:
  - grouping edges (`GroupBy(...).ToDictionary(... ToList())`),
  - materializing all node DTOs (`ToImmutableArray()`),
  - building a node dictionary (`ToDictionary()`),
  - computing outgoing totals (`Sum(...)`),
  - materializing all edge DTOs.

This effectively couples *bin selection rate* to *full render-payload rebuild rate*.

In large windows, `Topology.razor` also rebuilds per-node metric dictionaries on each bin:

- `UpdateActiveMetrics(int bin)` loops over `windowData.Nodes` and constructs a new `Dictionary<string, NodeBinMetrics>`.
- It constructs per-node `RawMetrics` dictionaries and computes derived values (success/error rates, PMF summaries, queue status) per node.

These are correct functionally, but they are too expensive to run at interactive rates (pointer + playback + hover) without careful throttling and caching.

### C) DOM and SVG side-load

- Large DOM overlay (node proxies) can incur style recalculation and event overhead.
- SVG components (e.g., inspector sparkline) can be expensive if rerendered frequently.

## Proposed Architecture (Spec)

### 1) Define the three “update lanes”

1. **Input lane (highest priority):** pointermove/wheel/pan/hover.
   - Must be RAF-coalesced.
   - Must never perform heavy allocations or interop.
2. **Paint lane (frame-bound):** canvas paint + DOM transform updates.
   - At most once per animation frame.
   - Uses cached scene geometry.
3. **Data lane (background / debounced):** metric recompute, DTO rebuild, inspector model updates.
   - Runs on timers/debounced tasks.
   - May skip intermediate updates (latest wins).

#### Applicability across loading modes

Regardless of whether data arrives via selective windows or a bulk bundle, the UI must:

- parse/hydrate in the **data lane** (prefer Worker for large JSON/bundles),
- keep input handling in the **input lane**,
- repaint in the **paint lane**.

### 2) Strict ownership rules

- **Hover detection & highlight:** JS-only.
- **Interop on hover:** only when the inspector is open *and* hover id changes.
- **Viewport persistence:** emitted on interaction end (pointerup/wheel settle), not continuously.
- **Selection changes:** propagate to .NET (click/keyboard), but avoid rebuilding DOM proxy lists unnecessarily.

### 3) Split Topology data into static vs dynamic payloads

Define two payload types sent to JS:

- **Static scene payload**: node ids, topology geometry, labels, edge routing points, per-node bounds.
  - Rebuilt only when graph structure changes (filters, classes included/excluded, full DAG toggle, topology layout changes).
- **Dynamic overlay payload**: per-node metrics for the current bin (or a small set), selected node id, hovered ids, warning highlights.
  - Updated frequently.
  - Prefer incremental updates (e.g., only changed nodes) if possible.

### 4) Scene cache and signatures

Introduce stable signatures:

- `graphSignature`: changes when topology structure/filtering changes.
- `overlaySignature`: changes when settings that alter scene style/geometry change.
- `metricsSignature`: changes when the sampled bin changes.

The renderer and the Blazor layer use signatures to decide whether to:

- rebuild scene,
- repaint only,
- or skip entirely.

### 5) Hit-testing and hover intent

- Enforce RAF-only hover evaluation.
- Keep a “latest pointer sample” and process only the most recent sample each frame.
- Add a spatial index (world-space grid) for edge hitboxes to reduce candidate edges.
- Avoid layout thrash: cache `getBoundingClientRect()` per frame (or until resize/scroll).

### 6) Blazor render hygiene

- Avoid doing heavy work in `OnParametersSet` when parameters update frequently.
- Move heavy calculations behind explicit “recompute” calls that are debounced.
- Ensure proxy DOM list rebuilds are not triggered by hover and are minimized for focus/selection.

#### Observed coupling to break

- `TopologyCanvasBase.OnParametersSet()` currently rebuilds `CanvasRenderRequest` unconditionally when `HasSourceGraph`.
- `Topology.razor` passes `ActiveBin="selectedBin"` and updates `selectedBin` frequently (timeline input + playback).

The practical change needed is to ensure **bin ticks do not force a full scene payload rebuild**.

## Work Breakdown (Candidate Milestone)

### Phase 0 — Measurement & Baseline

- Capture a Chrome Performance trace on a representative large run.
- Record:
  - input latency during pan and hover,
  - main-thread long tasks > 16ms,
  - JS ↔ .NET interop call rates,
  - top allocators (WASM GC).

### Phase 1 — Input-lane strictness

- Ensure drag and hover are RAF-coalesced (no direct draw calls per pointer event).
- Remove/guard hot-path logging.
- Cache expensive layout reads per frame.

### Phase 2 — Scene vs Paint split

- Introduce scene cache in JS.
- Recompute hitboxes/geometry only when static payload changes.
- Paint-only updates for pan/zoom/hover highlight.

### Phase 3 — .NET render hygiene

- Prevent full graph payload rebuild on frequent parameter updates (e.g., active bin).
- Convert high-frequency updates into small “dynamic overlay updates” (or JS-only updates).
- Reduce proxy rebuilds (update attributes instead of rebuilding list where possible).

#### Concrete implementation candidates

1. **Split `CanvasRenderRequest` into static and dynamic payloads**
  - Static: `Nodes` geometry, `Edges` geometry, `Viewport` bounds, `Title`.
  - Dynamic: `SelectedBin`, per-node metrics used for fill/labels, hovered/focused ids, warnings.
  - JS gets a `renderScene(scenePayload)` only when the graph/filter changes.
  - JS gets `applyOverlay(overlayDelta)` for bin changes.

2. **Stop rebuilding the full render request on bin updates**
  - In `TopologyCanvasBase.OnParametersSet()`, detect “bin-only” updates.
  - If only `ActiveBin` (and associated metric values) changed, call a smaller JS interop method (e.g., `FlowTime.TopologyCanvas.updateBin(...)`) rather than `render(...)`.

3. **Throttle/quantize bin updates during drag/playback**
  - During dial drag, update the pointer immediately, but defer heavy recompute (metrics + canvas update) to RAF or a short debounce.
  - During playback, consider updating at a capped rate (e.g., <= 30 fps effective) and/or skip intermediate bins if the UI falls behind.

4. **Reduce per-bin allocations in `UpdateActiveMetrics`**
  - Reuse dictionaries/arrays where possible (object pooling) or compute only what the canvas requires while the inspector is closed.
  - Gate expensive per-node work behind feature flags (e.g., class contribution maps only when inspector is open).

5. **Reduce per-bin inspector work**
  - When inspector is closed, avoid building inspector-only models.
  - When inspector is open, update charts at most once per frame.

### Phase 4 — Spatial index for edge hit-testing

- Implement world-space grid and query only nearby edges.
- Validate hover correctness with tests / diagnostics.

## Validation Protocol

Define a repeatable protocol:

- Use a stable run (e.g., transportation class-heavy run).
- Test scenarios:
  - idle cursor over canvas for 10s,
  - hover edges continuously for 10s,
  - pan for 10s,
  - playback scrub for 10s.
- Collect:
  - Chrome trace,
  - diagnostic CSV dumps (canvas + hover).

### Targets (initial)

- No sustained main-thread long tasks > 50ms during hover/pan.
- Hover interop calls: 0/sec when inspector closed; ≤ 1/frame when open.
- Pan redraw rate: ≤ 1/frame; pointer events do not queue visibly.

### Additional targets derived from current behavior

- Bin updates should not trigger full `render(scene)` on every tick. Target: bin changes call a lighter `updateBin`/delta method.
- Playback loop should not call expensive per-node recomputations at higher-than-frame cadence.

## Open Questions

- Should dynamic metric overlays be computed in JS or in .NET (current approach is .NET DTO materialization)?
- What is the maximum graph size we expect in Time-Travel topology?

## Appendix: Known hotspots (initial)

- Blazor: large allocations in parameter set/render paths; repeated LINQ projections.
- JS: edge hit-testing, full redraw on pointermove, logging.

