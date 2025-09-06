# FlowTime-Sim

![Build](https://github.com/23min/FlowTime-Sim/actions/workflows/build.yml/badge.svg?branch=main)
![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)

> FlowTime-Sim is a spec-driven synthetic data generator for FlowTime. It produces deterministic per-series time series packs and (optionally) event streams to power demos, CI, and what-if experiments when real telemetry is unavailable.

---

## Table of contents

- Overview
- Repository layout
- Quickstart
- Devcontainer
- CI & tasks
- Usage
- Docs & roadmap

---

## Overview

FlowTime-Sim generates arrivals and related measures consistent with domains like logistics, manufacturing, and cloud systems. It emits a **run artifact pack** consisting of JSON metadata and per-series CSV files. Event enrichment, Parquet Gold tables, and service endpoints are deferred to later milestones.

See the high-level plan in `docs/ROADMAP.md`.

### Current status (Milestone SIM-M2 – Artifact Parity & Series Index)

SIM-M2 focuses on a stable artifact layout (dual JSON + index + per-series CSVs) with deterministic hashing & integrity checks. Removed legacy `metadata.json` and single `gold.csv` patterns.

Artifacts per run (optional `events.ndjson`):
```
runs/<runId>/
  run.json
  manifest.json
  series/
    <seriesId>.csv
  series/index.json
  [events.ndjson]
```
- runId format: `sim_YYYY-MM-DDTHH-mm-ssZ_<8slug>` (opaque; do not parse).
- Per-series CSV schema: `t,value` where `t` = 0..(bins-1).
- `series/index.json` enumerates all series (id, kind, unit, path, hash, points).
- `manifest.json` lists per-series SHA-256 hashes (`sha256:<64hex>`); `run.json` currently mirrors it (future semantic divergence reserved).

### Model YAML Compatibility

FlowTime-Sim uses the same YAML model format as [FlowTime](https://github.com/23min/FlowTime) but handles determinism differently:

- **FlowTime-Sim**: Requires `seed` and `rng` fields for deterministic synthetic data generation
- **FlowTime**: Always deterministic - ignores `seed` and `rng` fields if present

This means you can share models between both engines. FlowTime-Sim uses the randomness fields for stochastic simulation, while FlowTime provides purely deterministic flow modeling without randomness.

## Repository layout

```
flowtime-sim-vnext/
  src/
    FlowTime.Sim.Core/
  tests/
    FlowTime.Sim.Tests/
  docs/
    ROADMAP.md
    contracts.md
```

## Quickstart

Prereqs: .NET 9 SDK, Git.

```bash
# restore & build
dotnet restore
dotnet build

# run unit tests
dotnet test
```

Run the simulator CLI against the example spec:

```bash
dotnet run --project src/FlowTime.Sim.Cli -- --model examples/m0.const.yaml --out runs
```

After completion, inspect the newest `runs/<runId>/` directory.

## Devcontainer

This repo includes a devcontainer with the .NET 9 SDK and GitHub CLI for consistent development across local and Codespaces environments. The container supports cross-repository development with the main FlowTime API.

For detailed setup instructions, see [`docs/development/devcontainer.md`](docs/development/devcontainer.md).

## CI & tasks

- GitHub Actions workflow runs build and tests on PRs to main.
- VS Code tasks:
  - build: `dotnet build`
  - test: `dotnet test`
  - Run SIM-M0 example (legacy example; will be updated to SIM-M2 artifacts)
- Development scripts: See `scripts/README.md` for API testing, configuration validation, and debugging tools.

---

## Usage (Simulator CLI)

Minimal constant-arrivals spec:
```yaml
schemaVersion: 1
grid: { bins: 3, binMinutes: 60, start: 2025-01-01T00:00:00Z }
seed: 123
arrivals: { kind: const, values: [4,5,6] }
route: { id: nodeA }
```
Run:
```bash
dotnet run --project src/FlowTime.Sim.Cli -- --model examples/m0.const.yaml --out runs
```
Outputs (SIM-M2):
```
run.json         # run summary (schemaVersion, runId, grid, hashes via series listing)
manifest.json    # integrity document (currently identical to run.json)
series/index.json# discovery (series list, units, per-series hashes)
series/*.csv     # canonical per-series time series
[events.ndjson]  # optional events (may be absent)
```
Determinism: identical spec (including seed & rng) ⇒ identical per-series CSV bytes and hashes.

## Docs & roadmap

- **CLI Guide**: [`docs/guides/CLI.md`](docs/guides/CLI.md) - Complete command-line usage and examples
- **Configuration**: [`docs/guides/configuration.md`](docs/guides/configuration.md) - Setup and configuration guide
- **Roadmap**: [`docs/ROADMAP.md`](docs/ROADMAP.md) - Project roadmap and future plans
- **Development**: [`docs/development/`](docs/development/) - Development setup, testing, and devcontainer guides
- **Reference**: [`docs/reference/`](docs/reference/) - Technical contracts and specifications
- **Milestones**: [`docs/milestones/`](docs/milestones/) - SIM-M0, SIM-M1, SIM-M2 milestone tracking
- **Releases**: [`docs/releases/`](docs/releases/) - Release notes and documentation

## License

MIT. A `LICENSE` may be added later.

