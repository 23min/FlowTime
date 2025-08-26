using System.Globalization;
using System.Net.Http.Headers;
using System.Text;

namespace FlowTime.Sim.Cli;

public sealed record RunOptions(
    string ModelPath,
    string FlowTimeUrl,
    string? OutPath,
    string Format,
    bool Verbose
)
{
    public static RunOptions Defaults => new("", "http://localhost:8080", null, "csv", false);
}

internal static class Program
{
    static async Task<int> Main(string[] args)
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

            var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (s, e) => { e.Cancel = true; cts.Cancel(); };

            var res = await FlowTimeClient.RunAsync(http, opts.FlowTimeUrl, yaml, cts.Token);

            if (opts.Verbose)
            {
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
        Console.WriteLine("Usage: flow-sim --model <file.yaml> [--flowtime http://localhost:8080] [--out out.csv] [--format csv|json] [--verbose]");
    }
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
        Console.WriteLine("Usage: flow-sim --model <file.yaml> [--flowtime http://localhost:8080] [--out out.csv] [--format csv|json] [--verbose]");
    }
}
