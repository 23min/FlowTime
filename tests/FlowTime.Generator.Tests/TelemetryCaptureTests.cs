using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using FlowTime.Generator;
using FlowTime.Generator.Models;
using FlowTime.Generator.Processing;
using FlowTime.Tests.Support;
using Json.Schema;
using Xunit.Sdk;

namespace FlowTime.Generator.Tests;

public sealed class TelemetryCaptureTests
{
    [Fact]
    public async Task ExecuteAsync_WritesTelemetryBundle_WithManifest()
    {
        using var temp = new TempDirectory();
        var runDir = TelemetryRunFactory.CreateRunArtifacts(temp.Path, "run_test_capture", includeTopology: true);
        var outputDir = System.IO.Path.Combine(temp.Path, "capture-out");

        var capture = new TelemetryCapture();
        var options = new TelemetryCaptureOptions
        {
            RunDirectory = runDir,
            OutputDirectory = outputDir,
            DryRun = false,
            GapOptions = GapInjectorOptions.Default
        };

        var plan = await capture.ExecuteAsync(options);

        Assert.Equal("run_test_capture", plan.RunId);
        Assert.Equal(6, plan.Files.Count);
        Assert.Equal(plan.Files.Count, Directory.GetFiles(outputDir, "*.csv").Length);

        foreach (var file in plan.Files)
        {
            var csvPath = System.IO.Path.Combine(outputDir, file.TargetFileName);
            Assert.True(File.Exists(csvPath));
            var lines = await File.ReadAllLinesAsync(csvPath);
            Assert.Equal("bin_index,value", lines.First());
            Assert.Equal(5, lines.Length); // header + 4 rows
        }

        var manifestPath = System.IO.Path.Combine(outputDir, "manifest.json");
        Assert.True(File.Exists(manifestPath));

        using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(manifestPath));
        var root = doc.RootElement;
        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(plan.Files.Count, root.GetProperty("files").GetArrayLength());
        Assert.Equal("sha256:1111111111111111111111111111111111111111111111111111111111111111", root.GetProperty("provenance").GetProperty("scenarioHash").GetString());

        var schema = LoadTelemetryManifestSchema();
        var evaluation = schema.Evaluate(root, new EvaluationOptions { OutputFormat = OutputFormat.Hierarchical });
        if (!evaluation.IsValid)
        {
            var details = JsonSerializer.Serialize(evaluation, new JsonSerializerOptions { WriteIndented = true });
            throw new Xunit.Sdk.XunitException(details);
        }

        foreach (var file in root.GetProperty("files").EnumerateArray())
        {
            var fileName = file.GetProperty("path").GetString()!;
            var manifestHash = file.GetProperty("hash").GetString();
            var csvPath = System.IO.Path.Combine(outputDir, fileName);
            Assert.Equal(ComputeHash(csvPath), manifestHash);
        }

        // Spot check first file data was normalised correctly (bin 0 value)
        var arrivalsCsv = System.IO.Path.Combine(outputDir, "OrderService_arrivals.csv");
        var arrivalLines = await File.ReadAllLinesAsync(arrivalsCsv);
        Assert.Equal("0,10", arrivalLines[1]);
    }

    [Fact]
    public async Task ExecuteAsync_WithGapFill_WritesWarnings()
    {
        using var temp = new TempDirectory();
        var runDir = TelemetryRunFactory.CreateRunArtifacts(temp.Path, "run_gap_fill", includeTopology: true);

        // Introduce NaN in two canonical series
        await InjectNanAsync(Path.Combine(runDir, "series", "order_served@ORDER_SERVED@DEFAULT.csv"), binIndex: 1);
        await InjectNanAsync(Path.Combine(runDir, "series", "payment_served@PAYMENT_SERVED@DEFAULT.csv"), binIndex: 2);

        var capture = new TelemetryCapture();
        var options = new TelemetryCaptureOptions
        {
            RunDirectory = runDir,
            OutputDirectory = System.IO.Path.Combine(temp.Path, "gap-out"),
            DryRun = false,
            GapOptions = new FlowTime.Generator.Processing.GapInjectorOptions(FillNaNWithZero: true)
        };

        var plan = await capture.ExecuteAsync(options);

        var nanWarnings = plan.Warnings.Where(w => w.Code == "nan_fill").ToArray();
        Assert.Equal(2, nanWarnings.Length);

        var manifestPath = System.IO.Path.Combine(options.OutputDirectory, "manifest.json");
        using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(manifestPath));
        var warnings = doc.RootElement.GetProperty("warnings");
        Assert.Equal(2, warnings.GetArrayLength());
    }

    [Fact]
    public async Task ExecuteAsync_WithLengthMismatch_EmitsWarning()
    {
        using var temp = new TempDirectory();
        var runDir = TelemetryRunFactory.CreateRunArtifacts(temp.Path, "run_length_mismatch", includeTopology: true);

        var seriesPath = Path.Combine(runDir, "series", "order_arrivals@ORDER_ARRIVALS@DEFAULT.csv");
        var lines = (await File.ReadAllLinesAsync(seriesPath)).ToList();
        lines.RemoveAt(lines.Count - 1); // remove final data row
        await File.WriteAllLinesAsync(seriesPath, lines);

        var capture = new TelemetryCapture();
        var options = new TelemetryCaptureOptions
        {
            RunDirectory = runDir,
            OutputDirectory = Path.Combine(temp.Path, "length-out"),
            DryRun = false,
            GapOptions = GapInjectorOptions.Default
        };

        var plan = await capture.ExecuteAsync(options);

        Assert.Contains(plan.Warnings, w => w.Code == "length_mismatch");

        var manifestPath = Path.Combine(options.OutputDirectory, "manifest.json");
        using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(manifestPath));
        var warnings = doc.RootElement.GetProperty("warnings");
        Assert.Contains(warnings.EnumerateArray(), w => w.GetProperty("code").GetString() == "length_mismatch");
    }

    private static JsonSchema LoadTelemetryManifestSchema()
    {
        var schemaPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "docs", "schemas", "telemetry-manifest.schema.json"));
        return JsonSchema.FromText(File.ReadAllText(schemaPath));
    }

    private static string ComputeHash(string path)
    {
        using var stream = File.OpenRead(path);
        var hash = SHA256.HashData(stream);
        return "sha256:" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static async Task InjectNanAsync(string filePath, int binIndex)
    {
        var lines = await File.ReadAllLinesAsync(filePath);
        var parts = lines[binIndex + 1].Split(',');
        parts[1] = "NaN";
        lines[binIndex + 1] = string.Join(',', parts);
        await File.WriteAllLinesAsync(filePath, lines);
    }
}
