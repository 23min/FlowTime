using FlowTime.Sim.Core.Templates.Exceptions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace FlowTime.Sim.Core.Templates;

/// <summary>
/// Parses YAML templates into Template objects with validation.
/// </summary>
public static class TemplateParser
{
    private static readonly IDeserializer YamlDeserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    /// <summary>
    /// Parse a YAML string into a Template object with validation.
    /// </summary>
    /// <param name="yaml">YAML template content</param>
    /// <returns>Parsed and validated Template</returns>
    /// <exception cref="TemplateParsingException">Thrown when parsing fails</exception>
    /// <exception cref="TemplateValidationException">Thrown when validation fails</exception>
    public static Template ParseFromYaml(string yaml)
    {
        try
        {
            // Parse YAML into template object
            var template = YamlDeserializer.Deserialize<Template>(yaml);
            
            if (template == null)
            {
                throw new TemplateParsingException("Failed to parse YAML template");
            }

            // Validate the template
            ValidateTemplate(template);

            return template;
        }
        catch (YamlDotNet.Core.YamlException ex)
        {
            throw new TemplateParsingException($"YAML parsing error: {ex.Message}", ex);
        }
        catch (TemplateValidationException)
        {
            throw; // Re-throw validation exceptions as-is
        }
        catch (Exception ex)
        {
            throw new TemplateParsingException($"Template parsing failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Validate a parsed template for correctness.
    /// </summary>
    private static void ValidateTemplate(Template template)
    {
        // Validate metadata
        if (string.IsNullOrEmpty(template.Metadata?.Id))
        {
            throw new TemplateValidationException("Template metadata.id is required");
        }

        // Validate grid
        if (template.Grid == null)
        {
            throw new TemplateValidationException("Template grid is required");
        }

        if (template.Grid.Bins <= 0)
        {
            throw new TemplateValidationException("Grid bins must be greater than 0");
        }

        if (template.Grid.BinSize <= 0)
        {
            throw new TemplateValidationException("Grid binSize must be greater than 0");
        }

        if (string.IsNullOrEmpty(template.Grid.BinUnit))
        {
            throw new TemplateValidationException("Grid binUnit is required");
        }

        // Validate nodes
        if (template.Nodes == null || template.Nodes.Count == 0)
        {
            throw new TemplateValidationException("Template must have at least one node");
        }

        foreach (var node in template.Nodes)
        {
            ValidateNode(node);
        }

        // Validate outputs
        if (template.Outputs == null || template.Outputs.Count == 0)
        {
            throw new TemplateValidationException("Template must have at least one output");
        }

        foreach (var output in template.Outputs)
        {
            ValidateOutput(output, template.Nodes);
        }

        // Validate RNG (optional)
        if (template.Rng != null)
        {
            ValidateRng(template.Rng);
        }
    }

    /// <summary>
    /// Validate a template node.
    /// </summary>
    private static void ValidateNode(TemplateNode node)
    {
        if (string.IsNullOrEmpty(node.Id))
        {
            throw new TemplateValidationException("Node id is required");
        }

        if (string.IsNullOrEmpty(node.Kind))
        {
            throw new TemplateValidationException("Node kind is required");
        }

        // Validate node kind
        var validKinds = new[] { "const", "pmf", "expr" };
        if (!validKinds.Contains(node.Kind))
        {
            throw new TemplateParsingException($"Invalid node kind '{node.Kind}'. Valid kinds are: {string.Join(", ", validKinds)}");
        }

        // Validate node-specific properties
        switch (node.Kind)
        {
            case "const":
                if (node.Values == null || node.Values.Length == 0)
                {
                    throw new TemplateValidationException($"Const node '{node.Id}' must have values");
                }
                break;

            case "pmf":
                if (node.Pmf == null)
                {
                    throw new TemplateValidationException($"PMF node '{node.Id}' must have pmf specification");
                }
                ValidatePmfSpec(node.Pmf, node.Id);
                break;

            case "expr":
                if (string.IsNullOrEmpty(node.Expression))
                {
                    throw new TemplateValidationException($"Expression node '{node.Id}' must have expression");
                }
                if (node.Dependencies == null || node.Dependencies.Length == 0)
                {
                    throw new TemplateValidationException($"Expression node '{node.Id}' must have dependencies");
                }
                break;
        }
    }

    /// <summary>
    /// Validate a PMF specification.
    /// </summary>
    private static void ValidatePmfSpec(PmfSpec pmf, string nodeId)
    {
        if (pmf.Values == null || pmf.Values.Length == 0)
        {
            throw new TemplateValidationException($"PMF node '{nodeId}' must have values");
        }

        if (pmf.Probabilities == null || pmf.Probabilities.Length == 0)
        {
            throw new TemplateValidationException($"PMF node '{nodeId}' must have probabilities");
        }

        if (pmf.Values.Length != pmf.Probabilities.Length)
        {
            throw new TemplateValidationException($"PMF node '{nodeId}' values and probabilities must have the same length");
        }

        // Check probabilities sum to 1.0 (within tolerance)
        var sum = pmf.Probabilities.Sum();
        if (Math.Abs(sum - 1.0) > 1e-10)
        {
            throw new TemplateValidationException($"PMF node '{nodeId}' probabilities must sum to 1.0 (got {sum})");
        }

        // Check non-negative probabilities
        if (pmf.Probabilities.Any(p => p < 0))
        {
            throw new TemplateValidationException($"PMF node '{nodeId}' probabilities must be non-negative");
        }
    }

    /// <summary>
    /// Validate a template output.
    /// </summary>
    private static void ValidateOutput(TemplateOutput output, List<TemplateNode> nodes)
    {
        if (string.IsNullOrEmpty(output.Id))
        {
            throw new TemplateValidationException("Output id is required");
        }

        if (string.IsNullOrEmpty(output.Source))
        {
            throw new TemplateValidationException($"Output '{output.Id}' must have a source");
        }

        // Check that source node exists
        if (!nodes.Any(n => n.Id == output.Source))
        {
            throw new TemplateValidationException($"Output '{output.Id}' references unknown node '{output.Source}'");
        }
    }

    /// <summary>
    /// Validate RNG configuration.
    /// </summary>
    private static void ValidateRng(TemplateRng rng)
    {
        if (string.IsNullOrEmpty(rng.Kind))
        {
            throw new TemplateValidationException("RNG kind is required");
        }

        // Validate RNG kind
        var validKinds = new[] { "pcg32" };
        if (!validKinds.Contains(rng.Kind))
        {
            throw new TemplateValidationException($"Invalid RNG kind '{rng.Kind}'. Valid kinds are: {string.Join(", ", validKinds)}");
        }

        if (string.IsNullOrEmpty(rng.Seed))
        {
            throw new TemplateValidationException("RNG seed is required");
        }
    }
}