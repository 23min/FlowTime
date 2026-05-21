---
title: Process and Deployment (As-Of 2026-05-06)
status: as-of-2026-05-06
owner: investigation
---

# Process and Deployment

This document maps every binary FlowTime produces, every long-lived service, and how those processes wire together at runtime. Code is treated as truth; `docs/` may have drifted.

## 1. Binary inventory

The .NET solution `FlowTime.sln` contains 11 source projects. Five produce executables; the rest are libraries.

| Project | csproj SDK | OutputType / Effective | What it runs as | Binary / assembly name |
|---|---|---|---|---|
| `src/FlowTime.Core` | `Microsoft.NET.Sdk` | library | Engine evaluation core, in-process | `FlowTime.Core.dll` |
| `src/FlowTime.Contracts` | `Microsoft.NET.Sdk` | library | Shared DTOs/contracts | `FlowTime.Contracts.dll` |
| `src/FlowTime.Expressions` | `Microsoft.NET.Sdk` | library | Expression parser | `FlowTime.Expressions.dll` |
| `src/FlowTime.TimeMachine` | `Microsoft.NET.Sdk` | library | Validation orchestration / sweep / analysis runners | `FlowTime.TimeMachine.dll` |
| `src/FlowTime.Adapters.Synthetic` | `Microsoft.NET.Sdk` | library | Run-artifact reader (CSVs, index.json) | `FlowTime.Adapters.Synthetic.dll` |
| `src/FlowTime.Sim.Core` | `Microsoft.NET.Sdk` | library | Template parsing/generation | `FlowTime.Sim.Core.dll` |
| `src/FlowTime.API` | `Microsoft.NET.Sdk.Web` | implicit web app (long-lived) | ASP.NET Core minimal-API service on port 8081 | `FlowTime.API.dll` |
| `src/FlowTime.Sim.Service` | `Microsoft.NET.Sdk.Web` | implicit web app (long-lived) | ASP.NET Core minimal-API service on port 8090 | `FlowTime.Sim.Service.dll` |
| `src/FlowTime.Cli` | `Microsoft.NET.Sdk` | `OutputType=Exe` | One-shot CLI | `FlowTime.Cli.dll` (default name `flowtime` not set; runs as `dotnet run --project src/FlowTime.Cli`) |
| `src/FlowTime.Sim.Cli` | `Microsoft.NET.Sdk` | `OutputType=Exe`, `AssemblyName=flow-sim` | One-shot CLI | `flow-sim.dll` |
| `src/FlowTime.UI` | `Microsoft.NET.Sdk.BlazorWebAssembly` | Blazor WASM SPA | Static-asset bundle hosted by `dotnet run` dev server | `FlowTime.UI.dll` (compiled to wasm/dll bundle) |

Citations:
- `src/FlowTime.Cli/FlowTime.Cli.csproj:19` declares `<OutputType>Exe</OutputType>`.
- `src/FlowTime.Sim.Cli/FlowTime.Sim.Cli.csproj:3,5` declares `<OutputType>Exe</OutputType>` and `<AssemblyName>flow-sim</AssemblyName>`.
- `src/FlowTime.API/FlowTime.API.csproj:1` and `src/FlowTime.Sim.Service/FlowTime.Sim.Service.csproj:1` use `Microsoft.NET.Sdk.Web` (implies executable web host).
- `src/FlowTime.UI/FlowTime.UI.csproj:1` uses `Microsoft.NET.Sdk.BlazorWebAssembly`.

In addition, the **Rust engine** under `engine/` produces a single binary:

| Crate | Output | Role |
|---|---|---|
| `engine/cli` (`flowtime-engine`) | `engine/target/release/flowtime-engine` | Standalone Rust binary; subcommands `parse`, `plan`, `eval`, `validate`, `session`. The `session` subcommand opens a persistent stdin/stdout MessagePack RPC. |

