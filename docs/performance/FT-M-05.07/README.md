# FT-M-05.07 Performance Artifacts

This directory stores reference data, HUD captures, Playwright summaries, and Chrome trace notes that support milestone FT-M-05.07.

| File/Folder | Purpose |
|-------------|---------|
| `playwright-plan.md` | End-to-end automation plan for the hover/pan/scrub latency suite (Task 1.2). |
| `automation.md` | Running notes for the Playwright harness (commands, known failures, CI hooks). |
| `captures/` | (Repo-local pointer only) See `/performance/FT-M-05.07/captures/` for raw diagnostics dumps. |
| `traces/` | (Repo-local pointer only) See `/performance/FT-M-05.07/traces/` for Chrome trace exports. |
| `reports/` | Summaries of before/after metrics used in milestone release notes. |

> Keep raw dumps lightweight (≤10 MB per file). Compress Chrome traces if they exceed the repo’s recommended size limits.

## Playwright Harness (Task 1.2)

1. Ensure the FlowTime stack is running locally (API + SIM + UI). By default the UI listens on `http://localhost:5219`. Override `FLOWTIME_UI_BASE_URL` if needed.
2. Install Node deps and Playwright browsers (once per container):
   ```bash
   npm install
   npm run test-ui:install
   ```
3. Run the RED suite (will currently fail because the latency budgets are not met yet):
   ```bash
   npm run test-ui
   ```
   Use `npm run test-ui:debug` for headed debugging with the Playwright inspector.
4. Artifacts (traces, screenshots) land under `out/playwright-artifacts/`.

## HUD & CSV counters (Task 2.4)

The hover HUD and CSV now expose the budget guardrails we need for this milestone:

| Field | Description | Notes |
|-------|-------------|-------|
| `sceneRebuilds` / `overlayUpdates` | Count of full geometry rebuilds vs. overlay deltas since the last dump. | Should remain `0 / N` for steady-state hover/scrub workloads. |
| `frame rate` | Derived FPS (frames processed / elapsed seconds). | Values below ~30 fps suggest the canvas is starved. |
| `layoutReads` | Number of `getBoundingClientRect` calls during the window. | Helps prove we are not thrashing layout during RAF sampling. |
| `pointerInp*` | Aggregated pointer INP samples (avg/max) for hover gestures. | Playwright checks `pointerInpAverageMs` ≤ 200 ms. |
| `pointerEvents*` + `pointerThrottleSkips` | Input queue health. | We tint only when drops exceed ~60 % to avoid false alarms during normal sweeps. |

Programmatic access:

```js
const canvas = document.querySelector('canvas[data-topology-canvas]');
const hoverPayload = window.FlowTime.TopologyCanvas.dumpHoverDiagnostics(canvas);
const canvasPayload = window.FlowTime.TopologyCanvas.getCanvasDiagnostics(canvas, 'manual-canvas');
```

Both payloads are POSTed automatically when diagnostics uploads are enabled, and the FlowTime.API CSV writer (`diagnostics/hover-diagnostics.csv`) appends the columns listed above for long‑term tracking.

### Spatial index validation (Task 4.3)

The new HUD rows and CSV columns now include edge candidate, grid, and cache stats. Representative captures live under `/performance/FT-M-05.07/captures/` (gitignored):

| Scenario | File | Highlights |
|----------|------|------------|
| Full graph hover sweep | `/performance/FT-M-05.07/captures/full-hover.json` | `edgeCandidatesAverage≈13`, `edgeCacheHits=910` vs. `edgeCacheMisses=468`, pointer INP avg 19.6 ms while scene rebuilds stay at 0. |
| Full graph drag/pan | `/performance/FT-M-05.07/captures/full-drag.json` | 64 drag frames at ~2.4 ms, overlay-only updates (72) with no scene rebuilds; spatial stats hold at ≤15 candidates/sample. |
| Operational-only hover | `/performance/FT-M-05.07/captures/operational-hover.json` | Small graph drops average candidates to ~5 with strong cache ratio (237/46), demonstrating adaptive cell sizing (≈392 px). |

Use these payloads (or capture fresh ones via `FlowTime.TopologyCanvas.dumpHoverDiagnostics`) whenever you need to compare future tweaks against the FT-M-05.07 budgets.

## Chrome trace validation (Task 4.4)

The `traces/` folder contains a step-by-step guide for recording the final Chrome Performance traces. In short:

1. Start FlowTime.API, FlowTime.Sim.Service (if needed), and FlowTime.UI locally (`dotnet run` commands are listed in the trace README).
2. Open Chrome DevTools → Performance, enable *Screenshots* + *Web Vitals*, and record a ~15 s hover/pan session for each scenario (full graph with inspector closed/open, operational view, etc.).
3. Save the exported trace into `docs/performance/FT-M-05.07/traces/` using the naming convention outlined there and grab a matching HUD dump via the “Dump” button.

See [`traces/README.md`](traces/README.md) for detailed instructions and the results table to fill in once the traces are captured.
