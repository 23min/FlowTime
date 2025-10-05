using FlowTime.Contracts.Services;

namespace FlowTime.Cli.Formatting;

/// <summary>
/// Formats artifact lists as console tables.
/// </summary>
public static class ArtifactTableFormatter
{
    /// <summary>
    /// Formats artifacts as a console table with ID, Type, Created, and Title columns.
    /// </summary>
    /// <param name="artifacts">Artifacts to format</param>
    /// <param name="totalCount">Total artifact count (for pagination info)</param>
    public static void PrintTable(IEnumerable<Artifact> artifacts, int totalCount)
    {
        var artifactList = artifacts.ToList();
        
        if (artifactList.Count == 0)
        {
            Console.WriteLine("No artifacts found.");
            return;
        }

        // Print table header
        Console.WriteLine($"{"ID",-40} {"Type",-8} {"Created",-20} {"Title"}");
        Console.WriteLine(new string('-', 100));

        // Print each artifact
        foreach (var artifact in artifactList)
        {
            var createdAt = artifact.Created.ToString("yyyy-MM-dd HH:mm:ss");
            var title = artifact.Title ?? string.Empty;
            if (title.Length > 30)
                title = title.Substring(0, 27) + "...";

            Console.WriteLine($"{artifact.Id,-40} {artifact.Type,-8} {createdAt,-20} {title}");
        }

        // Print footer
        Console.WriteLine();
        Console.WriteLine($"Total: {totalCount} artifacts (showing {artifactList.Count})");
    }
}
