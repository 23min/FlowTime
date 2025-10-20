using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using FlowTime.UI.Configuration;
using FlowTime.UI.Services;
using Microsoft.Extensions.Options;

namespace FlowTime.UI.Tests;

public class FlowTimeApiClientTests
{
    [Fact]
    public async Task CreateRunAsync_ReturnsFriendlyError_WhenTemplateMissing()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("{\"error\":\"Template not found: missing-template\"}", Encoding.UTF8, "application/json")
        });

        using var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:8080/")
        };
        var opts = Options.Create(new FlowTimeApiOptions { ApiVersion = "v1" });
        var client = new FlowTimeApiClient(http, opts.Value);

        var response = await client.CreateRunAsync(new RunCreateRequestDto("missing-template", "simulation", null, null, null), CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal(404, response.StatusCode);
        Assert.Equal("Template not found: missing-template", response.Error);
    }

    [Fact]
    public async Task CreateRunAsync_ReturnsRunId_OnSuccess()
    {
        var payload = JsonSerializer.Serialize(new
        {
            isDryRun = false,
            metadata = new
            {
                runId = "run_123",
                templateId = "order-system",
                templateTitle = "Order System",
                templateVersion = "1.0.0",
                mode = "simulation",
                provenanceHash = (string?)null,
                telemetrySourcesResolved = true,
                schema = new { id = "time-travel/v1", version = "1", hash = "sha256:deadbeef" },
                storage = new { modelPath = "data/runs/run_123/model/model.yaml", metadataPath = "data/runs/run_123/model/metadata.json", provenancePath = (string?)null }
            },
            warnings = Array.Empty<object>(),
            canReplay = true
        });

        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.Created)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        });

        using var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:8080/")
        };
        var opts = Options.Create(new FlowTimeApiOptions { ApiVersion = "v1" });
        var client = new FlowTimeApiClient(http, opts.Value);

        var response = await client.CreateRunAsync(new RunCreateRequestDto("order-system", "simulation", null, null, null), CancellationToken.None);

        Assert.True(response.Success);
        Assert.Equal(201, response.StatusCode);
        Assert.Equal("run_123", response.Value?.Metadata?.RunId);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> responder;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            this.responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(responder(request));
        }
    }
}
