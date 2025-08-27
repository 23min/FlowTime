using System.Text;
using FlowTime.Sim.Core;
using Xunit;

namespace FlowTime.Sim.Tests;

public class WriterTests
{
    private SimulationSpec Spec(int bins, int binMinutes = 60) => new()
    {
        grid = new GridSpec { bins = bins, binMinutes = binMinutes, start = "2025-01-01T00:00:00Z" },
        route = new RouteSpec { id = "n1" },
        arrivals = new ArrivalsSpec { kind = "const", values = Enumerable.Repeat(2.0, bins).ToList() }
    };

    [Fact]
    public void EventFactory_Generates_Correct_Count_And_Timestamps()
    {
        var spec = Spec(3);
        var validation = SimulationSpecValidator.Validate(spec);
        Assert.True(validation.IsValid, string.Join(";", validation.Errors));
        var arrivals = ArrivalGenerators.Generate(spec);
        var events = EventFactory.BuildEvents(spec, arrivals).ToList();
        Assert.Equal(3 * 2, events.Count);
        Assert.Equal("e1", events[0].entity_id);
        Assert.Equal("2025-01-01T00:00:00Z", events[0].ts);
        Assert.Equal("2025-01-01T02:00:00Z", events[^1].ts);
    }

    [Fact]
    public async Task NdjsonWriter_Writes_One_Line_Per_Event()
    {
        var spec = Spec(2);
        var arrivals = new ArrivalGenerationResult(new[] { 1, 3 });
        var events = EventFactory.BuildEvents(spec, arrivals);
        await using var ms = new MemoryStream();
        await NdjsonWriter.WriteAsync(events, ms, CancellationToken.None);
        var text = Encoding.UTF8.GetString(ms.ToArray());
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(4, lines.Length);
        Assert.Contains("\"event_type\":\"arrival\"", lines[0]);
    }

    [Fact]
    public async Task GoldWriter_Writes_Header_And_Bin_Rows()
    {
        var spec = Spec(2);
        var arrivals = new ArrivalGenerationResult(new[] { 5, 7 });
        await using var ms = new MemoryStream();
        await GoldWriter.WriteAsync(spec, arrivals, ms, CancellationToken.None);
        var csv = Encoding.UTF8.GetString(ms.ToArray()).Replace("\r\n", "\n");
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(3, lines.Length); // header + 2 rows
        Assert.Equal("timestamp,node,flow,arrivals,served,errors", lines[0]);
        Assert.EndsWith(",5,5,0", lines[1]);
        Assert.EndsWith(",7,7,0", lines[2]);
    }
}
