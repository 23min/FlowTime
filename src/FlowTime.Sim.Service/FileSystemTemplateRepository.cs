using System.Globalization;
using System.Text.Json;
using FlowTime.Sim.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace FlowTime.Sim.Service;

public class FileSystemTemplateRepository : ITemplateRepository
{
    private readonly string _templatesDirectory;
    private readonly ILogger<FileSystemTemplateRepository> _logger;
    private readonly IDeserializer _yamlDeserializer;

    public FileSystemTemplateRepository(string templatesDirectory, ILogger<FileSystemTemplateRepository> logger)
    {
        _logger = logger;
        _templatesDirectory = templatesDirectory;
        _yamlDeserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
    }

    public async Task<IReadOnlyList<TemplateDef>> GetAllTemplatesAsync()
    {
        var templates = new List<TemplateDef>();

        if (!Directory.Exists(_templatesDirectory))
        {
            _logger.LogWarning("Templates directory not found: {TemplatesDirectory}", _templatesDirectory);
            return templates;
        }

        _logger.LogInformation("Loading templates from directory: {TemplatesDirectory}", _templatesDirectory);
        var yamlFiles = Directory.GetFiles(_templatesDirectory, "*.yaml", SearchOption.TopDirectoryOnly);
        
        foreach (var yamlFile in yamlFiles)
        {
            try
            {
                var template = await LoadTemplateFromFileAsync(yamlFile);
                if (template != null)
                {
                    templates.Add(template);
                    _logger.LogDebug("Loaded template: {TemplateId} from {FilePath}", template.Id, yamlFile);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load template from file: {FilePath}", yamlFile);
            }
        }

        _logger.LogInformation("Loaded {Count} templates", templates.Count);
        return templates;
    }

    public async Task<TemplateDef?> GetTemplateAsync(string templateId)
    {
        var templates = await GetAllTemplatesAsync();
        return templates.FirstOrDefault(t => t.Id == templateId);
    }

    private async Task<TemplateDef?> LoadTemplateFromFileAsync(string yamlFilePath)
    {
        var yamlContent = await File.ReadAllTextAsync(yamlFilePath);
        
        // Parse YAML to extract metadata and full content
        var yamlDocument = _yamlDeserializer.Deserialize<Dictionary<string, object>>(yamlContent);
        
        if (!yamlDocument.TryGetValue("metadata", out var metadataObj) || metadataObj == null)
        {
            _logger.LogWarning("Template file {FilePath} does not contain metadata section", yamlFilePath);
            return null;
        }

        var metadataJson = JsonSerializer.Serialize(metadataObj);
        var metadata = JsonSerializer.Deserialize<TemplateMetadata>(metadataJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (metadata == null)
        {
            _logger.LogWarning("Failed to parse metadata from template file {FilePath}", yamlFilePath);
            return null;
        }

        // Extract parameters from the YAML content
        var parameters = ExtractParametersFromYaml(yamlContent);

        return new TemplateDef(
            Id: metadata.TemplateId ?? Path.GetFileNameWithoutExtension(yamlFilePath),
            Title: metadata.Title ?? "Untitled Template",
            Description: metadata.Description ?? string.Empty,
            Category: metadata.Category ?? "general",
            Tags: (metadata.Tags ?? []).ToArray(),
            Yaml: yamlContent,
            Parameters: parameters.ToArray()
        );
    }

    private List<TemplateParameter> ExtractParametersFromYaml(string yamlContent)
    {
        var parameters = new List<TemplateParameter>();

        try
        {
            // First try to read parameter definitions from metadata
            var yaml = new YamlDotNet.Serialization.DeserializerBuilder()
                .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.CamelCaseNamingConvention.Instance)
                .Build();

            var parsedYaml = yaml.Deserialize<Dictionary<string, object>>(yamlContent);
            
            _logger.LogDebug($"Parsed YAML keys: {string.Join(", ", parsedYaml?.Keys.Cast<string>() ?? Array.Empty<string>())}");
            
            if (parsedYaml?.TryGetValue("metadata", out var metadataObj) == true)
            {
                _logger.LogDebug($"Found metadata, type: {metadataObj?.GetType()?.Name}");
                if (metadataObj is Dictionary<object, object> metadata)
                {
                    _logger.LogDebug($"Metadata keys: {string.Join(", ", metadata.Keys)}");
                    if (metadata.TryGetValue("parameters", out var parametersObj) == true)
                    {
                        _logger.LogDebug($"Found parameters, type: {parametersObj?.GetType()?.Name}");
                        if (parametersObj is List<object> parametersList)
                        {
                            _logger.LogDebug($"Processing {parametersList.Count} parameter definitions from metadata");
                            // Read parameter definitions from template metadata
                            foreach (var paramObj in parametersList)
                            {
                                if (paramObj is Dictionary<object, object> paramDict)
                                {
                                    var parameter = CreateParameterFromMetadata(paramDict);
                                    if (parameter != null)
                                    {
                                        parameters.Add(parameter);
                                    }
                                }
                            }
                        }
                        else
                        {
                            _logger.LogDebug("Parameters object is not a List<object>");
                        }
                    }
                    else
                    {
                        _logger.LogDebug("No 'parameters' key found in metadata");
                    }
                }
                else
                {
                    _logger.LogDebug("Metadata is not a Dictionary<object, object>");
                }
            }
            else
            {
                _logger.LogDebug("No 'metadata' key found in YAML");
            }
            
            if (parameters.Count == 0)
            {
                _logger.LogDebug("No parameters extracted from metadata, falling back to placeholder extraction");
                // Fallback: extract parameters from placeholders (legacy approach)
                parameters = ExtractParametersFromPlaceholders(yamlContent);
            }
            else
            {
                _logger.LogDebug($"Successfully extracted {parameters.Count} parameters from metadata");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Failed to parse parameter metadata, falling back to placeholder extraction: {ex.Message}");
            // Fallback: extract parameters from placeholders
            parameters = ExtractParametersFromPlaceholders(yamlContent);
        }

        return parameters;
    }

    private List<TemplateParameter> ExtractParametersFromPlaceholders(string yamlContent)
    {
        var parameters = new List<TemplateParameter>();
        var seenParameters = new HashSet<string>();

        // Find all {{parameter}} placeholders in the YAML content
        var matches = System.Text.RegularExpressions.Regex.Matches(yamlContent, @"\{\{(\w+)\}\}");
        
        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            var paramName = match.Groups[1].Value;
            if (seenParameters.Contains(paramName))
                continue;

            seenParameters.Add(paramName);

            // Use fallback parameter definition
            var paramDef = CreateFallbackParameterDefinition(paramName);
            parameters.Add(paramDef);
        }

        return parameters;
    }

    private TemplateParameter? CreateParameterFromMetadata(Dictionary<object, object> paramDict)
    {
        try
        {
            var name = paramDict.GetValueOrDefault("name")?.ToString();
            var type = paramDict.GetValueOrDefault("type")?.ToString();
            var title = paramDict.GetValueOrDefault("title")?.ToString();
            var description = paramDict.GetValueOrDefault("description")?.ToString();
            var defaultValue = paramDict.GetValueOrDefault("defaultValue");
            var minimum = paramDict.GetValueOrDefault("minimum");
            var maximum = paramDict.GetValueOrDefault("maximum");

            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(type))
                return null;

            var parameterType = type.ToLowerInvariant() switch
            {
                "integer" => ParameterType.Integer,
                "number" => ParameterType.Number,
                "numberarray" => ParameterType.Number, // We'll treat arrays as Number type for now
                _ => ParameterType.Number
            };

            return new TemplateParameter(
                Name: name,
                Type: parameterType,
                Title: title ?? name,
                Description: description ?? $"Parameter {name}",
                DefaultValue: ConvertToDecimal(defaultValue),
                Minimum: ConvertToDecimal(minimum),
                Maximum: ConvertToDecimal(maximum)
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Failed to create parameter from metadata: {ex.Message}");
            return null;
        }
    }

    private static decimal ConvertToDecimal(object? value)
    {
        return value switch
        {
            null => 0m,
            decimal d => d,
            double d => (decimal)d,
            float f => (decimal)f,
            int i => (decimal)i,
            long l => (decimal)l,
            string s when decimal.TryParse(s, out var result) => result,
            List<object> list when list.Count > 0 => ConvertToDecimal(list[0]), // Use first value for arrays
            _ => 0m
        };
    }

    private TemplateParameter CreateFallbackParameterDefinition(string parameterName)
    {
        // Fallback parameter definitions for legacy templates without metadata
        // TODO: Remove this once all templates have parameter metadata
        return parameterName.ToLowerInvariant() switch
        {
            "bins" => new TemplateParameter(
                Name: parameterName,
                Type: ParameterType.Integer,
                Title: "Time Periods",
                Description: "Number of time periods to simulate",
                DefaultValue: 12,
                Minimum: 3,
                Maximum: 48
            ),
            "binminutes" => new TemplateParameter(
                Name: parameterName,
                Type: ParameterType.Integer,
                Title: "Minutes per Period",
                Description: "Duration of each time period",
                DefaultValue: 60,
                Minimum: 15,
                Maximum: 480
            ),
            "qualityrate" => new TemplateParameter(
                Name: parameterName,
                Type: ParameterType.Number,
                Title: "Quality Rate",
                Description: "Quality pass rate (0-1)",
                DefaultValue: 0.95m,
                Minimum: 0.1m,
                Maximum: 1.0m
            ),
            "productionrate" => new TemplateParameter(
                Name: parameterName,
                Type: ParameterType.Number,
                Title: "Production Rate",
                Description: "Production rate per period",
                DefaultValue: 12m,
                Minimum: 1m,
                Maximum: 100m
            ),
            "buffersize" => new TemplateParameter(
                Name: parameterName,
                Type: ParameterType.Number,
                Title: "Buffer Size",
                Description: "Buffer multiplier",
                DefaultValue: 1.5m,
                Minimum: 1.0m,
                Maximum: 5.0m
            ),
            _ => new TemplateParameter(
                Name: parameterName,
                Type: ParameterType.Number,
                Title: parameterName,
                Description: $"Parameter {parameterName}",
                DefaultValue: 1m,
                Minimum: 0m,
                Maximum: 100m
            )
        };
    }

    public async Task<string> GenerateScenarioAsync(string templateId, Dictionary<string, object> parameters)
    {
        var template = await GetTemplateAsync(templateId);
        if (template == null)
        {
            throw new ArgumentException($"Template not found: {templateId}");
        }

        var scenario = template.Yaml;
        
        // Replace all {{parameter}} placeholders with actual values
        foreach (var kvp in parameters)
        {
            var placeholder = $"{{{{{kvp.Key}}}}}";
            var replacementValue = FormatParameterValue(kvp.Value);
            scenario = scenario.Replace(placeholder, replacementValue);
        }

        return scenario;
    }

    private string FormatParameterValue(object value)
    {
        return value switch
        {
            decimal d => d.ToString(CultureInfo.InvariantCulture),
            double d => d.ToString(CultureInfo.InvariantCulture),
            float f => f.ToString(CultureInfo.InvariantCulture),
            int i => i.ToString(CultureInfo.InvariantCulture),
            long l => l.ToString(CultureInfo.InvariantCulture),
            bool b => b.ToString().ToLowerInvariant(),
            string s => s,
            JsonElement je when je.ValueKind == JsonValueKind.Array => FormatJsonArray(je),
            JsonElement je => FormatJsonElement(je),
            System.Collections.IEnumerable enumerable => FormatEnumerable(enumerable),
            _ => value?.ToString() ?? "null"
        };
    }

    private string FormatJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? "",
            JsonValueKind.Number => element.ToString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => "null",
            JsonValueKind.Array => FormatJsonArray(element),
            _ => element.ToString()
        };
    }

    private string FormatJsonArray(JsonElement arrayElement)
    {
        var values = new List<string>();
        foreach (var item in arrayElement.EnumerateArray())
        {
            values.Add(FormatJsonElement(item));
        }
        return $"[{string.Join(", ", values)}]";
    }

    private string FormatEnumerable(System.Collections.IEnumerable enumerable)
    {
        var values = new List<string>();
        foreach (var item in enumerable)
        {
            values.Add(FormatParameterValue(item));
        }
        return $"[{string.Join(", ", values)}]";
    }
}

public class TemplateMetadata
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? TemplateId { get; set; }
    public string? Category { get; set; }
    public List<string>? Tags { get; set; }
}