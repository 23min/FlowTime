# UI Integration Guide for PMF Testing

## Overview

With SIM-M2.1, FlowTime-Sim now supports PMF (Probability Mass Function) arrival generation, enabling complete end-to-end testing of probabilistic workflows between Sim, Engine, and UI.

## PMF Testing Workflow

### 1. Generate PMF Data with Sim
```yaml
# Example: Fibonacci-like distribution
schemaVersion: 1
rng: pcg
seed: 123

arrivals:
  kind: pmf
  values: [1, 2, 3, 5, 8]          # Discrete arrival counts
  probabilities: [0.1, 0.2, 0.3, 0.3, 0.1]  # Corresponding probabilities
  # Expected value: 3.6 arrivals per bin

route:
  id: COMP_A
```

**Via Service API:**
```http
POST /v1/sim/run
Content-Type: text/plain

[YAML configuration above]
```

**Via CLI:**
```bash
dotnet run --project src/FlowTime.Sim.Cli -- \
  --mode sim \
  --model examples/m15.complex-pmf.yaml \
  --out out/pmf-test
```

### 2. Generated Artifacts

**Standard FlowTime Artifacts (UI-compatible):**
- `manifest.json` - Run metadata with PMF configuration
- `series/index.json` - Series catalog for data binding
- `series/arrivals@COMP_A.csv` - PMF-sampled arrival data
- `spec.yaml` - Original PMF specification

**Sample Output (`arrivals@COMP_A.csv`):**
```csv
t,value
0,3    # Bin 0: 3 arrivals (sampled from PMF)
1,2    # Bin 1: 2 arrivals
2,5    # Bin 2: 5 arrivals
3,8    # Bin 3: 8 arrivals
...
```

### 3. Engine Processing

Use the PMF arrival data as input to Engine M2 PMF nodes:

```yaml
# Engine model using Sim-generated PMF data
nodes:
  - id: pmf_validation
    kind: pmf
    expression: "arrivals@COMP_A"  # References Sim-generated series
    pmf:
      values: [1, 2, 3, 5, 8]
      probabilities: [0.1, 0.2, 0.3, 0.3, 0.1]
```

### 4. UI Testing Scenarios

#### Scenario A: PMF Distribution Validation
**Goal:** Verify that Sim-generated PMF data follows specified distribution

```typescript
// UI Test (pseudo-code)
const simRun = await simService.generatePmfRun({
  values: [1, 2, 3, 5, 8],
  probabilities: [0.1, 0.2, 0.3, 0.3, 0.1]
});

// Validate statistical properties
const arrivals = await simRun.getArrivals('COMP_A');
const actualDistribution = calculateDistribution(arrivals);
expectDistributionMatches(actualDistribution, expectedPmf, tolerance=0.05);
```

#### Scenario B: End-to-End PMF Workflow
**Goal:** Complete Sim→Engine→UI PMF processing validation

```typescript
// 1. Generate PMF data with Sim
const simRun = await simService.generatePmfRun(pmfConfig);

// 2. Process through Engine PMF nodes
const engineRun = await engineService.run({
  data: simRun.artifacts,
  model: pmfModelWithNodes
});

// 3. Validate results in UI
const results = await engineRun.getResults();
expectPmfCalculationsCorrect(results, expectedStatistics);
```

#### Scenario C: Deterministic Testing
**Goal:** Ensure reproducible PMF generation for consistent testing

```typescript
const seed = 42;
const run1 = await simService.generatePmfRun({...config, seed});
const run2 = await simService.generatePmfRun({...config, seed});

// Should be identical
expectArraysEqual(run1.arrivals, run2.arrivals);
```

## PMF Configuration Options

### Basic PMF
```yaml
arrivals:
  kind: pmf
  values: [1, 2, 3]
  probabilities: [0.2, 0.5, 0.3]
```

### Complex PMF (Multi-modal)
```yaml
arrivals:
  kind: pmf
  values: [0, 1, 2, 5, 10]
  probabilities: [0.1, 0.2, 0.3, 0.3, 0.1]
```

### Edge Cases
```yaml
# Single value (deterministic)
arrivals:
  kind: pmf
  values: [5]
  probabilities: [1.0]

# Uniform distribution
arrivals:
  kind: pmf
  values: [1, 2, 3, 4]
  probabilities: [0.25, 0.25, 0.25, 0.25]
```

## Validation Rules

### PMF Specification Constraints
- `values` and `probabilities` arrays must have equal length
- All probabilities must be non-negative
- Probabilities must sum to 1.0 (±1e-9 tolerance)
- All values must be non-negative integers

### Error Handling
```yaml
# This will fail validation:
arrivals:
  kind: pmf
  values: [1, 2, 3]
  probabilities: [0.3, 0.3, 0.3]  # Sum = 0.9, not 1.0
```

**Expected Error:**
```
PMF probabilities must sum to 1.0
```

## Integration with Existing UI Features

### Data Binding
PMF-generated data uses the same series contracts as existing generators:

```json
// series/index.json (standard format)
{
  "series": [
    {
      "id": "arrivals@COMP_A",
      "kind": "flow",
      "path": "series/arrivals@COMP_A.csv",
      "unit": "entities/bin",
      "componentId": "COMP_A"
    }
  ]
}
```

### Visualization
UI charts and graphs work unchanged with PMF data - the time series format is identical to const/poisson generators.

### Statistical Analysis
PMF data enables new statistical validation scenarios:
- Distribution fitting tests
- Expected value verification
- Variance analysis
- Chi-square goodness-of-fit tests

## Troubleshooting

### Common Issues

**1. PMF doesn't sum to 1.0**
```yaml
# Wrong:
probabilities: [0.2, 0.3, 0.4]  # Sum = 0.9

# Correct:
probabilities: [0.2, 0.3, 0.5]  # Sum = 1.0
```

**2. Array length mismatch**
```yaml
# Wrong:
values: [1, 2, 3]
probabilities: [0.5, 0.5]  # Only 2 probabilities for 3 values

# Correct:
values: [1, 2, 3]
probabilities: [0.2, 0.3, 0.5]  # Matching lengths
```

**3. Non-deterministic results**
```yaml
# Always specify seed for reproducible testing:
seed: 42
rng: pcg
```

## Next Steps

With PMF generation now available, UI testing can expand to cover:
1. **Statistical Validation** - Verify PMF calculations in Engine M2
2. **Probabilistic Workflows** - Test complex multi-node PMF scenarios  
3. **Performance Testing** - Generate large-scale PMF datasets
4. **Edge Case Coverage** - Test boundary conditions and error handling

## See Also
- [SIM-M2.1 Milestone](../milestones/SIM-M2.1.md) - Technical implementation details
- [PMF Examples](../../examples/) - Sample PMF configurations
- [Engine M2 PMF Nodes](https://docs.flowtime.io/engine/m2/pmf) - Engine PMF documentation
