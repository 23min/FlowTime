using FlowTime.Core.TimeTravel;

namespace FlowTime.Core.Tests.TimeTravel;

public sealed class RunManifestReaderTests : IDisposable
{
    private readonly string rootDirectory;

    public RunManifestReaderTests()
    {
        rootDirectory = Path.Combine(Path.GetTempPath(), $"ft_manifest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(rootDirectory);
    }

    [Fact]
    public async Task ReadAsync_ReturnsMetadataAndTelemetrySources()
    {
        var modelYaml = """
schemaVersion: 1
mode: simulation
metadata:
  id: order-system
  title: Order System
  version: 1.1.0
window:
  start: 2025-10-07T00:00:00Z
  timezone: UTC
grid:
  bins: 288
  binSize: 5
  binUnit: minutes
topology:
  nodes:
    - id: OrderService
      kind: service
      semantics:
        arrivals: service_arrivals
        served: service_served
        capacity: service_capacity
        errors: null
        queue: service_queue
  edges: []
nodes:
  - id: service_arrivals
    kind: const
    source: file://telemetry/order-service/arrivals.csv
  - id: service_served
    kind: const
    source: file://telemetry/order-service/served.csv
  - id: service_capacity
    kind: const
    values:
      - 150
      - 150
  - id: service_queue
    kind: const
    source: file://telemetry/order-service/queue.csv
provenance:
  source: flowtime-sim
  templateId: order-system
  templateVersion: "1.1.0"
  mode: simulation
  schemaVersion: 1
  modelId: order-system-sim
  parameters:
    binSize: 5
""";

        var modelDir = Path.Combine(rootDirectory, "schema-1", "mode-simulation", "sha256-abc123");
        Directory.CreateDirectory(modelDir);
        var modelPath = Path.Combine(modelDir, "model.yaml");
        await File.WriteAllTextAsync(modelPath, modelYaml);

        var metadataJson = """
{
  "templateId": "order-system",
  "templateTitle": "Order System",
  "templateVersion": "1.1.0",
  "schemaVersion": 1,
  "mode": "simulation",
  "modelHash": "sha256:abc12345",
  "hasTelemetrySources": true,
  "generatedAtUtc": "2025-10-07T00:00:00Z"
}
""";
        await File.WriteAllTextAsync(Path.Combine(modelDir, "metadata.json"), metadataJson);

        var reader = new RunManifestReader();
        var result = await reader.ReadAsync(modelDir, CancellationToken.None);

        Assert.Equal("order-system", result.TemplateId);
        Assert.Equal("1.1.0", result.TemplateVersion);
        Assert.Equal("Order System", result.TemplateTitle);
        Assert.Equal("simulation", result.Mode);

        Assert.Equal("time-travel/v1", result.Schema.Id);
        Assert.Equal("1", result.Schema.Version);
        Assert.Equal("sha256:abc12345", result.Schema.Hash);

        Assert.Equal("sha256:abc12345", result.ProvenanceHash);
        Assert.Equal(modelPath, result.Storage.ModelPath);

        Assert.Contains("file://telemetry/order-service/arrivals.csv", result.TelemetrySources);
        Assert.Contains("file://telemetry/order-service/served.csv", result.TelemetrySources);
        Assert.Contains("file://telemetry/order-service/queue.csv", result.TelemetrySources);

        Assert.Equal("file://telemetry/order-service/arrivals.csv", result.NodeSources["service_arrivals"]);
        Assert.Equal("file://telemetry/order-service/served.csv", result.NodeSources["service_served"]);
        Assert.Equal("file://telemetry/order-service/queue.csv", result.NodeSources["service_queue"]);
    }

    [Fact]
    public async Task ReadAsync_WithoutMetadataJson_Throws()
    {
        var modelDir = Path.Combine(rootDirectory, "schema-1", "mode-simulation", "missing-metadata");
        Directory.CreateDirectory(modelDir);
        await File.WriteAllTextAsync(Path.Combine(modelDir, "model.yaml"), "schemaVersion: 1\nmetadata: { id: test }\n");

        var reader = new RunManifestReader();
        await Assert.ThrowsAsync<InvalidOperationException>(() => reader.ReadAsync(modelDir, CancellationToken.None));
    }

    [Fact]
    public async Task ReadAsync_DetectsTelemetryUrisFromTopologySemantics()
    {
        var modelDir = Path.Combine(rootDirectory, "schema-1", "mode-telemetry", "semantics-only");
        Directory.CreateDirectory(modelDir);

        var modelYaml = """
schemaVersion: 1
topology:
  nodes:
    - id: Service
      semantics:
        arrivals: "file:arrivals.csv"
        served: "file:served.csv"
        errors: null
""";
        await File.WriteAllTextAsync(Path.Combine(modelDir, "model.yaml"), modelYaml);

        var metadataJson = """
{
  "templateId": "fixture",
  "templateTitle": "Fixture",
  "templateVersion": "1.0.0",
  "schemaVersion": 1,
  "mode": "telemetry",
  "modelHash": "sha256:fixture"
}
""";
        await File.WriteAllTextAsync(Path.Combine(modelDir, "metadata.json"), metadataJson);

        var reader = new RunManifestReader();
        var result = await reader.ReadAsync(modelDir, CancellationToken.None);

        Assert.Contains("file:arrivals.csv", result.TelemetrySources);
        Assert.Contains("file:served.csv", result.TelemetrySources);
        Assert.True(result.TelemetrySources.Count >= 2);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(rootDirectory))
            {
                Directory.Delete(rootDirectory, recursive: true);
            }
        }
        catch
        {
            // swallow cleanup exceptions; tests create unique directories
        }
    }
}
