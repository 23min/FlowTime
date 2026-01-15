using System.Net.Http.Json;
using System.Text;
using System.Linq;
using FlowTime.Api.Tests;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Hosting;
using FlowTime.Core.Models;
using FlowTime.Core.Nodes;
using FlowTime.Core.Execution;

public class ParityTests : IClassFixture<TestWebApplicationFactory>
{
	private readonly TestWebApplicationFactory factory;

	public ParityTests(TestWebApplicationFactory factory)
	{
		// TestWebApplicationFactory already configures test isolation
		this.factory = factory;
	}

	[Fact]
	public async Task Api_Run_Equals_Core_For_Example_Model()
	{
		// Arrange: model YAML mirrors examples/hello/model.yaml (no tabs)
		var yaml =
			"schemaVersion: 1\n" +
			"grid:\n" +
			"  bins: 8\n" +
			"  binSize: 60\n" +
			"  binUnit: minutes\n" +
			"nodes:\n" +
			"  - id: demand\n" +
			"    kind: const\n" +
			"    values: [10,10,10,10,10,10,10,10]\n" +
			"  - id: served\n" +
			"    kind: expr\n" +
			"    expr: \"demand * 0.8\"\n" +
			"outputs:\n" +
			"  - series: served\n" +
			"    as: served.csv\n";

		// Act: call API
		var client = factory.CreateClient();
		var resp = await client.PostAsync("/v1/run", new StringContent(yaml, Encoding.UTF8, "text/plain"));
		if (!resp.IsSuccessStatusCode)
		{
			var body = await resp.Content.ReadAsStringAsync();
			throw new Xunit.Sdk.XunitException($"/run returned {(int)resp.StatusCode} {resp.StatusCode}: {body}");
		}
		var doc = await resp.Content.ReadFromJsonAsync<RunResponse>();
		Assert.NotNull(doc);

		// Compute expected values using Core directly
		var grid = new TimeGrid(8, 60, TimeUnit.Minutes);
		var nodes = new INode[]
		{
						new ConstSeriesNode("demand", Enumerable.Repeat(10.0, 8).ToArray()),
						new BinaryOpNode("served", new NodeId("demand"), new NodeId("__scalar__"), BinOp.Mul, 0.8)
		};
		var graph = new Graph(nodes);
		var order = graph.TopologicalOrder();
		var ctx = graph.Evaluate(grid);
		var expected = ctx[new NodeId("served")].ToArray();

		// Assert exact equality (M0 determinism)
		Assert.Equal(expected, doc!.series["served"]);
		Assert.Equal(grid.Bins, doc.grid.bins);
		Assert.Equal(grid.BinSize, doc.grid.binSize);
		Assert.Equal(grid.BinUnit.ToString().ToLowerInvariant(), doc.grid.binUnit);
		Assert.Contains("served", doc.series.Keys);
	}

	[Fact]
	public async Task Api_Run_Applies_Router_Overrides()
	{
		var client = factory.CreateClient();
		var resp = await client.PostAsync("/v1/run", new StringContent(RouterOverrideModel, Encoding.UTF8, "text/plain"));
		if (!resp.IsSuccessStatusCode)
		{
			var body = await resp.Content.ReadAsStringAsync();
			throw new Xunit.Sdk.XunitException($"/run returned {(int)resp.StatusCode} {resp.StatusCode}: {body}");
		}

		var doc = await resp.Content.ReadFromJsonAsync<RunResponse>();
		Assert.NotNull(doc);
		Assert.True(doc!.series.TryGetValue("route_air", out var routeAir), "API response missing route_air series");
		Assert.True(doc.series.TryGetValue("route_ground", out var routeGround), "API response missing route_ground series");

		Assert.Equal(new[] { 8d }, routeAir);
		Assert.Equal(new[] { 2d }, routeGround);
	}

	private const string RouterOverrideModel = """
schemaVersion: 1
generator: flowtime-sim
grid:
  bins: 1
  binSize: 60
  binUnit: minutes
classes:
  - id: Alpha
  - id: Beta
traffic:
  arrivals:
    - nodeId: alpha_inflow
      classId: Alpha
      pattern:
        kind: constant
        ratePerBin: 8
    - nodeId: beta_inflow
      classId: Beta
      pattern:
        kind: constant
        ratePerBin: 2
topology:
  nodes:
    - id: RouterNode
      kind: service
      semantics:
        arrivals: router_input
        served: route_air
        errors: route_ground
nodes:
  - id: alpha_inflow
    kind: const
    values: [8]
  - id: beta_inflow
    kind: const
    values: [2]
  - id: router_input
    kind: expr
    expr: alpha_inflow + beta_inflow
  - id: route_air
    kind: const
    values: [0]
  - id: route_ground
    kind: const
    values: [0]
  - id: hub_router
    kind: router
    inputs:
      queue: router_input
    routes:
      - target: route_air
        classes: [Alpha]
      - target: route_ground
        classes: [Beta]
outputs: []
""";

	public sealed class RunResponse
	{
		public required Grid grid { get; init; }
		public required string[] order { get; init; }
		public required Dictionary<string, double[]> series { get; init; }
	}
	public sealed class Grid { public int bins { get; init; } public int binSize { get; init; } public string binUnit { get; init; } = ""; }
}
