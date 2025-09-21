# PMF Modeling in FlowTime

> **📋 Charter Context**: PMF modeling capabilities described here support the [FlowTime-Engine Charter](../flowtime-engine-charter.md) execution engine. PMFs enable stochastic modeling within the deterministic artifacts-centric workflow, producing reproducible series outputs that integrate seamlessly with the charter paradigm.

## Overview

Probability Mass Functions (PMFs) are a fundamental tool in FlowTime for modeling uncertainty and variability in business processes. This guide explains PMF concepts, implementation across FlowTime components, and best practices for effective probabilistic modeling within the charter execution framework.

## What are PMFs?

A **Probability Mass Function** defines the probability distribution of a discrete random variable. In FlowTime, PMFs model uncertainty in workflow parameters like arrival rates, attempt counts, or processing volumes.

### Mathematical Foundation

A PMF assigns probabilities to discrete values such that:
- All probabilities are non-negative: `P(X = x) ≥ 0`
- All probabilities sum to 1: `∑ P(X = x) = 1`
- Expected value: `E[X] = ∑ x * P(X = x)`

### Example PMF
```
Arrivals per hour: { 1: 0.1, 2: 0.3, 3: 0.4, 4: 0.2 }
Expected value: 1*0.1 + 2*0.3 + 3*0.4 + 4*0.2 = 2.7 arrivals/hour
```

## PMFs Across FlowTime Components

### 1. FlowTime Engine — Evaluator
**Role**: Consume PMFs as part of the computation graph

**How it deals with PMFs**:
- **Deterministic Compilation**: Compiles PMFs to deterministic series aligned to the canonical grid (no randomness at runtime)
- **Expected Value Computation**: Computes expected values in early milestones (e.g., average retries per bin)
- **Convolution Support**: Applies convolutions when PMFs represent retry/delay kernels (e.g., `errors * retry_kernel → future arrivals`)
- **Telemetry Interchangeability**: Ensures PMFs are interchangeable with telemetry—once compiled, a PMF-driven node is just another input series

**Usage**: Workflow designers embed PMFs in node specifications
```yaml
nodes:
  - id: customer_attempts
    kind: pmf
    pmf: { "1": 0.6, "2": 0.3, "3": 0.1 }
  
  - id: capacity_analysis  
    kind: expr
    expr: served := MIN(capacity, customer_attempts)
```

**Output**: Expected value time series for downstream calculations

> 💡 **Think of the engine as spreadsheet math on vectors, where PMFs are just another formula source.**

### 2. FlowTime-Sim — Generator
**Role**: Produce input data that looks like telemetry

**How it deals with PMFs**:
- **Synthetic Data Generation**: Uses PMFs to synthesize arrivals, retries, or delays when telemetry isn't available
- **Empirical PMF Fitting**: Can take raw telemetry and fit empirical PMFs (e.g., average Monday shape)
- **Telemetry-Compatible Output**: Emits bin-aligned artifacts (`run.json`, `series/*.csv`) so the engine sees no difference between "real" telemetry and "PMF-generated" series
- **What-If Scenarios**: Enables scenario testing by tweaking PMFs (e.g., "burstier Monday arrivals" or "retry tail heavier")

**Usage**: Test engineers create PMF generators for Engine validation
```yaml
arrivals:
  - id: test_arrivals
    component: COMP_A
    kind: pmf
    values: [1, 2, 3, 4]
    probabilities: [0.1, 0.3, 0.4, 0.2]
    grid:
      bins: 24
      binMinutes: 60
```

**Output**: Synthetic events and time series following PMF distribution

> 💡 **Think of the simulator as a data factory that can either replay telemetry or generate synthetic flows from PMFs.**

### 3. FlowTime UI (Future)
**Purpose**: Visual PMF creation and editing for business users

**Features** (Planned):
- Interactive distribution editors
- Historical data analysis and PMF fitting
- Visual validation of PMF properties
- Template-based PMF libraries

## PMF Design Personas

### The Telemetry Interchangeability Principle

A core architectural principle in FlowTime is that **PMFs are interchangeable with telemetry**. This means:

- **Engine Perspective**: Once compiled, a PMF-driven node produces the same type of time series as telemetry-driven nodes
- **Data Sources**: You can start with PMFs for modeling, then swap in real telemetry data without changing your workflow
- **Testing Strategy**: Sim can generate PMF-based synthetic data that the Engine processes identically to real telemetry
- **Hybrid Scenarios**: Mix PMF-modeled uncertainty with known telemetry baselines in the same workflow

This interchangeability enables a smooth progression from initial modeling to production deployment:
```
PMF Modeling → Synthetic Testing → Telemetry Integration → Production Monitoring
```

