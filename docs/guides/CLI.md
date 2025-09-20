# FlowTime.Sim CLI Guide (Pre-Charter Documentation)

> ⚠️ **CHARTER COMPLIANCE NOTICE**: This guide describes pre-charter FlowTime-Sim that violates Charter v1.0 by generating telemetry. 
> Per Charter v1.0, FlowTime-Sim is a "modeling front-end" that creates model artifacts but **never computes telemetry**.
> This guide will be updated to reflect charter-compliant model authoring workflows in SIM-M2.6+.

This guide shows legacy CLI functionality for synthetic data generation. 
**Charter-aligned model authoring guides will replace this content.**

## Prerequisites

- .NET 9 SDK
- Git
- Windows PowerShell (examples use PowerShell syntax)

## Build and test

```powershell
# from the repo root
dotnet restore
dotnet build
dotnet test --nologo
```

## Run the example simulation

```powershell
# Generate synthetic data using constant arrivals and write SIM-M2 artifact set to data/runs
dotnet run --project src/FlowTime.Sim.Cli -- --model examples/m0.const.sim.yaml --mode sim --out data --verbose

# Peek at the generated artifacts
Get-ChildItem data/runs/sim_* -Recurse | Select-Object Name
Get-Content data/runs/sim_*/series/arrivals@nodeA.csv | Select-Object -First 5
```

Bash equivalents:

```bash
# Generate synthetic data using constant arrivals and write SIM-M2 artifact set to data/runs
dotnet run --project src/FlowTime.Sim.Cli -- --model examples/m0.const.sim.yaml --mode sim --out data --verbose

# Peek at the generated artifacts
find data/runs/sim_* -type f
head -n 5 data/runs/sim_*/series/arrivals@nodeA.csv
```

Beginning in SIM-M2 (Artifact Parity & Series Index), all simulation runs emit a structured artifact set under `runs/<runId>/` including `run.json`, `manifest.json`, `series/index.json`, per‑series CSV files, and the original `spec.yaml` with deterministic hashing.

## Usage

```text
dotnet run --project src/FlowTime.Sim.Cli -- --model <file.yaml> [--mode engine|sim] [--flowtime <url>] [--out <dir>] [--format csv|json] [--debug-events <file>] [--verbose]
```

Options:
* `--model <file.yaml>`: Simulation specification file (required).
* `--mode <mode>`: Execution mode - `sim` for synthetic data generation, `engine` for FlowTime integration (default: `engine`, but simulation specs require explicit `--mode sim`).
* `--flowtime <url>`: FlowTime API endpoint for integration mode (default: `http://localhost:8080`).
* `--out <dir>`: Output directory - artifacts will be written to `<dir>/runs/` unless `<dir>` already ends with "runs" (default: current directory).
* `--format <format>`: Output format - `csv` or `json` (default: `csv`).
* `--debug-events <file>`: Write debug events to specified NDJSON file.
* `--verbose`: Print detailed run summary including run ID, grid info, and file locations.
* Help: `-h`, `--help` — print usage and exit 0.

## SIM-M2 Artifact Structure

Each simulation run generates a complete artifact set under `<outDir>/runs/<runId>/`:

```
sim_20250906T154200Z_53509771/
├── run.json                            # run summary, series listing
├── manifest.json                       # determinism & integrity (hashes, RNG seed)
├── spec.yaml                           # original model, normalized
└── series/
    ├── index.json                      # series discovery metadata
    ├── arrivals@nodeA.csv              # per-series data (measure@componentId)
    ├── served@nodeA.csv                # additional series
    └── errors@nodeA.csv                # error series if applicable
```

**Key Features:**
- **Deterministic runId**: Format `sim_YYYY-MM-DDTHH-mm-ssZ_<8slug>` (opaque; do not parse)
- **Series hashing**: Each CSV file SHA-256 tracked in `manifest.json` and `series/index.json`
- **Per-series CSV schema**: `t,value` where `t` = 0..(bins-1)
- **Determinism**: Identical specs (including seed & RNG) produce identical per-series CSV bytes and hashes

## Simulation Model Format (SIM-M2)

FlowTime.Sim generates synthetic arrival data using `.sim.yaml` specification files.

### Simulation Specification (--mode sim)

For synthetic data generation, use `.sim.yaml` files with explicit `--mode sim`:

```yaml
schemaVersion: 1
grid: 
  bins: 4
  binMinutes: 60
  start: 2025-01-01T00:00:00Z
seed: 12345                    # For deterministic results
arrivals:
  kind: const                  # const | poisson
  values: [5, 5, 5, 5]        # For const: exact counts per bin
  # rate: 3.5                 # For poisson: arrival rate
route:
  id: nodeA                    # Target node identifier
# Optional RNG configuration
rng: pcg                       # pcg (default) | legacy
# Optional output customization
outputs:
  events: out/events.ndjson    # Debug events (use --debug-events flag)
  gold: out/gold.csv           # Gold standard data
```

