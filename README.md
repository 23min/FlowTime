# FlowTime

[![Build](https://github.com/23min/FlowTime/actions/workflows/build.yml/badge.svg)](https://github.com/23min/FlowTime/actions/workflows/build.yml)
![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)

FlowTime is a unified platform for modelling, simulating, and exploring service flows. It combines:

- **FlowTime Engine** – a deterministic, discrete-time execution engine and API for “what-is/what-was” time-travel observability.
- **FlowTime Sim** – a template-driven simulation toolkit for “what-if” scenario generation and model authoring.

Together they let you design flows, generate synthetic demand, execute models, and inspect artifacts from a single mono-repository.

---

## FlowTime as Flow Literacy

FlowTime is not just a simulator or engine; it is a common language for talking about flows and resilience across many kinds of systems.

It brings together telemetry, architecture, scenarios, incidents, and business impact into a single, consistent representation of how work moves through a system over time.

Realizing this vision means investing in:

- Clear core concepts (flows, classes, paths, subsystems, incidents, modes).
- Reusable visual idioms for understanding how work moves and where it gets stuck.
- Shared definitions of recovery, impact, severity, and risk that can be applied across domains.

---

## At a Glance

| Surface | Purpose | Key Projects |
|---------|---------|--------------|
| **Engine** | Deterministic execution, artifact registry, REST API, Blazor UI | `src/FlowTime.Core`, `src/FlowTime.API`, `src/FlowTime.Cli`, `src/FlowTime.Expressions`, `src/FlowTime.Generator`, `src/FlowTime.UI`, `tests/FlowTime.UI.Tests`, `tests/FlowTime.*` |
| **Sim** | Template-based model authoring, provenance, synthetic data APIs | `src/FlowTime.Sim.Core`, `src/FlowTime.Sim.Service`, `src/FlowTime.Sim.Cli`, `templates/`, `examples/`, `catalogs/`, `fixtures/`, `tests/FlowTime.Sim.Tests` |

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
│  ├─ FlowTime.Cli/              # Engine CLI
│  ├─ FlowTime.Contracts/        # Shared models/schemas
│  ├─ FlowTime.Adapters.Synthetic/ # Engine synthetic adapters
│  ├─ FlowTime.Expressions/       # Expression language support
│  ├─ FlowTime.Generator/         # Model/generator utilities
│  ├─ FlowTime.Sim.Core/         # Simulation templates + provenance
│  ├─ FlowTime.Sim.Service/      # Simulation HTTP API (:8090)
│  ├─ FlowTime.Sim.Cli/          # Simulation CLI utilities
│  └─ FlowTime.UI/               # Blazor WebAssembly UI (:5219)
├─ tests/                        # Engine + Sim test projects
├─ docs/                         # Engine + shared documentation (roadmap, architecture, schemas)
├─ templates/                    # Simulation templates
├─ examples/                     # Example models
├─ catalogs/                     # Scenario catalogs and sample systems
├─ fixtures/                     # HTTP/microservices/time-travel fixtures
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
dotnet run --project src/FlowTime.UI

# Execute a model via CLI (writes CSV artifacts under out/)
dotnet run --project src/FlowTime.Cli -- run examples/m0.const.yaml --out out/m0
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
- Planning & epics: `docs/ROADMAP.md` (current and future work, including time-travel and upcoming epics).
- Architecture docs: `docs/architecture/` (time-travel, expression extensions, classes, edge time bins, engine semantics layer, overlays).

---

## Contributing

FlowTime is AI-first. Code changes and milestone specs are authored by AI workflows and must follow `docs/development/` and `.github/copilot-instructions.md`.

- Humans may contribute by writing or improving documentation.
- For code changes, open a discussion or issue describing the desired behavior; AI will author the milestone spec and implementation.
- Do not submit human-written code PRs; they will be declined.

---

## License

MIT. See [`LICENSE`](LICENSE).
