using FlowTime.Sim.Core.Templates.Exceptions;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace FlowTime.Sim.Core.Templates;

/// <summary>
/// Parses YAML templates into <see cref="Template"/> objects and performs schema validation.
/// </summary>
public static class TemplateParser
{
    private static readonly IDeserializer yamlDeserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    /// <summary>
    /// Parse a YAML string into a <see cref="Template"/> with validation.
    /// </summary>
    /// <param name="yaml">The YAML template content.</param>
    /// <returns>The validated template.</returns>
    /// <exception cref="TemplateParsingException">Thrown when YAML cannot be parsed.</exception>
    /// <exception cref="TemplateValidationException">Thrown when validation fails.</exception>
    public static Template ParseFromYaml(string yaml)
    {
        if (string.IsNullOrWhiteSpace(yaml))
        {
            throw new TemplateParsingException("Template YAML content is empty.");
        }

        try
        {
            Template template;
            try
            {
                template = yamlDeserializer.Deserialize<Template>(yaml)
                    ?? throw new TemplateParsingException("Failed to parse YAML template.");
            }
            catch (TemplateValidationException)
            {
                // If the converter raised a validation exception during deserialization, bubble it up.
                throw;
            }
            catch (YamlException ex)
            {
                throw new TemplateParsingException($"YAML parsing error: {ex.Message}", ex);
            }

            TemplateValidator.Validate(template);
            return template;
        }
        catch (TemplateValidationException)
        {
            throw;
        }
        catch (TemplateParsingException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new TemplateParsingException($"Template parsing failed: {ex.Message}", ex);
        }
    }
}
