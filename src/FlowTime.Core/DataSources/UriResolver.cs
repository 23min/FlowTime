using System;
using System.IO;

namespace FlowTime.Core.DataSources;

public static class UriResolver
{
    public static string ResolveFilePath(string uri, string? modelDirectory)
    {
        if (string.IsNullOrWhiteSpace(uri))
            throw new ArgumentException("URI must be provided", nameof(uri));

        if (Uri.TryCreate(uri, UriKind.Absolute, out var parsed) &&
            string.Equals(parsed.Scheme, "file", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrEmpty(parsed.Host))
            {
                // Treat file://telemetry/... as relative to the model directory
                if (parsed.Host.Equals("telemetry", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrWhiteSpace(modelDirectory))
                    {
                        throw new InvalidOperationException("Telemetry URIs require a model directory for resolution.");
                    }

                    var relative = parsed.AbsolutePath.TrimStart('/');
                    var combined = Path.Combine(modelDirectory, parsed.Host, relative.Replace('/', Path.DirectorySeparatorChar));
                    return Path.GetFullPath(combined);
                }

                var uncPath = $"//{parsed.Host}{parsed.AbsolutePath}";
                return Path.GetFullPath(uncPath.Replace('/', Path.DirectorySeparatorChar));
            }

            var localPath = parsed.LocalPath;
            if (string.IsNullOrEmpty(localPath))
            {
                localPath = uri.Substring("file:".Length);
            }

            var normalized = localPath.Replace('/', Path.DirectorySeparatorChar);

            if (Path.IsPathRooted(normalized))
            {
                return normalized;
            }

            if (string.IsNullOrEmpty(modelDirectory))
                throw new InvalidOperationException("Relative file URIs require a model directory for resolution.");

            return Path.Combine(modelDirectory, normalized);
        }

        if (uri.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
        {
            var segment = uri.Substring("file:".Length);

            if (segment.StartsWith("//telemetry/", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(modelDirectory))
                {
                    throw new InvalidOperationException("Telemetry URIs require a model directory for resolution.");
                }

                var relative = segment.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
                return Path.GetFullPath(Path.Combine(modelDirectory, relative));
            }

            var normalized = segment.Replace('/', Path.DirectorySeparatorChar);
            if (Path.IsPathRooted(normalized))
            {
                return Path.GetFullPath(normalized);
            }

            if (string.IsNullOrEmpty(modelDirectory))
                throw new InvalidOperationException("Relative file URIs require a model directory for resolution.");

            var trimmed = normalized.TrimStart(Path.DirectorySeparatorChar);
            return Path.Combine(modelDirectory, trimmed);
        }

        throw new NotSupportedException($"Unsupported URI scheme for '{uri}'. Only file: URIs are supported.");
    }
}
