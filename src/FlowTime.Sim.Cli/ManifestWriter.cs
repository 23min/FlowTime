using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FlowTime.Sim.Core;

#pragma warning disable CS0618 // Type or member is obsolete - CLI is the intended consumer of legacy code

namespace FlowTime.Sim.Cli;

// Reworked for SIM-M2 (artifact parity): produce run.json, manifest.json (identical), series/index.json and per-series CSVs.
public static class RunArtifactsWriter
{
    public static async Task<RunArtifacts> WriteAsync(string originalYaml, SimulationSpec spec, ArrivalGenerationResult arrivals, string rootOutDir, bool includeEvents, CancellationToken ct)
    {
        var runId = GenerateRunId();
        
        // Create the runs directory structure
        // If rootOutDir ends with "runs", use it directly; otherwise append "runs"
        var runsDir = rootOutDir.EndsWith("runs") 
            ? rootOutDir 
            : Path.Combine(rootOutDir, "runs");
            
        var runDir = Path.Combine(runsDir, runId);
        Directory.CreateDirectory(runDir);
        var seriesDir = Path.Combine(runDir, "series");
        Directory.CreateDirectory(seriesDir);

    // Persist original spec for overlay derivations (SIM-SVC-M2 overlay bootstrap).
    var specPath = Path.Combine(runDir, "spec.yaml");
    await File.WriteAllTextAsync(specPath, originalYaml, Encoding.UTF8, ct);

        var scenarioHash = ModelHasher.ComputeModelHash(originalYaml); // model==scenario for now
        int bins = spec.grid!.bins!.Value;
        int binMinutes = spec.grid!.binMinutes!.Value;
        var engineVersion = "sim-0.1.0"; // TODO derive from assembly
        var seed = spec.seed ?? 12345;
        var rngKind = "pcg32"; // enforced
        var componentId = spec.route?.id ?? "COMP";
        var @class = "DEFAULT";

        // Build series arrays
        var arrivalsSeries = arrivals.BinCounts.Select((v, i) => (Index: i, Value: (double)v)).ToArray();
        var servedSeries = arrivalsSeries; // identity for M2
        var errorsSeries = Enumerable.Range(0, arrivalsSeries.Length).Select(i => (Index: i, Value: 0.0)).ToArray();

        // Write per-series CSVs (t,value)
        var seriesEntries = new List<SeriesEntryRaw>();
        await WriteSeriesCsv("arrivals", arrivalsSeries, seriesDir, componentId, @class, bins, seriesEntries, ct);
        await WriteSeriesCsv("served", servedSeries, seriesDir, componentId, @class, bins, seriesEntries, ct);
        await WriteSeriesCsv("errors", errorsSeries, seriesDir, componentId, @class, bins, seriesEntries, ct);

        // Optional events
        string? eventsPath = null;
        int eventCount = 0;
        if (includeEvents)
        {
            eventsPath = Path.Combine(runDir, "events.ndjson");
            await using (var ev = File.Create(eventsPath))
            {
                await NdjsonWriter.WriteAsync(EventFactory.BuildEvents(spec, arrivals), ev, ct);
            }
            eventCount = await CountLinesAsync(eventsPath, ct);
        }

        // series/index.json
        var index = new SeriesIndex(
            1,
            new GridInfo(bins, binMinutes, "UTC"),
            seriesEntries.Select(se => new SeriesEntry(
                se.Id, se.Kind, se.Path, se.Unit, se.ComponentId, se.Class, se.Points, se.Hash
            )).ToList(),
            null
        );
        await WriteJson(Path.Combine(runDir, "series", "index.json"), index, ct);

        // manifest + run (identical except manifest adds seriesHashes + eventCount) — we include seriesHashes in both for simplicity
        var seriesHashes = seriesEntries.ToDictionary(e => e.Id, e => e.Hash);
        var timestamp = DateTime.UtcNow;
        var gridObj = new { bins, binMinutes, timezone = "UTC", align = "left" };
    var baseRun = new RunDocument(1, runId, engineVersion, gridObj, scenarioHash, new RngInfo(rngKind, seed), timestamp, "sim", Array.Empty<string>(), Array.Empty<string>());
    var manifest = new ManifestDocument(baseRun, seriesHashes, eventCount);

        await WriteJson(Path.Combine(runDir, "run.json"), manifest, ct);
        await WriteJson(Path.Combine(runDir, "manifest.json"), manifest, ct);

        return new RunArtifacts(runId, runDir, manifest, index);
    }

