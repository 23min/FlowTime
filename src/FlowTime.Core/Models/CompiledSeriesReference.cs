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

    public string ToAuthoredValue() => RawText;

    public string? ResolveProducerId(string ownerNodeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerNodeId);

        return Kind switch
        {
            CompiledSeriesReferenceKind.Self => ownerNodeId,
            CompiledSeriesReferenceKind.Node => NodeId,
            _ => null
        };
    }

    private static string GetFileStem(string path)
    {
        var value = path.Trim();
        if (value.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
        {
            value = value["file:".Length..];
        }

        return Path.GetFileNameWithoutExtension(value.Replace('\\', '/'));
    }
}