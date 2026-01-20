using System.IO;
using FlowTime.Contracts.TimeTravel;
using FlowTime.Contracts.Storage;
using FlowTime.Generator.Orchestration;
using FlowTime.Sim.Service;
using FlowTime.Sim.Core.Templates.Exceptions;

namespace FlowTime.Sim.Service.Extensions;

internal static class RunOrchestrationEndpointExtensions
{
    public static RouteGroupBuilder MapRunOrchestrationEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/runs", HandleCreateRunAsync);
        return group;
    }

    private static async Task<IResult> HandleCreateRunAsync(
        RunCreateRequest request,
        RunOrchestrationService orchestration,
        IConfiguration configuration,
        IStorageBackend storage,
        ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return Results.BadRequest(new { error = "Request body is required." });
        }

        if (string.IsNullOrWhiteSpace(request.TemplateId))
        {
            return Results.BadRequest(new { error = "templateId is required." });
        }

        var mode = string.IsNullOrWhiteSpace(request.Mode) ? "telemetry" : request.Mode.Trim().ToLowerInvariant();
        if (mode is not ("telemetry" or "simulation"))
        {
            return Results.BadRequest(new { error = "mode must be 'telemetry' or 'simulation'." });
        }

        if (mode == "telemetry" && (request.Telemetry?.CaptureDirectory is null || string.IsNullOrWhiteSpace(request.Telemetry.CaptureDirectory)))
        {
            return Results.BadRequest(new { error = "telemetry.captureDirectory is required for telemetry runs." });
        }

        var runsRoot = Path.Combine(global::Program.ServiceHelpers.DataRoot(configuration), "runs");
        Directory.CreateDirectory(runsRoot);
        var parameters = RunOrchestrationContractMapper.ConvertParameters(request.Parameters);
        var telemetryBindings = RunOrchestrationContractMapper.CloneTelemetryBindings(request.Telemetry);

        var orchestrationRequest = new RunOrchestrationRequest
        {
            TemplateId = request.TemplateId,
            Mode = mode,
            CaptureDirectory = request.Telemetry?.CaptureDirectory,
            TelemetryBindings = telemetryBindings,
            Parameters = parameters,
            OutputRoot = runsRoot,
            DeterministicRunId = request.Options?.DeterministicRunId ?? false,
            RunId = request.Options?.RunId,
            DryRun = request.Options?.DryRun ?? false,
            OverwriteExisting = request.Options?.OverwriteExisting ?? false,
            Rng = request.Rng
        };

        try
        {
            var outcome = await orchestration.CreateRunAsync(orchestrationRequest, cancellationToken).ConfigureAwait(false);
            if (outcome.IsDryRun)
            {
                var plan = outcome.Plan ?? throw new InvalidOperationException("Dry-run outcome missing plan details.");
                return Results.Ok(new RunCreateResponse
                {
                    IsDryRun = true,
                    Plan = RunOrchestrationContractMapper.BuildPlan(plan),
                    Warnings = Array.Empty<StateWarning>(),
                    CanReplay = false,
                    Telemetry = null,
                    WasReused = false
                });
            }

            var result = outcome.Result ?? throw new InvalidOperationException("Run outcome missing result payload.");
            var metadata = RunOrchestrationContractMapper.BuildStateMetadata(result);
            var warnings = RunOrchestrationContractMapper.BuildStateWarnings(result.TelemetryManifest);
            var canReplay = RunOrchestrationContractMapper.DetermineCanReplay(result);
            var archive = Program.BuildRunArchive(result.RunDirectory);
            var bundleWrite = await storage.WriteAsync(new StorageWriteRequest
            {
                Kind = StorageKind.Run,
                Id = metadata.RunId,
                Content = archive,
                ContentType = "application/zip",
                Metadata = new Dictionary<string, string>
                {
                    ["templateId"] = metadata.TemplateId,
                    ["mode"] = metadata.Mode
                }
            }, cancellationToken).ConfigureAwait(false);

            logger.LogInformation("Run {RunId} created for template {TemplateId} (mode={Mode}, reused={Reused})", metadata.RunId, metadata.TemplateId, metadata.Mode, result.WasReused);

            return Results.Created($"/api/v1/orchestration/runs/{metadata.RunId}", new RunCreateResponse
            {
                IsDryRun = false,
                Metadata = metadata,
                Warnings = warnings,
                CanReplay = canReplay,
                Telemetry = RunOrchestrationContractMapper.BuildTelemetrySummary(result),
                BundleRef = bundleWrite.Reference,
                WasReused = result.WasReused
            });
        }
        catch (TemplateValidationException ex)
        {
            logger.LogWarning(ex, "Template validation failed for template {TemplateId}", request.TemplateId);
            return Results.BadRequest(new { error = ex.Message });
        }
        catch (ArgumentException ex) when (
            ex.Message.Contains("Template not found", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("was not found", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning(ex, "Template {TemplateId} was not found", request.TemplateId);
            return Results.NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "Run validation failed for template {TemplateId}", request.TemplateId);
            return Results.BadRequest(new { error = ex.Message });
        }
        catch (FileNotFoundException ex)
        {
            logger.LogWarning(ex, "Missing capture artifact for template {TemplateId}", request.TemplateId);
            return Results.Problem(
                title: "Capture artifacts missing",
                detail: ex.Message,
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create run for template {TemplateId}", request.TemplateId);
            return Results.Problem(title: "Run creation failed", detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}
