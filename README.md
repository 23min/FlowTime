# FlowTime

[![Build](https://github.com/23min/FlowTime/actions/workflows/build.yml/badge.svg)](https://github.com/23min/FlowTime/actions/workflows/build.yml)
![License: Apache 2.0](https://img.shields.io/badge/License-Apache_2.0-blue.svg)
![doc-health](docs/badges/doc-health.svg)
![doc-correctness](docs/badges/doc-correctness.svg)

FlowTime is a modeling engine for service flows. You describe a system — services, queues, arrival patterns, dependencies, capacity — and FlowTime computes what happens over time: where work accumulates, when queues grow, how delays propagate, and why.

---

## Why not just dashboards?

Observability tools are good at telling you *what* happened: CPU spiked at 14:23, p99 latency crossed 500ms, the backlog hit 10,000 items. What they can't tell you is *why it propagated*, or what would happen under different conditions.

You can't ask Grafana: "If arrivals to service A increase 20%, when does service C's queue start growing?" You can't replay last Tuesday's incident with more capacity and see whether the backlog clears. You can't compare two staffing plans side-by-side and see which one creates a bottleneck at 3pm.

Dashboards show metrics. FlowTime models the mechanics that produce them.

---

## How it works

You describe a system as a **network of services, queues, and dependencies**. FlowTime evaluates it over a discrete time grid — one time bin at a time, in topological order, like a spreadsheet recalculating its cells. Change an input and every downstream cell updates.

For each time bin the engine computes arrivals, served, queue depth, errors, retry rates, utilization, latency, and more. The result is a complete time-series picture of how the system behaved — or would behave under a proposed change.

**A concrete example.** Three services in sequence: Intake, Processing, Dispatch. Arrivals to Intake spike from 100/hr to 300/hr at minute 30. Processing has capacity for 200/hr. FlowTime shows: Processing's queue starts growing at minute 30, reaches 500 items by minute 60, Dispatch sees delayed arrivals, and total cycle time doubles. All computed analytically — no simulation, no sampling, no waiting.

Feed it telemetry from your production systems, or author a model from scratch. The engine treats both the same way.

**Key properties:**

- **Deterministic.** Same model, same data, same outputs. Always. Reproducible replays, comparable scenarios.
- **Fast.** Analytical computation in milliseconds, not hours of discrete-event simulation. The evaluation core is written in Rust.
- **Explainable.** Every metric is a traceable formula. There is no black box.
- **Programmable.** Parameter sweeps, sensitivity analysis, goal-seeking, and optimization are built in. The engine is a callable function: model + parameters in, time series out.

See [engine capabilities](docs/reference/engine-capabilities.md) and [flow theory foundations](docs/reference/flow-theory-foundations.md) for depth.

---

## Who it's for

- **SRE / Reliability** — diagnose bottlenecks, backlog growth, retry storms, and capacity shortfalls before they page you.
- **Engineering / Platform** — model the downstream flow impact of a system change before deploying it.
- **Operations / Support** — explain why queues grew or SLAs dipped during specific windows, with reproducible evidence.
- **Process Optimization** — compare as-is vs to-be flows; identify constraints and model interventions.
- **Business / Exec** — throughput, delay, and risk at a high level, from a single shared model.

---

## FlowTime as Flow Literacy

FlowTime is more than an engine. It is a common language for talking about flows and resilience — one that is computable rather than conversational.

When a team can load the same deterministic model, run it against historical telemetry, and read the same time series, the conversation about bottlenecks, recovery times, and capacity risk becomes concrete. SREs, operations, engineering, and business stakeholders can reason from the same artifacts rather than from different dashboards with different definitions of utilization and latency.

The core concepts — flows, services, queues, classes, paths — and the analytics built from them — throughput, queue depth, cycle time, utilization, backlog risk, retry amplification — are not proprietary metrics. They are the vocabulary of queueing theory and flow analysis, made directly executable. Like a shared spreadsheet where everyone sees the same formulas and the same numbers, and "what if" is a cell edit away.

---

## At a Glance

| Surface | Purpose |
|---------|---------|
| **Engine** | Deterministic evaluation core (Rust), artifact registry, REST API, CLI |
| **Time Machine** | Headless analysis pipeline — parameter sweeps, sensitivity, goal-seeking, optimization |
| **Sim** | Template-driven model authoring and synthetic demand generation |
| **UI** | Interactive flow visualization and what-if exploration (Svelte) |

**Shipped:** interactive what-if (parameter manipulation with real-time topology updates), parameter sweeps, sensitivity analysis, goal-seeking, multi-parameter optimization (Nelder-Mead), pipeable JSON CLI for all analysis modes.

**On the horizon:** model discovery (fit parameters to observed telemetry automatically), telemetry ingestion from production systems, process mining from event logs.

---

## Repository Layout

```
flowtime-vnext/
├─ src/
│  ├─ FlowTime.Core/               # Engine execution core
│  ├─ FlowTime.API/                # Engine HTTP API (:8081)
│  ├─ FlowTime.Cli/                # Engine CLI
│  ├─ FlowTime.Contracts/          # Shared models and schemas
│  ├─ FlowTime.Adapters.Synthetic/ # Synthetic data adapters
│  ├─ FlowTime.Expressions/        # Expression language
│  ├─ FlowTime.TimeMachine/        # Headless analysis pipeline
│  ├─ FlowTime.Sim.Core/           # Simulation templates and provenance
│  ├─ FlowTime.Sim.Service/        # Simulation HTTP API (:8090)
│  ├─ FlowTime.Sim.Cli/            # Simulation CLI utilities
│  └─ FlowTime.UI/                 # Blazor WebAssembly UI (:5219)
├─ tests/                          # Engine and Sim test projects
├─ docs/                           # Architecture reference, guides, schemas
├─ work/                           # Epics, milestones, decisions, gaps
├─ templates/                      # Simulation templates
├─ examples/                       # Example models
├─ catalogs/                       # Scenario catalogs and sample systems
├─ .devcontainer/                  # Dev container setup
├─ .github/                        # CI workflows
├─ ROADMAP.md                      # High-level roadmap
└─ FlowTime.sln                    # Solution file
```

---

## Quickstart

### Prerequisites

The recommended way to work with FlowTime is via the included **Dev Container** — it provides .NET 9, the Rust toolchain, and all required extensions pre-configured. See [`docs/development/devcontainer.md`](docs/development/devcontainer.md).

Without the Dev Container you will need .NET 9 SDK, a Rust toolchain, and Git set up manually.

### Build and test

```bash
dotnet restore
dotnet build FlowTime.sln
dotnet test FlowTime.sln
```

### Run the Engine

```bash
# Start the Engine API on http://localhost:8081
dotnet run --project src/FlowTime.API

# Run a model via CLI
dotnet run --project src/FlowTime.Cli -- run examples/m0.const.yaml --out out/m0

# Launch the UI (http://localhost:5219)
dotnet run --project src/FlowTime.UI
```

### Run the Simulation surface

```bash
# Start the Sim API on http://localhost:8090
ASPNETCORE_URLS=http://0.0.0.0:8090 dotnet run --project src/FlowTime.Sim.Service

# Generate a model from a template
dotnet run --project src/FlowTime.Sim.Cli -- generate --id transportation-basic --out out/model.yaml
```

---

## Documentation

| Topic | Location |
|-------|----------|
| What FlowTime is and how to think about models | [`docs/flowtime.md`](docs/flowtime.md) |
| Engine capabilities (shipped behavior) | [`docs/reference/engine-capabilities.md`](docs/reference/engine-capabilities.md) |
| Flow theory foundations | [`docs/reference/flow-theory-foundations.md`](docs/reference/flow-theory-foundations.md) |
| Architecture and engine design | [`docs/architecture/whitepaper.md`](docs/architecture/whitepaper.md) |
| Modeling guide | [`docs/modeling.md`](docs/modeling.md) |
| CLI reference | [`docs/guides/CLI.md`](docs/guides/CLI.md) |
| Dev container setup | [`docs/development/devcontainer.md`](docs/development/devcontainer.md) |
| Roadmap | [`ROADMAP.md`](ROADMAP.md) |

---

## Collaboration

FlowTime is not currently accepting unsolicited code contributions.

What we are looking for are **collaborators with real systems to model** — SREs, operations teams, and process engineers who want to test FlowTime against actual telemetry or explore what-if scenarios for a live system. If that describes you, open a [discussion](https://github.com/23min/FlowTime/discussions) or reach out directly.

For documentation improvements, open an issue describing what is unclear or missing.

---

## License

Apache License 2.0. See [`LICENSE`](LICENSE).