Citations:
- `engine/Cargo.toml:1-3` declares the workspace with members `core`, `cli`.
- `engine/cli/Cargo.toml:2` sets the package name to `flowtime-engine`.
- `engine/cli/src/main.rs:9-39` implements the subcommand dispatch.

The Svelte UI (`ui/`) is not a .NET project. It is a SvelteKit application built with Vite; output depends on adapter selection.

- `ui/package.json` (no `private:false`, no publish step).
- `ui/svelte.config.js:14-18` sets `adapter: adapter()` from `@sveltejs/adapter-auto` — at dev time it serves via `vite dev`. No production adapter is pinned.

## 2. Service topology

Two long-lived HTTP services run in this solution. They do **not** depend on each other at startup or at request time — neither calls the other.

### 2.1 FlowTime.API (Engine API)

- **Port**: `8081` by default.
  - Override mechanism: `ASPNETCORE_URLS` env var (e.g., `http://0.0.0.0:8081`). Default profile in `src/FlowTime.API/Properties/launchSettings.json:8` is `http://0.0.0.0:8081`. The `start-api` task in `.vscode/tasks.json:75` sets `ASPNETCORE_URLS=http://0.0.0.0:8081`.
- **Endpoint surface (top-level paths)**:
  - `GET  /healthz`, `GET /v1/healthz` — health (`src/FlowTime.API/Program.cs:179,182`).
  - `POST /v1/run` — accept YAML model, evaluate, write run artifacts (`Program.cs:620`).
  - `POST /v1/graph` — return DAG nodes/edges from a YAML model (`Program.cs:752`).
  - `GET  /v1/runs/{runId}/{graph,metrics,state,state_window,index,model,series/{seriesId}}` (`Program.cs:802,974,1010,1028,1065,1092,1120`).
  - `POST /v1/runs/{runId}/export`, `GET /v1/runs/{runId}/export/{format}` (`Program.cs:1183,1219`).
  - `GET  /v1/artifacts`, `POST /v1/artifacts/index`, `POST /v1/artifacts/bulk-delete`, `POST /v1/artifacts/archive`, `GET /v1/artifacts/{id}`, `GET /v1/artifacts/{id}/relationships`, `GET /v1/artifacts/{id}/download`, `GET /v1/artifacts/{id}/files/{fileName}` (`Program.cs:223,230,378,397,416,466,511,563`).
  - `POST /v1/diagnostics/hover` (`Program.cs:273`).
  - `POST /v1/templates/refresh` (`Program.cs:215`) — proxy to the embedded `SimITemplateService`.
  - `GET  /v1/engine/session/health`, `GET /v1/engine/session` (WebSocket upgrade) (`Program.cs:199,204`).
  - Endpoint extension groups mounted on `/v1` (registered at `Program.cs:189-196`):
    - `/v1/runs` (list, get) — `src/FlowTime.API/Endpoints/RunOrchestrationEndpoints.cs:14-15`.
    - `/v1/telemetry/captures` — `TelemetryCaptureEndpoints.cs:15`.
    - `/v1/validate` — `ValidationEndpoints.cs:12`.
    - `/v1/sweep` — `SweepEndpoints.cs:12`.
    - `/v1/sensitivity` — `SensitivityEndpoints.cs:12`.
    - `/v1/goal-seek` — `GoalSeekEndpoints.cs:9`.
    - `/v1/optimize` — `OptimizeEndpoints.cs:9`.
