using FlowTime.TimeMachine.Sweep;

namespace FlowTime.Cli.Commands;

/// <summary>
/// <c>flowtime sensitivity</c> — per-parameter metric sensitivity via central difference.
/// JSON-over-stdio, byte-compatible with <c>POST /v1/sensitivity</c>.
/// </summary>
public static class SensitivityCommand
{
    public const string HelpText = """
        Usage: flowtime sensitivity [options]

        Per-parameter metric sensitivity via central-difference derivatives.

        Input (JSON body identical to POST /v1/sensitivity):
          { "modelYaml": "...", "paramIds": ["arrivals","capacity"],
            "metricSeriesId": "served", "delta": 0.01 }

        Options:
          -s, --spec <path>     Read JSON spec from a file (default: stdin, or "-").
          -o, --output <path>   Write JSON result to a file (default: stdout, or "-").
          --no-session          Use RustModelEvaluator instead of session.
          --engine <path>       Override engine binary path.
          -h, --help            Print this help and exit.

        Example:
          cat sens.json | flowtime sensitivity | jq '.entries[] | {param, dMetric_dParam}'
        """;

    public static Task<int> ExecuteAsync(
        string[] args,
        TextReader stdin,
        TextWriter stdout,
        TextWriter stderr) =>
        AnalysisCliRunner.ExecuteAsync<SensitivitySpec, SensitivityResult>(
            args,
            HelpText,
            async (spec, evaluator, ct) =>
                await new SensitivityRunner(new SweepRunner(evaluator)).RunAsync(spec, ct),
            stdin, stdout, stderr);
}
