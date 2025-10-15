using FlowTime.Cli.Configuration;
using FlowTime.Generator;

namespace FlowTime.Cli.Commands;

public static class TelemetryBundleCommand
{
    public static async Task<int> ExecuteAsync(string[] args)
    {
        if (args.Length == 0 || IsHelp(args[0]))
        {
            PrintUsage();
            return 0;
        }

        if (!string.Equals(args[0], "bundle", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine("Unknown telemetry subcommand. Expected: bundle");
            PrintUsage();
            return 2;
        }

        string? captureDir = null;
        string? modelPath = null;
        string? outputRoot = null;
        string? provenancePath = null;
        string? explicitRunId = null;
        var overwrite = false;
        var deterministicRunId = false;

        for (var i = 1; i < args.Length; i++)
        {
            var arg = args[i];
            if (IsHelp(arg))
            {
                PrintUsage();
                return 0;
            }

            if (string.Equals(arg, "--capture-dir", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= args.Length)
                {
                    Console.Error.WriteLine("Missing value for --capture-dir");
                    return 2;
                }

                captureDir = args[++i];
                continue;
            }

            if (string.Equals(arg, "--model", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= args.Length)
                {
                    Console.Error.WriteLine("Missing value for --model");
                    return 2;
                }

                modelPath = args[++i];
                continue;
            }

            if (string.Equals(arg, "--output", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(arg, "--out", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= args.Length)
                {
                    Console.Error.WriteLine("Missing value for --output");
                    return 2;
                }

                outputRoot = args[++i];
                continue;
            }

            if (string.Equals(arg, "--provenance", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= args.Length)
                {
                    Console.Error.WriteLine("Missing value for --provenance");
                    return 2;
                }

                provenancePath = args[++i];
                continue;
            }

            if (string.Equals(arg, "--run-id", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= args.Length)
                {
                    Console.Error.WriteLine("Missing value for --run-id");
                    return 2;
                }

                explicitRunId = args[++i];
                continue;
            }

            if (string.Equals(arg, "--overwrite", StringComparison.OrdinalIgnoreCase))
            {
                overwrite = true;
                continue;
            }

            if (string.Equals(arg, "--deterministic-run-id", StringComparison.OrdinalIgnoreCase))
            {
                deterministicRunId = true;
                continue;
            }

            if (string.Equals(arg, "--random-run-id", StringComparison.OrdinalIgnoreCase))
            {
                deterministicRunId = false;
                continue;
            }

            Console.Error.WriteLine($"Unknown option: {arg}");
            PrintUsage();
            return 2;
        }

        if (string.IsNullOrWhiteSpace(captureDir))
        {
            Console.Error.WriteLine("--capture-dir is required.");
            PrintUsage();
            return 2;
        }

        if (string.IsNullOrWhiteSpace(modelPath))
        {
            Console.Error.WriteLine("--model is required.");
            PrintUsage();
            return 2;
        }

        var outputDirectory = string.IsNullOrWhiteSpace(outputRoot)
            ? Path.Combine(OutputDirectoryProvider.GetDefaultOutputDirectory(), "runs")
            : outputRoot;

        try
        {
            var options = new TelemetryBundleOptions
            {
                CaptureDirectory = Path.GetFullPath(captureDir),
                ModelPath = Path.GetFullPath(modelPath),
                OutputRoot = Path.GetFullPath(outputDirectory),
                ProvenancePath = provenancePath is null ? null : Path.GetFullPath(provenancePath),
                RunId = explicitRunId,
                Overwrite = overwrite,
                DeterministicRunId = deterministicRunId
            };

            var builder = new TelemetryBundleBuilder();
            var result = await builder.BuildAsync(options).ConfigureAwait(false);

            Console.WriteLine($"Bundle written to: {result.RunDirectory}");
            Console.WriteLine($"Run ID: {result.RunId}");
            Console.WriteLine($"Telemetry files: {result.TelemetryManifest.Files?.Count ?? 0}");
            return 0;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Telemetry bundle creation canceled.");
            return 130;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Telemetry bundle failed: {ex.Message}");
            return 1;
        }
    }

    private static bool IsHelp(string value) => value switch
    {
        "-h" or "--help" or "help" => true,
        _ => false
    };

    private static void PrintUsage()
    {
        Console.WriteLine("Telemetry Bundle");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  flowtime telemetry bundle --capture-dir <path> --model <model.yaml> [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --capture-dir <path>    Telemetry capture directory containing manifest.json and CSV files.");
        Console.WriteLine("  --model <path>          FlowTime-Sim generated model.yaml to normalise.");
        Console.WriteLine("  --output <dir>          Root directory for canonical run output (default: $FLOWTIME_DATA_DIR/runs).");
        Console.WriteLine("                          --out is accepted as an alias.");
        Console.WriteLine("  --provenance <path>     Optional provenance JSON emitted by FlowTime-Sim.");
        Console.WriteLine("  --run-id <value>        Explicit run directory name.");
        Console.WriteLine("  --overwrite             Overwrite existing run directory when --run-id is supplied.");
        Console.WriteLine("  --deterministic-run-id  Derive run ID deterministically from the model spec.");
        Console.WriteLine("  --random-run-id         Generate a unique run ID (default).");
    }
}
