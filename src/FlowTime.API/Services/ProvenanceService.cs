using FlowTime.API.Models;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace FlowTime.API.Services;

/// <summary>
/// Service for extracting and processing model provenance metadata.
/// </summary>
public static class ProvenanceService
{
    // Generic deserializer without naming convention enforcement
    // Used to parse the full YAML document without modifying field names
    private static readonly IDeserializer genericDeserializer = new DeserializerBuilder()
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
            var headerJson = headerValue.FirstOrDefault()?.Trim();
            if (!string.IsNullOrWhiteSpace(headerJson))
            {
                try
                {
                    // Try to parse as full JSON object first
                    headerProvenance = System.Text.Json.JsonSerializer.Deserialize<ProvenanceMetadata>(headerJson);
                    
                    // If deserialization succeeded but ModelId is empty, treat as invalid
                    if (headerProvenance != null && string.IsNullOrWhiteSpace(headerProvenance.ModelId))
                    {
                        headerProvenance = null;
                    }
                }
                catch (System.Text.Json.JsonException)
                {
                    // Fallback: treat as simple modelId string (backward compatibility)
                    headerProvenance = new ProvenanceMetadata
                    {
                        ModelId = headerJson
                    };
                }
            }
        }

        // Try to extract from embedded YAML
        try
        {
            var yamlDoc = genericDeserializer.Deserialize<Dictionary<string, object>>(yaml);
            if (yamlDoc != null && yamlDoc.ContainsKey("provenance"))
            {
                var provenanceObj = yamlDoc["provenance"];
                
                // Validate provenance is an object, not a string/primitive
                if (provenanceObj is not IDictionary<object, object> && provenanceObj is not Dictionary<string, object>)
                {
                    throw new InvalidOperationException("Provenance must be an object, not a string or other primitive type");
                }

                // Serialize to JSON and deserialize back to ProvenanceMetadata
                // Use camelCase naming policy to match our property names
                // WriteIndented=false and proper type handling for embedded objects
                var jsonOptions = new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                    WriteIndented = false
                };
                // Convert to JSON preserving types from YAML deserialization
                var provenanceJson = System.Text.Json.JsonSerializer.Serialize(provenanceObj, new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
                });
                embeddedProvenance = System.Text.Json.JsonSerializer.Deserialize<ProvenanceMetadata>(provenanceJson, jsonOptions);
                
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
    /// Uses a <see cref="YamlStream"/>-based surgical removal (mirroring the pattern in
    /// <c>RunArtifactWriter.NormalizeTopologySemantics</c>) so that scalar styles on every
    /// other node are preserved byte-for-byte.
    ///
    /// <para>Why structural removal, not Dictionary&lt;string,object&gt; round-trip.</para>
    /// The previous implementation deserialized to a generic dictionary and re-serialized,
    /// which discards every original scalar's <see cref="YamlDotNet.Core.ScalarStyle"/>.
    /// Strings whose literal text is YAML-1.2-ambiguous (e.g. <c>pmf.expected</c> emitted
    /// by <c>SimModelBuilder</c> as a <c>G17</c>-formatted string like <c>"3.5"</c>) were
    /// re-emitted as plain scalars, which the canonical schema's
    /// <c>nodes[].metadata.additionalProperties.type: string</c> constraint then rejects.
    /// Walking the parsed <see cref="YamlMappingNode"/> tree and emitting via
    /// <see cref="YamlStream.Save(System.IO.TextWriter, bool)"/> preserves every
    /// untouched scalar exactly as it appeared on the wire.
    /// </summary>
    /// <param name="yaml">The original YAML with potential provenance section</param>
    /// <returns>YAML without provenance section, with all other scalar styles preserved</returns>
    public static string StripProvenance(string yaml)
    {
        if (string.IsNullOrWhiteSpace(yaml))
        {
            return yaml;
        }

        var stream = new YamlStream();
        try
        {
            stream.Load(new StringReader(yaml));
        }
        catch
        {
            // If the YAML cannot be parsed we fall back to the original text.
            return yaml;
        }

        if (stream.Documents.Count == 0 || stream.Documents[0].RootNode is not YamlMappingNode root)
        {
            return yaml;
        }

        var provenanceKey = new YamlScalarNode("provenance");
        if (!root.Children.ContainsKey(provenanceKey))
        {
            return yaml;
        }

        root.Children.Remove(provenanceKey);

        var writer = new StringWriter();
        stream.Save(writer, assignAnchors: false);
        return writer.ToString();
    }
}
