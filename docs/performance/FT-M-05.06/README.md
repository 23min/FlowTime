# FT-M-05.06 — Topology Hover Performance Study

**Goal:** Reduce JS↔.NET hover chatter on the topology canvas by ≥50% so the UI stays responsive on class-heavy runs.

## Reference Runs & Datasets

| Run | Data Path | Purpose |
| --- | --- | --- |
| Transportation basic classes | `data/runs/run_transportation-basic-classes_0e29c545` | Baseline hover spam reproduction (hundreds of class edges). |
| Supply-chain multi-tier classes | `data/runs/run_supply-chain-multi-tier-classes_ecc81d58` | Stress test inspector batching + dependency overlays. |

Both runs live under `/workspaces/flowtime-vnext/data/runs` when the generator has been executed.

## Instrumentation Helpers

The canvas now ships with a live diagnostics HUD (bottom-right chip). Click **Dump** to download the current stats and push them to the API automatically; hover/sec, totals, build hash, and run id are updated every ~0.75 s. Use `?diag=1` to force the HUD on if it was disabled via config.

You can still interact via DevTools:

```js
const canvas = document.querySelector('canvas.topology-canvas');
FlowTime.TopologyCanvas.resetHoverDiagnostics(canvas);
// Hover edges for ~10 seconds, then sample:
FlowTime.TopologyCanvas.getHoverDiagnostics(canvas);
```

Returned shape:

```json
{
  "runId": "run_transportation-basic-classes_0e29c545",
  "buildHash": "devlocal",
  "payloadSignature": "1.0000|0.00|0.00|0.0000|0.0000|1.0000|1.0000",
  "interopDispatches": 42,
  "durationMs": 10123.33,
  "ratePerSecond": 4.15,
  "timestampUtc": "2025-12-06T12:25:00.000Z",
  "source": "snapshot",
  "canvas": { "width": 1280, "height": 720 }
}
```

Use `resetHoverDiagnostics` right before and after a Chrome Performance trace so the counter window matches the trace window. `FlowTime.TopologyCanvas.dumpHoverDiagnostics(canvas)` mirrors the HUD “Dump” action if you prefer scripting.

## Automatic Collection

When `Diagnostics:Hover.AutoUploadEnabled` is true the HUD POSTs to `POST /v1/diagnostics/hover` every ~10 s. FlowTime.API stores each payload under `<data>/diagnostics/<runId>/hover_<timestamp>.json`, so the artifacts live next to the canonical run bundles. The payload schema mirrors `FlowTime.TopologyCanvas.getHoverDiagnostics`.

## Chrome Trace Capture

1. `dotnet run --project src/FlowTime.UI/FlowTime.UI.csproj` (or launch via VS) and load the Transportation run.
2. Open Chrome DevTools → Performance → enable “Screenshots” + “JS Samples”.
3. Start recording, hover across busy areas for 10 seconds, then stop.
4. Save the trace as `docs/performance/FT-M-05.06/transportation-before.json`.
5. Apply the FT-M-05.06 branch, reload, repeat and save as `transportation-after.json`.
6. Repeat the same workflow for the supply-chain run (`supply-chain-before/after.json`).

> **Tip:** Keep Chrome throttling disabled so the RAF batching aligns with the WASM runtime’s real cadence.

## Results Snapshot (to be updated after trace capture)

| Run | Baseline Hover Calls (10s) | Optimized Hover Calls (10s) | Reduction | Trace |
| --- | --- | --- | --- | --- |
| Transportation | _pending_ | _pending_ | _pending_ | `transportation-before/after.json` |
| Supply-chain | _pending_ | _pending_ | _pending_ | `supply-chain-before/after.json` |

Once the traces are dropped into this directory, add screenshots/links plus notes about scripting vs. idle slices.

## Diagnostics HUD Dumps

We are mirroring every HUD capture under this folder so future sessions can validate improvements without re-running the UI. Each capture includes the raw hover/canvas CSV excerpts plus the JSON dump produced by the HUD button.

| Scenario | Hover CSV | Canvas CSV | JSON Snapshot | Key Metrics |
| --- | --- | --- | --- | --- |
| Transportation — full graph, inspector open (after Phase 5 work) | `FT-M-05.06-after-full-hover-inspector-open-hover.csv` | `FT-M-05.06-after-full-hover-inspector-open-canvas.csv` | `FT-M-05.06-after-full-hover-inspector-open.json` | `interopDispatches=52`, `ratePerSecond=1.02`, `pointerQueueDrops=7`, `dragAverageFrameMs=5.477`, `dragMaxFrameMs=10.9` |
| Transportation — full graph, inspector closed (after Phase 5 work) | `FT-M-05.06-after-full-hover-inspector-closed-hover.csv` | `FT-M-05.06-after-full-hover-inspector-closed-canvas.csv` | `FT-M-05.06-after-full-hover-inspector-closed.json` | `interopDispatches=0`, `totalDispatches=58`, `pointerQueueDrops=1`, `avgDrawMs≈5.6–6.8 ms`, `zoomPct=111.81` |
| Transportation — operational-only, inspector closed (after Phase 5 work) | `FT-M-05.06-after-operational-hover-inspector-closed-hover.csv` | `FT-M-05.06-after-operational-hover-inspector-closed-canvas.csv` | `FT-M-05.06-after-operational-hover-inspector-closed.json` | `interopDispatches=111`, `ratePerSecond=2.82`, `pointerQueueDrops=738`, `dragAverageFrameMs=3.612`, `dragMaxFrameMs=13.1` |
| Transportation — operational-only, inspector open (after Phase 5 work) | `FT-M-05.06-after-operational-hover-inspector-open-hover.csv` | `FT-M-05.06-after-operational-hover-inspector-open-canvas.csv` | `FT-M-05.06-after-operational-hover-inspector-open.json` | `interopDispatches=161`, `ratePerSecond=2.48`, `pointerQueueDrops=792`, `avgDrawMs≈3.5 ms`, `dragAverageFrameMs≈3.8 ms` |

## Follow-up Checklist

- [ ] Attach four trace files (before/after for both runs).
- [ ] Record the `FlowTime.TopologyCanvas.getHoverDiagnostics` counts alongside each trace window.
- [ ] Copy a screenshot of the Chrome Performance summary after the “after” run for the release notes.
