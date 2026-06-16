using System;
using System.IO;

namespace FlowTime.Contracts.Storage;

public static class RunPathResolver
{
    private static readonly char[] PathSeparators = { '/', '\\' };

    public static string GetSafeRunDirectory(string artifactsDirectory, string runId)
    {
        if (string.IsNullOrWhiteSpace(artifactsDirectory))
        {
            throw new ArgumentException("artifactsDirectory must be provided.", nameof(artifactsDirectory));
        }

        if (string.IsNullOrWhiteSpace(runId))
        {
            throw new ArgumentException("runId must be provided.", nameof(runId));
        }

        if (runId.IndexOfAny(PathSeparators) >= 0)
        {
            throw new ArgumentException($"runId '{runId}' must not contain path separators.", nameof(runId));
        }

        if (runId == "." || runId == "..")
        {
            throw new ArgumentException($"runId '{runId}' is not a valid run identifier.", nameof(runId));
        }

        string rootFull;
        try
        {
            rootFull = Path.GetFullPath(artifactsDirectory);
        }
        catch (Exception ex) when (ex is ArgumentException || ex is PathTooLongException || ex is NotSupportedException)
        {
            throw new ArgumentException($"artifactsDirectory '{artifactsDirectory}' is not a valid path.", nameof(artifactsDirectory), ex);
        }

        var rootWithSeparator = rootFull.EndsWith(Path.DirectorySeparatorChar)
            ? rootFull
            : rootFull + Path.DirectorySeparatorChar;

        string candidate;
        try
        {
            candidate = Path.GetFullPath(Path.Combine(rootWithSeparator, runId));
        }
        catch (Exception ex) when (ex is ArgumentException || ex is PathTooLongException || ex is NotSupportedException)
        {
            throw new ArgumentException($"runId '{runId}' is not a valid path segment.", nameof(runId), ex);
        }

        var candidateWithSeparator = candidate.EndsWith(Path.DirectorySeparatorChar)
            ? candidate
            : candidate + Path.DirectorySeparatorChar;

        if (!candidateWithSeparator.StartsWith(rootWithSeparator, StringComparison.Ordinal))
        {
            throw new ArgumentException($"runId '{runId}' resolves outside the artifacts directory.", nameof(runId));
        }

        return candidate.TrimEnd(Path.DirectorySeparatorChar);
    }
}
