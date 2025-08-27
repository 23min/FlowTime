using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Hosting;

public class ExampleModelParsesTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;

    public ExampleModelParsesTests(WebApplicationFactory<Program> baseFactory)
    {
        this.factory = baseFactory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting(WebHostDefaults.EnvironmentKey, "Development");
            builder.UseTestServer();
            builder.UseSetting(WebHostDefaults.ServerUrlsKey, "http://127.0.0.1:0");
        });
    }

    [Fact]
    public async Task HelloExample_Yaml_Parses_And_Runs()
    {
        // Mirrors examples/hello/model.yaml exact structure
        var yaml =
            "grid:\n" +
            "  bins: 8\n" +
            "  binMinutes: 60\n" +
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

        var client = factory.CreateClient();
        var resp = await client.PostAsync("/run", new StringContent(yaml, Encoding.UTF8, "text/plain"));
        resp.EnsureSuccessStatusCode();
        var doc = await resp.Content.ReadFromJsonAsync<RunResponse>();
        Assert.NotNull(doc);
        Assert.Equal(8, doc!.grid.bins);
        Assert.Equal(60, doc.grid.binMinutes);
        Assert.Contains("served", doc.series.Keys);
        Assert.Equal(new double[] { 8,8,8,8,8,8,8,8 }, doc.series["served"]);
    }

    public sealed class RunResponse
    {
        public required Grid grid { get; init; }
        public required string[] order { get; init; }
        public required Dictionary<string, double[]> series { get; init; }
    }
    public sealed class Grid { public int bins { get; init; } public int binMinutes { get; init; } }
}
