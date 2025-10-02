using FlowTime.Sim.Core.Models;

namespace FlowTime.Sim.Core.Services;

/// <summary>
/// Service for generating provenance metadata for Sim-generated models.
/// SIM-M2.7: Model Provenance Integration.
/// </summary>
public interface IProvenanceService
{
    /// <summary>
    /// Creates provenance metadata for a generated model.
    /// </summary>
    /// <param name="templateId">Template identifier</param>
    /// <param name="templateVersion">Template version</param>
    /// <param name="templateTitle">Human-readable template title</param>
    /// <param name="parameters">Parameter values used in generation</param>
    /// <returns>Complete provenance metadata</returns>
    ProvenanceMetadata CreateProvenance(
        string templateId,
        string templateVersion,
        string templateTitle,
        Dictionary<string, object> parameters);
}
