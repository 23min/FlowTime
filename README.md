# FlowTime

[![Build](https://github.com/23min/FlowTime/actions/workflows/build.yml/badge.svg)](https://github.com/23min/FlowTime/actions/workflows/build.yml)
![License: Apache 2.0](https://img.shields.io/badge/License-Apache_2.0-blue.svg)

**A deterministic flow algebra engine. A spreadsheet for flow dynamics. Queueing theory made executable.**

Traditional monitoring tools measure symptoms — high latency, growing backlogs, missed SLAs. FlowTime models the mechanics: how work arrives, moves through services, accumulates in queues, and propagates delays forward in time. It produces stable, explainable, time-series outputs that teams can replay, compare, and reason about together.

---

## FlowTime as Flow Literacy

FlowTime is more than an engine. It is a common language for talking about flows and resilience — one that is computable rather than conversational.

When a team can load the same deterministic model, run it against historical telemetry, and read the same time series, the conversation about bottlenecks, recovery times, and capacity risk becomes concrete. SREs, operations, engineering, and business stakeholders can reason from the same artifacts rather than from different dashboards with different definitions of utilization and latency.

This shared understanding rests on clear core concepts — flows, services, queues, classes, paths — and a set of reusable analytics built from them: throughput, queue depth, cycle time, utilization, backlog risk, retry amplification. These are not proprietary metrics. They are the vocabulary of queueing theory and flow analysis, made directly executable.

---

## What FlowTime does

FlowTime represents a system as a **network of services, queues, and dependencies** and evaluates it over a **discrete time grid** — one bin at a time, in topological order, like a spreadsheet recalculating its cells.

For each time bin the engine computes: arrivals, served, queue depth, errors, retry rates, utilization, latency, and more. The result is a complete time-series picture of how the system behaved — or would behave under a proposed change.

Key properties:

- **Deterministic.** Same model, same data → same outputs, always. Reproducible replays, comparable scenarios.
- **Fast.** Analytical computation in milliseconds, not the hours of discrete-event simulation. The performance-critical evaluation core is written in Rust — chosen for its predictable performance and memory safety guarantees.
- **Grounded.** Feed it telemetry from your existing systems or author a model from scratch. The engine treats both the same way.
- **Explainable.** Every metric is a traceable formula. There is no black box.

See [engine capabilities](docs/reference/engine-capabilities.md) and [flow theory foundations](docs/reference/flow-theory-foundations.md) for depth.

---

## Who it's for

- **SRE / Reliability** — diagnose bottlenecks, backlog growth, retry storms, and capacity shortfalls.
- **Engineering / Platform** — validate system changes and understand their downstream flow impact.
- **Operations / Support** — explain why queues grew or SLAs dipped during specific windows.
- **Process Optimization** — compare as-is vs to-be flows; identify constraints and model interventions.
- **Business / Exec** — throughput, delay, and risk at a high level, without dashboard stitching.

---

## On the horizon

- **Interactive what-if** — manipulate model parameters and see the system's state update in real time. Arriving now.
- **Model discovery** — fit model parameters to observed telemetry to produce a calibrated, grounded model automatically.
- **Process mining** — derive flow topology from event logs rather than authoring it by hand.

---

## At a Glance

| Surface | Purpose |
|---------|---------|
| **Engine** | Deterministic evaluation, artifact registry, REST API, CLI |
| **Time Machine** | Headless analysis pipeline — parameter studies, sensitivity, goal-seeking |
| **Sim** | Template-driven model authoring and synthetic demand generation |
| **UI** | Interactive flow visualization and what-if exploration |

Why FlowTime:

- Every metric is a labeled formula — explainable and auditable, not opaque ML output.
- Deterministic replay — run a scenario today, get the same answer next month.
- Fast analytical evaluation — not discrete-event simulation; models run in milliseconds.
- Grounded in real telemetry — bring existing data or generate synthetic demand.
- One model, one vocabulary, many stakeholders.

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
