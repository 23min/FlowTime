# FlowTime CLI Guide (M0)

This guide shows how to build, test, and run the FlowTime CLI at milestone M0.
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
# Evaluate the example YAML and write CSV outputs to out/hello
Dotnet run --project src/FlowTime.Cli -- run examples/hello/model.yaml --out out/hello --verbose

# Peek at the CSV (first 5 lines)
Get-Content out/hello/served.csv | Select-Object -First 5
```

Bash equivalents:

```bash
# Evaluate the example YAML and write CSV outputs to out/hello
dotnet run --project src/FlowTime.Cli -- run examples/hello/model.yaml --out out/hello --verbose

# Peek at the CSV (first 5 lines)
head -n 5 out/hello/served.csv
```

Outputs are written to the directory passed via `--out` (e.g., `out/hello`).

## Usage

```text
flowtime run <model.yaml> [--out <dir>] [--verbose] [--via-api <url>]
```

Options:
- `--out <dir>`: Output directory (default `out/run`).
- `--verbose`: Print a short run summary (grid, topo order, outputs).
- `--via-api <url>`: Optional. Route the run through the API for parity testing.
  - Note: Until the API milestone (SVC-M0) ships, this flag logs a notice and falls back to local execution.
 - Help: `-h`, `--help`, `/?`, `/h` — print usage and exit 0.

## Model format (M0)

Minimal YAML schema with a canonical grid and a small node set.

```yaml
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
- `values` length must equal `grid.bins`.
- `expr` supports simple forms like `name * <scalar>` or `name + <scalar>`.
- Numbers use culture-invariant parsing/formatting.

## VS Code tasks

This repo includes basic tasks:

```text
Terminal > Run Task...
  - build         # dotnet build
  - test          # dotnet test
  - run: hello    # run CLI on examples/hello/model.yaml
```

Use these for fast iteration without typing full commands.

Tip: In Bash shells, the dotnet commands are identical; only shell utilities (like `head` vs `Get-Content`) differ.

## Troubleshooting

- Missing model file: check the path or quote it if it has spaces.
- YAML parse errors: ensure proper indentation and commas.
- Length mismatch: `values` array must match `grid.bins`.
- Non-ASCII/locale issues: numbers must use `.` as the decimal separator.

## What’s next

- API (SVC-M0): a minimal FlowTime.API HTTP surface (POST /run, GET /graph, GET /healthz), host-agnostic (Functions, ASP.NET Core, etc.).
- When available, you can run the CLI via API with `--via-api <url>` for parity checks.

See also:
- Roadmap: docs/ROADMAP.md
- Concepts: docs/concepts/nodes-and-expressions.md
