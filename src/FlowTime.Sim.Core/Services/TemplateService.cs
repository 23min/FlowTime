using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using FlowTime.Sim.Core.Templates;
using FlowTime.Sim.Core.Templates.Exceptions;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace FlowTime.Sim.Core.Services;

/// <summary>
/// Provides parameterised generation for FlowTime.Sim templates, producing KISS time-travel schema artifacts with embedded provenance.
/// </summary>
public class TemplateService : ITemplateService
{
    private readonly string templatesDirectory;
    private readonly ILogger<TemplateService> logger;
    private readonly Dictionary<string, (Template template, string originalYaml)> templateCache = new();
    private readonly object cacheLock = new();
    private readonly ISerializer yamlSerializer;

    public TemplateService(string templatesDirectory, ILogger<TemplateService> logger)
    {
        this.templatesDirectory = templatesDirectory ?? throw new ArgumentNullException(nameof(templatesDirectory));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        yamlSerializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();
    }

    // Constructor for testing with pre-loaded templates
    public TemplateService(
        Dictionary<string, (Template template, string originalYaml)> preloadedTemplates, 
        ILogger<TemplateService> logger)
    {
        this.templatesDirectory = string.Empty; // Not used when pre-loaded
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        // Copy pre-loaded templates to our cache
        foreach (var kvp in preloadedTemplates)
        {
            templateCache[kvp.Key] = kvp.Value;
        }
        
        yamlSerializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();
    }

