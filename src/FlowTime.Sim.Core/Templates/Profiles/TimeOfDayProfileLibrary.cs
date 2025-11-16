using System;
using System.Collections.Generic;
using System.Linq;
using FlowTime.Sim.Core.Templates.Exceptions;

namespace FlowTime.Sim.Core.Templates.Profiles;

/// <summary>
/// Library of reusable 24-hour profiles that can be referenced by PMF nodes.
/// </summary>
internal static class TimeOfDayProfileLibrary
{
    private static readonly Dictionary<string, BuiltInProfile> Profiles = new(StringComparer.OrdinalIgnoreCase)
    {
        ["weekday-office"] = new BuiltInProfile(
            "weekday-office",
            "Dual-peak weekday with morning ingress and evening egress surges.",
            ProfileMath.Normalize(ProfileMath.ExpandHourly(new[]
            {
                0.18, 0.16, 0.15, 0.15, 0.2, 0.3,
                0.45, 0.7, 0.95, 1.15, 1.25, 1.3,
                1.25, 1.15, 1.05, 1.0, 0.95, 0.85,
                0.65, 0.5, 0.38, 0.3, 0.25, 0.2
            })) ),
        ["three-shift"] = new BuiltInProfile(
            "three-shift",
            "24/7 manufacturing cadence with slightly stronger day shift and lighter overnight output.",
            ProfileMath.Normalize(ProfileMath.ExpandHourly(new[]
            {
                0.9, 0.9, 0.9, 0.92, 0.95, 0.98,
                1.0, 1.03, 1.06, 1.1, 1.12, 1.15,
                1.12, 1.08, 1.05, 1.02, 1.0, 0.98,
                0.96, 0.94, 0.92, 0.9, 0.9, 0.9
            })) ),
        ["hub-rush-hour"] = new BuiltInProfile(
            "hub-rush-hour",
            "Transit network with pronounced commuter peaks feeding a central hub.",
            ProfileMath.Normalize(ProfileMath.ExpandHourly(new[]
            {
                0.2, 0.18, 0.17, 0.17, 0.2, 0.35,
                0.7, 1.2, 1.4, 1.25, 1.0, 0.85,
                0.8, 0.85, 0.95, 1.1, 1.35, 1.5,
                1.35, 1.0, 0.65, 0.4, 0.3, 0.22
            })) )
    };

    public static double[] Resolve(string name, int targetBins)
    {
        if (!Profiles.TryGetValue(name, out var profile))
        {
            throw new TemplateValidationException($"Unknown builtin profile '{name}'.");
        }

        var weights = ProfileMath.Resample(profile.Weights, targetBins);
        return ProfileMath.Normalize(weights);
    }

    public static IReadOnlyDictionary<string, string> DescribeBuiltIns() =>
        Profiles.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Description, StringComparer.OrdinalIgnoreCase);
}

internal sealed record BuiltInProfile(string Name, string Description, double[] Weights);

internal static class ProfileMath
{
    public static double[] Normalize(double[] weights)
    {
        if (weights == null || weights.Length == 0)
        {
            throw new TemplateValidationException("Profiles must contain at least one weight.");
        }

        var sum = 0d;
        for (int i = 0; i < weights.Length; i++)
        {
            sum += weights[i];
        }

        var average = sum / weights.Length;
        if (average <= 0)
        {
            throw new TemplateValidationException("Profile weights must have a positive average.");
        }

        var normalized = new double[weights.Length];
        var scale = 1d / average;
        for (int i = 0; i < weights.Length; i++)
        {
            normalized[i] = weights[i] * scale;
        }

        return normalized;
    }

    public static double[] Resample(double[] source, int targetLength)
    {
        if (source == null || source.Length == 0)
        {
            throw new TemplateValidationException("Builtin profiles must provide a non-empty weight vector.");
        }

        if (targetLength <= 0)
        {
            throw new TemplateValidationException("grid.bins must be greater than zero to use profiles.");
        }

        if (source.Length == targetLength)
        {
            return (double[])source.Clone();
        }

        var result = new double[targetLength];
        for (int i = 0; i < targetLength; i++)
        {
            var position = (double)i / targetLength * (source.Length - 1);
            var lowerIndex = (int)Math.Floor(position);
            var upperIndex = Math.Min(lowerIndex + 1, source.Length - 1);
            var fraction = position - lowerIndex;
            result[i] = source[lowerIndex] + (source[upperIndex] - source[lowerIndex]) * fraction;
        }

        return result;
    }

    public static double[] ExpandHourly(double[] hourlyValues)
    {
        if (hourlyValues == null || hourlyValues.Length != 24)
        {
            throw new TemplateValidationException("Builtin profiles must define exactly 24 hourly weights.");
        }

        const int binsPerHour = 60 / 5;
        var values = new double[hourlyValues.Length * binsPerHour];
        for (int hour = 0; hour < hourlyValues.Length; hour++)
        {
            for (int slot = 0; slot < binsPerHour; slot++)
            {
                values[hour * binsPerHour + slot] = hourlyValues[hour];
            }
        }

        return values;
    }
}