- **Inbound dependencies** (callers):
  - Blazor UI: `src/FlowTime.UI/Program.cs:42-49` registers `HttpClient("FlowTimeAPI")` with `BaseUrl` from `FlowTimeApiOptions.SectionName == "FlowTimeApi"`. `src/FlowTime.UI/wwwroot/appsettings.json:9-13` resolves to `http://localhost:8081/`.
  - Svelte UI: `ui/vite.config.ts:13-21` proxies `/v1`, `/healthz`, and the WebSocket upgrade (`ws: true`) from dev port 5173 to `http://localhost:8081`.
  - `tools/mcp-server` task env: `.vscode/tasks.json:157` sets `FLOWTIME_API_URL=http://localhost:8081/v1`.
  - `FlowTime.Cli` Time-Machine subcommands (`validate`, `sweep`, `sensitivity`, `goal-seek`, `optimize`) accept JSON on stdin and produce JSON byte-compatible with the same `/v1/*` endpoints, but the CLI **does not call the API at runtime** — it links `FlowTime.Core` and `FlowTime.TimeMachine` directly (`src/FlowTime.Cli/Program.cs:30-41`).
  - Test infrastructure (in-process): `tests/FlowTime.Api.Tests`, `tests/FlowTime.Cli.Tests`, `tests/FlowTime.Integration.Tests` use `WebApplicationFactory<Program>` (e.g., `tests/FlowTime.Integration.Tests/IsolatedWebApplicationFactory.cs:8`). No live HTTP.
- **Outbound dependencies**:
  - **Spawns the Rust engine subprocess** when `RustEngine:Enabled=true` (`Program.cs:37-80`). Default binary path: `engine/target/release/flowtime-engine` resolved via `DirectoryProvider.FindSolutionRoot()` (`Program.cs:42-46`).
  - **WebSocket → Rust engine session bridge**: `EngineSessionBridge` spawns `flowtime-engine session` per `/v1/engine/session` connection (`src/FlowTime.API/Services/EngineSessionBridge.cs:55-65`). One subprocess per WebSocket, killed when the socket closes.
  - **In-process Sim core embedding**: `Program.cs:95-110` registers a `FlowTime.Sim.Core.Services.TemplateService` directly (no HTTP to Sim service). It reads templates from `${solutionRoot}/templates`. See drift note below.
  - **Filesystem**: writes run artifacts to `ArtifactsDirectory` (config) or `FLOWTIME_DATA_DIR` env var, default to `${solutionRoot}/data` (`Program.cs:1330-1362`). `appsettings.Development.json` pins `ArtifactsDirectory: /workspaces/flowtime-vnext/data/runs`.
  - **Filesystem (storage backend)**: `IStorageBackend` registered via `StorageBackendFactory.Create(StorageBackendOptions.FromConfiguration(config))` (`Program.cs:87-92`); dev config sets `Storage:Backend=filesystem` rooted at `/workspaces/flowtime-vnext/data/storage`.

### 2.2 FlowTime.Sim.Service

- **Port**: `8090` by default.
  - Override mechanism: `ASPNETCORE_URLS` env var. Default profile in `src/FlowTime.Sim.Service/Properties/launchSettings.json:8` is `http://0.0.0.0:8090`. The `start-sim-api` task at `.vscode/tasks.json:135` sets `ASPNETCORE_URLS=http://0.0.0.0:8090`.
- **Endpoint surface (top-level paths)** — all under `/api/v1` (group registered at `Program.cs:218`):
  - `GET  /healthz`, `GET /v1/healthz` (with `?detailed`) — `Program.cs:118,162`.
  - `GET  /api/v1/templates`, `GET /api/v1/templates/{id}`, `GET /api/v1/templates/{id}/source`, `GET /api/v1/templates/categories`, `POST /api/v1/templates/refresh` — `Program.cs:223,247,290,309,317`.
  - `POST /api/v1/templates/{id}/generate` — generate Engine model from template — `Program.cs:326`.
  - `POST /api/v1/drafts/generate`, `POST /api/v1/drafts/run`, `POST /api/v1/drafts/map-profile` — draft template flow — `Program.cs:396,455,841`.
  - `POST /api/v1/series/ingest`, `POST /api/v1/series/summarize` — series ingestion — `Program.cs:567,649`.
  - `POST /api/v1/profiles/fit`, `POST /api/v1/profiles/preview` — profile fitting — `Program.cs:713,804`.
  - `GET  /api/v1/models`, `GET /api/v1/models/{templateId}` — generated-model listing — `Program.cs:956,991`.
  - Run-orchestration extensions (mounted under `/api/v1/orchestration`, `Program.cs:219`) live in `src/FlowTime.Sim.Service/Extensions/RunOrchestrationEndpointExtensions.cs`.
