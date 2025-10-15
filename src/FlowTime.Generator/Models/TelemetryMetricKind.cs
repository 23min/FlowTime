using System.Text.Json.Serialization;

namespace FlowTime.Generator.Models;

/// <summary>
/// Canonical metric categories emitted for telemetry bundles.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TelemetryMetricKind
{
    Arrivals,
    Served,
    Errors,
    ExternalDemand,
    QueueDepth,
    Capacity
}
