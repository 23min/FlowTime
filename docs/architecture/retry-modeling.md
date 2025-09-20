# FlowTime-Sim Role in Retry Modeling

**Version:** 2.0 (Charter v1.0 Compliant)  
**Purpose:** Define FlowTime-Sim's charter-compliant role in retry model creation with preview capabilities for modeling validation.

---

## Executive Summary (Charter v1.0 Aligned)

FlowTime-Sim serves as the **modeling front-end** for retry patterns, creating model definitions that FlowTime Engine executes to generate retry telemetry. **FlowTime-Sim creates retry models; FlowTime Engine executes models and generates telemetry.**

### Key Principle: Charter-Compliant Dual Output Strategy

- **Primary Output**: Retry model specifications for FlowTime Engine consumption
- **Secondary Output**: Preview CSV for internal modeling validation and distribution visualization
- **Clear Boundary**: Engine consumes models only; preview data stays within FlowTime-Sim

### Charter-Compliant Architecture

```
┌─────────────────┐    model.yaml     ┌─────────────────┐
│   FlowTime-Sim  │ ──────────────────▶│ FlowTime Engine │
│                 │                    │                 │
│ • Retry models  │                    │ • Execution     │
│ • Preview CSV   │                    │ • Telemetry     │
│   (internal)    │                    │ • Analytics     │
└─────────────────┘                    └─────────────────┘
         │
         ▼
    preview/ (modeling validation only)
```

---

## FlowTime Engine's Retry Architecture (Reference)

Per the engineering whitepaper, FlowTime engine models retry patterns using:

### 1. Expression Language for Retry Processing
```yaml
# FlowTime engine expressions (NOT implemented in FlowTime-Sim)
arrivals_total := arrivals + retries
attempts := MIN(capacity, arrivals_total)  
errors := attempts * fail_rate
retries := CONV(errors, [0.0,0.6,0.3,0.1])  # Temporal convolution
```

### 2. Retry Patterns
- **Internal retries**: Capacity tax or explicit retry modeling
- **Externalized retries**: Failed work re-queued as future arrivals
- **Attempt limits**: Retry exhaustion feeding dead letter queue (DLQ)
- **Status-aware retries**: 4xx vs 5xx failure handling

### 3. Conservation Laws
- `arrivals + retries - served - ΔQ - dlq ≈ 0`
- `attempts >= served` (effort vs throughput distinction)
- `served == successes`

---

## FlowTime-Sim's Charter-Compliant Retry Role

### Primary Output: Retry Model Specifications

#### 1. **Retry Model Definition**
```yaml
# retry-model.yaml (for FlowTime Engine)
retryModel:
  mode: explicit
  kernel: [0.0, 0.6, 0.3, 0.1]
  failureRate: 0.1
  maxAttempts: 3
  statusSplit:
    retryable: 0.7         # 5xx-like failures that retry
    permanent: 0.3         # 4xx-like failures that don't retry
  
# FlowTime Engine processes this model and generates telemetry
```

#### 2. **Stochastic Pattern Definitions**
```yaml
# Distribution specifications for Engine execution
arrivalPatterns:
  serviceA:
    type: poisson
    lambda: 5.0
    timeHorizon: 100
  
failurePatterns:
  serviceA:
    type: bernoulli
    probability: 0.1
```

### Secondary Output: Preview CSV (Internal Modeling Tool)

#### 1. **Distribution Validation**
```
preview/
├── arrivals-preview.csv     # Poisson pattern visualization
├── retry-kernel-preview.csv # Kernel distribution sample
└── validation-metrics.csv   # Statistical validation data
```

#### 2. **Modeling Workflow Enhancement**
- **Quick feedback**: Visualize distribution parameters
- **Parameter tuning**: Iterate on PMF/kernel specifications  
- **Educational value**: See theoretical distributions in practice
- **Model validation**: Check pattern sanity before Engine handoff

#### 3. **Charter Compliance**
- Preview CSV **never leaves FlowTime-Sim**
- Engine **only consumes model specifications**
- Clear separation: modeling validation vs operational telemetry

### What FlowTime-Sim DOES NOT Do (Charter Boundaries)

#### ❌ **Operational Telemetry Generation**
FlowTime-Sim does NOT generate telemetry for Engine consumption:
- No `arrivals@serviceA.csv` for Engine processing
- No `served@serviceA.csv` or `errors@serviceA.csv` for analytics
- No operational time series that Engine would analyze

#### ❌ **Expression Evaluation**
FlowTime-Sim does NOT implement FlowTime Engine capabilities:
- `CONV(errors, kernel)` convolution operations
- `SHIFT`, `DELAY`, `MIN`, `MAX` operators  
- DAG evaluation or dependency resolution
- Real-time retry decision making

#### ❌ **Analytical Processing**
FlowTime-Sim does NOT perform Engine-level analytics:
- Compute derived metrics from retry data
- Perform what-if analysis on retry policies
- Optimize retry parameters
- Provide retry recommendations

---

## Charter-Compliant Model Generation Process

