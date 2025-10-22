using System;
using System.Collections.Generic;
using System.Linq;
using FlowTime.Contracts.TimeTravel;
using FlowTime.Generator;
using FlowTime.Generator.Models;
using Microsoft.AspNetCore.Http;

namespace FlowTime.API.Endpoints;

internal static class TelemetryCaptureEndpoints
{
    public static RouteGroupBuilder MapTelemetryCaptureEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/telemetry/captures", HandleGenerateTelemetryAsync);
        return group;
    }

    private static async Task<IResult> HandleGenerateTelemetryAsync(
        TelemetryCaptureRequest request,
        TelemetryGenerationService generationService,
        IConfiguration configuration,
        ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return Results.BadRequest(new { error = "Request body is required." });
        }

        if (!string.Equals(request.Source.Type ?? "run", "run", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(request.Source.RunId))
        {
            return Results.BadRequest(new { error = "source.type must be 'run' and source.runId is required." });
        }

        var runsRoot = Program.ServiceHelpers.RunsRoot(configuration);
        var telemetryRoot = Program.ServiceHelpers.TelemetryRoot(configuration);

        try
        {
            var result = await generationService.GenerateAsync(
                request.Source.RunId!,
                new TelemetryGenerationOutput(request.Output.CaptureKey, request.Output.Directory, request.Output.Overwrite),
                runsRoot,
                telemetryRoot,
                cancellationToken).ConfigureAwait(false);

            var summary = BuildSummary(result);

            if (result.AlreadyExists && !request.Output.Overwrite)
            {
                logger.LogInformation("Telemetry bundle already exists for run {RunId}", request.Source.RunId);
                return Results.Conflict(new TelemetryCaptureResponse { Capture = summary });
            }

            return Results.Ok(new TelemetryCaptureResponse { Capture = summary });
        }
        catch (DirectoryNotFoundException ex)
        {
            logger.LogWarning(ex, "Source run not found for telemetry generation");
            return Results.NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Telemetry generation failed for run {RunId}", request.Source.RunId);
            return Results.Problem(title: "Telemetry generation failed", detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static TelemetryCaptureSummary BuildSummary(TelemetryGenerationResult result)
    {
        var warnings = ConvertWarnings(result.Warnings);
        return new TelemetryCaptureSummary
        {
            Generated = result.Generated,
            AlreadyExists = result.AlreadyExists,
            GeneratedAtUtc = result.GeneratedAtUtc,
            SourceRunId = result.SourceRunId,
            Warnings = warnings
        };
    }

    private static IReadOnlyList<StateWarning> ConvertWarnings(IReadOnlyList<CaptureWarning> warnings)
    {
        if (warnings is null || warnings.Count == 0)
        {
            return Array.Empty<StateWarning>();
        }

        return warnings
            .Select(w => new StateWarning
            {
                Code = w.Code,
                Message = w.Message,
                Severity = "warning",
                NodeId = w.NodeId
            })
            .ToArray();
    }
}
