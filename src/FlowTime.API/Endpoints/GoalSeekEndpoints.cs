using FlowTime.TimeMachine.Sweep;

namespace FlowTime.API.Endpoints;

internal static class GoalSeekEndpoints
{
    public static RouteGroupBuilder MapGoalSeekEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/goal-seek", HandleGoalSeekAsync);
        return group;
    }

    /// <summary>
    /// POST /v1/goal-seek — find the parameter value that drives a metric mean to a target.
    ///
    /// Body: { "yaml", "paramId", "metricSeriesId", "target", "searchLo", "searchHi",
    ///         "tolerance"?: double, "maxIterations"?: int }
    ///
    /// Response (200): { "paramValue", "achievedMetricMean", "converged", "iterations" }
    ///
    /// Returns 400 for missing/invalid fields (including searchLo ≥ searchHi).
    /// Returns 503 when the Rust engine is not enabled.
    /// </summary>
    private static async Task<IResult> HandleGoalSeekAsync(
        GoalSeekRequest request,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        // Input validation
        if (string.IsNullOrWhiteSpace(request.Yaml))
            return Results.BadRequest(new { error = "yaml is required and must not be empty." });

        if (string.IsNullOrWhiteSpace(request.ParamId))
            return Results.BadRequest(new { error = "paramId is required and must not be empty." });

        if (string.IsNullOrWhiteSpace(request.MetricSeriesId))
            return Results.BadRequest(new { error = "metricSeriesId is required and must not be empty." });

        if (request.SearchLo is null || request.SearchHi is null)
            return Results.BadRequest(new { error = "searchLo and searchHi are required." });

        if (request.SearchLo.Value >= request.SearchHi.Value)
            return Results.BadRequest(new { error = "searchLo must be less than searchHi." });

        // Engine availability check
        var goalSeeker = services.GetService<GoalSeeker>();
        if (goalSeeker is null)
        {
            return Results.Problem(
                detail: "The Rust engine is not enabled. Set RustEngine:Enabled=true in configuration.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        var spec = new GoalSeekSpec(
            request.Yaml,
            request.ParamId,
            request.MetricSeriesId,
            request.Target ?? 0.0,
            request.SearchLo.Value,
            request.SearchHi.Value,
            request.Tolerance ?? 1e-6,
            request.MaxIterations ?? 50);

        var result = await goalSeeker.SeekAsync(spec, cancellationToken);

        return Results.Ok(new GoalSeekResponse(
            result.ParamValue,
            result.AchievedMetricMean,
            result.Converged,
            result.Iterations,
            result.Trace));
    }
}

internal sealed record GoalSeekRequest(
    string? Yaml,
    string? ParamId,
    string? MetricSeriesId,
    double? Target,
    double? SearchLo,
    double? SearchHi,
    double? Tolerance,
    int? MaxIterations);

internal sealed record GoalSeekResponse(
    double ParamValue,
    double AchievedMetricMean,
    bool Converged,
    int Iterations,
    IReadOnlyList<GoalSeekTracePoint> Trace);
