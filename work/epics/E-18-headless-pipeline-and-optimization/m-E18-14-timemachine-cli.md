# m-E18-14 — .NET Time Machine CLI

**Epic:** E-18 Time Machine
**Branch:** `milestone/m-E18-14-timemachine-cli` (from `epic/E-18-time-machine`)
**Status:** in-progress

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

## Acceptance Criteria

- [ ] Five CLI commands (`validate`, `sweep`, `sensitivity`, `goal-seek`, `optimize`) wired into `Program.cs` router
- [ ] Each command parses `--spec` / stdin, `--output` / stdout, `--no-session`, `--engine`, `--help`
- [ ] `validate` reads YAML (not JSON) via `--model` / stdin; outputs `ValidationResult` as JSON
- [ ] Each analysis command reads its matching `*Spec` as JSON and writes its matching result as JSON, byte-compatible with the corresponding `/v1/` endpoint
- [ ] `CliEngineSetup` helper resolves binary path via `--engine` → `FLOWTIME_RUST_BINARY` → solution-relative default → `$PATH`
- [ ] `CliEngineSetup` constructs `SessionModelEvaluator` by default; `--no-session` selects `RustModelEvaluator`
- [ ] `CliJsonIO` helper reads JSON from stdin-or-file and writes JSON to stdout-or-file with camelCase / web defaults matching the API
- [ ] Exit codes follow the 0/1/2/3 contract (success / analysis-failed / input-error / engine-error)
- [ ] Missing engine binary produces exit 2 with a readable stderr message
- [ ] Invalid JSON produces exit 2 with a stderr message; no partial stdout
- [ ] `--help` on any command prints command-specific usage and exits 0
- [ ] Unit tests pass for each command: missing-args, invalid-JSON, help, success-to-stdout, output-to-file
- [ ] Integration tests pass with the Rust binary present, covering:
  - [ ] `flowtime validate` with valid and invalid YAML
  - [ ] `flowtime sweep` end-to-end producing correct series
  - [ ] `flowtime optimize` converging on a bowl function
  - [ ] Session vs. per-eval flag (`--no-session`) both work
  - [ ] Output to file (`-o`) matches output to stdout
- [ ] Branch coverage of every reachable path in the new command classes and helpers
- [ ] `docs/architecture/time-machine-analysis-modes.md` — new "CLI surface" section documents the five commands, JSON I/O contract, exit codes, and the pipeline composition example
- [ ] `Program.cs` `PrintUsage` updated with the five new commands
- [ ] `dotnet build FlowTime.sln` green
- [ ] `dotnet test FlowTime.sln` all green

## Dependencies

- m-E18-06 (TimeMachineValidator) — delivered
- m-E18-09 (`IModelEvaluator`, `SweepRunner`, `ConstNodePatcher`) — delivered
- m-E18-10 (`SensitivityRunner`, `ConstNodeReader`) — delivered
- m-E18-11 (`GoalSeeker`) — delivered
- m-E18-12 (`Optimizer`, `OptimizeSpec`) — delivered
- m-E18-13 (`SessionModelEvaluator`, evaluator config switch) — delivered

## Risks / notes

- **Argument parser consistency.** The existing CLI uses manual `for`-loop parsing. New
  commands should follow the same convention; don't introduce a parsing library in this
  milestone. A future cleanup epic can migrate all commands to `System.CommandLine`.
- **Test isolation.** Integration tests spawn the Rust engine subprocess per test. Same
  skip-if-missing pattern as m-E18-13 integration tests.
- **Stdin handling in tests.** Tests should not actually redirect Console.In — use the
  `--spec <path>` flag with a temp file for test inputs. Reserve stdin testing for a
  single smoke test that sets `Console.SetIn`.
- **Binary resolution on Windows.** Paths use `Path.Combine`; binary name on Windows is
  `flowtime-engine.exe`. The existing `DirectoryProvider` / `EngineSessionBridge` handle
  this correctly — reuse their logic.
