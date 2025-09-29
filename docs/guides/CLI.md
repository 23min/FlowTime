# FlowTime CLI Guide (M0 → M1)

> **📋 Charter Context**: CLI capabilities described here remain core to the [FlowTime-Engine Charter](../flowtime-engine-charter.md). The CLI provides the foundational execution engine supporting the artifacts-centric workflow. See [Charter Roadmap](../milestones/CHARTER-ROADMAP.md) for current development direction.

This guide shows how to build, test, and run the FlowTime CLI with M1 (Contracts Parity) features.
Examples use PowerShell by default (Windows-first), with Bash equivalents where syntax differs.

## Prerequisites

- .NET 9 SDK
- Git
- Windows PowerShell (examples use PowerShell syntax)

## Build and test

```powershell
# from the repo root
Dotnet restore
Dotnet build
Dotnet test --nologo
```

## Run the example model

```powershell
# Evaluate the example YAML and write M1 artifact set to out/hello
Dotnet run --project src/FlowTime.Cli -- run examples/hello/model.yaml --out out/hello --verbose

# Peek at the generated artifacts
Get-ChildItem out/hello/run_* -Recurse | Select-Object Name
Get-Content out/hello/run_*/series/served@SERVED@DEFAULT.csv | Select-Object -First 5
```

Bash equivalents:

```bash
# Evaluate the example YAML and write M1 artifact set to out/hello
dotnet run --project src/FlowTime.Cli -- run examples/hello/model.yaml --out out/hello --verbose

# Peek at the generated artifacts
find out/hello/run_* -type f
head -n 5 out/hello/run_*/series/served@SERVED@DEFAULT.csv
```

Beginning in M1 (Contracts Parity), all runs emit a structured artifact set under `runs/<runId>/` including `spec.yaml`, `run.json`, `manifest.json`, `series/index.json`, per‑series CSV files, and placeholder directory for `gold/`.

## Usage

```text
flowtime run <model-artifact.yaml> [--out <dir>] [--verbose] [--deterministic-run-id] [--seed <n>]
```

Options:
* `--out <dir>`: Output directory (default `out/run`).
* `--verbose`: Print run summary (grid, topology, outputs, schema validation results).
* `--deterministic-run-id`: Generate stable runId based on scenario hash (for testing/CI).
* `--seed <n>`: RNG seed for reproducible results (default: random).

Help: `-h`, `--help`, `/?`, `/h` — print usage and exit 0.

## M1 Artifact Structure

Each run generates a complete artifact set under `out/<dir>/<runId>/`:

```
run_20250903T133923Z_e60c400a/
├── spec.yaml                           # original model artifact, normalized line endings
├── run.json                            # run summary, series listing
├── manifest.json                       # determinism & integrity (hashes, RNG seed)
├── series/
│   ├── index.json                      # series discovery metadata
│   └── served@SERVED@DEFAULT.csv       # per-series data (measure@componentId@class)
└── gold/                               # placeholder for analytics tables
```

**Key Features:**
- **Canonical hashing**: `scenarioHash` and `modelHash` computed via YAML normalization (LF endings, trimmed whitespace, key-order invariant)
- **Series hashing**: Each CSV file SHA-256 tracked in `manifest.json` and `series/index.json`
- **Schema validation**: Automatic JSON Schema validation against [docs/schemas/](schemas/) files
- **Determinism**: `--deterministic-run-id` + `--seed` enable reproducible artifacts for CI/testing

## Model Artifact Format (M2.x)

Unified Model artifact structure that works with both FlowTime and FlowTime-Sim:

```yaml
kind: Model
schemaVersion: 1
metadata:
  title: "Hello World Flow"
  created: "2024-09-20T10:00:00Z"
spec:
  grid:
    bins: 8
    binMinutes: 60
  nodes:
    - id: demand
      kind: const
      values: [10,10,10,10,10,10,10,10]
    - id: served
      kind: expr
      expr: "demand * 0.8"
  outputs:
    - series: served
      as: served.csv
```

Notes:
- `kind: Model` identifies this as a Model artifact for registry and UI
- `metadata` provides tracking and discovery information
- `spec` contains the actual model definition both engines understand
- `values` length must equal `grid.bins`
- `expr` supports simple forms like `name * <scalar>` or `name + <scalar>`
- Numbers use culture-invariant parsing/formatting

