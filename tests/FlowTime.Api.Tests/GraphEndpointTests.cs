using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FlowTime.Api.Tests.Infrastructure;
using FlowTime.Contracts.TimeTravel;
using Xunit;

namespace FlowTime.Api.Tests;

public sealed class GraphEndpointTests : IClassFixture<TestWebApplicationFactory>, IDisposable
{
    private const string runId = "run_graph_fixture";
    private const string runIdNoTopology = "run_graph_notopology";
    private readonly string artifactsRoot;
    private readonly HttpClient client;

    public GraphEndpointTests(TestWebApplicationFactory factory)
    {
        artifactsRoot = Path.Combine(Path.GetTempPath(), $"flowtime_graph_fixture_{Guid.NewGuid():N}");
        Directory.CreateDirectory(artifactsRoot);

        CreateGraphRun();

        client = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ArtifactsDirectory", artifactsRoot);
            builder.UseSetting("DataDirectory", artifactsRoot);
        }).CreateClient();
    }

    [Fact]
    public async Task GetGraph_ReturnsTopology()
    {
        var response = await client.GetAsync($"/v1/runs/{runId}/graph");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<GraphResponse>(new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.NotNull(payload);
        Assert.Collection(payload!.Nodes,
            node =>
            {
                Assert.Equal("LoadBalancer", node.Id);
                Assert.Equal("service", node.Kind);
                Assert.Equal("series:incoming", node.Semantics.Arrivals);
                Assert.Equal("series:routed", node.Semantics.Served);
                Assert.Null(node.Ui);
                Assert.NotNull(node.Semantics.Aliases);
                Assert.Equal("Requests Routed", node.Semantics.Aliases!["served"]);
            },
            node =>
            {
                Assert.Equal("Database", node.Id);
                Assert.Equal("service", node.Kind);
                Assert.Equal("series:routed", node.Semantics.Arrivals);
                Assert.Equal("series:served", node.Semantics.Served);
                Assert.NotNull(node.Ui);
                Assert.Equal(160, node.Ui!.X);
                Assert.Equal(48, node.Ui.Y);
            },
            node =>
            {
                Assert.Equal("Analytics", node.Id);
                Assert.Equal("service", node.Kind);
                Assert.Equal("series:analytics_load", node.Semantics.Arrivals);
                Assert.Equal("series:analytics_served", node.Semantics.Served);
                Assert.Equal("series:analytics_errors", node.Semantics.Errors);
                Assert.Null(node.Ui);
            });

        Assert.Collection(payload.Edges,
            edge =>
            {
                Assert.Equal("edge_lb_db", edge.Id);
                Assert.Equal("LoadBalancer:out", edge.From);
                Assert.Equal("Database:in", edge.To);
                Assert.Equal(1.0, edge.Weight);
                Assert.Equal("throughput", edge.EdgeType);
                Assert.Equal("served", edge.Field);
                Assert.Null(edge.Multiplier);
                Assert.Null(edge.Lag);
            },
            edge =>
            {
                Assert.Equal("edge_lb_analytics", edge.Id);
                Assert.Equal("LoadBalancer:out", edge.From);
                Assert.Equal("Analytics:in", edge.To);
                Assert.Equal("effort", edge.EdgeType);
                Assert.Equal("load", edge.Field);
                Assert.Equal(0.5, edge.Multiplier);
                Assert.Equal(1, edge.Lag);
            });

        var sanitized = SanitizeGraphResponse(payload);
        GoldenTestUtils.AssertMatchesGolden("graph-run_graph_fixture.json", sanitized);
    }

    [Fact]
    public async Task GetGraph_MissingRunReturns404()
    {
        var response = await client.GetAsync("/v1/runs/unknown_run/graph");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("Run 'unknown_run' not found", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetGraph_NoTopologyReturns412()
    {
        CreateRunWithoutTopology();
        var response = await client.GetAsync($"/v1/runs/{runIdNoTopology}/graph");
        Assert.Equal((HttpStatusCode)412, response.StatusCode);
    }

    [Fact]
    public async Task GetGraph_FullMode_EmitsProfiledPmfNodes()
    {
        var response = await client.GetAsync($"/v1/runs/{runId}/graph?mode=full");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<GraphResponse>(new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.NotNull(payload);
        var pmfNode = Assert.Single(payload!.Nodes, node => string.Equals(node.Id, "demand_curve", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("pmf", pmfNode.Kind);
        Assert.Equal("series:demand_curve", pmfNode.Semantics.Series);
        Assert.NotNull(pmfNode.Semantics.Distribution);
        Assert.Equal(new[] { 50d, 100d, 150d }, pmfNode.Semantics.Distribution!.Values.ToArray());
        Assert.Equal(new[] { 0.2d, 0.5d, 0.3d }, pmfNode.Semantics.Distribution!.Probabilities.ToArray());
        Assert.NotNull(pmfNode.Semantics.InlineValues);
        Assert.Equal(new[] { 80d, 100d, 120d, 140d }, pmfNode.Semantics.InlineValues!.ToArray());
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
    }

    private void CreateGraphRun()
    {
        var runDir = Path.Combine(artifactsRoot, runId, "model");
        Directory.CreateDirectory(runDir);

        const string yaml = """
schemaVersion: 1

grid:
  bins: 4
  binSize: 15
  binUnit: minutes

topology:
  nodes:
    - id: LoadBalancer
      kind: service
      semantics:
        arrivals: series:incoming
        served: series:routed
        errors: series:errors
        aliases:
          served: "Requests Routed"
          errors: "Dropped Requests"
    - id: Database
      kind: service
      ui:
        x: 160
        y: 48
      semantics:
        arrivals: series:routed
        served: series:served
        errors: series:errors
        capacity: series:capacity
        aliases:
          served: "Transactions Committed"
    - id: Analytics
      kind: service
      semantics:
        arrivals: series:analytics_load
        served: series:analytics_served
        errors: series:analytics_errors
  edges:
    - id: edge_lb_db
      from: LoadBalancer:out
      to: Database:in
      weight: 1
      type: throughput
      measure: served
    - id: edge_lb_analytics
      from: LoadBalancer:out
      to: Analytics:in
      weight: 1
      type: effort
      measure: load
      multiplier: 0.5
      lag: 1

nodes:
  - id: demand_curve
    kind: const
    values: [80, 100, 120, 140]
    pmf:
      values: [50, 100, 150]
      probabilities: [0.2, 0.5, 0.3]
    metadata:
      origin.kind: pmf
      profile.kind: builtin
      profile.name: weekday-office
""";

        File.WriteAllText(Path.Combine(runDir, "model.yaml"), yaml, Encoding.UTF8);
    }

    private void CreateRunWithoutTopology()
    {
        var runDir = Path.Combine(artifactsRoot, runIdNoTopology, "model");
        if (Directory.Exists(runDir))
        {
            return;
        }

        Directory.CreateDirectory(runDir);
        const string yaml = """
schemaVersion: 1

grid:
  bins: 4
  binSize: 15
  binUnit: minutes

nodes:
  - id: demand
    kind: const
    values: [1, 1, 1, 1]
""";
        File.WriteAllText(Path.Combine(runDir, "model.yaml"), yaml, Encoding.UTF8);
    }

    private static JsonNode SanitizeGraphResponse(GraphResponse response)
    {
        var node = JsonSerializer.SerializeToNode(response, GoldenTestUtils.SerializerOptions)
                   ?? throw new InvalidOperationException("Graph response serialization failed.");

        if (node is JsonObject obj && obj["nodes"] is JsonArray nodes)
        {
            foreach (var element in nodes.OfType<JsonObject>())
            {
                RemoveNullProperty(element, "ui");

                if (element.TryGetPropertyValue("semantics", out var semanticsNode) && semanticsNode is JsonObject semantics)
                {
                    RemoveNullProperty(semantics, "queue");
                    RemoveNullProperty(semantics, "capacity");
                    RemoveNullProperty(semantics, "attempts");
                    RemoveNullProperty(semantics, "failures");
                    RemoveNullProperty(semantics, "retryEcho");
                }
            }
        }

        if (node is JsonObject root && root["edges"] is JsonArray edges)
        {
            foreach (var edge in edges.OfType<JsonObject>())
            {
                RemoveNullProperty(edge, "field");
                RemoveNullProperty(edge, "multiplier");
                RemoveNullProperty(edge, "lag");
            }
        }

        return node;

        static void RemoveNullProperty(JsonObject obj, string propertyName)
        {
            if (obj.TryGetPropertyValue(propertyName, out var value) && value is null)
            {
                obj.Remove(propertyName);
            }
        }
    }
}
