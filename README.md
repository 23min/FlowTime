# FlowTime

 [![Build](https://github.com/23min/FlowTime/actions/workflows/build.yml/badge.svg)](https://github.com/23min/FlowTime/actions/workflows/build.yml)
![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)

> **FlowTime** is a deterministic, discrete-time, graph-based engine that models flows (entities) across services and queues, producing explainable time-series for backlog, latency, and throughput—useful for **what-if** (simulation) and **what-is/what-was** (time-travel observability). It feels **spreadsheet-like**, with a lightweight **SPA UI** for interactive analysis.

---

## Highlights

* **What-if**: model scenarios like outages, surges, reroutes, retry storms.
* **What-is / what-was**: visualize the actual system state and history for incident forensics, SLA monitoring, and business flow impact.
* **Unification**: provide a *single source of truth* that maps technical telemetry into business-relevant flows.
* **Scalability**: run in seconds on 100+ nodes × months of telemetry, suitable for interactive analysis.
* **Ownership**: lightweight codebase, no costly DES licenses, extensible by your team.

---

## Table of contents

* [Who is this for?](#who-is-this-for)
* [Design principles](#design-principles)
* [Architecture](#architecture)
* [Repository layout](#repository-layout)
* [Quickstart](#quickstart)

  * [Local development](#local-development)
  * [Configuration & secrets](#configuration--secrets)
  * [Storage](#storage)
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

Current top-level structure (trimmed to primary source + docs):

```
FlowTime/
├─ src/FlowTime.API/              # Minimal API surface (healthz, run, graph)
├─ docs/                       # Roadmap, contracts, schemas, concepts, releases
│  ├─ schemas/                 # JSON Schemas: run, manifest, series-index
│  └─ concepts/
├─ examples/hello/             # Sample model
├─ src/
│  ├─ FlowTime.Core/           # Engine (grid, graph, nodes)
│  └─ FlowTime.Cli/            # CLI driver
├─ tests/
│  ├─ FlowTime.Tests/          # Core + contract tests
│  └─ FlowTime.Api.Tests/      # API slice tests
├─ ui/
│  ├─ FlowTime.UI/             # Blazor WASM SPA
│  └─ FlowTime.UI.Tests/       # UI tests (early)
├─ FlowTime.sln
└─ README.md
```

Planned future roots (not yet or partially present):

```
adapters/                     # Synthetic + telemetry adapters (SYN milestones)
infra/                        # Deployment & IaC
.github/workflows/            # CI/CD workflows
storage/                      # Pluggable storage providers
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

> API/UI are not part of M0. They’ll arrive as Functions (+ SPA) in later milestones.

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

curl -s -X POST http://flowtime-api:8080/run \
  -H "Content-Type: application/yaml" \
  --data-binary @/tmp/model.yaml | jq .
```

### Model YAML Compatibility

FlowTime uses the same YAML model format as [FlowTime-Sim](https://github.com/23min/FlowTime-Sim) but handles determinism differently:

- **FlowTime**: Always deterministic - ignores `seed` and `rng` fields if present
- **FlowTime-Sim**: Requires `seed` and `rng` fields for deterministic synthetic data generation

This means you can share models between both engines, but only FlowTime-Sim uses randomness fields for stochastic simulation. See [Model Schema Documentation](docs/concepts/nodes-and-expressions.md#model-yaml-schema-flowtime-vs-flowtime-sim) for details.

Tip (VS Code): use the preconfigured tasks

```text
Terminal > Run Task...
  - build         # runs dotnet build
  - test          # runs dotnet test
  - run: hello    # runs the CLI against examples/hello/model.yaml (writes out/hello)
```

### Configuration & secrets

### Run the UI (Blazor WASM demo + simulation mode)

The UI lets you invoke `/healthz`, `/run`, and `/graph` using a unified `IRunClient` abstraction that can point at the real API or an in‑browser deterministic simulation.

1. (Optional) Start the API locally (`http://localhost:8080`).
2. Build once:
  ```powershell
  dotnet build
  ```
3. Run the UI:
  ```powershell
  # Default development port (5219)
  dotnet run --project ui/FlowTime.UI
  
  # Or specify a custom port (recommended: 3000 for containers)
  dotnet run --project ui/FlowTime.UI --urls http://localhost:3000
  ```
4. Open the printed URL and go to the API Demo page.
5. Toggle the Sim/API switch in the top bar to swap between real network calls and synthetic instant results. Persisted in `localStorage` (or force with `?sim=1`).

#### UI Port Configuration
- **Development**: Default port 5219 (configured in `launchSettings.json`)
- **Production/Containers**: Recommended port 3000 (use `--urls` flag or `ASPNETCORE_URLS` environment variable)
- **Documentation**: See `docs/UI.md` for detailed port configuration options

Features:
* Sample YAML model (const + expression node).
* Health / Run / Graph buttons with spinners and timeouts (8s health, 12s run/graph).
* Snackbar errors; no silent fallbacks.
* Unified run output: `GraphRunResult { bins, binMinutes, order, series }`.
* NEW: Structural graph view (no series) via `GraphStructureResult { order, nodes[] }` showing:
  * Topological order index (#)
  * Each node's direct inputs (fan‑in)
  * Computed in/out degree, role chips (Source, Sink, Internal)
  * Aggregate stats (sources, sinks, max fan‑out)
* Simulation toggle (API vs deterministic in‑browser model) persisted in `localStorage` (override with `?sim=1` / `?sim=0`).

Hot reload:
```powershell
dotnet watch --project ui/FlowTime.UI run
```

Troubleshooting:
* 400 on Run: usually indentation—use provided sample YAML.
* API down? Switch to Sim mode to keep exploring.
* Timeouts surface as snackbar errors; check network/devtools.

Planned next: editable YAML textarea + persistence, charts, visual DAG, richer node kinds.

Structural graph vs run:

* Use **Run** when you want numeric time‑series for output nodes (series data is returned).
* Use **Graph** to validate model topology quickly (order, dependencies) before costly future features (visualization, large scenarios).

See also: [Capability Matrix](docs/capability-matrix.md) and [Node Concepts](docs/concepts/nodes-and-expressions.md).

Not applicable for M0 CLI runs. Future backend + SPA will use environment configuration and optional secret stores (e.g., Key Vault) as needed.

### Calling the API from another container

If you want to call FlowTime.API from a sibling container (e.g., flowtime-sim), join both to a shared Docker network and use `http://flowtime-api:8080`. Details and curl examples in `docs/devcontainer.md`.

### Storage

* **CSV** is first-class (models + outputs).
* **Blob storage** is optional and opt‑in for persisting runs/artifacts.
* **Later**: plug in **ADLS Gen2** or **S3** via the storage abstraction.

### Real‑time

Planned: **WebSocket pub/sub** with a **negotiate** step; alternatives like **SignalR** or **SSE** can fit behind an interface.

### CLI reference (M1)

```bash
# Evaluate a YAML model and generate M1 artifact set (run.json, manifest.json, series CSVs, etc.)
dotnet run --project src/FlowTime.Cli -- run <path/to/model.yaml> --out out/<name> --verbose

# M1 Options
# --out out/run1                    # output directory
# --verbose                         # print evaluation summary + schema validation results
# --deterministic-run-id            # stable runId for testing/CI (based on scenario hash)
# --seed 42                         # RNG seed for reproducible results
# --via-api http://localhost:7071   # route via API for parity testing (when available)
```

**M1 Output Structure:**
```
out/<name>/<runId>/
├── spec.yaml                           # original model (normalized)
├── run.json                            # run summary with series listing
├── manifest.json                       # hashes, RNG seed, integrity metadata  
├── series/
│   ├── index.json                      # series discovery & metadata
│   └── served@SERVED@DEFAULT.csv       # per-series data files
└── gold/                               # placeholder for analytics tables
```

Features:
* **Canonical hashing**: SHA-256 for scenarios and per-series data
* **Schema validation**: Automatic validation against JSON Schema files
* **Determinism**: Reproducible artifacts for CI/testing with `--deterministic-run-id` + `--seed`
* **SeriesId format**: `measure@componentId@class` per contracts specification

---

## Milestone M0 scope ✅ COMPLETED

Minimal useful slice implemented:

* Canonical grid and numeric Series types.
* DAG execution with **topological ordering** and **cycle detection**.
* Minimal node set: **constant series** and **binary Add/Mul** (supports scalar RHS).
* YAML → **evaluate** → **CSV export** via CLI.
* Tiny sample model (`examples/hello`) and unit tests.

## Milestone M1 scope ✅ COMPLETED  

**Contracts Parity** - Complete artifact generation system:

* **Structured artifacts**: `spec.yaml`, `run.json`, `manifest.json`, `series/index.json`, per-series CSVs
* **Canonical hashing**: SHA-256 for scenario/model and per-series data with YAML normalization
* **Schema validation**: JSON Schema validation for all artifact formats
* **Determinism**: `--deterministic-run-id` and `--seed` flags for reproducible CI/testing
* **SeriesId format**: `measure@componentId@class` specification compliance

Deferred to next milestones: backlog/queues, routing, autoscale; backend; SPA viewer; extended nodes (shift/resample/delay).

---

## What’s next (milestone ladder)

A high‑level view of upcoming work (details live in `docs/ROADMAP.md`):

* **M1 — Contracts Parity**: artifact freeze (`spec.yaml`, `run.json` w/ `source` & expanded `grid`, `manifest.json` w/ `rng` object, `series/index.json` + per‑series hashes, placeholders `events.ndjson`, `gold/`) + deterministic formatting & hashing.
* **M1.5 — Expressions**: parser + references + basic built‑ins (unblocks richer nodes & PMF composition).
* **M2 — PMF (expected value)**: basic PMF nodes with normalization warnings.
* **M3 — (reserved / potential hygiene)**: may absorb overflow from earlier milestones.
* **M7 — Backlog & latency**: queued later (single queue + Little's Law latency) per current roadmap.
* Further milestones: routing/capacity, scenarios/compare, synthetic adapters, backlog extensions, uncertainty (see full `docs/ROADMAP.md`).

> Note: Ordering locks artifact contracts (M1) before expanding modeling surface (Expressions at M1.5) to avoid churn across CLI/API/UI/adapters.

> Track progress and comment on prioritization in **`docs/ROADMAP.md`**.

---

## CI/CD & deployment

**Local-first**: use the CLI. CI workflows and code scanning can be added under `.github/workflows/`.

**Cloud (optional, later)**

* Any static host for the SPA + any app host/serverless for the backend.
* Secret/config management via your platform’s secret store (e.g., Key Vault + App Config if you deploy on Azure).

---

## Data & formats

**CSV outputs** use a simple, culture‑invariant, human‑readable schema per series (example). Beginning with Contracts Parity (M1) each run also emits a set of deterministic artifacts under `runs/<runId>/`:

```
runs/<runId>/
  spec.yaml              # canonicalized copy of the submitted model (for hashing)
  run.json               # run metadata (schemaVersion, runId, engineVersion, source, grid{bins,binMinutes,timezone,align}, scenarioHash, modelHash?, warnings[], series[], events{schemaVersion,fieldsReserved[]})
  manifest.json          # manifest (schemaVersion, scenarioHash, rng{kind,seed}, seriesHashes{}, eventCount, createdUtc, modelHash optional)
  series/
    index.json           # series metadata (ids, kind, unit, componentId, class, points, hash, formats.goldTable placeholder)
    <series>.csv         # per-series time series (t,value)
  events.ndjson          # (placeholder for future structured events)
  gold/                  # (placeholder directory for normalized “gold table” view)
```

Determinism rules (enforced progressively through M1):
* Canonical YAML hashing: strip comments, normalize line endings (LF), trim trailing whitespace, collapse blank line runs, key-order insensitive.
* Hashes: SHA‑256 prefixed `sha256:` for model/scenario and per-series content (raw CSV bytes, LF newlines, invariant `G17` formatting for doubles).
* Stable ordering: series entries listed in deterministic order (e.g., lexical by id) so `index.json` diff noise is avoided.

```
t,value
0,12.5
1,13.1
2,15.0
```

Where `t` is the **bin index** (aligned to the model’s canonical grid). Run/series metadata (grid, hashes, RNG, units, warnings) lives in the JSON companions above to keep CSV lean and diff‑friendly.

## Artifacts (M1 Contracts Parity)

FlowTime emits a deterministic artifact set. Field-level definitions live in [docs/contracts.md](docs/contracts.md). Schemas:
* [run.schema.json](docs/schemas/run.schema.json)
* [manifest.schema.json](docs/schemas/manifest.schema.json)
* [series-index.schema.json](docs/schemas/series-index.schema.json)

See milestone status in [docs/ROADMAP.md](docs/ROADMAP.md). `schemaVersion=1` changes are additive-only; breaking changes will bump the version.

---

## Docs & roadmap

* **Roadmap**: see [`docs/ROADMAP.md`](docs/ROADMAP.md).
* **Release notes**: see `docs/releases/` (e.g., [`docs/releases/M0.md`](docs/releases/M0.md)). Consider tagging GitHub Releases that link to these notes.
* **Concepts**: see [Nodes, expressions, and execution](docs/concepts/nodes-and-expressions.md) for how models compile to node graphs and run deterministically.

---

## Contributing

We welcome issues and PRs.

1. Create a topic branch from `main`.
2. `dotnet test` must pass; keep analyzers/formatting clean.
3. Follow Conventional Commits (`feat:`, `fix:`, `docs:`, ...).
4. For larger changes, open an issue first to align on direction.

> Be kind and constructive. A `CODE_OF_CONDUCT.md` may be added; until then, treat others with respect.

---

## License

**MIT** — permissive and simple. See [`LICENSE`](LICENSE).

---

## Trademark

**FlowTime** and the associated word mark are claimed by the project owner. You may reference FlowTime factually, but please avoid implying endorsement. For brand questions, contact the owner.