### Phase 1: Model Specification Creation
1. **Define retry patterns**: Kernel specifications, failure rates, attempt limits
2. **Specify distributions**: Poisson parameters, PMF definitions, stochastic patterns
3. **Set constraints**: Capacity limits, class-based behaviors, temporal patterns

### Phase 2: Model Validation (Preview Generation)
1. **Generate preview samples**: Create sample time series for visual inspection
2. **Validate parameters**: Check statistical properties of specified distributions
3. **Test model structure**: Ensure model completeness and consistency
4. **Preview outputs**: Save validation CSV to `preview/` (internal only)

### Phase 3: Model Export
1. **Write model specification**: Complete retry model as `retry-model.yaml`
2. **Generate metadata**: Document model parameters and validation results
3. **Hash validation**: Ensure deterministic model output for same inputs

---

## Example: Charter-Compliant Retry Modeling

### Scenario
- Service fails 10% of attempts
- Retry kernel: `[0.0, 0.6, 0.3, 0.1]` (60% retry next bin, 30% in bin+2, 10% in bin+3)
- Maximum 3 attempts before DLQ

### FlowTime-Sim Model Output
```yaml
# retry-model.yaml (Primary Output - for Engine)
retryModel:
  serviceA:
    failureRate: 0.1
    kernel: [0.0, 0.6, 0.3, 0.1]
    maxAttempts: 3
    arrivalPattern:
      type: poisson
      lambda: 50
      horizon: 10
```

### FlowTime-Sim Preview Output (Internal Validation)
```csv
# preview/retry-kernel-sample.csv (Secondary Output - for Modeler)
t,arrivals_sample,errors_sample,retries_sample
0,47,5,0              # No retries in first bin
1,52,5,3              # 5 * 0.6 from bin 0
2,48,5,5              # 5 * 0.3 + 5 * 0.6 from bins 0,1
3,51,5,2              # 5 * 0.1 + 5 * 0.3 + 5 * 0.6 from bins 0,1,2
```

### FlowTime Engine Processing
FlowTime Engine consumes the model and generates operational telemetry:
```yaml
# FlowTime Engine executes the model:
retries_computed := CONV(errors_actual, [0.0,0.6,0.3,0.1])
# Engine generates actual telemetry from model specification
```

---

## Charter-Compliant Model Schema Requirements

### 1. **Model Specification Format**
```yaml
# Primary output for FlowTime Engine consumption
retryModel:
  components:
    serviceA:
      failureRate: 0.1
      kernel: [0.0, 0.6, 0.3, 0.1]
      maxAttempts: 3
      arrivalPattern:
        type: poisson
        lambda: 50
```

### 2. **Preview Schema (Internal Only)**
```
preview/
├── retry-kernel-sample.csv     # Kernel behavior visualization
├── arrival-pattern-sample.csv  # Distribution preview
└── validation-metrics.csv      # Statistical validation
```

### 3. **Model Metadata Documentation**
```yaml
# model-metadata.yaml
modelInfo:
  version: "1.0"
  type: "retry-model"
  generator: "FlowTime-Sim"
  validation:
    previewGenerated: true
    statisticalChecks: ["kernel-mass", "distribution-shape"]
```

---

## Testing & Validation

### 1. **Conservation Tests**
- Verify `arrivals + retries - served - dlq ≈ constant`
- Ensure `attempts >= served` always
- Validate retry volumes match kernel specifications

### 2. **FlowTime Engine Integration**
- FlowTime engine processes FlowTime-Sim retry artifacts without errors
- `CONV` operations on generated `errors` series produce expected `retries`
- Expression evaluation yields consistent results

### 3. **Temporal Pattern Validation**
- Retry echoes appear at correct time offsets
- Kernel mass distributes correctly across future bins
- DLQ accumulates retry-exhausted work accurately

---

## Future Enhancements (Roadmap Alignment)

### SIM-M7: Enhanced Retry Modeling
- Multi-component retry propagation
- Cross-service retry dependencies  
- Realistic failure rate fluctuations

### SIM-M8: Multi-Class Retry Patterns
- Per-class retry behaviors (VIP vs Standard)
- Class-specific retry kernels and limits
- Priority-aware retry scheduling

### SIM-M9: Stateful Retry Modeling  
- Queue-backed retry patterns
- Backlog-influenced retry rates
- Capacity-aware retry scheduling

---

## Conclusion

FlowTime-Sim's charter-compliant role in retry modeling is to **create retry model specifications** with optional preview capabilities for modeling validation. By maintaining clear boundaries—FlowTime-Sim models, FlowTime Engine executes—both systems excel in their respective domains while preserving excellent modeling UX.

### Key Benefits of Charter-Compliant Architecture:

1. **Clear Separation**: Models vs execution, preserving architectural boundaries
2. **Modeling UX**: Preview CSV enables distribution visualization and parameter tuning  
3. **Engine Integration**: Clean model handoff without telemetry dependency
4. **Educational Value**: Theoretical distributions become tangible through preview tools

This approach enables comprehensive retry model development with immediate feedback while respecting the charter's "never computes telemetry" principle for Engine consumption.
