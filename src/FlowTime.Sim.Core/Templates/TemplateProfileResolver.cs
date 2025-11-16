using System;
using System.Linq;
using FlowTime.Sim.Core.Templates.Exceptions;
using FlowTime.Sim.Core.Templates.Profiles;

namespace FlowTime.Sim.Core.Templates;

/// <summary>
/// Resolves template profile references (builtin or inline) into normalized weight vectors.
/// </summary>
internal static class TemplateProfileResolver
{
    public static ProfileResolution? TryResolve(TemplateProfile? profile, TemplateGrid grid)
    {
        if (profile == null)
        {
            return null;
        }

        ArgumentNullException.ThrowIfNull(grid);

        if (grid.Bins <= 0)
        {
            throw new TemplateValidationException("Template grid.bins must be greater than zero when using profiles.");
        }

        var kind = string.IsNullOrWhiteSpace(profile.Kind)
            ? "builtin"
            : profile.Kind.Trim().ToLowerInvariant();

        return kind switch
        {
            "builtin" => ResolveBuiltin(profile, grid.Bins),
            "inline" => ResolveInline(profile, grid.Bins),
            _ => throw new TemplateValidationException($"Profile kind '{profile.Kind}' is not supported. Use 'builtin' or 'inline'.")
        };
    }

    private static ProfileResolution ResolveBuiltin(TemplateProfile profile, int bins)
    {
        if (string.IsNullOrWhiteSpace(profile.Name))
        {
            throw new TemplateValidationException("Builtin profiles must specify a name.");
        }

        var weights = TimeOfDayProfileLibrary.Resolve(profile.Name, bins);
        return new ProfileResolution(weights, "builtin", profile.Name);
    }

    private static ProfileResolution ResolveInline(TemplateProfile profile, int bins)
    {
        if (profile.Weights == null || profile.Weights.Length == 0)
        {
            throw new TemplateValidationException("Inline profiles must provide a non-empty weights array.");
        }

        if (profile.Weights.Length != bins)
        {
            throw new TemplateValidationException($"Inline profile weights must contain exactly {bins} values to match grid.bins.");
        }

        if (profile.Weights.Any(w => w < 0))
        {
            throw new TemplateValidationException("Inline profile weights must be non-negative.");
        }

        var normalized = ProfileMath.Normalize(profile.Weights);
        return new ProfileResolution(normalized, "inline", null);
    }
}

internal sealed record ProfileResolution(double[] Weights, string Kind, string? Name);
