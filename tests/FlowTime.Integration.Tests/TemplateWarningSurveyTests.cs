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
/// E-24 schema-reality regression canary — iterates every template under the repo's
/// `templates/` directory, renders it with default parameters, runs tier-3 analyse
/// validation in-process, and (when the Engine API is reachable) posts the rendered
/// YAML and collects any warnings that arrive on run-detail + state_window responses.
///
/// <para>
/// <b>Hard assertion (m-E24-05):</b> the in-process tier-3 validator must report
/// <c>val-err = 0</c> across every shipped template. A non-zero count fails the
/// build. The diagnostic histogram is retained as <see cref="ITestOutputHelper"/>
/// output for actionable failures, but the gate is the assertion.
/// </para>
///
/// <para>
/// <b>Graceful-skip discipline:</b> the in-process validator portion is independent
/// of any live API and runs unconditionally. Only the live-API survey
/// (<c>POST /v1/run</c> + run-detail + state_window warnings) is skipped when the
/// Engine API is unreachable at <c>http://localhost:8081</c>. The skip applies to
/// infrastructure absence only, never to assertion failures (per m-E24-05 spec).
/// </para>
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
        var templatesDir = GetRepoPath("templates");
        var yamlFiles = Directory.EnumerateFiles(templatesDir, "*.yaml", SearchOption.TopDirectoryOnly)
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

        Assert.NotEmpty(yamlFiles);

        // ── Phase 1: in-process tier-3 (Analyse) validator survey + hard assertion. ──
        // This phase does not depend on a live Engine API. It is the regression gate
        // for E-24's schema-reality convergence: every shipped template must render
        // and validate cleanly under `TimeMachineValidator.Validate(..., Analyse)`.

        output.WriteLine($"Surveying {yamlFiles.Count} templates from {templatesDir}");
        output.WriteLine(new string('=', 100));

        int totalValidatorErrors = 0;
        int totalValidatorWarnings = 0;
        int templatesWithValidatorWarnings = 0;
        var warningCodeTally = new Dictionary<string, int>();
        var perTemplate = new List<(string templateId, int valErr, int valWarn, string? firstErrMessage, string? renderFailure)>();

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
                var renderErr = ex.GetType().Name + ": " + Truncate(ex.Message, 200);
                perTemplate.Add((templateId, 0, 0, null, renderErr));
                continue;
            }

            // Tier-3 analyse — in-process; no live API needed.
            var validation = TimeMachineValidator.Validate(rendered, ValidationTier.Analyse);
            var valErr = validation.Errors.Count;
            var valWarn = validation.Warnings.Count;
            totalValidatorErrors += valErr;
            totalValidatorWarnings += valWarn;
            if (valWarn > 0) templatesWithValidatorWarnings++;
            foreach (var w in validation.Warnings)
                warningCodeTally[w.Code] = warningCodeTally.GetValueOrDefault(w.Code) + 1;

            var firstErrMessage = valErr > 0 && validation.Errors.FirstOrDefault() is { } firstErr
                ? firstErr.Message
                : null;
            perTemplate.Add((templateId, valErr, valWarn, firstErrMessage, null));
        }

        // Diagnostic table.
        output.WriteLine(string.Format("{0,-45} {1,10} {2,10} {3}",
            "template", "val-err", "val-warn", "notes"));
        output.WriteLine(new string('-', 100));
        foreach (var row in perTemplate)
        {
            if (row.renderFailure is not null)
            {
                output.WriteLine(string.Format("{0,-45} {1,10} {2,10} RENDER FAIL: {3}",
                    row.templateId, "-", "-", row.renderFailure));
            }
            else
            {
                output.WriteLine(string.Format("{0,-45} {1,10} {2,10}",
                    row.templateId, row.valErr, row.valWarn));
                if (row.firstErrMessage is not null)
                {
                    output.WriteLine($"    ↳ first err: {Truncate(row.firstErrMessage, 200)}");
                }
            }
        }
        output.WriteLine(new string('-', 100));
        output.WriteLine($"Totals: validator-errors={totalValidatorErrors}, " +
            $"validator-warnings={totalValidatorWarnings} across {templatesWithValidatorWarnings}/{yamlFiles.Count} templates.");

        if (warningCodeTally.Count > 0)
        {
            output.WriteLine("");
            output.WriteLine("Validator warning codes (across all templates):");
            foreach (var (code, count) in warningCodeTally.OrderByDescending(kv => kv.Value))
                output.WriteLine($"  {count,4}  {code}");
        }

        // Hard assertion (m-E24-05 AC1). Fails the build on any non-zero val-err.
        // Render failures (template that did not render at all) are also a regression
        // — they prevent validation from running, which is functionally worse than a
        // validator error.
        var renderFailures = perTemplate.Where(r => r.renderFailure is not null).ToList();
        var offenders = perTemplate
            .Where(r => r.valErr > 0)
            .Select(r => $"{r.templateId} (val-err={r.valErr}; first: {Truncate(r.firstErrMessage ?? "<none>", 120)})")
            .ToList();

        Assert.True(renderFailures.Count == 0,
            $"Expected every template to render under default parameters; {renderFailures.Count} failed: " +
            string.Join("; ", renderFailures.Select(r => $"{r.templateId} → {r.renderFailure}")));

        Assert.True(totalValidatorErrors == 0,
            $"Expected val-err=0 across {yamlFiles.Count} templates at ValidationTier.Analyse; " +
            $"got {totalValidatorErrors} errors across {offenders.Count} templates. " +
            $"Offenders: {string.Join(" | ", offenders)}");

        // ── Phase 2: optional live-API survey for run-warn diagnostic. ──
        // Skipped (early-return after the hard assertion has already passed) when
        // the Engine API is not reachable. This phase produces evidence-only output;
        // it does not gate the build. The hard assertion above is the gate.

        using var http = new HttpClient { BaseAddress = new Uri(EngineBaseUrl), Timeout = TimeSpan.FromSeconds(30) };

        try
        {
            var probe = await http.GetAsync("/v1/healthz");
            if (!probe.IsSuccessStatusCode)
            {
                output.WriteLine("");
                output.WriteLine($"SKIP live-API survey: Engine API probe returned {probe.StatusCode}.");
                return;
            }
        }
        catch (HttpRequestException ex)
        {
            output.WriteLine("");
            output.WriteLine($"SKIP live-API survey: Engine API not reachable at {EngineBaseUrl} — {ex.Message}");
            return;
        }

        output.WriteLine("");
        output.WriteLine("=== Live-API survey (POST /v1/run + run-detail + state_window warnings) ===");
        output.WriteLine(string.Format("{0,-45} {1,10} {2}", "template", "run-warn", "notes"));
        output.WriteLine(new string('-', 100));

        int totalRunWarnings = 0;
        int templatesWithRunWarnings = 0;

        foreach (var row in perTemplate.Where(r => r.renderFailure is null))
        {
            var path = yamlFiles.First(p => Path.GetFileNameWithoutExtension(p) == row.templateId);
            var templateYaml = await File.ReadAllTextAsync(path);
            var service = new TemplateService(
                new Dictionary<string, string> { [row.templateId] = templateYaml },
                NullLogger<TemplateService>.Instance);
            var rendered = await service.GenerateEngineModelAsync(row.templateId, new Dictionary<string, object>());

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

            output.WriteLine(string.Format("{0,-45} {1,10} {2}", row.templateId, runWarn, notes));
        }

        output.WriteLine(new string('-', 100));
        output.WriteLine($"Totals: run-warnings={totalRunWarnings} across {templatesWithRunWarnings}/{yamlFiles.Count} templates.");
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
