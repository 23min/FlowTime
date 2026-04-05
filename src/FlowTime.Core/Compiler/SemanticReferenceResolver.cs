using System.Globalization;
using FlowTime.Core.Models;

namespace FlowTime.Core.Compiler;

public static class SemanticReferenceResolver
{
    public static CompiledSeriesReference ParseSeriesReference(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Semantic reference must be provided.", nameof(value));
        }

        var trimmed = value.Trim();
        if (trimmed.Equals("self", StringComparison.OrdinalIgnoreCase))
        {
            return new CompiledSeriesReference
            {
                Kind = CompiledSeriesReferenceKind.Self,
                RawText = "self"
            };
        }

        if (trimmed.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
        {
            return new CompiledSeriesReference
            {
                Kind = CompiledSeriesReferenceKind.File,
                RawText = trimmed,
                FilePath = trimmed["file:".Length..]
            };
        }

        var seriesText = trimmed.StartsWith("series:", StringComparison.OrdinalIgnoreCase)
            ? trimmed["series:".Length..]
            : trimmed;

        var at = seriesText.IndexOf('@');
        var nodeId = at >= 0 ? seriesText[..at].Trim() : seriesText.Trim();
        var classId = at >= 0 ? seriesText[(at + 1)..].Trim() : null;
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            throw new ArgumentException($"Semantic reference '{value}' does not contain a node id.", nameof(value));
        }

        return new CompiledSeriesReference
        {
            Kind = CompiledSeriesReferenceKind.Node,
            RawText = trimmed,
            NodeId = nodeId,
            ClassId = string.IsNullOrWhiteSpace(classId) ? null : classId
        };
    }

    public static CompiledSeriesReference? ParseOptionalSeriesReference(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : ParseSeriesReference(value);
    }

    public static CompiledParallelismReference? ParseParallelismReference(object? value)
    {
        if (value is null)
        {
            return null;
        }

        if (value is string text)
        {
            var trimmed = text.Trim();
            if (trimmed.Length == 0)
            {
                return null;
            }

            if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) &&
                double.IsFinite(parsed) &&
                parsed > 0d)
            {
                return new CompiledParallelismReference
                {
                    RawText = trimmed,
                    Constant = parsed
                };
            }

            return new CompiledParallelismReference
            {
                RawText = trimmed,
                Series = ParseSeriesReference(trimmed)
            };
        }

        if (value is IConvertible)
        {
            var parsed = Convert.ToDouble(value, CultureInfo.InvariantCulture);
            if (!double.IsFinite(parsed) || parsed <= 0d)
            {
                return null;
            }

            return new CompiledParallelismReference
            {
                RawText = parsed.ToString("G", CultureInfo.InvariantCulture),
                Constant = parsed
            };
        }

        throw new ArgumentException("Parallelism must be a numeric literal or series reference.", nameof(value));
    }
}