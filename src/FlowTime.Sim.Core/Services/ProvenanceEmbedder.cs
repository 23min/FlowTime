using System.Globalization;
using System.Text;
using FlowTime.Sim.Core.Models;

namespace FlowTime.Sim.Core.Services;

/// <summary>
/// Helper for embedding provenance metadata in YAML models.
/// SIM-M2.7 Phase 2: API Enhancement.
/// </summary>
public static class ProvenanceEmbedder
{
    /// <summary>
    /// Embeds provenance metadata into a YAML model after schemaVersion.
    /// Format matches SIM-M2.7 specification.
    /// </summary>
    /// <param name="modelYaml">Original model YAML</param>
    /// <param name="provenance">Provenance metadata to embed</param>
    /// <returns>Model YAML with embedded provenance section</returns>
    public static string EmbedProvenance(string modelYaml, ProvenanceMetadata provenance)
    {
        ArgumentNullException.ThrowIfNull(modelYaml);
        ArgumentNullException.ThrowIfNull(provenance);

        var lines = modelYaml.Split('\n');
        var result = new StringBuilder();
        var provenanceInserted = false;

        foreach (var line in lines)
        {
            result.AppendLine(line);

            // Insert provenance after schemaVersion line
            if (!provenanceInserted && line.TrimStart().StartsWith("schemaVersion:"))
            {
                result.AppendLine();
                result.AppendLine("# Model provenance (SIM-M2.7)");
                result.AppendLine("provenance:");
                result.AppendLine($"  source: {provenance.Source}");
                result.AppendLine($"  modelId: {provenance.ModelId}");
                result.AppendLine($"  templateId: {provenance.TemplateId}");
                result.AppendLine($"  templateVersion: \"{provenance.TemplateVersion}\"");
                result.AppendLine($"  templateTitle: \"{provenance.TemplateTitle}\"");
                result.AppendLine($"  generatedAt: \"{provenance.GeneratedAt}\"");
                result.AppendLine($"  generator: \"{provenance.Generator}\"");
                result.AppendLine($"  schemaVersion: \"{provenance.SchemaVersion}\"");
                
                // Add parameters
                result.AppendLine("  parameters:");
                if (provenance.Parameters.Count == 0)
                {
                    result.AppendLine("    {}");
                }
                else
                {
                    foreach (var param in provenance.Parameters.OrderBy(p => p.Key))
                    {
                        var value = FormatYamlValue(param.Value);
                        result.AppendLine($"    {param.Key}: {value}");
                    }
                }
                
                provenanceInserted = true;
            }
        }

        return result.ToString().TrimEnd();
    }

    private static string FormatYamlValue(object value)
    {
        return value switch
        {
            string s => $"\"{s}\"",
            bool b => b.ToString().ToLowerInvariant(),
            double d => d.ToString(CultureInfo.InvariantCulture),
            float f => f.ToString(CultureInfo.InvariantCulture),
            decimal m => m.ToString(CultureInfo.InvariantCulture),
            int i => i.ToString(CultureInfo.InvariantCulture),
            long l => l.ToString(CultureInfo.InvariantCulture),
            _ => value.ToString() ?? "null"
        };
    }
}