**Required Fields:**
- `schemaVersion`: Must be `1`
- `grid`: Time discretization settings
- `arrivals`: Arrival pattern specification  
- `route.id`: Target node identifier

**Determinism & Reproducibility:**
- `seed`: Integer seed for deterministic results (required for reproducibility)
- `rng`: RNG algorithm - `pcg` (default, recommended) or `legacy`
- **Guarantee**: Identical specs with same `seed` produce identical output files
- **Testing**: Same seed always generates same CSV data, even across runs

**Arrival Patterns:**
- `const`: Exact arrivals per time bin (use `values` array)
- `poisson`: Stochastic arrivals with Poisson distribution (use `rate` parameter)

**Debug Events:**
When using `--debug-events <file>`, FlowTime-Sim outputs detailed event information to NDJSON format for analysis and debugging.

**Important**: Simulation specs require explicit `--mode sim` parameter.

## Examples

### Constant Arrivals

```powershell
# Generate predictable constant arrivals
dotnet run --project src/FlowTime.Sim.Cli -- `
  --model examples/m0.const.sim.yaml `
  --mode sim `
  --out data `
  --verbose
```

### Poisson Arrivals

```powershell
# Generate realistic Poisson-distributed arrivals
dotnet run --project src/FlowTime.Sim.Cli -- `
  --model examples/m0.poisson.sim.yaml `
  --mode sim `
  --out data `
  --debug-events data/debug.ndjson `
  --verbose
```

### Generating Debug Events

```powershell
# Generate arrivals with detailed debug events for analysis
dotnet run --project src/FlowTime.Sim.Cli -- `
  --model examples/m0.poisson.sim.yaml `
  --mode sim `
  --out data `
  --debug-events data/debug.ndjson `
  --verbose
```

The `--debug-events` flag outputs detailed event information in NDJSON format, useful for:
- Debugging arrival generation logic
- Analyzing event timing and distribution
- Validating simulation behavior
- Integration testing with external tools

## VS Code tasks

This repo includes development tasks:

```text
Terminal > Run Task...
  - build                         # dotnet build
  - test                          # dotnet test --nologo
  - .NET Build (sim)             # build CLI and tests
  - .NET Test (sim)              # run test suite
  - Run SIM-M0 example           # run CLI on examples/m0.const.sim.yaml
```

Use these for fast iteration without typing full commands.

## Troubleshooting

### Common Issues

- **Missing model file**: Check the path or quote it if it has spaces
- **Missing --mode sim**: Simulation specs (`.sim.yaml`) require explicit `--mode sim` parameter
- **YAML parse errors**: Ensure proper indentation and syntax
- **Length mismatch**: `values` array must match `grid.bins` for constant arrivals
- **Missing seed**: Add `seed` field for deterministic/reproducible results
- **Permission errors**: Check write permissions for output directory

### Debugging with Events

Use `--debug-events` to capture detailed event information:

```powershell
# Generate events file for debugging
dotnet run --project src/FlowTime.Sim.Cli -- `
  --model examples/m0.const.sim.yaml `
  --mode sim `
  --debug-events debug.ndjson `
  --verbose

# Examine events (PowerShell)
Get-Content debug.ndjson | ConvertFrom-Json | Select-Object -First 5
```

```bash
# Examine events (Bash)
head -n 5 debug.ndjson | jq .
```

### Configuration Issues

FlowTime.Sim respects data directory configuration:

```powershell
# Set custom data directory
$env:FLOWTIME_SIM_DATA_DIR = "C:\FlowTime\Data"
dotnet run --project src/FlowTime.Sim.Cli -- --model examples/m0.const.sim.yaml --mode sim
```

## What's next

✅ **Milestone SIM-M2 (Artifact Parity & Series Index)**: COMPLETED
* Deterministic artifact set (`run.json`, `manifest.json`, `series/index.json`, per‑series CSVs)
* Per-series and scenario hashing (SHA-256) with deterministic generation
* Dual JSON + index + per-series CSV layout
* Removed legacy `metadata.json` and single `gold.csv` patterns

**Next Milestones:**
* SIM-M3: Enhanced arrival patterns (seasonal, mixed distributions)
* SIM-SVC-M3: Advanced API endpoints for simulation management
* Integration improvements with FlowTime engine

### Output Layout (Artifact Parity)

Complete field definitions: [contracts.md](../reference/contracts.md). The CLI guarantees:
* `runId` format: `sim_YYYY-MM-DDTHH-mm-ssZ_<8slug>` (opaque; do not parse)
* Per-series CSV schema: `t,value` where `t` = 0..(bins-1)
* `series/index.json` enumerates all series (id, kind, unit, path, hash, points)
* `manifest.json` lists per-series SHA-256 hashes; `run.json` mirrors it (semantic divergence reserved)
* SeriesId format: `measure@componentId` (e.g., `arrivals@nodeA`)

See also:
- Roadmap: [ROADMAP.md](../ROADMAP.md)
- Contracts: [contracts.md](../reference/contracts.md)
- Configuration: [configuration.md](configuration.md)
- Development Setup: [development-setup.md](../development/development-setup.md)
