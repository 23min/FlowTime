using System.Text.Json.Nodes;
using FlowTime.Core.Configuration;
using Json.Schema;

namespace FlowTime.Core.TimeTravel;

public static class ManifestSchemaValidator
{
    private static readonly Lazy<JsonSchema?> manifestSchema = new(LoadSchema);

    public static void EnsureValid(string manifestJson)
    {
        var schema = manifestSchema.Value;
        if (schema is null)
        {
            return;
        }

        var node = JsonNode.Parse(manifestJson) ?? throw new InvalidDataException("Manifest JSON could not be parsed.");
        var evaluation = schema.Evaluate(node, new EvaluationOptions { OutputFormat = OutputFormat.Hierarchical });
        if (!evaluation.IsValid)
        {
            var errorMessage = string.Join("; ", CollectErrors(evaluation))
                .Trim();
            if (string.IsNullOrWhiteSpace(errorMessage))
            {
                errorMessage = evaluation.ToString();
            }

            throw new InvalidDataException($"Manifest JSON failed schema validation: {errorMessage}");
        }
    }

    private static JsonSchema? LoadSchema()
    {
        try
        {
            var solutionRoot = DirectoryProvider.FindSolutionRoot();
            if (string.IsNullOrWhiteSpace(solutionRoot))
            {
                return null;
            }

            var schemaPath = Path.Combine(solutionRoot, "docs", "schemas", "manifest.schema.json");
            if (!File.Exists(schemaPath))
            {
                return null;
            }

            var schemaText = File.ReadAllText(schemaPath);
            return JsonSchema.FromText(schemaText);
        }
        catch
        {
            // Schema validation is a safety net; if we cannot load the schema we skip validation.
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
}
