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

## Phase 4 — JS Hover Ownership & Timeline UX

_Status (2025‑12‑17): ✅ Complete — HUD toggle, JS hover ownership, scrubber gating, and operational reload resiliency are in place; remaining perf polish moves to the follow-up UI-perf epic._

1. **Node hover interop from JS**  
   - Surface a new `FlowTime.TopologyCanvas.onNodeHoverChanged` callback that fires whenever JS hit-tests detect a new node.  
   - Update `TopologyCanvas.razor.cs` to consume that callback, update focus/tooltip state, and bypass the DOM `@onmouseenter` handlers (leave them for keyboard focus).  
   - Remove duplicate work from `HoverNode/FocusNodeInternal` so hover and selection do not fight.

2. **HUD collapse preference (done)**  
   - Add “Hide” button + collapsed chip, remember the collapsed state in `localStorage`, and reset stats whenever the HUD expands.

3. **Timeline pointer immediacy**  
   - Split `OnBinChanged` into (a) immediate pointer/UI update and (b) background metric/sparkline recompute.  
   - During dial drags we now cache the latest requested bin and flush it once the drag ends, so scrubber motion stays instant even on large graphs. Playback/inspector still refresh once the recompute finishes.
4. **Operational toggle resilience**  
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
   - The inspector no longer supports a pinned state—selecting a node opens it, and any background click or completed drag will close it—so keep the cursor over a node while gathering the “inspector open” samples.
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

### Captured Samples (2025‑12‑16)

- **Transportation, mode=full, inspector open** — Interaction script executed as described above (pan 30 s, hover 30 s, inspector opened mid-run).  
  - Hover CSV: `docs/performance/FT-M-05.06/FT-M-05.06-after-full-hover-inspector-open-hover.csv`  
  - Canvas CSV: `docs/performance/FT-M-05.06/FT-M-05.06-after-full-hover-inspector-open-canvas.csv`  
  - HUD dump: `docs/performance/FT-M-05.06/FT-M-05.06-after-full-hover-inspector-open.json`  
  - Highlights: `interopDispatches=52`, `totalDispatches=58`, `ratePerSecond=1.02`, `pointerQueueDrops=7`, `dragAverageFrameMs=5.477 ms`, `dragMaxFrameMs=10.9 ms`, `avgDrawMs=5.368 ms`.
- **Transportation, mode=operational (operationalOnly=true), inspector closed** — Same interaction script (pan 30 s, hover 30 s) but kept the inspector collapsed for the entire capture.  
  - Hover CSV: `docs/performance/FT-M-05.06/FT-M-05.06-after-operational-hover-inspector-closed-hover.csv`  
  - Canvas CSV: `docs/performance/FT-M-05.06/FT-M-05.06-after-operational-hover-inspector-closed-canvas.csv`  
  - HUD dump: `docs/performance/FT-M-05.06/FT-M-05.06-after-operational-hover-inspector-closed.json`  
  - Highlights: `interopDispatches=111`, `totalDispatches=134`, `ratePerSecond=2.82`, `pointerQueueDrops=738`, `pointerEventsProcessed=916` of `1733`, `dragAverageFrameMs=3.612 ms`, `dragMaxFrameMs=13.1 ms`, `avgDrawMs=3.428–4.215 ms`.
- **Transportation, mode=full, inspector closed** — Inspector collapsed throughout to show the lowest interop baseline.  
  - Hover CSV: `docs/performance/FT-M-05.06/FT-M-05.06-after-full-hover-inspector-closed-hover.csv`  
  - Canvas CSV: `docs/performance/FT-M-05.06/FT-M-05.06-after-full-hover-inspector-closed-canvas.csv`  
  - HUD dump: `docs/performance/FT-M-05.06/FT-M-05.06-after-full-hover-inspector-closed.json`  
  - Highlights: `interopDispatches=0`, `totalDispatches=58`, `ratePerSecond≈0`, `pointerQueueDrops=1`, `avgDrawMs≈5.6–6.8 ms`, `dragAverageFrameMs≈5.8–6.8 ms`.
- **Transportation, mode=operational, inspector open** — Inspector kept open while repeating the operational-only interaction script.  
  - Hover CSV: `docs/performance/FT-M-05.06/FT-M-05.06-after-operational-hover-inspector-open-hover.csv`  
  - Canvas CSV: `docs/performance/FT-M-05.06/FT-M-05.06-after-operational-hover-inspector-open-canvas.csv`  
  - HUD dump: `docs/performance/FT-M-05.06/FT-M-05.06-after-operational-hover-inspector-open.json`  
  - Highlights: `interopDispatches=161`, `totalDispatches=173`, `ratePerSecond=2.48`, `pointerQueueDrops=792`, `avgDrawMs≈3.5 ms`, `dragAverageFrameMs≈3.8 ms`.

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

## Phase 5 — Main-Thread Latency Remediation

Manual profiling showed the “freeze → flush” interaction pattern even after Phases 1–2. This phase reduces main-thread pressure so hover/pan stay responsive:

1. **Strip hot-path logging**  
   - Replace raw `console.*` calls in drag/hover/render paths with a guarded `debugLog` helper so production sessions incur zero logging overhead.

2. **RAF-only drag/hover processing**  
   - Enforce a single `requestAnimationFrame` per gesture for both panning and hovering, dropping intermediate pointer samples to keep queues shallow.

3. **Avoid full redraws for hover-only changes**  
   - Split “scene rebuild” (hitboxes, node maps, edge geometry) from “paint” (highlights, tooltips) so hover tweaks do not rehydrate world data, and short-circuit draws when `hasHoverVisualDelta` is false.

4. **Faster edge hit-tests**  
   - Introduce a coarse world-space grid/quadtree for edge hitboxes to prefilter candidates, and cache distance computations whenever the pointer stays within the hover epsilon.

5. **Interop throttling & layout hygiene**  
   - Keep `.NET` hover callbacks suppressed unless the inspector is visible and IDs change, cache `getBoundingClientRect()` once per RAF tick, and debounce viewport-change notifications so .NET only sees drag/wheel completions.
