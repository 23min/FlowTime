# Release FT-M-05.06 — Topology Canvas Performance Sweep

**Release Date:** 2025-12-17  
**Type:** Milestone delivery  
**Canonical Runs:** `data/runs/run_transportation-basic-classes_9a88904467fb066d93d8b60a984918685018a4c5360efaf0ae32e3456a821d10`, `data/runs/run_supply-chain-multi-tier-classes_5fbe89c92931f8fa0842884b032d9fb3b14d1fb2872dd6270b08b548f2613a0c`

## Overview

FT-M-05.06 focuses on keeping the Time-Travel topology page responsive on class-heavy runs. Hover detection is now fully owned by JS (with HUD diagnostics and CSV dumps under `data/diagnostics/`), node/tooltips render without Blazor interop unless the inspector is open, and the timeline scrubber updates instantly by deferring metric recompute until the drag completes. Operational filter toggles re-use the async refresh pipeline instead of reloading the entire page, so flipping between “All” and “Operational” no longer locks the browser. These changes set the stage for the upcoming UI-perf epic by separating input/paint/data lanes and documenting the validation protocol.

## Key Changes

1. **Diagnostics HUD + CSV capture**  
   - Added a diagnostics overlay (with hide/collapse preference in `localStorage`), manual dump button, and automatic CSV logging under `data/diagnostics` so hover throughput, pointer drops, and drag timing can be analyzed without DevTools.  
   - The HUD chip show/hide state is persisted per user, and inspector IDs are abbreviated to keep the panel compact.
2. **JS hover ownership and throttling**  
   - `topologyCanvas.js` now drives hover hit-testing, tooltips, and node highlighting entirely in JS; Blazor hover callbacks only fire when the inspector is visible and the canonical ID changes. Edge hit-tests reuse cached world-space bounds and RAF-coalesced pointer events.  
   - Hot-path console logging was removed and layout reads are cached per frame, eliminating main-thread stalls when DevTools is open.
3. **Timeline scrub gating & async recompute**  
   - `ApplyBinSelection` updates the pointer/window immediately, but while the dial is dragging we simply cache the latest bin; `EndDialDrag` flushes the cache and schedules a single `RunBinDataRefreshAsync`. This prevents long recomputes from running mid-drag, restoring instant slider feedback. Playback and persistence still execute once the recompute finishes.  
   - Operational toggle reloads now reuse the async state-window refresh path (with “Refreshing view…” overlay) instead of reloading the entire page, so the canvas stays interactive during filter changes.

## Tests

- `dotnet build`
- `dotnet test --nologo` *(known issue: `FlowTime.Tests.Performance.M15PerformanceTests.Test_ExpressionType_Performance` still reports “Complex expressions too slow compared to simple”; same perf harness sensitivity as previous builds and unrelated to the UI changes.)*

## Verification

- Enabled diagnostics overlay via `appsettings.Development.json` (no querystring needed) and captured HUD dumps for transportation runs in both full and operational modes (`docs/performance/FT-M-05.06/*after-*.json` + CSV pairs).  
- Manually scrubbed the timeline on `run_transportation-basic-classes_9a88904467fb066d93d8b60a984918685018a4c5360efaf0ae32e3456a821d10` to confirm pointer updates are instant and hover resumes immediately after drag end.  
- Toggled the feature-bar Operational switch repeatedly to verify the UI stays responsive and the “Refreshing view…” overlay clears once the async window reload completes.
