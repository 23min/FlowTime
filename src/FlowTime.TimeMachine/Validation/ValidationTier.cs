namespace FlowTime.TimeMachine.Validation;

/// <summary>
/// The depth of validation to perform.
/// Tiers are cumulative: each tier includes all checks from the tier(s) before it.
/// </summary>
public enum ValidationTier
{
    /// <summary>
    /// Tier 1: YAML parses + JSON schema validates + class references resolve.
    /// Cheap — no compile, no evaluation.
    /// </summary>
    Schema,

    /// <summary>
    /// Tier 2: Schema (tier 1) + model compiles into a Graph.
    /// Catches structural errors: unresolved node references, expression parse failures.
    /// </summary>
    Compile,

    /// <summary>
    /// Tier 3: Compile (tier 2) + deterministic evaluation + invariant checks.
    /// Catches semantic issues: conservation violations, capacity/utilization breaches.
    /// </summary>
    Analyse,
}
