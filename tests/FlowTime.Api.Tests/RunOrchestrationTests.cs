using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using FlowTime.Api.Tests.Infrastructure;
using FlowTime.Generator;
using FlowTime.Generator.Models;
using FlowTime.Tests.Support;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using FlowTime.Sim.Core.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using FlowTime.Sim.Core.Templates;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using FlowTime.Generator.Orchestration;
using FlowTime.Contracts.TimeTravel;
using FlowTime.Contracts.Services;

namespace FlowTime.Api.Tests;

public class RunOrchestrationTests : IClassFixture<TestWebApplicationFactory>, IDisposable
{
    private const string templateId = "test-order";
    private const string simulationTemplateId = "sim-order";
    private const string simulationTemplateWithRngId = "sim-order-rng";
    private const string networkReliabilityTemplateId = "network-reliability";

    private readonly TestWebApplicationFactory factory;
    private readonly WebApplicationFactory<Program> serverFactory;
    private readonly HttpClient client;
    private readonly string dataRoot;
    private readonly string templateDirectory;
    private readonly string telemetryRoot;

    public RunOrchestrationTests(TestWebApplicationFactory factory)
    {
        this.factory = factory;
        dataRoot = factory.TestDataDirectory;

        templateDirectory = Path.Combine(dataRoot, "templates");
        Directory.CreateDirectory(templateDirectory);
        File.WriteAllText(Path.Combine(templateDirectory, $"{templateId}.yaml"), TestTemplateYaml);
        File.WriteAllText(Path.Combine(templateDirectory, $"{simulationTemplateId}.yaml"), SimulationTemplateYaml);
        File.WriteAllText(Path.Combine(templateDirectory, $"{simulationTemplateWithRngId}.yaml"), SimulationTemplateWithRngYaml);
        telemetryRoot = Path.Combine(dataRoot, "telemetry-root");
        Directory.CreateDirectory(telemetryRoot);
        var networkTemplateSource = ResolveTemplatePath($"{networkReliabilityTemplateId}.yaml");
        File.Copy(networkTemplateSource, Path.Combine(templateDirectory, $"{networkReliabilityTemplateId}.yaml"), overwrite: true);

        serverFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((context, config) =>
            {
                var settings = new Dictionary<string, string?>
                {
                    ["TemplatesDirectory"] = templateDirectory,
                    ["ArtifactsDirectory"] = dataRoot,
                    ["TelemetryRoot"] = telemetryRoot
                };
                config.AddInMemoryCollection(settings!);
            });
        });

        client = serverFactory.CreateClient();
    }

    [Fact]
    public async Task CreateSimulationRun_WithRngSeed_EchoesSeedInResponse()
    {
        var requestPayload = new
        {
            templateId = simulationTemplateWithRngId,
            mode = "simulation",
            rng = new
            {
                kind = "pcg32",
                seed = 777
            }
        };

        var response = await client.PostAsJsonAsync("/v1/runs", requestPayload);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var payload = JsonNode.Parse(body) ?? throw new InvalidOperationException("Response was not valid JSON.");
        var metadata = payload["metadata"] ?? throw new InvalidOperationException("Metadata missing from response.");
        Assert.Equal(777, metadata["rng"]?["seed"]?.GetValue<int>());
    }

    [Fact]
    public async Task CreateSimulationRun_WithTemplateRngAndNoSeed_ReturnsBadRequest()
    {
        var requestPayload = new
        {
            templateId = simulationTemplateWithRngId,
            mode = "simulation"
        };

        var response = await client.PostAsJsonAsync("/v1/runs", requestPayload);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("rng.seed", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateSimulationRun_ReturnsMetadataAndCreatesRun()
    {
        var requestPayload = new
        {
            templateId = simulationTemplateId,
            mode = "simulation",
            parameters = new
            {
                bins = 4,
                binSize = 5
            },
            options = new
            {
                deterministicRunId = true
            }
        };

        var response = await client.PostAsJsonAsync("/v1/runs", requestPayload);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var payload = JsonNode.Parse(body) ?? throw new InvalidOperationException("Response was not valid JSON.");
        Assert.False(payload["isDryRun"]?.GetValue<bool>() ?? true);
        var metadata = payload["metadata"] ?? throw new InvalidOperationException("Metadata missing from simulation response.");
        Assert.Equal("simulation", metadata["mode"]?.GetValue<string>());
        Assert.True(payload["canReplay"]?.GetValue<bool>() ?? false);

        var runId = metadata["runId"]?.GetValue<string>() ?? throw new InvalidOperationException("runId missing");
        var runPath = Path.Combine(dataRoot, runId);
        Assert.True(Directory.Exists(runPath));
        Assert.True(File.Exists(Path.Combine(runPath, "model", "telemetry", "telemetry-manifest.json")), "Telemetry manifest should be written for simulation runs.");

        var detail = await client.GetFromJsonAsync<JsonNode>($"/v1/runs/{runId}");
        Assert.NotNull(detail);
        Assert.Equal("simulation", detail!["metadata"]?["mode"]?.GetValue<string>());
        Assert.True(detail["canReplay"]?.GetValue<bool>() ?? false);
        Assert.Equal(0, detail["warnings"]?.AsArray()?.Count ?? 0);
    }

    [Fact]
    public async Task CreateSimulationRun_NetworkReliabilityDefaults_Succeeds()
    {
        var requestPayload = new
        {
            templateId = networkReliabilityTemplateId,
            mode = "simulation",
            rng = new
            {
                kind = "pcg32",
                seed = 42
            },
            parameters = new
            {
                rngSeed = 42
            }
        };

        var response = await client.PostAsJsonAsync("/v1/runs", requestPayload);
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.Created, body);
    }

    [Fact]
    public async Task CreateSimulationRun_NetworkReliabilityArrayOverride_BindsValues()
    {
        var baseLoadOverride = Enumerable.Range(0, 12).Select(i => 80d + i * 5d).ToArray();

        var requestPayload = new
        {
            templateId = networkReliabilityTemplateId,
            mode = "simulation",
            rng = new
            {
                kind = "pcg32",
                seed = 42
            },
            parameters = new
            {
                baseLoad = baseLoadOverride,
                rngSeed = 42
            },
            options = new
            {
                deterministicRunId = true
            }
        };

        var response = await client.PostAsJsonAsync("/v1/runs", requestPayload);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var payload = JsonNode.Parse(body) ?? throw new InvalidOperationException("Response was not valid JSON.");
        var runId = payload["metadata"]?["runId"]?.GetValue<string>() ?? throw new InvalidOperationException("runId missing from response.");

        var modelPath = Path.Combine(dataRoot, runId, "model", "model.yaml");
        Assert.True(File.Exists(modelPath), $"Expected generated model at {modelPath}");

        var modelContent = await File.ReadAllTextAsync(modelPath);
        var inlineSequence = $"[{string.Join(", ", baseLoadOverride.Select(v => v.ToString(System.Globalization.CultureInfo.InvariantCulture)))}]";

        if (!modelContent.Contains(inlineSequence, StringComparison.Ordinal))
        {
            foreach (var value in baseLoadOverride)
            {
                Assert.Contains($"- {value.ToString(System.Globalization.CultureInfo.InvariantCulture)}", modelContent);
            }
        }
    }

    [Fact]
    public async Task CreateSimulationRun_NetworkReliabilityArrayLengthMismatch_ReturnsBadRequest()
    {
        var requestPayload = new
        {
            templateId = networkReliabilityTemplateId,
            mode = "simulation",
            rng = new
            {
                kind = "pcg32",
                seed = 42
            },
            parameters = new
            {
                baseLoad = new[] { 100, 110, 120, 130, 140, 150, 160, 170, 180, 190 },
                rngSeed = 42
            }
        };

        var response = await client.PostAsJsonAsync("/v1/runs", requestPayload);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("baseLoad", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("length", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TemplateService_NetworkReliability_GeneratesModel()
    {
        await using var scope = serverFactory.Services.CreateAsyncScope();
        var templateService = scope.ServiceProvider.GetRequiredService<ITemplateService>();

        var model = await templateService.GenerateEngineModelAsync(networkReliabilityTemplateId, new Dictionary<string, object>());

        Assert.False(string.IsNullOrWhiteSpace(model));

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        var artifact = deserializer.Deserialize<SimModelArtifact>(model);
        Assert.NotNull(artifact);
        Assert.Null(artifact.Nodes.First(n => n.Id == "network_reliability").Values);
        Assert.Null(artifact.Nodes.First(n => n.Id == "network_requests").Values);

        var definition = ModelService.ParseAndConvert(model);
        Assert.NotNull(definition);
    }

    [Fact]
    public async Task RunOrchestrationService_NetworkReliabilitySimulation_Succeeds()
    {
        await using var scope = serverFactory.Services.CreateAsyncScope();
        var orchestration = scope.ServiceProvider.GetRequiredService<RunOrchestrationService>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var runsRoot = Program.ServiceHelpers.RunsRoot(configuration);

        var request = new RunOrchestrationRequest
        {
            TemplateId = networkReliabilityTemplateId,
            Mode = "simulation",
            OutputRoot = runsRoot,
            Parameters = new Dictionary<string, object?>
            {
                ["rngSeed"] = 42
            },
            Rng = new RunRngOptions
            {
                Kind = "pcg32",
                Seed = 42
            },
            DeterministicRunId = true
        };

        var outcome = await orchestration.CreateRunAsync(request);

        Assert.False(outcome.IsDryRun);
        Assert.NotNull(outcome.Result);
    }

    [Fact]
    public async Task CreateSimulationRun_MissingWindow_ReturnsBadRequest()
    {
        var invalidTemplatePath = Path.Combine(templateDirectory, "sim-invalid.yaml");
        await File.WriteAllTextAsync(invalidTemplatePath, SimulationTemplateMissingWindow);

        var requestPayload = new
        {
            templateId = "sim-invalid",
            mode = "simulation"
        };

        var response = await client.PostAsJsonAsync("/v1/runs", requestPayload);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("window", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateTelemetryRun_ReturnsMetadataAndCreatesRun()
    {
        var captureDir = await CreateTelemetryCaptureAsync(asRelative: true);

        var requestPayload = new
        {
            templateId,
            mode = "telemetry",
            telemetry = new
            {
                captureDirectory = captureDir,
                bindings = new Dictionary<string, string>
                {
                    ["telemetryArrivals"] = "OrderService_arrivals.csv",
                    ["telemetryServed"] = "OrderService_served.csv"
                }
            },
            parameters = new
            {
                bins = 4,
                binSize = 5
            },
            options = new
            {
                deterministicRunId = true
            }
        };

        var response = await client.PostAsJsonAsync("/v1/runs", requestPayload);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var payload = JsonNode.Parse(body) ?? throw new InvalidOperationException("Response was not valid JSON.");
        Assert.False(payload["isDryRun"]?.GetValue<bool>() ?? true);
        Assert.True(payload["canReplay"]?.GetValue<bool>() ?? false);
        var metadata = payload["metadata"] ?? throw new InvalidOperationException("Metadata is missing from response.");
        var runId = metadata["runId"]?.GetValue<string>();
        Assert.False(string.IsNullOrWhiteSpace(runId));

        var runPath = Path.Combine(dataRoot, runId!);
        Assert.True(Directory.Exists(runPath), $"Expected run directory to exist at {runPath}");
        Assert.True(File.Exists(Path.Combine(runPath, "model", "model.yaml")), "Expected model.yaml to be present.");

        var detail = await client.GetFromJsonAsync<JsonNode>($"/v1/runs/{runId}");
        Assert.NotNull(detail);
        Assert.Equal(runId, detail!["metadata"]?["runId"]?.GetValue<string>());
        Assert.Equal("telemetry", detail["metadata"]?["mode"]?.GetValue<string>());
        Assert.True(detail["metadata"]?["telemetrySourcesResolved"]?.GetValue<bool?>());
        Assert.True(detail["canReplay"]?.GetValue<bool>() ?? false);

        var listing = await client.GetFromJsonAsync<JsonNode>("/v1/runs");
        Assert.NotNull(listing);
        var items = listing!["items"]?.AsArray() ?? new JsonArray();
        Assert.Contains(items, node => string.Equals(node?["runId"]?.GetValue<string>(), runId, StringComparison.OrdinalIgnoreCase));
        Assert.Equal(1, listing["page"]?.GetValue<int>());
        Assert.Equal(50, listing["pageSize"]?.GetValue<int>());
        Assert.True((listing["totalCount"]?.GetValue<int>() ?? 0) >= 1);
    }

    [Fact]
    public async Task CreateTelemetryRunDryRun_ReturnsPlan()
    {
        var captureDir = await CreateTelemetryCaptureAsync(asRelative: true);

        var requestPayload = new
        {
            templateId,
            mode = "telemetry",
            telemetry = new
            {
                captureDirectory = captureDir,
                bindings = new Dictionary<string, string>
                {
                    ["telemetryArrivals"] = "OrderService_arrivals.csv",
                    ["telemetryServed"] = "OrderService_served.csv"
                }
            },
            options = new
            {
                dryRun = true
            }
        };

        var response = await client.PostAsJsonAsync("/v1/runs", requestPayload);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = JsonNode.Parse(body) ?? throw new InvalidOperationException("Response was not valid JSON.");
        Assert.True(payload["isDryRun"]?.GetValue<bool>() ?? false);
        var plan = payload["plan"] ?? throw new InvalidOperationException("Plan is missing from dry-run response.");
        Assert.Equal(templateId, plan["templateId"]?.GetValue<string>());
        Assert.Equal("telemetry", plan["mode"]?.GetValue<string>());
        var files = plan["files"]?.AsArray() ?? new JsonArray();
        Assert.NotEmpty(files);
        Assert.False(payload["canReplay"]?.GetValue<bool>() ?? true);

        var runsRoot = Path.Combine(dataRoot, "runs");
        Assert.False(Directory.Exists(runsRoot));
    }

    [Fact]
    public async Task CreateTelemetryRun_WithAbsoluteCaptureDirectoryStillSupported()
    {
        var captureDir = await CreateTelemetryCaptureAsync();

        var requestPayload = new
        {
            templateId,
            mode = "telemetry",
            telemetry = new
            {
                captureDirectory = captureDir,
                bindings = new Dictionary<string, string>
                {
                    ["telemetryArrivals"] = "OrderService_arrivals.csv",
                    ["telemetryServed"] = "OrderService_served.csv"
                }
            },
            options = new
            {
                deterministicRunId = true
            }
        };

        var response = await client.PostAsJsonAsync("/v1/runs", requestPayload);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Contains("\"metadata\"", body);
    }

    [Fact]
    public async Task ListRuns_AppliesFiltersAndPagination()
    {
        var captureDir = await CreateTelemetryCaptureAsync(asRelative: true);

        async Task<string> CreateAsync()
        {
            var requestPayload = new
            {
                templateId,
                mode = "telemetry",
                telemetry = new
                {
                    captureDirectory = captureDir,
                    bindings = new Dictionary<string, string>
                    {
                        ["telemetryArrivals"] = "OrderService_arrivals.csv",
                        ["telemetryServed"] = "OrderService_served.csv"
                    }
                }
            };

            var response = await client.PostAsJsonAsync("/v1/runs", requestPayload);
            var body = await response.Content.ReadAsStringAsync();
            Assert.True(response.IsSuccessStatusCode, $"Run creation failed. Status={response.StatusCode}; Body={body}");
            var payload = JsonNode.Parse(body) ?? throw new InvalidOperationException("Response was not valid JSON.");
            return payload["metadata"]?["runId"]?.GetValue<string>() ?? throw new InvalidOperationException("runId missing");
        }

        var runIdOne = await CreateAsync();
        await Task.Delay(10); // ensure ordering by creation timestamp
        var runIdTwo = await CreateAsync();

        var allRuns = await client.GetFromJsonAsync<JsonNode>("/v1/runs?mode=telemetry&templateId=test-order&hasWarnings=false&page=1&pageSize=200");
        Assert.NotNull(allRuns);
        var runIds = allRuns!["items"]?.AsArray()
            ?.Select(node => node?["runId"]?.GetValue<string>())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Assert.Contains(runIdOne, runIds);
        Assert.Contains(runIdTwo, runIds);

        var firstPage = await client.GetFromJsonAsync<JsonNode>("/v1/runs?mode=telemetry&templateId=test-order&hasWarnings=false&page=1&pageSize=1");
        Assert.NotNull(firstPage);
        Assert.Equal(1, firstPage!["page"]?.GetValue<int>());
        Assert.Equal(1, firstPage["pageSize"]?.GetValue<int>());
        Assert.Single(firstPage["items"]?.AsArray() ?? new JsonArray());

        var secondPage = await client.GetFromJsonAsync<JsonNode>("/v1/runs?mode=telemetry&hasWarnings=false&page=2&pageSize=1");
        Assert.NotNull(secondPage);
        Assert.Single(secondPage!["items"]?.AsArray() ?? new JsonArray());

        var empty = await client.GetFromJsonAsync<JsonNode>("/v1/runs?templateId=does-not-exist");
        Assert.NotNull(empty);
        Assert.Equal(0, empty!["totalCount"]?.GetValue<int>());
        Assert.Empty(empty["items"]?.AsArray() ?? new JsonArray());
    }

    [Fact]
    public async Task CreateSimulationRun_MissingTemplate_ReturnsNotFound()
    {
        var requestPayload = new
        {
            templateId = "does-not-exist",
            mode = "simulation",
            parameters = new { bins = 4 }
        };

        var response = await client.PostAsJsonAsync("/v1/runs", requestPayload);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var json = JsonNode.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Template not found: does-not-exist", json? ["error"]?.GetValue<string>());
    }

    private async Task<string> CreateTelemetryCaptureAsync(bool asRelative = false)
    {
        var sourceRoot = Path.Combine(dataRoot, $"source_{Guid.NewGuid():N}");
        Directory.CreateDirectory(sourceRoot);

        var sourceRun = TelemetryRunFactory.CreateRunArtifacts(sourceRoot, "run_capture", includeTopology: true);
        var captureKey = $"capture_{Guid.NewGuid():N}";
        var captureDir = Path.Combine(telemetryRoot, captureKey);
        Directory.CreateDirectory(captureDir);

        var capture = new TelemetryCapture();
        await capture.ExecuteAsync(new TelemetryCaptureOptions
        {
            RunDirectory = sourceRun,
            OutputDirectory = captureDir
        });

        return asRelative ? captureKey : captureDir;
    }

    private static string ResolveTemplatePath(string templateFileName)
    {
        var directory = Directory.GetCurrentDirectory();
        while (!string.IsNullOrEmpty(directory))
        {
            var solutionPath = Path.Combine(directory, "FlowTime.sln");
            if (File.Exists(solutionPath))
            {
                var templatePath = Path.Combine(directory, "templates", templateFileName);
                if (File.Exists(templatePath))
                {
                    return templatePath;
                }

                throw new FileNotFoundException($"Template file '{templateFileName}' was not found under templates directory.");
            }

            directory = Directory.GetParent(directory)?.FullName ?? string.Empty;
        }

        throw new DirectoryNotFoundException("Unable to locate templates directory relative to solution root.");
    }

    public void Dispose()
    {
        client.Dispose();
        serverFactory.Dispose();
    }

    private const string TestTemplateYaml = """
schemaVersion: 1
generator: flowtime-sim
metadata:
  id: test-order
  title: Test Order System
  description: Test telemetry playback template
  version: 1.0.0
window:
  start: 2025-01-01T00:00:00Z
  timezone: UTC

parameters:
  - name: telemetryArrivals
    type: string
    default: ""
  - name: telemetryServed
    type: string
    default: ""
  - name: bins
    type: integer
    default: 4
  - name: binSize
    type: integer
    default: 5

grid:
  bins: ${bins}
  binSize: ${binSize}
  binUnit: minutes

topology:
  nodes:
    - id: OrderService
      kind: service
      semantics:
        arrivals: "order_arrivals"
        served: "order_served"
        errors: "order_errors"
  edges: []

nodes:
  - id: order_arrivals
    kind: const
    source: ${telemetryArrivals}
  - id: order_served
    kind: const
    source: ${telemetryServed}
  - id: order_errors
    kind: const
    values: [0, 0, 0, 0]

outputs:
  - series: order_arrivals
    as: OrderService_arrivals.csv
  - series: order_served
    as: OrderService_served.csv
  - series: order_errors
    as: OrderService_errors.csv
""";

    private const string SimulationTemplateYaml = """
schemaVersion: 1
generator: flowtime-sim
metadata:
  id: sim-order
  title: Simulation Order Template
  description: Template for simulation-mode orchestration tests
  version: 1.0.0
window:
  start: 2025-01-01T00:00:00Z
  timezone: UTC

parameters:
  - name: bins
    type: integer
    default: 4
  - name: binSize
    type: integer
    default: 5

grid:
  bins: ${bins}
  binSize: ${binSize}
  binUnit: minutes

topology:
  nodes:
    - id: OrderService
      kind: service
      semantics:
        arrivals: arrivals
        served: served
        errors: errors
  edges: []

nodes:
  - id: arrivals
    kind: const
    values: [10, 12, 14, 16]
  - id: served
    kind: const
    values: [8, 11, 13, 15]
  - id: errors
    kind: const
    values: [1, 0, 0, 0]

outputs:
  - series: "*"
""";

    private const string SimulationTemplateWithRngYaml = """
schemaVersion: 1
generator: flowtime-sim
metadata:
  id: sim-order-rng
  title: Simulation Order Template (RNG)
  description: Template for RNG seeding tests
  version: 1.0.0
rng:
  kind: pcg32
  seed: "123"
window:
  start: 2025-01-01T00:00:00Z
  timezone: UTC

parameters:
  - name: bins
    type: integer
    default: 4
  - name: binSize
    type: integer
    default: 5

grid:
  bins: ${bins}
  binSize: ${binSize}
  binUnit: minutes

topology:
  nodes:
    - id: OrderService
      kind: service
      semantics:
        arrivals: arrivals
        served: served
        errors: errors
  edges: []

nodes:
  - id: arrivals
    kind: const
    values: [10, 12, 14, 16]
  - id: served
    kind: const
    values: [8, 11, 13, 15]
  - id: errors
    kind: const
    values: [1, 0, 0, 0]

outputs:
  - series: "*"
""";

    private const string SimulationTemplateMissingWindow = """
schemaVersion: 1
generator: flowtime-sim
metadata:
  id: sim-invalid
  title: Simulation Invalid Template
  description: Missing window for validation failure
  version: 1.0.0

grid:
  bins: 4
  binSize: 5
  binUnit: minutes

topology:
  nodes:
    - id: OrderService
      kind: service
      semantics:
        arrivals: arrivals
        served: served
        errors: errors
  edges: []

nodes:
  - id: arrivals
    kind: const
    values: [10, 12, 14, 16]
  - id: served
    kind: const
    values: [8, 11, 13, 15]
  - id: errors
    kind: const
    values: [1, 0, 0, 0]
""";
}
