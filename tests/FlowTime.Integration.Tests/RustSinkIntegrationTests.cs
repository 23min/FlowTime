using FlowTime.Adapters.Synthetic;
using FlowTime.Core.Execution;

namespace FlowTime.Integration.Tests;

/// <summary>
/// AC-9/AC-10: Verify Rust engine sink output is readable by C# infrastructure.
/// Uses FileSeriesReader to load run.json, manifest.json, index.json, and series CSVs
/// produced by the Rust engine's full sink layout.
/// </summary>
public class RustSinkIntegrationTests : IClassFixture<RustSinkIntegrationTests.SinkFixture>
{
    private readonly SinkFixture fixture;

    public RustSinkIntegrationTests(SinkFixture fixture)
    {
        this.fixture = fixture;
    }

    public sealed class SinkFixture
    {
        public string? EnginePath { get; }

        public SinkFixture()
        {
            var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
            var binaryPath = Path.Combine(repoRoot, "engine", "target", "release", "flowtime-engine");
            EnginePath = File.Exists(binaryPath) ? binaryPath : null;
        }
    }

    private const string SimpleModel = """
        schemaVersion: 1
        grid:
          bins: 4
          binSize: 1
          binUnit: hours
        nodes:
          - id: demand
            kind: const
            values: [10, 20, 30, 40]
          - id: served
            kind: expr
            expr: "demand * 0.8"
        """;

    private const string ClassModel = """
        schemaVersion: 1
        classes:
          - id: Premium
            displayName: Premium Tier
          - id: Standard
            displayName: Standard Tier
        grid:
          bins: 4
          binSize: 1
          binUnit: hours
        nodes:
          - id: arrivals
            kind: const
            values: [100, 100, 100, 100]
          - id: served
            kind: expr
            expr: "MIN(arrivals, 80)"
        traffic:
          arrivals:
            - nodeId: arrivals
              classId: Premium
              pattern:
                kind: constant
                ratePerBin: 40
            - nodeId: arrivals
              classId: Standard
              pattern:
                kind: constant
                ratePerBin: 60
        """;

    private const string TopologyModel = """
        schemaVersion: 1
        grid:
          bins: 4
          binSize: 1
          binUnit: hours
        nodes:
          - id: arrivals
            kind: const
            values: [10, 10, 10, 10]
          - id: served
            kind: const
            values: [3, 3, 3, 3]
        topology:
          nodes:
            - id: Queue
              kind: serviceWithBuffer
              semantics:
                arrivals: arrivals
                served: served
          edges: []
          constraints: []
        """;

