using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace FlowTime.Api.Tests;

public class ValidationEndpointsTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient client;

    private const string MinimalValidYaml = """
        schemaVersion: 1
        grid:
          bins: 4
          binSize: 15
          binUnit: minutes
        nodes:
          - id: Source
            kind: const
            values: [10, 10, 10, 10]
        """;

    private const string InvalidSchemaYaml = """
        schemaVersion: 999
        nodes: []
        """;

    public ValidationEndpointsTests(TestWebApplicationFactory factory)
    {
        client = factory.CreateClient();
    }

    [Theory]
    [InlineData("schema")]
    [InlineData("compile")]
    [InlineData("analyse")]
    public async Task ValidModel_AllTiers_Returns200_IsValid(string tier)
    {
        var response = await client.PostAsJsonAsync("/v1/validate", new { yaml = MinimalValidYaml, tier });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        Assert.True(body["isValid"]!.GetValue<bool>(), $"Expected isValid=true for tier={tier}; errors={body["errors"]}");
        Assert.Equal(tier, body["tier"]!.GetValue<string>());
        Assert.Empty(body["errors"]!.AsArray());
    }

    [Theory]
    [InlineData("schema")]
    [InlineData("compile")]
    [InlineData("analyse")]
    public async Task InvalidModel_AllTiers_Returns200_NotValid(string tier)
    {
        var response = await client.PostAsJsonAsync("/v1/validate", new { yaml = InvalidSchemaYaml, tier });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        Assert.False(body["isValid"]!.GetValue<bool>(), $"Expected isValid=false for tier={tier}");
        Assert.NotEmpty(body["errors"]!.AsArray());
    }

    [Fact]
    public async Task EmptyYaml_Returns400()
    {
        var response = await client.PostAsJsonAsync("/v1/validate", new { yaml = "", tier = "schema" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task NullYaml_Returns400()
    {
        var response = await client.PostAsJsonAsync("/v1/validate", new { yaml = (string?)null, tier = "schema" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UnknownTier_Returns400()
    {
        var response = await client.PostAsJsonAsync("/v1/validate", new { yaml = MinimalValidYaml, tier = "bogus" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task NullTier_Returns400()
    {
        var response = await client.PostAsJsonAsync("/v1/validate", new { yaml = MinimalValidYaml, tier = (string?)null });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ResponseShape_HasRequiredFields()
    {
        var response = await client.PostAsJsonAsync("/v1/validate", new { yaml = MinimalValidYaml, tier = "schema" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        Assert.NotNull(body["tier"]);
        Assert.NotNull(body["isValid"]);
        Assert.NotNull(body["errors"]);
        Assert.NotNull(body["warnings"]);
    }

    [Fact]
    public async Task Analyse_ValidModel_ReturnsWarningsArray()
    {
        // Tier 3 must return a warnings array (may be empty for a simple model)
        var response = await client.PostAsJsonAsync("/v1/validate", new { yaml = MinimalValidYaml, tier = "analyse" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        Assert.True(body["isValid"]!.GetValue<bool>());
        Assert.NotNull(body["warnings"]);
    }

    [Fact]
    public async Task CaseInsensitiveTier_Works()
    {
        var response = await client.PostAsJsonAsync("/v1/validate", new { yaml = MinimalValidYaml, tier = "SCHEMA" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
