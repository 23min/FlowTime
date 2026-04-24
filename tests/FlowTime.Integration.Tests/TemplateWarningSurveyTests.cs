using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FlowTime.Sim.Core.Services;
using FlowTime.Sim.Core.Templates;
using FlowTime.TimeMachine.Validation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;

namespace FlowTime.Integration.Tests;

/// <summary>
/// Evidence-gathering survey (not a contract guard) — iterates every template under
/// the repo's `templates/` directory, renders it with default parameters, runs tier-3
/// analyse validation in-process, posts the rendered YAML to the live Engine API, and
/// collects any warnings that arrive on run-detail + state_window responses.
///
/// Output goes to xunit's <see cref="ITestOutputHelper"/>. Used to scope m-E21-07
/// Validation Surface: if warnings are rare, the milestone ships the minimum viable
/// list; if frequent, the middle tier (badges + click-to-navigate + tier grouping)
/// is justified.
///
/// Graceful-skip when the Engine API is not reachable at http://localhost:8081.
/// </summary>
public class TemplateWarningSurveyTests
{
    private const string EngineBaseUrl = "http://localhost:8081";
    private readonly ITestOutputHelper output;

    public TemplateWarningSurveyTests(ITestOutputHelper output) => this.output = output;

    private static string GetRepoPath(params string[] segments)
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
        return Path.Combine(new[] { repoRoot }.Concat(segments).ToArray());
    }

    [Fact]
    public async Task Survey_Templates_For_Warnings()
    {
        using var http = new HttpClient { BaseAddress = new Uri(EngineBaseUrl), Timeout = TimeSpan.FromSeconds(30) };

        // Probe the Engine API. Graceful skip if not up.
        try
        {
            var probe = await http.GetAsync("/v1/healthz");
            if (!probe.IsSuccessStatusCode)
            {
                output.WriteLine($"SKIP: Engine API probe returned {probe.StatusCode}.");
                return;
            }
        }
        catch (HttpRequestException ex)
        {
            output.WriteLine($"SKIP: Engine API not reachable at {EngineBaseUrl} — {ex.Message}");
            return;
        }

        var templatesDir = GetRepoPath("templates");
        var yamlFiles = Directory.EnumerateFiles(templatesDir, "*.yaml", SearchOption.TopDirectoryOnly)
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

        output.WriteLine($"Surveying {yamlFiles.Count} templates from {templatesDir}");
        output.WriteLine(new string('=', 100));
        output.WriteLine(string.Format("{0,-45} {1,10} {2,10} {3,10} {4}",
            "template", "val-err", "val-warn", "run-warn", "notes"));
        output.WriteLine(new string('-', 100));

        int totalValidatorErrors = 0;
        int totalValidatorWarnings = 0;
        int totalRunWarnings = 0;
        int templatesWithValidatorWarnings = 0;
        int templatesWithRunWarnings = 0;
        var warningCodeTally = new Dictionary<string, int>();

        foreach (var path in yamlFiles)
        {
            var templateId = Path.GetFileNameWithoutExtension(path);
            string rendered;
            try
            {
                var templateYaml = await File.ReadAllTextAsync(path);
                var service = new TemplateService(
                    new Dictionary<string, string> { [templateId] = templateYaml },
                    NullLogger<TemplateService>.Instance);
                rendered = await service.GenerateEngineModelAsync(templateId, new Dictionary<string, object>());
            }
            catch (Exception ex)
            {
                output.WriteLine(string.Format("{0,-45} {1,10} {2,10} {3,10} RENDER FAIL: {4}",
                    templateId, "-", "-", "-", ex.GetType().Name + ": " + Truncate(ex.Message, 40)));
                continue;
            }

            // Tier-3 analyse — in-process.
            var validation = TimeMachineValidator.Validate(rendered, ValidationTier.Analyse);
            var valErr = validation.Errors.Count;
            var valWarn = validation.Warnings.Count;
            totalValidatorErrors += valErr;
            totalValidatorWarnings += valWarn;
            if (valWarn > 0) templatesWithValidatorWarnings++;
            foreach (var w in validation.Warnings)
                warningCodeTally[w.Code] = warningCodeTally.GetValueOrDefault(w.Code) + 1;
            // One-line sample of the first validator error per template (diagnostic —
            // helps interpret the aggregate error count).
            if (valErr > 0 && validation.Errors.FirstOrDefault() is { } firstErr)
            {
                output.WriteLine($"    ↳ first err: {Truncate(firstErr.Message, 120)}");
            }

            // End-to-end: POST /v1/run, then read run detail + state_window warnings.
            int runWarn = 0;
            string notes = string.Empty;
            try
            {
                using var runReq = new HttpRequestMessage(HttpMethod.Post, "/v1/run")
                {
                    Content = new StringContent(rendered, Encoding.UTF8, "text/plain"),
                };
                runReq.Content.Headers.ContentType = new MediaTypeHeaderValue("application/x-yaml");
                var runResp = await http.SendAsync(runReq);
                if (!runResp.IsSuccessStatusCode)
                {
                    notes = $"/v1/run {(int)runResp.StatusCode}";
                }
                else
                {
                    var runJson = await runResp.Content.ReadAsStringAsync();
                    using var runDoc = JsonDocument.Parse(runJson);
                    var runId = runDoc.RootElement.TryGetProperty("runId", out var idEl)
                        ? idEl.GetString()
                        : null;
                    if (runId is null)
                    {
                        notes = "no runId";
                    }
                    else
                    {
                        // Run-detail warnings.
                        var detailResp = await http.GetAsync($"/v1/runs/{Uri.EscapeDataString(runId)}");
                        if (detailResp.IsSuccessStatusCode)
                        {
                            var detailJson = await detailResp.Content.ReadAsStringAsync();
                            runWarn += CountWarnings(detailJson);
                        }

                        // state_window warnings (covers a full-window scan).
                        var binCount = await TryGetBinCount(http, runId);
                        if (binCount > 0)
                        {
                            var windowUrl = $"/v1/runs/{Uri.EscapeDataString(runId)}/state_window?startBin=0&endBin={binCount - 1}";
                            var windowResp = await http.GetAsync(windowUrl);
                            if (windowResp.IsSuccessStatusCode)
                            {
                                var windowJson = await windowResp.Content.ReadAsStringAsync();
                                runWarn += CountWarnings(windowJson);
                            }
                            else
                            {
                                notes = notes.Length > 0 ? notes + "; " : notes;
                                notes += $"state_window {(int)windowResp.StatusCode}";
                            }
                        }
                        else
                        {
                            notes = notes.Length > 0 ? notes + "; " : notes;
                            notes += "no binCount";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                notes = ex.GetType().Name + ": " + Truncate(ex.Message, 40);
            }

            totalRunWarnings += runWarn;
            if (runWarn > 0) templatesWithRunWarnings++;

            output.WriteLine(string.Format("{0,-45} {1,10} {2,10} {3,10} {4}",
                templateId, valErr, valWarn, runWarn, notes));
        }

        output.WriteLine(new string('-', 100));
        output.WriteLine($"Totals: validator-errors={totalValidatorErrors}, " +
            $"validator-warnings={totalValidatorWarnings} across {templatesWithValidatorWarnings}/{yamlFiles.Count} templates; " +
            $"run-warnings={totalRunWarnings} across {templatesWithRunWarnings}/{yamlFiles.Count} templates.");

        if (warningCodeTally.Count > 0)
        {
            output.WriteLine("");
            output.WriteLine("Validator warning codes (across all templates):");
            foreach (var (code, count) in warningCodeTally.OrderByDescending(kv => kv.Value))
                output.WriteLine($"  {count,4}  {code}");
        }
    }

    private static async Task<int> TryGetBinCount(HttpClient http, string runId)
    {
        var resp = await http.GetAsync($"/v1/runs/{Uri.EscapeDataString(runId)}/index");
        if (!resp.IsSuccessStatusCode) return 0;
        var json = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("grid", out var gridEl) &&
            gridEl.TryGetProperty("bins", out var binsEl) &&
            binsEl.TryGetInt32(out var bins))
        {
            return bins;
        }
        return 0;
    }

    private static int CountWarnings(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("warnings", out var warningsEl) &&
            warningsEl.ValueKind == JsonValueKind.Array)
        {
            return warningsEl.GetArrayLength();
        }
        return 0;
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "…";
}
