using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
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
    private static readonly Regex ParameterPlaceholderRegex = new(@"\$\{([^}]+)\}", RegexOptions.Compiled);

    public TemplateService(string templatesDirectory, ILogger<TemplateService> logger)
    {
        this.templatesDirectory = templatesDirectory ?? throw new ArgumentNullException(nameof(templatesDirectory));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        yamlSerializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .WithEventEmitter(next => new FlowSequenceEventEmitter(next))
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull | DefaultValuesHandling.OmitDefaults)
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
            .WithEventEmitter(next => new FlowSequenceEventEmitter(next))
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull | DefaultValuesHandling.OmitDefaults)
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
            .WithEventEmitter(next => new FlowSequenceEventEmitter(next))
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull | DefaultValuesHandling.OmitDefaults)
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

        var parameterizedConstNodes = FindConstNodeParameterBindings(cached.originalYaml);
        var mergedParameters = MergeParameterValues(cached.template, parameters);
        var substitutionValues = BuildSubstitutionValues(mergedParameters);
        var structuredParameters = IdentifyStructuredParameters(mergedParameters);
        var substitutedYaml = SubstituteParameters(cached.originalYaml, substitutionValues, structuredParameters);

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

        TemplateValidator.ValidateArrayParameters(parsedTemplate, mergedParameters);
        ValidateConstNodeLengths(parsedTemplate, mergedParameters, parameterizedConstNodes);

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
        var parameterDefinitions = new Dictionary<string, TemplateParameter>(StringComparer.Ordinal);

        foreach (var parameter in template.Parameters)
        {
            if (string.IsNullOrWhiteSpace(parameter.Name))
            {
                continue;
            }

            parameterDefinitions[parameter.Name] = parameter;

            if (parameter.Default != null)
            {
                merged[parameter.Name] = TemplateParameterValueConverter.Normalize(parameter, parameter.Default);
            }
        }

        foreach (var kvp in parameterOverrides)
        {
            parameterDefinitions.TryGetValue(kvp.Key, out var definition);
            merged[kvp.Key] = TemplateParameterValueConverter.Normalize(definition, kvp.Value);
        }

        return merged;
    }

    private Dictionary<string, string> BuildSubstitutionValues(Dictionary<string, object?> parameterValues)
    {
        var substitutions = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var kvp in parameterValues)
        {
            substitutions[kvp.Key] = TemplateParameterFormatter.FormatForSubstitution(kvp.Value);
        }

        return substitutions;
    }

    private static HashSet<string> IdentifyStructuredParameters(Dictionary<string, object?> parameterValues)
    {
        var structured = new HashSet<string>(StringComparer.Ordinal);
        foreach (var kvp in parameterValues)
        {
            if (kvp.Value is null)
            {
                continue;
            }

            if (kvp.Value is string)
            {
                continue;
            }

            if (kvp.Value is IEnumerable)
            {
                structured.Add(kvp.Key);
                continue;
            }

            if (kvp.Value is JsonElement element &&
                (element.ValueKind == JsonValueKind.Array || element.ValueKind == JsonValueKind.Object))
            {
                structured.Add(kvp.Key);
            }
        }

        return structured;
    }

    private string SubstituteParameters(string yaml, Dictionary<string, string> substitutions, ISet<string> structuredParameters)
    {
        var result = yaml;
        foreach (var kvp in substitutions)
        {
            var placeholder = $"${{{kvp.Key}}}";
            if (structuredParameters.Contains(kvp.Key))
            {
                result = result.Replace($"\"{placeholder}\"", kvp.Value);
                result = result.Replace($"'{placeholder}'", kvp.Value);
            }
            result = result.Replace(placeholder, kvp.Value);
        }
        return result;
    }

    private static Dictionary<string, string> FindConstNodeParameterBindings(string originalYaml)
    {
        var bindings = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrEmpty(originalYaml))
        {
            return bindings;
        }

        var lines = originalYaml.Split('\n');
        var inNodesSection = false;
        string? currentNodeId = null;
        bool currentNodeIsConst = false;
        var currentNodeIndent = 0;

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd();
            var trimmed = line.TrimStart();
            var indent = rawLine.Length - rawLine.TrimStart().Length;

            if (trimmed.Length == 0 || trimmed.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            if (!inNodesSection)
            {
                if (line.Equals("nodes:", StringComparison.Ordinal))
                {
                    inNodesSection = true;
                }
                continue;
            }

            if (indent <= 0 && !trimmed.StartsWith("- ", StringComparison.Ordinal))
            {
                inNodesSection = false;
                currentNodeId = null;
                currentNodeIsConst = false;
                continue;
            }

            if (trimmed.StartsWith("- ", StringComparison.Ordinal))
            {
                currentNodeId = null;
                currentNodeIsConst = false;
                currentNodeIndent = indent;

                var afterDash = trimmed.Substring(2).TrimStart();
                if (afterDash.StartsWith("id:", StringComparison.Ordinal))
                {
                    currentNodeId = afterDash.Substring(3).Trim().Trim('\'', '"');
                }
                else if (afterDash.StartsWith("kind:", StringComparison.Ordinal))
                {
                    currentNodeIsConst = afterDash.Substring(5).Trim().Trim('\'', '"')
                        .Equals("const", StringComparison.OrdinalIgnoreCase);
                }

                continue;
            }

            if (indent <= currentNodeIndent)
            {
                currentNodeId = null;
                currentNodeIsConst = false;
                continue;
            }

            if (trimmed.StartsWith("id:", StringComparison.Ordinal))
            {
                currentNodeId = trimmed.Substring(3).Trim().Trim('\'', '"');
                continue;
            }

            if (trimmed.StartsWith("kind:", StringComparison.Ordinal))
            {
                currentNodeIsConst = trimmed.Substring(5).Trim().Trim('\'', '"')
                    .Equals("const", StringComparison.OrdinalIgnoreCase);
                continue;
            }

            if (!currentNodeIsConst || string.IsNullOrEmpty(currentNodeId))
            {
                continue;
            }

            if (trimmed.StartsWith("values:", StringComparison.Ordinal))
            {
                var valueText = trimmed.Substring("values:".Length).Trim();
                var match = ParameterPlaceholderRegex.Match(valueText);
                if (match.Success)
                {
                    bindings[currentNodeId] = match.Groups[1].Value;
                }
            }
        }

        return bindings;
    }

    private static void ValidateConstNodeLengths(
        Template template,
        Dictionary<string, object?> parameterValues,
        Dictionary<string, string> nodeBindings)
    {
        if (template.Grid == null || template.Grid.Bins <= 0 || nodeBindings.Count == 0)
        {
            return;
        }

        foreach (var binding in nodeBindings)
        {
            if (!parameterValues.TryGetValue(binding.Value, out var value) || value is null)
            {
                continue;
            }

            var length = GetSequenceLength(value);
            if (!length.HasValue)
            {
                continue;
            }

            if (length.Value != template.Grid.Bins)
            {
                throw new TemplateValidationException(
                    $"Parameter '{binding.Value}' provides {length.Value} values but grid.bins is {template.Grid.Bins}; const node '{binding.Key}' requires matching length.");
            }
        }
    }

    private static int? GetSequenceLength(object value)
    {
        return value switch
        {
            Array array => array.Length,
            ICollection collection => collection.Count,
            JsonElement element when element.ValueKind == JsonValueKind.Array => element.GetArrayLength(),
            _ => null
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
