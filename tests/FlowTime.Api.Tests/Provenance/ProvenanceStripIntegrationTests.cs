using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using FlowTime.API.Services;
using FlowTime.Core;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace FlowTime.Api.Tests.Provenance;

/// <summary>
/// Integration coverage for <see cref="ProvenanceService.StripProvenance"/> at the
/// <c>POST /v1/run</c> seam, plus the cross-validator contract that
/// <c>StripProvenance → ModelSchemaValidator.Validate</c> must hold for ambiguous
/// string scalars in <c>nodes[].metadata</c>.
///
/// <para>m-E23-02 production-fix watertight regression.</para>
/// The previous <c>Dictionary&lt;string,object&gt;</c>-based <c>StripProvenance</c>
/// re-emitted YAML 1.2-ambiguous quoted strings (e.g. <c>"3.5"</c>, <c>"true"</c>,
/// <c>"100"</c>) as plain scalars. The canonical schema's
/// <c>nodes[].metadata.additionalProperties.type: string</c> rule then rejected the
/// post-strip wire form. The unit tests in
/// <see cref="FlowTime.Api.Tests.Services.ProvenanceServiceStripTests"/> pin the function's
/// own branches; these tests pin the user-visible contract — that a model carrying both
/// a <c>provenance:</c> block AND <c>pmf.expected</c>-style ambiguous metadata round-trips
/// the full <c>Strip → ModelSchemaValidator.Validate → ModelParser → execute</c> pipeline
/// without losing scalar styles.
/// </summary>
public sealed class ProvenanceStripIntegrationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory factory;

    public ProvenanceStripIntegrationTests(TestWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    /// <summary>
    /// The watertight regression. A YAML payload carrying BOTH a root <c>provenance:</c>
    /// block AND every flavour of YAML-1.2-ambiguous metadata scalar
    /// (number-like <c>"3.5"</c>, bool-like <c>"true"</c>, enum-like <c>"pmf"</c>,
    /// integer-like <c>"100"</c>, null-like <c>"null"</c>) must POST to <c>/v1/run</c>
    /// and execute successfully. If <c>StripProvenance</c> regresses to drop scalar
    /// styles, this test fires before the bug ships.
    /// </summary>
    [Fact]
    public async Task PostRun_EmbeddedProvenance_WithAmbiguousMetadataScalars_RoundTripsSuccessfully()
    {
        // Arrange — every flavour of ambiguous string scalar that SimModelBuilder.cs:267
        // emits via G17 formatting or plain string literal, plus a few extras to pin the
        // contract more broadly than the current production emitters use.
        var client = factory.CreateClient();
        var model = """
            schemaVersion: 1
            provenance:
              source: flowtime-sim
              modelId: model_20260426T100000Z_strip_ambig
              templateId: ambiguous-scalars-regression
              templateVersion: "1.0"
              generatedAt: "2026-04-26T10:00:00Z"
              generator: "flowtime-sim/0.4.0"
            grid:
              bins: 4
              binSize: 1
              binUnit: hours
            nodes:
              - id: demand
                kind: const
                values: [100, 120, 150, 130]
                metadata:
                  origin.kind: "pmf"
                  pmf.expected: "3.5"
                  graph.hidden: "true"
                  count.literal: "100"
                  null.literal: "null"
            """;

        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/run")
        {
            Content = new StringContent(model, new MediaTypeHeaderValue("application/x-yaml"))
        };

        // Act
        var response = await client.SendAsync(request);

        // Assert — the user-visible contract: pipeline executed, response body has the
        // canonical run-response shape (runId, grid, order, series).
        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected 200 OK; got {(int)response.StatusCode} {response.StatusCode}. Body: {responseBody}");

        var doc = JsonSerializer.Deserialize<JsonElement>(responseBody);

        Assert.True(doc.TryGetProperty("runId", out var runId));
        Assert.False(string.IsNullOrWhiteSpace(runId.GetString()));

        Assert.True(doc.TryGetProperty("series", out var series));
        Assert.Equal(JsonValueKind.Object, series.ValueKind);

        // The run actually produced output, not just an empty success.
        Assert.True(series.TryGetProperty("demand", out var demandSeries));
        Assert.Equal(JsonValueKind.Array, demandSeries.ValueKind);
        Assert.Equal(4, demandSeries.GetArrayLength());
        Assert.Equal(100, demandSeries[0].GetDouble());
        Assert.Equal(120, demandSeries[1].GetDouble());
        Assert.Equal(150, demandSeries[2].GetDouble());
        Assert.Equal(130, demandSeries[3].GetDouble());
    }

    /// <summary>
    /// Cross-validator contract — <c>StripProvenance</c>'s post-strip output must satisfy
    /// the canonical <see cref="ModelSchemaValidator"/>. This is the gap the production fix
    /// closed: the previous Dictionary round-trip produced a wire shape the schema rejected
    /// because plain-scalar <c>3.5</c> failed <c>nodes[].metadata.additionalProperties.type: string</c>.
    ///
    /// <para>Lives in the integration-test file because it ties two distinct components
    /// (Strip + Validator) — distinct from <see cref="FlowTime.Api.Tests.Services.ProvenanceServiceStripTests"/>,
    /// which covers Strip's own branches only.</para>
    /// </summary>
    [Fact]
    public void StripProvenance_OutputWithAmbiguousMetadataScalars_PassesModelSchemaValidator()
    {
        // Arrange — same payload shape as the integration test, exercised at the unit seam.
        const string yaml = """
            schemaVersion: 1
            provenance:
              source: flowtime-sim
              modelId: model_20260426T100000Z_strip_ambig
              templateId: ambiguous-scalars-regression
              templateVersion: "1.0"
              generatedAt: "2026-04-26T10:00:00Z"
              generator: "flowtime-sim/0.4.0"
            grid:
              bins: 4
              binSize: 1
              binUnit: hours
            nodes:
              - id: demand
                kind: const
                values: [100, 120, 150, 130]
                metadata:
                  origin.kind: "pmf"
                  pmf.expected: "3.5"
                  graph.hidden: "true"
                  count.literal: "100"
                  null.literal: "null"
            """;

        // Act
        var stripped = ProvenanceService.StripProvenance(yaml);
        var validation = ModelSchemaValidator.Validate(stripped);

        // Assert — schema validation succeeds. If StripProvenance regresses to drop
        // scalar styles, the schema validator's metadata type-check rejects the output
        // and IsValid flips false with errors mentioning string-type expectations.
        Assert.True(
            validation.IsValid,
            $"Schema validation failed unexpectedly. Errors: {string.Join("; ", validation.Errors)}");
        Assert.Empty(validation.Errors);
    }
}
