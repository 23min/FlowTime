using FlowTime.TimeMachine.Validation;

namespace FlowTime.Cli.Commands;

/// <summary>
/// <c>flowtime validate</c> — tiered model validation (schema / compile / analyse).
/// Reads YAML from a file path or stdin; writes a JSON response to stdout or a file,
/// byte-compatible with <c>POST /v1/validate</c>.
/// </summary>
public static class ValidateCommand
{
    public const string HelpText = """
        Usage: flowtime validate [<model.yaml>] [options]

        Validate a model YAML at the specified tier (default: analyse).

        Input:
          <model.yaml>                     Positional path to model YAML. Omit or pass "-" for stdin.
          -m, --model <path>               Alternative to the positional argument.

        Options:
          --tier <schema|compile|analyse>  Validation depth (default: analyse).
          -o, --output <path>              Write JSON result to a file instead of stdout ("-" = stdout).
          -h, --help                       Print this help and exit.

        Exit codes:
          0  Valid
          1  Invalid (JSON response is still written)
          2  Input error (missing YAML, malformed args)

        Examples:
          flowtime validate examples/hello/model.yaml
          cat model.yaml | flowtime validate --tier schema
        """;

    public static async Task<int> ExecuteAsync(
        string[] args,
        TextReader stdin,
        TextWriter stdout,
        TextWriter stderr)
    {
        var (parsed, tier, tierError) = ParseArgs(args);
        if (parsed.ShowHelp)
        {
            stdout.WriteLine(HelpText);
            return 0;
        }
        if (parsed.ParseError is not null)
        {
            stderr.WriteLine(parsed.ParseError);
            return 2;
        }
        if (tierError is not null)
        {
            stderr.WriteLine(tierError);
            return 2;
        }

        string yaml;
        try
        {
            yaml = CliJsonIO.ReadYaml(parsed.SpecPath, stdin);
        }
        catch (FileNotFoundException ex)
        {
            stderr.WriteLine(ex.Message);
            return 2;
        }

        var result = TimeMachineValidator.Validate(yaml, tier);
        var response = new CliValidationResponse(
            Tier: result.Tier.ToString().ToLowerInvariant(),
            IsValid: result.IsValid,
            Errors: result.Errors.Select(e => new CliValidationError(e.Message)).ToList(),
            Warnings: result.Warnings
                .Select(w => new CliValidationWarning(w.NodeId, w.Code, w.Message)).ToList());

        CliJsonIO.WriteJson(parsed.OutputPath, response, stdout);

        await Task.CompletedTask;
        return result.IsValid ? 0 : 1;
    }

    internal static (CliCommonArgs parsed, ValidationTier tier, string? tierError)
        ParseArgs(string[] args)
    {
        // Extract --tier <value> before handing to the common parser.
        ValidationTier tier = ValidationTier.Analyse;
        string? tierError = null;
        var filtered = new List<string>();

        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--tier")
            {
                if (i + 1 >= args.Length)
                {
                    tierError = "Missing value for --tier";
                    break;
                }
                var raw = args[++i];
                if (!Enum.TryParse<ValidationTier>(raw, ignoreCase: true, out var parsed))
                {
                    tierError = $"Invalid tier '{raw}'. Valid values: schema, compile, analyse.";
                }
                else
                {
                    tier = parsed;
                }
            }
            else
            {
                filtered.Add(args[i]);
            }
        }

        return (CliCommonArgs.Parse(filtered.ToArray()), tier, tierError);
    }

    // DTOs byte-compatible with the /v1/validate response.
    internal sealed record CliValidationResponse(
        string Tier,
        bool IsValid,
        List<CliValidationError> Errors,
        List<CliValidationWarning> Warnings);

    internal sealed record CliValidationError(string Message);

    internal sealed record CliValidationWarning(string NodeId, string Code, string Message);
}
