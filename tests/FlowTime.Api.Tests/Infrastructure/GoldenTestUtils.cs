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

    private static readonly string goldenDirectory = Path.Combine(AppContext.BaseDirectory, "Golden");

    public static JsonSerializerOptions SerializerOptions => serializerOptions;

    public static void AssertMatchesGolden(string fileName, JsonNode actual)
    {
        var expectedPath = Path.Combine(goldenDirectory, fileName);
        if (!File.Exists(expectedPath))
        {
            throw new FileNotFoundException($"Golden file not found at {expectedPath}. Payload:\n{actual.ToJsonString(serializerOptions)}");
        }

        var expectedNode = JsonNode.Parse(File.ReadAllText(expectedPath))
            ?? throw new InvalidOperationException($"Golden file '{fileName}' contains invalid JSON.");

        var expectedJson = expectedNode.ToJsonString(serializerOptions);
        var actualJson = actual.ToJsonString(serializerOptions);
        Assert.Equal(expectedJson, actualJson);
    }
}
