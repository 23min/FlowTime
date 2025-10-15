using FlowTime.Cli.Configuration;
using FlowTime.Generator;
using FlowTime.Generator.Capture;
using FlowTime.Generator.Models;
using FlowTime.Generator.Processing;

namespace FlowTime.Cli.Commands;

public static class TelemetryCaptureCommand
{
    public static async Task<int> ExecuteAsync(string[] args)
    {
        if (args.Length == 0 || IsHelp(args[0]))
        {
            PrintUsage();
            return 0;
        }

        if (!string.Equals(args[0], "capture", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine("Unknown telemetry subcommand. Expected: capture");
            PrintUsage();
            return 2;
        }

        string? runDir = null;
        string? outputDir = null;
        var dryRun = false;
        var fillNan = false;
        var gapHandling = GapHandlingMode.Ignore;

        for (var i = 1; i < args.Length; i++)
        {
            var arg = args[i];
            if (IsHelp(arg))
            {
                PrintUsage();
                return 0;
            }

            if (string.Equals(arg, "--run-dir", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= args.Length)
                {
                    Console.Error.WriteLine("Missing value for --run-dir");
                    return 2;
                }

                runDir = args[++i];
                continue;
            }

            if (string.Equals(arg, "--output", StringComparison.OrdinalIgnoreCase) || string.Equals(arg, "--out", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= args.Length)
                {
                    Console.Error.WriteLine("Missing value for --output");
                    return 2;
                }

                outputDir = args[++i];
                continue;
            }

            if (string.Equals(arg, "--dry-run", StringComparison.OrdinalIgnoreCase))
            {
                dryRun = true;
                continue;
            }

            if (string.Equals(arg, "--gap-fill-nan", StringComparison.OrdinalIgnoreCase))
            {
                fillNan = true;
                continue;
            }

            if (string.Equals(arg, "--gap-detect", StringComparison.OrdinalIgnoreCase))
            {
                gapHandling = GapHandlingMode.WarnOnly;
                continue;
            }

            if (string.Equals(arg, "--gap-fill-gaps", StringComparison.OrdinalIgnoreCase))
            {
                gapHandling = GapHandlingMode.FillWithZero;
                continue;
            }

            Console.Error.WriteLine($"Unknown option: {arg}");
            PrintUsage();
            return 2;
        }

        if (string.IsNullOrWhiteSpace(runDir))
        {
            Console.Error.WriteLine("--run-dir is required.");
            PrintUsage();
            return 2;
        }

        try
        {
            runDir = Path.GetFullPath(runDir);
            if (!Directory.Exists(runDir))
            {
                Console.Error.WriteLine($"Run directory not found: {runDir}");
                return 1;
            }

            var reader = new RunArtifactReader();
            if (string.IsNullOrWhiteSpace(outputDir))
            {
                var context = await reader.ReadAsync(runDir).ConfigureAwait(false);
                var telemetryRoot = Path.Combine(OutputDirectoryProvider.GetDefaultOutputDirectory(), "telemetry");
                outputDir = Path.Combine(telemetryRoot, context.Manifest.RunId);
            }

            outputDir = Path.GetFullPath(outputDir);

            var capture = new TelemetryCapture(reader);
            var options = new TelemetryCaptureOptions
            {
                RunDirectory = runDir,
                OutputDirectory = outputDir,
                DryRun = dryRun,
                GapOptions = new GapInjectorOptions(
                    FillNaNWithZero: fillNan,
                    MissingValueHandling: gapHandling)
            };

            var plan = await capture.ExecuteAsync(options).ConfigureAwait(false);
            PrintPlan(plan, dryRun);
            return 0;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Telemetry capture canceled.");
            return 130;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Telemetry capture failed: {ex.Message}");
            return 1;
        }
    }

    private static void PrintPlan(TelemetryCapturePlan plan, bool dryRun)
    {
        Console.WriteLine($"Run ID: {plan.RunId}");
        Console.WriteLine($"Output Directory: {plan.OutputDirectory}");
        Console.WriteLine($"Files ({plan.Files.Count}):");
        foreach (var file in plan.Files)
        {
            Console.WriteLine($"  - {file.TargetFileName} ({file.NodeId}:{file.Metric}, source={file.SourceSeriesId})");
        }

        if (plan.Warnings.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Warnings:");
            foreach (var warning in plan.Warnings)
            {
                var bins = warning.Bins is { Count: > 0 } ? $" [bins: {string.Join(",", warning.Bins)}]" : string.Empty;
                var node = string.IsNullOrWhiteSpace(warning.NodeId) ? string.Empty : $" (node {warning.NodeId})";
                Console.WriteLine($"  - {warning.Code}: {warning.Message}{node}{bins}");
            }
        }

        Console.WriteLine();
        if (dryRun)
        {
            Console.WriteLine("Dry-run complete. No files were written.");
        }
        else
        {
            var manifestPath = Path.Combine(plan.OutputDirectory, "manifest.json");
            Console.WriteLine($"Capture complete. Manifest written to {manifestPath}");
        }
    }

    private static bool IsHelp(string value) => value switch
    {
        "-h" or "--help" or "help" => true,
        _ => false
    };

    private static void PrintUsage()
    {
        Console.WriteLine("Telemetry Capture");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  flowtime telemetry capture --run-dir <path> [--output <dir>] [--dry-run] [--gap-fill-nan]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --run-dir <path>     Source run directory produced by FlowTime Engine.");
        Console.WriteLine("  --output <dir>       Target directory for telemetry bundle (default: $FLOWTIME_DATA_DIR/telemetry/<runId>)." );
        Console.WriteLine("                       --out is accepted as an alias.");
        Console.WriteLine("  --dry-run            Describe the capture without writing files.");
        Console.WriteLine("  --gap-fill-nan       Replace NaN/∞ values with zero and record a warning.");
        Console.WriteLine("  --gap-detect         Emit data_gap warnings for missing bins while preserving gaps.");
        Console.WriteLine("  --gap-fill-gaps      Fill missing bins with zero and emit data_gap warnings.");
    }
}
