# FlowTime-Sim

![Build](https://github.com/23min/FlowTime-Sim/actions/workflows/build.yml/badge.svg?branch=main)
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

### Current status (Milestone SIM-M1 – In Progress)

SIM-M0 established deterministic arrivals with Gold/event outputs. SIM-M1 adds:

- `schemaVersion: 1` (validated; legacy specs accepted with warning as version 0).
- RNG hardening: PCG32 default (`rng: legacy` opt-out for one milestone).
- Metadata manifest (`metadata.json`) with SHA256 hashes & reproducibility metadata.
- Service time spec scaffold (`service` block: const | exp) parsed & validated (no runtime effect yet).
- Adapter parity harness tests verifying:
	- Gold arrivals match engine-evaluated demand series.
	- Aggregated events timestamps align with Gold counts (including zero bins).
	- Manifest integrity and basic hash sanity.
	- Negative guard (deliberate mismatch detected).

See `docs/milestones/SIM-M1.md` for phase breakdown and acceptance criteria.

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

Run the simulator CLI against the example model (ensure FlowTime API is running if using engine mode; see Usage):

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

## Usage (Simulator CLI)

Start FlowTime API (from the sibling repo) and run the example:

- From container-to-container:
	- API URL: `http://flowtime-api:8080`
- From host to devcontainer: `http://localhost:8080`

Example (const arrivals) run:

```bash
dotnet run --project src/FlowTime.Sim.Cli -- \
	--model examples/m0.const.yaml \
	--flowtime http://flowtime-api:8080 \
	--out out/m0.csv \
	--format csv
```

Outputs (default):

```
events.ndjson   # per-arrival NDJSON
gold.csv        # aggregated per-bin counts (arrivals==served in SIM-M1)
metadata.json   # manifest (schemaVersion, seed, rng, hashes)
```

Manifest example snippet:

```json
{
	"schemaVersion": 1,
	"seed": 12345,
	"rng": "pcg",
	"events": { "path": "events.ndjson", "sha256": "..." },
	"gold": { "path": "gold.csv", "sha256": "..." },
	"generatedAt": "2025-08-27T12:34:56Z"
}
```

Notes:
- Requests use `Content-Type: text/plain` (YAML) and expect JSON for engine mode.
- Culture-invariant CSV formatting.
- Determinism: identical spec (including seed & rng) ⇒ identical hashes.
- API errors surface as `InvalidOperationException` (exit code 1) or validation errors (exit code 2).

### Minimal spec (SIM-M1)
```yaml
schemaVersion: 1
grid: { bins: 3, binMinutes: 60, start: 2025-01-01T00:00:00Z }
seed: 123
arrivals: { kind: const, values: [4,5,6] }
route: { id: nodeA }
```

### Service time scaffold (future effect)
```yaml
service:
	kind: exp
	rate: 2.5
```

### Parity harness (Phase 5)
Tests (see `AdapterParityTests`) run the simulator twice to assert reproducibility, reconstruct an engine `ConstSeriesNode` graph, and compare:
- Gold arrivals vs engine series.
- Event aggregation vs Gold counts.
- Manifest structure & hashes.

Run tests:
```bash
dotnet test
```

### Integration (Live API) testing (opportunistic local / opt-in CI)

FlowTime-Sim can exercise a running FlowTime API (`/run`) via **live API tests** (`LiveApiTests`).

Behavior:
- Local (not GitHub Actions): tests attempt to run by default if the API is reachable; otherwise they skip with a message.
- CI (GitHub Actions): tests are skipped by default to keep PR runs fast/flaky-free.
- Overrides:
	- `RUN_LIVE_API_TESTS=1` (local or CI) → force attempt; fail loud if unreachable.
	- `RUN_LIVE_API_TESTS=0` → force skip.

Why:
- Detect accidental contract drift (grid shape, ordering, series lengths).
- Validate deterministic responses from the API over multiple calls.
- Surface parsing/validation differences early.

How it works:
- Tests attempt to resolve a base URL (env `FLOWTIME_API_BASE` first, else common candidates: `flowtime-api:8080`, `flowtime-api:5000`, `localhost:8080`, `localhost:5000`).
- They probe `/healthz` before invoking `/run`.
- A custom header `X-Live-Test: flowtime-sim` is added so you can confirm calls in API logs.
- If forced (`RUN_LIVE_API_TESTS=1`) and not reachable → fail. Otherwise (opportunistic local) they skip with a reason (`API not reachable`).

Force enable (bash):
```bash
export RUN_LIVE_API_TESTS=1
export FLOWTIME_API_BASE=http://flowtime-api:8080   # or http://localhost:8080
dotnet test --filter LiveApiTests
```

Force enable (PowerShell):
```powershell
$Env:RUN_LIVE_API_TESTS = '1'
$Env:FLOWTIME_API_BASE = 'http://localhost:8080'
dotnet test --filter LiveApiTests
```

Starting the API on a fixed port:
```bash
dotnet run --project ../flowtime-vnext/apis/FlowTime.API --urls http://0.0.0.0:8080
```

Container networking tips:
- If using separate devcontainers, attach both to the same Docker network (e.g. `flowtime-dev`).
- Or use host mapping: add `--add-host=host.docker.internal:host-gateway` and set `FLOWTIME_API_BASE=http://host.docker.internal:8080`.

When to run / rely on:
- Every local `dotnet test` (you get automatic coverage if API is up).
- Force before tagging a release (`RUN_LIVE_API_TESTS=1`) to ensure a hard failure if misconfigured.
- After API changes to `/run` or YAML parsing.
- Periodically in a nightly job (set `RUN_LIVE_API_TESTS=1`) to catch regressions without slowing PR CI.

Future hardening ideas:
- Schema hash snapshot & diff.
- Latency budget assertions.
- Unified spec translation (simulation spec → engine YAML) prior to API call.

## Docs & roadmap

- Roadmap: `docs/ROADMAP.md`.
- Contracts & spec: `docs/contracts.md`.
- Metadata manifest: `docs/metadata-manifest.md`.
- Testing strategy & parity harness: `docs/testing.md`.
- Branching strategy: `docs/branching-strategy.md`.
- Milestones: `docs/milestones/` (SIM-M0, SIM-M1).
- Releases: `docs/releases/`.

## License

MIT. A `LICENSE` may be added later.

