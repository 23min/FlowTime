using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using FlowTime.Sim.Core; // bring core simulation types into scope

#pragma warning disable CS0618 // Type or member is obsolete - CLI is the intended consumer of legacy code

namespace FlowTime.Sim.Cli
{
    public sealed record RunOptions(
        string ModelPath,
        string FlowTimeUrl,
        string? OutPath,
        string Format,
        bool Verbose,
        string Mode, // engine | sim
        string? DebugEventsPath, // Optional debug events file
        string ApiVersion // FlowTime API version (e.g., "v1")
    )
    {
        public static RunOptions Defaults => new("", 
            "http://localhost:8080", 
            null, "csv", false, "engine", null,
            "v1");
    }

    internal static class Program
    {
        internal static async Task<int> Main(string[] args)
        {
            try
            {
                CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
                CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

                var opts = ArgParser.ParseArgs(args);
                if (string.IsNullOrWhiteSpace(opts.ModelPath) || !File.Exists(opts.ModelPath))
                {
                    Console.Error.WriteLine("Missing --model <path> or file does not exist.");
                    return 2;
                }

                var yaml = await File.ReadAllTextAsync(opts.ModelPath, Encoding.UTF8);
                using var http = new HttpClient();
                http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                // Validate format early (csv|json) – keeps behavior explicit if a typo occurs
                if (!opts.Format.Equals("csv", StringComparison.OrdinalIgnoreCase) &&
                    !opts.Format.Equals("json", StringComparison.OrdinalIgnoreCase))
                {
                    Console.Error.WriteLine($"Unsupported --format '{opts.Format}'. Expected 'csv' or 'json'.");
                    return 2;
                }

                var cts = new CancellationTokenSource();
                Console.CancelKeyPress += (s, e) => { e.Cancel = true; cts.Cancel(); };

                if (opts.Mode.Equals("engine", StringComparison.OrdinalIgnoreCase))
                {
                    // Heuristic: if YAML contains simulation-only keys, guide user.
                    if (yaml.Contains("schemaVersion:") && yaml.Contains("arrivals:"))
                    {
                        Console.Error.WriteLine("Spec appears to be a simulation spec (contains schemaVersion & arrivals). Use --mode sim or supply an engine model (grid + nodes).");
                        return 2;
                    }
                    var res = await FlowTimeClient.RunAsync(http, opts.FlowTimeUrl, yaml, cts.Token, opts.ApiVersion);
                    if (opts.Verbose)
                    {
                        Console.WriteLine("Mode: engine");
                        Console.WriteLine($"Grid: bins={res.grid.bins}, binMinutes={res.grid.binMinutes}");
                        Console.WriteLine("Order: " + string.Join(",", res.order));
                    }
                    if (opts.OutPath is null)
                    {
                        await using var stdout = Console.OpenStandardOutput();
                        if (opts.Format.Equals("json", StringComparison.OrdinalIgnoreCase))
                            await Writers.WriteJsonAsync(res, stdout, cts.Token);
                        else
                            await Writers.WriteCsvAsync(res, stdout, cts.Token);
                    }
                    else
                    {
                        var full = Path.GetFullPath(opts.OutPath);
                        var dir = Path.GetDirectoryName(full);
                        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                        await using var fs = File.Create(full);
                        if (opts.Format.Equals("json", StringComparison.OrdinalIgnoreCase))
                            await Writers.WriteJsonAsync(res, fs, cts.Token);
                        else
                            await Writers.WriteCsvAsync(res, fs, cts.Token);

                        Console.WriteLine($"Wrote {opts.Format} -> {full}");
                    }
                }
                else if (opts.Mode.Equals("sim", StringComparison.OrdinalIgnoreCase))
                {
                    var spec = SimulationSpecLoader.LoadFromString(yaml);
                    var validation = SimulationSpecValidator.Validate(spec);
                    if (!validation.IsValid)
                    {
                        Console.Error.WriteLine("Spec validation failed:\n - " + string.Join("\n - ", validation.Errors));
                        return 2;
                    }
                    var arrivals = ArrivalGenerators.Generate(spec);
                    var events = EventFactory.BuildEvents(spec, arrivals);

                    // Determine output directory.
                    string outDir;
                    if (opts.OutPath is null)
                    {
                        outDir = Directory.GetCurrentDirectory();
                    }
                    else
                    {
                        var root = Path.GetFullPath(opts.OutPath);
                        var treatAsDir = !Path.HasExtension(root);
                        if (treatAsDir)
                        {
                            Directory.CreateDirectory(root);
                            outDir = root;
                        }
                        else
                        {
                            // If a file path was provided, use its directory.
                            var dir = Path.GetDirectoryName(root) ?? Directory.GetCurrentDirectory();
                            Directory.CreateDirectory(dir);
                            outDir = dir;
                        }
                    }

                    var runArtifacts = await RunArtifactsWriter.WriteAsync(yaml, spec, arrivals, outDir, includeEvents: false, cts.Token);
                    
                    // Write debug events file if requested
                    if (opts.DebugEventsPath != null)
                    {
                        await WriteDebugEventsAsync(spec, arrivals, opts.DebugEventsPath, cts.Token);
                    }
                    
                    if (opts.Verbose)
                    {
                        Console.WriteLine("Mode: sim");
                        Console.WriteLine($"RunId={runArtifacts.RunId} Bins={spec.grid!.bins} binMinutes={spec.grid.binMinutes} totalEvents={arrivals.Total}");
                        Console.WriteLine($"RunDir -> {runArtifacts.RunDirectory}");
                        Console.WriteLine("series/index.json -> written");
                        Console.WriteLine("run.json + manifest.json -> written (dual)");
                        if (opts.DebugEventsPath != null)
                        {
                            Console.WriteLine($"Debug events -> {opts.DebugEventsPath}");
                        }
                    }
                }
                else
                {
                    Console.Error.WriteLine($"Unsupported --mode '{opts.Mode}'. Expected 'engine' or 'sim'.");
                    return 2;
                }

                return 0;
            }
            catch (OperationCanceledException)
            {
                Console.Error.WriteLine("Canceled.");
                return 130;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                return 1;
            }
        }

