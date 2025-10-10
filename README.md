# FlowTime

[![Build](https://github.com/23min/FlowTime/actions/workflows/build.yml/badge.svg)](https://github.com/23min/FlowTime/actions/workflows/build.yml)
![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)

FlowTime is a unified platform for modelling, simulating, and exploring service flows. It combines:

- **FlowTime Engine** – a deterministic, discrete-time execution engine and API for “what-is/what-was” time-travel observability.
- **FlowTime Sim** – a template-driven simulation toolkit for “what-if” scenario generation and model authoring.

Together they let you design flows, generate synthetic demand, execute models, and inspect artifacts from a single mono-repository.

---

## ⚠️ Repository Consolidation Notice

As of v0.7.0 the FlowTime-Sim codebase lives inside this repository alongside the Engine surface.

**Sim projects now in-scope**
- `src/FlowTime.Sim.Core`
- `src/FlowTime.Sim.Service` (API on :8090)
- `src/FlowTime.Sim.Cli`
- `tests/FlowTime.Sim.Tests`

**Unified build/run workflow**
```bash
dotnet build              # Builds Engine + Sim + UI
dotnet test               # Runs all test projects

# Engine API (http://localhost:8080)
dotnet run --project src/FlowTime.API

# Sim API (http://localhost:8090)
dotnet run --project src/FlowTime.Sim.Service
```

Documentation parity is still in progress; simulation docs remain under `docs-sim/` until the Phase 5 migration wraps.

---

## At a Glance

| Surface | Purpose | Key Projects |
|---------|---------|--------------|
| **Engine** | Deterministic execution, artifact registry, REST API, Blazor UI | `src/FlowTime.Core`, `src/FlowTime.API`, `src/FlowTime.CLI`, `ui/FlowTime.UI`, `tests/FlowTime.*` |
| **Sim** | Template-based model authoring, provenance, synthetic data APIs | `src/FlowTime.Sim.Core`, `src/FlowTime.Sim.Service`, `src/FlowTime.Sim.Cli`, `tests/FlowTime.Sim.Tests` |

Why FlowTime:
- Explainable flow modelling with spreadsheet-like graphs and expressions.
- Side-by-side “what-if” (Sim) and “what-is” (Engine) validation.
- Shared schemas/contracts to keep downstream consumers aligned.
- Lightweight .NET 9 stack with CLI, API, and UI entry points.

---

## Repository Layout

```
flowtime-vnext/
├─ src/
│  ├─ FlowTime.Core/             # Engine execution core
│  ├─ FlowTime.API/              # Engine HTTP API (:8080)
│  ├─ FlowTime.CLI/              # Engine CLI
│  ├─ FlowTime.Contracts/        # Shared models/schemas
│  ├─ FlowTime.Adapters.Synthetic/ # Engine synthetic adapters
│  ├─ FlowTime.Sim.Core/         # Simulation templates + provenance
│  ├─ FlowTime.Sim.Service/      # Simulation HTTP API (:8090)
│  └─ FlowTime.Sim.Cli/          # Simulation CLI utilities
├─ ui/FlowTime.UI/               # Blazor WebAssembly UI (:5219)
├─ tests/                        # Engine + Sim test projects
├─ docs/                         # Engine + shared documentation
├─ docs-sim/                     # Simulation documentation (Phase 5 merge)
├─ templates/                    # Simulation templates
├─ examples/                     # Example models
├─ .devcontainer/                # Unified dev container setup
├─ .github/                      # CI workflows and Copilot instructions
└─ FlowTime.sln                  # Unified solution file
```

---

## Quickstart

### Prerequisites

- .NET 9 SDK
- Git
- Optional: VS Code with Dev Containers for a ready-to-run environment (`.devcontainer/`).

### Build & Test Everything

```bash
dotnet restore
dotnet build FlowTime.sln
dotnet test FlowTime.sln
```

Or use VS Code tasks: `build`, `build-sim`, `test`, `test-sim`.

### Run the Engine Surface

```bash
# Start the Engine API on http://localhost:8080
dotnet run --project src/FlowTime.API --urls http://0.0.0.0:8080

# Launch the Blazor UI (default http://localhost:5219)
dotnet run --project ui/FlowTime.UI

# Execute a model via CLI (writes CSV artifacts under out/)
dotnet run --project src/FlowTime.CLI -- run examples/m0.const.yaml --out out/m0
```

### Run the Simulation Surface

```bash
# Start the Sim API on http://localhost:8090
ASPNETCORE_URLS=http://0.0.0.0:8090 dotnet run --project src/FlowTime.Sim.Service

# Generate a model from a template
dotnet run --project src/FlowTime.Sim.Cli -- generate --id transportation-basic --out out/model.yaml

# Execute Sim unit tests only
dotnet test tests/FlowTime.Sim.Tests/FlowTime.Sim.Tests.csproj
```

---

## Documentation

- Engine + shared docs: `docs/` (roadmap, architecture, schemas, onboarding).
- Simulation docs: `docs-sim/` (templates, CLI guides, milestone history) – slated for Phase 5 consolidation.
- Consolidation plan: [`REPO-CONSOLIDATION-PLAN.md`](REPO-CONSOLIDATION-PLAN.md).

---

## Contributing

1. Branch from the appropriate milestone branch (e.g., `feature/<surface>-mX/<desc>` aligned with the active milestone).
2. Keep builds/tests passing (`dotnet build`, `dotnet test`).
3. Follow Conventional Commits (`feat(sim): ...`, `fix(api): ...`, `docs: ...`).
4. Update docs/tests alongside code changes.

Issues and pull requests are welcome—open a discussion first for large changes to align with the roadmap.

---

## License

MIT. See [`LICENSE`](LICENSE).
