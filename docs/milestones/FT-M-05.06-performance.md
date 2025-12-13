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

## Progress to Date (2025‑12‑11)

- **Diagnostics instrumentation shipped:** HUD overlay with live hover/sec stats, manual dump, auto upload (`/v1/diagnostics/hover`). Payloads land under `/data/diagnostics/<runId>/...` and can be reset from the UI.
- **Config plumbing cleaned up:** UI appsettings now live under `wwwroot` so the WASM boot loader picks up diagnostics defaults without a `?diag=1` flag. Added hooks to disable caching via query string for profiling.
- **Edge hover dedupe / throttling implemented:** RAF-based hover scheduling, hit-test caching, and inspector debounce are in production, providing the baseline for additional work.

## Scope Update — JS-First Hover & Scrubber Responsiveness

During manual validation we still see ~1 interop/sec while moving the cursor because node hover events rely on Razor button elements rather than the canvas hit-tests. The new scope focuses on:

1. **JS-driven node hover reporting:** Move node hover/tooltip ownership into `topologyCanvas.js`, mirroring the existing edge hover callback. JS will invoke a `.NET` method with the hovered node ID + tooltip payload every frame, eliminating DOM-event lag while keeping the current buttons for accessibility/clicks.
2. **HUD UX polish:** HUD can now be collapsed to a tiny chip in the bottom-right corner, with the state persisted in `localStorage` so it stays hidden between sessions.
3. **Scrubber responsiveness (planned next):** After hover is JS-driven, make the timeline pointer and selection window jump immediately by updating `selectedBin`/CSS before rebuilding sparklines. Heavy recompute work will move to a deferred task so the pointer no longer waits ~1 s.
4. **Operational toggle resilience:** Toggling “Operational” today locks up large graphs due to synchronous recompute loops (tracked as Bug FT-M-05.06-OP1). We will defer chunking/cancellation work until hover fixes land but it remains in scope for this milestone.

We will track these additions under an extra phase below.

## Phase 4 — JS Hover Ownership & Timeline UX

1. **Node hover interop from JS**  
   - Surface a new `FlowTime.TopologyCanvas.onNodeHoverChanged` callback that fires whenever JS hit-tests detect a new node.  
   - Update `TopologyCanvas.razor.cs` to consume that callback, update focus/tooltip state, and bypass the DOM `@onmouseenter` handlers (leave them for keyboard focus).  
   - Remove duplicate work from `HoverNode/FocusNodeInternal` so hover and selection do not fight.

2. **HUD collapse preference (done)**  
   - Add “Hide” button + collapsed chip, remember the collapsed state in `localStorage`, and reset stats whenever the HUD expands.

3. **Timeline pointer immediacy (next after hover)**  
   - Split `OnBinChanged` into (a) immediate pointer/UI update and (b) background metric/sparkline recompute.  
   - Ensure playback, inspector, and persisted state still refresh once recompute finishes.
4. **Operational toggle resilience (after timeline work)**  
   - Cancel in-flight `BuildNodeSparklines`/`UpdateActiveMetrics` when filters change.  
   - Break recompute into async batches so the UI thread can repaint, avoiding the current “infinite rerender” loop.

Phase 4 is considered complete when node hover feels instantaneous (JS callback fires at frame rate), the timeline pointer/window snap immediately, and the operational toggle no longer locks the browser.
