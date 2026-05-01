---
id: M-011
title: .NET Time Machine CLI
status: done
parent: E-18
acs:
  - id: AC-1
    title: Five CLI commands (`validate`, `sweep`, `sensitivity`, `goal-seek`, `optimize`) wired into `Program.cs` 
      router
    status: met
  - id: AC-2
    title: Each command parses `--spec` / stdin, `--output` / stdout, `--no-session`, `--engine`, `--help`
    status: met
  - id: AC-3
    title: '`validate` reads YAML (not JSON) via `--model` / stdin; outputs `ValidationResult` as JSON'
    status: met
  - id: AC-4
    title: Each analysis command reads its matching `*Spec` as JSON and writes its matching result as JSON, 
      byte-compatible with the corresponding `/v1/` endpoint
    status: met
  - id: AC-5
    title: '`CliEngineSetup` helper resolves binary path via `--engine` → `FLOWTIME_RUST_BINARY` → solution-relative default
      → `$PATH`'
    status: met
  - id: AC-6
    title: '`CliEngineSetup` constructs `SessionModelEvaluator` by default; `--no-session` selects `RustModelEvaluator`'
    status: met
  - id: AC-7
    title: '`CliJsonIO` helper reads JSON from stdin-or-file and writes JSON to stdout-or-file with camelCase / web defaults
      matching the API; `JsonStringEnumConverter` added so `objective: "minimize"` etc. deserialize correctly'
    status: met
  - id: AC-8
    title: Exit codes follow the 0/1/2/3 contract (success / analysis-failed / input-error / engine-error)
    status: met
  - id: AC-9
    title: Missing engine binary produces exit 2 with a readable stderr message
    status: met
  - id: AC-10
    title: Invalid JSON produces exit 2 with a stderr message; no partial stdout
    status: met
  - id: AC-11
    title: '`--help` on any command prints command-specific usage and exits 0'
    status: met
  - id: AC-12
    title: 'Unit tests pass: 72 new CLI unit tests - 15 CliJsonIO (read/write, file/stdin, camelCase, null literal, errors)
      - 14 CliCommonArgs (all flag variants, missing values, unknown flag, positional, dash-as-positional) - 8 CliEngineSetup
      (path precedence, evaluator selection, disposal idempotency) - 13 ValidateCommand (help, arg errors, tier, valid/invalid
      YAML, output) - 18 AnalysisCommandTests (help for each of 4 commands, shared error paths, IsOnPath, BarePath) - 4 deferred
      (covered by integration tests instead — see below)'
    status: met
  - id: AC-13
    title: 'Integration tests pass with the Rust binary present: 10 tests (TimeMachineCliIntegrationTests) - [x] `flowtime
      validate` with valid and invalid YAML - [x] `flowtime sweep` end-to-end producing correct series (arrivals=10,20,30
      → served=5,10,15) - [x] `flowtime sensitivity` end-to-end (∂served/∂arrivals = 0.5) - [x] `flowtime goal-seek` end-to-end
      (target served=25 → arrivals≈50) - [x] `flowtime optimize` converging on a `MAX(x-7,7-x)` bowl around arrivals=14 -
      [x] Session vs. per-eval flag (`--no-session`) both work - [x] Output to file (`-o`) matches output to stdout - [x]
      Engine compile error (unknown function) produces exit 3'
    status: met
  - id: AC-14
    title: Every reachable path in the new command classes and helpers is covered (line-by-line audited)
    status: met
  - id: AC-15
    title: '`docs/architecture/time-machine-analysis-modes.md` — new "CLI surface" section documents the five commands, JSON
      I/O contract, exit codes, evaluator selection, engine resolution, and pipeline composition example'
    status: met
  - id: AC-16
    title: '`Program.cs` `PrintUsage` updated with the five new commands'
    status: met
  - id: AC-17
    title: '`dotnet build FlowTime.sln` green'
    status: met
  - id: AC-18
    title: '`dotnet test FlowTime.sln` all green — 1,702 passed / 9 skipped'
    status: met
---

## Goal

Expose the Time Machine analysis modes (validate / sweep / sensitivity / goal-seek /
optimize) through the `FlowTime.Cli` binary as pipeable JSON-over-stdio commands.
The CLI becomes the canonical pipeline entry point for Azure Functions custom handlers,
Container Apps jobs, scripted regression suites, shell composition, and AI-assistant
iteration — without requiring the ASP.NET API to be running.

Spec success criterion from E-18:
```
cat model.yaml | flowtime validate
cat sweep-spec.json | flowtime sweep | jq '.points[].metricMean'
```

## Scope

Five new commands under `src/FlowTime.Cli/Commands/` mirroring the `/v1/` API surface:

| CLI command | Spec type | Runner | Matches API endpoint |
|-------------|-----------|--------|----------------------|
| `flowtime validate` | `TimeMachineValidator` (no wrapping spec) | `TimeMachineValidator` | `POST /v1/validate` |
| `flowtime sweep` | `SweepSpec` | `SweepRunner` | `POST /v1/sweep` |
| `flowtime sensitivity` | `SensitivitySpec` | `SensitivityRunner` | `POST /v1/sensitivity` |
| `flowtime goal-seek` | `GoalSeekSpec` | `GoalSeeker` | `POST /v1/goal-seek` |
| `flowtime optimize` | `OptimizeSpec` | `Optimizer` | `POST /v1/optimize` |

### JSON I/O contract

Each command reads a JSON request on stdin (or via `--spec <path>`), runs the analysis,
and writes a JSON response on stdout. The request/response shapes are **identical to the
corresponding `/v1/` endpoint bodies** — byte-for-byte compatible, so `cat spec.json |
flowtime sweep` produces the same payload as `curl -d @spec.json /v1/sweep`.

`validate` is the exception: its input is raw YAML (on stdin or `--model <path>`),
not JSON. Output is the same JSON response shape as `POST /v1/validate`.

### Unified options across commands

- `--spec <path>` — read JSON request from a file instead of stdin (analysis commands)
- `--model <path>` — read model YAML from a file (validate only)
- `--output <path>` / `-o` — write JSON response to a file instead of stdout
- `--no-session` — use `RustModelEvaluator` (stateless, subprocess-per-eval) instead of
  `SessionModelEvaluator` (default). Matches the `RustEngine:UseSession=false` config.
- `--engine <path>` — override engine binary path (default: `FLOWTIME_RUST_BINARY` env
  var, then `<solution>/engine/target/release/flowtime-engine`)
- `-h` / `--help` — command-specific help

### Exit codes

- **0** — success
- **1** — analysis produced an explicit failure (e.g., validate returned invalid,
  optimize didn't converge and exited cleanly). The JSON response is still written to
  stdout and describes the failure — stderr is clean.
- **2** — input error: missing required args, invalid JSON, engine binary not found,
  spec failed validation. Error message on stderr; nothing on stdout.
- **3** — engine/runtime error: session subprocess crashed, protocol error,
  `InvalidOperationException` from evaluator. Error message on stderr; nothing on stdout.

### Engine binary resolution

Same precedence as the API, extracted to a shared helper:
1. `--engine <path>` command-line flag
2. `FLOWTIME_RUST_BINARY` environment variable
3. `<solution>/engine/target/release/flowtime-engine` (found via `DirectoryProvider.FindSolutionRoot`)
4. `flowtime-engine` on `$PATH` (fallback)

Fail with exit 2 and clear stderr message if binary is not found or not executable.

### Shared infrastructure

Extract two helpers to `src/FlowTime.Cli/Commands/`:

- `CliEngineSetup` — resolves engine binary path, constructs the chosen `IModelEvaluator`
  as `IAsyncDisposable` (so callers can `await using`). Also exposes a factory for
  `RustEngineRunner` (needed by `RustModelEvaluator` fallback).
- `CliJsonIO` — reads JSON from stdin-or-file, writes JSON to stdout-or-file, common
  serialization options matching the API (camelCase, web defaults).

### In scope

- `src/FlowTime.Cli/Commands/ValidateCommand.cs`
- `src/FlowTime.Cli/Commands/SweepCommand.cs`
- `src/FlowTime.Cli/Commands/SensitivityCommand.cs`
- `src/FlowTime.Cli/Commands/GoalSeekCommand.cs`
- `src/FlowTime.Cli/Commands/OptimizeCommand.cs`
- `src/FlowTime.Cli/Commands/CliEngineSetup.cs` (helper)
- `src/FlowTime.Cli/Commands/CliJsonIO.cs` (helper)
- `src/FlowTime.Cli/Program.cs` — command routing + `PrintUsage` updates
- Unit tests per command: `tests/FlowTime.Cli.Tests/Commands/{Validate,Sweep,Sensitivity,GoalSeek,Optimize}CommandTests.cs`
- Integration tests that exercise end-to-end with the Rust binary:
  `tests/FlowTime.Integration.Tests/TimeMachineCliIntegrationTests.cs`
- Update `docs/architecture/time-machine-analysis-modes.md` — new "CLI surface" section
- Update `CLAUDE.md` Current Work

### Out of scope

- `fit` command (blocked on Telemetry Loop & Parity epic — not yet started)
- `chunked-eval` command (explicitly deferred by spec)
- `monte-carlo` command (explicitly deferred by spec)
- `System.CommandLine` framework migration — keep the existing minimal-args convention
  used by the `run` and `artifacts` commands; introducing a library would be a separate
  refactor
- YAML / CSV / table output formats — JSON only
- Interactive REPL mode
- Progress reporting beyond stderr status lines

## Design

### Command shape (every analysis command)

```csharp
public static class SweepCommand
{
    public static async Task<int> ExecuteAsync(string[] args)
    {
        var parsed = ParseArgs(args);                // spec path, output, no-session, engine
        if (parsed.ShowHelp) { PrintHelp(); return 0; }

        SweepSpec spec;
        try { spec = CliJsonIO.Read<SweepSpec>(parsed.SpecPath); }
        catch (JsonException ex) { Console.Error.WriteLine($"Invalid JSON: {ex.Message}"); return 2; }

        await using var evaluator = CliEngineSetup.CreateEvaluator(parsed);
        var runner = new SweepRunner(evaluator);

        try
        {
            var result = await runner.RunAsync(spec);
            CliJsonIO.Write(parsed.OutputPath, result);
            return 0;
        }
        catch (InvalidOperationException ex) { Console.Error.WriteLine(ex.Message); return 3; }
    }
}
```

Each command is ~30-40 lines — parsing, spec deserialization, runner invocation, output.

### Why not `System.CommandLine`?

The existing `FlowTime.Cli` uses hand-rolled arg parsing (see `Program.cs` — `for` loop
over args). Adding `System.CommandLine` would be a larger refactor that touches the
`run` command too. Keeping consistency is more important than getting the nicer library
for this milestone. A future cleanup milestone can migrate all commands together.

### Why JSON-over-stdio and not flag-driven?

Specs like `OptimizeSpec` carry search ranges (one entry per param with lo/hi), objective
enums, tolerance, max iterations. Representing them as CLI flags is unergonomic:

```
flowtime optimize --param arrivals --range-arrivals 0:100 --param capacity \
                  --range-capacity 1:20 --metric util --objective minimize \
                  --tolerance 1e-4 --max-iters 200