- **Inbound dependencies** (callers):
  - Blazor UI: `src/FlowTime.UI/Program.cs:81` registers `IFlowTimeSimApiClient` as `FlowTimeSimApiClientWithFallback`; base URL from `FlowTimeSimApi:BaseUrl`, default `http://localhost:8090/` with fallback `http://localhost:8091/` (`appsettings.json:14-19`, `Configuration/FlowTimeSimApiOptions.cs:30,35`).
  - Svelte UI: `ui/vite.config.ts:9-12` proxies `/api/v1` from dev port 5173 to `http://localhost:8090`.
  - `tools/mcp-server` task env: `.vscode/tasks.json:155-156` sets `FLOWTIME_SIM_API_URL=http://localhost:8090/api/v1`.
  - Test infrastructure: `tests/FlowTime.Cli.Tests` uses `WebApplicationFactory<SimProgram>` (`TelemetryRunCommandTests.cs:35`).
- **Outbound dependencies**:
  - **Filesystem only**. Reads templates from `FLOWTIME_SIM_TEMPLATES_DIR` env / `FlowTimeSim:TemplatesDir` config / default `${cwd}/../../templates` (`Program.cs:1775-1803`). Writes generated models under `{DataRoot}/models` (`Program.cs:1842-1848`).
  - **Does not** call the Engine API. **Does not** spawn the Rust engine. (Verified: no `RustEngine` references in `src/FlowTime.Sim.Service/Program.cs`.)

### 2.3 The two services are decoupled

There is no HTTP edge from Engine API → Sim service or vice versa. The Engine API embeds `FlowTime.Sim.Core` directly via `ProjectReference` (`src/FlowTime.API/FlowTime.API.csproj:45`) and constructs a `SimTemplateService` in-process to serve `POST /v1/templates/refresh`. The Sim service exposes its own `/api/v1/templates/*` endpoints over a different prefix (`/api/v1` vs `/v1`).

> **Drift:** The README, `docs/architecture/headless-engine-architecture.md`, and the original "two-service" framing imply the API and Sim service are separate runtimes that communicate. In practice, the Engine API references and instantiates `FlowTime.Sim.Core` directly, and the Sim service is independent of the API at runtime. See `02-code-graph.md` for the project-reference diagram.

## 3. CLI surfaces

### 3.1 `FlowTime.Cli` (engine CLI)

Entry point: `src/FlowTime.Cli/Program.cs`. Top-level dispatch is positional-arg switch (`Program.cs:21-46`).

| Subcommand | What it does | Service-bound? | Files produced |
|---|---|---|---|
| `run <model.yaml> [--out <dir>] [--verbose] [--deterministic-run-id] [--seed <n>]` | Parse + evaluate model, write run artifacts (legacy in-proc path). | Standalone — links `FlowTime.Core` directly (`Program.cs:73-156`). | Run directory: `model/model.yaml`, `series/*.csv`, `manifest.json`, `run.json`, `spec.yaml`, etc. (via `RunArtifactWriter.WriteArtifactsAsync`). Default out dir: `OutputDirectoryProvider.GetDefaultOutputDirectory()`. |
| `run --template-id <id> --mode simulation\|telemetry ...` | Orchestrated run via `TelemetryRunCommand`. | Calls Sim Service in-proc via `WebApplicationFactory<SimProgram>` in tests; in production, uses `FlowTime.Sim.Core` directly via `RunOrchestrationService` (`Program.cs:51`). | Same artifact shape as legacy run. |
| `validate \| sweep \| sensitivity \| goal-seek \| optimize` | JSON-over-stdio Time-Machine commands; "byte-compatible with `/v1/*` endpoints" per `Program.cs:26-27`. | **Standalone**, even though the protocol matches the API. Links `FlowTime.TimeMachine` directly. | None unless `-o <path>` is given; reads JSON spec on stdin (or `--spec`), writes JSON to stdout. |
| `artifacts list [--template-id ...] [--model-id ...] [--limit ...] [--data-dir ...]` | Query the local `FileSystemArtifactRegistry`. | **Offline**, reads `${dataDir}` directly (`Program.cs:170-260`). | None — prints a table to stdout. |

