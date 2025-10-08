using System;
using System.IO;

namespace FlowTime.Core.DataSources;

public static class UriResolver
{
    public static string ResolveFilePath(string uri, string? modelDirectory)
    {
        if (string.IsNullOrWhiteSpace(uri))
            throw new ArgumentException("URI must be provided", nameof(uri));

        if (!uri.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException($"Unsupported URI scheme for '{uri}'. Only file: URIs are supported.");

        var path = uri.Substring("file:".Length);

        if (Path.IsPathRooted(path))
            return path;

        if (string.IsNullOrEmpty(modelDirectory))
            throw new InvalidOperationException("Relative file URIs require a model directory for resolution.");

        return Path.Combine(modelDirectory, path);
    }
}