### Business Analysts
- **Role**: Define uncertainty patterns based on domain knowledge
- **Tools**: Engine YAML specifications, UI distribution editors
- **Focus**: Capture real-world variability in business processes

### Performance Engineers  
- **Role**: Model system behavior under uncertain loads
- **Tools**: Engine capacity analysis, statistical validation
- **Focus**: Capacity planning and performance prediction

### Test Engineers
- **Role**: Generate realistic test data for validation
- **Tools**: Sim PMF generators, synthetic data validation
- **Focus**: Comprehensive testing of PMF-based workflows

### Data Scientists
- **Role**: Extract PMFs from historical data and telemetry
- **Tools**: Statistical analysis, data export to FlowTime formats
- **Focus**: Evidence-based PMF design and validation

## PMF Specification Formats

### Engine Format (YAML)
```yaml
nodes:
  - id: service_attempts
    kind: pmf
    pmf:
      "1": 0.4    # 40% chance of 1 attempt
      "2": 0.35   # 35% chance of 2 attempts  
      "3": 0.2    # 20% chance of 3 attempts
      "5": 0.05   # 5% chance of 5 attempts
```

### Sim Format (YAML)
```yaml
arrivals:
  - id: pmf_arrivals
    component: SERVICE_A
    kind: pmf
    values: [1, 2, 3, 5]
    probabilities: [0.4, 0.35, 0.2, 0.05]
    grid:
      bins: 168      # One week in hours
      binMinutes: 60
```

### JSON API Format
```json
{
  "kind": "pmf",
  "distribution": {
    "values": [1, 2, 3, 5],
    "probabilities": [0.4, 0.35, 0.2, 0.05]
  },
  "metadata": {
    "source": "historical_analysis_q3_2025",
    "confidence": 0.85
  }
}
```

## Best Practices

### 1. PMF Design Principles

**Start Simple**: Begin with 3-5 discrete values covering typical scenarios
```yaml
# Good: Covers common cases
pmf: { "1": 0.5, "2": 0.3, "3": 0.2 }

# Avoid: Over-detailed initially  
pmf: { "1": 0.12, "2": 0.15, "3": 0.18, "4": 0.22, "5": 0.16, "6": 0.10, "7": 0.07 }
```

**Ground in Data**: Base PMFs on historical evidence when available
```yaml
# Example: Derived from 6 months of ticket data
support_tickets_per_hour:
  pmf: { "0": 0.1, "1": 0.3, "2": 0.4, "3": 0.15, "4": 0.05 }
  source: "support_telemetry_jan_jun_2025"
```

**Validate Expectations**: Verify expected values match business intuition
```yaml
# Expected: 0*0.1 + 1*0.3 + 2*0.4 + 3*0.15 + 4*0.05 = 1.75 tickets/hour
# Does this align with observed averages?
```

### 2. Validation and Testing

**Normalization Checks**: Always verify probabilities sum to 1.0
```csharp
var sum = probabilities.Sum();
if (Math.Abs(sum - 1.0) > 1e-6) 
    throw new ArgumentException($"PMF probabilities must sum to 1.0, got {sum}");
```

**Statistical Testing**: Use chi-square tests for Sim-generated data
```csharp
// Validate that generated samples follow expected distribution
var chiSquare = CalculateChiSquare(observedCounts, expectedCounts);
Assert.IsTrue(chiSquare < criticalValue, "PMF sampling deviates from expected distribution");
```

**Deterministic Output**: Ensure fixed RNG seeds produce identical results
```csharp
var pmf1 = GeneratePmf(values, probabilities, new Random(12345));
var pmf2 = GeneratePmf(values, probabilities, new Random(12345));
CollectionAssert.AreEqual(pmf1, pmf2, "PMF generation must be deterministic");
```

### 3. Performance Considerations

**Sampling Efficiency**: Use cumulative distribution for fast sampling
```csharp
// Efficient O(n) sampling
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
    return values.Last(); // Fallback for floating point precision
}
```

**Memory Management**: Avoid storing large PMF tables in memory
```csharp
// For large PMFs, consider lazy evaluation or streaming
public class StreamingPmf 
{
    public IEnumerable<int> GenerateSamples(int count, Random rng)
    {
        for (int i = 0; i < count; i++)
            yield return SampleFromPmf(values, probabilities, rng);
    }
}
```

## Common PMF Patterns

### 1. Skewed Distributions
**Use Case**: Most events are small, few are large (e.g., request sizes, processing times)
```yaml
request_size_kb:
  pmf: { "1": 0.6, "5": 0.25, "10": 0.1, "50": 0.04, "100": 0.01 }
  # Expected: ~4.9 KB, but high variability
```

