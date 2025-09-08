# FlowTime

[![Build](https://github.com/23All features are callable via the **HTTP API** first; CLI and UI consume the same surface.

## Repository layoutactions/workflows/build.yml/badge.svg)](https://github.com/23min/FlowTime/actions/workflows/build.yml)
![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)

> **FlowTime** is a deterministic, discrete-time, graph-based engine that models flows (entities) across services and queues, producing explainable time-series for backlog, latency, and throughput—useful for **what-if** (simulation) and **what-is/what-was** (time-travel observability). It feels **spreadsheet-like**, with a lightweight **SPA UI** for interactive analysis.

---

## Table of contents

- Overview
- Repository layout  
- Quickstart
- Current status
- Usage
- Docs & roadmap
- Contributing
- License

---

## Overview

FlowTime provides **explainable flow modeling** and **time-travel observability** without heavyweight simulation tooling. Designed for SREs, platform engineers, data/ops analysts, and product owners who want "spreadsheet-like" clarity with reproducible runs and CSV outputs.

### Key features

* **What-if**: model scenarios like outages, surges, reroutes, retry storms
* **What-is/what-was**: visualize system state and history for incident forensics, SLA monitoring, and business flow impact
* **Unification**: single source of truth that maps technical telemetry into business-relevant flows
* **Scalability**: run in seconds on 100+ nodes × months of telemetry, suitable for interactive analysis
* **Ownership**: lightweight codebase, no costly DES licenses, extensible by your team

### Architecture

**Monorepo** with API-first design:

* **FlowTime.Core** — the engine: canonical time grid, series math, DAG, nodes/evaluation
* **FlowTime.Cli** — CLI that evaluates YAML models and writes CSV artifacts
* **FlowTime.API** — HTTP API for graph/run operations with health monitoring
* **FlowTime.UI** — Blazor WASM SPA for interactive analysis and visualization

All features are callable via the **HTTP API** first; CLI and UI consume the same surface.
  * [Real‑time](#real-time)
  * [CLI reference (M0)](#cli-reference-m0)
* [Milestone M0 scope](#milestone-m0-scope)
* [What’s next (milestone ladder)](#whats-next-milestone-ladder)
* [CI/CD & deployment](#cicd--deployment)
* [Data & formats](#data--formats)
* [Concepts](docs/concepts/nodes-and-expressions.md)
* [CLI Guide](docs/CLI.md)
* [Docs & roadmap](#docs--roadmap)
* [Contributing](#contributing)
* [License](#license)
* [Trademark](#trademark)

---

## Who is this for?

Teams who need **explainable flow modeling** and **time-travel observability**—without heavyweight simulation tooling. SREs, platform eng, data/ops analysts, and product owners who want “spreadsheet-like” clarity with reproducible runs and CSV outputs.

## Design principles

### API‑First

* All features are callable via the **HTTP API** first.
* CLI, UI, and automation layers consume the same API surface.
* Ensures end‑to‑end validation from day one. *(Initial hosting may use **Azure Functions**, but the API is host‑agnostic and swappable.)*

### Spreadsheet Metaphor

* Deterministic, **grid‑aligned** evaluation.
* Cells ≈ time‑bins; formulas = **expressions**, **PMFs**, and built‑ins.
* Graph nodes reference each other like spreadsheet cells.

### Visualization Early

* UI is introduced early, not deferred.
* Basic charts validate the model, aid debugging, and improve adoption.

### Expressions as Core

* `expr:` fields make FlowTime “spreadsheet‑y”.
* Roles: **modeling** dependencies & math; **lineage** across nodes; **teaching/demo** for non‑experts.

### Probabilistic Mass Functions (PMFs)

* Optional approximation for arrivals/attempts.
* Replaceable by real telemetry; telemetry can also be reduced to PMFs.
* Scope: start with **expected‑value series**; later add **convolution/propagation**.

---

## Architecture

**Monorepo** with these components:

* **FlowTime.Core** — the engine: canonical time grid, series math, DAG, nodes/evaluation. *(present in M0)*
* **FlowTime.Cli** — a CLI that evaluates YAML models and writes CSV. *(present in M0)*
* **FlowTime.UI** — SPA (Blazor WASM) to visualize runs. *(early demo present)*
* **FlowTime.API** — backend for graph/run/state (also hosts negotiate for real-time). *(early minimal version present)*

**API‑first**: All features are exposed via the HTTP API; CLI and UI consume the same surface. The API will be hosted behind a neutral "FlowTime.API" service (initially Azure Functions is a likely host, but the implementation is swappable). Endpoints: negotiate, `/graph`, `/run`, `/state_window`.

### Hosting options (FlowTime.API)

FlowTime.API is host‑agnostic. Pick the option that fits your platform; the engine stays the same and only a thin adapter changes.

- Azure Functions (HTTP triggers): quick local dev, scale‑to‑zero, serverless ergonomics.
- ASP.NET Core minimal APIs (Kestrel or container): portable, simple, no platform dependency.
- Containers/orchestrators: package either host in a container for Kubernetes/App Service/etc.

Swappability contract: HTTP surface and DTOs remain identical regardless of host; only wiring and deployment differ.

**Real‑time** (planned) is provider‑agnostic: the UI calls a **negotiate** endpoint to obtain a temporary WebSocket pub/sub URL; implementations can swap behind an abstraction (Web PubSub, SignalR, SSE).

**Data** starts with **CSV**; artifacts can optionally persist in **blob storage**. Cloud data layers (**ADLS Gen2**, **S3**) are planned via a storage provider abstraction.

**Auth**: **KISS (anonymous)** for dev/demo; enterprises can enable **Entra ID** later without changing the core.

---

## Repository layout

```
flowtime-vnext/
├─ src/FlowTime.API/              # HTTP API with health monitoring
├─ src/FlowTime.Core/             # Engine (grid, graph, nodes)
├─ src/FlowTime.Cli/              # CLI driver
├─ ui/FlowTime.UI/                # Blazor WASM SPA
├─ tests/                         # Core + API tests
├─ docs/                          # Roadmap, contracts, schemas, concepts
├─ examples/hello/                # Sample model
└─ FlowTime.sln
```

---

## Quickstart

### Local development

**Prereqs**: .NET **9** SDK, Git.

```powershell
# restore & build
dotnet restore
dotnet build

# run unit tests
dotnet test

# run the example model (writes out/hello/served.csv)
dotnet run --project src/FlowTime.Cli -- run examples/hello/model.yaml --out out/hello

# peek at the CSV (first lines)
Get-Content out/hello/served.csv | Select-Object -First 5
```

Bash equivalent:

```bash
head -n 5 out/hello/served.csv
```

## Running and calling the API

You can run the minimal API locally from this repo and call it over HTTP.

- VS Code: Run and Debug → "FlowTime.API" (F5). It binds to `http://0.0.0.0:8080` inside the container or `http://localhost:5091` per launch settings when run on the host.
- CLI: from the repo root:
  - `dotnet run --project src/FlowTime.API --urls http://0.0.0.0:8080`
  - or hot reload: `dotnet watch --project src/FlowTime.API run --urls http://0.0.0.0:8080`

Call the API (examples use the shared network name `flowtime-api`; use `localhost:8080` from host):

```bash
curl -s http://flowtime-api:8080/healthz

cat > /tmp/model.yaml << 'YAML'
grid: { bins: 4, binMinutes: 60 }
nodes:
  - id: demand
    kind: const
    values: [10,20,30,40]
  - id: served
    kind: expr
    expr: "demand * 0.8"
outputs:
  - series: served
    as: served.csv
YAML

curl -s -X POST http://localhost:8080/v1/run \
  -H "Content-Type: text/plain" \
  --data-binary @/tmp/model.yaml | jq .
```

### Model YAML Compatibility

FlowTime uses the same YAML model format as [FlowTime-Sim](https://github.com/23min/FlowTime-Sim) but handles determinism differently:

- **FlowTime**: Always deterministic - ignores `seed` and `rng` fields if present
- **FlowTime-Sim**: Requires `seed` and `rng` fields for deterministic synthetic data generation

This means you can share models between both engines.

## Current status

| Milestone | Description | Status |
|-----------|-------------|--------|
| **M0** | Minimal deterministic engine with CLI and CSV export | ✅ Completed |
| **M1** | Contracts parity with structured artifacts and schema validation | ✅ Completed |
| **SVC-M0** | HTTP API with `/run`, `/graph`, `/healthz` endpoints | ✅ Completed |
| **SVC-M1** | Artifact serving API with run data access endpoints | ✅ Completed |
| **SYN-M0** | Synthetic adapter for reading FlowTime-Sim and CLI artifacts | ✅ Completed |
| **UI-M0** | Blazor WASM SPA with API integration and simulation mode | ✅ Completed |
| **UI-M1** | Template-based simulation runner with dynamic forms | ✅ Completed |
| **UI-M2** | Health monitoring, API versioning, and real API integration | ✅ Completed |

## Usage

### CLI

```bash
# Evaluate a YAML model and generate artifact set
dotnet run --project src/FlowTime.Cli -- run <path/to/model.yaml> --out out/<name> --verbose

# Options
# --out out/run1                    # output directory
# --verbose                         # print evaluation summary + schema validation results
# --deterministic-run-id            # stable runId for testing/CI (based on scenario hash)
# --seed 42                         # RNG seed for reproducible results
```

## Docs & roadmap

- **CLI Guide**: [`docs/CLI.md`](docs/CLI.md) - Complete command-line usage and examples
- **UI Documentation**: [`docs/UI.md`](docs/UI.md) - UI setup, configuration, and usage
- **Deployment Guide**: [`docs/deployment.md`](docs/deployment.md) - Production deployment options
- **Roadmap**: [`docs/ROADMAP.md`](docs/ROADMAP.md) - Project roadmap and milestone tracking
- **Development Setup**: [`docs/development-setup.md`](docs/development-setup.md) - Development environment setup
- **Configuration**: [`docs/configuration.md`](docs/configuration.md) - Configuration reference
- **Testing Strategy**: [`docs/testing.md`](docs/testing.md) - Testing approach and guidelines
- **Concepts**: [`docs/concepts/nodes-and-expressions.md`](docs/concepts/nodes-and-expressions.md) - Core modeling concepts
- **Contracts**: [`docs/contracts.md`](docs/contracts.md) - API contracts and specifications

## Contributing

We welcome issues and PRs.

1. Create a topic branch from `main`.
2. `dotnet test` must pass; keep analyzers/formatting clean.
3. Follow Conventional Commits (`feat:`, `fix:`, `docs:`, ...).
4. For larger changes, open an issue first to align on direction.

## License

**MIT** — permissive and simple. See [`LICENSE`](LICENSE).

