using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using FlowTime.Api.Tests.Infrastructure;
using System.Security.Cryptography;
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
        File.WriteAllText(Path.Combine(templateDir, "sim-order.yaml"), SimulationTemplateYaml);

        var customizedFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("TemplatesDirectory", templateDir);
        });

        using var client = customizedFactory.CreateClient();

        var simulationPayload = new
        {
            templateId = "sim-order",
            mode = "simulation",
            parameters = new { bins = 4, binSize = 5 },
            options = new { deterministicRunId = true }
        };

        var simulateResponse = await client.PostAsJsonAsync("/v1/runs", simulationPayload);
        simulateResponse.EnsureSuccessStatusCode();
        var simulateJson = await simulateResponse.Content.ReadFromJsonAsync<JsonNode>() ?? throw new InvalidOperationException("Simulation response was not valid JSON.");
        var runId = simulateJson["metadata"]?["runId"]?.GetValue<string>() ?? throw new InvalidOperationException("runId missing");

        var captureRequest = new
        {
            source = new { type = "run", runId },
            output = new { overwrite = false }
        };

        var captureResponse = await client.PostAsJsonAsync("/v1/telemetry/captures", captureRequest);
        captureResponse.EnsureSuccessStatusCode();
        var captureJson = await captureResponse.Content.ReadFromJsonAsync<JsonNode>() ?? throw new InvalidOperationException("Capture response invalid");
        Assert.True(captureJson["capture"]?["generated"]?.GetValue<bool>() ?? false);

        var customizedTestFactory = customizedFactory as TestWebApplicationFactory ?? factory;
        var runDirectory = Path.Combine(customizedTestFactory.TestDataDirectory, runId);
        var telemetryDir = Path.Combine(runDirectory, "model", "telemetry");
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
    public async Task GenerateTelemetry_WritesAutocaptureMetadata()
    {
        var templateDir = Path.Combine(factory.TestDataDirectory, "templates-capture-tests-metadata");
        Directory.CreateDirectory(templateDir);
        File.WriteAllText(Path.Combine(templateDir, "sim-order.yaml"), SimulationTemplateYaml);

        var customizedFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("TemplatesDirectory", templateDir);
        });

        using var client = customizedFactory.CreateClient();

        var simulationPayload = new
        {
            templateId = "sim-order",
            mode = "simulation",
            parameters = new { bins = 4, binSize = 5 },
            options = new { deterministicRunId = true }
        };

        var simulateResponse = await client.PostAsJsonAsync("/v1/runs", simulationPayload);
        simulateResponse.EnsureSuccessStatusCode();
        var simulateJson = await simulateResponse.Content.ReadFromJsonAsync<JsonNode>() ?? throw new InvalidOperationException("Simulation response was not valid JSON.");
        var runId = simulateJson["metadata"]? ["runId"]?.GetValue<string>() ?? throw new InvalidOperationException("runId missing");

        var captureRequest = new
        {
            source = new { type = "run", runId },
            output = new { overwrite = false }
        };

        var captureResponse = await client.PostAsJsonAsync("/v1/telemetry/captures", captureRequest);
        captureResponse.EnsureSuccessStatusCode();

        var runDirectory = Path.Combine(factory.TestDataDirectory, runId);
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

    private const string SimulationTemplateYaml = "schemaVersion: 1\ngenerator: flowtime-sim\nmetadata:\n  id: sim-order\n  title: Simulation Order Template\n  version: 1.0.0\nwindow:\n  start: 2025-01-01T00:00:00Z\n  timezone: UTC\n\nparameters:\n  - name: bins\n    type: integer\n    default: 4\n  - name: binSize\n    type: integer\n    default: 5\n\ngrid:\n  bins: ${bins}\n  binSize: ${binSize}\n  binUnit: minutes\n\ntopology:\n  nodes:\n    - id: OrderService\n      kind: service\n      semantics:\n        arrivals: arrivals\n        served: served\n        errors: errors\n  edges: []\n\nnodes:\n  - id: arrivals\n    kind: const\n    values: [10, 10, 10, 10]\n  - id: served\n    kind: const\n    values: [8, 9, 9, 10]\n  - id: errors\n    kind: const\n    values: [0, 0, 0, 0]\n\noutputs:\n  - series: \"*\"";
}
