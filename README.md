# FlowTime-Sim

![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)

> FlowTime-Sim is a spec-driven synthetic data generator for FlowTime. It produces realistic event streams and "Gold" series to power demos, CI, and what-if experiments when real telemetry is unavailable.

---

## Table of contents

- Overview
- Repository layout
- Quickstart
- Devcontainer
- CI & tasks
- Docs & roadmap

---

## Overview

FlowTime-Sim generates arrivals, routing, retries, and timing distributions consistent with domains like logistics, transport, manufacturing, and cloud systems. It can emit NDJSON events, Gold series (Parquet/CSV), or PMFs for shape-first modeling.

See the high-level plan in `docs/ROADMAP.md`.

### Current status (Milestone M0)

The initial model-driven slice is available:

- Read a FlowTime YAML model.
- Call FlowTime API `/run`.
- Write results as CSV or JSON.

Details live in `docs/milestones/M0.md`.

## Repository layout

```
flowtime-sim-vnext/
	src/
		FlowTime.Sim.Core/
	tests/
		FlowTime.Sim.Tests/
	docs/
		ROADMAP.md
	.devcontainer/
	.github/workflows/
	.vscode/
	FlowTimeSim.sln
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

Run the SIM-M0 CLI against the example model (ensure FlowTime API is running; see Usage):

```bash
dotnet run --project src/FlowTime.Sim.Cli -- \
	--model examples/m0.const.yaml \
	--flowtime http://flowtime-api:8080 \
	--out out/m0.csv \
	--format csv
```

## Devcontainer

This repo includes a devcontainer with the .NET 9 SDK and GitHub CLI. Open in a container to get a consistent environment. See `.devcontainer/devcontainer.json`.

## CI & tasks

- GitHub Actions workflow runs build and tests on PRs to main.
- VS Code tasks:
	- build: `dotnet build`
	- test: `dotnet test`
	- Run SIM-M0 example (localhost or container API)

---

## Usage (SIM-M0 CLI)

Start FlowTime API (from the sibling repo) and run the example:

- From container-to-container:
	- API URL: `http://flowtime-api:8080`
- From host to devcontainer: `http://localhost:8080`

Example run:

```bash
dotnet run --project src/FlowTime.Sim.Cli -- \
	--model examples/m0.const.yaml \
	--flowtime http://flowtime-api:8080 \
	--out out/m0.csv \
	--format csv
```

Notes
- Requests use `Content-Type: text/plain` (YAML) and expect JSON.
- Culture-invariant CSV formatting.
- API errors are surfaced as `InvalidOperationException` with the `{ error }` message.

## Docs & roadmap

- Roadmap: `docs/ROADMAP.md`.
- Testing: `docs/testing.md` (stub).
- Branching strategy: `docs/branching-strategy.md` (stub).
 - Milestone M0 details: `docs/milestones/M0.md`.
 - Release notes: `docs/releases/M0.md`.

## License

MIT. A `LICENSE` may be added later.

