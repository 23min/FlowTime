using FlowTime.TimeMachine.Validation;

namespace FlowTime.API.Endpoints;

internal static class ValidationEndpoints
{
    /// <summary>
    /// Map the tiered validation endpoint onto the given route group.
    /// </summary>
    public static RouteGroupBuilder MapValidationEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/validate", HandleValidateAsync);
        return group;
    }

    /// <summary>
    /// POST /v1/validate — validate a model YAML at the specified tier.
    ///
    /// Body: { "yaml": "...", "tier": "schema" | "compile" | "analyse" }
    /// Response (always 200): { "tier", "isValid", "errors": [...], "warnings": [...] }
    ///
    /// Returns 400 for missing/empty yaml or unrecognized tier.
    /// </summary>
    private static IResult HandleValidateAsync(ValidationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Yaml))
        {
            return Results.BadRequest(new { error = "yaml is required and must not be empty." });
        }

        if (!TryParseTier(request.Tier, out var tier))
        {
            return Results.BadRequest(new
            {
                error = $"Invalid tier '{request.Tier}'. Valid values: schema, compile, analyse.",
            });
        }

        var result = TimeMachineValidator.Validate(request.Yaml, tier);

        return Results.Ok(new ValidationResponse(
            Tier: result.Tier.ToString().ToLowerInvariant(),
            IsValid: result.IsValid,
            Errors: result.Errors.Select(e => new ValidationErrorDto(e.Message)).ToList(),
            Warnings: result.Warnings.Select(w => new ValidationWarningDto(w.NodeId, w.Code, w.Message)).ToList()
        ));
    }

    private static bool TryParseTier(string? value, out ValidationTier tier)
    {
        tier = default;
        if (string.IsNullOrWhiteSpace(value)) return false;
        return Enum.TryParse(value, ignoreCase: true, out tier);
    }
}

internal sealed record ValidationRequest(string? Yaml, string? Tier);
internal sealed record ValidationResponse(string Tier, bool IsValid, List<ValidationErrorDto> Errors, List<ValidationWarningDto> Warnings);
internal sealed record ValidationErrorDto(string Message);
internal sealed record ValidationWarningDto(string NodeId, string Code, string Message);
