# FlowTime UI (Blazor WASM)

> **Charter Notice**: For current UI development direction, see [M-02.08](../milestones/M-02.08.md) (backend) and [UI Charter Transition](../transitions/UI-M2.6-CHARTER-TRANSITION.md) (reference).

The FlowTime UI is a **charter-aligned** Blazor WebAssembly SPA that implements the artifacts-centric workflow: **[Models] → [Runs] → [Artifacts] → [Learn]**. The UI is transitioning from direct API consumption to registry-based artifact analysis while maintaining backward compatibility.

## Layout
```
/ui/FlowTime.UI            # Blazor WASM project
/src/FlowTime.API         # Minimal API (run, graph, healthz)
```

## Development Setup

**For complete development setup including ports and configuration, see [development-setup.md](../development/development-setup.md)**

The UI runs on port 5219 by default during development. For production deployment configurations, see [deployment.md](deployment.md).

## Path Base
The project sets `<StaticWebAssetBasePath>ui</StaticWebAssetBasePath>`. The generated static assets expect to be served from `/ui/`.

Dev usage (separate processes):
1. Run API: `dotnet run --project src/FlowTime.API`
2. Run UI:  `dotnet run --project ui/FlowTime.UI` (default port 5219)
3. Open the UI dev server URL (printed by the Blazor dev server). During dev it serves at `/`.
4. The UI issues POST requests to `/run` relative to its origin. For cross-origin setups, configure reverse proxy or adjust `HttpClient` base address.

Publish UI only:
```
dotnet publish ui/FlowTime.UI -c Release
# Output: ui/FlowTime.UI/bin/Release/net9.0/wwwroot/** (expects to be mounted at /ui/)
```
You can deploy those static files to a CDN or static host (e.g. Azure Static Web Apps) under `/ui/` path.

## API Demo Page
`Pages/ApiDemo.razor` (M-00.00 scope) provides:
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

## Charter Workflow Implementation

### Current State (M-02.06+)
- **Models**: YAML selector from static collection
- **Runs**: API-driven execution with immediate results
- **Artifacts**: Export capability producing structured outputs
- **Learn**: Basic visualization and analysis

### Charter Milestones (M-02.07–M-02.09)
- **M-02.07 Registry**: Browse/filter runs by metadata; basic artifact discovery
- **M-02.08 Charter UI**: Incremental migration to registry-centric interface  
- **M-02.09 Compare**: Contextual cross-run analysis within artifact collections

## Future Enhancements (Legacy Roadmap)
> **⚠️ Charter Superseded**: The enhancements below represent the pre-charter roadmap. See [ROADMAP.md](../ROADMAP.md) for current development priorities.

- Editable model editor + validation pass before run
- Rich graph visualization (edges, layout)  
- Persisted model gallery (user models)
- Copy‑to‑clipboard and download buttons (YAML & results)

## Data Loading (Post-M-01.00)
After Contracts Parity (M-01.00) the UI consumes `series/index.json` (see [contracts.md](../reference/contracts.md)) to:
* Enumerate series (id, kind, unit, componentId, class)
* Lazy load individual CSVs via `path`

Planned optional formats (aggregates table, events) are referenced but safely ignored if absent.

## Keeping Concerns Separate
No build-time copy of UI into API. Integration for single-domain hosting can be added later via an opt-in MSBuild target or reverse proxy rules.

### Charter Artifacts Workflow (M-02.07+)

**Registry Integration** (M-02.07):
1. Browse runs through file-based registry with `index.json` metadata
2. Filter/search runs by tags, timestamps, model parameters  
3. Select artifact collections for analysis

**Incremental Charter UI** (M-02.08):
1. **Models**: Enhanced model management with registry integration
2. **Runs**: Execution with immediate registry registration
3. **Artifacts**: Direct consumption from registry structure  
4. **Learn**: Registry-aware analysis and visualization

**Legacy Compatibility**: Direct API consumption maintained during transition period for existing workflows.
