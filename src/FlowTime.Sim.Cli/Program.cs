using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using FlowTime.Sim.Core; // bring core simulation types into scope

namespace FlowTime.Sim.Cli
{
    public sealed record RunOptions(
        string ModelPath,
        string FlowTimeUrl,
        string? OutPath,
        string Format,
        bool Verbose,
        string Mode // engine | sim
    )
    {
        public static RunOptions Defaults => new("", "http://localhost:8080", null, "csv", false, "engine");
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
                    var res = await FlowTimeClient.RunAsync(http, opts.FlowTimeUrl, yaml, cts.Token);
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

                    var eventsRel = spec.outputs?.events ?? "events.ndjson";
                    var goldRel = spec.outputs?.gold ?? "gold.csv";
                    var eventsPath = Path.Combine(outDir, eventsRel);
                    var goldPath = Path.Combine(outDir, goldRel);
                    Directory.CreateDirectory(Path.GetDirectoryName(eventsPath)!);
                    Directory.CreateDirectory(Path.GetDirectoryName(goldPath)!);

                    await using (var ev = File.Create(eventsPath))
                    {
                        await NdjsonWriter.WriteAsync(events, ev, cts.Token);
                    }
                    await using (var gold = File.Create(goldPath))
                    {
                        await GoldWriter.WriteAsync(spec, arrivals, gold, cts.Token);
                    }

                    // Write metadata manifest (metadata.json in same output dir by default)
                    string manifestPath = Path.Combine(outDir, "metadata.json");
                    MetadataManifest? manifest = null;
                    try
                    {
                        manifest = await MetadataWriter.WriteAsync(spec, eventsPath, goldPath, manifestPath, cts.Token);
                    }
                    catch (Exception mex)
                    {
                        Console.Error.WriteLine($"[warn] Failed to write metadata manifest: {mex.Message}");
                    }

                    if (opts.Verbose)
                    {
                        Console.WriteLine("Mode: sim");
                        Console.WriteLine($"Bins={spec.grid!.bins} binMinutes={spec.grid.binMinutes} totalEvents={arrivals.Total}");
                        Console.WriteLine($"Events -> {eventsPath}");
                        Console.WriteLine($"Gold   -> {goldPath}");
                        if (manifest is not null)
                        {
                            Console.WriteLine($"Manifest -> {manifestPath}");
                            Console.WriteLine($"Events SHA256: {manifest.events.sha256}");
                            Console.WriteLine($"Gold   SHA256: {manifest.gold.sha256}");
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

        static void PrintHelp()
        {
            Console.WriteLine("Usage: flow-sim --model <file.yaml> [--mode engine|sim] [--flowtime http://localhost:8080] [--out outDir] [--format csv|json] [--verbose]");
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
            for (int i = 0; i < args.Length; i++)
            {
                var a = args[i];
                if (a is "--model" or "-f") opts = opts with { ModelPath = ArgValue(args, ref i) };
                else if (a is "--flowtime") opts = opts with { FlowTimeUrl = ArgValue(args, ref i) };
                else if (a is "--out" or "-o") opts = opts with { OutPath = ArgValue(args, ref i) };
                else if (a is "--format") opts = opts with { Format = ArgValue(args, ref i) };
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
            Console.WriteLine("Usage: flow-sim --model <file.yaml> [--mode engine|sim] [--flowtime http://localhost:8080] [--out outDir] [--format csv|json] [--verbose]");
        }
    }
}
