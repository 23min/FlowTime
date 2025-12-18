# FT-M-05.07 Performance Artifacts

This directory stores reference data, HUD captures, Playwright summaries, and Chrome trace notes that support milestone FT-M-05.07.

| File/Folder | Purpose |
|-------------|---------|
| `playwright-plan.md` | End-to-end automation plan for the hover/pan/scrub latency suite (Task 1.2). |
| `automation.md` | Running notes for the Playwright harness (commands, known failures, CI hooks). |
| `captures/` | Raw diagnostics dumps (`hover-diagnostics_*.json`, `canvas-diagnostics_*.csv`) captured during validation. |
| `traces/` | Chrome performance trace exports (`.json.gz`) once Phase 4 validation runs. |
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
| `layoutReads` | Number of `getBoundingClientRect` calls during the window. | Helps prove we are not thrashing layout during RAF sampling. |
| `pointerInp*` | Aggregated pointer INP samples (avg/max) for hover gestures. | Playwright checks `pointerInpAverageMs` ≤ 200 ms. |
| `pointerEvents*` + `pointerThrottleSkips` | Input queue health. | Drop rate (drops / received) should stay below 5 %. |

Programmatic access:

```js
const canvas = document.querySelector('canvas[data-topology-canvas]');
const hoverPayload = window.FlowTime.TopologyCanvas.dumpHoverDiagnostics(canvas);
const canvasPayload = window.FlowTime.TopologyCanvas.getCanvasDiagnostics(canvas, 'manual-canvas');
```

Both payloads are POSTed automatically when diagnostics uploads are enabled, and the FlowTime.API CSV writer (`diagnostics/hover-diagnostics.csv`) appends the columns listed above for long‑term tracking.
