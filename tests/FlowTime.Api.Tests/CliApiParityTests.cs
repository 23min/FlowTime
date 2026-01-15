using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FlowTime.Api.Tests;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;

// Parity test: Invoke the real CLI (dotnet run) on examples/hello/model.yaml and compare
// the produced served.csv values with the API /run JSON response.
public class CliApiParityTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory factory;

    public CliApiParityTests(TestWebApplicationFactory factory)
    {
        // TestWebApplicationFactory already configures test isolation
        this.factory = factory;
    }

    [Fact]
    public async Task Cli_Output_ServedCsv_Equals_Api_Run_JSON_Served_Series()
	{
		// Arrange paths
		var root = FindRepoRoot();
        Assert.NotNull(root);
        var cliProj = Path.Combine(root!, "src", "FlowTime.Cli");
        var modelPath = Path.Combine(root!, "examples", "hello", "model.yaml");
        Assert.True(File.Exists(modelPath), $"Model file missing: {modelPath}");

        var outDir = Path.Combine(Path.GetTempPath(), "flowtime_cli_parity_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outDir);

        // Run CLI
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{cliProj}\" -- run \"{modelPath}\" --out \"{outDir}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        var proc = Process.Start(psi)!;
        var stdout = await proc.StandardOutput.ReadToEndAsync();
        var stderr = await proc.StandardError.ReadToEndAsync();
        proc.WaitForExit();
        Assert.Equal(0, proc.ExitCode);

        // Find the engine artifact directory from CLI output
        // CLI outputs: "Wrote artifacts to {path}"
        var match = System.Text.RegularExpressions.Regex.Match(stdout, @"Wrote artifacts to (.+)");
        Assert.True(match.Success, $"CLI stdout did not contain expected 'Wrote artifacts to' message. Stdout: {stdout}\nStderr: {stderr}");
        var artifactDir = match.Groups[1].Value.Trim();
        Assert.True(Directory.Exists(artifactDir), $"Artifact directory {artifactDir} does not exist");
        
        // Look for the served series CSV file
        var seriesDir = Path.Combine(artifactDir, "series");
        var csvPath = Path.Combine(seriesDir, "served@SERVED@DEFAULT.csv");
        Assert.True(File.Exists(csvPath), $"CLI did not produce served CSV at {csvPath}. Stdout: {stdout}\nStderr: {stderr}");
        var cliValues = File.ReadAllLines(csvPath)
            .Skip(1) // header
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Select(l => l.Split(',', 2)[1])
            .Select(s => double.Parse(s, CultureInfo.InvariantCulture))
            .ToArray();

        // Call API
        var yaml = await File.ReadAllTextAsync(modelPath);
        var client = factory.CreateClient();
        var resp = await client.PostAsync("/v1/run", new StringContent(yaml, Encoding.UTF8, "text/plain"));
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync();
            throw new Xunit.Sdk.XunitException($"/run returned {(int)resp.StatusCode} {resp.StatusCode}: {body}");
        }
        var doc = await resp.Content.ReadFromJsonAsync<RunResponse>();
        Assert.NotNull(doc);

        // Compare
        Assert.True(doc!.series.ContainsKey("served"), "API response missing 'served' series");
        var apiValues = doc.series["served"];
        Assert.Equal(cliValues.Length, apiValues.Length);
		Assert.Equal(cliValues, apiValues);
	}

	[Fact]
	public async Task Cli_Run_Applies_Router_Overrides()
	{
		var root = FindRepoRoot();
		Assert.NotNull(root);
		var cliProj = Path.Combine(root!, "src", "FlowTime.Cli");

		var tempRoot = Path.Combine(Path.GetTempPath(), "flowtime_cli_router_" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempRoot);
		var modelPath = Path.Combine(tempRoot, "router-model.yaml");
		await File.WriteAllTextAsync(modelPath, RouterOverrideModel);
		var outDir = Path.Combine(tempRoot, "out");
		Directory.CreateDirectory(outDir);

		try
		{
			var psi = new ProcessStartInfo
			{
				FileName = "dotnet",
				Arguments = $"run --project \"{cliProj}\" -- run \"{modelPath}\" --out \"{outDir}\" --deterministic-run-id",
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
				CreateNoWindow = true
			};
			var proc = Process.Start(psi)!;
			var stdout = await proc.StandardOutput.ReadToEndAsync();
			var stderr = await proc.StandardError.ReadToEndAsync();
			await proc.WaitForExitAsync();
			if (proc.ExitCode != 0)
			{
				throw new Xunit.Sdk.XunitException($"CLI exited with code {proc.ExitCode}: {stdout}\n{stderr}");
			}

			var match = System.Text.RegularExpressions.Regex.Match(stdout, @"Wrote artifacts to (.+)");
			Assert.True(match.Success, $"CLI stdout did not contain expected 'Wrote artifacts to' message. Stdout: {stdout}\nStderr: {stderr}");
			var runDir = match.Groups[1].Value.Trim();
			Assert.True(Directory.Exists(runDir), $"Run directory {runDir} does not exist");

			var routeAir = ReadDefaultSeriesValue(runDir, "ROUTE_AIR");
			var routeGround = ReadDefaultSeriesValue(runDir, "ROUTE_GROUND");

			Assert.Equal(8d, routeAir);
			Assert.Equal(2d, routeGround);
		}
		finally
		{
			try
			{
				Directory.Delete(tempRoot, recursive: true);
			}
			catch
			{
				// ignore cleanup errors in CI
			}
		}
	}

    private static string? FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        for (int i = 0; i < 8 && dir is not null; i++)
        {
            if (File.Exists(Path.Combine(dir!, "FlowTime.sln"))) return dir!;
            dir = Path.GetDirectoryName(dir);
        }
        return null;
    }

    private static double ReadDefaultSeriesValue(string runDirectory, string componentId)
    {
        var indexPath = Path.Combine(runDirectory, "series", "index.json");
        using var document = JsonDocument.Parse(File.ReadAllText(indexPath));
        foreach (var entry in document.RootElement.GetProperty("series").EnumerateArray())
        {
            var entryComponent = entry.GetProperty("componentId").GetString();
            var entryClass = entry.GetProperty("class").GetString();
            if (string.Equals(entryComponent, componentId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(entryClass, "DEFAULT", StringComparison.OrdinalIgnoreCase))
            {
                var relativePath = entry.GetProperty("path").GetString()
                    ?? throw new InvalidOperationException($"Series path missing for {componentId}");
                var resolvedPath = Path.Combine(runDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
                var lines = File.ReadAllLines(resolvedPath);
                Assert.True(lines.Length >= 2, $"Series {componentId} missing value rows");
                var parts = lines[1].Split(',', 2);
                Assert.Equal(2, parts.Length);
                return double.Parse(parts[1], CultureInfo.InvariantCulture);
            }
        }

        throw new Xunit.Sdk.XunitException($"Series {componentId} with DEFAULT class not found in index.json");
    }

    private const string RouterOverrideModel = """
schemaVersion: 1
generator: flowtime-sim
grid:
  bins: 1
  binSize: 60
  binUnit: minutes
classes:
  - id: Alpha
  - id: Beta
traffic:
  arrivals:
    - nodeId: alpha_inflow
      classId: Alpha
      pattern:
        kind: constant
        ratePerBin: 8
    - nodeId: beta_inflow
      classId: Beta
      pattern:
        kind: constant
        ratePerBin: 2
topology:
  nodes:
    - id: RouterNode
      kind: service
      semantics:
        arrivals: router_input
        served: route_air
        errors: route_ground
nodes:
  - id: alpha_inflow
    kind: const
    values: [8]
  - id: beta_inflow
    kind: const
    values: [2]
  - id: router_input
    kind: expr
    expr: alpha_inflow + beta_inflow
  - id: route_air
    kind: const
    values: [0]
  - id: route_ground
    kind: const
    values: [0]
  - id: hub_router
    kind: router
    inputs:
      queue: router_input
    routes:
      - target: route_air
        classes: [Alpha]
      - target: route_ground
        classes: [Beta]
outputs: []
""";

    public sealed class RunResponse
    {
        public required Grid grid { get; init; }
        public required string[] order { get; init; }
        public required Dictionary<string, double[]> series { get; init; }
    }
    public sealed class Grid { public int bins { get; init; } public int binSize { get; init; } public string binUnit { get; init; } = ""; }
}
