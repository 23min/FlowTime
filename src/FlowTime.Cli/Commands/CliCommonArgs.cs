namespace FlowTime.Cli.Commands;

/// <summary>
/// Options parsed off the Time Machine CLI command argument list. All commands share
/// these; the analysis commands additionally read a spec file/stdin.
/// </summary>
public sealed class CliCommonArgs
{
    /// <summary>True when the user passed <c>-h</c>, <c>--help</c>, or <c>/?</c>.</summary>
    public bool ShowHelp { get; init; }

    /// <summary>Path to a spec/model file, or <c>"-"</c> / <c>null</c> for stdin.</summary>
    public string? SpecPath { get; init; }

    /// <summary>Path to write the result to, or <c>"-"</c> / <c>null</c> for stdout.</summary>
    public string? OutputPath { get; init; }

    /// <summary>True if <c>--no-session</c> was passed (use RustModelEvaluator fallback).</summary>
    public bool NoSession { get; init; }

    /// <summary>Explicit engine binary path from <c>--engine</c>, or null to resolve.</summary>
    public string? EnginePath { get; init; }

    /// <summary>Unknown / malformed argument message, set by the parser when parsing fails.</summary>
    public string? ParseError { get; init; }

    /// <summary>
    /// Parse arguments shared across all Time Machine CLI commands. Recognizes:
    /// <c>--spec</c>/<c>--model</c>/<c>-s</c>/<c>-m</c>, <c>--output</c>/<c>-o</c>,
    /// <c>--no-session</c>, <c>--engine</c>, <c>--help</c>/<c>-h</c>/<c>/?</c>.
    ///
    /// <para>
    /// <paramref name="args"/> must start at the argument AFTER the command name (the
    /// caller slices off args[0]).
    /// </para>
    /// </summary>
    public static CliCommonArgs Parse(string[] args)
    {
        string? specPath = null;
        string? outputPath = null;
        bool noSession = false;
        string? enginePath = null;
        bool showHelp = false;

        for (var i = 0; i < args.Length; i++)
        {
            var a = args[i];
            if (a is "-h" or "--help" or "/?")
            {
                showHelp = true;
            }
            else if (a is "--spec" or "--model" or "-s" or "-m")
            {
                if (i + 1 >= args.Length)
                {
                    return new CliCommonArgs { ParseError = $"Missing value for {a}" };
                }
                specPath = args[++i];
            }
            else if (a is "--output" or "-o")
            {
                if (i + 1 >= args.Length)
                {
                    return new CliCommonArgs { ParseError = $"Missing value for {a}" };
                }
                outputPath = args[++i];
            }
            else if (a == "--no-session")
            {
                noSession = true;
            }
            else if (a == "--engine")
            {
                if (i + 1 >= args.Length)
                {
                    return new CliCommonArgs { ParseError = "Missing value for --engine" };
                }
                enginePath = args[++i];
            }
            else if (i == 0 && (a == "-" || !a.StartsWith('-')))
            {
                // Positional: first non-flag argument (or the literal "-") is treated
                // as the spec/model path. "-" is a conventional alias for stdin.
                specPath = a;
            }
            else
            {
                return new CliCommonArgs { ParseError = $"Unknown argument: {a}" };
            }
        }

        return new CliCommonArgs
        {
            ShowHelp = showHelp,
            SpecPath = specPath,
            OutputPath = outputPath,
            NoSession = noSession,
            EnginePath = enginePath,
        };
    }
}
