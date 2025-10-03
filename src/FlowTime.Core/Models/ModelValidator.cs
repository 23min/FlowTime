using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace FlowTime.Core;

/// <summary>
/// Validates FlowTime model YAML against the schema requirements.
/// </summary>
public static class ModelValidator
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    /// <summary>
    /// Validates a model YAML string against schema requirements.
    /// </summary>
    /// <param name="yaml">The YAML content to validate</param>
    /// <returns>Validation result with errors if any</returns>
    public static ValidationResult Validate(string yaml)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(yaml))
        {
            errors.Add("Model YAML cannot be null or empty");
            return new ValidationResult(errors);
        }

        try
        {
            // Parse YAML to a dynamic object first to check raw structure
            var rawModel = Deserializer.Deserialize<Dictionary<string, object>>(yaml);

            // Check for legacy top-level fields that are no longer supported
            if (rawModel.ContainsKey("arrivals"))
            {
                errors.Add("Top-level 'arrivals' field is not supported. Use 'nodes' array with node definitions instead.");
            }
            if (rawModel.ContainsKey("route"))
            {
                errors.Add("Top-level 'route' field is not supported. Use 'nodes' array with node definitions instead.");
            }

            // Validate schema version
            if (!rawModel.ContainsKey("schemaVersion"))
            {
                errors.Add("schemaVersion is required");
            }
            else
            {
                var schemaVersionObj = rawModel["schemaVersion"];
                if (!TryConvertToInt(schemaVersionObj, out var schemaVersion))
                {
                    errors.Add("schemaVersion must be an integer");
                }
                else if (schemaVersion != 1)
                {
                    errors.Add("schemaVersion must be 1");
                }
            }

            // Validate grid definition
            if (!rawModel.ContainsKey("grid"))
            {
                errors.Add("Model must have a grid definition");
            }
            else if (rawModel["grid"] is IDictionary<object, object> gridDict)
            {
                // Convert dictionary keys to strings for easier access
                var grid = new Dictionary<string, object>();
                foreach (var kvp in gridDict)
                {
                    if (kvp.Key is string key)
                        grid[key] = kvp.Value;
                }
                ValidateGrid(grid, errors);
            }
            else
            {
                errors.Add("Grid must be an object");
            }

            // Validate nodes definition
            if (rawModel.ContainsKey("nodes") && rawModel["nodes"] is IDictionary<object, object> nodesDict)
            {
                ValidateNodes(nodesDict, errors);
            }
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

    private static void ValidateGrid(Dictionary<string, object> grid, List<string> errors)
    {
        // Check for legacy binMinutes (should be rejected)
        if (grid.ContainsKey("binMinutes"))
        {
            errors.Add("binMinutes is no longer supported, use binSize and binUnit instead");
            return; // Don't continue validation if using legacy format
        }

        // Validate bins
        if (!grid.ContainsKey("bins"))
        {
            errors.Add("Grid must specify bins");
        }
        else if (!TryConvertToInt(grid["bins"], out var bins))
        {
            errors.Add("bins must be an integer");
        }
        else if (bins < 1 || bins > 10000)
        {
            errors.Add("bins must be between 1 and 10000");
        }

        // Validate binSize
        if (!grid.ContainsKey("binSize"))
        {
            errors.Add("Grid must specify binSize");
        }
        else if (!TryConvertToInt(grid["binSize"], out var binSize))
        {
            errors.Add("binSize must be an integer");
        }
        else if (binSize < 1 || binSize > 1000)
        {
            errors.Add("binSize must be between 1 and 1000");
        }

        // Validate binUnit
        if (!grid.ContainsKey("binUnit"))
        {
            errors.Add("Grid must specify binUnit");
        }
        else if (grid["binUnit"] is not string binUnit || string.IsNullOrWhiteSpace(binUnit))
        {
            errors.Add("binUnit must be a non-empty string");
        }
        else
        {
            // Validate it's a valid time unit
            var validUnits = new[] { "minutes", "hours", "days", "weeks" };
            if (!validUnits.Contains(binUnit.ToLowerInvariant()))
            {
                errors.Add($"binUnit must be one of: {string.Join(", ", validUnits)}");
            }
        }
    }

    private static void ValidateNodes(IDictionary<object, object> nodesDict, List<string> errors)
    {
        foreach (var kvp in nodesDict)
        {
            if (kvp.Key is not string nodeName)
                continue;

            if (kvp.Value is not IDictionary<object, object> nodeDict)
                continue;

            // Check for legacy "expression" field (should be "expr")
            foreach (var nodeKvp in nodeDict)
            {
                if (nodeKvp.Key is string fieldName && fieldName == "expression")
                {
                    errors.Add($"Node '{nodeName}': 'expression' field is no longer supported, use 'expr' instead");
                }
            }
        }
    }

    private static bool TryConvertToInt(object value, out int result)
    {
        result = 0;
        if (value is int intValue)
        {
            result = intValue;
            return true;
        }
        if (value is long longValue && longValue >= int.MinValue && longValue <= int.MaxValue)
        {
            result = (int)longValue;
            return true;
        }
        if (value is string strValue && int.TryParse(strValue, out result))
        {
            return true;
        }
        return false;
    }
}

/// <summary>
/// Result of model validation
/// </summary>
public class ValidationResult
{
    public List<string> Errors { get; }
    public bool IsValid => Errors.Count == 0;

    public ValidationResult(List<string> errors)
    {
        Errors = errors ?? new List<string>();
    }
}
