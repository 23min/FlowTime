using FlowTime.TimeMachine.Sweep;

namespace FlowTime.API.Endpoints;

internal static class OptimizeEndpoints
{
    public static RouteGroupBuilder MapOptimizeEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/optimize", HandleOptimizeAsync);
        return group;
    }

    /// <summary>
    /// POST /v1/optimize — find the parameter values that minimize or maximize a metric mean.
    ///
    /// Body: { "yaml", "paramIds", "metricSeriesId", "objective", "searchRanges",
    ///         "tolerance"?: double, "maxIterations"?: int }
    ///
    /// searchRanges: { "&lt;paramId&gt;": { "lo": N, "hi": N }, ... }
    /// objective: "minimize" | "maximize" (case-insensitive)
    ///
    /// Response (200): { "paramValues", "achievedMetricMean", "converged", "iterations" }
    ///
    /// Returns 400 for missing/invalid fields (including lo >= hi or unknown objective).
    /// Returns 503 when the Rust engine is not enabled.
    /// </summary>
    private static async Task<IResult> HandleOptimizeAsync(
        OptimizeRequest request,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        // ── Input validation ─────────────────────────────────────────────

        if (string.IsNullOrWhiteSpace(request.Yaml))
            return Results.BadRequest(new { error = "yaml is required and must not be empty." });

        if (request.ParamIds is null || request.ParamIds.Length == 0)
            return Results.BadRequest(new { error = "paramIds is required and must not be empty." });

        if (string.IsNullOrWhiteSpace(request.MetricSeriesId))
            return Results.BadRequest(new { error = "metricSeriesId is required and must not be empty." });

        if (string.IsNullOrWhiteSpace(request.Objective))
            return Results.BadRequest(new { error = "objective is required ('minimize' or 'maximize')." });

        if (!Enum.TryParse<OptimizeObjective>(request.Objective, ignoreCase: true, out var objective))
            return Results.BadRequest(new { error = "objective must be 'minimize' or 'maximize'." });

        if (request.SearchRanges is null || request.SearchRanges.Count == 0)
            return Results.BadRequest(new { error = "searchRanges is required." });

        // Validate each range
        var ranges = new Dictionary<string, SearchRange>(StringComparer.OrdinalIgnoreCase);
        foreach (var paramId in request.ParamIds)
        {
            if (!request.SearchRanges.TryGetValue(paramId, out var dto) || dto is null)
                return Results.BadRequest(new { error = $"searchRanges must contain an entry for '{paramId}'." });

            var lo = dto.Lo ?? 0.0;
            var hi = dto.Hi ?? 0.0;

            if (lo >= hi)
                return Results.BadRequest(new { error = $"searchRanges['{paramId}'].lo must be less than hi." });

            ranges[paramId] = new SearchRange(lo, hi);
        }

        // ── Engine availability check ──────────────────────────────────────

        var optimizer = services.GetService<Optimizer>();
        if (optimizer is null)
        {
            return Results.Problem(
                detail: "The Rust engine is not enabled. Set RustEngine:Enabled=true in configuration.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        var spec = new OptimizeSpec(
            request.Yaml,
            request.ParamIds,
            request.MetricSeriesId,
            objective,
            ranges,
            request.Tolerance ?? 1e-4,
            request.MaxIterations ?? 200);

        var result = await optimizer.OptimizeAsync(spec, cancellationToken);

        return Results.Ok(new OptimizeResponse(
            result.ParamValues,
            result.AchievedMetricMean,
            result.Converged,
            result.Iterations));
    }
}

internal sealed record OptimizeRequest(
    string? Yaml,
    string[]? ParamIds,
    string? MetricSeriesId,
    string? Objective,
    Dictionary<string, SearchRangeDto?>? SearchRanges,
    double? Tolerance,
    int? MaxIterations);

internal sealed record SearchRangeDto(double? Lo, double? Hi);

internal sealed record OptimizeResponse(
    IReadOnlyDictionary<string, double> ParamValues,
    double AchievedMetricMean,
    bool Converged,
    int Iterations);
