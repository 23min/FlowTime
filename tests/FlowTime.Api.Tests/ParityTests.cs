using System.Net.Http.Json;
using System.Text;
using System.Linq;
using FlowTime.Core;
using FlowTime.Api.Tests;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Hosting;

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

	public sealed class RunResponse
	{
		public required Grid grid { get; init; }
		public required string[] order { get; init; }
		public required Dictionary<string, double[]> series { get; init; }
	}
	public sealed class Grid { public int bins { get; init; } public int binSize { get; init; } public string binUnit { get; init; } = ""; }
}