Help text: `Program.cs:299-339`.

### 3.2 `FlowTime.Sim.Cli` (sim CLI, assembly name `flow-sim`)

Entry point: `src/FlowTime.Sim.Cli/Program.cs`. Verb+noun routing (`Program.cs:117-144`).

| Verb / Noun | What it does | Service-bound? | Files produced |
|---|---|---|---|
| `init` | Write `.flow-sim.yaml` config in cwd. | Offline. | `.flow-sim.yaml`. |
| `list templates` / `list models` | List templates from templates dir, or models from `{ModelsDir}`. | Offline (links `FlowTime.Sim.Core`). | None — stdout. |
| `show template --id <id>` / `show model --id <id>` | Show metadata + parameters. | Offline. | None. |
| `generate [model] --id <id> [--params <file>] [--out <file>] [--mode ...] [--provenance <file>]` | Substitute template parameters → emit Engine YAML model. | Offline. | YAML model file (or stdout); optional provenance JSON. |
| `validate [template\|params] --id <id> [--params <file>]` | Validate parameter overrides against template schema. | Offline. | None. |
| `refresh templates` | Reload template cache. | Offline. | None. |

The CLI **does not call the Sim Service over HTTP**. It instantiates `FlowTime.Sim.Core.Services.TemplateService` directly (`Program.cs:113-114`).

## 4. UI surfaces

### 4.1 Blazor UI (`src/FlowTime.UI`)

- **Runtime stack**: **Blazor WebAssembly SPA**.
  - SDK: `Microsoft.NET.Sdk.BlazorWebAssembly` (`FlowTime.UI.csproj:1`).
  - Bootstrap: `WebAssemblyHostBuilder.CreateDefault(args)` (`Program.cs:11`).
  - Component bag: MudBlazor 8.15.0 (`FlowTime.UI.csproj:14`).
  - The UI is NOT Blazor Server. The `dotnet run` invocation simply hosts the WASM bundle on a dev server (port 5219).
- **Default URL**: `http://localhost:5219` (HTTPS variant `https://localhost:7047`) per `Properties/launchSettings.json:9,19`.
- **Calls** (configured in `Program.cs:42-104` via named `HttpClient`s; resolved against `wwwroot/appsettings.json`):
  - **FlowTime.API** at `FlowTimeApi:BaseUrl` (default `http://localhost:8081/`, `appsettings.json:9-13`). Used by `IFlowTimeApiClient` (`Program.cs:70-78`, `Services/FlowTimeApiClient.cs:40`).
  - **FlowTime.Sim.Service** at `FlowTimeSimApi:BaseUrl` (default `http://localhost:8090/`, fallback `http://localhost:8091/`, `appsettings.json:14-19`). Used by `IFlowTimeSimApiClient` via `FlowTimeSimApiClientWithFallback` (`Program.cs:81`).
- **Configuration discovery**: standard Blazor config — `wwwroot/appsettings.json` plus `wwwroot/appsettings.Development.json` are downloaded by the WASM runtime at boot. There is no compile-time URL pinning.

### 4.2 Svelte UI (`ui/`)

- **Runtime stack**: **SvelteKit + Svelte 5 (runes)**, dev-served by **Vite**.
  - Adapter: `@sveltejs/adapter-auto` (`ui/svelte.config.js:14-18`) — no production target pinned.
  - Build tool: Vite 7 (`ui/package.json:34`).
  - Tailwind 4 + bits-ui + tw-animate-css.
