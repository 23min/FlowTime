using FlowTime.Sim.Cli;
using FlowTime.Sim.Core;
using Xunit;

namespace FlowTime.Sim.Tests;

public class ManifestWriterTests
{
    private const string specYaml = """
schemaVersion: 1
grid:
  bins: 5
  binMinutes: 60
  start: 2025-01-01T00:00:00Z
seed: 123
arrivals:
  kind: const
  values: [1,2,3,4,5]
route:
  id: COMP_A
""";

    [Fact]
    public async Task RunArtifacts_Deterministic_SeriesHashes()
    {
        var spec = SimulationSpecLoader.LoadFromString(specYaml);
        var arr1 = ArrivalGenerators.Generate(spec);
        var arr2 = ArrivalGenerators.Generate(spec);
        var t1 = Directory.CreateTempSubdirectory();
        var t2 = Directory.CreateTempSubdirectory();
        try
        {
            var run1 = await FlowTime.Sim.Cli.RunArtifactsWriter.WriteAsync(specYaml, spec, arr1, t1.FullName, includeEvents:false, CancellationToken.None);
            var run2 = await FlowTime.Sim.Cli.RunArtifactsWriter.WriteAsync(specYaml, spec, arr2, t2.FullName, includeEvents:false, CancellationToken.None);
            Assert.Equal(run1.Manifest.ScenarioHash, run2.Manifest.ScenarioHash);
            foreach (var kv in run1.Manifest.SeriesHashes)
            {
                Assert.True(run2.Manifest.SeriesHashes.TryGetValue(kv.Key, out var h2));
                Assert.Equal(kv.Value, h2);
            }
        }
        finally
        {
            try { t1.Delete(true); } catch { }
            try { t2.Delete(true); } catch { }
        }
    }
}