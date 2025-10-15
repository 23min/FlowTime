using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using FlowTime.Generator;
using FlowTime.Generator.Models;
using FlowTime.Generator.Processing;
using Xunit.Abstractions;
using FlowTime.Tests.Support;

namespace FlowTime.Generator.Tests;

public sealed class TelemetryCaptureGoldenTests
{
    private readonly ITestOutputHelper output;

    public TelemetryCaptureGoldenTests(ITestOutputHelper output) => this.output = output;
    public static IEnumerable<object[]> FixtureData => new[]
    {
        new object[] { FixtureKind.OrderSystem, "run_order_system" },
        new object[] { FixtureKind.Microservices, "run_microservices" },
        new object[] { FixtureKind.HttpService, "run_http_service" }
    };

    [Theory]
    [MemberData(nameof(FixtureData))]
    public async Task CaptureBundlesMatchGoldenExpectations(FixtureKind kind, string runId)
    {
        using var temp = new TempDirectory();
        var runDir = TelemetryRunFactory.CreateRunArtifacts(temp.Path, runId, kind, includeTopology: true);
        var captureDir = Path.Combine(temp.Path, "capture");

        var capture = new TelemetryCapture();
        var options = new TelemetryCaptureOptions
        {
            RunDirectory = runDir,
            OutputDirectory = captureDir,
            DryRun = false,
            GapOptions = GapInjectorOptions.Default
        };

        var plan = await capture.ExecuteAsync(options);
        Assert.Empty(plan.Warnings);

        foreach (var file in plan.Files)
        {
            output.WriteLine($"planned file: {file.TargetFileName} ({file.NodeId}:{file.Metric})");
        }

        var specPath = Path.Combine(runDir, "spec.yaml");
        output.WriteLine(File.ReadAllText(specPath));

        var definition = TelemetryRunFactory.GetDefinition(kind);
        Assert.Equal(definition.Series.Count, plan.Files.Count);
        var manifestPath = Path.Combine(captureDir, "manifest.json");
        Assert.True(File.Exists(manifestPath), "manifest.json should be generated");

        using var manifestDoc = JsonDocument.Parse(await File.ReadAllTextAsync(manifestPath));
        var manifestRoot = manifestDoc.RootElement;
        Assert.Equal(runId, manifestRoot.GetProperty("provenance").GetProperty("runId").GetString());
        Assert.Equal(definition.Series.Count, manifestRoot.GetProperty("files").GetArrayLength());

        foreach (var series in definition.Series)
        {
            var expectedPath = series.OutputFileName.Replace('\\', '/');
            var fileElement = manifestRoot
                .GetProperty("files")
                .EnumerateArray()
                .Single(f => string.Equals(f.GetProperty("path").GetString(), expectedPath, StringComparison.Ordinal));

            var csvPath = Path.Combine(captureDir, expectedPath.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(csvPath), $"CSV not found: {csvPath}");

            var lines = await File.ReadAllLinesAsync(csvPath);
            Assert.Equal("bin_index,value", lines[0]);
            Assert.Equal(series.Values.Length + 1, lines.Length);

            for (var i = 0; i < series.Values.Length; i++)
            {
                var parts = lines[i + 1].Split(',', StringSplitOptions.TrimEntries);
                Assert.Equal(i.ToString(CultureInfo.InvariantCulture), parts[0]);
                Assert.Equal(series.Values[i].ToString("G17", CultureInfo.InvariantCulture), parts[1]);
            }

            var expectedHash = ComputeSha256(csvPath);
            Assert.Equal(expectedHash, fileElement.GetProperty("hash").GetString());
        }
    }

    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        var hash = SHA256.HashData(stream);
        return "sha256:" + Convert.ToHexString(hash).ToLowerInvariant();
    }
}
