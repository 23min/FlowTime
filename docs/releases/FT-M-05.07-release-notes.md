# FT-M-05.07 — Topology UI Input/Paint/Data Separation

## Summary

Milestone FT-M-05.07 delivers the three-lane architecture for the Time-Travel Topology page. Scene geometry is now cached, overlay deltas apply without rehydrating DOM, hover/pan input is RAF-only, and the inspector/data lane work happens asynchronously. Diagnostics (HUD, CSV, Playwright, Chrome traces) confirm that hover, drag, and operational views stay frame-rate on the transportation benchmark run.

## Key Changes

1. **Input lane strictness** — All pointer/hover/pan handlers moved behind a single RAF queue with cached `getBoundingClientRect()`. Diagnostics now surface pointer INP, throttle/drop stats, and per-frame draw durations. (`src/FlowTime.UI/wwwroot/js/topologyCanvas.js`, `docs/performance/FT-M-05.07/README.md`)
2. **Scene vs. overlay payload split** — `TopologyCanvasBase` produces separate scene and overlay payloads; the JS renderer calls `renderScene` only when the graph structure changes and `applyOverlayDelta` for hover/bin updates. DOM proxies reuse cached statics. (`src/FlowTime.UI/Components/Topology/TopologyCanvas.razor*`, `src/FlowTime.UI/wwwroot/js/topologyCanvas.js`)
3. **Data lane hygiene** — Bin refresh work now runs via `AsyncWorkQueue`, inspector-only calculations are gated, and run-state persistence is debounced. Playwright gained an inspector toggle budget test. (`src/FlowTime.UI/TimeTravel/*`, `tests/FlowTime.UI.Tests`)
4. **Spatial index & diagnostics** — Edge hit-tests use an adaptive world-space grid with per-cell caching. HUD/CSV expose candidate counts, cache hits/misses, and the panel adds a smoothed FPS indicator. Diagnostics dumps live under `docs/performance/FT-M-05.07/captures/` for regression checks.
5. **Chrome traces & validation** — Four traces (`traces/*.json.gz`) plus HUD dumps document hover/pan behavior (full vs. operational runs, inspector open/closed). No scene rebuilds occur during scrubs, pointer INP stays in the 9–30 ms band, and adaptive grid drops JS time by ~30 % for operational view.

## Validation

- `dotnet build`, `dotnet test --nologo` (full suite; perf benchmarks skip as expected)
- Playwright (`npm run test-ui`) covering hover baseline, inspector toggle, and spatial-index candidate ceilings
- HUD dumps: `/performance/FT-M-05.07/captures/full-hover.json`, `full-drag.json`, `operational-hover.json` (gitignored)
- Chrome traces: `/performance/FT-M-05.07/traces/full-graph-inspector-closed/open`, `operational-inspector-closed/open` (gitignored)

## Follow-ups / Notes

- Refactor nits (HUD/CSV duplication, keyboard focus doc) are captured in the milestone doc for future grooming.
- No blocking issues remain; milestone status is ✅ Completed.
