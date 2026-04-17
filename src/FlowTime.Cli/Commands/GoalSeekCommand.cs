using FlowTime.TimeMachine.Sweep;

namespace FlowTime.Cli.Commands;

/// <summary>
/// <c>flowtime goal-seek</c> — 1D bisection to find the parameter value that drives a
/// metric to a target. JSON-over-stdio, byte-compatible with <c>POST /v1/goal-seek</c>.
/// </summary>
public static class GoalSeekCommand
{
    public const string HelpText = """
        Usage: flowtime goal-seek [options]

        Find the parameter value that drives a metric mean to a target via bisection.

        Input (JSON body identical to POST /v1/goal-seek):
          { "modelYaml": "...", "paramId": "arrivals", "metricSeriesId": "served",
            "targetValue": 25, "searchLo": 0, "searchHi": 100, "tolerance": 0.01 }

        Options:
          -s, --spec <path>     Read JSON spec from a file (default: stdin, or "-").
          -o, --output <path>   Write JSON result to a file (default: stdout, or "-").
          --no-session          Use RustModelEvaluator instead of session.
          --engine <path>       Override engine binary path.
          -h, --help            Print this help and exit.

        Example:
          cat goal.json | flowtime goal-seek | jq '.paramValue'
        """;

    public static Task<int> ExecuteAsync(
        string[] args,
        TextReader stdin,
        TextWriter stdout,
        TextWriter stderr) =>
        AnalysisCliRunner.ExecuteAsync<GoalSeekSpec, GoalSeekResult>(
            args,
            HelpText,
            async (spec, evaluator, ct) =>
                await new GoalSeeker(new SweepRunner(evaluator)).SeekAsync(spec, ct),
            stdin, stdout, stderr);
}
