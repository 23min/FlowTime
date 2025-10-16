using System.Globalization;
using System.Text.Json;
using FlowTime.Tests.Support;
using Json.Schema;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace FlowTime.Generator.Tests;

public sealed class TemplateSchemaValidationTests
{
    private static readonly JsonSchema TemplateSchema = LoadSchema();
    private static readonly IDeserializer YamlDeserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public static IEnumerable<string> TemplatePaths
    {
        get
        {
            var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
            var templatesDir = Path.Combine(root, "templates");
            return Directory.EnumerateFiles(templatesDir, "*.yaml", SearchOption.TopDirectoryOnly);
        }
    }

    [Fact]
    public void AllTemplates_ConformToTemplateSchema()
    {
        foreach (var path in TemplatePaths)
        {
            var yaml = File.ReadAllText(path);
            var parsed = YamlDeserializer.Deserialize<Dictionary<string, object?>>(yaml);
            var payload = NormalizeYaml(parsed);

            using var document = JsonDocument.Parse(JsonSerializer.Serialize(payload));
            var evaluation = TemplateSchema.Evaluate(document.RootElement, new EvaluationOptions { OutputFormat = OutputFormat.Hierarchical });
            if (!evaluation.IsValid)
            {
                var errors = string.Join("; ", CollectErrors(evaluation).Take(10));
                throw new Xunit.Sdk.XunitException($"Template '{Path.GetFileName(path)}' failed schema validation: {errors}");
            }

            if (document.RootElement.TryGetProperty("parameters", out var parametersElement) && parametersElement.ValueKind == JsonValueKind.Array)
            {
                ValidateParameterDefaults(parametersElement, Path.GetFileName(path));
            }
        }
    }

    private static void ValidateParameterDefaults(JsonElement parameters, string template)
    {
        foreach (var parameter in parameters.EnumerateArray())
        {
            if (!parameter.TryGetProperty("type", out var typeElement))
            {
                continue;
            }

            var type = typeElement.GetString();
            if (!parameter.TryGetProperty("default", out var defaultElement))
            {
                continue;
            }

            try
            {
                switch (type)
                {
                    case "integer":
                        Assert.True(defaultElement.ValueKind == JsonValueKind.Number && defaultElement.TryGetInt32(out _), $"Template '{template}' parameter '{GetName(parameter)}' expected integer default.");
                        break;
                    case "number":
                        Assert.True(defaultElement.ValueKind == JsonValueKind.Number, $"Template '{template}' parameter '{GetName(parameter)}' expected numeric default.");
                        break;
                    case "boolean":
                        Assert.True(defaultElement.ValueKind == JsonValueKind.True || defaultElement.ValueKind == JsonValueKind.False, $"Template '{template}' parameter '{GetName(parameter)}' expected boolean default.");
                        break;
                    case "string":
                        Assert.True(defaultElement.ValueKind == JsonValueKind.String, $"Template '{template}' parameter '{GetName(parameter)}' expected string default.");
                        break;
                    case "array":
                        Assert.True(defaultElement.ValueKind == JsonValueKind.Array, $"Template '{template}' parameter '{GetName(parameter)}' expected array default.");
                        break;
                }
            }
            catch (InvalidOperationException)
            {
                throw new Xunit.Sdk.XunitException($"Template '{template}' parameter '{GetName(parameter)}' has incompatible default value.");
            }
        }
    }

    private static string GetName(JsonElement parameter) => parameter.TryGetProperty("name", out var name) ? name.GetString() ?? "<unknown>" : "<unknown>";

    private static object? NormalizeYaml(object? value)
    {
        return value switch
        {
            null => null,
            IDictionary<object, object?> dict => dict.ToDictionary(kvp => kvp.Key.ToString() ?? string.Empty, kvp => NormalizeYaml(kvp.Value), StringComparer.OrdinalIgnoreCase),
            IDictionary<string, object?> stringDict => stringDict.ToDictionary(kvp => kvp.Key, kvp => NormalizeYaml(kvp.Value), StringComparer.OrdinalIgnoreCase),
            IEnumerable<object?> list => list.Select(NormalizeYaml).ToList(),
            string s when TryConvertScalar(s, out var converted) => converted,
            _ => value
        };
    }

    private static bool TryConvertScalar(string input, out object? converted)
    {
        if (int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
        {
            converted = intValue;
            return true;
        }

        if (double.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out var doubleValue))
        {
            converted = doubleValue;
            return true;
        }

        if (bool.TryParse(input, out var boolValue))
        {
            converted = boolValue;
            return true;
        }

        converted = null;
        return false;
    }

    private static IEnumerable<string> CollectErrors(EvaluationResults results)
    {
        if (results.IsValid)
        {
            yield break;
        }

        if (results.Errors is { Count: > 0 } errors)
        {
            foreach (var error in errors)
            {
                yield return $"{results.InstanceLocation}: {error.Value}";
            }
        }

        foreach (var detail in results.Details)
        {
            foreach (var error in CollectErrors(detail))
            {
                yield return error;
            }
        }
    }

    private static JsonSchema LoadSchema()
    {
        var schemaPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "docs", "schemas", "template.schema.json"));
        return JsonSchema.FromText(File.ReadAllText(schemaPath));
    }
}
