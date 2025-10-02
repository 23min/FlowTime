using FlowTime.Sim.Core.Templates;
using FlowTime.Sim.Core.Templates.Exceptions;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.RegularExpressions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace FlowTime.Sim.Core.Services;

/// <summary>
/// Charter-compliant template service implementing the SIM-M2.6-CORRECTIVE node-based schema.
/// Generates Engine-compatible models with proper parameter substitution and schema conversion.
/// </summary>
public class NodeBasedTemplateService : INodeBasedTemplateService
{
    private readonly string templatesDirectory;
    private readonly ILogger<NodeBasedTemplateService> logger;
    private readonly Dictionary<string, (Template template, string originalYaml)> templateCache = new();
    private readonly object cacheLock = new();
    private readonly ISerializer yamlSerializer;

    public NodeBasedTemplateService(string templatesDirectory, ILogger<NodeBasedTemplateService> logger)
    {
        this.templatesDirectory = templatesDirectory ?? throw new ArgumentNullException(nameof(templatesDirectory));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        yamlSerializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();
    }

    // Constructor for testing with pre-loaded templates
    public NodeBasedTemplateService(
        Dictionary<string, (Template template, string originalYaml)> preloadedTemplates, 
        ILogger<NodeBasedTemplateService> logger)
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
    public NodeBasedTemplateService(
        Dictionary<string, string> preloadedYaml,
        ILogger<NodeBasedTemplateService> logger)
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

