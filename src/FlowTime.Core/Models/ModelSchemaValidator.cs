using System.Text.Json.Nodes;
using System.Text.Json;
using System.Globalization;
using FlowTime.Core.Configuration;
using Json.Schema;
using YamlDotNet.RepresentationModel;

namespace FlowTime.Core;

/// <summary>
/// Validates model YAML against the published JSON schema and enforces class references.
/// </summary>
public static class ModelSchemaValidator
{
    private static readonly Lazy<JsonSchema?> schema = new(LoadSchema);
    private static string? schemaLoadError;

    /// <summary>
    /// Validate a model YAML document using the canonical schema and class rules.
    /// </summary>
    public static ValidationResult Validate(string yaml)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(yaml))
        {
            errors.Add("Model YAML cannot be null or empty.");
            return new ValidationResult(errors);
        }

        var schemaInstance = schema.Value;
        if (schemaInstance is null)
        {
            errors.Add(schemaLoadError is not null
                ? $"Model schema could not be loaded for validation: {schemaLoadError}"
                : "Model schema could not be loaded for validation.");
            return new ValidationResult(errors);
        }

        try
        {
            var json = ConvertYamlToJson(yaml);
            var node = JsonNode.Parse(json) ?? throw new InvalidDataException("Parsed JSON was null.");

            var evaluation = schemaInstance.Evaluate(node, new EvaluationOptions { OutputFormat = OutputFormat.Hierarchical });
            if (!evaluation.IsValid)
            {
                var collectedBefore = errors.Count;
                errors.AddRange(CollectErrors(evaluation));
                if (errors.Count == collectedBefore)
                {
                    // Silent-error fallback (m-E23-01 / D3): JsonEverything keywords like
                    // `not`, `oneOf`-no-arm-match, and deep `allOf` failures can mark a
                    // subtree invalid without populating any leaf `Errors` entry. Without
                    // this fallback the validator returns IsValid==false with an empty
                    // Errors list, which is the silent-error class D3 found in the canary.
                    // Synthesize a path-only diagnostic so an invalid evaluation always
                    // produces at least one human-readable message.
                    errors.Add(SynthesizePathOnlyError(evaluation));
                }
            }

            errors.AddRange(ValidateClassReferences(node));
        }
        catch (YamlDotNet.Core.YamlException ex)
        {
            errors.Add($"Invalid YAML syntax: {ex.Message}");
        }
        catch (Exception ex)
        {
            errors.Add($"Validation error: {ex.Message}");
        }

        return new ValidationResult(errors);
    }

    private static JsonSchema? LoadSchema()
    {
        try
        {
            var solutionRoot = DirectoryProvider.FindSolutionRoot();
            if (string.IsNullOrWhiteSpace(solutionRoot))
            {
                schemaLoadError = "Solution root not found.";
                return null;
            }

            var schemaPath = Path.Combine(solutionRoot, "docs", "schemas", "model.schema.yaml");
            if (!File.Exists(schemaPath))
            {
                schemaLoadError = $"Schema file not found at {schemaPath}.";
                return null;
            }

            var yaml = File.ReadAllText(schemaPath);
            var json = ConvertYamlToJson(yaml);
            var schemaNode = JsonNode.Parse(json) ?? throw new InvalidDataException("Schema JSON could not be parsed.");
            if (schemaNode is JsonObject schemaObj)
            {
                schemaObj.Remove("$schema");
            }
            json = schemaNode.ToJsonString();
            return JsonSchema.FromText(json);
        }
        catch (Exception ex)
        {
            schemaLoadError = ex.ToString();
            return null;
        }
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

    /// <summary>
    /// Synthesize a path-only diagnostic from an invalid <see cref="EvaluationResults"/>
    /// when no node in the tree populated a textual error. Picks the deepest invalid node
    /// (by <see cref="EvaluationResults.EvaluationPath"/> segment count, ties broken by
    /// <see cref="EvaluationResults.InstanceLocation"/> segment count) so the message
    /// points at the most specific schema rule that failed. The choice is deterministic:
    /// repeated calls on the same <see cref="EvaluationResults"/> always return the same
    /// string. Format: <c>{instance}: schema rule failed at {path}</c>.
    /// </summary>
    private static string SynthesizePathOnlyError(EvaluationResults results)
    {
        var deepest = WalkForDeepestInvalid(results) ?? results;
        var instance = deepest.InstanceLocation.ToString();
        var path = deepest.EvaluationPath.ToString();
        return $"{instance}: schema rule failed at {path}";
    }

    /// <summary>
    /// Recursive descent over <see cref="EvaluationResults.Details"/>. Returns the deepest
    /// invalid node, where "deepest" is measured by EvaluationPath segment count (with
    /// InstanceLocation segment count as a stable tiebreaker). Returns <c>null</c> when
    /// no descendant is invalid; the caller should fall back to the root.
    /// </summary>
    private static EvaluationResults? WalkForDeepestInvalid(EvaluationResults results)
    {
        EvaluationResults? best = results.IsValid ? null : results;
        foreach (var detail in results.Details)
        {
            var candidate = WalkForDeepestInvalid(detail);
            if (candidate is null)
            {
                continue;
            }
            if (best is null || IsDeeperThan(candidate, best))
            {
                best = candidate;
            }
        }
        return best;
    }

    private static bool IsDeeperThan(EvaluationResults a, EvaluationResults b)
    {
        var aPath = a.EvaluationPath.Segments.Length;
        var bPath = b.EvaluationPath.Segments.Length;
        if (aPath != bPath)
        {
            return aPath > bPath;
        }
        return a.InstanceLocation.Segments.Length > b.InstanceLocation.Segments.Length;
    }

    /// <summary>
    /// Test-only entry point exposing <see cref="CollectErrors(EvaluationResults)"/> for
    /// the silent-error regression tests in <c>FlowTime.Core.Tests</c>. Visible via
    /// <c>InternalsVisibleTo("FlowTime.Core.Tests")</c>; not part of the public surface.
    /// </summary>
    internal static IEnumerable<string> CollectErrorsForTests(EvaluationResults results)
        => CollectErrors(results);

    /// <summary>
    /// Test-only entry point exposing <see cref="SynthesizePathOnlyError(EvaluationResults)"/>
    /// for the silent-error regression tests in <c>FlowTime.Core.Tests</c>. Visible via
    /// <c>InternalsVisibleTo("FlowTime.Core.Tests")</c>; not part of the public surface.
    /// </summary>
    internal static string SynthesizePathOnlyErrorForTests(EvaluationResults results)
        => SynthesizePathOnlyError(results);

    private static IEnumerable<string> ValidateClassReferences(JsonNode node)
    {
        var classes = node["classes"] as JsonArray;
        var classIds = new HashSet<string>(StringComparer.Ordinal);

        if (classes is not null)
        {
            foreach (var classNode in classes)
            {
                var id = classNode?["id"]?.GetValue<string>();
                if (!string.IsNullOrWhiteSpace(id))
                {
                    classIds.Add(id);
                }
            }
        }

        var arrivals = node["traffic"]?["arrivals"] as JsonArray;
        if (arrivals is null || arrivals.Count == 0)
        {
            yield break;
        }

        foreach (var arrivalNode in arrivals)
        {
            var nodeId = arrivalNode?["nodeId"]?.GetValue<string>() ?? "<unknown>";
            var classId = arrivalNode?["classId"]?.GetValue<string>();

            if (classIds.Count == 0)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(classId))
            {
                yield return $"Arrival targeting '{nodeId}' must declare classId because model.classes are defined.";
                continue;
            }

            if (!classIds.Contains(classId))
            {
                yield return $"Class '{classId}' is not declared under model.classes.";
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
            YamlSequenceNode sequence => ConvertSequence(sequence),
            YamlMappingNode mapping => ConvertMapping(mapping),
            _ => JsonValue.Create(node.ToString() ?? string.Empty)!
        };
    }

    private static JsonNode ConvertSequence(YamlSequenceNode sequence)
    {
        var array = new JsonArray();
        foreach (var child in sequence.Children)
        {
            array.Add(ConvertYamlNode(child));
        }
        return array;
    }

    private static JsonNode ConvertMapping(YamlMappingNode mapping)
    {
        var obj = new JsonObject();
        foreach (var entry in mapping.Children)
        {
            if (entry.Key is not YamlScalarNode keyNode)
            {
                continue;
            }

            var key = keyNode.Value ?? string.Empty;
            obj[key] = ConvertYamlNode(entry.Value);
        }
        return obj;
    }

    /// <summary>
    /// Convert a YAML scalar node to a typed JSON value. Honors YAML 1.2's rule that
    /// quoted scalars (<see cref="YamlDotNet.Core.ScalarStyle.SingleQuoted"/>,
    /// <see cref="YamlDotNet.Core.ScalarStyle.DoubleQuoted"/>) and block scalars
    /// (<see cref="YamlDotNet.Core.ScalarStyle.Literal"/>,
    /// <see cref="YamlDotNet.Core.ScalarStyle.Folded"/>) are explicitly typed as strings;
    /// only plain scalars are subject to bool/int/double type resolution
    /// (m-E24-04 / ADR-E-24-04). <c>internal</c> for direct test access.
    /// </summary>
    internal static JsonNode ParseScalar(YamlScalarNode scalar)
    {
        var value = scalar.Value;
        if (value is null)
        {
            return JsonValue.Create((string?)null)!;
        }

        // YAML 1.2: only plain scalars are candidates for type resolution. Quoted and
        // block scalars are unconditionally typed as strings — author intent is preserved.
        if (scalar.Style != YamlDotNet.Core.ScalarStyle.Plain)
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
}
