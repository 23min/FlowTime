using System.Text;
using Xunit;

namespace FlowTime.Sim.Tests;

public class CliSimModeTests
{
    [Fact]
    public async Task SimMode_ProducesEventsAndGold()
    {
        // Create a temporary spec to avoid relying on repo-relative paths during test runs.
        var specYaml = """
schemaVersion: 1
grid:
    bins: 4
    binMinutes: 60
    start: 2025-01-01T00:00:00Z
seed: 12345
arrivals:
    kind: const
    values: [5,5,5,5]
route:
    id: nodeA
outputs:
    events: out/events.ndjson
    gold: out/gold.csv
""";
        var specPath = Path.GetTempFileName();
        await File.WriteAllTextAsync(specPath, specYaml, Encoding.UTF8);
        var outDir = Path.Combine(Path.GetTempPath(), "flow-sim-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outDir);

        var exit = await FlowTime.Sim.Cli.ProgramWrapper.InvokeMain(new[] { "--mode", "sim", "--model", specPath, "--out", outDir });
        Assert.Equal(0, exit);

        var eventsPath = Path.Combine(outDir, "out/events.ndjson");
        var goldPath = Path.Combine(outDir, "out/gold.csv");
        Assert.True(File.Exists(eventsPath));
        Assert.True(File.Exists(goldPath));

        var lines = await File.ReadAllLinesAsync(eventsPath, Encoding.UTF8);
        Assert.Equal(4 * 5, lines.Length); // 4 bins * 5 arrivals
        var goldLines = await File.ReadAllLinesAsync(goldPath, Encoding.UTF8);
        Assert.Equal(1 + 4, goldLines.Length); // header + 4 rows

        // Metadata manifest
    var manifestPath = Path.Combine(outDir, "manifest.json");
        Assert.True(File.Exists(manifestPath));
        var json = await File.ReadAllTextAsync(manifestPath, Encoding.UTF8);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(12345, root.GetProperty("seed").GetInt32());
        Assert.Equal("pcg", root.GetProperty("rng").GetString());
        var ev = root.GetProperty("events");
        var gd = root.GetProperty("gold");
        Assert.EndsWith("events.ndjson", ev.GetProperty("path").GetString());
        Assert.EndsWith("gold.csv", gd.GetProperty("path").GetString());
        bool Hex64(string s)
        {
            if (s.StartsWith("sha256:")) s = s.Substring(7);
            return System.Text.RegularExpressions.Regex.IsMatch(s, "^[0-9a-f]{64}$");
        }
        Assert.True(Hex64(ev.GetProperty("sha256").GetString()!.ToLowerInvariant()));
        Assert.True(Hex64(gd.GetProperty("sha256").GetString()!.ToLowerInvariant()));
    }

    [Fact]
    public async Task SimMode_InvalidSpec_ReturnsUsageExitCode()
    {
        var tmp = Path.GetTempFileName();
        await File.WriteAllTextAsync(tmp, "grid: { bins: 2, binMinutes: 60 }\narrivals: { kind: const, values: [1] }\nroute: { id: n }\n", Encoding.UTF8);
    // Intentionally invalid (arrivals length mismatch). No schemaVersion added to test legacy warning path.
        var outDir = Path.Combine(Path.GetTempPath(), "flow-sim-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outDir);

        var exit = await FlowTime.Sim.Cli.ProgramWrapper.InvokeMain(new[] { "--mode", "sim", "--model", tmp, "--out", outDir });
        Assert.Equal(2, exit); // validation failure
    }
}
