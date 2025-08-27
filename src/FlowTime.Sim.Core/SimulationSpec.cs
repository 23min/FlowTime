using System.Globalization;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace FlowTime.Sim.Core;

// Root simulation spec DTOs (Phase 1/2) — minimal for SIM-M0.

public sealed class SimulationSpec
{
    public GridSpec? grid { get; set; }
    public int? seed { get; set; }
    public ArrivalsSpec? arrivals { get; set; }
    public RouteSpec? route { get; set; }
    public OutputsSpec? outputs { get; set; }
}

public sealed class GridSpec
{
    public int? bins { get; set; }
    public int? binMinutes { get; set; }
    public string? start { get; set; } // ISO8601 UTC expected.
}

public sealed class ArrivalsSpec
{
    public string? kind { get; set; } // const | poisson
    public List<double>? values { get; set; } // const counts per bin
    public double? rate { get; set; } // single lambda
    public List<double>? rates { get; set; } // per-bin lambda
}

public sealed class RouteSpec
{
    public string? id { get; set; }
}

public sealed class OutputsSpec
{
    public string? events { get; set; }
    public string? gold { get; set; }
}

public static class SimulationSpecLoader
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public static SimulationSpec LoadFromString(string yaml)
    {
        if (string.IsNullOrWhiteSpace(yaml)) throw new ArgumentException("YAML is empty", nameof(yaml));
        var spec = Deserializer.Deserialize<SimulationSpec>(yaml) ?? new SimulationSpec();
        return spec;
    }

    public static SimulationSpec LoadFromFile(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("Spec file not found", path);
        var text = File.ReadAllText(path);
        return LoadFromString(text);
    }
}

public sealed record SimulationSpecValidationResult(bool IsValid, IReadOnlyList<string> Errors)
{
    public void ThrowIfInvalid()
    {
        if (!IsValid) throw new InvalidOperationException(string.Join("; ", Errors));
    }
}

public static class SimulationSpecValidator
{
    public static SimulationSpecValidationResult Validate(SimulationSpec spec)
    {
        var errors = new List<string>();

        // grid
        if (spec.grid is null) errors.Add("grid: section is required");
        else
        {
            if (spec.grid.bins is null || spec.grid.bins <= 0) errors.Add("grid.bins: must be > 0");
            if (spec.grid.binMinutes is null || spec.grid.binMinutes <= 0) errors.Add("grid.binMinutes: must be > 0");
            if (!string.IsNullOrWhiteSpace(spec.grid.start))
            {
                var raw = spec.grid.start.Trim();
                if (!DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dto))
                {
                    errors.Add("grid.start: invalid timestamp");
                }
                else
                {
                    // Require explicit 'Z' to reduce ambiguity; offsets like +02:00 are rejected even though they can be normalized.
                    if (!raw.EndsWith("Z", StringComparison.Ordinal))
                    {
                        errors.Add("grid.start: must be UTC (Z)");
                    }
                }
            }
        }

        // arrivals
        if (spec.arrivals is null) errors.Add("arrivals: section is required");
        else
        {
            var kind = spec.arrivals.kind?.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(kind)) errors.Add("arrivals.kind: required");
            else if (kind is not ("const" or "poisson")) errors.Add("arrivals.kind: unsupported (expected const|poisson)");

