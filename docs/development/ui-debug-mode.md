# FlowTime UI Debug Mode Reference

FlowTime UI is a Blazor WebAssembly app under `src/FlowTime.UI` that depends on both the Engine (`http://localhost:8080`) and Sim (`http://localhost:8090`) APIs. This note captures the exact commands we use to launch the UI in .NET debug mode and the Chrome DevTools workflow used while validating performance milestones (see [TT-M-03.29 performance report](../performance/TT-M-03.29-performance-report.md) and [perf-log](../performance/perf-log.md)).

---

## 1. Launch the UI in Debug Mode

The UI uses the `FlowTimeApi` and `FlowTimeSimApi` sections from `appsettings*.json`. Overriding them through environment variables keeps the commands portable across Bash and PowerShell.

### Bash

```bash
#!/usr/bin/env bash
set -euo pipefail

cd /workspaces/flowtime-vnext

export ASPNETCORE_ENVIRONMENT=Development
export FlowTimeApi__BaseUrl="${FLOWTIME_API_URL:-http://localhost:8080/}"
export FlowTimeSimApi__BaseUrl="${FLOWTIME_SIM_API_URL:-http://localhost:8090/}"
export FlowTimeSimApi__FallbackUrls__0="${FLOWTIME_SIM_API_FALLBACK:-http://localhost:8091/}"

ASPNETCORE_URLS="${FLOWTIME_UI_URL:-http://0.0.0.0:5219}" \
dotnet watch run \
  --project src/FlowTime.UI/FlowTime.UI.csproj \
  --configuration Debug
```

### PowerShell

```powershell
cd /workspaces/flowtime-vnext

$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:FlowTimeApi__BaseUrl = $env:FLOWTIME_API_URL ?? "http://localhost:8080/"
$env:FlowTimeSimApi__BaseUrl = $env:FLOWTIME_SIM_API_URL ?? "http://localhost:8090/"
$env:FlowTimeSimApi__FallbackUrls__0 = $env:FLOWTIME_SIM_API_FALLBACK ?? "http://localhost:8091/"

dotnet watch run `
  --project src/FlowTime.UI/FlowTime.UI.csproj `
  --configuration Debug `
  --urls ($env:FLOWTIME_UI_URL ?? "http://localhost:5219")
```

**Notes**

- `dotnet watch run` keeps the Blazor DevServer and WASM debug proxy alive so Chrome can attach. We stick to `--configuration Debug` to avoid AOT stripping during investigation.
- Override `FLOWTIME_API_URL`, `FLOWTIME_SIM_API_URL`, or `FLOWTIME_UI_URL` when the Engine/Sim APIs run on different hosts or ports (e.g., inside a dev container).
- The commands print `FlowTime.UI started v<version>` once the WASM payload is available. Wait for that log before attaching a debugger.

---

## 2. Chrome WebAssembly Debugging Workflow

1. Launch the UI with one of the commands above and open Chrome to the configured URL (default `http://localhost:5219`).
2. Press `Shift+Alt+D` (Windows/Linux) or `⌘+⌥+D` (macOS) to open the Blazor WebAssembly debug window. If shortcuts are blocked, browse to `chrome://inspect/#devices` and select the `FlowTime.UI` target exposed at `_framework/debug/ws-proxy`.
3. In the DevTools instance that opens:
   - Use **Sources → dotnet://** to load `.razor` and `.cs` sources; the structure mirrors the repo (e.g., `dotnet://FlowTime.UI/Pages/TimeTravel/Topology.razor`).
   - Add breakpoints, watches, and conditional breakpoints exactly like a .NET server-side debug session.
   - The **Call Stack** toggles between JavaScript and .NET frames, which is critical when tracing PixiJS interop under `wwwroot/js/topologyCanvas.js`.
4. Use **Network** to ensure API calls target the intended Engine/Sim hosts (the base URLs come from the environment variables set earlier).
5. When the debugger pauses, Chrome shows both the compiled WebAssembly and the original C# in parallel. Use the **Locals** pane to inspect strongly typed state such as `TopologyCanvasState` or `TimeTravelRange`.

Troubleshooting tips:

- If `Shift+Alt+D` shows an error, confirm that you're using a Chromium-based browser (Chrome/Edge) and that the UI is served over HTTP/HTTPS, not a `file://` URL.
- Clear Blazor's cache via **Application → Clear storage** when hot reload stops updating `dotnet.wasm`.
- For JS interop issues, open the standard DevTools window (`F12`) alongside the WASM debugger so you can watch `console.log` output and Pixi warnings in real time.

---

## 3. Using Chrome for WASM + Performance Milestones

The FlowTime time-travel milestones capture Chrome traces under [TT-M-03.29 performance report](../performance/TT-M-03.29-performance-report.md) and summarized samples in [perf-log](../performance/perf-log.md). Reproducing that workflow:

1. Open the UI in Chrome and start the WASM debugger (even if you only need perf data—the debugger keeps symbol names friendly).
2. Go to **DevTools → Performance**, enable **Memory** and **WebAssembly** in the capture options, then click **Record**.
3. Interact with the UI scenario you're validating (e.g., scrub the topology timeline or run the PMF dashboard) to reproduce the perf issue.
4. Stop the recording to inspect:
   - `Main` thread tasks (JS interop, layout, MudBlazor rendering).
   - `Wasm` thread samples (`dotnet.wasm` + Mono runtime work).
   - `JS Heap`/`Wasm Memory` graphs to catch regressions similar to those documented in the perf milestone.
5. Use **Performance → Export** to save the trace (`.json`) and attach it when updating the perf milestone docs.

For memory-deep dives, switch to **DevTools → Memory → Allocation instrumentation on timeline**. That view highlights object churn inside components like `TopologyCanvas`, which we referenced when writing TT-M-03.29. Keep the exported snapshots alongside perf logs so future milestones can compare allocations apples-to-apples.

---

## 4. Debugging Checklist

- **Verify API reachability** before launching (the UI fails fast if the Engine or Sim base URLs are unreachable).
- **Keep the Performance tab docked** during long debugging sessions so recordings stay aligned with the actions you take.
- **Restart `dotnet watch`** after switching between Debug/Release—the WASM linker cache otherwise keeps stripped symbols that prevent Chrome from showing accurate source maps.
- **Document any findings** in `docs/performance/perf-log.md` immediately after exporting traces so the context stays tied to the captured chrome profile.
