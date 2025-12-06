using System.Globalization;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using FlowTime.Contracts.TimeTravel;

namespace FlowTime.Cli.Commands;

public static class TelemetryRunCommand
{
    private enum RunReuseMode
    {
        AutoReuse,
        ForceOverwrite,
        Fresh
    }

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    internal static Func<HttpClient>? HttpClientFactoryOverride { get; set; }

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
        bool deterministicExplicit = false;
        bool overwrite = false;
        bool overwriteExplicit = false;
        bool dryRun = false;
        string? runId = null;
        var reuseMode = RunReuseMode.AutoReuse;

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
                    deterministicExplicit = true;
                    break;
                case "--run-id" when i + 1 < args.Length:
                    runId = args[++i];
                    break;
                case "--overwrite":
                    overwrite = true;
                    overwriteExplicit = true;
                    reuseMode = RunReuseMode.ForceOverwrite;
                    break;
                case "--dry-run":
                    dryRun = true;
                    break;
                case "--reuse":
                    reuseMode = RunReuseMode.AutoReuse;
                    break;
                case "--force-overwrite":
                    reuseMode = RunReuseMode.ForceOverwrite;
                    break;
                case "--fresh-run":
                case "--no-reuse":
                    reuseMode = RunReuseMode.Fresh;
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

        if (!string.IsNullOrWhiteSpace(runId) && reuseMode == RunReuseMode.AutoReuse && !deterministicExplicit)
        {
            reuseMode = RunReuseMode.Fresh;
        }

        switch (reuseMode)
        {
            case RunReuseMode.AutoReuse:
                deterministicRunId = true;
                overwrite = false;
                break;
            case RunReuseMode.ForceOverwrite:
                deterministicRunId = true;
                overwrite = true;
                break;
            case RunReuseMode.Fresh:
                if (!deterministicExplicit)
                {
                    deterministicRunId = false;
                }
                if (!overwriteExplicit)
                {
                    overwrite = false;
                }
                break;
        }

        var normalizedMode = NormalizeMode(mode);
        if (normalizedMode is null)
        {
            Console.Error.WriteLine("--mode must be 'telemetry' or 'simulation'.");
            PrintUsage();
            return 2;
        }

        mode = normalizedMode;

