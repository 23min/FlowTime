# FlowTime-Sim Role in Retry Modeling

**Version:** 1.0  
**Purpose:** Clarify FlowTime-Sim's role in generating synthetic retry patterns aligned with FlowTime engine's retry/feedback architecture.

---

## Executive Summary

FlowTime-Sim serves as the **synthetic telemetry generator** for retry patterns, producing realistic data that FlowTime engine can consume and process. **FlowTime-Sim generates the data; FlowTime engine analyzes it.**

### Key Principle: Separation of Concerns

- **FlowTime-Sim**: Generates synthetic retry telemetry (the "what happened")
- **FlowTime Engine**: Processes retry data with expressions and DAG evaluation (the "what does it mean")

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

## FlowTime-Sim's Retry Role

### What FlowTime-Sim DOES Generate

#### 1. **Synthetic Flow Series with Retry Patterns**
```
arrivals@serviceA.csv    # Base demand
attempts@serviceA.csv    # Total effort (including retries)  
served@serviceA.csv      # Successful completions
errors@serviceA.csv      # Failed attempts
retries@serviceA.csv     # Retry volume per bin
dlq@serviceA.csv         # Dead letter queue accumulation
```

#### 2. **Temporal Echo Patterns**
FlowTime-Sim generates `errors` and `retries` series that exhibit realistic temporal relationships:
- `retries[t]` shows echoes from previous `errors[t-k]` 
- Follows specified retry kernels: `[0.0, 0.6, 0.3, 0.1]`
- Creates patterns that validate FlowTime's `CONV` operator

#### 3. **Conservation Law Compliance**
Generated synthetic data respects FlowTime's invariants:
- `attempts >= served` always
- Retry volumes match specified kernels
- DLQ accumulates exhausted retry attempts
- Flow conservation holds across the system

#### 4. **Failure Mode Modeling**
```yaml
# FlowTime-Sim retry specification
retries:
  mode: explicit           # vs "tax" 
  kernel: [0.0, 0.6, 0.3, 0.1]
  failureRate: 0.1
  maxAttempts: 3
  statusSplit:
    retryable: 0.7         # 5xx-like failures that retry
    permanent: 0.3         # 4xx-like failures that don't retry
```

### What FlowTime-Sim DOES NOT Do

#### ❌ **Expression Evaluation**
FlowTime-Sim does NOT implement:
- `CONV(errors, kernel)` convolution operations
- `SHIFT`, `DELAY`, `MIN`, `MAX` operators  
- DAG evaluation or dependency resolution
- Real-time retry decision making

#### ❌ **Analytical Processing**
FlowTime-Sim does NOT:
- Compute derived metrics from retry data
- Perform what-if analysis on retry policies
- Optimize retry parameters
- Provide retry recommendations

---

## Retry Data Generation Process

### Phase 1: Base Pattern Generation
1. **Generate arrival patterns**: Base demand using Poisson/constant generators
2. **Apply capacity constraints**: Determine service capacity per bin
3. **Compute base served/errors**: Initial success/failure split

### Phase 2: Retry Pattern Application  
1. **Apply retry kernel**: Generate retry echoes from historical errors
2. **Compute total attempts**: `arrivals + retries` 
3. **Apply attempt limits**: Feed exhausted retries to DLQ
4. **Validate conservation**: Ensure flow balance across bins

### Phase 3: Output Generation
1. **Write flow series**: All retry-related measures to CSV
2. **Generate metadata**: Document retry parameters in `manifest.json`
3. **Hash validation**: Ensure deterministic output for same retry spec

---

## Example: Retry Kernel Validation

### Scenario
- Service fails 10% of attempts
- Retry kernel: `[0.0, 0.6, 0.3, 0.1]` (60% retry next bin, 30% in bin+2, 10% in bin+3)
- Maximum 3 attempts before DLQ

### FlowTime-Sim Generated Data
```csv
# errors@serviceA.csv
t,value
0,10
1,5
2,8

# retries@serviceA.csv  
t,value
0,0              # No retries in first bin
1,6              # 10 * 0.6 from bin 0
2,8              # 10 * 0.3 + 5 * 0.6 from bins 0,1
3,4              # 10 * 0.1 + 5 * 0.3 + 8 * 0.6 from bins 0,1,2
```

### FlowTime Engine Processing
FlowTime engine consumes this data and validates:
```yaml
# FlowTime model validation
retries_computed := CONV(errors, [0.0,0.6,0.3,0.1])
# Should match FlowTime-Sim generated retries@serviceA.csv
```

---

## Schema Compatibility Requirements

### 1. **Series Naming Convention**
- Format: `measure@componentId[.className].csv`
- Examples: `retries@serviceA.csv`, `dlq@queueB.VIP.csv`

### 2. **Required Retry Series**
```
arrivals@{component}.csv    # Base demand
attempts@{component}.csv    # Total effort 
served@{component}.csv      # Successes
errors@{component}.csv      # Failures
retries@{component}.csv     # Retry volume
dlq@{component}.csv         # Dead letter queue
```

### 3. **Metadata Documentation**
```json
// manifest.json
{
  "retryConfig": {
    "mode": "explicit",
    "kernel": [0.0, 0.6, 0.3, 0.1],
    "failureRate": 0.1,
    "maxAttempts": 3
  }
}
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

FlowTime-Sim's role in retry modeling is to **generate realistic synthetic telemetry** that exhibits the retry patterns FlowTime engine expects to process. By maintaining clear separation of concerns—FlowTime-Sim generates, FlowTime engine analyzes—both systems can excel in their respective domains while maintaining perfect schema compatibility.

This approach enables comprehensive testing of FlowTime's retry capabilities without requiring real-world failure scenarios, supporting both development validation and operational what-if analysis.