- **Default URL**: `http://localhost:5173`. Hard-pinned via `strictPort: true` (`ui/vite.config.ts:8-9`).
- **Calls**: routed through Vite's dev proxy — the UI talks to its own origin and Vite forwards:
  - `/api/v1/*` → `http://localhost:8090` (Sim Service) (`vite.config.ts:9-12`).
  - `/v1/*` → `http://localhost:8081` (Engine API), with `ws: true` to forward the WebSocket upgrade (`vite.config.ts:13-17`).
  - `/healthz` → `http://localhost:8081` (`vite.config.ts:18-21`).
  - Some routes also read `import.meta.env.VITE_API_BASE` as a direct override, falling back to `http://localhost:8081` (`ui/src/routes/engine-test/+page.svelte`, `ui/src/routes/what-if/+page.svelte`).
- **Configuration discovery**: Vite's `import.meta.env.VITE_*` env vars (none committed) and the proxy in `vite.config.ts`. There is no `appsettings.json` equivalent.
- **Routes present** (`ui/src/routes/`): `/`, `/analysis`, `/engine-test`, `/health`, `/run`, `/time-travel`, `/what-if`.

> **Drift:** The Blazor UI and Svelte UI are concurrent surfaces. Per `CLAUDE.md`, the Svelte UI is the active rewrite target. Both are buildable today and both are referenced by VS Code tasks (`start-ui` for Blazor, `start-svelte-ui` for Svelte).

## 5. Devcontainer / dev workflow

VS Code tasks in `.vscode/tasks.json` are the canonical developer entry points.

| Task | Command | Effect |
|---|---|---|
| `build` | `dotnet build` | Builds the entire solution. (`tasks.json:5-19`.) |
| `test` | `dotnet test --nologo` | Runs all tests. (`tasks.json:38-54`.) |
| `start-api` | PowerShell-style `$env:ASPNETCORE_URLS = 'http://0.0.0.0:8081'; dotnet run --project src/FlowTime.API` | Engine API on 8081. **Note:** uses `$env:` PowerShell syntax. (`tasks.json:72-85`.) |
| `start-api-integration` | `dotnet run --project src/FlowTime.API` with `ASPNETCORE_URLS` and `FLOWTIME_DATA_DIR` env. | Same with explicit data dir. (`tasks.json:86-104`.) |
| `stop-api` | `pkill -f 'FlowTime.API'` | Kills API by process name. (`tasks.json:106-119`.) |
| `start-sim-api` | `dotnet run --project src/FlowTime.Sim.Service` with `ASPNETCORE_URLS=http://0.0.0.0:8090`. | Sim API on 8090. (`tasks.json:120-138`.) |
| `stop-sim-api` | `pkill -f 'FlowTime.Sim.Service'`. | (`tasks.json:165-178`.) |
| `start-ui` | `dotnet run --project src/FlowTime.UI`. | Blazor WASM dev server on 5219. (`tasks.json:179-192`.) |
| `start-svelte-ui` | `pnpm dev --host` in `ui/`. | Vite dev server on 5173. (`tasks.json:207-235`.) |
| `start-mcp-server` | `npm run dev` in `tools/mcp-server`. | MCP stdio subprocess (per `docs/guides/deployment.md:5`). (`tasks.json:139-164`.) |
| `demo-1-build-engine` → `demo-2-start-api` → `demo-3-start-ui` | Sequence: build Rust release binary, start Engine API, start Svelte UI. (`tasks.json:278-389`.) | This is the canonical "demo workflow" because the Engine API needs the Rust binary at `engine/target/release/flowtime-engine`. |
| `stop-all` | `bash scripts/stop-all.sh`. | Sends SIGTERM (or SIGKILL with `--force`) to `FlowTime.API`, `FlowTime.Sim.Service`, `FlowTime.UI`, and `node.*vite`, filtering out VS Code/port-forwarder. (`scripts/stop-all.sh`.) |

