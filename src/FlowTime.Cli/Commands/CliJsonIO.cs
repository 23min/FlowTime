using System.Text.Json;
using System.Text.Json.Serialization;

namespace FlowTime.Cli.Commands;

/// <summary>
/// Shared JSON / YAML I/O helpers for Time Machine CLI commands. Reads from a file path
/// or a provided <see cref="TextReader"/>; writes to a file path or a provided
/// <see cref="TextWriter"/>. A <c>null</c> or <c>"-"</c> path means use the stream.
///
/// Serialization options match the <c>/v1/</c> API (camelCase, web defaults, indented
/// output on write) so CLI payloads are byte-compatible with API payloads.
/// </summary>
public static class CliJsonIO
{
    /// <summary>
    /// Shared serializer options: camelCase, web defaults, indented writes, plus a
    /// case-insensitive <see cref="JsonStringEnumConverter"/> so enum-typed spec fields
    /// (e.g., <c>OptimizeSpec.Objective</c>) deserialize from strings like <c>"minimize"</c>
    /// and <c>"maximize"</c> — matching how the <c>/v1/</c> API accepts them in request bodies.
    /// </summary>
    public static readonly JsonSerializerOptions Options = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true,
        };
        options.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
        return options;
    }

    /// <summary>
    /// Read JSON of type <typeparamref name="T"/> from <paramref name="specPath"/> or
    /// <paramref name="stdin"/> when the path is <c>null</c> or <c>"-"</c>.
    /// </summary>
    /// <exception cref="FileNotFoundException">Path specified but file missing.</exception>
    /// <exception cref="JsonException">Input is not valid JSON.</exception>
    public static T ReadJson<T>(string? specPath, TextReader stdin)
    {
        var text = ReadText(specPath, stdin);
        return JsonSerializer.Deserialize<T>(text, Options)
            ?? throw new JsonException("JSON deserialized to null.");
    }

    /// <summary>
    /// Read raw YAML text from <paramref name="modelPath"/> or <paramref name="stdin"/>
    /// when the path is <c>null</c> or <c>"-"</c>.
    /// </summary>
    /// <exception cref="FileNotFoundException">Path specified but file missing.</exception>
    public static string ReadYaml(string? modelPath, TextReader stdin)
    {
        return ReadText(modelPath, stdin);
    }

    /// <summary>
    /// Write <paramref name="value"/> as JSON to <paramref name="outputPath"/> or
    /// <paramref name="stdout"/> when the path is <c>null</c> or <c>"-"</c>.
    /// </summary>
    public static void WriteJson<T>(string? outputPath, T value, TextWriter stdout)
    {
        var text = JsonSerializer.Serialize(value, Options);
        if (UseStream(outputPath))
        {
            stdout.WriteLine(text);
        }
        else
        {
            File.WriteAllText(outputPath!, text);
        }
    }

    private static string ReadText(string? path, TextReader stdin)
    {
        if (UseStream(path))
        {
            return stdin.ReadToEnd();
        }
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Input file not found: {path}", path);
        }
        return File.ReadAllText(path);
    }

    private static bool UseStream(string? path) =>
        string.IsNullOrEmpty(path) || path == "-";
}
