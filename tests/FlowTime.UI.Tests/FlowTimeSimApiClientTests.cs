using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using FlowTime.UI.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace FlowTime.UI.Tests;

public class FlowTimeSimApiClientTests
{
    [Fact]
    public async Task CreateRunAsync_ReturnsRunId_OnSuccess()
    {
        HttpRequestMessage? captured = null;
        var payload = JsonSerializer.Serialize(new
        {
            isDryRun = false,
            metadata = new
            {
                runId = "run_sim_42",
                templateId = "sim-order",
                templateTitle = "Simulation Order Template",
                templateVersion = "1.0.0",
                mode = "simulation",
                telemetrySourcesResolved = true,
                schema = new { id = "time-travel/v1", version = "1", hash = "sha256:cafe" },
                storage = new { modelPath = "runs/run_sim_42/model/model.yaml", metadataPath = (string?)null, provenancePath = (string?)null }
            },
            warnings = Array.Empty<object>(),
            canReplay = true
        });

        var handler = new StubHandler(request =>
        {
            captured = request;
            return new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
        });

        using var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:5219/")
        };
        var client = new FlowTimeSimApiClient(http, NullLogger<FlowTimeSimApiClient>.Instance, "v1");

        var response = await client.CreateRunAsync(new RunCreateRequestDto("sim-order", "simulation", null, null, null), CancellationToken.None);

        Assert.True(response.Success);
        Assert.Equal(201, response.StatusCode);
        Assert.Equal("run_sim_42", response.Value?.Metadata?.RunId);
        Assert.Equal("/api/v1/orchestration/runs", captured?.RequestUri?.AbsolutePath);
    }

    [Fact]
    public async Task CreateRunAsync_PropagatesErrors()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("{\"error\":\"Template not found\"}", Encoding.UTF8, "application/json")
        });

        using var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:5219/")
        };
        var client = new FlowTimeSimApiClient(http, NullLogger<FlowTimeSimApiClient>.Instance, "v1");

        var response = await client.CreateRunAsync(new RunCreateRequestDto("missing", "simulation", null, null, null), CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal(400, response.StatusCode);
        Assert.Equal("Template not found", response.Error);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> responder;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            this.responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(responder(request));
    }
}
