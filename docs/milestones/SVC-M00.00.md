# Service Milestone SVC-M-0 — Minimal API (FlowTime.API)

## Lighthouse scenario

- Call a single HTTP endpoint to evaluate a model and return outputs as JSON/CSV. This unlocks automation, quick UI integration, and aligns with API-first.

## What’s included

- POST /run: accepts YAML, returns JSON (grid + outputs) and optionally CSV artifacts.
- GET /graph: returns the compiled node graph (ids, inputs, edges) for explainability.
- GET /healthz: liveness check.
- Optional: POST /negotiate placeholder for future real-time.

## How it works

- A thin FlowTime.API host wraps the existing Core engine and CLI parsing logic. Initial implementations may use Azure Functions or ASP.NET Core minimal APIs; the abstraction keeps hosting swappable.
- The function deserializes YAML, builds the node graph, evaluates deterministically, and emits JSON.
- No database; ephemeral storage for artifacts in dev.

## CLI vs GUI

- CLI: continues to produce CSV locally by default (offline-friendly, simple pipelines).
- GUI: prefers the API for reads/writes when available; can also open CSV files for quick visualization.
- Optional CLI “via API” mode: `--via-api <url>` routes runs through the API to assert parity and enable remote execution.

## No-drift policy (parity)

- Single evaluation path (FlowTime.Core) shared by CLI and API; no duplicate math.
- Contract tests ensure CSV from CLI equals CSV from API for the same model; JSON values must match numerically.
- Version the output schema; gate breaking changes with a version flag.

## Why now (early)

- Enables UI-M-0 to use the API path immediately (or fall back to local CSV).
- Makes scripted runs (pipelines, notebooks) trivial via HTTP.
- Keeps surface minimal and stable while core evolves.

## Hosting and pluggability

FlowTime.API is host-agnostic. The initial host can be Azure Functions or ASP.NET Core minimal APIs; the HTTP surface and DTOs remain identical across hosts. Only wiring/DI and deployment differ.

- Azure Functions (HTTP triggers): great for serverless and scale-to-zero.
- ASP.NET Core minimal APIs: portable, simple, and container-friendly.
- Containers/orchestrators: package either host for Kubernetes/App Service/etc.

Keep domain logic in Core/App/Handlers; hosts are thin adapters.

## Endpoints (SVC‑M-0)

- GET `/healthz` — health check
- POST `/graph` — compiled node graph (nodes, edges) from request YAML; GET may be added later when models become server resources
- POST `/run` — accepts model YAML (Content-Type: text/plain in M-0) and returns results as JSON
- Optional: POST `/negotiate` — placeholder for future real‑time

Error contract (M-0):
- On invalid input or parse errors, return `400 Bad Request` with JSON payload `{ "error": "..." }`.
- Use invariant-culture numbers; do not return stack traces.

## Acceptance criteria (additions)

- Host-agnostic: API runs locally on at least one host (Functions or ASP.NET Core).
- Parity tests pass: CLI CSV equals API output for sample models.
- Contracts documented: DTOs and HTTP semantics defined once and reused by all hosts.

## Example (stubbed until API ships)

```powershell
dotnet run --project src/FlowTime.Cli -- run examples/hello/model.yaml --out out/hello --via-api http://localhost:7071
# If API is unavailable, CLI falls back to local eval and notes the fallback.
```

---

## Pluggable architecture (practical blueprint)

### Layered split

Keep the HTTP host thin and swappable by separating concerns:

- FlowTime.Core — the engine (what you have).
- FlowTime.Contracts — request/response DTOs, JSON options, and error contract.
- FlowTime.App — application service (e.g., IRunService) orchestrating Core: Run, GetGraph, GetStateWindow. No HTTP here.
- FlowTime.Api.Handlers — HTTP-agnostic handlers that map DTOs ⇄ App, centralize validation and error mapping.
- FlowTime.API.Functions — Azure Functions HTTP endpoints binding to the same Handlers.
- FlowTime.API.AspNetCore — ASP.NET Core minimal API endpoints binding to the same Handlers.

### Shared serialization and validation

- One JSON configuration (options, converters) defined in Contracts, reused by all hosts.
- Centralized validation in Handlers; hosts only do binding/DI and delegate.
- Versioned DTOs: additive changes are non-breaking; breaking changes require a version bump/flag.

### Why swapping hosts is easy

- Hosts contain zero domain logic (only binding/DI/wiring).
- Routes, DTOs, and status codes live in Contracts + Handlers.
- Adding or removing a host does not touch Core/App/Contracts — it's just another adapter.

### First implementation (SVC‑M-0)

- Choose one host to start (e.g., Azure Functions) and keep it in a host-specific project (FlowTime.API.Functions).
- If/when adding ASP.NET Core, create FlowTime.API.AspNetCore and reuse Contracts + Handlers unchanged.
- CLI can optionally call the API (`--via-api <url>`) but continues to run locally by default; both paths call the same App layer → no drift.

### Recommended repository layout

```
flowtime/
├─ src/
│  ├─ FlowTime.Core/
│  ├─ FlowTime.Contracts/            # DTOs, JSON options, error model
│  ├─ FlowTime.App/                  # IRunService, orchestration over Core
│  └─ FlowTime.Api.Handlers/         # HTTP-agnostic handlers
├─ apis/
│  ├─ FlowTime.API.Functions/        # Initial host
│  └─ FlowTime.API.AspNetCore/       # Optional later host
├─ tests/
│  ├─ FlowTime.Tests/                # engine unit tests (existing)
│  ├─ FlowTime.Api.ContractsTests/   # CLI vs API parity tests
│  └─ FlowTime.Api.SmokeTests.Functions/   # host smoke tests (Functions)
│     FlowTime.Api.SmokeTests.AspNetCore/  # host smoke tests (AspNetCore)
└─ examples/ ...
```

### Guardrails (tests and policy)

- No-drift: contract tests compare CLI CSV vs API JSON/CSV for the same model.
- Versioned DTOs: bump minor when adding fields; avoid silent semantic changes.
- Single serialization config shared by all hosts.
- CI runs smoke tests for each host implementation.
