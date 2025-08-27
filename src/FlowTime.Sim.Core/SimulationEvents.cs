using System.Globalization;
using System.Text;
using System.Text.Json;

namespace FlowTime.Sim.Core;

public sealed record SimEvent(
    string entity_id,
    string event_type,
    string ts,
    string node,
    string flow
);

public static class EventFactory
{
    public static IEnumerable<SimEvent> BuildEvents(SimulationSpec spec, ArrivalGenerationResult arrivals)
    {
        if (spec.grid?.bins is null || spec.grid.binMinutes is null)
            throw new InvalidOperationException("grid.bins and grid.binMinutes required");
        if (spec.route?.id is null) throw new InvalidOperationException("route.id required");

        var bins = spec.grid.bins.Value;
        var binMinutes = spec.grid.binMinutes.Value;
        var start = ResolveStart(spec.grid.start);
        long entityCounter = 0;
        var node = spec.route.id;
        const string flow = "*";

        for (int b = 0; b < bins; b++)
        {
            var ts = start.AddMinutes(binMinutes * b).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
            var count = b < arrivals.BinCounts.Length ? arrivals.BinCounts[b] : 0;
            for (int i = 0; i < count; i++)
            {
                entityCounter++;
                yield return new SimEvent($"e{entityCounter}", "arrival", ts, node, flow);
            }
        }
    }

    private static DateTimeOffset ResolveStart(string? start)
    {
        if (string.IsNullOrWhiteSpace(start)) return DateTimeOffset.FromUnixTimeSeconds(0);
        // Validator ensures 'Z' suffix; safe to parse.
        return DateTimeOffset.Parse(start, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
    }
}

public static class NdjsonWriter
{
    public static async Task WriteAsync(IEnumerable<SimEvent> events, Stream output, CancellationToken ct)
    {
        foreach (var e in events)
        {
            var json = JsonSerializer.Serialize(e);
            var line = json + "\n";
            var bytes = Encoding.UTF8.GetBytes(line);
            await output.WriteAsync(bytes.AsMemory(0, bytes.Length), ct).ConfigureAwait(false);
        }
    }
}

public static class GoldWriter
{
    public static async Task WriteAsync(SimulationSpec spec, ArrivalGenerationResult arrivals, Stream output, CancellationToken ct)
    {
        if (spec.grid?.bins is null || spec.grid.binMinutes is null)
            throw new InvalidOperationException("grid.bins and grid.binMinutes required");
        if (spec.route?.id is null) throw new InvalidOperationException("route.id required");
        var start = ResolveStart(spec.grid.start);
        var bins = spec.grid.bins.Value;
        var binMinutes = spec.grid.binMinutes.Value;
        var sb = new StringBuilder();
        sb.AppendLine("timestamp,node,flow,arrivals,served,errors");

        for (int b = 0; b < bins; b++)
        {
            var ts = start.AddMinutes(binMinutes * b).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
            var arrivalsCount = b < arrivals.BinCounts.Length ? arrivals.BinCounts[b] : 0;
            var served = arrivalsCount; // SIM-M0 identity
            sb.Append(ts).Append(',')
              .Append(spec.route.id).Append(',')
              .Append('*').Append(',')
              .Append(arrivalsCount.ToString(CultureInfo.InvariantCulture)).Append(',')
              .Append(served.ToString(CultureInfo.InvariantCulture)).Append(',')
              .Append('0').AppendLine();
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        await output.WriteAsync(bytes.AsMemory(0, bytes.Length), ct).ConfigureAwait(false);
    }

    private static DateTimeOffset ResolveStart(string? start)
    {
        if (string.IsNullOrWhiteSpace(start)) return DateTimeOffset.FromUnixTimeSeconds(0);
        return DateTimeOffset.Parse(start, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
    }
}