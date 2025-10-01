using System.Text;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace FlowTime.Sim.Tests;

public class SchemaValidationTests
{
    private static (string runDir, string runJson, string manifestJson, string seriesIndexJson) WriteRun(string specYaml)
    {
        var specPath = Path.GetTempFileName();
        File.WriteAllText(specPath, specYaml, Encoding.UTF8);
        var outDir = Path.Combine(Path.GetTempPath(), "flow-sim-schema-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outDir);
        var exit = FlowTime.Sim.Cli.ProgramWrapper.InvokeMain(new[] { "--mode", "sim", "--model", specPath, "--out", outDir }).GetAwaiter().GetResult();
        Assert.Equal(0, exit);
        var runsRoot = Path.Combine(outDir, "runs");
        Assert.True(Directory.Exists(runsRoot));
        var runDir = Directory.GetDirectories(runsRoot).Single();
        string Read(string rel) => File.ReadAllText(Path.Combine(runDir, rel), Encoding.UTF8);
        return (runDir, Read("run.json"), Read("manifest.json"), Read(Path.Combine("series", "index.json")));
    }

        [Fact(Skip = "Legacy run-based storage removed - CLI now generates models only")]
    public async Task Run_Artifacts_MinimalManualContractChecks()
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
  values: [2,3,4]
route:
  id: nodeA
""";
        var (runDir, runJson, manifestJson, seriesIndexJson) = WriteRun(spec);
        using var runDoc = JsonDocument.Parse(runJson);
        var root = runDoc.RootElement;
  Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
  var runId = root.GetProperty("runId").GetString();
  // New standardized opaque pattern (documented): sim_YYYY-MM-DDTHH-mm-ssZ_abcdefgh
  Assert.Matches("^sim_\\d{4}-\\d{2}-\\d{2}T\\d{2}-\\d{2}-\\d{2}Z_[a-f0-9]{8}$", runId!);
        var grid = root.GetProperty("grid");
        Assert.True(grid.GetProperty("bins").GetInt32() > 0);
        Assert.True(grid.GetProperty("binMinutes").GetInt32() > 0);
        var rng = root.GetProperty("rng");
        Assert.Equal("pcg32", rng.GetProperty("kind").GetString());
        _ = rng.GetProperty("seed").GetInt64();
        var seriesHashes = root.GetProperty("seriesHashes");
        foreach (var kv in seriesHashes.EnumerateObject())
        {
            var hash = kv.Value.GetString();
            Assert.NotNull(hash);
            Assert.Matches("^sha256:[0-9a-fA-F]{64}$", hash!);
        }
        using var siDoc = JsonDocument.Parse(seriesIndexJson);
        var entries = siDoc.RootElement.GetProperty("series");
        Assert.True(entries.GetArrayLength() >= 1);
        foreach (var e in entries.EnumerateArray())
        {
            var path = e.GetProperty("path").GetString();
            Assert.NotNull(path);
            var full = Path.Combine(runDir, path!);
            Assert.True(File.Exists(full), $"Missing series CSV: {full}");
            var fileHash = e.GetProperty("hash").GetString();
            Assert.NotNull(fileHash);
            Assert.Matches("^sha256:[0-9a-fA-F]{64}$", fileHash!);
        }
        // Dual write identity covered in separate test.
    }

    [Fact(Skip = "Legacy run-based storage removed - CLI now generates models only")]
    public void Run_And_Manifest_Dual_Write_Identical()
    {
        var spec = """
schemaVersion: 1
grid:
  bins: 2
  binMinutes: 30
  start: 2025-01-01T00:00:00Z
seed: 9
arrivals:
  kind: const
  values: [5,6]
route:
  id: nodeB
""";
        var (_, runJson, manifestJson, _) = WriteRun(spec);
        Assert.Equal(runJson, manifestJson);
    }

    [Fact(Skip = "Legacy run-based storage removed - CLI now generates models only")]
    public async Task Series_Hash_Detects_Tampering()
    {
        var spec = """
schemaVersion: 1
grid:
  bins: 2
  binMinutes: 30
  start: 2025-01-01T00:00:00Z
seed: 9
arrivals:
  kind: const
  values: [5,6]
route:
  id: nodeB
""";
        var (runDir, runJson, manifestJson, _) = WriteRun(spec);
        using var manifestDoc = JsonDocument.Parse(manifestJson);
        var seriesHashes = manifestDoc.RootElement.GetProperty("seriesHashes");
        var firstId = seriesHashes.EnumerateObject().First().Name;
        var firstPath = Path.Combine(runDir, "series", firstId + ".csv");
        // Tamper: append an extra newline
        File.AppendAllText(firstPath, "\n");
        // Recompute hash
        using var fs = File.OpenRead(firstPath);
        var sha = System.Security.Cryptography.SHA256.HashData(fs);
        var newHash = "sha256:" + Convert.ToHexString(sha).ToLowerInvariant();
        var originalHash = seriesHashes.GetProperty(firstId).GetString();
        Assert.NotEqual(originalHash, newHash);
    }
}

internal static class TestContext
{
    public static string RepoRoot { get; } = LocateRepoRoot();

    private static string LocateRepoRoot()
    {
        var dir = Directory.GetCurrentDirectory();
        while (dir != null && !File.Exists(Path.Combine(dir, "FlowTimeSim.sln")))
        {
            dir = Path.GetDirectoryName(dir);
        }
        return dir ?? Directory.GetCurrentDirectory();
    }
}