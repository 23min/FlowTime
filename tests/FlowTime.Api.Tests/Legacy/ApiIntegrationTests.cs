using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Hosting;

namespace FlowTime.Api.Tests.Legacy;

public class ApiIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
  private readonly WebApplicationFactory<Program> factory;

  public ApiIntegrationTests(WebApplicationFactory<Program> factory)
  {
    // Force TestServer to avoid binding real ports (e.g., 8080) during tests
    this.factory = factory.WithWebHostBuilder(builder =>
    {
      builder.UseEnvironment("Development");
      builder.UseTestServer();
      builder.UseSetting(Microsoft.AspNetCore.Hosting.WebHostDefaults.ServerUrlsKey, "http://127.0.0.1:0");
    });
  }

  [Fact]
  public async Task Run_InvalidExpr_ReturnsBadRequestWithError()
  {
    var client = factory.CreateClient();
    var yaml = @"grid:
  bins: 3
  binMinutes: 60
nodes:
  - id: demand
    kind: const
    values: [10,10,10]
  - id: bad
    kind: expr
  expr: ""demand ** 0.8"""; // unsupported operator
    var content = new StringContent(yaml, Encoding.UTF8, "text/plain");
    var resp = await client.PostAsync("/v1/run", content);
    Assert.Equal(System.Net.HttpStatusCode.BadRequest, resp.StatusCode);
    var error = await resp.Content.ReadFromJsonAsync<Dictionary<string, string>>();
    Assert.NotNull(error);
    Assert.True(error!.ContainsKey("error"), "Expected error payload with 'error' field");
  }

  [Fact]
  public async Task Healthz_ReturnsOk()
  {
    var client = factory.CreateClient();
    var resp = await client.GetAsync("/healthz");
    resp.EnsureSuccessStatusCode();
    var json = await resp.Content.ReadFromJsonAsync<Dictionary<string, string>>();
    Assert.NotNull(json);
    Assert.Equal("ok", json!["status"]);
  }

  [Fact]
  public async Task Run_ReturnsExpectedSeries()
  {
    var client = factory.CreateClient();
    var yaml = @"grid:
  bins: 3
  binMinutes: 60
nodes:
  - id: demand
    kind: const
    values: [10,10,10]
  - id: served
    kind: expr
    expr: ""demand * 0.8""
outputs:
  - series: served
    as: served.csv";
    var content = new StringContent(yaml, Encoding.UTF8, "text/plain");
    var resp = await client.PostAsync("/v1/run", content);
    resp.EnsureSuccessStatusCode();
    var doc = await resp.Content.ReadFromJsonAsync<RunResponse>();
    Assert.NotNull(doc);
    Assert.Equal(3, doc!.grid.bins);
    Assert.Contains("served", doc.series.Keys);
    Assert.Equal(new[] { 8.0, 8.0, 8.0 }, doc.series["served"]);
  }

  [Fact]
  public async Task Graph_ReturnsNodesAndOrder()
  {
    var client = factory.CreateClient();
    var yaml = @"grid:
  bins: 3
  binMinutes: 60
nodes:
  - id: demand
    kind: const
    values: [10,10,10]
  - id: served
    kind: expr
    expr: ""demand * 0.8""";
    var content = new StringContent(yaml, Encoding.UTF8, "text/plain");
    var resp = await client.PostAsync("/v1/graph", content);
    resp.EnsureSuccessStatusCode();
    var doc = await resp.Content.ReadFromJsonAsync<GraphResponse>();
    Assert.NotNull(doc);
    Assert.Contains("demand", doc!.nodes);
    Assert.Contains("served", doc.nodes);
    Assert.Equal(2, doc.order.Length);
  }

  [Fact]
  public async Task Graph_Structure_Invariants_Hold()
  {
    var client = factory.CreateClient();
    var yaml = @"grid:
  bins: 4
  binMinutes: 60
nodes:
  - id: a
    kind: const
    values: [1,1,1,1]
  - id: b
    kind: expr
    expr: ""a * 2""
  - id: c
    kind: expr
    expr: ""b * 2"""; // no outputs section needed for /graph
    var resp = await client.PostAsync("/v1/graph", new StringContent(yaml, Encoding.UTF8, "text/plain"));
    resp.EnsureSuccessStatusCode();
    var g = await resp.Content.ReadFromJsonAsync<GraphResponse>();
    Assert.NotNull(g);
    var ids = g!.edges.Select(e => e.id).ToHashSet();
    // 1. Every edge id must appear in order
    foreach (var id in ids)
      Assert.Contains(id, g.order);
    // 2. Order contains no duplicates
    Assert.Equal(g.order.Length, g.order.Distinct().Count());
    // 3. All inputs exist as node ids
    foreach (var inp in g.edges.SelectMany(e => e.inputs))
      Assert.Contains(inp, ids);
    // 4. In-degree / out-degree consistency
    var inDeg = new Dictionary<string,int>();
    var outDeg = new Dictionary<string,int>();
    foreach (var e in g.edges)
    {
      if (!outDeg.ContainsKey(e.id)) outDeg[e.id]=0;
      foreach (var inp in e.inputs)
      {
        inDeg[inp] = inDeg.TryGetValue(inp, out var v)? v+1:1;
        outDeg[e.id] = outDeg[e.id] + 1;
      }
      if (!inDeg.ContainsKey(e.id)) inDeg[e.id]=inDeg.GetValueOrDefault(e.id,0);
    }
    // At least one source (in-degree 0) and one sink (out-degree 0)
    Assert.Contains(inDeg, kv => kv.Value == 0);
    Assert.Contains(outDeg, kv => kv.Value == 0);
  }

  public sealed class RunResponse
  {
    public required Grid grid { get; init; }
    public required string[] order { get; init; }
    public required Dictionary<string, double[]> series { get; init; }
  }
  public sealed class Grid { public int bins { get; init; } public int binMinutes { get; init; } }

  public sealed class GraphResponse
  {
    public required string[] nodes { get; init; }
    public required string[] order { get; init; }
    public required Edge[] edges { get; init; }
  }
  public sealed class Edge { public required string id { get; init; } public required string[] inputs { get; init; } }
}
