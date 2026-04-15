using FlowTime.TimeMachine.Sweep;

namespace FlowTime.Cli.Commands;

/// <summary>
/// <c>flowtime optimize</c> — multi-parameter Nelder-Mead optimization over const nodes.
/// JSON-over-stdio, byte-compatible with <c>POST /v1/optimize</c>.
/// </summary>
public static class OptimizeCommand
{
    public const string HelpText = """
        Usage: flowtime optimize [options]

        Multi-parameter Nelder-Mead simplex optimization over const-node parameters.

        Input (JSON body identical to POST /v1/optimize):
          { "modelYaml": "...", "paramIds": ["arrivals","capacity"],
            "metricSeriesId": "util", "objective": "minimize",
            "searchRanges": { "arrivals": {"lo":0,"hi":200},
                              "capacity": {"lo":1,"hi":20} },
            "tolerance": 1e-4, "maxIterations": 200 }

        Options:
          -s, --spec <path>     Read JSON spec from a file (default: stdin, or "-").
          -o, --output <path>   Write JSON result to a file (default: stdout, or "-").
          --no-session          Use RustModelEvaluator instead of session.
          --engine <path>       Override engine binary path.
          -h, --help            Print this help and exit.

        Example:
          cat opt.json | flowtime optimize | jq '{params: .paramValues, metric: .achievedMetricMean}'
        """;

    public static Task<int> ExecuteAsync(
        string[] args,
        TextReader stdin,
        TextWriter stdout,
        TextWriter stderr) =>
        AnalysisCliRunner.ExecuteAsync<OptimizeSpec, OptimizeResult>(
            args,
            HelpText,
            async (spec, evaluator, ct) =>
                await new Optimizer(evaluator).OptimizeAsync(spec, ct),
            stdin, stdout, stderr);
}
