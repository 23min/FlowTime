using System.Text.RegularExpressions;
using FlowTime.Sim.Core.Templates.Exceptions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace FlowTime.Sim.Core.Templates;

/// <summary>
/// Handles parameter substitution in templates by replacing ${parameter} references with actual values.
/// </summary>
public static class ParameterSubstitution
{
    private static readonly Regex parameterPattern = new(@"\$\{([^}]+)\}", RegexOptions.Compiled);

    /// <summary>
    /// Apply parameter substitution to a template, replacing ${param} references with actual values.
    /// This method performs substitution on raw YAML first, then parses the result.
    /// </summary>
    /// <param name="template">The template to process</param>
    /// <param name="parameterValues">Dictionary of parameter names to values</param>
    /// <returns>A new template with parameters substituted</returns>
    public static Template Apply(Template template, Dictionary<string, object> parameterValues)
    {
        // For backward compatibility, we'll still support object-level substitution
        // but the preferred approach is to use ApplyToYaml before parsing
        
        // Create a merged dictionary with defaults from template parameters
        var allValues = GetMergedParameterValues(template, parameterValues);

        // Clone the template and substitute parameters
        var substitutedTemplate = CloneTemplate(template);
        SubstituteInTemplate(substitutedTemplate, allValues);
        
        return substitutedTemplate;
    }

    /// <summary>
    /// Apply parameter substitution to raw YAML content before parsing.
    /// This is the preferred method for handling parameter substitution.
    /// </summary>
    /// <param name="yamlContent">The raw YAML content with ${param} references</param>
    /// <param name="template">The parsed template (for parameter defaults)</param>
    /// <param name="parameterValues">Dictionary of parameter names to values</param>
    /// <returns>YAML content with parameters substituted</returns>
    public static string ApplyToYaml(string yamlContent, Template template, Dictionary<string, object> parameterValues)
    {
        // Create a merged dictionary with defaults from template parameters
        var allValues = GetMergedParameterValues(template, parameterValues);
        
        // Substitute parameters in the raw YAML string
        return SubstituteInString(yamlContent, allValues);
    }

    /// <summary>
    /// Parse YAML with parameter substitution applied.
    /// This method handles the complete workflow: substitute parameters, then parse.
    /// </summary>
    /// <param name="yamlContent">The raw YAML content with ${param} references</param>
    /// <param name="parameterValues">Dictionary of parameter names to values</param>
    /// <returns>Parsed template with parameters substituted</returns>
    public static Template ParseWithSubstitution(string yamlContent, Dictionary<string, object> parameterValues)
    {
        // Extract parameter definitions from YAML to get defaults
        var parametersWithDefaults = ExtractParametersFromYaml(yamlContent);
        
        // Create merged parameter values (defaults + provided)
        var allValues = new Dictionary<string, object>();
        
        // Add defaults first
        foreach (var param in parametersWithDefaults)
        {
            if (param.Default != null)
            {
                allValues[param.Name] = param.Default;
            }
        }
        
        // Override with provided values
        foreach (var kvp in parameterValues)
        {
            allValues[kvp.Key] = kvp.Value;
        }

        // Substitute parameters in raw YAML
        var substitutedYaml = SubstituteInString(yamlContent, allValues);
        
        // Parse the substituted YAML
        return TemplateParser.ParseFromYaml(substitutedYaml);
    }

