using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Linq;
using FlowTime.UI.Configuration;
using FlowTime.UI.Services;
using FlowTime.UI.Components.Topology;
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

    [Fact]
    public async Task GetRunStateAsync_UsesQueryAndParsesResponse()
    {
        HttpRequestMessage? captured = null;
        var handler = new StubHandler(request =>
        {
            captured = request;
            var json = """
            {
              "metadata": {
                "runId": "run_abc",
                "templateId": "orders",
                "templateTitle": "Orders",
                "templateVersion": "1.0.0",
                "mode": "simulation",
                "telemetrySourcesResolved": true,
                "schema": { "id": "time-travel/v1", "version": "1", "hash": "sha256:123" },
                "storage": { "modelPath": "runs/run_abc/model.yaml", "metadataPath": null, "provenancePath": null }
              },
              "bin": { "index": 5, "startUtc": "2025-01-01T00:25:00Z", "endUtc": "2025-01-01T00:30:00Z", "durationMinutes": 5 },
              "nodes": [
                {
                  "id": "OrderSvc",
                  "kind": "service",
                  "metrics": {},
                  "derived": {},
                  "telemetry": { "sources": ["series/orders"], "warnings": [] }
                }
              ],
              "warnings": []
            }
            """;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        });

        using var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:8080/")
        };
        var opts = Options.Create(new FlowTimeApiOptions { ApiVersion = "v1" });
        var client = new FlowTimeApiClient(http, opts.Value);

        var response = await client.GetRunStateAsync("run_abc", 5, CancellationToken.None);

        Assert.True(response.Success);
        Assert.Equal("run_abc", response.Value?.Metadata.RunId);
        Assert.Equal(5, response.Value?.Bin.Index);
        Assert.Equal("/v1/runs/run_abc/state?binIndex=5", captured?.RequestUri?.PathAndQuery);
    }

    [Fact]
    public async Task GetRunStateWindowAsync_ReturnsErrorPayload_OnFailure()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("{\"error\":\"Run 'missing' not found\"}", Encoding.UTF8, "application/json")
        });

        using var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:8080/")
        };
        var opts = Options.Create(new FlowTimeApiOptions { ApiVersion = "v1" });
        var client = new FlowTimeApiClient(http, opts.Value);

        var response = await client.GetRunStateWindowAsync("missing", 0, 10, CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal(404, response.StatusCode);
        Assert.Equal("Run 'missing' not found", response.Error);
    }

    [Fact]
    public async Task GetRunStateWindowAsync_ParsesResponse()
    {
        HttpRequestMessage? captured = null;
        var handler = new StubHandler(request =>
        {
            captured = request;
            var json = """
            {
              "metadata": {
                "runId": "run_xyz",
                "templateId": "orders",
                "templateTitle": "Orders",
                "templateVersion": "1.0.0",
                "mode": "simulation",
                "telemetrySourcesResolved": true,
                "schema": { "id": "time-travel/v1", "version": "1", "hash": "sha256:cafe" },
                "storage": { "modelPath": "runs/run_xyz/model.yaml", "metadataPath": null, "provenancePath": null }
              },
              "window": { "startBin": 10, "endBin": 12, "binCount": 3 },
              "timestampsUtc": ["2025-01-01T00:50:00Z", "2025-01-01T00:55:00Z", "2025-01-01T01:00:00Z"],
              "nodes": [
                {
                  "id": "OrderSvc",
                  "kind": "service",
                  "series": { "lat": [ 0.5, 0.6, 0.7 ] },
                  "telemetry": { "sources": ["series/orders"], "warnings": [] }
                }
              ],
              "warnings": []
            }
            """;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        });

        using var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:8080/")
        };
        var opts = Options.Create(new FlowTimeApiOptions { ApiVersion = "v1" });
        var client = new FlowTimeApiClient(http, opts.Value);

        var response = await client.GetRunStateWindowAsync("run_xyz", 10, 12, CancellationToken.None);

        Assert.True(response.Success);
        Assert.Equal(3, response.Value?.Window.BinCount);
        Assert.Equal(3, response.Value?.Nodes.Single().Series["lat"].Length);
        Assert.Equal("/v1/runs/run_xyz/state_window?startBin=10&endBin=12", captured?.RequestUri?.PathAndQuery);
    }

    [Fact]
    public async Task GetRunGraphAsync_ParsesResponse()
    {
        HttpRequestMessage? captured = null;
        var handler = new StubHandler(request =>
        {
            captured = request;
            const string json = """
            {
              "nodes": [
                { "id": "source", "kind": "service", "semantics": { "arrivals": "series:arr", "served": "series:srv" } },
                { "id": "target", "kind": "queue", "semantics": { "arrivals": "series:arr", "served": "series:srv" } }
              ],
              "edges": [
                { "id": "edge_source_target", "from": "source:out", "to": "target:in", "weight": 1 }
              ]
            }
            """;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        });

        using var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:8080/")
        };
        var opts = Options.Create(new FlowTimeApiOptions { ApiVersion = "v1" });
        var client = new FlowTimeApiClient(http, opts.Value);

        var response = await client.GetRunGraphAsync("run_graph", null, CancellationToken.None);

        Assert.True(response.Success);
        Assert.Equal(2, response.Value?.Nodes.Count);
        Assert.Single(response.Value?.Edges);
        Assert.Equal("/v1/runs/run_graph/graph", captured?.RequestUri?.PathAndQuery);
    }

    [Fact]
    public async Task GenerateTelemetryCaptureAsync_PostsRequestAndParsesResponse()
    {
        HttpRequestMessage? captured = null;
        string? capturedBody = null;
        var handler = new StubHandler(request =>
        {
            captured = request;
            capturedBody = request.Content is null ? null : request.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            const string payload = """
            {
              "capture": {
                "generated": true,
                "alreadyExists": false,
                "generatedAtUtc": "2025-10-22T01:23:45Z",
                "sourceRunId": "RUN_ABC",
                "warnings": []
              }
            }
            """;

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
        });

        using var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:8080/")
        };
        var opts = Options.Create(new FlowTimeApiOptions { ApiVersion = "v1" });
        var client = new FlowTimeApiClient(http, opts.Value);

        var requestDto = new TelemetryCaptureRequestDto(
            Source: new TelemetryCaptureSourceDto("run", "RUN_ABC"),
            Output: new TelemetryCaptureOutputDto("capture-key", null, false));

        var response = await client.GenerateTelemetryCaptureAsync(requestDto, CancellationToken.None);

        Assert.True(response.Success);
        Assert.True(response.Value?.Capture.Generated);
        Assert.Equal("/v1/telemetry/captures", captured?.RequestUri?.PathAndQuery);

        Assert.False(string.IsNullOrWhiteSpace(capturedBody));
        using var doc = JsonDocument.Parse(capturedBody!);
        Assert.Equal("run", doc.RootElement.GetProperty("source").GetProperty("type").GetString());
        Assert.Equal("RUN_ABC", doc.RootElement.GetProperty("source").GetProperty("runId").GetString());
        Assert.Equal("capture-key", doc.RootElement.GetProperty("output").GetProperty("captureKey").GetString());
        Assert.False(doc.RootElement.GetProperty("output").GetProperty("overwrite").GetBoolean());
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