### 2. Bimodal Distributions  
**Use Case**: Two distinct operational modes (e.g., peak vs off-peak)
```yaml
hourly_arrivals:
  pmf: { "10": 0.4, "15": 0.1, "45": 0.1, "50": 0.4 }
  # Represents off-peak (10-15) and peak (45-50) traffic
```

### 3. Bounded Distributions
**Use Case**: Physical or logical constraints limit possible values
```yaml
concurrent_users:
  pmf: { "1": 0.2, "2": 0.3, "3": 0.3, "4": 0.15, "5": 0.05 }
  # Maximum 5 users due to license constraints
```

## Integration with Other FlowTime Features

### Expression Integration
```yaml
nodes:
  - id: arrivals
    kind: pmf
    pmf: { "10": 0.3, "20": 0.5, "30": 0.2 }
    
  - id: capacity_utilization
    kind: expr
    expr: |
      utilization := arrivals / capacity
      alert_threshold := CLAMP(utilization, 0, 1)
```

### Routing Integration (Future)
```yaml
nodes:
  - id: request_complexity
    kind: pmf  
    pmf: { "simple": 0.7, "medium": 0.2, "complex": 0.1 }
    
  - id: routing_decision
    kind: router
    routes:
      simple: ["fast_path"]
      medium: ["standard_path"] 
      complex: ["expert_path"]
```

### Convolution and Retry Kernels (Future)
PMFs can represent retry/delay patterns that affect future time bins:
```yaml
nodes:
  - id: retry_kernel
    kind: pmf
    pmf: { "0": 0.1, "1": 0.6, "2": 0.3 }  # Retries in 0, 1, or 2 bins later
    
  - id: error_arrivals
    kind: expr
    expr: |
      base_errors := capacity_exceeded * error_rate
      retry_arrivals := CONV(base_errors, retry_kernel)
      total_arrivals := organic_arrivals + retry_arrivals
```

This enables modeling where errors in one time bin create additional load in future bins based on the retry distribution.

### Batching Integration (Future)
```yaml
nodes:
  - id: batch_size
    kind: pmf
    pmf: { "5": 0.3, "10": 0.5, "15": 0.2 }
    
  - id: batch_processing
    kind: batch_gate
    trigger: size_based
    size_distribution: batch_size
```

## Troubleshooting PMFs

### Common Issues

**Probabilities Don't Sum to 1.0**
```
Error: PMF probabilities must sum to 1.0, got 0.95
Solution: Verify all probability values, check for missing cases
```

**Negative Probabilities**
```
Error: PMF probabilities must be non-negative
Solution: Review data analysis, ensure no calculation errors
```

**Expected Value Mismatch**
```
Issue: PMF expected value doesn't match observed averages
Solution: Re-examine historical data, consider seasonal patterns
```

**Poor Sampling Performance**
```
Issue: PMF sampling is slow for large distributions
Solution: Pre-compute cumulative distribution, use binary search
```

### Debugging Tools

**Statistical Validation**
```csharp
public class PmfValidator
{
    public static ValidationResult Validate(Pmf pmf)
    {
        var issues = new List<string>();
        
        // Check normalization
        var sum = pmf.Probabilities.Sum();
        if (Math.Abs(sum - 1.0) > 1e-6)
            issues.Add($"Probabilities sum to {sum}, not 1.0");
            
        // Check for negative values
        if (pmf.Probabilities.Any(p => p < 0))
            issues.Add("Contains negative probabilities");
            
        // Check for degenerate cases
        if (pmf.Values.Count == 1)
            issues.Add("Single-value PMF should use constant distribution");
            
        return new ValidationResult(issues);
    }
}
```

**Distribution Visualization** (Future UI Feature)
```typescript
// Planned: Interactive PMF visualization
function renderPmfChart(pmf: PmfDefinition) {
    return {
        type: 'bar',
        data: pmf.values.map((value, i) => ({
            x: value,
            y: pmf.probabilities[i]
        })),
        expectedValue: calculateExpectedValue(pmf)
    };
}
```

## Future Enhancements

### Advanced PMF Features (Planned)
- **PMF Convolution**: Combine multiple independent PMFs
- **Conditional PMFs**: Dependencies between random variables  
- **Temporal PMFs**: Time-varying probability distributions
- **Fitted PMFs**: Automatic fitting from historical data

### Integration Roadmap
- **M3+**: PMF convolution for complex uncertainty propagation
- **M5+**: PMF-aware routing and capacity allocation
- **M7+**: Dynamic PMFs adapting to real-time conditions
- **UI-M3+**: Visual PMF editors and validation tools

---

*This guide provides the foundation for effective PMF modeling in FlowTime. As the platform evolves, PMFs will become increasingly powerful tools for capturing and reasoning about uncertainty in complex business processes.*
