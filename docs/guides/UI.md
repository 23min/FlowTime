# FlowTime UI (Blazor WebAssembly)

The UI is a Blazor WASM app in `src/FlowTime.UI` that talks to the FlowTime Engine API (default `http://localhost:8080/`) and, where needed, the Sim API (`http://localhost:8090/`) for template/catalog calls. This guide reflects the current implementation; legacy charter/transition content has been removed.

## Project layout
```
src/FlowTime.UI/              # Blazor WASM app
src/FlowTime.UI.Tests/        # UI test project
```

## Running the UI in development
1) Start the Engine API:
```bash
dotnet run --project src/FlowTime.API --urls http://0.0.0.0:8080
```
2) (Optional) Start the Sim Service if you want template-backed flows:
```bash
ASPNETCORE_URLS=http://0.0.0.0:8090 dotnet run --project src/FlowTime.Sim.Service
```
3) Start the UI:
```bash
dotnet run --project src/FlowTime.UI
```
- Default dev URL: `http://localhost:5219`
- API base URLs come from `src/FlowTime.UI/appsettings*.json` (`FlowTimeApi.BaseUrl`, `FlowTimeSimApi.BaseUrl`). Adjust these if you run the backends on different ports/hosts.

## What the UI uses today
- Engine API endpoints: `/v1/runs` family (run creation, graph, state/state_window, metrics), and supporting discovery endpoints.
- Sim API endpoints (optional): template listing/generation for orchestrated runs (template runner/time-travel pages).
- Static assets: model samples under `wwwroot/models`, default styles/scripts from the Blazor project.

## Notable pages/components (high level)
- **Artifacts/Time Travel**: browse runs and view topology/state dashboards using `/v1/runs/{id}` endpoints.
- **Template Runner**: front-end for Sim-backed template orchestration when Sim Service is available.
- **API Demo**: simple panel to post models/runs and view results (dev utility).
- **Health**: backend health checks.

## Build/Publish
```bash
# Build and run tests
dotnet build src/FlowTime.UI
dotnet test src/FlowTime.UI.Tests

# Publish static assets (served from /ui by default)
dotnet publish src/FlowTime.UI -c Release
# Output: src/FlowTime.UI/bin/Release/net9.0/publish/wwwroot/**
```
Static assets expect to be hosted at `/ui/` (`StaticWebAssetBasePath`); adjust hosting or base paths accordingly.

## Configuration tips
- Update `appsettings.Development.json` for local API endpoints.
- For production hosting, ensure the API base URLs are reachable from the client and that the UI is served from the configured base path (`/ui/`).
- No charter/registry transition features are assumed; the UI consumes the current Engine/Sim APIs as shipped.
