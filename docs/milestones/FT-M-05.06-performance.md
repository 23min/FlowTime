# FT-M-05.06 — Topology Canvas Performance Sweep

## Goal

Reduce JS↔.NET churn from topology hover interactions so the FlowTime UI remains responsive even on class-heavy runs. This follows the router solidification work (FT-M-05.05) and targets hover throttling, inspector batching, and profiling.

## Motivation

- Chrome traces show repeated `pointerMove → updateHover → setHoveredEdge` invocations even when the cursor is outside the canvas, generating constant WASM traffic.
- UI feels sluggish on large runs; CPU fans spin due to unthrottled hover interop (tracked as a follow-up after router fixes).
- We need to capture before/after traces to prove the improvement.

## Phase 1 — Input Guardrails & Throttling

1. Gate hover processing on canvas bounds (skip when pointer outside `getBoundingClientRect()`).
2. Wrap hover updates in `requestAnimationFrame` so pointer events coalesce to one update per frame.
3. Avoid canvas redraw when hover state didn’t change.

## Phase 2 — JS/Interop Optimizations

1. De-duplicate `OnEdgeHoverChanged` interop calls (track last ID, avoid redundant null→null).
2. Cache hit-tests to avoid re-evaluating chip/edge bounding boxes when the cursor barely moved.
3. Debounce inspector state updates (Blazor) so the inspector rerenders at most once per frame while hovering edges.

## Phase 3 — Profiling & Validation

1. Produce Chrome Performance traces (before/after) and document them under `docs/performance/FT-M-05.06`.
2. Add instrumentation/logs (optional) counting hover interop calls to confirm reductions.
3. Ensure UI tests (existing or new Playwright smoke) still pass and hover behavior remains correct.

## Definition of Done

- JS/interop call count during hover drops significantly (target ≥50% reduction on the transportation run).
- Chrome scripting slice when cursor is idle is <1 s and fans stay quiet.
- Docs capture the profiling method/results, and performance work lands cleanly after FT-M-05.05.
