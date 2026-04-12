using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using FlowTime.Api.Tests.Infrastructure;
using FlowTime.TimeMachine.Orchestration;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FlowTime.Api.Tests;

public class TelemetryCaptureEndpointsTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory factory;

    public TelemetryCaptureEndpointsTests(TestWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task GenerateTelemetry_FromSimulationRun_Succeeds()
    {
        var templateDir = Path.Combine(factory.TestDataDirectory, "templates-capture-tests");
        Directory.CreateDirectory(templateDir);
        File.WriteAllText(Path.Combine(templateDir, "sim-order.yaml"), simulationTemplateYaml);

        var customizedFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("TemplatesDirectory", templateDir);
        });
        var customizedTestFactory = customizedFactory as TestWebApplicationFactory ?? factory;

        using var client = customizedFactory.CreateClient();
        var runId = await CreateRunAndImportAsync(
            customizedFactory,
            client,
            "sim-order",
            new Dictionary<string, object?>
            {
                ["bins"] = 4,
                ["binSize"] = 5
            });
        var runDirectory = Path.Combine(customizedTestFactory.TestDataDirectory, runId);
        var telemetryDir = Path.Combine(runDirectory, "model", "telemetry");
        if (Directory.Exists(telemetryDir))
        {
            Directory.Delete(telemetryDir, recursive: true);
        }

        var captureRequest = new
        {
            source = new { type = "run", runId },
            output = new { overwrite = false }
        };

        var captureResponse = await client.PostAsJsonAsync("/v1/telemetry/captures", captureRequest);
        captureResponse.EnsureSuccessStatusCode();
        var captureJson = await captureResponse.Content.ReadFromJsonAsync<JsonNode>() ?? throw new InvalidOperationException("Capture response invalid");
        var captureNode = captureJson["capture"] ?? throw new InvalidOperationException("Capture summary missing");
        Assert.True(captureNode["generated"]?.GetValue<bool>() ?? false);
        Assert.False(captureNode["supportsClassMetrics"]?.GetValue<bool>() ?? true);
        Assert.True(captureNode["classes"] is JsonArray { Count: 0 } or null);
        Assert.Equal("missing", captureNode["classCoverage"]?.GetValue<string>());

        Assert.True(Directory.Exists(telemetryDir));
        Assert.True(File.Exists(Path.Combine(telemetryDir, "manifest.json")));

        // Second call without overwrite should conflict
        var conflictResponse = await client.PostAsJsonAsync("/v1/telemetry/captures", captureRequest);
        Assert.Equal(HttpStatusCode.Conflict, conflictResponse.StatusCode);

        // With overwrite it should succeed again
        var overwriteRequest = new
        {
            source = new { type = "run", runId },
            output = new { overwrite = true }
        };

        var overwriteResponse = await client.PostAsJsonAsync("/v1/telemetry/captures", overwriteRequest);
        overwriteResponse.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task GenerateTelemetry_FromClassAwareRun_ReturnsClassMetadata()
    {
        var templateDir = Path.Combine(factory.TestDataDirectory, "templates-capture-tests-classes");
        Directory.CreateDirectory(templateDir);
        File.WriteAllText(Path.Combine(templateDir, "sim-order-classes.yaml"), simulationClassTemplateYaml);

        var customizedFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("TemplatesDirectory", templateDir);
        });

        using var client = customizedFactory.CreateClient();
        var runId = await CreateRunAndImportAsync(
            customizedFactory,
            client,
            "sim-order-classes",
            new Dictionary<string, object?>
            {
                ["bins"] = 6,
                ["binSize"] = 5
            });

        var captureRequest = new
        {
            source = new { type = "run", runId },
            output = new { overwrite = true }
        };

        var captureResponse = await client.PostAsJsonAsync("/v1/telemetry/captures", captureRequest);
        captureResponse.EnsureSuccessStatusCode();

        var captureJson = await captureResponse.Content.ReadFromJsonAsync<JsonNode>() ?? throw new InvalidOperationException("Capture response invalid JSON.");
        var captureNode = captureJson["capture"] ?? throw new InvalidOperationException("Capture summary missing.");
        Assert.True(captureNode["supportsClassMetrics"]?.GetValue<bool>() ?? false);
        Assert.Equal("full", captureNode["classCoverage"]?.GetValue<string>());

        var classesNode = captureNode["classes"] as JsonArray ?? throw new InvalidOperationException("classes missing");
        var classes = classesNode.Select(c => c?.GetValue<string>() ?? string.Empty).Where(id => !string.IsNullOrWhiteSpace(id)).OrderBy(id => id, StringComparer.OrdinalIgnoreCase).ToArray();
        Assert.Equal(new[] { "Retail", "Wholesale" }, classes);

        var customizedTestFactory = customizedFactory as TestWebApplicationFactory ?? factory;
        var runDirectory = Path.Combine(customizedTestFactory.TestDataDirectory, runId);
        var telemetryDir = Path.Combine(runDirectory, "model", "telemetry");
        var manifestPath = Path.Combine(telemetryDir, "telemetry-manifest.json");
        Assert.True(File.Exists(manifestPath), "telemetry-manifest.json should be created.");

        var manifestJson = await JsonNode.ParseAsync(File.OpenRead(manifestPath)) ?? throw new InvalidOperationException("telemetry-manifest.json invalid");

        Assert.Equal("full", manifestJson["classCoverage"]?.GetValue<string>());
        var manifestClasses = manifestJson["classes"] as JsonArray ?? throw new InvalidOperationException("Manifest classes missing");
        var manifestClassIds = manifestClasses.Select(c => c?.GetValue<string>() ?? string.Empty)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        Assert.Equal(new[] { "Retail", "Wholesale" }, manifestClassIds);
    }

    [Fact]
    public async Task GenerateTelemetry_WritesAutocaptureMetadata()
    {
        var templateDir = Path.Combine(factory.TestDataDirectory, "templates-capture-tests-metadata");
        Directory.CreateDirectory(templateDir);
        File.WriteAllText(Path.Combine(templateDir, "sim-order.yaml"), simulationTemplateYaml);

        var customizedFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("TemplatesDirectory", templateDir);
        });

        using var client = customizedFactory.CreateClient();
        var runId = await CreateRunAndImportAsync(
            customizedFactory,
            client,
            "sim-order",
            new Dictionary<string, object?>
            {
                ["bins"] = 4,
                ["binSize"] = 5
            });

        var captureRequest = new
        {
            source = new { type = "run", runId },
            output = new { overwrite = false }
        };

        var captureResponse = await client.PostAsJsonAsync("/v1/telemetry/captures", captureRequest);
        captureResponse.EnsureSuccessStatusCode();

        var customizedTestFactory = customizedFactory as TestWebApplicationFactory ?? factory;
        var runDirectory = Path.Combine(customizedTestFactory.TestDataDirectory, runId);
        var autocapturePath = Path.Combine(runDirectory, "model", "telemetry", "autocapture.json");
        Assert.True(File.Exists(autocapturePath), "autocapture.json should exist after capture.");

        var autocapture = await JsonNode.ParseAsync(File.OpenRead(autocapturePath)) ?? throw new InvalidOperationException("autocapture.json malformed");

        var rngSeed = autocapture["rngSeed"]?.GetValue<int?>();
        Assert.Equal(123, rngSeed);

        var metadataNode = await JsonNode.ParseAsync(File.OpenRead(Path.Combine(runDirectory, "model", "metadata.json"))) ?? throw new InvalidOperationException("metadata.json missing");
        var parametersNode = metadataNode["parameters"] as JsonObject ?? new JsonObject();
        var parametersDict = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
        foreach (var kvp in parametersNode)
        {
            parametersDict[kvp.Key] = kvp.Value?.DeepClone();
        }

        var templateVersion = metadataNode["templateVersion"]?.GetValue<string>() ?? "";

        var parametersHash = autocapture["parametersHash"]?.GetValue<string?>();
        var expectedParametersHash = ComputeParametersHash("sim-order", templateVersion, parametersDict, rngSeed ?? 0);
        Assert.Equal(expectedParametersHash, parametersHash);

        var scenarioHash = autocapture["scenarioHash"]?.GetValue<string?>();
        var manifestJson = await JsonNode.ParseAsync(File.OpenRead(Path.Combine(runDirectory, "manifest.json"))) ?? throw new InvalidOperationException("manifest.json missing");
        var expectedScenarioHash = manifestJson["scenarioHash"]?.GetValue<string?>();
        Assert.Equal(expectedScenarioHash, scenarioHash);
    }

    private static string ComputeParametersHash(string templateId, string templateVersion, IDictionary<string, JsonNode?> parameters, int seed)
    {
        var root = new JsonObject
        {
            ["templateId"] = templateId,
            ["templateVersion"] = templateVersion,
            ["rngSeed"] = seed
        };

        var parametersObject = new JsonObject();
        foreach (var kvp in parameters.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            parametersObject[kvp.Key] = kvp.Value?.DeepClone();
        }

        root["parameters"] = parametersObject;

        var json = root.ToJsonString();
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
        var hash = SHA256.HashData(bytes);
        return "sha256:" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static async Task<string> CreateRunAndImportAsync(
        WebApplicationFactory<Program> currentFactory,
        HttpClient client,
        string templateId,
        IReadOnlyDictionary<string, object?> parameters)
    {
        await using var scope = currentFactory.Services.CreateAsyncScope();
        var orchestration = scope.ServiceProvider.GetRequiredService<RunOrchestrationService>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var runsRoot = Program.ServiceHelpers.RunsRoot(configuration);
        Directory.CreateDirectory(runsRoot);

        var request = new RunOrchestrationRequest
        {
            TemplateId = templateId,
            Mode = "simulation",
            OutputRoot = runsRoot,
            DeterministicRunId = true,
            RunId = $"{templateId}_{Guid.NewGuid():N}",
            Parameters = parameters
        };

        var outcome = await orchestration.CreateRunAsync(request);
        var result = outcome.Result ?? throw new InvalidOperationException("Run orchestration did not produce a bundle.");
        return Path.GetFileName(result.RunDirectory);
    }

    private static void TryDeleteDirectory(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            return;
        }

        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch
        {
            // Swallow cleanup exceptions; tests shouldn't fail on best-effort cleanup.
        }
    }

    private const string simulationTemplateYaml = "schemaVersion: 1\ngenerator: flowtime-sim\nmetadata:\n  id: sim-order\n  title: Simulation Order Template\n  version: 1.0.0\nwindow:\n  start: 2025-01-01T00:00:00Z\n  timezone: UTC\n\nparameters:\n  - name: bins\n    type: integer\n    default: 4\n  - name: binSize\n    type: integer\n    default: 5\n\ngrid:\n  bins: ${bins}\n  binSize: ${binSize}\n  binUnit: minutes\n\ntopology:\n  nodes:\n    - id: OrderService\n      kind: service\n      semantics:\n        arrivals: arrivals\n        served: served\n        errors: errors\n  edges: []\n\nnodes:\n  - id: arrivals\n    kind: const\n    values: [10, 10, 10, 10]\n  - id: served\n    kind: const\n    values: [8, 9, 9, 10]\n  - id: errors\n    kind: const\n    values: [0, 0, 0, 0]\n\noutputs:\n  - series: \"*\"";

    private const string simulationClassTemplateYaml =
        "schemaVersion: 1\n" +
        "generator: flowtime-sim\n" +
        "metadata:\n" +
        "  id: sim-order-classes\n" +
        "  title: Simulation Order Template (Classes)\n" +
        "  version: 1.0.0\n" +
        "window:\n" +
        "  start: 2025-01-01T00:00:00Z\n" +
        "  timezone: UTC\n" +
        "\n" +
        "parameters:\n" +
        "  - name: bins\n" +
        "    type: integer\n" +
        "    default: 6\n" +
        "  - name: binSize\n" +
        "    type: integer\n" +
        "    default: 5\n" +
        "\n" +
        "grid:\n" +
        "  bins: ${bins}\n" +
        "  binSize: ${binSize}\n" +
        "  binUnit: minutes\n" +
        "\n" +
        "classes:\n" +
        "  - id: Retail\n" +
        "    displayName: Retail Flow\n" +
        "  - id: Wholesale\n" +
        "    displayName: Wholesale Flow\n" +
        "\n" +
        "traffic:\n" +
        "  arrivals:\n" +
        "    - nodeId: retail_demand\n" +
        "      classId: Retail\n" +
        "      pattern:\n" +
        "        kind: constant\n" +
        "        ratePerBin: 1\n" +
        "    - nodeId: wholesale_demand\n" +
        "      classId: Wholesale\n" +
        "      pattern:\n" +
        "        kind: constant\n" +
        "        ratePerBin: 1\n" +
        "\n" +
        "topology:\n" +
        "  nodes:\n" +
        "    - id: OrderService\n" +
        "      kind: service\n" +
        "      semantics:\n" +
        "        arrivals: total_arrivals\n" +
        "        served: total_served\n" +
        "        errors: total_errors\n" +
        "  edges: []\n" +
        "\n" +
        "nodes:\n" +
        "  - id: retail_demand\n" +
        "    kind: const\n" +
        "    values: [10, 11, 12, 13, 14, 15]\n" +
        "  - id: wholesale_demand\n" +
        "    kind: const\n" +
        "    values: [3, 4, 3, 4, 3, 4]\n" +
        "  - id: total_arrivals\n" +
        "    kind: expr\n" +
        "    expr: retail_demand + wholesale_demand\n" +
        "  - id: total_served\n" +
        "    kind: expr\n" +
        "    expr: total_arrivals\n" +
        "  - id: total_errors\n" +
        "    kind: const\n" +
        "    values: [0, 0, 0, 0, 0, 0]\n" +
        "\n" +
        "outputs:\n" +
        "  - series: retail_demand\n" +
        "    as: retail_demand.csv\n" +
        "  - series: wholesale_demand\n" +
        "    as: wholesale_demand.csv\n" +
        "  - series: total_arrivals\n" +
        "    as: total_arrivals.csv\n" +
        "  - series: total_served\n" +
        "    as: total_served.csv\n" +
        "  - series: total_errors\n" +
        "    as: total_errors.csv\n";

}