    // Constructor for testing with pre-loaded YAML only (avoid strict parsing of nodes)
    public TemplateService(
        Dictionary<string, string> preloadedYaml,
        ILogger<TemplateService> logger)
    {
        this.templatesDirectory = string.Empty; // Not used when pre-loaded
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));

        foreach (var kvp in preloadedYaml)
        {
            var header = BuildHeaderTemplate(kvp.Key, kvp.Value);
            templateCache[kvp.Key] = (header, kvp.Value);
        }

        yamlSerializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();
    }

    public async Task<IReadOnlyList<Template>> GetAllTemplatesAsync()
    {
        await LoadTemplatesIfNeededAsync();
        
        lock (cacheLock)
        {
            return templateCache.Values.Select(v => v.template).ToList();
        }
    }

    public async Task<Template?> GetTemplateAsync(string templateId)
    {
        await LoadTemplatesIfNeededAsync();
        
        lock (cacheLock)
        {
            return templateCache.TryGetValue(templateId, out var cached) ? cached.template : null;
        }
    }

    public async Task<string> GenerateEngineModelAsync(string templateId, Dictionary<string, object> parameters, TemplateMode? modeOverride = null)
    {
        await LoadTemplatesIfNeededAsync();
        
        (Template template, string originalYaml) cached;

        lock (cacheLock)
        {
            if (!templateCache.TryGetValue(templateId, out cached))
            {
                throw new ArgumentException($"Template not found: {templateId}");
            }
        }

        logger.LogInformation("Generating KISS schema model for template {TemplateId} with {ParamCount} parameters", 
            templateId, parameters.Count);

        var mergedParameters = MergeParameterValues(cached.template, parameters);
        var substitutionValues = BuildSubstitutionValues(mergedParameters);
        var substitutedYaml = SubstituteParameters(cached.originalYaml, substitutionValues);

        Template parsedTemplate;
        try
        {
            parsedTemplate = TemplateParser.ParseFromYaml(substitutedYaml);
        }
        catch (TemplateParsingException ex)
        {
            logger.LogError(ex, "Template parsing failed after parameter substitution for {TemplateId}", templateId);
            throw;
        }
        catch (TemplateValidationException ex)
        {
            logger.LogError(ex, "Template validation failed for {TemplateId}: {Message}", templateId, ex.Message);
            throw;
        }

        if (modeOverride.HasValue)
        {
            parsedTemplate.Mode = modeOverride.Value;
        }

        var artifact = SimModelBuilder.Build(parsedTemplate, mergedParameters, substitutedYaml);
        var yaml = yamlSerializer.Serialize(artifact);

        logger.LogDebug("Generated model for {TemplateId}, output length: {Length} chars", templateId, yaml.Length);

        return yaml;
    }

    public Task<ValidationResult> ValidateParametersAsync(string templateId, Dictionary<string, object> parameters)
    {
        // For SIM-M2.6 node-based templates, we accept parameters leniently.
        // Deeper schema-aware validation will be added alongside schema versioning.
        return Task.FromResult(ValidationResult.Success());
    }

    private async Task LoadTemplatesIfNeededAsync()
    {
        lock (cacheLock)
        {
            if (templateCache.Any())
            {
                return; // Already loaded (from files or pre-loaded for testing)
            }
        }

        // If templates directory is empty, we're using pre-loaded templates
        if (string.IsNullOrEmpty(templatesDirectory))
        {
            return;
        }

        if (!Directory.Exists(templatesDirectory))
        {
            logger.LogWarning("Templates directory does not exist: {Directory}", templatesDirectory);
            return;
        }

        var yamlFiles = Directory.GetFiles(templatesDirectory, "*.yaml");
        logger.LogInformation("Loading {Count} template files from {Directory}", yamlFiles.Length, templatesDirectory);

        foreach (var filePath in yamlFiles)
        {
            try
            {
                var yaml = await File.ReadAllTextAsync(filePath);

                // Always build a header-only template first so listing works even if strict parse fails
                var fallbackId = System.IO.Path.GetFileNameWithoutExtension(filePath);
                var header = BuildHeaderTemplate(fallbackId, yaml);
                if (string.IsNullOrWhiteSpace(header.Metadata.Id))
                {
                    header.Metadata.Id = fallbackId;
                }
                lock (cacheLock)
                {
                    templateCache[header.Metadata.Id] = (header, yaml);
                }
                logger.LogInformation("Added header-only template: {TemplateId} from {FilePath}", header.Metadata.Id, filePath);

                // Now try strict parsing and validation to enrich/replace the header template
                try
                {
                    var template = Templates.TemplateParser.ParseFromYaml(yaml);
                    lock (cacheLock)
                    {
                        templateCache[template.Metadata.Id] = (template, yaml);
                    }
                    logger.LogDebug("Loaded template (strict): {TemplateId} from {FilePath}", template.Metadata.Id, filePath);
                }
                catch (Templates.Exceptions.TemplateParsingException ex)
                {
                    // Keep header-only version in cache - this is expected for templates with parameter placeholders
                    logger.LogDebug("Strict parse failed for {FilePath}: {Message}. Using header-only for template id '{TemplateId}'. Generation will substitute parameters before engine conversion.", filePath, ex.Message, header.Metadata.Id);
                }
                catch (Templates.Exceptions.TemplateValidationException ex)
                {
                    // Keep header-only version in cache - this is expected for templates with parameters
                    logger.LogDebug("Validation failed for {FilePath}: {Message}. Using header-only for template id '{TemplateId}'. Generation will proceed with parameter substitution.", filePath, ex.Message, header.Metadata.Id);
                }
                catch (Exception ex)
                {
                    // Keep header-only version in cache - unexpected error type
                    logger.LogWarning("Unexpected parse error for {FilePath}: {Message}. Using header-only for template id '{TemplateId}'.", filePath, ex.Message, header.Metadata.Id);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to load template from {FilePath}", filePath);
            }
        }

        logger.LogInformation("Loaded {Count} templates successfully", templateCache.Count);
        if (templateCache.Count > 0)
        {
            logger.LogDebug("Template ids: {Ids}", string.Join(", ", templateCache.Keys));
        }
    }

    private Dictionary<string, object?> MergeParameterValues(Template template, Dictionary<string, object> parameterOverrides)
    {
        var merged = new Dictionary<string, object?>(StringComparer.Ordinal);

        foreach (var parameter in template.Parameters)
        {
            if (string.IsNullOrWhiteSpace(parameter.Name))
            {
                continue;
            }

            if (parameter.Default != null)
            {
                merged[parameter.Name] = NormalizeParameterValue(parameter.Default);
            }
        }

        foreach (var kvp in parameterOverrides)
        {
            merged[kvp.Key] = NormalizeParameterValue(kvp.Value);
        }

        return merged;
    }

    private static object? NormalizeParameterValue(object? value)
    {
        if (value is JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.Number => element.TryGetInt64(out var integer) ? integer : element.GetDouble(),
                JsonValueKind.String => element.GetString(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                JsonValueKind.Array => element.GetRawText(),
                JsonValueKind.Object => element.GetRawText(),
                _ => element.GetRawText()
            };
        }

        return value;
    }

    private Dictionary<string, string> BuildSubstitutionValues(Dictionary<string, object?> parameterValues)
    {
        var substitutions = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var kvp in parameterValues)
        {
            substitutions[kvp.Key] = FormatParameterValueForSubstitution(kvp.Value);
        }

        return substitutions;
    }

    private string SubstituteParameters(string yaml, Dictionary<string, string> substitutions)
    {
        var result = yaml;
        foreach (var kvp in substitutions)
        {
            var placeholder = $"${{{kvp.Key}}}";
            result = result.Replace(placeholder, kvp.Value);
        }
        return result;
    }

    private string FormatParameterValueForSubstitution(object? value)
    {
        return value switch
        {
            null => "null",
            bool b => b.ToString().ToLowerInvariant(),
            double or float or decimal => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
            int or long or short or byte or sbyte => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
            JsonElement element => FormatJsonElement(element),
            string s => ShouldQuoteString(s) ? $"\"{s}\"" : s,
            IEnumerable enumerable when value is not string => FormatEnumerable(enumerable),
            _ => value.ToString() ?? string.Empty
        };
    }

    private string FormatEnumerable(IEnumerable enumerable)
    {
        var items = new List<string>();
        foreach (var item in enumerable)
        {
            items.Add(FormatParameterValueForSubstitution(item));
        }

        return $"[{string.Join(", ", items)}]";
    }

    private static bool ShouldQuoteString(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return true;
        }

        if (value.StartsWith("[") || value.StartsWith("{") || (value.StartsWith("\"") && value.EndsWith("\"")))
        {
            return false;
        }

        return value.Any(char.IsWhiteSpace) || value.Contains(':') || value.Contains('#');
    }

    private string FormatJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Array => element.GetRawText(),
            JsonValueKind.Object => element.GetRawText(),
            JsonValueKind.Null => "null",
            _ => element.GetRawText()
        };
    }

    // Line-based parser to extract template metadata without full YAML parsing
    // This allows templates with ${placeholders} to be listed before generation
    private static Template BuildHeaderTemplate(string templateId, string yaml)
    {
        var template = new Template
        {
            Metadata = new TemplateMetadata { Id = templateId },
            Parameters = new List<TemplateParameter>()
        };

        var lines = yaml.Split('\n');
        string currentSection = string.Empty;
        TemplateParameter? currentParam = null;

        foreach (var raw in lines)
        {
            var line = raw.TrimEnd();
            var trimmed = line.TrimStart();

            if (trimmed.StartsWith("#")) continue;

            if (trimmed.StartsWith("metadata:"))
            {
                // Leaving parameters section if we encounter new top-level section
                if (currentSection == "parameters")
                {
                    if (currentParam != null)
                    {
                        template.Parameters.Add(currentParam);
                        currentParam = null;
                    }
                }
                currentSection = "metadata";
                continue;
            }
            if (trimmed.StartsWith("rng:"))
            {
                if (currentSection == "parameters")
                {
                    if (currentParam != null)
                    {
                        template.Parameters.Add(currentParam);
                        currentParam = null;
                    }
                }
                currentSection = "rng";
                template.Rng ??= new TemplateRng();
                continue;
            }
            if (trimmed.StartsWith("parameters:"))
            {
                currentSection = "parameters";
                continue;
            }

            // If a new top-level section begins, exit current section
            var isTopLevel = line.Length > 0 && line[0] != ' ';
            if (isTopLevel && trimmed.EndsWith(":") && trimmed != "metadata:" && trimmed != "parameters:" && trimmed != "rng:")
            {
                if (currentSection == "parameters")
                {
                    if (currentParam != null)
                    {
                        template.Parameters.Add(currentParam);
                        currentParam = null;
                    }
                }
                currentSection = string.Empty;
                continue;
            }

            if (currentSection == "metadata")
            {
                if (trimmed.StartsWith("id:"))
                {
                    var id = trimmed.Substring("id:".Length).Trim().Trim('"', '\'');
                    if (!string.IsNullOrWhiteSpace(id)) template.Metadata.Id = id;
                }
                else if (trimmed.StartsWith("captureKey:", StringComparison.OrdinalIgnoreCase))
                {
                    var captureKey = trimmed.Substring(trimmed.IndexOf(':') + 1).Trim().Trim('"', '\'');
                    if (!string.IsNullOrWhiteSpace(captureKey)) template.Metadata.CaptureKey = captureKey;
                }
                else if (trimmed.StartsWith("title:"))
                {
                    var title = trimmed.Substring("title:".Length).Trim().Trim('"', '\'');
                    if (!string.IsNullOrWhiteSpace(title)) template.Metadata.Title = title;
                }
                else if (trimmed.StartsWith("description:"))
                {
                    var desc = trimmed.Substring("description:".Length).Trim().Trim('"', '\'');
                    if (!string.IsNullOrWhiteSpace(desc)) template.Metadata.Description = desc;
                }
                else if (trimmed.StartsWith("version:"))
                {
                    var version = trimmed.Substring("version:".Length).Trim().Trim('"', '\'');
                    if (!string.IsNullOrWhiteSpace(version)) template.Metadata.Version = version;
                }
                else if (trimmed.StartsWith("tags:"))
                {
                    // Parse tags array: tags: [tag1, tag2, tag3]
                    var tagsStr = trimmed.Substring("tags:".Length).Trim();
                    if (tagsStr.StartsWith("[") && tagsStr.EndsWith("]"))
                    {
                        tagsStr = tagsStr.Substring(1, tagsStr.Length - 2);
                        var tags = tagsStr.Split(',').Select(t => t.Trim().Trim('"', '\'', ' ')).Where(t => !string.IsNullOrWhiteSpace(t));
                        template.Metadata.Tags.AddRange(tags);
                    }
                }
                continue;
            }
            else if (currentSection == "rng" && template.Rng is not null)
            {
                if (trimmed.StartsWith("kind:"))
                {
                    var kind = trimmed.Substring("kind:".Length).Trim().Trim('"', '\'');
                    if (!string.IsNullOrWhiteSpace(kind)) template.Rng.Kind = kind;
                }
                else if (trimmed.StartsWith("seed:"))
                {
                    var seed = trimmed.Substring("seed:".Length).Trim().Trim('"', '\'');
                    if (!string.IsNullOrWhiteSpace(seed)) template.Rng.Seed = seed;
                }
                continue;
            }

            if (currentSection == "parameters")
            {
                if (trimmed.StartsWith("- "))
                {
                    // Start new parameter
                    if (currentParam != null)
                    {
                        template.Parameters.Add(currentParam);
                    }
                    currentParam = new TemplateParameter();

                    var afterDash = trimmed.Substring(2).TrimStart();
                    if (afterDash.StartsWith("name:"))
                    {
                        currentParam.Name = afterDash.Substring("name:".Length).Trim();
                    }
                    continue;
                }

                if (currentParam != null)
                {
                    if (trimmed.StartsWith("name:"))
                    {
                        currentParam.Name = trimmed.Substring("name:".Length).Trim();
                    }
                    else if (trimmed.StartsWith("type:"))
                    {
                        currentParam.Type = trimmed.Substring("type:".Length).Trim();
                    }
                    else if (trimmed.StartsWith("title:"))
                    {
                        var title = trimmed.Substring("title:".Length).Trim().Trim('"', '\'');
                        currentParam.Title = title;
                    }
                    else if (trimmed.StartsWith("description:"))
                    {
                        var desc = trimmed.Substring("description:".Length).Trim().Trim('"', '\'');
                        currentParam.Description = desc;
                    }
                    else if (trimmed.StartsWith("default:"))
                    {
                        var defVal = trimmed.Substring("default:".Length).Trim();
                        currentParam.Default = defVal;
                    }
                    else if (trimmed.StartsWith("min:") || trimmed.StartsWith("minimum:"))
                    {
                        var prefix = trimmed.StartsWith("min:") ? "min:" : "minimum:";
                        var v = trimmed.Substring(prefix.Length).Trim();
                        if (double.TryParse(v, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var d))
                            currentParam.Min = d;
                    }
                    else if (trimmed.StartsWith("max:") || trimmed.StartsWith("maximum:"))
                    {
                        var prefix = trimmed.StartsWith("max:") ? "max:" : "maximum:";
                        var v = trimmed.Substring(prefix.Length).Trim();
                        if (double.TryParse(v, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var d))
                            currentParam.Max = d;
                    }
                }

                continue;
            }
        }

        if (currentParam != null)
        {
            template.Parameters.Add(currentParam);
        }

        return template;
    }
}