        if (string.IsNullOrWhiteSpace(captureDir) && string.Equals(mode, "telemetry", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine("--capture-dir is required for telemetry mode runs.");
            PrintUsage();
            return 2;
        }

        Dictionary<string, JsonElement>? parameterElements = null;
        if (!string.IsNullOrWhiteSpace(paramFile))
        {
            await using var stream = File.OpenRead(paramFile);
            using var doc = await JsonDocument.ParseAsync(stream).ConfigureAwait(false);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                Console.Error.WriteLine("Parameter file must contain a JSON object.");
                return 2;
            }

            parameterElements = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                parameterElements[prop.Name] = prop.Value.Clone();
            }
        }

        if (!string.IsNullOrWhiteSpace(outputRoot))
        {
            Console.WriteLine("Note: --output is managed by FlowTime.Sim Service. Configure FLOWTIME_SIM_DATA_DIR on the service host instead.");
        }

        var request = new RunCreateRequest
        {
            TemplateId = templateId,
            Mode = mode,
            Parameters = parameterElements,
            Telemetry = BuildTelemetryOptions(mode, captureDir, telemetryBindings),
            Options = new RunCreationOptions
            {
                DeterministicRunId = deterministicRunId,
                RunId = runId,
                DryRun = dryRun,
                OverwriteExisting = overwrite
            }
        };

        if (!TryCreateSimHttpClient(out var httpClient, out var httpClientError))
        {
            Console.Error.WriteLine(httpClientError);
            return 1;
        }

        using var client = httpClient;

        try
        {
            var response = await client.PostAsJsonAsync("api/v1/orchestration/runs", request, SerializerOptions).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var errorPayload = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var errorMessage = ExtractErrorMessage(errorPayload, response.StatusCode);
                Console.Error.WriteLine($"Run creation failed: {errorMessage}");
                return 1;
            }

            var payload = await response.Content.ReadFromJsonAsync<RunCreateResponse>(SerializerOptions).ConfigureAwait(false);
            if (payload is null)
            {
                Console.Error.WriteLine("Run creation failed: orchestration response was empty.");
                return 1;
            }

            if (payload.IsDryRun)
            {
                if (payload.Plan is null)
                {
                    Console.Error.WriteLine("Run creation failed: dry-run response missing plan details.");
                    return 1;
                }

                PrintPlanSummary(payload.Plan);
                return 0;
            }

            if (payload.Metadata is null)
            {
                Console.Error.WriteLine("Run creation failed: response missing metadata.");
                return 1;
            }

            PrintRunSummary(payload);
            return 0;
        }
        catch (HttpRequestException ex)
        {
            Console.Error.WriteLine($"Run creation failed: {ex.Message}");
            return 1;
        }
        catch (TaskCanceledException ex)
        {
            Console.Error.WriteLine($"Run creation timed out: {ex.Message}");
            return 1;
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
        Console.WriteLine("  --run-id <value>         Explicit run directory name.");
        Console.WriteLine("  --reuse                  Reuse deterministic bundles when inputs match (default).");
        Console.WriteLine("  --force-overwrite        Regenerate deterministic bundles even if they already exist.");
        Console.WriteLine("  --fresh-run              Always create a new run id (disables reuse unless --deterministic-run-id is set).");
        Console.WriteLine("  --overwrite              Alias for --force-overwrite (backward compatibility).");
        Console.WriteLine("  --deterministic-run-id   Generate deterministic run id based on model hash.");
        Console.WriteLine("  --dry-run                Plan the run without writing files (report planned operations).");
    }

    private static void PrintPlanSummary(RunCreatePlan plan)
    {
        Console.WriteLine("Dry run completed. No files were written.");
        Console.WriteLine($"Template: {plan.TemplateId} (mode: {plan.Mode})");
        Console.WriteLine($"Output root: {plan.OutputRoot}");
        if (!string.IsNullOrWhiteSpace(plan.CaptureDirectory))
        {
            Console.WriteLine($"Capture directory: {plan.CaptureDirectory}");
        }

        if (plan.Parameters.Count > 0)
        {
            Console.WriteLine("Parameters:");
            foreach (var parameter in plan.Parameters)
            {
                Console.WriteLine($"  {parameter.Key}: {FormatPlanValue(parameter.Value)}");
            }
        }

        if (plan.TelemetryBindings.Count > 0)
        {
            Console.WriteLine("Telemetry bindings:");
            foreach (var binding in plan.TelemetryBindings)
            {
                Console.WriteLine($"  {binding.Key} -> {binding.Value}");
            }
        }

        if (plan.Files is { Count: > 0 })
        {
            Console.WriteLine("Planned telemetry files:");
            foreach (var file in plan.Files)
            {
                Console.WriteLine($"  {file.NodeId}:{file.Metric} => {file.Path}");
            }
        }

        PrintPlanWarnings(plan.Warnings);
    }

    private static void PrintRunSummary(RunCreateResponse response)
    {
        var metadata = response.Metadata!;
        Console.WriteLine($"Run created: {metadata.RunId}");

        var directory = ResolveRunDirectory(metadata.Storage);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Console.WriteLine($"Directory: {directory}");
        }

        var templateVersion = string.IsNullOrWhiteSpace(metadata.TemplateVersion) ? "n/a" : metadata.TemplateVersion;
        Console.WriteLine($"Mode: {metadata.Mode}");
        Console.WriteLine($"Template: {metadata.TemplateId} (version: {templateVersion})");
        Console.WriteLine($"Telemetry sources resolved: {metadata.TelemetrySourcesResolved}");
        if (!string.IsNullOrWhiteSpace(metadata.InputHash))
        {
            Console.WriteLine($"Input hash: {metadata.InputHash}");
        }

        if (response.WasReused)
        {
            Console.WriteLine("Existing deterministic bundle reused (pass --force-overwrite for regeneration).");
        }

        PrintTelemetrySummary(response.Telemetry);
        PrintStateWarnings(response.Warnings);
    }

    private static void PrintTelemetrySummary(RunTelemetrySummary? telemetry)
    {
        if (telemetry is null)
        {
            Console.WriteLine("Telemetry: unavailable.");
            return;
        }

        var status = telemetry.Available ? "available" : "not generated";
        var generated = string.IsNullOrWhiteSpace(telemetry.GeneratedAtUtc) ? "n/a" : telemetry.GeneratedAtUtc;
        Console.WriteLine($"Telemetry: {status} (warnings: {telemetry.WarningCount}, generated: {generated})");
        if (!string.IsNullOrWhiteSpace(telemetry.SourceRunId))
        {
            Console.WriteLine($"Telemetry source run: {telemetry.SourceRunId}");
        }
    }

    private static void PrintStateWarnings(IReadOnlyList<StateWarning>? warnings)
    {
        if (warnings is not { Count: > 0 })
        {
            return;
        }

        Console.WriteLine("Warnings:");
        foreach (var warning in warnings)
        {
            var node = string.IsNullOrWhiteSpace(warning.NodeId) ? string.Empty : $" (node {warning.NodeId})";
            Console.WriteLine($"  - {warning.Code}: {warning.Message}{node}");
        }
    }

    private static void PrintPlanWarnings(IReadOnlyList<RunCreatePlanWarning>? warnings)
    {
        if (warnings is not { Count: > 0 })
        {
            return;
        }

        Console.WriteLine("Warnings:");
        foreach (var warning in warnings)
        {
            var node = string.IsNullOrWhiteSpace(warning.NodeId) ? string.Empty : $" (node {warning.NodeId})";
            var bins = warning.Bins is { Count: > 0 } ? $" [bins: {string.Join(",", warning.Bins)}]" : string.Empty;
            Console.WriteLine($"  - {warning.Code}: {warning.Message}{node}{bins}");
        }
    }

    private static string? ResolveRunDirectory(StorageDescriptor storage)
    {
        if (string.IsNullOrWhiteSpace(storage.ModelPath))
        {
            return null;
        }

        var modelDirectory = Path.GetDirectoryName(storage.ModelPath);
        if (string.IsNullOrWhiteSpace(modelDirectory))
        {
            return null;
        }

        var runDirectory = Directory.GetParent(modelDirectory)?.FullName;
        return runDirectory ?? modelDirectory;
    }

    private static string FormatPlanValue(object? value) => value switch
    {
        null => "null",
        string s => s,
        JsonElement element => element.ToString(),
        bool b => b.ToString(),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => JsonSerializer.Serialize(value, value.GetType(), SerializerOptions)
    };

    private static string? NormalizeMode(string requested)
    {
        if (string.IsNullOrWhiteSpace(requested))
        {
            return "telemetry";
        }

        var normalized = requested.Trim().ToLowerInvariant();
        return normalized is "telemetry" or "simulation" ? normalized : null;
    }

    private static RunTelemetryOptions? BuildTelemetryOptions(
        string mode,
        string? captureDir,
        Dictionary<string, string> telemetryBindings)
    {
        if (!string.Equals(mode, "telemetry", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(captureDir))
        {
            return null;
        }

        return new RunTelemetryOptions
        {
            CaptureDirectory = captureDir,
            Bindings = new Dictionary<string, string>(telemetryBindings, StringComparer.OrdinalIgnoreCase)
        };
    }

    private static bool TryCreateSimHttpClient(out HttpClient client, out string? errorMessage)
    {
        if (HttpClientFactoryOverride is not null)
        {
            client = HttpClientFactoryOverride();
            errorMessage = null;
            return true;
        }

        var baseUrl = Environment.GetEnvironmentVariable("FLOWTIME_SIM_API_BASE_URL");
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            baseUrl = "http://localhost:8090/";
        }

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri))
        {
            client = null!;
            errorMessage = $"FLOWTIME_SIM_API_BASE_URL '{baseUrl}' is not a valid absolute URI.";
            return false;
        }

        client = new HttpClient { BaseAddress = baseUri };
        errorMessage = null;
        return true;
    }

    private static string ExtractErrorMessage(string payload, System.Net.HttpStatusCode statusCode)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return $"HTTP {(int)statusCode} ({statusCode})";
        }

        try
        {
            using var doc = JsonDocument.Parse(payload);
            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("error", out var errorProp) &&
                errorProp.ValueKind == JsonValueKind.String)
            {
                return errorProp.GetString() ?? payload;
            }
        }
        catch
        {
            // Ignore parsing errors and fall back to raw payload.
        }

        return payload.Trim();
    }
}