    /// <summary>
    /// Extract parameter definitions from YAML content, even if the YAML contains unresolved parameter references.
    /// This method parses only the parameters section to get default values.
    /// </summary>
    /// <param name="yamlContent">Raw YAML content</param>
    /// <returns>List of template parameters with their defaults</returns>
    private static List<TemplateParameter> ExtractParametersFromYaml(string yamlContent)
    {
        try
        {
            // Try to parse the full template first
            var template = TemplateParser.ParseFromYaml(yamlContent);
            return template.Parameters.ToList();
        }
        catch
        {
            // If full parsing fails, try to extract just the parameters section
            try
            {
                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .Build();

                var yamlObject = deserializer.Deserialize<Dictionary<string, object>>(yamlContent);
                
                if (yamlObject.TryGetValue("parameters", out var parametersObj) && parametersObj is List<object> paramsList)
                {
                    var parameters = new List<TemplateParameter>();
                    
                    foreach (var paramObj in paramsList)
                    {
                        if (paramObj is Dictionary<object, object> paramDict)
                        {
                            var param = new TemplateParameter();
                            
                            if (paramDict.TryGetValue("name", out var name) && name != null)
                                param.Name = name.ToString() ?? string.Empty;
                            if (paramDict.TryGetValue("type", out var type) && type != null)
                                param.Type = type.ToString() ?? string.Empty;
                            if (paramDict.TryGetValue("title", out var title) && title != null)
                                param.Title = title.ToString() ?? string.Empty;
                            if (paramDict.TryGetValue("default", out var defaultValue))
                                param.Default = defaultValue;
                                
                            parameters.Add(param);
                        }
                    }
                    
                    return parameters;
                }
            }
            catch
            {
                // If parameter extraction fails, return empty list
            }
            
            return new List<TemplateParameter>();
        }
    }

    private static Dictionary<string, object> GetMergedParameterValues(Template template, Dictionary<string, object> parameterValues)
    {
        var allValues = new Dictionary<string, object>();
        
        // First, add default values from template parameters
        foreach (var param in template.Parameters)
        {
            if (param.Default != null)
            {
                allValues[param.Name] = param.Default;
            }
        }
        
        // Then, override with provided values
        foreach (var kvp in parameterValues)
        {
            allValues[kvp.Key] = kvp.Value;
        }

        return allValues;
    }

    private static Template CloneTemplate(Template template)
    {
        // Use JSON serialization for deep cloning
        var json = System.Text.Json.JsonSerializer.Serialize(template);
        return System.Text.Json.JsonSerializer.Deserialize<Template>(json) ?? throw new InvalidOperationException("Template cloning failed");
    }

    private static void SubstituteInTemplate(Template template, Dictionary<string, object> values)
    {
        // Substitute in metadata
        template.Metadata.Id = SubstituteInString(template.Metadata.Id, values);
        template.Metadata.Title = SubstituteInString(template.Metadata.Title, values);
        template.Metadata.Description = SubstituteInString(template.Metadata.Description, values);

        // Substitute in grid
        template.Grid.BinUnit = SubstituteInString(template.Grid.BinUnit, values);

        // Substitute in nodes
        foreach (var node in template.Nodes)
        {
            SubstituteInNode(node, values);
        }

        // Substitute in outputs
        foreach (var output in template.Outputs)
        {
            output.Id = SubstituteInString(output.Id, values);
            output.Source = SubstituteInString(output.Source, values);
            output.Description = SubstituteInString(output.Description, values);
        }

        // Substitute in RNG
        if (template.Rng != null)
        {
            template.Rng.Kind = SubstituteInString(template.Rng.Kind, values);
            template.Rng.Seed = SubstituteInString(template.Rng.Seed, values);
        }
    }

    private static void SubstituteInNode(TemplateNode node, Dictionary<string, object> values)
    {
        node.Id = SubstituteInString(node.Id, values);
        node.Kind = SubstituteInString(node.Kind, values);

        // Substitute in expression
        if (!string.IsNullOrEmpty(node.Expression))
        {
            node.Expression = SubstituteInString(node.Expression, values);
        }

        // Substitute in dependencies
        if (node.Dependencies != null)
        {
            for (int i = 0; i < node.Dependencies.Length; i++)
            {
                node.Dependencies[i] = SubstituteInString(node.Dependencies[i], values);
            }
        }

        // For PMF nodes, we'll rely on YAML-level substitution instead of object-level
        // since numeric array substitution is complex at the object level
    }

    private static string SubstituteInString(string input, Dictionary<string, object> values)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        return parameterPattern.Replace(input, match =>
        {
            var paramName = match.Groups[1].Value;
            if (values.TryGetValue(paramName, out var value))
            {
                return value.ToString() ?? string.Empty;
            }

            throw new ParameterSubstitutionException($"Parameter '${{{paramName}}}' was not found in provided values");
        });
    }
}