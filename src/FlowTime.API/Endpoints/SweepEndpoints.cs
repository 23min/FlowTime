using FlowTime.TimeMachine.Sweep;

namespace FlowTime.API.Endpoints;

internal static class SweepEndpoints
{
    /// <summary>
    /// Map the parameter sweep endpoint onto the given route group.
    /// </summary>
    public static RouteGroupBuilder MapSweepEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/sweep", HandleSweepAsync);
        return group;
    }

    /// <summary>
    /// POST /v1/sweep — evaluate a model YAML over a range of parameter values.
    ///
    /// Body: { "yaml": "...", "paramId": "arrivals", "values": [10, 20, 30],
    ///         "captureSeriesIds": ["arrivals", "served"] /* optional */ }
    ///
    /// Response (200): { "paramId", "points": [{ "paramValue", "series": { id: double[] } }] }
    ///
    /// Returns 400 for missing/empty yaml, paramId, or values.
    /// Returns 503 when the Rust engine is not enabled (RustEngine:Enabled=false).
    /// </summary>
    private static async Task<IResult> HandleSweepAsync(
        SweepRequest request,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        // Input validation (always checked, even when engine is absent)
        if (string.IsNullOrWhiteSpace(request.Yaml))
            return Results.BadRequest(new { error = "yaml is required and must not be empty." });

        if (string.IsNullOrWhiteSpace(request.ParamId))
            return Results.BadRequest(new { error = "paramId is required and must not be empty." });

        if (request.Values is null || request.Values.Length == 0)
            return Results.BadRequest(new { error = "values must not be null or empty." });

        // Engine availability check
        var sweepRunner = services.GetService<SweepRunner>();
        if (sweepRunner is null)
        {
            return Results.Problem(
                detail: "The Rust engine is not enabled. Set RustEngine:Enabled=true in configuration.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        var spec = new SweepSpec(
            request.Yaml,
            request.ParamId,
            request.Values,
            request.CaptureSeriesIds);

        var result = await sweepRunner.RunAsync(spec, cancellationToken);

        return Results.Ok(new SweepResponse(
            ParamId: result.ParamId,
            Points: result.Points
                .Select(p => new SweepPointDto(
                    p.ParamValue,
                    p.Series.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)))
                .ToArray()
        ));
    }
}

internal sealed record SweepRequest(
    string? Yaml,
    string? ParamId,
    double[]? Values,
    string[]? CaptureSeriesIds);

internal sealed record SweepResponse(string ParamId, SweepPointDto[] Points);

internal sealed record SweepPointDto(double ParamValue, Dictionary<string, double[]> Series);
