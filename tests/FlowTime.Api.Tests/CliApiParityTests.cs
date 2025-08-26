using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;

// Parity test: Invoke the real CLI (dotnet run) on examples/hello/model.yaml and compare
// the produced served.csv values with the API /run JSON response.
public class CliApiParityTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;

    public CliApiParityTests(WebApplicationFactory<Program> factory)
    {
        this.factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseTestServer();
            builder.UseSetting(Microsoft.AspNetCore.Hosting.WebHostDefaults.ServerUrlsKey, "http://127.0.0.1:0");
        });
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

        var csvPath = Path.Combine(outDir, "served.csv");
        Assert.True(File.Exists(csvPath), $"CLI did not produce served.csv. Stdout: {stdout}\nStderr: {stderr}");
        var cliValues = File.ReadAllLines(csvPath)
            .Skip(1) // header
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Select(l => l.Split(',', 2)[1])
            .Select(s => double.Parse(s, CultureInfo.InvariantCulture))
            .ToArray();

        // Call API
        var yaml = await File.ReadAllTextAsync(modelPath);
        var client = factory.CreateClient();
        var resp = await client.PostAsync("/run", new StringContent(yaml, Encoding.UTF8, "text/plain"));
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

    public sealed class RunResponse
    {
        public required Grid grid { get; init; }
        public required string[] order { get; init; }
        public required Dictionary<string, double[]> series { get; init; }
    }
    public sealed class Grid { public int bins { get; init; } public int binMinutes { get; init; } }
}