**Tests bringing up services**: API/CLI/Integration test projects use in-process `WebApplicationFactory<Program>` rather than launching real services (e.g., `tests/FlowTime.Integration.Tests/IsolatedWebApplicationFactory.cs:8`). The `tests/ui/` Playwright suite, per `CLAUDE.md`, expects services to be running and skips when unreachable.

**Service boot dependencies**: The Engine API does not require the Sim service to start. The Sim service does not require the Engine API. The Engine API requires the Rust binary at startup **only** if `RustEngine:Enabled=true`, which `appsettings.json` sets in `src/FlowTime.API/appsettings.json:13-15`. If the binary is missing, `EngineSessionBridge.IsAvailable` returns false and `/v1/engine/session` returns a WebSocket close with `engine_unavailable` (`src/FlowTime.API/Services/EngineSessionBridge.cs:35-50`).

**Devcontainer** (`.devcontainer/devcontainer.json`):
- Image: `mcr.microsoft.com/devcontainers/dotnet:1-9.0` (`devcontainer.json:3`).
- Installs Node 20 feature; .NET SDK 9 in image; Rust toolchain expected per the demo task (`. "$HOME/.cargo/env" && cargo build --release` at `tasks.json:282`).
- Forwarded ports: `1455` (Codex OAuth), `5173` (Svelte), `8080`, `8090`, `8091` (`devcontainer.json:68-93`). Note **8081 is NOT in `forwardPorts`** but is the actual API port; it is auto-forwarded by VS Code.
- `containerEnv`: `FLOWTIME_API_BASEURL=http://flowtime-api:8081`, `FLOWTIME_API_VERSION=v1` (`devcontainer.json:63-67`). These reference a `flowtime-api` host that does not match the local `start-api` task — they look like leftover compose-style hostnames.

> **Drift:** `devcontainer.json:65` sets `FLOWTIME_API_BASEURL=http://flowtime-api:8081`, but no Compose stack or DNS makes `flowtime-api` resolve. In current dev, the API is reached at `localhost:8081`. The env var appears unused at runtime — UI configs (`wwwroot/appsettings.json`) hard-code `http://localhost:8081/`.

> **Drift:** `devcontainer.json:68-73` forwards `8080` (labeled "FlowTime API") but the live API runs on `8081`. The deployment doc proposes `8080` for production (`docs/guides/deployment.md:15`); the devcontainer manifest seems to anticipate that future state.

## 6. Production / release shape

**No Dockerfiles, no Compose files, no Kubernetes manifests, no Helm charts exist in the repo.**

- Verified: `find /workspaces/flowtime-vnext -maxdepth 5 -name "Dockerfile" -not -path "*/node_modules/*" -not -path "*/.claude/worktrees/*" -not -path "*/bin/*" -not -path "*/obj/*" -not -path "*/target/*"` returns no results.
- The only production-deployment artifact is **`docs/guides/deployment.md`**, which documents an aspirational Compose layout including paths like `src/FlowTime.API/Dockerfile` and `src/FlowTime.Sim.Service/Dockerfile` — **neither file exists** (`docs/guides/deployment.md:28-49`).
- `docs/releases/` contains release notes (e.g., `FT-M-05.07-release-notes.md`, `CL-M-04.04.md`) but no deployment manifests.
- CI: `.github/workflows/build.yml` runs `dotnet build` + per-project `dotnet test` with `--blame-hang-timeout 60s`. No publish, container, or release step.
- Rust engine release: built locally via `cargo build --release` in `engine/` (`tasks.json:282`). No release pipeline observed.

> **Drift:** `docs/guides/deployment.md` describes Docker/Compose deployment as if it were the recommended path, with explicit references to `src/FlowTime.API/Dockerfile` and `src/FlowTime.Sim.Service/Dockerfile`. Neither file exists. There is no production deployment shape today; the project is dev-only as of this snapshot.

