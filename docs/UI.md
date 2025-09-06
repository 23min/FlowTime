# FlowTime UI (Blazor WASM)

The FlowTime UI is a standalone Blazor WebAssembly SPA intended to remain **decoupled** from the API (which may evolve to Azure Functions or another hosting model). It targets path base `/ui/` when published.

## Layout
```
/ui/FlowTime.UI            # Blazor WASM project
/src/FlowTime.API         # Minimal API (run, graph, healthz)
```

## Path Base
The project sets `<StaticWebAssetBasePath>ui</StaticWebAssetBasePath>`. The generated static assets expect to be served from `/ui/`.

Dev usage (separate processes):
1. Run API: `dotnet run --project src/FlowTime.API`
2. Run UI:  `dotnet run --project ui/FlowTime.UI`
3. Open the UI dev server URL (printed by the Blazor dev server). During dev it serves at `/`.
4. The UI issues POST requests to `/run` relative to its origin. For cross-origin setups, configure reverse proxy or adjust `HttpClient` base address.

Publish UI only:
```
dotnet publish ui/FlowTime.UI -c Release
# Output: ui/FlowTime.UI/bin/Release/net9.0/wwwroot/** (expects to be mounted at /ui/)
```
You can deploy those static files to a CDN or static host (e.g. Azure Static Web Apps) under `/ui/` path.

## API Demo Page
`Pages/ApiDemo.razor` (M0 scope) provides:
- Model selector (static YAML models loaded from `wwwroot/models/*.yaml`)
- YAML preview (read‑only this milestone)
- Run button (POST `/run`) and result table
- Graph button (POST `/graph`) with basic node list + stats (sources, sinks, max fan‑out)
- Simple line chart (MudBlazor v8 chart API) for returned series

### Model Loading
Models are stored as plain YAML under `ui/FlowTime.UI/wwwroot/models/`. They are fetched at startup via the default `HttpClient` whose `BaseAddress` is the UI origin. This keeps them cacheable as static assets and avoids embedding large string literals in the component.

### Dual HttpClients
Blazor default scoped `HttpClient` (UI origin) is used for static `/models/*.yaml`. A dedicated API `HttpClient` is registered for `IFlowTimeApiClient` with `BaseAddress` from `FlowTimeApiOptions` (defaults to `http://localhost:8080/`). This separation prevents accidental mixing of API and static asset origins.

### Preferences & State
`PreferencesService` restores the last selected model key on load. Expansion state for panels may be persisted in a later milestone.

## Future Enhancements
- Editable model editor + validation pass before run (M1+)
- CSV upload + charting (artifact replay)
- Rich graph visualization (edges, layout)
- Persisted model gallery (user models)
- Copy‑to‑clipboard and download buttons (YAML & results)
- Backlog & latency charts (after M7 — not early)
- Basic metrics (latency, node counts) surfaced in UI (latency deferred to backlog milestone)
- Series index consumption: fetch `index.json` (Contracts Parity M1) instead of inferring outputs from run response
- Manifest viewer: render `run.json` & `manifest.json` (hashes, warnings)

## Data Loading (Post-M1)
After Contracts Parity (M1) the UI consumes `series/index.json` (see [contracts.md](contracts.md)) to:
* Enumerate series (id, kind, unit, componentId, class)
* Lazy load individual CSVs via `path`

Planned optional formats (gold table, events) are referenced but safely ignored if absent.

## Keeping Concerns Separate
No build-time copy of UI into API. Integration for single-domain hosting can be added later via an opt-in MSBuild target or reverse proxy rules.

### Artifacts (Planned M1)

Once the Contracts Parity milestone lands, the UI will:
1. Request a run (`POST /run`) which writes artifacts under a run directory.
2. Fetch `index.json` (future GET endpoint or embedded link) to enumerate available series.
3. Lazily stream individual CSVs for selected series (enables large run scaling without bloating initial payload).
4. Optionally display manifest & run metadata (hashes, seed, grid) for parity/debug.

Until then, the UI consumes the inline series arrays returned directly from `/run`.
