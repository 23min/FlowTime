# FlowTime UI (Blazor WASM)

The FlowTime UI is a standalone Blazor WebAssembly SPA intended to remain **decoupled** from the API (which may evolve to Azure Functions or another hosting model). It targets path base `/ui/` when published.

## Layout
```
/ui/FlowTime.UI            # Blazor WASM project
/apis/FlowTime.API         # Minimal API (run, graph, healthz)
```

## Path Base
The project sets `<StaticWebAssetBasePath>ui</StaticWebAssetBasePath>`. The generated static assets expect to be served from `/ui/`.

Dev usage (separate processes):
1. Run API: `dotnet run --project apis/FlowTime.API`
2. Run UI:  `dotnet run --project ui/FlowTime.UI`
3. Open the UI dev server URL (printed by the Blazor dev server). During dev it serves at `/`.
4. The UI issues POST requests to `/run` relative to its origin. For cross-origin setups, configure reverse proxy or adjust `HttpClient` base address.

Publish UI only:
```
dotnet publish ui/FlowTime.UI -c Release
# Output: ui/FlowTime.UI/bin/Release/net9.0/wwwroot/** (expects to be mounted at /ui/)
```
You can deploy those static files to a CDN or static host (e.g. Azure Static Web Apps) under `/ui/` path.

## Run Page
`Pages/Run.razor` provides:
- YAML model editor (pre-filled example)
- Run button (POST /run)
- Tabular preview of returned series

## Future Enhancements
- CSV upload + charting
- Graph visualization (using `/graph`)
- Persisted model gallery

## Keeping Concerns Separate
No build-time copy of UI into API. Integration for single-domain hosting can be added later via an opt-in MSBuild target or reverse proxy rules.
