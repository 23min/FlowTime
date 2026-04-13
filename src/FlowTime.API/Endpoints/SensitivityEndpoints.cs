using FlowTime.TimeMachine.Sweep;

namespace FlowTime.API.Endpoints;

internal static class SensitivityEndpoints
{
    /// <summary>
    /// Map the sensitivity analysis endpoint onto the given route group.
    /// </summary>
    public static RouteGroupBuilder MapSensitivityEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/sensitivity", HandleSensitivityAsync);
        return group;
    }

    /// <summary>
    /// POST /v1/sensitivity — compute ∂metric_mean/∂param for a set of const-node parameters.
    ///
    /// Body: { "yaml": "...", "paramIds": ["arrivals", "capacity"],
    ///         "metricSeriesId": "queue.queueTimeMs", "perturbation": 0.05 /* optional */ }
    ///
    /// Response (200): { "metricSeriesId", "points": [{ "paramId", "baseValue", "gradient" }] }
    ///   Points sorted by |gradient| descending. Unknown/non-const params are omitted.
    ///
    /// Returns 400 for missing/empty yaml, paramIds, or metricSeriesId.
    /// Returns 503 when the Rust engine is not enabled (RustEngine:Enabled=false).
    /// </summary>
    private static async Task<IResult> HandleSensitivityAsync(
        SensitivityRequest request,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        // Input validation (always checked, even when engine is absent)
        if (string.IsNullOrWhiteSpace(request.Yaml))
            return Results.BadRequest(new { error = "yaml is required and must not be empty." });

        if (request.ParamIds is null || request.ParamIds.Length == 0)
            return Results.BadRequest(new { error = "paramIds must not be null or empty." });

        if (string.IsNullOrWhiteSpace(request.MetricSeriesId))
            return Results.BadRequest(new { error = "metricSeriesId is required and must not be empty." });

        // Engine availability check
        var sensitivityRunner = services.GetService<SensitivityRunner>();
        if (sensitivityRunner is null)
        {
            return Results.Problem(
                detail: "The Rust engine is not enabled. Set RustEngine:Enabled=true in configuration.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        var spec = new SensitivitySpec(
            request.Yaml,
            request.ParamIds,
            request.MetricSeriesId,
            request.Perturbation ?? 0.05);

        var result = await sensitivityRunner.RunAsync(spec, cancellationToken);

        return Results.Ok(new SensitivityResponse(
            MetricSeriesId: result.MetricSeriesId,
            Points: result.Points
                .Select(p => new SensitivityPointDto(p.ParamId, p.BaseValue, p.Gradient))
                .ToArray()
        ));
    }
}

internal sealed record SensitivityRequest(
    string? Yaml,
    string[]? ParamIds,
    string? MetricSeriesId,
    double? Perturbation);

internal sealed record SensitivityResponse(string MetricSeriesId, SensitivityPointDto[] Points);
internal sealed record SensitivityPointDto(string ParamId, double BaseValue, double Gradient);
