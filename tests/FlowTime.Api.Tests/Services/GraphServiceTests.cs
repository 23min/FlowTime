using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using FlowTime.API.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FlowTime.Api.Tests.Services;

public sealed class GraphServiceTests : IDisposable
{
    private readonly string artifactsRoot;
    private readonly IConfiguration configuration;
    private readonly GraphService service;
    private readonly string? originalDataDir;

    public GraphServiceTests()
    {
        artifactsRoot = Path.Combine(Path.GetTempPath(), $"graph_service_tests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(artifactsRoot);

        originalDataDir = Environment.GetEnvironmentVariable("FLOWTIME_DATA_DIR");
        Environment.SetEnvironmentVariable("FLOWTIME_DATA_DIR", null);

        configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new[]
            {
                new KeyValuePair<string, string?>("ArtifactsDirectory", artifactsRoot)
            })
            .Build();

        service = new GraphService(configuration, NullLogger<GraphService>.Instance);
    }

    [Fact]
    public async Task GetGraphAsync_ReturnsNodesAndEdgesFromModel()
    {
        const string runId = "run_graph_unit";
        CreateRun(runId, """
schemaVersion: 1

grid:
  bins: 4
  binSize: 5
  binUnit: minutes

topology:
  nodes:
    - id: ServiceA
      kind: service
      ui:
        x: 10
        y: 20
      semantics:
        arrivals: series:arrivals
        served: series:served
        errors: series:errors
        queueDepth: series:queue
        capacity: series:capacity
        aliases:
          served: "Cases Resolved"
    - id: QueueB
      kind: queue
      semantics:
        arrivals: series:q_arrivals
        served: series:q_served
        errors: series:q_errors
  edges:
    - id: edge1
      from: ServiceA:out
      to: QueueB:in
      weight: 1.5
""");

        var response = await service.GetGraphAsync(runId);

        Assert.Equal(2, response.Nodes.Count);
        var serviceNode = Assert.Single(response.Nodes, n => n.Id == "ServiceA");
        Assert.Equal("service", serviceNode.Kind);
        Assert.Equal("series:arrivals", serviceNode.Semantics.Arrivals);
        Assert.Equal("series:served", serviceNode.Semantics.Served);
        Assert.Equal("series:errors", serviceNode.Semantics.Errors);
        Assert.Equal("series:queue", serviceNode.Semantics.Queue);
        Assert.Equal("series:capacity", serviceNode.Semantics.Capacity);
        Assert.NotNull(serviceNode.Semantics.Aliases);
        Assert.Equal("Cases Resolved", serviceNode.Semantics.Aliases!["served"]);
        Assert.NotNull(serviceNode.Ui);
        Assert.Equal(10, serviceNode.Ui!.X);
        Assert.Equal(20, serviceNode.Ui.Y);

        var queueNode = Assert.Single(response.Nodes, n => n.Id == "QueueB");
        Assert.Equal("queue", queueNode.Kind);
        Assert.Null(queueNode.Ui);
        Assert.Equal("series:q_arrivals", queueNode.Semantics.Arrivals);
        Assert.Equal("series:q_served", queueNode.Semantics.Served);
        Assert.Equal("series:q_errors", queueNode.Semantics.Errors);
        Assert.Null(queueNode.Semantics.Queue);

        var edge = Assert.Single(response.Edges);
        Assert.Equal("edge1", edge.Id);
        Assert.Equal("ServiceA:out", edge.From);
        Assert.Equal("QueueB:in", edge.To);
        Assert.Equal(1.5, edge.Weight);
    }

    [Fact]
    public async Task GetGraphAsync_IncludesRetrySemanticsAndEdgeTypes()
    {
        const string runId = "run_graph_retry";
        CreateRun(runId, """
schemaVersion: 1

grid:
  bins: 4
  binSize: 5
  binUnit: minutes

topology:
  nodes:
    - id: ServiceA
      kind: service
      semantics:
        arrivals: series:arrivals
        served: series:served
        errors: series:errors
        attempts: series:attempts
        failures: series:failures
        retryEcho: series:retry_echo
    - id: QueueB
      kind: queue
      semantics:
        arrivals: series:q_arrivals
        served: series:q_served
        errors: series:q_errors
    - id: DatabaseC
      kind: service
      semantics:
        arrivals: series:db_arrivals
        served: series:db_served
        errors: series:db_errors
  edges:
    - id: edge-throughput
      from: ServiceA:served
      to: QueueB:arrivals
      type: throughput
      measure: served
      lag: 1
    - id: edge-effort
      from: ServiceA:attempts
      to: DatabaseC:arrivals
      type: effort
      measure: attempts
      multiplier: 2.5
""");

        var response = await service.GetGraphAsync(runId);

        var serviceNode = Assert.Single(response.Nodes, n => n.Id == "ServiceA");
        Assert.Equal("series:attempts", serviceNode.Semantics.Attempts);
        Assert.Equal("series:failures", serviceNode.Semantics.Failures);
        Assert.Equal("series:retry_echo", serviceNode.Semantics.RetryEcho);

        var throughputEdge = Assert.Single(response.Edges, e => e.Id == "edge-throughput");
        Assert.Equal("throughput", throughputEdge.EdgeType);
        Assert.Equal("served", throughputEdge.Field);
        Assert.Equal(1, throughputEdge.Lag);

        var effortEdge = Assert.Single(response.Edges, e => e.Id == "edge-effort");
        Assert.Equal("effort", effortEdge.EdgeType);
        Assert.Equal("attempts", effortEdge.Field);
        Assert.Equal(2.5, effortEdge.Multiplier);
    }

    [Fact]
    public async Task GetGraphAsync_MissingRunThrowsNotFound()
    {
        var ex = await Assert.ThrowsAsync<GraphQueryException>(() => service.GetGraphAsync("missing-run"));
        Assert.Equal(404, ex.StatusCode);
    }

    [Fact]
    public async Task GetGraphAsync_NoTopologyThrowsPreconditionFailed()
    {
        const string runId = "run_graph_no_topology";
        CreateRun(runId, """
schemaVersion: 1

grid:
  bins: 4
  binSize: 5
  binUnit: minutes
""");

        var ex = await Assert.ThrowsAsync<GraphQueryException>(() => service.GetGraphAsync(runId));
        Assert.Equal(412, ex.StatusCode);
    }

    private void CreateRun(string runId, string modelYaml)
    {
        var runDir = Path.Combine(artifactsRoot, runId, "model");
        Directory.CreateDirectory(runDir);
        File.WriteAllText(Path.Combine(runDir, "model.yaml"), modelYaml, Encoding.UTF8);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(artifactsRoot))
            {
                Directory.Delete(artifactsRoot, recursive: true);
            }
        }
        catch
        {
            // best effort cleanup
        }

        Environment.SetEnvironmentVariable("FLOWTIME_DATA_DIR", originalDataDir);
    }
}