## 7. Runtime topology diagram

```mermaid
flowchart LR
    subgraph Browser["Browser"]
        BlazorWASM["Blazor WASM<br/>(FlowTime.UI bundle)"]
        SvelteApp["Svelte SPA<br/>(ui/)"]
    end

    subgraph DevHosts["Dev hosts (dotnet run / vite)"]
        BlazorHost["Blazor dev server<br/>:5219"]
        SvelteVite["Vite dev server<br/>:5173<br/>(proxy)"]
    end

    subgraph Services["Long-lived .NET services"]
        EngineAPI["FlowTime.API<br/>(ASP.NET minimal-API)<br/>:8081"]
        SimService["FlowTime.Sim.Service<br/>(ASP.NET minimal-API)<br/>:8090"]
    end

    subgraph Rust["Rust engine subprocesses"]
        RustSession["flowtime-engine session<br/>(per-WebSocket subprocess)"]
        RustOneShot["flowtime-engine eval/plan<br/>(one-shot via RustEngineRunner)"]
    end

    subgraph CLIs["One-shot CLIs"]
        EngineCLI["FlowTime.Cli<br/>run / validate / sweep / ..."]
        SimCLI["flow-sim<br/>generate / list / show / ..."]
    end

    subgraph FS["Filesystem"]
        DataDir["data/runs/<br/>(artifacts, manifests,<br/>series/*.csv)"]
        TemplatesDir["templates/<br/>(YAML)"]
        StorageDir["data/storage/<br/>(IStorageBackend)"]
    end

    BlazorHost -->|serves WASM bundle| BlazorWASM
    SvelteVite -->|serves SPA| SvelteApp

    BlazorWASM -->|HTTP /v1/*| EngineAPI
    BlazorWASM -->|HTTP /api/v1/*| SimService

    SvelteApp -->|/v1/* via proxy| SvelteVite
    SvelteApp -->|/api/v1/* via proxy| SvelteVite
    SvelteVite -->|HTTP /v1/* + WebSocket| EngineAPI
    SvelteVite -->|HTTP /api/v1/*| SimService

    EngineAPI -.->|spawn subprocess<br/>(WebSocket bridge)| RustSession
    EngineAPI -.->|spawn subprocess<br/>(stateless eval)| RustOneShot

    EngineAPI -->|writes / reads| DataDir
    EngineAPI -->|reads| StorageDir
    EngineAPI -->|reads embedded TemplateService| TemplatesDir

    SimService -->|reads| TemplatesDir
    SimService -->|writes generated models| DataDir

    EngineCLI -->|writes| DataDir
    SimCLI -->|reads| TemplatesDir
    SimCLI -->|writes| DataDir

    classDef service fill:#dde,stroke:#447,stroke-width:1px;
    classDef rust fill:#fec,stroke:#a64,stroke-width:1px;
    classDef cli fill:#efe,stroke:#373,stroke-width:1px;
    classDef fs fill:#eee,stroke:#666,stroke-width:1px;
    class EngineAPI,SimService service;
    class RustSession,RustOneShot rust;
    class EngineCLI,SimCLI cli;
    class DataDir,TemplatesDir,StorageDir fs;
```

Notes on the diagram:
- The Engine API `→` Rust subprocess edges are dashed because they're spawned per-request, not continuous.
- There is **no edge between FlowTime.API and FlowTime.Sim.Service**. Engine API embeds `FlowTime.Sim.Core` directly via project reference (see `02-code-graph.md`).
- The Blazor UI talks to both services directly from the browser (CORS is `AllowAnyOrigin` in dev: `Program.cs:135-136` for API, `Program.cs:40` for Sim).
- The Svelte UI never talks to the services directly during dev — Vite always proxies. This means `localhost:5173` is the only origin the browser sees.
- `tools/mcp-server` is omitted from the diagram (it is an stdio-only subprocess of the MCP client, not part of the runtime topology of the FlowTime services).