    /// <summary>
    /// Evaluate a model through the Rust engine and return the output directory.
    /// Caller is responsible for cleanup.
    /// </summary>
    private async Task<string> EvalToDir(string modelYaml)
    {
        if (fixture.EnginePath is null) Assert.Fail("Rust engine binary not found — skipping");

        var tempDir = Path.Combine(Path.GetTempPath(), $"rust-sink-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        var modelPath = Path.Combine(tempDir, "model.yaml");
        var outputDir = Path.Combine(tempDir, "output");

        await File.WriteAllTextAsync(modelPath, modelYaml, new System.Text.UTF8Encoding(false));

        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = fixture.EnginePath,
            Arguments = $"eval \"{modelPath}\" --output \"{outputDir}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using var proc = System.Diagnostics.Process.Start(psi)!;
        await proc.WaitForExitAsync();

        Assert.Equal(0, proc.ExitCode);
        return outputDir;
    }

    // ── AC-9: C# FileSeriesReader can load Rust sink artifacts ──

    [Fact]
    public async Task RunJson_DeserializesViaFileSeriesReader()
    {
        if (fixture.EnginePath is null) return;
        var dir = await EvalToDir(SimpleModel);
        try
        {
            var reader = new FileSeriesReader();
            var manifest = await reader.ReadRunInfoAsync(dir);

            Assert.Equal(1, manifest.SchemaVersion);
            Assert.NotEmpty(manifest.RunId);
            Assert.Equal("0.1.0", manifest.EngineVersion);
            Assert.Equal("engine", manifest.Source);
            Assert.Equal(4, manifest.Grid.Bins);
            Assert.Equal(1, manifest.Grid.BinSize);
            Assert.Equal("hours", manifest.Grid.BinUnit);
            Assert.NotNull(manifest.ModelHash);
            Assert.StartsWith("sha256:", manifest.ModelHash);
            Assert.NotNull(manifest.ScenarioHash);
            Assert.NotEmpty(manifest.Series);
        }
        finally { Directory.Delete(Path.GetDirectoryName(dir)!, true); }
    }

    [Fact]
    public async Task ManifestJson_DeserializesViaFileSeriesReader()
    {
        if (fixture.EnginePath is null) return;
        var dir = await EvalToDir(SimpleModel);
        try
        {
            var reader = new FileSeriesReader();
            var manifest = await reader.ReadManifestAsync(dir);

            Assert.Equal(1, manifest.SchemaVersion);
            Assert.NotEmpty(manifest.ScenarioHash);
            Assert.NotNull(manifest.Rng);
            Assert.Equal("none", manifest.Rng.Kind);
            Assert.NotEmpty(manifest.SeriesHashes);
            foreach (var (_, hash) in manifest.SeriesHashes)
            {
                Assert.StartsWith("sha256:", hash);
            }
        }
        finally { Directory.Delete(Path.GetDirectoryName(dir)!, true); }
    }

    [Fact]
    public async Task IndexJson_DeserializesViaFileSeriesReader()
    {
        if (fixture.EnginePath is null) return;
        var dir = await EvalToDir(SimpleModel);
        try
        {
            var reader = new FileSeriesReader();
            var index = await reader.ReadIndexAsync(dir);

            Assert.NotNull(index);
            Assert.NotEmpty(index.Series);
            // Series should use @COMPONENT@CLASS naming
            Assert.Contains(index.Series, s => s.Id.Contains("@"));
        }
        finally { Directory.Delete(Path.GetDirectoryName(dir)!, true); }
    }

    [Fact]
    public async Task SeriesCsv_ReadableViaFileSeriesReader()
    {
        if (fixture.EnginePath is null) return;
        var dir = await EvalToDir(SimpleModel);
        try
        {
            var reader = new FileSeriesReader();
            var index = await reader.ReadIndexAsync(dir);

            // Read each series referenced in index
            foreach (var seriesRef in index.Series)
            {
                Assert.True(reader.SeriesExists(dir, seriesRef.Id),
                    $"Series file should exist for '{seriesRef.Id}'");

                var series = await reader.ReadSeriesAsync(dir, seriesRef.Id);
                Assert.NotNull(series);
                Assert.Equal(4, series.Length); // 4 bins
            }
        }
        finally { Directory.Delete(Path.GetDirectoryName(dir)!, true); }
    }

    // ── AC-9: Class-enabled model with full per-class decomposition ──

    [Fact]
    public async Task ClassModel_PerClassSeriesLoadable()
    {
        if (fixture.EnginePath is null) return;
        var dir = await EvalToDir(ClassModel);
        try
        {
            var reader = new FileSeriesReader();
            var manifest = await reader.ReadRunInfoAsync(dir);
            var index = await reader.ReadIndexAsync(dir);

            // Should have class metadata
            Assert.NotNull(manifest.Classes);
            Assert.Equal(2, manifest.Classes.Length);
            Assert.Contains(manifest.Classes, c => c.Id == "Premium");
            Assert.Contains(manifest.Classes, c => c.Id == "Standard");

            // Should have DEFAULT and per-class series
            var defaultSeries = index.Series.Where(s => s.Id.EndsWith("@DEFAULT")).ToList();
            var premiumSeries = index.Series.Where(s => s.Id.EndsWith("@Premium")).ToList();
            var standardSeries = index.Series.Where(s => s.Id.EndsWith("@Standard")).ToList();

            Assert.NotEmpty(defaultSeries);
            Assert.NotEmpty(premiumSeries);
            Assert.NotEmpty(standardSeries);

            // Read a per-class series to verify values
            var premiumArrivals = premiumSeries.FirstOrDefault(s => s.Id.StartsWith("arrivals@"));
            Assert.NotNull(premiumArrivals);

            var series = await reader.ReadSeriesAsync(dir, premiumArrivals!.Id);
            Assert.Equal(4, series.Length);
            // Premium fraction = 40/(40+60) = 0.4, total arrivals = 100
            // Normalized: 100 * 0.4 = 40
            Assert.Equal(40.0, series[0], precision: 6);
        }
        finally { Directory.Delete(Path.GetDirectoryName(dir)!, true); }
    }

    // ── AC-9: Topology model produces spec.yaml with file: URIs ──

    [Fact]
    public async Task TopologyModel_SpecYamlHasFileUris()
    {
        if (fixture.EnginePath is null) return;
        var dir = await EvalToDir(TopologyModel);
        try
        {
            var specPath = Path.Combine(dir, "spec.yaml");
            Assert.True(File.Exists(specPath), "spec.yaml should exist");

            var spec = await File.ReadAllTextAsync(specPath);
            Assert.Contains("file:series/arrivals@ARRIVALS@DEFAULT.csv", spec);
            Assert.Contains("file:series/served@SERVED@DEFAULT.csv", spec);
        }
        finally { Directory.Delete(Path.GetDirectoryName(dir)!, true); }
    }

    [Fact]
    public async Task TopologyModel_ModelDirectoryComplete()
    {
        if (fixture.EnginePath is null) return;
        var dir = await EvalToDir(TopologyModel);
        try
        {
            Assert.True(File.Exists(Path.Combine(dir, "model", "model.yaml")), "model/model.yaml");
            Assert.True(File.Exists(Path.Combine(dir, "model", "metadata.json")), "model/metadata.json");
            Assert.True(Directory.Exists(Path.Combine(dir, "aggregates")), "aggregates/ directory");

            // metadata.json should be valid JSON with required fields
            var meta = await File.ReadAllTextAsync(Path.Combine(dir, "model", "metadata.json"));
            var doc = System.Text.Json.JsonDocument.Parse(meta);
            Assert.Equal(1, doc.RootElement.GetProperty("schemaVersion").GetInt32());
            Assert.NotEmpty(doc.RootElement.GetProperty("templateId").GetString()!);
            Assert.NotEmpty(doc.RootElement.GetProperty("mode").GetString()!);
        }
        finally { Directory.Delete(Path.GetDirectoryName(dir)!, true); }
    }

    // ── AC-10: Verify series values match C# evaluation ──

    [Fact]
    public async Task SimpleModel_SeriesValuesMatchExpected()
    {
        if (fixture.EnginePath is null) return;
        var dir = await EvalToDir(SimpleModel);
        try
        {
            var reader = new FileSeriesReader();
            var index = await reader.ReadIndexAsync(dir);

            // Find demand and served DEFAULT series
            var demandRef = index.Series.First(s => s.Id.StartsWith("demand@") && s.Id.EndsWith("@DEFAULT"));
            var servedRef = index.Series.First(s => s.Id.StartsWith("served@") && s.Id.EndsWith("@DEFAULT"));

            var demand = await reader.ReadSeriesAsync(dir, demandRef.Id);
            var served = await reader.ReadSeriesAsync(dir, servedRef.Id);

            Assert.Equal(new double[] { 10, 20, 30, 40 }, demand.ToArray());
            // served = demand * 0.8
            Assert.Equal(new double[] { 8, 16, 24, 32 }, served.ToArray());
        }
        finally { Directory.Delete(Path.GetDirectoryName(dir)!, true); }
    }

    [Fact]
    public async Task TopologyModel_QueueDepthCorrect()
    {
        if (fixture.EnginePath is null) return;
        var dir = await EvalToDir(TopologyModel);
        try
        {
            var reader = new FileSeriesReader();
            var index = await reader.ReadIndexAsync(dir);

            // Find queue depth series
            var queueRef = index.Series.FirstOrDefault(s =>
                s.Id.Contains("queue", StringComparison.OrdinalIgnoreCase) && s.Id.EndsWith("@DEFAULT"));
            Assert.NotNull(queueRef);

            var queue = await reader.ReadSeriesAsync(dir, queueRef!.Id);
            // Q[0]=7, Q[1]=14, Q[2]=21, Q[3]=28 (inflow=10, outflow=3)
            Assert.Equal(new double[] { 7, 14, 21, 28 }, queue.ToArray());
        }
        finally { Directory.Delete(Path.GetDirectoryName(dir)!, true); }
    }
}
