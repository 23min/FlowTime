# SIM-M2.1 — PMF Generator Support

## Status
PLANNED — Required for Engine M2 PMF node testing and probabilistic modeling support.

## Goal
Extend FlowTime-Sim's arrival generation framework to support Probability Mass Function (PMF) distributions, enabling synthetic data generation that aligns with Engine M2's PMF node functionality for probabilistic workflow modeling.

## Scope
- Extend `ArrivalGenerators` class to support `pmf` kind in addition to existing `const` and `poisson` generators
- Add PMF validation (probabilities sum to ~1.0, non-negative values)
- Implement expected value calculation for PMF distributions
- Support discrete value distributions with configurable probability masses
- Maintain deterministic output using existing RNG infrastructure
- Update simulation specification schema to include PMF configuration

## Non-Goals
- Complex statistical distributions beyond discrete PMF
- Runtime PMF modification or adaptive distributions
- Performance optimization for large PMF tables (defer to future milestones)
- Integration with Engine PMF nodes (handled by Engine M2)

## Implementation Overview

### 1. Simulation Specification Extensions
Extend arrival generator specification to support PMF kind:

```yaml
arrivals:
  - id: pmf_arrivals
    component: COMP_A
    kind: pmf
    values: [1, 2, 3, 5, 8]          # discrete values
    probabilities: [0.1, 0.2, 0.3, 0.3, 0.1]  # corresponding probabilities
    grid:
      bins: 10
      binMinutes: 60
```

### 2. Core Implementation Changes

#### ArrivalGenerators Class Extension
Location: `src/FlowTime.Sim.Core/SimulationSpec.cs`

Current switch statement in `Generate()` method:
```csharp
switch (arrival.Kind)
{
    case "const": return GenerateConst(arrival, grid, rng);
    case "poisson": return GeneratePoisson(arrival, grid, rng);
    default: throw new NotSupportedException($"Unsupported arrival kind: {arrival.Kind}");
}
```

Add PMF case:
```csharp
case "pmf": return GeneratePmf(arrival, grid, rng);
```

#### New GeneratePmf Method
```csharp
private static List<ArrivalEvent> GeneratePmf(ArrivalSpec arrival, GridSpec grid, Random rng)
{
    // Validate PMF specification
    ValidatePmf(arrival.Values, arrival.Probabilities);
    
    // Calculate expected value for deterministic bin distribution
    var expectedValue = CalculateExpectedValue(arrival.Values, arrival.Probabilities);
    
    // Generate arrivals using PMF sampling
    var events = new List<ArrivalEvent>();
    var totalBins = grid.Bins;
    
    for (int bin = 0; bin < totalBins; bin++)
    {
        var binStart = TimeSpan.FromMinutes(bin * grid.BinMinutes);
        var arrivalCount = SampleFromPmf(arrival.Values, arrival.Probabilities, rng);
        
        // Distribute arrivals uniformly within bin
        for (int i = 0; i < arrivalCount; i++)
        {
            var offsetMinutes = rng.NextDouble() * grid.BinMinutes;
            var timestamp = binStart.Add(TimeSpan.FromMinutes(offsetMinutes));
            
            events.Add(new ArrivalEvent
            {
                ComponentId = arrival.Component,
                Timestamp = timestamp,
                Value = arrivalCount // or sampled value for individual events
            });
        }
    }
    
    return events;
}
```

### 3. Validation and Utility Methods

#### PMF Validation
```csharp
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
```

#### Expected Value Calculation
```csharp
private static double CalculateExpectedValue(List<int> values, List<double> probabilities)
{
    return values.Zip(probabilities, (v, p) => v * p).Sum();
}
```

#### PMF Sampling
```csharp
private static int SampleFromPmf(List<int> values, List<double> probabilities, Random rng)
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
```

### 4. Schema Updates

#### ArrivalSpec Class Extensions
Add new properties to support PMF configuration:

```csharp
public class ArrivalSpec
{
    // Existing properties...
    public string Kind { get; set; } = "const";
    public int? Value { get; set; }
    public double? Rate { get; set; }
    
    // New PMF properties
    public List<int>? Values { get; set; }
    public List<double>? Probabilities { get; set; }
}
```

## Testing Strategy

### 1. Unit Tests
- PMF validation (sum to 1.0, non-negative, equal lengths)
- Expected value calculation accuracy
- Sampling distribution correctness (chi-square test)
- Deterministic output with fixed RNG seeds

### 2. Integration Tests
- End-to-end simulation with PMF arrivals
- Output validation against expected distribution
- Compatibility with existing grid and binning logic

### 3. Example Configurations
Create example PMF configurations in `examples/` directory:
- `m0.pmf.yaml` — Simple PMF with 3 values
- `m1.complex-pmf.yaml` — Multi-component PMF scenario

## Engine M2 Alignment

This implementation provides synthetic data generation that complements Engine M2's PMF node functionality:

1. **Probabilistic Modeling**: Generate realistic arrival patterns with known distributions
2. **Testing Support**: Provide controlled PMF data for Engine M2 PMF node validation
3. **Workflow Validation**: Enable end-to-end testing of probabilistic workflows

## Deliverables

1. Extended `ArrivalGenerators` with PMF support
2. PMF validation and sampling infrastructure
3. Updated simulation specification schema
4. Comprehensive unit and integration tests
5. Example configurations demonstrating PMF usage
6. Documentation updates for PMF arrival generation

## Dependencies

- **Prerequisite**: SIM-M2 completion (artifact contracts and schema versioning)
- **Enables**: Engine M2 PMF node testing and validation
- **Follows**: Existing RNG and deterministic output patterns from SIM-M1

## Success Criteria

1. PMF arrivals generate with correct statistical distribution
2. Deterministic output with fixed RNG seeds
3. Validation catches malformed PMF specifications
4. Integration with existing grid/binning infrastructure
5. Example configurations run successfully
6. All tests pass with >95% code coverage for new PMF functionality

---

*This milestone bridges FlowTime-Sim's synthetic data generation with Engine M2's probabilistic modeling capabilities, enabling comprehensive testing of PMF-based workflows.*
