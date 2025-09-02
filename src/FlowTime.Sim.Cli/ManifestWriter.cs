using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FlowTime.Sim.Core;

namespace FlowTime.Sim.Cli;

// Slice 2 (SIM-M2): manifest writer (dual-write groundwork). For now only manifest.json; run/index in later slices.
public static class ManifestWriter
{
    public static async Task<SimManifest> WriteAsync(
        string originalYaml,
        SimulationSpec spec,
        string eventsPath,
        string goldPath,
        string manifestPath,
        CancellationToken ct)
    {
        var modelHash = ModelHasher.ComputeModelHash(originalYaml);
        var scenarioHash = modelHash; // identical until overlays differ.
        var seed = spec.seed ?? 12345;
        var rng = string.IsNullOrWhiteSpace(spec.rng) ? "pcg" : spec.rng!.Trim().ToLowerInvariant();

        var eventsHash = await ComputeFileHashAsync(eventsPath, ct).ConfigureAwait(false);
        var goldHash = await ComputeFileHashAsync(goldPath, ct).ConfigureAwait(false);

        var dictSeries = new Dictionary<string, string>
        {
            { "gold", goldHash }
        };

    var manifest = new SimManifest(
            schemaVersion: 1,
            modelHash: modelHash,
            scenarioHash: scenarioHash,
            seed: seed,
            rng: rng,
            seriesHashes: dictSeries,
            eventCount: await CountLinesAsync(eventsPath, ct).ConfigureAwait(false),
            generatedAtUtc: DateTime.UtcNow,
            events: new FileHashMeta(System.IO.Path.GetFileName(eventsPath), eventsHash, new FileInfo(eventsPath).Length),
            gold: new FileHashMeta(System.IO.Path.GetFileName(goldPath), goldHash, new FileInfo(goldPath).Length)
        );

        var json = JsonSerializer.Serialize(manifest, JsonOptions);
        Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)!);
        await File.WriteAllTextAsync(manifestPath, json + "\n", Encoding.UTF8, ct).ConfigureAwait(false);
        return manifest;
    }

    private static async Task<string> ComputeFileHashAsync(string path, CancellationToken ct)
    {
        await using var fs = File.OpenRead(path);
        var sha = await SHA256.HashDataAsync(fs, ct).ConfigureAwait(false);
        return "sha256:" + Convert.ToHexString(sha).ToLowerInvariant();
    }

    private static async Task<int> CountLinesAsync(string path, CancellationToken ct)
    {
        int count = 0;
        await using var fs = File.OpenRead(path);
        using var sr = new StreamReader(fs, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 4096, leaveOpen: false);
        while (!sr.EndOfStream)
        {
            await sr.ReadLineAsync(ct).ConfigureAwait(false);
            count++;
        }
        return count;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };
}

public sealed record FileHashMeta(string path, string sha256, long sizeBytes);

public sealed record SimManifest(
    int schemaVersion,
    string modelHash,
    string scenarioHash,
    int seed,
    string rng,
    Dictionary<string,string> seriesHashes,
    int eventCount,
    DateTime generatedAtUtc,
    FileHashMeta events,
    FileHashMeta gold
);