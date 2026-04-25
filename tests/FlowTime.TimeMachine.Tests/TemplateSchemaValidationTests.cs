using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using FlowTime.Tests.Support;
using Json.Schema;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace FlowTime.TimeMachine.Tests;

public sealed class TemplateSchemaValidationTests
{
    private static readonly JsonSchema templateSchema = LoadSchema();

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

            // m-E24-04 / AC3 — convert via YamlStream + ScalarStyle-aware coercion so
            // quoted scalars (`expr: "0"`, `graph.hidden: "true"`) resolve as strings,
            // matching ModelSchemaValidator.ParseScalar exactly. The earlier path used
            // YamlDotNet's generic dictionary deserializer plus an aggressive
            // TryConvertScalar helper that re-introduced the defect at the test layer
            // (every quoted-text field that happened to look like a number / bool was
            // silently coerced back to int / bool — masking the very contract this test
            // is meant to guard).
            var jsonNode = YamlToJsonNode(yaml);

            using var document = JsonDocument.Parse(jsonNode.ToJsonString());
            var evaluation = templateSchema.Evaluate(document.RootElement, new EvaluationOptions { OutputFormat = OutputFormat.Hierarchical });
            if (!evaluation.IsValid)
            {
                var errors = string.Join("; ", CollectErrors(evaluation).Take(10));
                throw new Xunit.Sdk.XunitException($"Template '{Path.GetFileName(path)}' failed schema validation: {errors}");
            }

            ValidateArrivalClasses(document);

            if (document.RootElement.TryGetProperty("parameters", out var parametersElement) && parametersElement.ValueKind == JsonValueKind.Array)
            {
                ValidateParameterDefaults(parametersElement, Path.GetFileName(path));
            }
        }
    }

    private static void ValidateArrivalClasses(JsonDocument doc)
    {
        var root = doc.RootElement;
        var classes = root.TryGetProperty("classes", out var classesElement) && classesElement.ValueKind == JsonValueKind.Array
            ? classesElement.EnumerateArray()
                .Select(c => c.GetProperty("id").GetString())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id!)
                .ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);

        if (root.TryGetProperty("traffic", out var traffic) && traffic.TryGetProperty("arrivals", out var arrivals) && arrivals.ValueKind == JsonValueKind.Array)
        {
            foreach (var arrival in arrivals.EnumerateArray())
            {
                var nodeId = arrival.TryGetProperty("nodeId", out var node) ? node.GetString() : "<unknown>";
                var classId = arrival.TryGetProperty("classId", out var classEl) ? classEl.GetString() : null;
                if (classes.Count == 0)
                {
                    continue; // implicit wildcard allowed
                }

                if (string.IsNullOrWhiteSpace(classId))
                {
                    throw new Xunit.Sdk.XunitException($"Arrival for node '{nodeId}' must declare classId when classes are defined.");
                }

                if (!classes.Contains(classId))
                {
                    throw new Xunit.Sdk.XunitException($"Arrival for node '{nodeId}' references undeclared class '{classId}'.");
                }
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

    /// <summary>
    /// Converts a YAML document to a <see cref="JsonNode"/> using ScalarStyle-aware
    /// coercion that mirrors <c>ModelSchemaValidator.ParseScalar</c>. Quoted scalars
    /// (<see cref="ScalarStyle.SingleQuoted"/>, <see cref="ScalarStyle.DoubleQuoted"/>) and
    /// block scalars (<see cref="ScalarStyle.Literal"/>, <see cref="ScalarStyle.Folded"/>)
    /// resolve as strings; only plain scalars are candidates for bool / int / double
    /// coercion. The two helpers must move in lock-step — the test layer cannot mask
    /// real wire-shape behavior.
    /// </summary>
    private static JsonNode YamlToJsonNode(string yaml)
    {
        var stream = new YamlStream();
        stream.Load(new StringReader(yaml));
        if (stream.Documents.Count == 0)
        {
            throw new InvalidDataException("YAML document was empty.");
        }
        return ConvertYamlNode(stream.Documents[0].RootNode);
    }

    private static JsonNode ConvertYamlNode(YamlNode node) => node switch
    {
        YamlScalarNode scalar => ParseScalar(scalar),
        YamlSequenceNode sequence => ConvertSequence(sequence),
        YamlMappingNode mapping => ConvertMapping(mapping),
        _ => JsonValue.Create(node.ToString() ?? string.Empty)!,
    };

    private static JsonArray ConvertSequence(YamlSequenceNode sequence)
    {
        var array = new JsonArray();
        foreach (var child in sequence.Children)
        {
            array.Add(ConvertYamlNode(child));
        }
        return array;
    }

    private static JsonObject ConvertMapping(YamlMappingNode mapping)
    {
        var obj = new JsonObject();
        foreach (var entry in mapping.Children)
        {
            if (entry.Key is not YamlScalarNode keyNode)
            {
                continue;
            }
            obj[keyNode.Value ?? string.Empty] = ConvertYamlNode(entry.Value);
        }
        return obj;
    }

    private static JsonNode ParseScalar(YamlScalarNode scalar)
    {
        var value = scalar.Value;
        if (value is null)
        {
            return JsonValue.Create((string?)null)!;
        }

        if (scalar.Style != ScalarStyle.Plain)
        {
            return JsonValue.Create(value)!;
        }

        if (bool.TryParse(value, out var boolResult))
        {
            return JsonValue.Create(boolResult)!;
        }

        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intResult))
        {
            return JsonValue.Create(intResult)!;
        }

        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var doubleResult))
        {
            return JsonValue.Create(doubleResult)!;
        }

        return JsonValue.Create(value)!;
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
        var schemaNode = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(schemaPath)) ?? throw new InvalidOperationException("Schema JSON could not be parsed.");
        if (schemaNode is System.Text.Json.Nodes.JsonObject schemaObject)
        {
            schemaObject.Remove("$schema");
        }

        return JsonSchema.FromText(schemaNode.ToJsonString());
    }
}
