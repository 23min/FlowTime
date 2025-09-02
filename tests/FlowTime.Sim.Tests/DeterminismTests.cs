using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace FlowTime.Sim.Tests;

public class DeterminismTests
{
    [Fact]
    public void ConstSpec_GeneratesIdenticalOutputs()
    {
        var specYaml = """
schemaVersion: 1
grid:
    bins: 3
    binMinutes: 60
    start: 2025-01-01T00:00:00Z
seed: 42
arrivals:
    kind: const
    values: [2,2,2]
route:
    id: x
""";
        var spec = FlowTime.Sim.Core.SimulationSpecLoader.LoadFromString(specYaml);
        FlowTime.Sim.Core.SimulationSpecValidator.Validate(spec).ThrowIfInvalid();
        var arrivals1 = FlowTime.Sim.Core.ArrivalGenerators.Generate(spec);
        var events1 = FlowTime.Sim.Core.EventFactory.BuildEvents(spec, arrivals1).ToList();
        var gold1 = ToGold(spec, arrivals1);

        var arrivals2 = FlowTime.Sim.Core.ArrivalGenerators.Generate(spec);
        var events2 = FlowTime.Sim.Core.EventFactory.BuildEvents(spec, arrivals2).ToList();
        var gold2 = ToGold(spec, arrivals2);

        Assert.Equal(HashLines(events1.Select(e => System.Text.Json.JsonSerializer.Serialize(e))), HashLines(events2.Select(e => System.Text.Json.JsonSerializer.Serialize(e))));
        Assert.Equal(HashString(gold1), HashString(gold2));
    }

    [Fact]
    public void PoissonSpec_ReproducibleGivenSeed()
    {
        var specYaml = """
schemaVersion: 1
grid:
    bins: 5
    binMinutes: 60
    start: 2025-01-01T00:00:00Z
seed: 777
arrivals:
    kind: poisson
    rate: 4.2
route:
    id: node
""";
        var spec = FlowTime.Sim.Core.SimulationSpecLoader.LoadFromString(specYaml);
        FlowTime.Sim.Core.SimulationSpecValidator.Validate(spec).ThrowIfInvalid();
        var a1 = FlowTime.Sim.Core.ArrivalGenerators.Generate(spec);
        var a2 = FlowTime.Sim.Core.ArrivalGenerators.Generate(spec);
        Assert.Equal(a1.Total, a2.Total);
        Assert.Equal(a1.BinCounts, a2.BinCounts);
    }

    private static string ToGold(FlowTime.Sim.Core.SimulationSpec spec, FlowTime.Sim.Core.ArrivalGenerationResult arrivals)
    {
        using var ms = new MemoryStream();
        FlowTime.Sim.Core.GoldWriter.WriteAsync(spec, arrivals, ms, CancellationToken.None).GetAwaiter().GetResult();
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private static string HashLines(IEnumerable<string> lines) => HashString(string.Join('\n', lines));
    private static string HashString(string s)
    {
        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(s);
        return Convert.ToHexString(sha.ComputeHash(bytes));
    }

    [Fact]
    public async Task SimMode_MetadataManifest_HashesStable()
    {
        var specYaml = """
schemaVersion: 1
grid:
    bins: 2
    binMinutes: 60
    start: 2025-01-01T00:00:00Z
seed: 999
arrivals:
    kind: const
    values: [3,4]
route:
    id: nodeA
""";
        async Task<string> WriteRunAndGetManifestHashAsync()
        {
            var specPath = Path.GetTempFileName();
            File.WriteAllText(specPath, specYaml, Encoding.UTF8);
            var outDir = Path.Combine(Path.GetTempPath(), "flow-sim-meta-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(outDir);
            var exit = await FlowTime.Sim.Cli.ProgramWrapper.InvokeMain(new[] { "--mode", "sim", "--model", specPath, "--out", outDir });
            Assert.Equal(0, exit);
            var manifestPath = Path.Combine(outDir, "manifest.json");
            Assert.True(File.Exists(manifestPath));
            var json = File.ReadAllText(manifestPath, Encoding.UTF8);
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var evHash = doc.RootElement.GetProperty("events").GetProperty("sha256").GetString()!;
            var goldHash = doc.RootElement.GetProperty("gold").GetProperty("sha256").GetString()!;
            return evHash + "|" + goldHash;
        }

        var h1 = await WriteRunAndGetManifestHashAsync();
        var h2 = await WriteRunAndGetManifestHashAsync();
        Assert.Equal(h1, h2);
    }
}
