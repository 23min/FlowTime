using FlowTime.Adapters.Synthetic;
using Xunit;

namespace FlowTime.Adapters.Synthetic.Tests;

/// <summary>
/// Integration test that demonstrates reading CLI-produced artifacts with the new adapter
/// </summary>
public class CliIntegrationTests
{
    [Fact]
    public async Task CanReadCliProducedArtifacts()
    {
        // This test assumes the CLI has been run with the hello example
        // We'll create a temporary run to ensure it exists
        var tempOutput = Path.Combine(Path.GetTempPath(), "flowtime-cli-test");
        
        // Run the CLI to produce artifacts
        var process = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"run --project {GetCliProjectPath()} -- run {GetHelloModelPath()} --out {tempOutput}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = GetFlowTimeRoot()
            }
        };

        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            throw new Exception($"CLI failed: {error}");
        }

        // Extract the run directory from output
        var outputLine = output.Trim();
        var runPath = outputLine.Replace("Wrote artifacts to ", "");
        
        try
        {
            // Use our adapter to read the CLI artifacts
            var reader = new FileSeriesReader();
            var adapter = new RunArtifactAdapter(reader, runPath);

            // Read the manifest
            var manifest = await adapter.GetManifestAsync();
            Assert.Equal("engine", manifest.Source); // CLI produces engine artifacts
            Assert.True(manifest.EngineVersion.Length > 0);
            Assert.Contains("engine_", manifest.RunId);

            // Read the series index
            var index = await adapter.GetIndexAsync();
            Assert.True(index.Series.Length > 0);

            // Read a series (the hello example should have 'served' series)
            var servedSeriesMetadata = index.Series.FirstOrDefault(s => s.Id.Contains("served"));
            if (servedSeriesMetadata != null)
            {
                var servedSeries = await adapter.GetSeriesAsync(servedSeriesMetadata.Id);
                Assert.True(servedSeries.Length > 0);
                Assert.Equal(manifest.Grid.Bins, servedSeries.Length);
            }

            // Validate artifacts
            var validation = await adapter.ValidateAsync();
            Assert.True(validation.IsValid, 
                $"Validation failed. Errors: [{string.Join(", ", validation.Errors)}]");

            // Test FlowTime.Core compatibility
            var coreGrid = await adapter.GetCoreTimeGridAsync();
            Assert.Equal(manifest.Grid.Bins, coreGrid.Bins);
            Assert.Equal(manifest.Grid.BinMinutes, coreGrid.BinMinutes);
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(tempOutput))
            {
                Directory.Delete(tempOutput, true);
            }
        }
    }

    private static string GetFlowTimeRoot()
    {
        // Navigate up from test directory to find the FlowTime root
        var current = Directory.GetCurrentDirectory();
        while (current != null && !File.Exists(Path.Combine(current, "FlowTime.sln")))
        {
            current = Directory.GetParent(current)?.FullName;
        }
        return current ?? throw new DirectoryNotFoundException("Could not find FlowTime root directory");
    }

    private static string GetCliProjectPath()
    {
        return Path.Combine(GetFlowTimeRoot(), "src", "FlowTime.Cli", "FlowTime.Cli.csproj");
    }

    private static string GetHelloModelPath()
    {
        return Path.Combine(GetFlowTimeRoot(), "examples", "hello", "model.yaml");
    }
}
