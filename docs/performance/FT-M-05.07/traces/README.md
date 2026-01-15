# Chrome Trace Validation (Task 4.4)

This README tracks the exported Chrome Performance traces. The binary trace files live under `/performance/FT-M-05.07/traces/` (gitignored) and the matching HUD dumps live under `/performance/FT-M-05.07/captures/`.

## Prerequisites

1. **Run the FlowTime stack locally**
   ```bash
   # Terminal 1 – Engine API
   dotnet run --project src/FlowTime.API --urls http://0.0.0.0:8080

   # Terminal 2 – Sim API (if the run requires SIM services)
   ASPNETCORE_URLS=http://0.0.0.0:8090 dotnet run --project src/FlowTime.Sim.Service

   # Terminal 3 – UI (Blazor WebAssembly dev server)
   dotnet run --project src/FlowTime.UI/FlowTime.UI.csproj
   ```
   The UI listens on `http://localhost:5219` by default. Load the transportation benchmark run (`/time-travel/topology?runId=run_20251214T151352Z_479f8f01&mode=simulation`).

2. **Reset diagnostics**: In the browser console run `FlowTime.TopologyCanvas.resetHoverDiagnostics(canvas)` before each recording so the HUD/JSON window matches the trace timeline.

3. **Chrome setup**: Use desktop Chrome (not Playwright Chromium) so you can open DevTools → Performance, enable *Screenshots* + *Web Vitals*, and export the trace.

## Capture workflow

For each scenario:

1. Open Chrome DevTools → Performance.
2. Click **Record**, exercise the canvas for ~15 s:
   - Hover sweep across the busiest area (full graph).
   - Drag/pan a few times (full graph).
   - Toggle inspector on and repeat.
   - Switch to Operational-only view and repeat (inspector closed).
3. Stop recording and save the trace with the naming convention below under `/performance/FT-M-05.07/traces/`.
4. Click the HUD “Dump” button to capture the JSON payload (store under `/performance/FT-M-05.07/captures/`).
5. Add a short summary to the table in this README.

### File naming

| Scenario | Suggested Trace File |
| --- | --- |
| Full graph — inspector closed | `/performance/FT-M-05.07/traces/full-graph-inspector-closed-trace.json.gz` |
| Full graph — inspector open | `/performance/FT-M-05.07/traces/full-graph-inspector-open-trace.json.gz` |
| Operational-only — inspector closed | `/performance/FT-M-05.07/traces/operational-inspector-closed-trace.json.gz` |
| Operational-only — inspector open (optional) | `/performance/FT-M-05.07/traces/operational-inspector-open-trace.json.gz` |

Compress traces if they exceed 10 MB.

### Results log (fill in after capture)

| Scenario | Trace | HUD Dump | Notes |
| --- | --- | --- | --- |
| Full graph — inspector closed | `/performance/FT-M-05.07/traces/full-graph-inspector-closed-trace.json.gz` | `/performance/FT-M-05.07/captures/full-hover.json` | Hover sweep only; HUD shows ~13 candidates/sample, zero scene rebuilds, pointer INP avg ≈ 20 ms. |
| Full graph — inspector open | `/performance/FT-M-05.07/traces/full-graph-inspector-open-trace.json.gz` | `/performance/FT-M-05.07/captures/full-drag.json` | Drag + inspector pinned; confirms gating keeps INP ≈ 9 ms while overlay updates handle 72 deltas. |
| Operational-only — inspector closed | `/performance/FT-M-05.07/traces/operational-inspector-closed-trace.json.gz` | `/performance/FT-M-05.07/captures/operational-hover.json` | Small graph hover; adaptive grid drops to ~5 candidates and cache hits dominate, no scene rebuilds. |
| Operational-only — inspector open | `/performance/FT-M-05.07/traces/operational-inspector-open-trace.json.gz` | (reuse `/performance/FT-M-05.07/captures/operational-hover.json` or capture a dedicated dump if needed) | Inspector view in ops mode; trace shows pointer INP and overlay cadence holding in the trimmed graph. |

Update this table once traces are saved.
