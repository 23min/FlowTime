# FlowTime CLI Guide (Engine + Sim)

The repository ships two CLIs:

- **Engine CLI (`src/FlowTime.Cli`)** — Runs engine models, writes artifacts, lists runs, and orchestrates template/telemetry runs.
- **Sim CLI (`src/FlowTime.Sim.Cli`)** — Lists/generates/validates templates and runs the template invariant analyzers.

This guide reflects the current code paths (tested November 24, 2025) and removes stale roadmap-only commands.

## Prerequisites

- .NET 9 SDK
- Git
- Any shell (examples use Bash; PowerShell equivalents are noted where helpful)

## Engine CLI (flowtime)

Entry point during development:

```bash
dotnet run --project src/FlowTime.Cli -- <command> [options]
```

The CLI name in help/usage is `flowtime`.

### Default paths and environment overrides
- Output root defaults to `FLOWTIME_DATA_DIR` if set, otherwise `<repo>/data` (see `DirectoryProvider`).
- Templates directory for orchestrated runs defaults to `FLOWTIME_TEMPLATES_DIR` if set, otherwise `<repo>/templates`.
- Run directories are created as `run_<timestamp>_<suffix>` directly under the chosen output root (for orchestrated runs, under `<outputRoot>/runs/<runId>`).

### Run a model file

```bash
dotnet run --project src/FlowTime.Cli -- run examples/hello/model.yaml \
  --out /tmp/flowtime-out \
  --verbose \
  --deterministic-run-id \
  --seed 123
```

Flags:
- `--out <dir>`: Output root (default: `<repo>/data` via `FLOWTIME_DATA_DIR`).
- `--verbose`: Prints grid + topological order.
- `--deterministic-run-id`: Stable runId from scenario hash.
- `--seed <n>`: RNG seed for reproducible runs.

Artifact layout from a real run (generated via the command above):

```
run_20251124T092144Z_8e8bc14a/
├── manifest.json
├── model/
│   ├── metadata.json
│   └── model.yaml
├── run.json                  # warnings from invariant analyzer are recorded here
├── series/
│   ├── index.json
│   └── served@SERVED@DEFAULT.csv
└── spec.yaml
```

Notes:
- Models are validated with `ModelValidator`; invariant analysis runs during artifact writing and warnings land in `run.json` (not on stdout).
- Series hashes and scenario hash are recorded in `manifest.json`.

### Orchestrated runs (templates / telemetry)

Uses the same orchestration path as FlowTime.API:

```bash
# Simulation from a template (no telemetry capture)
dotnet run --project src/FlowTime.Cli -- run \
  --template-id transportation-basic \
  --mode simulation \
  --out /tmp/ft-runs \
  --deterministic-run-id

# Telemetry replay from a capture bundle
dotnet run --project src/FlowTime.Cli -- run \
  --template-id transportation-basic \
  --mode telemetry \
  --capture-dir /path/to/capture \
  --bind arrivals=arrivals.csv \
  --out /tmp/ft-runs
```

Options:
- `--template-id <id>` (required)
- `--mode simulation|telemetry` (default: telemetry when `--capture-dir` is provided)
- `--capture-dir <path>` (required for telemetry mode)
- `--param-file <json>`: Parameter overrides (JSON object)
- `--bind key=file`: Bind telemetry inputs inside `capture-dir`
- `--run-id <value>`: Explicit run directory name
- `--overwrite`: Allow reuse of `--run-id`
- `--dry-run`: Plan without writing artifacts
- `--deterministic-run-id`: Stable runId from scenario hash

⚠ **RNG limitation:** Templates that declare an `rng` block currently require a seed, but the CLI has no `--rng` flag. Those templates will fail with “provide rng.seed” (e.g., `it-system-microservices`). Workarounds: (a) use a template without `rng`, or (b) run via FlowTime.API where `rng` can be supplied.

### List artifacts

Offline listing of run artifacts (provenance filters supported):

```bash
dotnet run --project src/FlowTime.Cli -- artifacts list \
  --data-dir /workspaces/flowtime-vnext/data \
  --template-id supply-chain-multi-tier \
  --limit 5
```

Options: `--template-id`, `--model-id`, `--limit`, `--skip`, `--data-dir` (default: `<repo>/data`).

## Sim CLI (flow-sim)

Entry point during development:

```bash
dotnet run --project src/FlowTime.Sim.Cli -- <verb> [noun] [options]
```

Supported verbs/nouns:
- `list [templates|models]`
- `show template|model`
- `generate [model]`
- `validate [template|params]`

Common options: `--id <templateId>`, `--params <json>`, `--out <path>`, `--format yaml|json`, `--templates-dir <dir>`, `--models-dir <dir>`, `--mode simulation|telemetry`, `--provenance <file>`, `--embed-provenance`, `--verbose`.

Examples:

```bash
# List available templates
dotnet run --project src/FlowTime.Sim.Cli -- list templates --templates-dir templates

# Generate an engine-ready model and run invariants
dotnet run --project src/FlowTime.Sim.Cli -- generate \
  --id supply-chain-multi-tier \
  --templates-dir templates \
  --mode simulation \
  --out /tmp/supply-chain.yaml

# Validate parameters only
dotnet run --project src/FlowTime.Sim.Cli -- validate template \
  --id transportation-basic \
  --templates-dir templates \
  --params overrides.json
```

Analyzer behavior:
- `generate` runs `TemplateInvariantAnalyzer` and prints ⚠ warnings (node ids + bin indices) to stdout.
- `validate` checks parameter shapes/types/bounds and exits non-zero on failure.

## Analyzer coverage (answering “can the CLIs run analyzers?”)
- **Sim CLI:** Yes. `generate` runs the template invariant analyzers; `validate` enforces parameter constraints.
- **Engine CLI:** Runs invariant analysis during artifact writing and records warnings in `run.json`, but it does not print analyzer output to stdout. There is no standalone “analyze” command; for explicit checks, use Sim CLI or inspect `run.json` after a run. FlowTime.API uses the same invariant path as the engine CLI.

## What’s not implemented (future/roadmap)
- The earlier “M-02.09 CLI evolution” items (validate/info commands on the engine CLI, profiling, JSON/Parquet output, registry add/remove) are not present in the current code. Keep them as roadmap only until implemented.

For full contract details, see `docs/reference/contracts.md` and the template authoring/testing guides under `docs/templates/`.

See also:
- Roadmap: docs/ROADMAP.md
- Contracts: docs/contracts.md
- Concepts: docs/concepts/nodes-and-expressions.md
