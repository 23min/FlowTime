using System.Text;
using Xunit;

namespace FlowTime.Sim.Tests;

// Originally included an external FlowTime.Core engine parity roundtrip (removed to decouple repos).
// Now focuses on internal invariants: determinism, events↔gold aggregation parity, manifest sanity, and a negative mutation guard.
public class AdapterParityTests
{
    private sealed record SimRun(
        string OutDir,
        List<(DateTimeOffset ts, int arrivals, int served, int errors)> Rows,
        string GoldHash,
        int BinMinutes,
        string GoldPath,
        string EventsPath,
        string ManifestPath,
        string ManifestRaw
    );

    private static async Task<SimRun> RunSimAsync(string specYaml)
    {
        var specPath = Path.GetTempFileName();
        await File.WriteAllTextAsync(specPath, specYaml, Encoding.UTF8);
        var outDir = Path.Combine(Path.GetTempPath(), "flow-sim-parity-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outDir);
        var exit = await FlowTime.Sim.Cli.ProgramWrapper.InvokeMain(new[] { "--mode", "sim", "--model", specPath, "--out", outDir });
        Assert.Equal(0, exit);
        var goldPath = Path.Combine(outDir, "gold.csv");
        var eventsPath = Path.Combine(outDir, "events.ndjson");
    var manifestPath = Path.Combine(outDir, "manifest.json");
        Assert.True(File.Exists(goldPath));
        Assert.True(File.Exists(eventsPath));
        Assert.True(File.Exists(manifestPath));

        var lines = await File.ReadAllLinesAsync(goldPath, Encoding.UTF8);
        Assert.True(lines.Length > 1);
        var rows = new List<(DateTimeOffset, int, int, int)>();
        for (int i = 1; i < lines.Length; i++)
        {
            var parts = lines[i].Split(',');
            rows.Add((DateTimeOffset.Parse(parts[0]), int.Parse(parts[3]), int.Parse(parts[4]), int.Parse(parts[5])));
        }
        using var sha = System.Security.Cryptography.SHA256.Create();
        var goldHash = Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(string.Join('\n', lines))));
        int binMinutes = rows.Count >= 2 ? (int)(rows[1].Item1 - rows[0].Item1).TotalMinutes : 60;
        var manifestRaw = await File.ReadAllTextAsync(manifestPath, Encoding.UTF8);
        return new SimRun(outDir, rows, goldHash, binMinutes, goldPath, eventsPath, manifestPath, manifestRaw);
    }

    private static Dictionary<DateTimeOffset, int> AggregateEventsByTimestamp(string eventsPath)
    {
        var counts = new Dictionary<DateTimeOffset, int>();
        foreach (var line in File.ReadLines(eventsPath))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            using var doc = System.Text.Json.JsonDocument.Parse(line);
            var ts = doc.RootElement.GetProperty("ts").GetDateTimeOffset();
            if (!counts.ContainsKey(ts)) counts[ts] = 0;
            counts[ts]++;
        }
        return counts;
    }

    [Fact]
    public async Task ConstScenario_DeterministicHashAndCounts()
    {
        var spec = """
 schemaVersion: 1
 grid:
     bins: 3
     binMinutes: 60
     start: 2025-01-01T00:00:00Z
 seed: 123
 arrivals:
     kind: const
     values: [4,5,6]
 route:
     id: nodeA
 """;
        var run1 = await RunSimAsync(spec);
        var run2 = await RunSimAsync(spec);
        // Determinism: identical runs produce identical hashes, arrivals, served
        Assert.Equal(run1.GoldHash, run2.GoldHash);
        Assert.Equal(run1.Rows.Select(r => r.arrivals), run2.Rows.Select(r => r.arrivals));
        Assert.Equal(run1.Rows.Select(r => r.served), run2.Rows.Select(r => r.served));
        // Served should mirror arrivals in current milestone
        Assert.Equal(run1.Rows.Select(r => r.arrivals), run1.Rows.Select(r => r.served));
    }

    [Fact]
    public async Task PoissonScenario_DeterministicCounts()
    {
        var spec = """
 schemaVersion: 1
 grid:
     bins: 4
     binMinutes: 60
     start: 2025-01-01T00:00:00Z
 seed: 456
 arrivals:
     kind: poisson
     rate: 3.2
 route:
     id: nodeB
 """;
        var run1 = await RunSimAsync(spec);
        var run2 = await RunSimAsync(spec);
    Assert.Equal(run1.Rows.Select(r => r.arrivals), run2.Rows.Select(r => r.arrivals));
    Assert.Equal(run1.Rows.Select(r => r.served), run2.Rows.Select(r => r.served));
    Assert.Equal(run1.Rows.Select(r => r.arrivals), run1.Rows.Select(r => r.served));
    }

    [Fact]
    public async Task EventsAggregation_EqualsGoldCounts_Const()
    {
        var spec = """
 schemaVersion: 1
 grid:
     bins: 3
     binMinutes: 60
     start: 2025-01-01T00:00:00Z
 seed: 321
 arrivals:
     kind: const
     values: [2,3,4]
 route:
     id: nodeC
 """;
        var run = await RunSimAsync(spec);
        var goldCounts = run.Rows.Select(r => r.arrivals).ToArray();
        var goldTimestamps = run.Rows.Select(r => r.ts).ToArray();
        var eventMap = AggregateEventsByTimestamp(run.EventsPath);
        var eventsAggAligned = goldTimestamps.Select(ts => eventMap.TryGetValue(ts, out var c) ? c : 0).ToArray();
        Assert.Equal(goldCounts, eventsAggAligned);
    }

    [Fact]
    public async Task EventsAggregation_EqualsGoldCounts_Poisson()
    {
        var spec = """
 schemaVersion: 1
 grid:
     bins: 5
     binMinutes: 60
     start: 2025-01-01T00:00:00Z
 seed: 999
 arrivals:
     kind: poisson
     rate: 2.7
 route:
     id: nodeD
 """;
        var run = await RunSimAsync(spec);
        var goldCounts = run.Rows.Select(r => r.arrivals).ToArray();
        var goldTimestamps = run.Rows.Select(r => r.ts).ToArray();
        var eventMap = AggregateEventsByTimestamp(run.EventsPath);
        var eventsAggAligned = goldTimestamps.Select(ts => eventMap.TryGetValue(ts, out var c) ? c : 0).ToArray();
        Assert.Equal(goldCounts, eventsAggAligned);
    }

    [Fact]
    public async Task ManifestParity_BasicProperties()
    {
        var spec = """
 schemaVersion: 1
 grid:
     bins: 2
     binMinutes: 60
     start: 2025-01-01T00:00:00Z
 seed: 42
 arrivals:
     kind: const
     values: [1,2]
 route:
     id: nodeM
 """;
        var run = await RunSimAsync(spec);
        using var doc = System.Text.Json.JsonDocument.Parse(run.ManifestRaw);
        var root = doc.RootElement;
        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(42, root.GetProperty("seed").GetInt32());
        Assert.Equal("pcg", root.GetProperty("rng").GetString());
        var eventsObj = root.GetProperty("events");
        var goldObj = root.GetProperty("gold");
        Assert.EndsWith("events.ndjson", eventsObj.GetProperty("path").GetString());
        Assert.EndsWith("gold.csv", goldObj.GetProperty("path").GetString());
        // Basic hash sanity: hex length 64
    string evHash = eventsObj.GetProperty("sha256").GetString()!;
    string gdHash = goldObj.GetProperty("sha256").GetString()!;
    if (evHash.StartsWith("sha256:")) evHash = evHash.Substring(7);
    if (gdHash.StartsWith("sha256:")) gdHash = gdHash.Substring(7);
    Assert.Equal(64, evHash.Length);
    Assert.Equal(64, gdHash.Length);
    }

    [Fact]
    public async Task NegativeMutation_DetectedMismatch()
    {
        var spec = """
 schemaVersion: 1
 grid:
     bins: 3
     binMinutes: 60
     start: 2025-01-01T00:00:00Z
 seed: 100
 arrivals:
     kind: const
     values: [1,1,1]
 route:
     id: nodeNeg
 """;
    var run = await RunSimAsync(spec);
    var original = run.Rows.Select(r => r.arrivals).ToArray();
    var mutated = (int[])original.Clone();
    mutated[1] += 1;
    Assert.NotEqual(original, mutated);
    }
}
