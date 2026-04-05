using System.Globalization;
using System.IO;

namespace FlowTime.Core.Models;

public enum CompiledSeriesReferenceKind
{
    Node,
    File,
    Self
}

public sealed record CompiledSeriesReference
{
    public required CompiledSeriesReferenceKind Kind { get; init; }
    public required string RawText { get; init; }
    public string? NodeId { get; init; }
    public string? ClassId { get; init; }
    public string? FilePath { get; init; }

    public string CanonicalText => Kind switch
    {
        CompiledSeriesReferenceKind.Self => "self",
        CompiledSeriesReferenceKind.File => RawText,
        CompiledSeriesReferenceKind.Node when string.IsNullOrWhiteSpace(ClassId) => NodeId ?? string.Empty,
        CompiledSeriesReferenceKind.Node => $"{NodeId}@{ClassId}",
        _ => RawText
    };

    public string LookupKey => Kind switch
    {
        CompiledSeriesReferenceKind.Self => "self",
        CompiledSeriesReferenceKind.Node => NodeId ?? string.Empty,
        CompiledSeriesReferenceKind.File => GetFileStem(FilePath ?? RawText),
        _ => RawText
    };

    public string? ProducerIdCandidate => Kind switch
    {
        CompiledSeriesReferenceKind.Self => "self",
        CompiledSeriesReferenceKind.Node => NodeId,
        CompiledSeriesReferenceKind.File => TrimClassSuffix(GetFileStem(FilePath ?? RawText)),
        _ => null
    };

    private static string GetFileStem(string path)
    {
        var value = path.Trim();
        if (value.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
        {
            value = value["file:".Length..];
        }

        return Path.GetFileNameWithoutExtension(value.Replace('\\', '/'));
    }

    private static string TrimClassSuffix(string value)
    {
        var at = value.IndexOf('@');
        return at > 0 ? value[..at] : value;
    }
}

public sealed record CompiledParallelismReference
{
    public string? RawText { get; init; }
    public double? Constant { get; init; }
    public CompiledSeriesReference? Series { get; init; }

    public string? CanonicalText => Constant.HasValue
        ? Constant.Value.ToString("G", CultureInfo.InvariantCulture)
        : Series?.CanonicalText ?? RawText;
}