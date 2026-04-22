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
/// Tests for POST /v1/optimize.
/// The Rust engine is NOT enabled in the test factory.
/// </summary>
public sealed class OptimizeEndpointsTests : IClassFixture<TestWebApplicationFactory>
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

    public OptimizeEndpointsTests(TestWebApplicationFactory factory)
    {
        client = factory.CreateClient();
    }

    private static object ValidBody() => new
    {
        yaml = MinimalYaml,
        paramIds = new[] { "arrivals" },
        metricSeriesId = "metric",
        objective = "minimize",
        searchRanges = new { arrivals = new { lo = 0.0, hi = 100.0 } },
    };

    // ── Input validation → 400 ─────────────────────────────────────────────

    [Fact]
    public async Task MissingYaml_Returns400()
    {
        var response = await client.PostAsJsonAsync("/v1/optimize", new
        {
            paramIds = new[] { "arrivals" },
            metricSeriesId = "metric",
            objective = "minimize",
            searchRanges = new { arrivals = new { lo = 0.0, hi = 100.0 } },
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task MissingParamIds_Returns400()
    {
        var response = await client.PostAsJsonAsync("/v1/optimize", new
        {
            yaml = MinimalYaml,
            metricSeriesId = "metric",
            objective = "minimize",
            searchRanges = new { arrivals = new { lo = 0.0, hi = 100.0 } },
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task EmptyParamIds_Returns400()
    {
        var response = await client.PostAsJsonAsync("/v1/optimize", new
        {
            yaml = MinimalYaml,
            paramIds = Array.Empty<string>(),
            metricSeriesId = "metric",
            objective = "minimize",
            searchRanges = new { arrivals = new { lo = 0.0, hi = 100.0 } },
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task MissingMetricSeriesId_Returns400()
    {
        var response = await client.PostAsJsonAsync("/v1/optimize", new
        {
            yaml = MinimalYaml,
            paramIds = new[] { "arrivals" },
            objective = "minimize",
            searchRanges = new { arrivals = new { lo = 0.0, hi = 100.0 } },
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task MissingObjective_Returns400()
    {
        var response = await client.PostAsJsonAsync("/v1/optimize", new
        {
            yaml = MinimalYaml,
            paramIds = new[] { "arrivals" },
            metricSeriesId = "metric",
            searchRanges = new { arrivals = new { lo = 0.0, hi = 100.0 } },
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task InvalidObjective_Returns400()
    {
        var response = await client.PostAsJsonAsync("/v1/optimize", new
        {
            yaml = MinimalYaml,
            paramIds = new[] { "arrivals" },
            metricSeriesId = "metric",
            objective = "maximise",   // typo
            searchRanges = new { arrivals = new { lo = 0.0, hi = 100.0 } },
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task MissingSearchRanges_Returns400()
    {
        var response = await client.PostAsJsonAsync("/v1/optimize", new
        {
            yaml = MinimalYaml,
            paramIds = new[] { "arrivals" },
            metricSeriesId = "metric",
            objective = "minimize",
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SearchRangeLoEqualsHi_Returns400()
    {
        var response = await client.PostAsJsonAsync("/v1/optimize", new
        {
            yaml = MinimalYaml,
            paramIds = new[] { "arrivals" },
            metricSeriesId = "metric",
            objective = "minimize",
            searchRanges = new { arrivals = new { lo = 50.0, hi = 50.0 } },
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SearchRangesMissingEntry_Returns400()
    {
        // paramIds names "capacity" but searchRanges only has "arrivals" — missing entry.
        var response = await client.PostAsJsonAsync("/v1/optimize", new
        {
            yaml = MinimalYaml,
            paramIds = new[] { "arrivals", "capacity" },
            metricSeriesId = "metric",
            objective = "minimize",
            searchRanges = new { arrivals = new { lo = 0.0, hi = 100.0 } },
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── Engine not enabled → 503 ───────────────────────────────────────────

    [Fact]
    public async Task EngineNotEnabled_Returns503()
    {
        var response = await client.PostAsJsonAsync("/v1/optimize", ValidBody());
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }
}

/// <summary>
/// End-to-end trace-shape assertions for POST /v1/optimize covering both exit paths
/// in <see cref="Optimizer.OptimizeAsync"/>. The factory flips RustEngine on and
/// substitutes a deterministic fake <see cref="IModelEvaluator"/> at DI configure
/// time so the endpoint exercises the real runner without any Rust binary.
///
/// Exit paths covered (per AC2):
///   1. Pre-loop convergence              — initial simplex already satisfies tolerance
///   2. Main-loop convergence             — f-spread falls below tolerance mid-loop
///   3. Max-iterations exhausted          — loop exits without converging
///
/// Unsigned-metric invariant covered for both minimize and maximize objectives.
/// </summary>
public sealed class OptimizeEndpointsTraceTests : IDisposable
{
    private const string BowlYaml = """
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

    /// <summary>metric = (arrivals - 50)^2 — always ≥ 0, minimum at arrivals=50.</summary>
    private sealed class BowlEvaluator : IModelEvaluator
    {
        public Task<IReadOnlyDictionary<string, double[]>> EvaluateAsync(
            string modelYaml, CancellationToken cancellationToken = default)
        {
            var arrivals = ConstNodeReader.ReadValue(modelYaml, "arrivals") ?? 0.0;
            var v = (arrivals - 50.0) * (arrivals - 50.0);
            return Task.FromResult<IReadOnlyDictionary<string, double[]>>(
                new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase)
                {
                    ["metric"] = [v, v, v, v],
                });
        }
    }

    /// <summary>metric = arrivals — strictly positive on [0,100], maximized at 100.</summary>
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

    private sealed class OptimizeFactory : TestWebApplicationFactory
    {
        private readonly Func<IModelEvaluator> evaluatorFactory;
        public OptimizeFactory(Func<IModelEvaluator> evaluatorFactory)
        {
            this.evaluatorFactory = evaluatorFactory;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("RustEngine:Enabled", "true");
            builder.UseSetting("RustEngine:BinaryPath", "/nonexistent/flowtime-engine");
            builder.UseSetting("RustEngine:UseSession", "false");
            base.ConfigureWebHost(builder);

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IModelEvaluator>();
                services.AddScoped<IModelEvaluator>(_ => evaluatorFactory());
            });
        }
    }

    private readonly OptimizeFactory bowlFactory = new(() => new BowlEvaluator());
    private readonly OptimizeFactory linearFactory = new(() => new LinearEvaluator());

    public void Dispose()
    {
        bowlFactory.Dispose();
        linearFactory.Dispose();
    }

    private static async Task<JsonElement> PostAndReadAsync(HttpClient client, object body)
    {
        var response = await client.PostAsJsonAsync("/v1/optimize", body);
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

    // ── Exit path 1: pre-loop convergence ──────────────────────────────────

    [Fact]
    public async Task PreLoopConvergence_TraceHasSingleIterationZeroEntry()
    {
        // Tiny range centred at the bowl minimum → f-spread across initial simplex
        // already below tolerance.
        using var client = bowlFactory.CreateClient();
        var root = await PostAndReadAsync(client, new
        {
            yaml = BowlYaml,
            paramIds = new[] { "arrivals" },
            metricSeriesId = "metric",
            objective = "minimize",
            searchRanges = new { arrivals = new { lo = 49.0, hi = 51.0 } },
            tolerance = 0.1,
        });

        Assert.True(root.GetProperty("converged").GetBoolean());
        Assert.Equal(0, root.GetProperty("iterations").GetInt32());

        var trace = ReadTrace(root);
        Assert.Single(trace);
        Assert.Equal(0, trace[0].GetProperty("iteration").GetInt32());
        // MetricMean must be unsigned (the bowl is always ≥ 0, so a negative value would
        // prove the sign-flipped internal f-value leaked through).
        Assert.True(trace[0].GetProperty("metricMean").GetDouble() >= 0.0);
    }

    // ── Exit path 2: main-loop convergence ─────────────────────────────────

    [Fact]
    public async Task MainLoopConvergence_TraceLengthEqualsIterationsPlusOne()
    {
        using var client = bowlFactory.CreateClient();
        var root = await PostAndReadAsync(client, new
        {
            yaml = BowlYaml,
            paramIds = new[] { "arrivals" },
            metricSeriesId = "metric",
            objective = "minimize",
            searchRanges = new { arrivals = new { lo = 0.0, hi = 100.0 } },
        });

        Assert.True(root.GetProperty("converged").GetBoolean());
        var iterations = root.GetProperty("iterations").GetInt32();
        Assert.True(iterations >= 1);

        var trace = ReadTrace(root);
        Assert.Equal(iterations + 1, trace.Length);

        // Ordering: 0, 1, ..., iterations.
        for (int i = 0; i < trace.Length; i++)
            Assert.Equal(i, trace[i].GetProperty("iteration").GetInt32());

        // Unsigned-metric invariant for minimize: bowl (x-50)^2 ≥ 0 → every entry ≥ 0.
        Assert.All(trace, tp =>
            Assert.True(tp.GetProperty("metricMean").GetDouble() >= 0.0));

        // Final trace entry matches the response's achievedMetricMean.
        var last = trace[^1];
        Assert.Equal(root.GetProperty("achievedMetricMean").GetDouble(),
            last.GetProperty("metricMean").GetDouble(), precision: 6);
    }

    // ── Exit path 3: max-iterations exhausted ──────────────────────────────

    [Fact]
    public async Task MaxIterationsExhausted_TraceLengthEqualsIterationsPlusOne()
    {
        using var client = bowlFactory.CreateClient();
        var root = await PostAndReadAsync(client, new
        {
            yaml = BowlYaml,
            paramIds = new[] { "arrivals" },
            metricSeriesId = "metric",
            objective = "minimize",
            searchRanges = new { arrivals = new { lo = 0.0, hi = 75.0 } },
            tolerance = 1e-15,
            maxIterations = 3,
        });

        Assert.False(root.GetProperty("converged").GetBoolean());
        Assert.Equal(3, root.GetProperty("iterations").GetInt32());

        var trace = ReadTrace(root);
        Assert.Equal(4, trace.Length);
        for (int i = 0; i < 4; i++)
            Assert.Equal(i, trace[i].GetProperty("iteration").GetInt32());
    }

    // ── Unsigned-metric invariant — MAXIMIZE ──────────────────────────────

    [Fact]
    public async Task Maximize_TraceMetricMean_IsUnsigned()
    {
        // LinearEvaluator: metric = arrivals ∈ [0, 100]. Maximize negates internally;
        // if the trace leaks the sign-flipped f-value it will be negative.
        using var client = linearFactory.CreateClient();
        var root = await PostAndReadAsync(client, new
        {
            yaml = BowlYaml,
            paramIds = new[] { "arrivals" },
            metricSeriesId = "metric",
            objective = "maximize",
            searchRanges = new { arrivals = new { lo = 0.0, hi = 100.0 } },
        });

        Assert.True(root.GetProperty("converged").GetBoolean());
        // achievedMetricMean is in user space (positive), and trace should match.
        var achievedMean = root.GetProperty("achievedMetricMean").GetDouble();
        Assert.True(achievedMean > 0.0);

        var trace = ReadTrace(root);
        Assert.NotEmpty(trace);
        Assert.All(trace, tp =>
            Assert.True(tp.GetProperty("metricMean").GetDouble() >= 0.0,
                $"Trace iter {tp.GetProperty("iteration").GetInt32()} has negative metricMean — " +
                $"sign-flipped internal f-value leaked"));

        // Final trace entry matches the achieved metric (both unsigned).
        Assert.Equal(achievedMean,
            trace[^1].GetProperty("metricMean").GetDouble(), precision: 6);
    }

    // ── JSON serialization shape ──────────────────────────────────────────

    [Fact]
    public async Task Response_TraceField_UsesCamelCasePropertyNames()
    {
        using var client = bowlFactory.CreateClient();
        var root = await PostAndReadAsync(client, new
        {
            yaml = BowlYaml,
            paramIds = new[] { "arrivals" },
            metricSeriesId = "metric",
            objective = "minimize",
            searchRanges = new { arrivals = new { lo = 0.0, hi = 100.0 } },
        });

        var trace = ReadTrace(root);
        Assert.NotEmpty(trace);
        var first = trace[0];

        Assert.True(first.TryGetProperty("iteration", out _));
        Assert.True(first.TryGetProperty("paramValues", out var pv));
        Assert.True(first.TryGetProperty("metricMean", out _));

        // paramValues must be an object keyed by the spec's paramIds.
        Assert.Equal(JsonValueKind.Object, pv.ValueKind);
        Assert.True(pv.TryGetProperty("arrivals", out _));
    }
}