            if (kind == "const")
            {
                if (spec.arrivals.values is null || spec.arrivals.values.Count == 0)
                    errors.Add("arrivals.values: required for kind=const");
            }
            else if (kind == "poisson")
            {
                var hasRate = spec.arrivals.rate.HasValue;
                var hasRates = spec.arrivals.rates is { Count: > 0 };
                if (!hasRate && !hasRates) errors.Add("arrivals.rate or arrivals.rates: one required for kind=poisson");
                if (hasRate && hasRates) errors.Add("arrivals: cannot specify both rate and rates");
            }
        }

        // route
        if (spec.route is null || string.IsNullOrWhiteSpace(spec.route.id)) errors.Add("route.id: required");

        // Cross-field length checks (only if we have bins value)
        var bins = spec.grid?.bins;
        if (bins is > 0 && spec.arrivals is not null)
        {
            var kind = spec.arrivals.kind?.Trim().ToLowerInvariant();
            if (kind == "const" && spec.arrivals.values is not null)
            {
                if (spec.arrivals.values.Count != bins)
                    errors.Add($"arrivals.values: length {spec.arrivals.values.Count} must match grid.bins {bins}");
                // Ensure non-negative integral counts
                for (int i = 0; i < spec.arrivals.values.Count; i++)
                {
                    var v = spec.arrivals.values[i];
                    if (v < 0) errors.Add($"arrivals.values[{i}]: must be >= 0");
                    else if (Math.Abs(v - Math.Round(v)) > 1e-9) errors.Add($"arrivals.values[{i}]: must be an integer");
                }
            }
            if (kind == "poisson" && spec.arrivals.rates is not null)
            {
                if (spec.arrivals.rates.Count != bins)
                    errors.Add($"arrivals.rates: length {spec.arrivals.rates.Count} must match grid.bins {bins}");
                for (int i = 0; i < spec.arrivals.rates.Count; i++)
                {
                    var r = spec.arrivals.rates[i];
                    if (r < 0) errors.Add($"arrivals.rates[{i}]: must be >= 0");
                }
            }
            if (kind == "poisson" && spec.arrivals.rate is not null && spec.arrivals.rate < 0)
            {
                errors.Add("arrivals.rate: must be >= 0");
            }
        }

        return new SimulationSpecValidationResult(errors.Count == 0, errors);
    }
}

// Phase 3: RNG & Arrival Generators

public interface IDeterministicRng
{
    double NextDouble(); // [0,1)
}

public sealed class DeterministicRng : IDeterministicRng
{
    private readonly Random _random;
    public DeterministicRng(int seed) => _random = new Random(seed);
    public double NextDouble() => _random.NextDouble();
}

public sealed class ArrivalGenerationResult
{
    public int[] BinCounts { get; }
    public int Total { get; }
    public ArrivalGenerationResult(int[] counts)
    {
        BinCounts = counts;
        Total = counts.Sum();
    }
}

public static class ArrivalGenerators
{
    public static ArrivalGenerationResult Generate(SimulationSpec spec, IDeterministicRng? rng = null)
    {
        // Assumes spec already validated.
        if (spec.grid?.bins is null) throw new InvalidOperationException("spec.grid.bins missing");
        var bins = spec.grid.bins.Value;
        if (spec.arrivals?.kind is null) throw new InvalidOperationException("arrivals.kind missing");
        var kind = spec.arrivals.kind.Trim().ToLowerInvariant();

        return kind switch
        {
            "const" => GenerateConst(spec, bins),
            "poisson" => GeneratePoisson(spec, bins, rng ?? new DeterministicRng(spec.seed ?? 12345)),
            _ => throw new NotSupportedException($"arrivals.kind '{spec.arrivals.kind}' not supported")
        };
    }

    private static ArrivalGenerationResult GenerateConst(SimulationSpec spec, int bins)
    {
        if (spec.arrivals?.values is null) throw new InvalidOperationException("arrivals.values required for const");
        var counts = spec.arrivals.values.Select(v => (int)Math.Round(v)).ToArray();
        return new ArrivalGenerationResult(counts);
    }

    private static ArrivalGenerationResult GeneratePoisson(SimulationSpec spec, int bins, IDeterministicRng rng)
    {
        double[] perBinRates;
        if (spec.arrivals?.rates is not null) perBinRates = spec.arrivals.rates.Select(r => r).ToArray();
        else if (spec.arrivals?.rate is not null) perBinRates = Enumerable.Repeat(spec.arrivals.rate.Value, bins).ToArray();
        else throw new InvalidOperationException("arrivals.rate(s) required for poisson");

        // Warning (not error) for large lambda values that may degrade Knuth performance.
        if (perBinRates.Any(r => r > 1000))
        {
            Console.Error.WriteLine("[warn] Poisson λ > 1000 detected; Knuth sampler may be slow. Consider future optimized sampler milestone.");
        }

        var counts = new int[bins];
        for (int i = 0; i < bins; i++) counts[i] = SamplePoisson(perBinRates[i], rng);
        return new ArrivalGenerationResult(counts);
    }

    // Knuth algorithm (sufficient for SIM-M0 scale)
    private static int SamplePoisson(double lambda, IDeterministicRng rng)
    {
        if (lambda <= 0) return 0;
        var L = Math.Exp(-lambda);
        int k = 0;
        double p = 1.0;
        do
        {
            k++;
            p *= rng.NextDouble();
        } while (p > L);
        return k - 1;
    }
}
