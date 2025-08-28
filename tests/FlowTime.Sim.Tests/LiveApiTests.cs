using System.Net.Http.Headers;
using System.Text;
using FlowTime.Sim.Cli;
using Xunit;

namespace FlowTime.Sim.Tests;

// Live API tests hitting a running FlowTime API instance.
// Behavior matrix:
//   Local (GITHUB_ACTIONS != true): default ON opportunistically (runs if API reachable). Override:
//       RUN_LIVE_API_TESTS=0 -> force skip
//       RUN_LIVE_API_TESTS=1 -> force attempt (fail loud if unreachable)
//   CI (GITHUB_ACTIONS == true): default OFF (skip). Override:
//       RUN_LIVE_API_TESTS=1 -> force attempt (fail loud if unreachable)
//       RUN_LIVE_API_TESTS=0 (or unset) -> skip
// In all non-forced attempts, if API not reachable after retries we skip with an explanatory message.
public class LiveApiTests
{
  private static bool IsCi => Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true";
  private static string? GateVar => Environment.GetEnvironmentVariable("RUN_LIVE_API_TESTS");
  private static bool ForceEnabled => GateVar == "1";
  private static bool ForceDisabled => GateVar == "0";
  // Should we even attempt to discover an API endpoint?
  private static bool ShouldAttempt => !ForceDisabled && (ForceEnabled || (!IsCi));

  // Resolve base URL: explicit env beats autodetect. Autodetect tries common container/host names.
  private static async Task<string?> AcquireBaseUrlAsync(CancellationToken ct)
  {
  if (!ShouldAttempt) return null;
    var explicitBase = Environment.GetEnvironmentVariable("FLOWTIME_API_BASE");
    if (!string.IsNullOrWhiteSpace(explicitBase) && await Healthy(explicitBase, ct)) return explicitBase.TrimEnd('/');

    // Fallback candidates (ordered): docker service name, localhost
    var candidates = new[]
    {
      // Common explicit 8080 mapping
      "http://flowtime-api:8080",
      // Default Kestrel HTTP port inside container (5000) if host maps 8080->5000
      "http://flowtime-api:5000",
      // Bare service name (if a resolver injects default port mapping)
      "http://flowtime-api",
      // Host loopback variants
      "http://localhost:8080",
      "http://localhost:5000"
    };
    foreach (var c in candidates)
    {
      if (await Healthy(c, ct)) return c.TrimEnd('/');
    }
    return null;
  }

  private static async Task<bool> Healthy(string baseUrl, CancellationToken ct)
  {
    try
    {
      using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(1.5) };
      var resp = await http.GetAsync(baseUrl.TrimEnd('/') + "/healthz", ct);
      return resp.IsSuccessStatusCode;
    }
    catch { return false; }
  }

  private static HttpClient CreateClient()
  {
    var http = new HttpClient();
    http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
  // Add a trace header so API logs can confirm test traffic.
  http.DefaultRequestHeaders.Add("X-Live-Test", "flowtime-sim");
    return http;
  }

  private const string minimalModel = """
  grid:
    bins: 3
    binMinutes: 60
  nodes:
    - id: demand
      kind: const
      values: [1,2,3]
  outputs: []
  """;

  private static async Task<string?> GetBaseOrSkip()
  {
    var cts = new CancellationTokenSource(TimeSpan.FromSeconds(4));
    // Light retry loop (3 attempts spaced)
    for (int i = 0; i < 3; i++)
    {
      var b = await AcquireBaseUrlAsync(cts.Token);
      if (b != null) return b;
      await Task.Delay(250, cts.Token);
    }
    // If explicitly enabled but unreachable, fail loudly for visibility.
    if (ForceEnabled)
    {
      Assert.Fail("RUN_LIVE_API_TESTS=1 but no reachable FlowTime API endpoint (tried flowtime-api:[8080,5000], localhost). Set FLOWTIME_API_BASE or start the API with --urls.");
    }
    // Opportunistic (local default) or CI default skip: emit context-specific skip reason
    var reason = IsCi ? "(CI default skip)" : ForceDisabled ? "(forced off)" : "(API not reachable)";
    Console.WriteLine($"[LiveApiTests] Skipped {reason}");
    return null;
  }

  [Fact]
  public async Task Run_MinimalModel_Succeeds()
  {
    var baseUrl = await GetBaseOrSkip();
    if (baseUrl is null) return; // skip silently
    using var http = CreateClient();
  var res = await FlowTimeClient.RunAsync(http, baseUrl, minimalModel, default);
    Assert.Equal(3, res.grid.bins);
    Assert.Equal(60, res.grid.binMinutes);
    Assert.Contains("demand", res.order);
    Assert.Equal(new double[]{1,2,3}, res.series["demand"]);
  }

  [Fact]
  public async Task Run_Deterministic_RepeatedCallsMatch()
  {
    var baseUrl = await GetBaseOrSkip();
    if (baseUrl is null) return;
    using var http = CreateClient();
  var r1 = await FlowTimeClient.RunAsync(http, baseUrl, minimalModel, default);
  var r2 = await FlowTimeClient.RunAsync(http, baseUrl, minimalModel, default);
    Assert.Equal(r1.series["demand"], r2.series["demand"]);
  }

  [Fact]
  public async Task Run_InvalidModel_ProducesError()
  {
    var baseUrl = await GetBaseOrSkip();
    if (baseUrl is null) return;
    using var http = CreateClient();
    var bad = "grid:{}"; // missing bins
    var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
      await FlowTimeClient.RunAsync(http, baseUrl, bad, default));
    Assert.Contains("FlowTime", ex.Message, StringComparison.OrdinalIgnoreCase);
  }

  [Fact]
  public async Task Run_ExprNode_ParsesAndEvaluates()
  {
    var baseUrl = await GetBaseOrSkip();
    if (baseUrl is null) return;
    using var http = CreateClient();
    var model = """
    grid:
      bins: 4
      binMinutes: 30
    nodes:
      - id: base
        kind: const
        values: [5,5,5,5]
      - id: scaled
        kind: expr
        expr: "base * 0.5"
    outputs: []
    """;
    var res = await FlowTimeClient.RunAsync(http, baseUrl, model, default);
    Assert.Equal(new double[]{5,5,5,5}, res.series["base"]);
    Assert.Equal(new double[]{2.5,2.5,2.5,2.5}, res.series["scaled"]);
  }
}
