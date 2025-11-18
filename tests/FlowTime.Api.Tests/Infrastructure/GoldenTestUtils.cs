using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace FlowTime.Api.Tests.Infrastructure;

internal static class GoldenTestUtils
{
    private static readonly JsonSerializerOptions serializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly string goldenDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Golden"));
    private static readonly string actualDirectory = goldenDirectory;

    static GoldenTestUtils()
    {
        Console.WriteLine($"Golden directory: {goldenDirectory}");
        Console.WriteLine($"Golden actual output directory: {actualDirectory}");
    }

    public static JsonSerializerOptions SerializerOptions => serializerOptions;

    public static void AssertMatchesGolden(string fileName, JsonNode actual)
    {
        var expectedPath = Path.Combine(goldenDirectory, fileName);
        Console.WriteLine($"Using golden file: {expectedPath}");
        if (!File.Exists(expectedPath))
        {
            throw new FileNotFoundException($"Golden file not found at {expectedPath}. Payload:\n{actual.ToJsonString(serializerOptions)}");
        }

        var expectedNode = JsonNode.Parse(File.ReadAllText(expectedPath))
            ?? throw new InvalidOperationException($"Golden file '{fileName}' contains invalid JSON.");

        var expectedJson = expectedNode.ToJsonString(serializerOptions);
        var actualJson = actual.ToJsonString(serializerOptions);
        Directory.CreateDirectory(actualDirectory);
        var actualPath = Path.Combine(actualDirectory, fileName + ".actual");
        File.WriteAllText(actualPath, actualJson);
        Console.WriteLine($"Wrote actual to {actualPath}");
        Console.WriteLine($"Actual payload for {fileName}:{Environment.NewLine}{actualJson}");
        Assert.Equal(expectedJson, actualJson);
    }

    private static string ResolveActualDirectory(string fallback)
    {
        var overridePath = Environment.GetEnvironmentVariable("FLOWTIME_GOLDEN_ACTUAL_DIR");
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            return Path.GetFullPath(overridePath);
        }

        return fallback;
    }
}
