namespace FlowTime.TimeMachine.Telemetry;

/// <summary>
/// Input contract for external data fed into the Time Machine.
///
/// Each implementation snapshots data from its source at ReadAsync time and
/// returns a deterministic TelemetryData payload. The Time Machine sees only
/// the snapshot — no live feeds, no non-determinism inside the machine boundary.
///
/// Current implementations:
///   <see cref="CanonicalBundleSource"/> — reads a canonical bundle directory
///   <see cref="FileCsvSource"/>          — reads a single file:-referenced CSV
///
/// ITelemetrySink is deliberately absent. Canonical bundle writing is a concrete
/// Time Machine capability, not a pluggable adapter (deferred per D-2026-04-07-020).
/// </summary>
public interface ITelemetrySource
{
    /// <summary>
    /// Read a snapshot of the source and return the telemetry payload.
    /// </summary>
    Task<TelemetryData> ReadAsync(CancellationToken cancellationToken = default);
}