    public async Task<string> GenerateEngineModelAsync(string templateId, Dictionary<string, object> parameters)
    {
        await LoadTemplatesIfNeededAsync();
        
        lock (cacheLock)
        {
            if (!templateCache.TryGetValue(templateId, out var cached))
            {
                throw new ArgumentException($"Template not found: {templateId}");
            }

            logger.LogInformation("Generating Engine-compatible model for template {TemplateId} with {ParamCount} parameters", 
                templateId, parameters.Count);

            // Start with original template YAML
            var yaml = cached.originalYaml;

            // 1. Substitute ${parameter} placeholders
            yaml = SubstituteParameters(yaml, parameters, cached.template);

            // 2. Remove template-specific sections (parameters, metadata) and ensure schemaVersion
            yaml = ConvertToEngineSchema(yaml);

            logger.LogDebug("Generated Engine model for {TemplateId}, output length: {Length} chars", 
                templateId, yaml.Length);

            return yaml;
        }
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
                    // Keep header-only version in cache
                    logger.LogWarning("Strict parse failed for {FilePath}: {Message}. Using header-only for template id '{TemplateId}'. Generation will substitute parameters before engine conversion.", filePath, ex.Message, header.Metadata.Id);
                }
                catch (Templates.Exceptions.TemplateValidationException ex)
                {
                    // Keep header-only version in cache
                    logger.LogWarning("Validation failed for {FilePath}: {Message}. Using header-only for template id '{TemplateId}'. Generation will proceed with parameter substitution.", filePath, ex.Message, header.Metadata.Id);
                }
                catch (Exception ex)
                {
                    // Keep header-only version in cache
                    logger.LogWarning("Generic parse error for {FilePath}: {Message}. Using header-only for template id '{TemplateId}'.", filePath, ex.Message, header.Metadata.Id);
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

    private string SubstituteParameters(string yaml, Dictionary<string, object> parameters, Template template)
    {
        var result = yaml;

        // Substitute provided parameters
        foreach (var kvp in parameters)
        {
            var placeholder = $"${{{kvp.Key}}}";
            var value = FormatParameterValue(kvp.Value);
            result = result.Replace(placeholder, value);
        }

        // Substitute default values for remaining placeholders
        foreach (var param in template.Parameters)
        {
            if (!parameters.ContainsKey(param.Name) && param.Default != null)
            {
                var placeholder = $"${{{param.Name}}}";
                if (result.Contains(placeholder))
                {
                    var value = FormatParameterValue(param.Default);
                    result = result.Replace(placeholder, value);
                }
            }
        }

        return result;
    }

    private string FormatParameterValue(object value)
    {
        return value switch
        {
            JsonElement element => FormatJsonElement(element),
            string s => s,
            bool b => b.ToString().ToLowerInvariant(),
            null => "null",
            _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? ""
        };
    }

    private string FormatJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.String => element.GetString() ?? "",
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Array => element.GetRawText(),
            JsonValueKind.Object => element.GetRawText(),
            _ => element.GetRawText()
        };
    }

    private int GetIndentLevel(string line)
    {
        int count = 0;
        foreach (char c in line)
        {
            if (c == ' ') count++;
            else break;
        }
        return count;
    }

    private string ConvertToEngineSchema(string yaml)
    {
        var lines = yaml.Split('\n').ToList();
        var result = new List<string>();
        var inParametersSection = false;
        var inMetadataSection = false;
        var inOutputsSection = false;
        var sectionIndentLevel = 0;
        var hasSchemaVersion = yaml.TrimStart().StartsWith("schemaVersion:");

        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var trimmed = line.Trim();
            var indentLevel = GetIndentLevel(line);

            // Skip comments
            if (trimmed.StartsWith("#"))
            {
                continue;
            }

            // Detect section starts
            if (trimmed == "parameters:")
            {
                inParametersSection = true;
                sectionIndentLevel = indentLevel;
                continue; // Skip entire parameters section
            }

            if (trimmed == "metadata:")
            {
                inMetadataSection = true;
                sectionIndentLevel = indentLevel;
                continue; // Skip entire metadata section
            }

            if (trimmed == "outputs:")
            {
                inOutputsSection = true;
                sectionIndentLevel = indentLevel;
                result.Add(line); // Keep the outputs: header
                continue;
            }

            // Detect section end
            if ((inParametersSection || inMetadataSection || inOutputsSection) && !string.IsNullOrWhiteSpace(line))
            {
                if (indentLevel <= sectionIndentLevel)
                {
                    inParametersSection = false;
                    inMetadataSection = false;
                    inOutputsSection = false;
                }
            }

            // Skip parameters/metadata content entirely
            if (inParametersSection || inMetadataSection)
            {
                continue;
            }

            // Convert outputs section from template format to engine format
            if (inOutputsSection && indentLevel > sectionIndentLevel)
            {
                // Convert template format to engine format:
                // Template: source: node_id, filename: file.csv
                // Engine: series: node_id, as: file.csv
                if (trimmed.StartsWith("source:"))
                {
                    var value = trimmed.Substring("source:".Length).Trim();
                    result.Add($"{new string(' ', indentLevel)}series: {value}");
                    continue;
                }
                if (trimmed.StartsWith("filename:"))
                {
                    var value = trimmed.Substring("filename:".Length).Trim();
                    result.Add($"{new string(' ', indentLevel)}as: {value}");
                    continue;
                }
            }

            // Convert expression format: Template uses 'expression:', Engine uses 'expr:'
            if (trimmed.StartsWith("expression:"))
            {
                var value = trimmed.Substring("expression:".Length).Trim();
                result.Add($"{new string(' ', indentLevel)}expr: {value}");
                continue;
            }

            result.Add(line);
        }

        // Ensure schemaVersion is present
        if (!result.Any(l => l.Trim().StartsWith("schemaVersion:")))
        {
            result.Insert(0, "schemaVersion: 1");
            result.Insert(1, "");
        }

        return string.Join('\n', result);
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
            if (trimmed.StartsWith("parameters:"))
            {
                currentSection = "parameters";
                continue;
            }

            // If a new top-level section begins, exit current section
            var isTopLevel = line.Length > 0 && line[0] != ' ';
            if (isTopLevel && trimmed.EndsWith(":") && trimmed != "metadata:" && trimmed != "parameters:")
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
