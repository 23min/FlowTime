using FlowTime.API.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace FlowTime.API.Services;

/// <summary>
/// Service for extracting and processing model provenance metadata.
/// </summary>
public static class ProvenanceService
{
    private static readonly IDeserializer camelCaseDeserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();
    
    private static readonly IDeserializer underscoreDeserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    /// <summary>
    /// Extracts provenance from HTTP header or embedded YAML.
    /// Header takes precedence if both are present.
    /// </summary>
    /// <param name="request">The HTTP request</param>
    /// <param name="yaml">The model YAML content</param>
    /// <param name="logger">Logger for warnings</param>
    /// <returns>Provenance metadata if found, null otherwise</returns>
    public static ProvenanceMetadata? ExtractProvenance(HttpRequest request, string yaml, ILogger logger)
    {
        ProvenanceMetadata? headerProvenance = null;
        ProvenanceMetadata? embeddedProvenance = null;

        // Try to extract from header (case-insensitive)
        if (request.Headers.TryGetValue("X-Model-Provenance", out var headerValue))
        {
            // Use FirstOrDefault to handle multiple headers (take first value)
            var modelId = headerValue.FirstOrDefault()?.Trim();
            if (!string.IsNullOrWhiteSpace(modelId))
            {
                headerProvenance = new ProvenanceMetadata
                {
                    ModelId = modelId
                };
            }
        }

        // Try to extract from embedded YAML
        try
        {
            var yamlDoc = camelCaseDeserializer.Deserialize<Dictionary<string, object>>(yaml);
            if (yamlDoc != null && yamlDoc.ContainsKey("provenance"))
            {
                var provenanceObj = yamlDoc["provenance"];
                
                // Validate provenance is an object, not a string/primitive
                if (provenanceObj is not IDictionary<object, object> && provenanceObj is not Dictionary<string, object>)
                {
                    throw new InvalidOperationException("Provenance must be an object, not a string or other primitive type");
                }

                // Serialize to JSON and deserialize back to ProvenanceMetadata with snake_case handling
                var provenanceJson = System.Text.Json.JsonSerializer.Serialize(provenanceObj);
                embeddedProvenance = System.Text.Json.JsonSerializer.Deserialize<ProvenanceMetadata>(provenanceJson);
                
                // Ignore if only empty values
                if (string.IsNullOrWhiteSpace(embeddedProvenance?.ModelId))
                {
                    embeddedProvenance = null;
                }
            }
        }
        catch (InvalidOperationException)
        {
            // Re-throw validation errors
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse embedded provenance from YAML");
        }

        // Header takes precedence
        if (headerProvenance != null && embeddedProvenance != null)
        {
            logger.LogWarning(
                "Provenance provided via both header and embedded YAML. Using header value: {HeaderModelId}, ignoring embedded: {EmbeddedModelId}",
                headerProvenance.ModelId,
                embeddedProvenance.ModelId
            );
            return headerProvenance;
        }

        return headerProvenance ?? embeddedProvenance;
    }

    /// <summary>
    /// Strips the provenance section from model YAML to ensure clean execution spec.
    /// </summary>
    /// <param name="yaml">The original YAML with potential provenance section</param>
    /// <returns>YAML without provenance section</returns>
    public static string StripProvenance(string yaml)
    {
        try
        {
            var yamlDoc = camelCaseDeserializer.Deserialize<Dictionary<string, object>>(yaml);
            if (yamlDoc != null && yamlDoc.ContainsKey("provenance"))
            {
                yamlDoc.Remove("provenance");
                
                // Re-serialize to YAML
                var serializer = new SerializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .Build();
                return serializer.Serialize(yamlDoc);
            }
        }
        catch (Exception)
        {
            // If parsing fails, return original YAML
        }

        return yaml;
    }
}