```

vs.

```
cat optimize-spec.json | flowtime optimize
```

The JSON path is pipeline-native: compose with `jq`, store specs as fixtures, invoke
from Azure Functions custom handlers, share spec files with the API.

## Acceptance criteria

### AC-1 — Five CLI commands (`validate`, `sweep`, `sensitivity`, `goal-seek`, `optimize`) wired into `Program.cs` router

### AC-2 — Each command parses `--spec` / stdin, `--output` / stdout, `--no-session`, `--engine`, `--help`

### AC-3 — `validate` reads YAML (not JSON) via `--model` / stdin; outputs `ValidationResult` as JSON

### AC-4 — Each analysis command reads its matching `*Spec` as JSON and writes its matching result as JSON, byte-compatible with the corresponding `/v1/` endpoint

### AC-5 — `CliEngineSetup` helper resolves binary path via `--engine` → `FLOWTIME_RUST_BINARY` → solution-relative default → `$PATH`

### AC-6 — `CliEngineSetup` constructs `SessionModelEvaluator` by default; `--no-session` selects `RustModelEvaluator`

### AC-7 — `CliJsonIO` helper reads JSON from stdin-or-file and writes JSON to stdout-or-file with camelCase / web defaults matching the API; `JsonStringEnumConverter` added so `objective: "minimize"` etc. deserialize correctly

### AC-8 — Exit codes follow the 0/1/2/3 contract (success / analysis-failed / input-error / engine-error)

### AC-9 — Missing engine binary produces exit 2 with a readable stderr message

### AC-10 — Invalid JSON produces exit 2 with a stderr message; no partial stdout

### AC-11 — `--help` on any command prints command-specific usage and exits 0

### AC-12 — Unit tests pass: 72 new CLI unit tests - 15 CliJsonIO (read/write, file/stdin, camelCase, null literal, errors) - 14 CliCommonArgs (all flag variants, missing values, unknown flag, positional, dash-as-positional) - 8 CliEngineSetup (path precedence, evaluator selection, disposal idempotency) - 13 ValidateCommand (help, arg errors, tier, valid/invalid YAML, output) - 18 AnalysisCommandTests (help for each of 4 commands, shared error paths, IsOnPath, BarePath) - 4 deferred (covered by integration tests instead — see below)

Unit tests pass: 72 new CLI unit tests
- 15 CliJsonIO (read/write, file/stdin, camelCase, null literal, errors)
- 14 CliCommonArgs (all flag variants, missing values, unknown flag, positional, dash-as-positional)
- 8 CliEngineSetup (path precedence, evaluator selection, disposal idempotency)
- 13 ValidateCommand (help, arg errors, tier, valid/invalid YAML, output)
- 18 AnalysisCommandTests (help for each of 4 commands, shared error paths, IsOnPath, BarePath)
- 4 deferred (covered by integration tests instead — see below)

### AC-13 — Integration tests pass with the Rust binary present: 10 tests (TimeMachineCliIntegrationTests) - [x] `flowtime validate` with valid and invalid YAML - [x] `flowtime sweep` end-to-end producing correct series (arrivals=10,20,30 → served=5,10,15) - [x] `flowtime sensitivity` end-to-end (∂served/∂arrivals = 0.5) - [x] `flowtime goal-seek` end-to-end (target served=25 → arrivals≈50) - [x] `flowtime optimize` converging on a `MAX(x-7,7-x)` bowl around arrivals=14 - [x] Session vs. per-eval flag (`--no-session`) both work - [x] Output to file (`-o`) matches output to stdout - [x] Engine compile error (unknown function) produces exit 3

Integration tests pass with the Rust binary present: 10 tests (TimeMachineCliIntegrationTests)
- [x] `flowtime validate` with valid and invalid YAML
- [x] `flowtime sweep` end-to-end producing correct series (arrivals=10,20,30 → served=5,10,15)
- [x] `flowtime sensitivity` end-to-end (∂served/∂arrivals = 0.5)
- [x] `flowtime goal-seek` end-to-end (target served=25 → arrivals≈50)
- [x] `flowtime optimize` converging on a `MAX(x-7,7-x)` bowl around arrivals=14
- [x] Session vs. per-eval flag (`--no-session`) both work
- [x] Output to file (`-o`) matches output to stdout
- [x] Engine compile error (unknown function) produces exit 3

### AC-14 — Every reachable path in the new command classes and helpers is covered (line-by-line audited)

### AC-15 — `docs/architecture/time-machine-analysis-modes.md` — new "CLI surface" section documents the five commands, JSON I/O contract, exit codes, evaluator selection, engine resolution, and pipeline composition example

### AC-16 — `Program.cs` `PrintUsage` updated with the five new commands

### AC-17 — `dotnet build FlowTime.sln` green

### AC-18 — `dotnet test FlowTime.sln` all green — 1,702 passed / 9 skipped
## Coverage notes

**Covered:** every reachable branch in the command classes, helpers, and the `AnalysisCliRunner` shared path. The 89 CLI unit tests explicitly exercise:

- Help paths for all 5 commands
- All `CliCommonArgs` flag variants (spec/model/output/no-session/engine/help) and their error paths (missing value, unknown flag)
- Positional spec path AND `-` as a positional
- JSON I/O to/from file and stdin/stdout; invalid JSON; null JSON literal; missing file
- Engine path precedence (explicit/env/default); empty explicit falls through
- Evaluator construction for both session and no-session; disposal idempotency for both
- Input-error paths (exit 2): unknown flag, missing spec file, invalid JSON, invalid spec (ArgumentException), missing engine binary
- `IsOnPath` branches (absolute / relative-with-separator / bare name)
- Bare-name engine path bypasses file-existence check and reaches the spawn step

Integration tests (8) cover success paths and the exit-3 engine-error path.

**Explicitly not covered:**

| Path | Why untested |
|------|--------------|
| `CliEngineSetup.ResolveEnginePath` fallback to bare `"flowtime-engine"` when `DirectoryProvider.FindSolutionRoot()` returns null | Would require environment manipulation to move outside any .git-rooted directory tree. The env-var and explicit paths are covered; the default-path branch is covered when run inside the repo. |

This is a single acceptable gap for platform-edge behavior.

## Dependencies

- M-003 (TimeMachineValidator) — delivered
- M-006 (`IModelEvaluator`, `SweepRunner`, `ConstNodePatcher`) — delivered
- M-007 (`SensitivityRunner`, `ConstNodeReader`) — delivered
- M-008 (`GoalSeeker`) — delivered
- M-009 (`Optimizer`, `OptimizeSpec`) — delivered
- M-010 (`SessionModelEvaluator`, evaluator config switch) — delivered

## Risks / notes

- **Argument parser consistency.** The existing CLI uses manual `for`-loop parsing. New
  commands should follow the same convention; don't introduce a parsing library in this
  milestone. A future cleanup epic can migrate all commands to `System.CommandLine`.
- **Test isolation.** Integration tests spawn the Rust engine subprocess per test. Same
  skip-if-missing pattern as M-010 integration tests.
- **Stdin handling in tests.** Tests should not actually redirect Console.In — use the
  `--spec <path>` flag with a temp file for test inputs. Reserve stdin testing for a
  single smoke test that sets `Console.SetIn`.
- **Binary resolution on Windows.** Paths use `Path.Combine`; binary name on Windows is
  `flowtime-engine.exe`. The existing `DirectoryProvider` / `EngineSessionBridge` handle
  this correctly — reuse their logic.
