using FlowTime.Sim.Cli;
using FlowTime.Sim.Core;
using Xunit;

namespace FlowTime.Sim.Tests;

public class ManifestWriterTests
{
    // Canonical small spec (schemaVersion 1) used for manifest generation tests.
    private const string specYaml = """
schemaVersion: 1
grid:
    bins: 3
    binMinutes: 60
    start: 2025-01-01T00:00:00Z
seed: 123
arrivals:
    kind: const
    values: [1,2,3]
route:
    id: main
""";

    [Fact]
    public async Task Manifest_Is_Written_With_ExpectedFields()
    {
        var temp = Directory.CreateTempSubdirectory("sim_manifest_test");
        try
        {
            var spec = SimulationSpecLoader.LoadFromString(specYaml);
            var arrivals = ArrivalGenerators.Generate(spec);
            var eventsPath = Path.Combine(temp.FullName, "events.ndjson");
            var goldPath = Path.Combine(temp.FullName, "gold.csv");
            await using (var ev = File.Create(eventsPath))
            {
                await NdjsonWriter.WriteAsync(EventFactory.BuildEvents(spec, arrivals), ev, CancellationToken.None);
            }
            await using (var gold = File.Create(goldPath))
            {
                await GoldWriter.WriteAsync(spec, arrivals, gold, CancellationToken.None);
            }

            var manifestPath = Path.Combine(temp.FullName, "manifest.json");
            var manifest = await ManifestWriter.WriteAsync(specYaml, spec, eventsPath, goldPath, manifestPath, CancellationToken.None);

            Assert.True(File.Exists(manifestPath));
            Assert.Equal(1, manifest.schemaVersion);
            Assert.StartsWith("sha256:", manifest.modelHash);
            Assert.Equal(manifest.modelHash, manifest.scenarioHash);
            Assert.True(manifest.seriesHashes.ContainsKey("gold"));
            Assert.StartsWith("sha256:", manifest.seriesHashes["gold"]);
            Assert.True(manifest.eventCount >= 0);
        }
        finally
        {
            try { temp.Delete(recursive: true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public async Task Manifest_RepeatedRun_DeterministicHashes()
    {
    var spec = SimulationSpecLoader.LoadFromString(specYaml);
        var temp1 = Directory.CreateTempSubdirectory("sim_manifest_run1");
        var temp2 = Directory.CreateTempSubdirectory("sim_manifest_run2");
        try
        {
            var m1 = await ProduceManifest(spec, temp1.FullName);
            var m2 = await ProduceManifest(spec, temp2.FullName);
            Assert.Equal(m1.modelHash, m2.modelHash);
            Assert.Equal(m1.seriesHashes["gold"], m2.seriesHashes["gold"]);
        }
        finally
        {
            try { temp1.Delete(true); } catch { }
            try { temp2.Delete(true); } catch { }
        }
    }

    private static async Task<SimManifest> ProduceManifest(SimulationSpec spec, string dir)
    {
        var arrivals = ArrivalGenerators.Generate(spec);
        var eventsPath = Path.Combine(dir, "events.ndjson");
        var goldPath = Path.Combine(dir, "gold.csv");
        Directory.CreateDirectory(dir);
        await using (var ev = File.Create(eventsPath))
        {
            await NdjsonWriter.WriteAsync(EventFactory.BuildEvents(spec, arrivals), ev, CancellationToken.None);
        }
        await using (var gold = File.Create(goldPath))
        {
            await GoldWriter.WriteAsync(spec, arrivals, gold, CancellationToken.None);
        }
    return await ManifestWriter.WriteAsync(specYaml, spec, eventsPath, goldPath, Path.Combine(dir, "manifest.json"), CancellationToken.None);
    }
}