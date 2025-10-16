using System.Text.Json;
using FlowTime.Cli.Configuration;
using FlowTime.Generator;
using FlowTime.Generator.Orchestration;
using FlowTime.Sim.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace FlowTime.Cli.Commands;

public static class TelemetryRunCommand
{
    public static async Task<int> ExecuteAsync(string[] args)
    {
        if (args.Length == 0 || IsHelp(args[0]))
        {
            PrintUsage();
            return 0;
        }

        if (!string.Equals(args[0], "run", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine("Unknown telemetry subcommand. Expected: run");
            PrintUsage();
            return 2;
        }

        string? templateId = null;
        string? captureDir = null;
        string mode = "telemetry";
        string? outputRoot = null;
        string? paramFile = null;
        var telemetryBindings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        bool deterministicRunId = false;
        bool overwrite = false;
        string? runId = null;

        for (var i = 1; i < args.Length; i++)
        {
            var arg = args[i];
            if (IsHelp(arg))
            {
                PrintUsage();
                return 0;
            }

            switch (arg)
            {
                case "--template-id" when i + 1 < args.Length:
                    templateId = args[++i];
                    break;
                case "--capture-dir" when i + 1 < args.Length:
                    captureDir = args[++i];
                    break;
                case "--mode" when i + 1 < args.Length:
                    mode = args[++i];
                    break;
                case "--out" when i + 1 < args.Length:
                case "--output" when i + 1 < args.Length:
                    outputRoot = args[++i];
                    break;
                case "--param-file" when i + 1 < args.Length:
                    paramFile = args[++i];
                    break;
                case "--bind" when i + 1 < args.Length:
                {
                    var binding = args[++i];
                    var split = binding.Split('=', 2, StringSplitOptions.RemoveEmptyEntries);
                    if (split.Length != 2)
                    {
                        Console.Error.WriteLine($"Invalid binding '{binding}'. Expected key=FILE");
                        return 2;
                    }

                    telemetryBindings[split[0]] = split[1];
                    break;
                }
                case "--deterministic-run-id":
                    deterministicRunId = true;
                    break;
                case "--run-id" when i + 1 < args.Length:
                    runId = args[++i];
                    break;
                case "--overwrite":
                    overwrite = true;
                    break;
                default:
                    Console.Error.WriteLine($"Unknown option: {arg}");
                    PrintUsage();
                    return 2;
            }
        }

        if (string.IsNullOrWhiteSpace(templateId))
        {
            Console.Error.WriteLine("--template-id is required.");
            PrintUsage();
            return 2;
        }

        if (string.IsNullOrWhiteSpace(captureDir) && string.Equals(mode, "telemetry", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine("--capture-dir is required for telemetry mode runs.");
            PrintUsage();
            return 2;
        }

        var runsRoot = string.IsNullOrWhiteSpace(outputRoot)
            ? Path.Combine(OutputDirectoryProvider.GetDefaultOutputDirectory(), "runs")
            : outputRoot;

        Dictionary<string, object?>? parameters = null;
        if (!string.IsNullOrWhiteSpace(paramFile))
        {
            var json = await File.ReadAllTextAsync(paramFile);
            var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                Console.Error.WriteLine("Parameter file must contain a JSON object.");
                return 2;
            }

            parameters = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                parameters[prop.Name] = ConvertJsonElement(prop.Value);
            }
        }

        var templatesDir = Environment.GetEnvironmentVariable("FLOWTIME_TEMPLATES_DIR");
        if (string.IsNullOrWhiteSpace(templatesDir))
        {
            var solutionRoot = DirectoryProvider.FindSolutionRoot();
            templatesDir = solutionRoot is not null
                ? Path.Combine(solutionRoot, "templates")
                : Path.Combine(AppContext.BaseDirectory, "templates");
        }

        Directory.CreateDirectory(templatesDir!);

        var templateService = new SimTemplateService(templatesDir!, NullLogger<SimTemplateService>.Instance);
        var bundleBuilder = new TelemetryBundleBuilder();
        var orchestration = new RunOrchestrationService(templateService, bundleBuilder, NullLogger<RunOrchestrationService>.Instance);

        var request = new RunOrchestrationRequest
        {
            TemplateId = templateId,
            Mode = mode,
            CaptureDirectory = captureDir,
            TelemetryBindings = telemetryBindings,
            Parameters = parameters,
            OutputRoot = runsRoot,
            DeterministicRunId = deterministicRunId,
            RunId = runId,
            DryRun = false,
            OverwriteExisting = overwrite
        };

        try
        {
            var result = await orchestration.CreateRunAsync(request).ConfigureAwait(false);
            Console.WriteLine($"Run created: {result.RunId}");
            Console.WriteLine($"Directory: {result.RunDirectory}");
            Console.WriteLine($"Mode: {result.ManifestMetadata.Mode}");
            Console.WriteLine($"Template: {result.ManifestMetadata.TemplateId}");
            Console.WriteLine($"Telemetry sources resolved: {result.TelemetrySourcesResolved}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Run creation failed: {ex.Message}");
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
        Console.WriteLine("Telemetry Run");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  flowtime telemetry run --template-id <id> --capture-dir <path> [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --template-id <id>       Template identifier (required).");
        Console.WriteLine("  --capture-dir <path>     Telemetry capture directory (required for telemetry mode).");
        Console.WriteLine("  --mode <mode>            Run mode (telemetry|simulation, default telemetry).");
        Console.WriteLine("  --param-file <path>      JSON file with template parameter overrides.");
        Console.WriteLine("  --bind key=FILE          Bind template telemetry parameter to CSV file within capture directory.");
        Console.WriteLine("  --output <dir>           Output directory for canonical run (default: $FLOWTIME_DATA_DIR/runs).");
        Console.WriteLine("  --deterministic-run-id   Generate deterministic run id based on model hash.");
        Console.WriteLine("  --run-id <value>         Explicit run directory name.");
        Console.WriteLine("  --overwrite              Overwrite existing run directory when --run-id is supplied.");
    }

    private static object? ConvertJsonElement(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number => element.TryGetInt64(out var integer) ? integer : element.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        JsonValueKind.Array => element.EnumerateArray().Select(ConvertJsonElement).ToArray(),
        JsonValueKind.Object => element.EnumerateObject().ToDictionary(p => p.Name, p => ConvertJsonElement(p.Value), StringComparer.OrdinalIgnoreCase),
        _ => element.GetRawText()
    };
}
