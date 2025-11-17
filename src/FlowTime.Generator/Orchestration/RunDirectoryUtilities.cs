using System.Text.Json;

namespace FlowTime.Generator.Orchestration;

internal static class RunDirectoryUtilities
{
    public static async Task<RunDocument> LoadRunDocumentAsync(string runDirectory, CancellationToken cancellationToken)
    {
        var runJsonPath = Path.Combine(runDirectory, "run.json");
        if (!File.Exists(runJsonPath))
        {
            throw new FileNotFoundException($"run.json not found at '{runJsonPath}'.");
        }

        await using var stream = File.OpenRead(runJsonPath);
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        var document = await JsonSerializer.DeserializeAsync<RunDocument>(stream, options, cancellationToken).ConfigureAwait(false);
        return document ?? throw new InvalidOperationException($"run.json at '{runJsonPath}' is invalid.");
    }
}
