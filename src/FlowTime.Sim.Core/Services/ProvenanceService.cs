using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FlowTime.Sim.Core.Models;

namespace FlowTime.Sim.Core.Services;

/// <summary>
/// Service for generating provenance metadata for Sim-generated models.
/// SIM-M2.7: Model Provenance Integration.
/// </summary>
public sealed class ProvenanceService : IProvenanceService
{
    /// <summary>
    /// Creates provenance metadata for a generated model.
    /// </summary>
    public ProvenanceMetadata CreateProvenance(
        string templateId,
        string templateVersion,
        string templateTitle,
        Dictionary<string, object> parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var timestamp = DateTime.UtcNow;
        var hash = ComputeDeterministicHash(templateId, parameters);
        var modelId = FormatModelId(timestamp, hash);

        return new ProvenanceMetadata
        {
            Source = "flowtime-sim",
            ModelId = modelId,
            TemplateId = templateId,
            TemplateVersion = templateVersion,
            TemplateTitle = templateTitle,
            Parameters = new Dictionary<string, object>(parameters), // Defensive copy
            GeneratedAt = timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture),
            Generator = $"flowtime-sim/{VersionInfo.Version}",
            SchemaVersion = "1"
        };
    }

    /// <summary>
    /// Computes deterministic hash from template ID and parameters.
    /// Same inputs always produce same hash (for reproducibility).
    /// </summary>
    private static string ComputeDeterministicHash(string templateId, Dictionary<string, object> parameters)
    {
        // Create deterministic content string using invariant culture
        var contentBuilder = new StringBuilder();
        contentBuilder.Append(templateId);
        contentBuilder.Append(':');

        // Sort parameters by key for deterministic ordering
        var sortedParams = parameters.OrderBy(kvp => kvp.Key);
        
        // Serialize parameters using invariant culture
        var json = JsonSerializer.Serialize(sortedParams, new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        contentBuilder.Append(json);

        // Compute SHA-256 hash
        var bytes = Encoding.UTF8.GetBytes(contentBuilder.ToString());
        var hashBytes = SHA256.HashData(bytes);

        // Return first 8 hex characters (lowercase)
        return Convert.ToHexString(hashBytes)[..8].ToLowerInvariant();
    }

    /// <summary>
    /// Formats model ID with timestamp and hash.
    /// Format: model_{YYYYMMDDTHHmmssZ}_{hash}
    /// </summary>
    private static string FormatModelId(DateTime timestamp, string hash)
    {
        var timestampStr = timestamp.ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture);
        return $"model_{timestampStr}_{hash}";
    }
}
