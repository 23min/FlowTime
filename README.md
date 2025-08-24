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

Note: No CLI/API yet; SIM-M0 will add a basic runner and sample specs.

## Devcontainer

This repo includes a devcontainer with the .NET 9 SDK and GitHub CLI. Open in a container to get a consistent environment. See `.devcontainer/devcontainer.json`.

## CI & tasks

- GitHub Actions workflow runs build and tests on PRs to main.
- VS Code tasks:
	- build: `dotnet build`
	- test: `dotnet test`

## Docs & roadmap

- Roadmap: `docs/ROADMAP.md`.
- Testing: `docs/testing.md` (stub).
- Branching strategy: `docs/branching-strategy.md` (stub).

## License

MIT. A `LICENSE` may be added later.

