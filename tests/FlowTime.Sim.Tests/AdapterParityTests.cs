using System.Text;
using Xunit;

namespace FlowTime.Sim.Tests;

// Phase 5: Adapter parity harness (initial scaffold)
// For now, we only verify that running the simulator twice yields identical gold arrival series
// and leave integration with the downstream adapter/engine placeholder until engine hook available.
public class AdapterParityTests
{
    private static async Task<(List<(DateTimeOffset ts,int arrivals,int served,int errors)> rows, string goldHash)> RunSimAsync(string specYaml)
    {
        var specPath = Path.GetTempFileName();
        await File.WriteAllTextAsync(specPath, specYaml, Encoding.UTF8);
        var outDir = Path.Combine(Path.GetTempPath(), "flow-sim-parity-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outDir);
        var exit = await FlowTime.Sim.Cli.ProgramWrapper.InvokeMain(new[] { "--mode", "sim", "--model", specPath, "--out", outDir });
        Assert.Equal(0, exit);
        var goldPath = Path.Combine(outDir, "gold.csv");
        Assert.True(File.Exists(goldPath));
        var lines = await File.ReadAllLinesAsync(goldPath, Encoding.UTF8);
        Assert.True(lines.Length > 1);
        var rows = new List<(DateTimeOffset,int,int,int)>();
        for (int i = 1; i < lines.Length; i++)
        {
            var parts = lines[i].Split(',');
            rows.Add((DateTimeOffset.Parse(parts[0]), int.Parse(parts[3]), int.Parse(parts[4]), int.Parse(parts[5])));
        }
        // Simple hash (stable) of file content
        using var sha = System.Security.Cryptography.SHA256.Create();
        var hash = Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(string.Join('\n', lines))));
        return (rows, hash);
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
        var (r1, h1) = await RunSimAsync(spec);
        var (r2, h2) = await RunSimAsync(spec);
        Assert.Equal(h1, h2);
        Assert.Equal(r1.Select(r => r.arrivals), r2.Select(r => r.arrivals));
        Assert.Equal(r1.Select(r => r.served), r2.Select(r => r.served));
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
        var (r1, _) = await RunSimAsync(spec);
        var (r2, _) = await RunSimAsync(spec);
        Assert.Equal(r1.Select(r => r.arrivals), r2.Select(r => r.arrivals));
        Assert.Equal(r1.Select(r => r.served), r2.Select(r => r.served));
    }
}