    private static string GenerateRunId()
    {
        var now = DateTime.UtcNow;
    // Standard format (contracts.md): <source>_<yyyy-MM-ddTHH-mm-ssZ>_<8-char slug>
    var ts = now.ToString("yyyy-MM-ddTHH-mm-ssZ");
    var slug = Guid.NewGuid().ToString("N")[..8]; // lowercase hex/alnum
    return $"sim_{ts}_{slug}";
    }

    private static async Task WriteSeriesCsv(string measure, IEnumerable<(int Index, double Value)> data, string seriesDir, string componentId, string @class, int bins, List<SeriesEntryRaw> sink, CancellationToken ct)
    {
        var id = $"{measure}@{componentId}" + (@class == "DEFAULT" ? string.Empty : $"@{@class}");
        var path = Path.Combine(seriesDir, id + ".csv");
        await using (var fs = File.Create(path))
        using (var sw = new StreamWriter(fs, new UTF8Encoding(false)))
        {
            await sw.WriteLineAsync("t,value");
            foreach (var (Index, Value) in data)
            {
                await sw.WriteLineAsync(FormattableString.Invariant($"{Index},{Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}"));
            }
        }
        var hash = await ComputeFileHashAsync(path, ct);
        sink.Add(new SeriesEntryRaw(id, "flow", $"series/{id}.csv", "entities/bin", componentId, @class, bins, hash));
    }

    private static async Task<string> ComputeFileHashAsync(string path, CancellationToken ct)
    {
        await using var fs = File.OpenRead(path);
        var sha = await SHA256.HashDataAsync(fs, ct);
        return "sha256:" + Convert.ToHexString(sha).ToLowerInvariant();
    }

    private static async Task<int> CountLinesAsync(string path, CancellationToken ct)
    {
        if (!File.Exists(path)) return 0;
        int count = 0;
        await using var fs = File.OpenRead(path);
        using var sr = new StreamReader(fs, Encoding.UTF8, true, 4096, false);
        while (!sr.EndOfStream) { await sr.ReadLineAsync(ct); count++; }
        return count;
    }

    private static Task WriteJson(string path, object payload, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        return File.WriteAllTextAsync(path, json + "\n", Encoding.UTF8, ct);
    }
}

public sealed record RngInfo(string Kind, int Seed);
public sealed record RunDocument(int SchemaVersion, string RunId, string EngineVersion, object Grid, string ScenarioHash, RngInfo Rng, DateTime CreatedUtc, string Source, string[] Warnings, string[] Notes);
public sealed record ManifestDocument(int SchemaVersion, string RunId, string EngineVersion, object Grid, string ScenarioHash, RngInfo Rng, DateTime CreatedUtc, string Source, string[] Warnings, string[] Notes, Dictionary<string,string> SeriesHashes, int EventCount)
{
    public ManifestDocument(RunDocument run, Dictionary<string,string> seriesHashes, int eventCount) : this(run.SchemaVersion, run.RunId, run.EngineVersion, run.Grid, run.ScenarioHash, run.Rng, run.CreatedUtc, run.Source, run.Warnings, run.Notes, seriesHashes, eventCount) { }
}

// series/index.json DTOs
public sealed record GridInfo(int Bins, int BinMinutes, string Timezone);
public sealed record SeriesEntry(string Id, string Kind, string Path, string Unit, string ComponentId, string Class, int Points, string Hash);
internal sealed record SeriesEntryRaw(string Id, string Kind, string Path, string Unit, string ComponentId, string Class, int Points, string Hash);
public sealed record SeriesIndex(int SchemaVersion, GridInfo Grid, List<SeriesEntry> Series, object? Formats);

public sealed record RunArtifacts(string RunId, string RunDirectory, ManifestDocument Manifest, SeriesIndex Index);