using System.Text.Json;
using FlowTime.TimeMachine.Sweep;

namespace FlowTime.Cli.Commands;

/// <summary>
/// <c>flowtime sweep</c> — evaluate a model at N values of one const-node parameter.
/// Reads a <see cref="SweepSpec"/> JSON request on stdin or via <c>--spec</c>; writes
/// the <see cref="SweepResult"/> to stdout or a file. Byte-compatible with
/// <c>POST /v1/sweep</c>.
/// </summary>
public static class SweepCommand
{
    public const string HelpText = """
        Usage: flowtime sweep [options]

        Evaluate a model at N values of one const-node parameter.

        Input (JSON body identical to POST /v1/sweep):
          { "modelYaml": "...", "paramId": "arrivals", "values": [10, 20, 30],
            "captureSeriesIds": ["served"] }

        Options:
          -s, --spec <path>     Read JSON spec from a file (default: stdin, or "-").
          -o, --output <path>   Write JSON result to a file (default: stdout, or "-").
          --no-session          Use RustModelEvaluator (per-eval subprocess) instead of session.
          --engine <path>       Override engine binary path.
          -h, --help            Print this help and exit.

        Exit codes:
          0  Success
          2  Input error (missing spec, invalid JSON, engine not found)
          3  Engine/runtime error

        Example:
          cat sweep-spec.json | flowtime sweep | jq '.points[0].series.served'
        """;

    public static Task<int> ExecuteAsync(
        string[] args,
        TextReader stdin,
        TextWriter stdout,
        TextWriter stderr) =>
        AnalysisCliRunner.ExecuteAsync<SweepSpec, SweepResult>(
            args,
            HelpText,
            async (spec, evaluator, ct) => await new SweepRunner(evaluator).RunAsync(spec, ct),
            stdin, stdout, stderr);
}

/// <summary>
/// Shared runner for the four analysis CLI commands (sweep / sensitivity / goal-seek /
/// optimize). Handles arg parsing, spec deserialization, evaluator construction,
/// invoking the analysis delegate, and output — all four commands have the same shape.
/// </summary>
internal static class AnalysisCliRunner
{
    public delegate Task<TResult> AnalysisFunc<TSpec, TResult>(
        TSpec spec, IModelEvaluator evaluator, CancellationToken ct);

    public static async Task<int> ExecuteAsync<TSpec, TResult>(
        string[] args,
        string helpText,
        AnalysisFunc<TSpec, TResult> run,
        TextReader stdin,
        TextWriter stdout,
        TextWriter stderr)
    {
        var parsed = CliCommonArgs.Parse(args);
        if (parsed.ShowHelp)
        {
            stdout.WriteLine(helpText);
            return 0;
        }
        if (parsed.ParseError is not null)
        {
            stderr.WriteLine(parsed.ParseError);
            return 2;
        }

        TSpec spec;
        try
        {
            spec = CliJsonIO.ReadJson<TSpec>(parsed.SpecPath, stdin);
        }
        catch (FileNotFoundException ex)
        {
            stderr.WriteLine(ex.Message);
            return 2;
        }
        catch (JsonException ex)
        {
            stderr.WriteLine($"Invalid JSON: {ex.Message}");
            return 2;
        }
        catch (ArgumentException ex)
        {
            // *Spec record constructors validate inputs and throw ArgumentException,
            // which System.Text.Json surfaces unwrapped during deserialization.
            stderr.WriteLine($"Invalid spec: {ex.Message}");
            return 2;
        }

        var enginePath = CliEngineSetup.ResolveEnginePath(parsed.EnginePath);
        if (!File.Exists(enginePath) && !IsOnPath(enginePath))
        {
            stderr.WriteLine($"Engine binary not found: {enginePath}");
            stderr.WriteLine("Set FLOWTIME_RUST_BINARY, pass --engine, or build with `cargo build --release`.");
            return 2;
        }

        await using var handle = CliEngineSetup.CreateEvaluator(enginePath, useSession: !parsed.NoSession);

        TResult result;
        try
        {
            result = await run(spec, handle.Evaluator, CancellationToken.None);
        }
        catch (InvalidOperationException ex)
        {
            // Engine-session or protocol errors surface here (unknown compile errors,
            // invalid responses from the Rust side, session crashes).
            stderr.WriteLine(ex.Message);
            return 3;
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            // Subprocess spawn failure — engine binary exists in the pre-flight check
            // (or was on $PATH, or was passed bare) but the OS refused to execute it.
            stderr.WriteLine($"Failed to launch engine: {ex.Message}");
            return 3;
        }

        CliJsonIO.WriteJson(parsed.OutputPath, result, stdout);
        return 0;
    }

    /// <summary>
    /// Rough check: does the name appear as a bare executable name that could resolve
    /// through <c>$PATH</c>? This is approximate — the real check happens at spawn time.
    /// </summary>
    internal static bool IsOnPath(string path) =>
        !Path.IsPathRooted(path) && !path.Contains(Path.DirectorySeparatorChar);
}