        static async Task WriteDebugEventsAsync(SimulationSpec spec, ArrivalGenerationResult arrivals, string debugEventsPath, CancellationToken ct)
        {
            try
            {
                await using var fs = File.Create(debugEventsPath);
                await NdjsonWriter.WriteAsync(EventFactory.BuildEvents(spec, arrivals), fs, ct);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Warning: Failed to write debug events to {debugEventsPath}: {ex.Message}");
            }
        }

        static void PrintHelp()
        {
            var defaultUrl = Environment.GetEnvironmentVariable("FLOWTIME_API_BASEURL") ?? "http://localhost:8080";
            var defaultApiVersion = Environment.GetEnvironmentVariable("FLOWTIME_API_VERSION") ?? "v1";
            Console.WriteLine($"Usage: flow-sim --model <file.yaml> [--mode engine|sim] [--flowtime {defaultUrl}] [--api-version {defaultApiVersion}] [--out outDir] [--format csv|json] [--debug-events <file>] [--verbose]");
            Console.WriteLine($"  Default FlowTime URL: {defaultUrl} (from FLOWTIME_API_BASEURL or fallback)");
            Console.WriteLine($"  Default API Version: {defaultApiVersion} (from FLOWTIME_API_VERSION or fallback)");
        }
    }

    public static class ProgramWrapper
    {
        public static Task<int> InvokeMain(string[] args) => Program.Main(args);
    }

    public static class ArgParser
    {
        public static RunOptions ParseArgs(string[] args)
        {
            var opts = RunOptions.Defaults;
            
            // Apply environment variable overrides to defaults
            var envFlowTimeUrl = Environment.GetEnvironmentVariable("FLOWTIME_API_BASEURL");
            if (!string.IsNullOrEmpty(envFlowTimeUrl))
            {
                opts = opts with { FlowTimeUrl = envFlowTimeUrl };
            }
            
            var envApiVersion = Environment.GetEnvironmentVariable("FLOWTIME_API_VERSION");
            if (!string.IsNullOrEmpty(envApiVersion))
            {
                opts = opts with { ApiVersion = envApiVersion };
            }
            
            for (int i = 0; i < args.Length; i++)
            {
                var a = args[i];
                if (a is "--model" or "-f") opts = opts with { ModelPath = ArgValue(args, ref i) };
                else if (a is "--flowtime") opts = opts with { FlowTimeUrl = ArgValue(args, ref i) };
                else if (a is "--api-version") opts = opts with { ApiVersion = ArgValue(args, ref i) };
                else if (a is "--out" or "-o") opts = opts with { OutPath = ArgValue(args, ref i) };
                else if (a is "--format") opts = opts with { Format = ArgValue(args, ref i) };
                else if (a is "--debug-events") opts = opts with { DebugEventsPath = ArgValue(args, ref i) };
                else if (a is "--verbose" or "-v") opts = opts with { Verbose = true };
                else if (a is "--mode") opts = opts with { Mode = ArgValue(args, ref i) };
                else if (a is "--help" or "-h")
                {
                    PrintHelp();
                    Environment.Exit(0);
                }
            }
            return opts;
        }

        private static string ArgValue(string[] args, ref int i)
        {
            if (i + 1 >= args.Length) throw new ArgumentException($"Missing value for {args[i]}");
            return args[++i];
        }

        private static void PrintHelp()
        {
            var defaultUrl = Environment.GetEnvironmentVariable("FLOWTIME_API_BASEURL") ?? "http://localhost:8080";
            var defaultApiVersion = Environment.GetEnvironmentVariable("FLOWTIME_API_VERSION") ?? "v1";
            Console.WriteLine($"Usage: flow-sim --model <file.yaml> [--mode engine|sim] [--flowtime {defaultUrl}] [--api-version {defaultApiVersion}] [--out outDir] [--format csv|json] [--debug-events <file>] [--verbose]");
            Console.WriteLine($"  Default FlowTime URL: {defaultUrl} (from FLOWTIME_API_BASEURL or fallback)");
            Console.WriteLine($"  Default API Version: {defaultApiVersion} (from FLOWTIME_API_VERSION or fallback)");
        }
    }
}