## VS Code tasks

This repo includes basic tasks:

```text
Terminal > Run Task...
  - build         # dotnet build
  - test          # dotnet test
  - run: hello    # run CLI on examples/hello/model.yaml (Model artifact)
```

Use these for fast iteration without typing full commands.

Tip: In Bash shells, the dotnet commands are identical; only shell utilities (like `head` vs `Get-Content`) differ.

## Troubleshooting

- Missing model file: check the path or quote it if it has spaces.
- YAML parse errors: ensure proper indentation and commas.
- Length mismatch: `values` array must match `grid.bins`.
- Non-ASCII/locale issues: numbers must use `.` as the decimal separator.

## What’s next

✅ **Milestone M1 (Contracts Parity)**: COMPLETED
* Deterministic artifact set (`spec.yaml`, `run.json`, `manifest.json`, `series/index.json`, per‑series CSVs, placeholder for `gold/`).
* Per-series and scenario hashing (SHA-256) with canonical YAML normalization.
* Implemented flags `--deterministic-run-id`, `--seed`.
* JSON Schema validation for all artifacts.

**Next Milestones:**
* M2: Expression grammar expansion, advanced node types
* SVC-M0: FlowTime.API HTTP surface (POST /run, GET /graph, GET /healthz)

## M2.9 CLI Evolution

Starting in M2.9, the CLI evolves to become the primary developer tool with enhanced capabilities:

### Architecture Changes
- **Direct Core Access**: CLI operates directly on FlowTime.Core, no API dependency
- **Command Name**: Primary command becomes `flowtime` (not `FlowTime.Cli`)
- **Developer Focus**: Optimized for development workflows, testing, and automation

### Enhanced Commands

#### Model Validation
```bash
flowtime validate <model-artifact.yaml> [--schema-version <ver>] [--strict]
```
- Validates model structure and syntax without execution
- Schema version compatibility checking
- Strict mode for enhanced validation rules
- Returns detailed validation reports

#### Model Information
```bash
flowtime info <model-artifact.yaml> [--format json|yaml|table]
```
- Display model metadata, structure, and statistics
- Node dependency analysis
- Grid configuration summary
- Output format options for tooling integration

### Enhanced Run Options

#### Advanced Output Control
```bash
flowtime run <model> --out-format [csv|json|parquet] --series <pattern>
```
- Multiple output formats beyond CSV
- Series filtering and selection
- Structured data for analytics workflows

#### Performance Profiling
```bash
flowtime run <model> --profile [--profile-out <file>]
```
- Execution timing and performance metrics
- Node-level performance analysis
- Memory usage tracking
- Optimization guidance

### Development Workflow Integration

#### Registry Integration
```bash
flowtime registry list [--format table|json]
flowtime registry add <model> [--tags <tag1,tag2>]
flowtime registry remove <model-id>
```
- Local model registry management
- Tagging and categorization
- Discovery and reuse workflows

### Future Enhancements

#### Configuration Management
- Global and workspace-level configuration files
- User preferences and tool behavior customization
- Environment-specific settings and profiles

### M2.9 Implementation Plan

**Phase 1**: Core CLI Architecture
- Migrate from API-dependent to direct Core access
- Implement new command structure and argument parsing
- Basic enhanced commands (validate, info)

**Phase 2**: Enhanced Capabilities
- Advanced output control and formatting options
- Performance profiling and optimization tools
- Registry integration for model management

**Phase 3**: Documentation and Polish
- Comprehensive CLI documentation updates
- Example workflows and use cases
- Integration guides for development teams

### Output Layout (Contracts Parity)
Complete file/field definitions: [contracts.md](../reference/contracts.md). The CLI guarantees:
* `spec.yaml` persisted verbatim with normalized line endings.
* JSON artifacts (`run.json`, `manifest.json`, `series/index.json`) written after CSVs to finalize hashes.
* Automatic schema validation with detailed error reporting.
* SeriesId format: `measure@componentId@class` (e.g., `served@SERVED@DEFAULT`).

See also:
- Roadmap: docs/ROADMAP.md
- Contracts: docs/contracts.md
- Concepts: docs/concepts/nodes-and-expressions.md
