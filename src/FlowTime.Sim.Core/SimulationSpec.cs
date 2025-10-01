using System.Globalization;
using System.Threading;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace FlowTime.Sim.Core;

// Root simulation spec DTOs (Phase 1/2) — minimal for SIM-M0.
// LEGACY: This is the old run-centric format from SIM-M0/M1. New code should use node-based templates.
// See docs/architecture/ for migration guidance. Maintained for CLI backward compatibility and tests.

#pragma warning disable CS0618 // Type or member is obsolete - suppressing within legacy code itself

[Obsolete("Legacy run-centric format from SIM-M0/M1. Use node-based templates (Template.cs) for new code. This is maintained for CLI backward compatibility.", false)]
public sealed class SimulationSpec
{
    // SIM-M1+: explicit contract version. If null treat as 0 (legacy) and warn; only 1 supported going forward.
    public int? schemaVersion { get; set; }
    // SIM-M1 Phase 2: RNG selection (default pcg). Supported: pcg | legacy
    public string? rng { get; set; }
    public GridSpec? grid { get; set; }
    public int? seed { get; set; }
    public ArrivalsSpec? arrivals { get; set; }
    public RouteSpec? route { get; set; }
    public OutputsSpec? outputs { get; set; }
    // SIM-M1 Phase 4: service time distribution foundation (no runtime effect yet)
    public ServiceSpec? service { get; set; }
}

public sealed class GridSpec
{
    public int? bins { get; set; }
    public int? binMinutes { get; set; }
    public string? start { get; set; } // ISO8601 UTC expected.
}

public sealed class ArrivalsSpec
{
    public string? kind { get; set; } // const | poisson | pmf
    public List<double>? values { get; set; } // const counts per bin | pmf discrete values (integers)
    public double? rate { get; set; } // single lambda
    public List<double>? rates { get; set; } // per-bin lambda
    
    // SIM-M2.1: PMF-specific properties
    public List<double>? probabilities { get; set; } // PMF probabilities corresponding to values
}

public sealed class ServiceSpec
{
    public string? kind { get; set; } // const | exp
    public double? value { get; set; } // for const (mean service time units)
    public double? rate { get; set; } // for exp (mean = 1/rate)
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
    // NOTE: Prior version kept a static shared IDeserializer. Under xUnit's default parallelization
    // this triggered rare IndexOutOfRangeException deep inside YamlDotNet's DefaultObjectFactory / Dictionary
    // when multiple threads deserialized concurrently. Creating a fresh deserializer per call is cheap for our scale
    // (SIM-M0/M1) and avoids the thread-safety issue.
    private static IDeserializer CreateDeserializer() => new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public static SimulationSpec LoadFromString(string yaml)
    {
        if (string.IsNullOrWhiteSpace(yaml)) throw new ArgumentException("YAML is empty", nameof(yaml));
        var deserializer = CreateDeserializer();
        var spec = deserializer.Deserialize<SimulationSpec>(yaml) ?? new SimulationSpec();
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
    // One-time warning guards to avoid noisy repetitive stderr output in test runs.
    private static int warnedSchemaVersionMissing;
    private static int warnedLegacyRng;
    public static SimulationSpecValidationResult Validate(SimulationSpec spec)
    {
        var errors = new List<string>();
    // Warnings are silent by default; set FLOWTIME_SIM_WARN_LEGACY=1 to re-enable legacy noise during focused migration work.
    var warnLegacy = Environment.GetEnvironmentVariable("FLOWTIME_SIM_WARN_LEGACY") == "1";
        // Versioning
        var ver = spec.schemaVersion;
        if (ver is null)
        {
            // legacy spec (SIM-M0) – tolerated; silently assumed unless explicit warning opt-in.
            if (warnLegacy && Interlocked.Exchange(ref warnedSchemaVersionMissing, 1) == 0)
            {
                Console.Error.WriteLine("[warn] schemaVersion missing; assuming 0 (pre-versioned spec). Consider adding 'schemaVersion: 1'.");
            }
        }
        else if (ver != 1)
        {
            errors.Add($"schemaVersion: unsupported value {ver} (only 1 is supported)");
        }

        // rng (Phase 2)
        if (!string.IsNullOrWhiteSpace(spec.rng))
        {
            var r = spec.rng.Trim().ToLowerInvariant();
            if (r is not ("pcg" or "legacy")) errors.Add("rng: unsupported (expected pcg|legacy)");
            else if (r == "legacy")
            {
                if (warnLegacy && Interlocked.Exchange(ref warnedLegacyRng, 1) == 0)
                {
                    Console.Error.WriteLine("[warn] rng=legacy selected; behavior will remain deterministic but pcg is the default going forward.");
                }
            }
        }

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
            else if (kind is not ("const" or "poisson" or "pmf")) errors.Add("arrivals.kind: unsupported (expected const|poisson|pmf)");

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
            else if (kind == "pmf")
            {
                if (spec.arrivals.values is null || spec.arrivals.values.Count == 0)
                    errors.Add("arrivals.values: required for kind=pmf");
                if (spec.arrivals.probabilities is null || spec.arrivals.probabilities.Count == 0)
                    errors.Add("arrivals.probabilities: required for kind=pmf");
                
                if (spec.arrivals.values is not null && spec.arrivals.probabilities is not null)
                {
                    if (spec.arrivals.values.Count != spec.arrivals.probabilities.Count)
                        errors.Add("PMF values and probabilities arrays must have the same length");
                    
                    // Check for non-negative values
                    if (spec.arrivals.values.Any(v => v < 0))
                        errors.Add("PMF values must be non-negative");
                    
                    // Check for non-negative probabilities
                    if (spec.arrivals.probabilities.Any(p => p < 0))
                        errors.Add("PMF probabilities must be non-negative");
                    
                    // Check probabilities sum to 1.0 (with tolerance)
                    var sum = spec.arrivals.probabilities.Sum();
                    if (Math.Abs(sum - 1.0) > 1e-9)
                        errors.Add("PMF probabilities must sum to 1.0");
                }
            }
        }

        // route
        if (spec.route is null || string.IsNullOrWhiteSpace(spec.route.id)) errors.Add("route.id: required");

        // service (Phase 4 parsing only; no effect yet)
        if (spec.service is not null)
        {
            var sk = spec.service.kind?.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(sk)) errors.Add("service.kind: required when service block present");
            else if (sk is not ("const" or "exp")) errors.Add("service.kind: unsupported (expected const|exp)");

            if (sk == "const")
            {
                if (spec.service.value is null) errors.Add("service.value: required for kind=const");
                if (spec.service.rate is not null) errors.Add("service: rate must not be set for kind=const");
                if (spec.service.value is not null && spec.service.value < 0) errors.Add("service.value: must be >= 0");
            }
            if (sk == "exp")
            {
                if (spec.service.rate is null) errors.Add("service.rate: required for kind=exp");
                if (spec.service.value is not null) errors.Add("service: value must not be set for kind=exp");
                if (spec.service.rate is not null && spec.service.rate <= 0) errors.Add("service.rate: must be > 0");
            }
        }

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

public sealed class Pcg32Rng : IDeterministicRng
{
    // PCG-XSH-RR 32
    private ulong state;
    private readonly ulong inc;

