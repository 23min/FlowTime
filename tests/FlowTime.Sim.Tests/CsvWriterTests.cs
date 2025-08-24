using System.Text;
using FlowTime.Sim.Cli;
using Xunit;

namespace FlowTime.Sim.Tests;

public class CsvWriterTests
{
    [Fact]
    public async Task Writes_Csv_With_Header_And_Values()
    {
        var res = new FlowTimeRunResponse
        {
            grid = new FlowTimeGrid { bins = 3, binMinutes = 60 },
            order = new[] { "a", "b" },
            series = new Dictionary<string, double[]> {
                ["a"] = new double[] { 1, 2, 3 },
                ["b"] = new double[] { 10, 20, 30 }
            }
        };

        await using var ms = new MemoryStream();
        await Writers.WriteCsvAsync(res, ms, CancellationToken.None);
        var csv = Encoding.UTF8.GetString(ms.ToArray());

        var expected = string.Join('\n', new[]
        {
            "bin,index,a,b",
            "0,0,1,10",
            "1,1,2,20",
            "2,2,3,30",
            ""
        });

        Assert.Equal(expected, csv.Replace("\r\n", "\n"));
    }
}
