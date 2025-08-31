using FlowTime.UI.Services;

public class SimulationRunClientTests
{
	[Fact]
	public async Task Simulation_Run_IsDeterministic()
	{
		var sim = new SimulationRunClient();
		var r1 = await sim.RunAsync("model: a", CancellationToken.None);
		var r2 = await sim.RunAsync("model: b", CancellationToken.None); // input ignored, should still match
		Assert.True(r1.Success && r2.Success);
		Assert.Equal(r1.Value!.Bins, r2.Value!.Bins);
		Assert.Equal(r1.Value.Order, r2.Value.Order);
		Assert.True(r1.Value.Series["demand"].SequenceEqual(r2.Value.Series["demand"]));
		Assert.True(r1.Value.Series["served"].SequenceEqual(r2.Value.Series["served"]));
	}

	[Fact]
	public async Task Simulation_Graph_HasExpectedStructure()
	{
		var sim = new SimulationRunClient();
		var g = await sim.GraphAsync("yaml", CancellationToken.None);
		Assert.True(g.Success);
		var value = g.Value!;
		Assert.Equal(new[]{"demand","served"}, value.Order);
		var demand = value.Nodes.Single(n => n.Id == "demand");
		var served = value.Nodes.Single(n => n.Id == "served");
		Assert.Empty(demand.Inputs);
		Assert.Single(served.Inputs);
		Assert.Equal("demand", served.Inputs[0]);
	}
}
