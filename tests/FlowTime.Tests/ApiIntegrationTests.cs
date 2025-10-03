using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Hosting;

public class ApiIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
  private readonly WebApplicationFactory<Program> factory;

  public ApiIntegrationTests(WebApplicationFactory<Program> factory)
  {
    this.factory = factory.WithWebHostBuilder(builder =>
    {
      builder.UseEnvironment("Development");
      builder.UseTestServer();
      builder.UseSetting(Microsoft.AspNetCore.Hosting.WebHostDefaults.ServerUrlsKey, "http://127.0.0.1:0");
    });
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
    var yaml = @"schemaVersion: 1
grid:
  bins: 3
  binSize: 60
  binUnit: minutes
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
    var resp = await client.PostAsync("/run", content);
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
    var yaml = @"schemaVersion: 1
grid:
  bins: 3
  binSize: 60
  binUnit: minutes
nodes:
  - id: demand
    kind: const
    values: [10,10,10]
  - id: served
    kind: expr
    expr: ""demand * 0.8""";
    var content = new StringContent(yaml, Encoding.UTF8, "text/plain");
    var resp = await client.PostAsync("/graph", content);
    resp.EnsureSuccessStatusCode();
    var doc = await resp.Content.ReadFromJsonAsync<GraphResponse>();
    Assert.NotNull(doc);
    Assert.Contains("demand", doc!.nodes);
    Assert.Contains("served", doc.nodes);
    Assert.Equal(2, doc.order.Length);
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