    public Pcg32Rng(int seed)
    {
        // Derive stream increment from seed (ensure odd)
        inc = ((ulong)(seed ^ 0xDA3E39CB)) << 1 | 1UL;
        // Initialize state (could use a fixed value mixed with seed for diffusion)
        state = 0UL;
        NextUInt();
        state += (ulong)(uint)seed + 0x853C49E6748FEA9BUL;
        NextUInt();
    }

    private uint NextUInt()
    {
        var old = state;
        state = unchecked(old * 6364136223846793005UL + inc);
        var xorshifted = (uint)(((old >> 18) ^ old) >> 27);
        var rot = (int)(old >> 59);
        return (xorshifted >> rot) | (xorshifted << ((-rot) & 31));
    }

    public double NextDouble() => NextUInt() / 4294967296.0; // 2^32
}

public sealed class DeterministicRng : IDeterministicRng
{
    private readonly Random randomField;
    public DeterministicRng(int seed) => randomField = new Random(seed);
    public double NextDouble() => randomField.NextDouble();
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
            "poisson" => GeneratePoisson(spec, bins, rng ?? CreateRng(spec)),
            "pmf" => GeneratePmf(spec, bins, rng ?? CreateRng(spec)),
            _ => throw new NotSupportedException($"arrivals.kind '{spec.arrivals.kind}' not supported")
        };
    }

    private static IDeterministicRng CreateRng(SimulationSpec spec)
    {
        var seed = spec.seed ?? 12345;
        var kind = spec.rng?.Trim().ToLowerInvariant();
        return kind switch
        {
            null or "" or "pcg" => new Pcg32Rng(seed),
            "legacy" => new DeterministicRng(seed),
            _ => new Pcg32Rng(seed) // fallback (should have been validated)
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

    // SIM-M2.1: PMF-based arrival generation
    private static ArrivalGenerationResult GeneratePmf(SimulationSpec spec, int bins, IDeterministicRng rng)
    {
        if (spec.arrivals?.values is null) throw new InvalidOperationException("arrivals.values required for pmf");
        if (spec.arrivals?.probabilities is null) throw new InvalidOperationException("arrivals.probabilities required for pmf");

        // Convert values to integers for PMF (design specifies integer values)
        var values = spec.arrivals.values.Select(v => (int)Math.Round(v)).ToList();
        var probabilities = spec.arrivals.probabilities.ToList();

        // Validate PMF specification
        ValidatePmf(values, probabilities);

        // Generate arrivals using PMF sampling
        var counts = new int[bins];
        for (int bin = 0; bin < bins; bin++)
        {
            counts[bin] = SampleFromPmf(values, probabilities, rng);
        }

        return new ArrivalGenerationResult(counts);
    }

    private static void ValidatePmf(List<int> values, List<double> probabilities)
    {
        if (values == null || probabilities == null)
            throw new ArgumentException("PMF values and probabilities cannot be null");

        if (values.Count != probabilities.Count)
            throw new ArgumentException("PMF values and probabilities must have equal length");

        if (values.Count == 0)
            throw new ArgumentException("PMF must have at least one value");

        if (probabilities.Any(p => p < 0))
            throw new ArgumentException("PMF probabilities must be non-negative");

        var sum = probabilities.Sum();
        if (Math.Abs(sum - 1.0) > 1e-6)
            throw new ArgumentException($"PMF probabilities must sum to 1.0, got {sum}");
    }

    private static double CalculateExpectedValue(List<int> values, List<double> probabilities)
    {
        return values.Zip(probabilities, (v, p) => v * p).Sum();
    }

    private static int SampleFromPmf(List<int> values, List<double> probabilities, IDeterministicRng rng)
    {
        var sample = rng.NextDouble();
        var cumulative = 0.0;

        for (int i = 0; i < probabilities.Count; i++)
        {
            cumulative += probabilities[i];
            if (sample <= cumulative)
                return values[i];
        }

        // Fallback to last value (handles floating point precision issues)
        return values[values.Count - 1];
    }
}
