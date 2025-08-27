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
    }

    [Fact]
    public async Task SimMode_InvalidSpec_ReturnsUsageExitCode()
    {
        var tmp = Path.GetTempFileName();
        await File.WriteAllTextAsync(tmp, "grid: { bins: 2, binMinutes: 60 }\narrivals: { kind: const, values: [1] }\nroute: { id: n }\n", Encoding.UTF8);
        var outDir = Path.Combine(Path.GetTempPath(), "flow-sim-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outDir);

        var exit = await FlowTime.Sim.Cli.ProgramWrapper.InvokeMain(new[] { "--mode", "sim", "--model", tmp, "--out", outDir });
        Assert.Equal(2, exit); // validation failure
    }
}
