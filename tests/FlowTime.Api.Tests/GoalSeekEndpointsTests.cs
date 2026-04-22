using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FlowTime.TimeMachine.Sweep;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FlowTime.Api.Tests;

/// <summary>
/// Tests for POST /v1/goal-seek.
/// The Rust engine is NOT enabled in the test factory.
/// </summary>
public sealed class GoalSeekEndpointsTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient client;

    private const string MinimalYaml = """
        schemaVersion: 1
        grid:
          bins: 4
          binSize: 15
          binUnit: minutes
        nodes:
          - id: arrivals
            kind: const
            values: [10, 10, 10, 10]
        """;

    public GoalSeekEndpointsTests(TestWebApplicationFactory factory)
    {
        client = factory.CreateClient();
    }

    // ── Input validation → 400 ─────────────────────────────────────────────

    [Fact]
    public async Task MissingYaml_Returns400()
    {
        var response = await client.PostAsJsonAsync("/v1/goal-seek",
            new { paramId = "arrivals", metricSeriesId = "metric", target = 50.0, searchLo = 0.0, searchHi = 100.0 });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task NullYaml_Returns400()
    {
        var response = await client.PostAsJsonAsync("/v1/goal-seek",
            new { yaml = (string?)null, paramId = "arrivals", metricSeriesId = "metric", target = 50.0, searchLo = 0.0, searchHi = 100.0 });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task MissingParamId_Returns400()
    {
        var response = await client.PostAsJsonAsync("/v1/goal-seek",
            new { yaml = MinimalYaml, metricSeriesId = "metric", target = 50.0, searchLo = 0.0, searchHi = 100.0 });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task MissingMetricSeriesId_Returns400()
    {
        var response = await client.PostAsJsonAsync("/v1/goal-seek",
            new { yaml = MinimalYaml, paramId = "arrivals", target = 50.0, searchLo = 0.0, searchHi = 100.0 });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SearchLoEqualSearchHi_Returns400()
    {
        var response = await client.PostAsJsonAsync("/v1/goal-seek",
            new { yaml = MinimalYaml, paramId = "arrivals", metricSeriesId = "metric", target = 50.0, searchLo = 50.0, searchHi = 50.0 });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SearchLoGreaterThanSearchHi_Returns400()
    {
        var response = await client.PostAsJsonAsync("/v1/goal-seek",
            new { yaml = MinimalYaml, paramId = "arrivals", metricSeriesId = "metric", target = 50.0, searchLo = 100.0, searchHi = 10.0 });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task MissingSearchBounds_Returns400()
    {
        var response = await client.PostAsJsonAsync("/v1/goal-seek",
            new { yaml = MinimalYaml, paramId = "arrivals", metricSeriesId = "metric", target = 50.0 });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── Engine not enabled → 503 ───────────────────────────────────────────

    [Fact]
    public async Task EngineNotEnabled_Returns503()
    {
        var response = await client.PostAsJsonAsync("/v1/goal-seek",
            new { yaml = MinimalYaml, paramId = "arrivals", metricSeriesId = "metric", target = 50.0, searchLo = 0.0, searchHi = 100.0 });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }
}

/// <summary>
/// End-to-end trace-shape assertions for POST /v1/goal-seek covering every return
/// path in <see cref="GoalSeeker.SeekAsync"/>. The factory flips RustEngine on and
/// substitutes a deterministic fake <see cref="IModelEvaluator"/> at DI configure
/// time so the endpoint exercises the real runner without any Rust binary.
///
/// Return paths covered (per AC1):
///   1. Converged at searchLo          — boundary hit
///   2. Converged at searchHi          — boundary hit
///   3. Not bracketed                  — both residuals same sign
///   4. Tolerance hit mid-loop         — bisection converges inside the loop
///   5. Max-iterations exhausted       — bracketed but tolerance never satisfied
/// </summary>
public sealed class GoalSeekEndpointsTraceTests : IDisposable
{
    private const string LinearYaml = """
        schemaVersion: 1
        grid:
          bins: 4
          binSize: 15
          binUnit: minutes
        nodes:
          - id: arrivals
            kind: const
            values: [10, 10, 10, 10]
        """;

    /// <summary>Linear evaluator: metric = arrivals value.</summary>
    private sealed class LinearEvaluator : IModelEvaluator
    {
        public Task<IReadOnlyDictionary<string, double[]>> EvaluateAsync(
            string modelYaml, CancellationToken cancellationToken = default)
        {
            var arrivals = ConstNodeReader.ReadValue(modelYaml, "arrivals") ?? 0.0;
            return Task.FromResult<IReadOnlyDictionary<string, double[]>>(
                new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase)
                {
                    ["metric"] = [arrivals, arrivals, arrivals, arrivals],
                });
        }
    }

    /// <summary>Test factory that flips RustEngine on and swaps IModelEvaluator for a fake.</summary>
    private sealed class GoalSeekFactory : TestWebApplicationFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("RustEngine:Enabled", "true");
            builder.UseSetting("RustEngine:BinaryPath", "/nonexistent/flowtime-engine");
            builder.UseSetting("RustEngine:UseSession", "false");
            base.ConfigureWebHost(builder);

            builder.ConfigureTestServices(services =>
            {
                // Replace the real IModelEvaluator (RustModelEvaluator) with the fake.
                services.RemoveAll<IModelEvaluator>();
                services.AddScoped<IModelEvaluator, LinearEvaluator>();
            });
        }
    }

    private readonly GoalSeekFactory factory = new();
    private readonly HttpClient client;

    public GoalSeekEndpointsTraceTests()
    {
        client = factory.CreateClient();
    }

    public void Dispose() => factory.Dispose();

    private static async Task<JsonElement> PostAndReadAsync(HttpClient client, object body)
    {
        var response = await client.PostAsJsonAsync("/v1/goal-seek", body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<JsonElement>(raw);
    }

    private static JsonElement[] ReadTrace(JsonElement root)
    {
        Assert.True(root.TryGetProperty("trace", out var traceEl),
            $"response did not include 'trace' field. payload: {root}");
        return traceEl.EnumerateArray().ToArray();
    }

    // ── Return path 1: converged at searchLo ───────────────────────────────

    [Fact]
    public async Task Converged_AtSearchLo_TraceHasTwoBoundaryEntries()
    {
        // target=0 → meanLo=0 within tolerance; meanHi never evaluated for convergence.
        var root = await PostAndReadAsync(client, new
        {
            yaml = LinearYaml, paramId = "arrivals", metricSeriesId = "metric",
            target = 0.0, searchLo = 0.0, searchHi = 100.0,
        });

        Assert.True(root.GetProperty("converged").GetBoolean());
        Assert.Equal(0, root.GetProperty("iterations").GetInt32());

        var trace = ReadTrace(root);
        Assert.Equal(2, trace.Length);
        Assert.Equal(0, trace[0].GetProperty("iteration").GetInt32());
        Assert.Equal(0, trace[1].GetProperty("iteration").GetInt32());
        Assert.Equal(0.0, trace[0].GetProperty("paramValue").GetDouble());
        Assert.Equal(100.0, trace[1].GetProperty("paramValue").GetDouble());
        // Both carry the original bracket.
        Assert.Equal(0.0, trace[0].GetProperty("searchLo").GetDouble());
        Assert.Equal(100.0, trace[0].GetProperty("searchHi").GetDouble());
        Assert.Equal(0.0, trace[1].GetProperty("searchLo").GetDouble());
        Assert.Equal(100.0, trace[1].GetProperty("searchHi").GetDouble());
    }

    // ── Return path 2: converged at searchHi ───────────────────────────────

    [Fact]
    public async Task Converged_AtSearchHi_TraceHasTwoBoundaryEntries()
    {
        var root = await PostAndReadAsync(client, new
        {
            yaml = LinearYaml, paramId = "arrivals", metricSeriesId = "metric",
            target = 100.0, searchLo = 0.0, searchHi = 100.0,
        });

        Assert.True(root.GetProperty("converged").GetBoolean());
        Assert.Equal(0, root.GetProperty("iterations").GetInt32());

        var trace = ReadTrace(root);
        Assert.Equal(2, trace.Length);
        Assert.Equal(0, trace[0].GetProperty("iteration").GetInt32());
        Assert.Equal(0, trace[1].GetProperty("iteration").GetInt32());
        Assert.Equal(0.0, trace[0].GetProperty("paramValue").GetDouble());
        Assert.Equal(100.0, trace[1].GetProperty("paramValue").GetDouble());
    }

    // ── Return path 3: not bracketed ───────────────────────────────────────

    [Fact]
    public async Task NotBracketed_TraceHasTwoBoundaryEntriesOnly()
    {
        // target=200 above the metric range [0,100] → both residuals negative.
        var root = await PostAndReadAsync(client, new
        {
            yaml = LinearYaml, paramId = "arrivals", metricSeriesId = "metric",
            target = 200.0, searchLo = 0.0, searchHi = 100.0,
        });

        Assert.False(root.GetProperty("converged").GetBoolean());
        Assert.Equal(0, root.GetProperty("iterations").GetInt32());

        var trace = ReadTrace(root);
        Assert.Equal(2, trace.Length);
        Assert.All(trace, tp => Assert.Equal(0, tp.GetProperty("iteration").GetInt32()));
    }

    // ── Return path 4: tolerance hit mid-loop ──────────────────────────────

    [Fact]
    public async Task ConvergedMidLoop_TraceHasBoundariesPlusIterations()
    {
        // target=50 = midpoint of [0,100] → converges at iteration 1.
        var root = await PostAndReadAsync(client, new
        {
            yaml = LinearYaml, paramId = "arrivals", metricSeriesId = "metric",
            target = 50.0, searchLo = 0.0, searchHi = 100.0,
        });

        Assert.True(root.GetProperty("converged").GetBoolean());
        var iterations = root.GetProperty("iterations").GetInt32();
        Assert.True(iterations >= 1);

        var trace = ReadTrace(root);
        Assert.Equal(2 + iterations, trace.Length);
        // Ordering: 0, 0, 1, 2, ..., iterations.
        Assert.Equal(0, trace[0].GetProperty("iteration").GetInt32());
        Assert.Equal(0, trace[1].GetProperty("iteration").GetInt32());
        for (int i = 2; i < trace.Length; i++)
            Assert.Equal(i - 1, trace[i].GetProperty("iteration").GetInt32());

        // Post-step bracket invariant: every bisection entry's paramValue sits inside
        // its own [searchLo, searchHi].
        for (int i = 2; i < trace.Length; i++)
        {
            var paramValue = trace[i].GetProperty("paramValue").GetDouble();
            var lo = trace[i].GetProperty("searchLo").GetDouble();
            var hi = trace[i].GetProperty("searchHi").GetDouble();
            Assert.True(lo <= paramValue && paramValue <= hi,
                $"iteration {i - 1}: paramValue {paramValue} not in post-step bracket [{lo}, {hi}]");
        }
    }

    // ── Return path 5: max-iterations exhausted ────────────────────────────

    [Fact]
    public async Task MaxIterationsExhausted_TraceHasBoundariesPlusMaxIterations()
    {
        // target=33 bracketed in [0,100] but never on a dyadic midpoint in 3 bisections.
        var root = await PostAndReadAsync(client, new
        {
            yaml = LinearYaml, paramId = "arrivals", metricSeriesId = "metric",
            target = 33.0, searchLo = 0.0, searchHi = 100.0,
            tolerance = 1e-15,
            maxIterations = 3,
        });

        Assert.False(root.GetProperty("converged").GetBoolean());
        Assert.Equal(3, root.GetProperty("iterations").GetInt32());

        var trace = ReadTrace(root);
        Assert.Equal(5, trace.Length);
        Assert.Equal(0, trace[0].GetProperty("iteration").GetInt32());
        Assert.Equal(0, trace[1].GetProperty("iteration").GetInt32());
        Assert.Equal(1, trace[2].GetProperty("iteration").GetInt32());
        Assert.Equal(2, trace[3].GetProperty("iteration").GetInt32());
        Assert.Equal(3, trace[4].GetProperty("iteration").GetInt32());
    }

    // ── JSON serialization shape ───────────────────────────────────────────

    [Fact]
    public async Task Response_TraceField_UsesCamelCasePropertyNames()
    {
        var root = await PostAndReadAsync(client, new
        {
            yaml = LinearYaml, paramId = "arrivals", metricSeriesId = "metric",
            target = 50.0, searchLo = 0.0, searchHi = 100.0,
        });

        var trace = ReadTrace(root);
        Assert.NotEmpty(trace);
        var first = trace[0];

        // Every property on GoalSeekTracePoint must be camelCase at the wire layer.
        Assert.True(first.TryGetProperty("iteration", out _));
        Assert.True(first.TryGetProperty("paramValue", out _));
        Assert.True(first.TryGetProperty("metricMean", out _));
        Assert.True(first.TryGetProperty("searchLo", out _));
        Assert.True(first.TryGetProperty("searchHi", out _));
    }
}
