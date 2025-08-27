using System.Text;
using Xunit;

namespace FlowTime.Sim.Tests;

// Phase 5: Adapter parity harness (initial engine integration)
// Strategy: run simulator -> parse gold.csv -> build engine ConstSeriesNode graph -> evaluate -> compare.
public class AdapterParityTests
{
    private static async Task<(List<(DateTimeOffset ts, int arrivals, int served, int errors)> rows, string goldHash, int binMinutes)> RunSimAsync(string specYaml)
    {
        var specPath = Path.GetTempFileName();
        await File.WriteAllTextAsync(specPath, specYaml, Encoding.UTF8);
        var outDir = Path.Combine(Path.GetTempPath(), "flow-sim-parity-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outDir);
        var exit = await FlowTime.Sim.Cli.ProgramWrapper.InvokeMain(new[] { "--mode", "sim", "--model", specPath, "--out", outDir });
        Assert.Equal(0, exit);
        var goldPath = Path.Combine(outDir, "gold.csv");
        var eventsPath = Path.Combine(outDir, "events.ndjson");
        Assert.True(File.Exists(goldPath));
        Assert.True(File.Exists(eventsPath));
        var lines = await File.ReadAllLinesAsync(goldPath, Encoding.UTF8);
        Assert.True(lines.Length > 1);
        var rows = new List<(DateTimeOffset, int, int, int)>();
        int? binMinutes = null;
        for (int i = 1; i < lines.Length; i++)
        {
            var parts = lines[i].Split(',');
            rows.Add((DateTimeOffset.Parse(parts[0]), int.Parse(parts[3]), int.Parse(parts[4]), int.Parse(parts[5])));
        }
        // Simple hash (stable) of file content
        using var sha = System.Security.Cryptography.SHA256.Create();
        var hash = Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(string.Join('\n', lines))));
        // Derive binMinutes from timestamps difference if possible (>=2 rows)
        if (rows.Count >= 2)
        {
            var delta = rows[1].Item1 - rows[0].Item1;
            binMinutes = (int)delta.TotalMinutes;
        }
        return (rows, hash, binMinutes ?? 60);
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
    public async Task ConstScenario_ParityBaseline()
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
        var (r1, h1, binMinutes) = await RunSimAsync(spec);
        var (r2, h2, _) = await RunSimAsync(spec);
        Assert.Equal(h1, h2);
        Assert.Equal(r1.Select(r => r.arrivals), r2.Select(r => r.arrivals));
        Assert.Equal(r1.Select(r => r.served), r2.Select(r => r.served));

        // Build engine graph from arrivals -> demand node
        var grid = new FlowTime.Core.TimeGrid(r1.Count, binMinutes);
        var demandNode = new FlowTime.Core.ConstSeriesNode("demand", r1.Select(r => (double)r.arrivals).ToArray());
        var graph = new FlowTime.Core.Graph(new[] { demandNode });
        var evaluated = graph.Evaluate(grid);
        var demandSeries = evaluated[demandNode.Id].ToArray();
        Assert.Equal(r1.Select(r => (double)r.arrivals), demandSeries);
    }

    [Fact]
    public async Task PoissonScenario_ParityBaseline()
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
        var (r1, _, binMinutes) = await RunSimAsync(spec);
        var (r2, _, _) = await RunSimAsync(spec);
        Assert.Equal(r1.Select(r => r.arrivals), r2.Select(r => r.arrivals));
        Assert.Equal(r1.Select(r => r.served), r2.Select(r => r.served));

        // Engine parity
        var grid = new FlowTime.Core.TimeGrid(r1.Count, binMinutes);
        var demandNode = new FlowTime.Core.ConstSeriesNode("demand", r1.Select(r => (double)r.arrivals).ToArray());
        var graph = new FlowTime.Core.Graph(new[] { demandNode });
        var series = graph.Evaluate(grid)[demandNode.Id].ToArray();
        Assert.Equal(r1.Select(r => (double)r.arrivals), series);
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
        var specPath = Path.GetTempFileName();
        await File.WriteAllTextAsync(specPath, spec, Encoding.UTF8);
        var outDir = Path.Combine(Path.GetTempPath(), "flow-sim-parity-evt-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outDir);
        var exit = await FlowTime.Sim.Cli.ProgramWrapper.InvokeMain(new[] { "--mode", "sim", "--model", specPath, "--out", outDir });
        Assert.Equal(0, exit);
        var goldPath = Path.Combine(outDir, "gold.csv");
        var eventsPath = Path.Combine(outDir, "events.ndjson");
        Assert.True(File.Exists(goldPath));
        Assert.True(File.Exists(eventsPath));
        var goldLines = File.ReadAllLines(goldPath); // header + rows
        var goldRows = goldLines.Skip(1).Select(l => l.Split(',')).ToArray();
        var goldCounts = goldRows.Select(p => int.Parse(p[3])).ToArray();
        var goldTimestamps = goldRows.Select(p => DateTimeOffset.Parse(p[0])).ToArray();
        var eventMap = AggregateEventsByTimestamp(eventsPath);
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
        var specPath = Path.GetTempFileName();
        await File.WriteAllTextAsync(specPath, spec, Encoding.UTF8);
        var outDir = Path.Combine(Path.GetTempPath(), "flow-sim-parity-evt-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outDir);
        var exit = await FlowTime.Sim.Cli.ProgramWrapper.InvokeMain(new[] { "--mode", "sim", "--model", specPath, "--out", outDir });
        Assert.Equal(0, exit);
        var goldPath = Path.Combine(outDir, "gold.csv");
        var eventsPath = Path.Combine(outDir, "events.ndjson");
        Assert.True(File.Exists(goldPath));
        Assert.True(File.Exists(eventsPath));
        var goldLines = File.ReadAllLines(goldPath);
        var goldRows = goldLines.Skip(1).Select(l => l.Split(',')).ToArray();
        var goldCounts = goldRows.Select(p => int.Parse(p[3])).ToArray();
        var goldTimestamps = goldRows.Select(p => DateTimeOffset.Parse(p[0])).ToArray();
        var eventMap = AggregateEventsByTimestamp(eventsPath);
        var eventsAggAligned = goldTimestamps.Select(ts => eventMap.TryGetValue(ts, out var c) ? c : 0).ToArray();
        Assert.Equal(goldCounts, eventsAggAligned);
    }
}
