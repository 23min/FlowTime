using System.Text.Json;
using System.Text.Json.Nodes;
using System.Globalization;
using FlowTime.Core.Configuration;
using Json.Schema;
using YamlDotNet.RepresentationModel;
using FlowTime.Sim.Core.Services;
using SchemaValidationResult = FlowTime.Sim.Core.Services.ValidationResult;

namespace FlowTime.Sim.Core.Templates;

/// <summary>
/// Loads and applies the template schema to YAML prior to semantic validation.
/// </summary>
internal static class TemplateSchemaValidator
{
    private static readonly Lazy<JsonSchema?> schema = new(LoadSchema);
    private static string? schemaLoadError;

    public static SchemaValidationResult Validate(string yaml)
    {
        if (string.IsNullOrWhiteSpace(yaml))
        {
            return SchemaValidationResult.Failure("Template YAML cannot be empty.");
        }

        if (!HasSchemaVersion(yaml))
        {
            // Legacy templates without schemaVersion skip schema validation.
            return SchemaValidationResult.Success();
        }

        var schemaInstance = schema.Value;
        if (schemaInstance is null)
        {
            return schemaLoadError is not null
                ? SchemaValidationResult.Failure(schemaLoadError)
                : SchemaValidationResult.Success();
        }

        try
        {
            var json = ConvertYamlToJson(yaml);
            var node = JsonNode.Parse(json) ?? throw new InvalidOperationException("Template YAML could not be converted to JSON.");
            var evaluation = schemaInstance.Evaluate(node, new EvaluationOptions { OutputFormat = OutputFormat.Hierarchical });
            if (!evaluation.IsValid)
            {
                var errors = CollectErrors(evaluation).ToArray();
                return SchemaValidationResult.Failure(errors);
            }
        }
        catch (YamlDotNet.Core.YamlException ex)
        {
            return SchemaValidationResult.Failure($"Invalid YAML syntax: {ex.Message}");
        }
        catch (Exception ex)
        {
            return SchemaValidationResult.Failure($"Schema validation error: {ex.Message}");
        }

        return SchemaValidationResult.Success();
    }

    private static JsonSchema? LoadSchema()
    {
        try
        {
            var root = DirectoryProvider.FindSolutionRoot();
            if (string.IsNullOrWhiteSpace(root))
            {
                schemaLoadError = "Template schema could not be loaded (solution root not found).";
                return null;
            }

            var path = Path.Combine(root, "docs", "schemas", "template.schema.json");
            if (!File.Exists(path))
            {
                schemaLoadError = $"Template schema not found at {path}.";
                return null;
            }

            var json = File.ReadAllText(path);
            var node = JsonNode.Parse(json) ?? throw new InvalidDataException("Template schema JSON could not be parsed.");
            if (node is JsonObject obj)
            {
                obj.Remove("$schema");
                json = obj.ToJsonString();
            }

            return JsonSchema.FromText(json);
        }
        catch (Exception ex)
        {
            schemaLoadError = ex.Message;
            return null;
        }
    }

    private static bool HasSchemaVersion(string yaml)
    {
        var stream = new YamlStream();
        stream.Load(new StringReader(yaml));
        if (stream.Documents.Count == 0 || stream.Documents[0].RootNode is not YamlMappingNode root)
        {
            return false;
        }

        return root.Children.ContainsKey(new YamlScalarNode("schemaVersion"));
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
            foreach (var message in CollectErrors(detail))
            {
                yield return message;
            }
        }
    }

    private static string ConvertYamlToJson(string yaml)
    {
        var stream = new YamlStream();
        stream.Load(new StringReader(yaml));
        if (stream.Documents.Count == 0)
        {
            throw new InvalidDataException("YAML document was empty.");
        }

        var root = stream.Documents[0].RootNode;
        var node = ConvertYamlNode(root);
        return node.ToJsonString();
    }

    private static JsonNode ConvertYamlNode(YamlNode node)
    {
        return node switch
        {
            YamlScalarNode scalar => ParseScalar(scalar),
            YamlSequenceNode seq => new JsonArray(seq.Children.Select(ConvertYamlNode).ToArray()),
            YamlMappingNode map => ConvertMapping(map),
            _ => JsonValue.Create(node.ToString() ?? string.Empty)!
        };
    }

    private static JsonObject ConvertMapping(YamlMappingNode map)
    {
        var obj = new JsonObject();
        foreach (var kvp in map.Children)
        {
            if (kvp.Key is YamlScalarNode key)
            {
                obj[key.Value ?? string.Empty] = ConvertYamlNode(kvp.Value);
            }
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

        if (bool.TryParse(value, out var b))
        {
            return JsonValue.Create(b)!;
        }

        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i))
        {
            return JsonValue.Create(i)!;
        }

        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
        {
            return JsonValue.Create(d)!;
        }

        return JsonValue.Create(value)!;
    }
}
