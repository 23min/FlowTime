using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FlowTime.Contracts.TimeTravel;
using FlowTime.Generator.Orchestration;

namespace FlowTime.API.Endpoints;

internal static class RunOrchestrationEndpoints
{
    public static RouteGroupBuilder MapRunOrchestrationEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/runs", HandleListRunsAsync);
        group.MapGet("/runs/{runId}", HandleGetRunAsync);
        return group;
    }

    private static async Task<IResult> HandleListRunsAsync(
        HttpContext context,
        RunOrchestrationService orchestration,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var runsRoot = Program.ServiceHelpers.RunsRoot(configuration);
        if (!Directory.Exists(runsRoot))
        {
            return Results.Ok(new RunSummaryResponse());
        }

        var query = context.Request.Query;
        var modeFilter = query.TryGetValue("mode", out var modeValues) ? modeValues.ToString() : null;
        var templateFilter = query.TryGetValue("templateId", out var templateValues) ? templateValues.ToString() : null;

        bool? hasWarningsFilter = null;
        if (query.TryGetValue("hasWarnings", out var warningsValues) && warningsValues.Count > 0)
        {
            if (bool.TryParse(warningsValues.ToString(), out var parsedWarnings))
            {
                hasWarningsFilter = parsedWarnings;
            }
            else
            {
                return Results.BadRequest(new { error = "hasWarnings must be 'true' or 'false'." });
            }
        }

        const int defaultPage = 1;
        const int defaultPageSize = 50;
        const int maxPageSize = 200;

        var page = defaultPage;
        if (query.TryGetValue("page", out var pageValues) && pageValues.Count > 0)
        {
            if (!int.TryParse(pageValues.ToString(), out page) || page < 1)
            {
                return Results.BadRequest(new { error = "page must be an integer greater than 0." });
            }
        }

        var pageSize = defaultPageSize;
        if (query.TryGetValue("pageSize", out var pageSizeValues) && pageSizeValues.Count > 0)
        {
            if (!int.TryParse(pageSizeValues.ToString(), out pageSize) || pageSize < 1)
            {
                return Results.BadRequest(new { error = "pageSize must be an integer greater than 0." });
            }

            pageSize = Math.Min(pageSize, maxPageSize);
        }

        var items = new List<RunSummary>();
        foreach (var directory in Directory.EnumerateDirectories(runsRoot))
        {
            try
            {
                var result = await orchestration.TryLoadRunAsync(directory, cancellationToken).ConfigureAwait(false);
                if (result is null)
                {
                    continue;
                }

                items.Add(RunOrchestrationContractMapper.BuildRunSummary(result));
            }
            catch
            {
                // ignore invalid runs to keep listing resilient
            }
        }

        items.Sort((a, b) => Nullable.Compare(b.CreatedUtc, a.CreatedUtc));

        IEnumerable<RunSummary> filtered = items;

        if (!string.IsNullOrWhiteSpace(modeFilter))
        {
            filtered = filtered.Where(item => string.Equals(item.Mode, modeFilter, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(templateFilter))
        {
            filtered = filtered.Where(item => string.Equals(item.TemplateId, templateFilter, StringComparison.OrdinalIgnoreCase));
        }

        if (hasWarningsFilter.HasValue)
        {
            filtered = filtered.Where(item => (item.WarningCount > 0) == hasWarningsFilter.Value);
        }

        var filteredList = filtered.ToList();
        var totalCount = filteredList.Count;

        var skip = (page - 1) * pageSize;
        if (skip >= totalCount)
        {
            filteredList = new List<RunSummary>();
        }
        else if (skip > 0)
        {
            filteredList = filteredList.Skip(skip).Take(pageSize).ToList();
        }
        else
        {
            filteredList = filteredList.Take(pageSize).ToList();
        }

        return Results.Ok(new RunSummaryResponse
        {
            Items = filteredList,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        });
    }

    private static async Task<IResult> HandleGetRunAsync(
        string runId,
        RunOrchestrationService orchestration,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(runId))
        {
            return Results.BadRequest(new { error = "runId is required." });
        }

        var runsRoot = Program.ServiceHelpers.RunsRoot(configuration);
        var runDirectory = Path.Combine(runsRoot, runId);

        var result = await orchestration.TryLoadRunAsync(runDirectory, cancellationToken).ConfigureAwait(false);
        if (result is null)
        {
            return Results.NotFound(new { error = $"Run '{runId}' not found." });
        }

        var metadata = RunOrchestrationContractMapper.BuildStateMetadata(result);
        var warnings = RunOrchestrationContractMapper.BuildStateWarnings(result.TelemetryManifest);
        var canReplay = RunOrchestrationContractMapper.DetermineCanReplay(result);
        return Results.Ok(new RunCreateResponse
        {
            IsDryRun = false,
            Metadata = metadata,
            Warnings = warnings,
            CanReplay = canReplay,
            Telemetry = RunOrchestrationContractMapper.BuildTelemetrySummary(result)
        });
    }
}
