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

## Validation Protocol

To make the milestone verifiable in any session, we will follow the same recipe for every before/after capture:

1. **Run selection**  
   - `run_transportation-basic-classes_9a88904467fb066d93d8b60a984918685018a4c5360efaf0ae32e3456a821d10` (transportation template).  
   - Execute twice: once in `mode=full` (operationalOnly=false) and once in `mode=operational` (operationalOnly=true).
2. **Browser setup**  
   - Chrome stable, DevTools closed, diagnostics HUD enabled via `appsettings` (no `?diag` flag needed).  
   - ZoomPercent pinned at 81% for full mode and 100% for operational mode to match prior dumps.
3. **Interaction script (per mode)**  
   - Pan continuously for 30 s (drag the canvas in small circles).  
   - Hover edges/nodes for 30 s with neighbor emphasis on, moving both slowly and quickly.  
   - Toggle inspector once (open for 15 s, close for 15 s) to gather both scenarios.  
   - Trigger a diagnostics dump immediately after the minute-long run (HUD “Dump” button) and save the CSV snapshot (`data/diagnostics/.../hover-diagnostics.csv`, `canvas-diagnostics.csv`).
4. **Metrics to record**  
   - From `hover-diagnostics.csv`: `interopDispatches`, `totalDispatches`, `ratePerSecond`, `pointerQueueDrops`, `pointerEventsReceived`, `pointerEventsProcessed`, `dragFrameCount`, `dragAverageFrameMs`, `inspecterVisible`.  
   - From `canvas-diagnostics.csv`: `avgDrawMs`, `maxDrawMs`, `frameCount`, `panDistance`, `zoomEvents`.
5. **Acceptance thresholds**  
   - Hover interop rate ≤ 0.5 dispatches/s when inspector is closed; ≤ 2 dispatches/s when inspector is open.  
   - Pointer queue drops ≤ 5% of pointer events.  
   - `avgDrawMs` ≤ 6 ms and `maxDrawMs` ≤ 12 ms while panning.  
   - Drag frame count/avg frame ms indicates ≤ 6 ms per drag frame.  
   - Manual “feel” check: no perceptible pause > 100 ms between releasing the mouse and hover resuming.
6. **Documentation**  
   - Attach before/after CSV excerpts plus HUD screenshots to `docs/performance/FT-M-05.06/README.md`.  
   - Note Chrome version, commit hash, and whether inspector was open or closed.

## Design Constraints

To prevent ambiguity during implementation:

1. **Scene rebuild vs. paint**  
   - *Rebuild* = regenerating node/edge hitboxes, nodeMap, chip metadata, edge polyline caches, and any derived layout structures. Rebuilds occur only when `payloadSignature`, overlay settings, or zoom scale changes the world geometry (e.g., new data, toggling class filters). Panning, hovering, or minor zoom deltas must not trigger rebuilds.  
   - *Paint* = drawing using cached structures plus hover/selection/tooltip overlays. Paints run per RAF tick and may update cursor, highlights, tooltips, HUD counters.
2. **Spatial index**  
   - World-space uniform grid (e.g., 256×256 world units per cell) keyed by edge ID + bounding box; rebuild only when edge geometry changes (payload replace, overlay toggled), not per draw.  
   - Hit-test pipeline: locate pointer in world coords → find overlapping cells → test only edges in those cells via polyline distance. Expected candidate count `k ≪ E` (observed ≤ 8 on transportation run).  
   - Cache last hit result if pointer stayed within `HOVER_CACHE_WORLD_EPSILON` to avoid re-querying the index.
3. **Interop rules**  
   - Node/edge hover events never call `.NET` when the inspector is hidden. JS still drives hover visuals in all cases.  
   - When the inspector is visible, `.NET` callbacks fire only when the canonical ID changes (case-insensitive compare matching the C# dictionaries).  
   - Viewport change notifications are debounced: fire once on drag end and wheel idle (≥ 100 ms with no new wheel events).  
   - JS tooltip updates stay responsive regardless of inspector state; Blazor components consume hover state asynchronously.
4. **Layout reads/writes**  
   - Cache `getBoundingClientRect()` per RAF tick; invalidate on resize, scroll, or when canvas CSS transforms change.  
   - DOM mutations (e.g., `.style.transform` for overlays) are batched once per frame to avoid interleaving read/write cycles.
5. **Testing expectations**  
   - Add a Playwright smoke (can live under `tests/FlowTime.UI.Tests` or a new perf harness) that simulates drag + hover and asserts the diagnostics HUD increments at ≤ 1 update/frame.  
   - Add a unit/integration check ensuring `OnNodeHoverChanged`/`OnEdgeHoverChanged` are not invoked for null→null transitions (can be a simple JS interop spy in bUnit/Playwright).

## Scope Refresh — Main-Thread Latency Remediation (2025‑12‑14)

Manual testing plus a fresh audit highlighted additional hot spots that still create the “freeze, then flush” effect. These are now part of FT‑M‑05.06 so the milestone reflects the real work needed to keep the canvas responsive:

1. **Strip hot-path logging**  
   - Remove `console.info` and other logging from `draw`, `render`, viewport emitters, and hover/drag loops.  
   - Add a guarded `debugLog` helper (reads from `state.debugEnabled`) so deep troubleshooting stays possible without paying the perf cost in production.

2. **RAF-only drag/hover processing**  
   - Pointermove while dragging must only schedule work via `requestAnimationFrame`; intermediate events should overwrite the pending snapshot so only the latest position renders.  
   - Hover updates follow the same pattern so high polling rates no longer enqueue dozens of hit-tests per frame.

3. **Avoid full redraws for hover-only changes**  
   - Split “scene rebuild” from “paint” so hover state tweaks (node/edge highlight, tooltip) don’t rebuild hitboxes or node maps.  
   - Short-circuit `draw` when `hasHoverVisualDelta` is false.

4. **Faster edge hit-tests**  
   - Introduce a coarse spatial index (grid or quadtree) for edge hitboxes so hover doesn’t check every edge each time.  
   - Cache distance computations when the pointer moves less than a world-space epsilon.

5. **Interop throttling & layout hygiene**  
   - Keep `.NET` callbacks suppressed unless the inspector is visible *and* the ID actually changed.  
   - Cache `getBoundingClientRect()` values per RAF tick and reuse until scroll/resize.  
   - Debounce viewport-change notifications so we only notify .NET at the end of drags/wheel bursts.

Deliverable: update the milestone tracker to include this scope, capture before/after diagnostics (HUD + CSV), and describe the remediation steps in the FT‑M‑05.06 summary when we wrap.
