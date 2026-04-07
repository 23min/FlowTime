# NaN, Infinity, and Division-by-Zero Policy

**Status**: Active  
**Last Updated**: 2026-04-01

## Introduction

FlowTime performs extensive floating-point arithmetic during DAG evaluation — division, modulus, scaling, accumulation. IEEE 754 defines three hazardous results that can arise from this arithmetic:

| Hazard | IEEE 754 default | Example |
|--------|------------------|---------|
| Division by zero | `+/-Infinity` | `10.0 / 0.0` |
| Invalid operation | `NaN` | `0.0 / 0.0` |
| Overflow | `+/-Infinity` | `double.MaxValue * 2` |

Left unchecked, a single `NaN` or `Infinity` value propagates through every downstream computation (NaN is "sticky" — any arithmetic involving NaN produces NaN). In a queue-depth accumulation like `depth[t] = depth[t-1] + inflow[t] - outflow[t]`, a single NaN inflow bin would make every subsequent bin NaN, destroying the entire series.

FlowTime uses a **three-tier policy** to handle these hazards, choosing the semantically correct response for each site rather than applying a blanket rule.

## Three-Tier Policy

### Tier 1: Return 0.0 — "No activity in this bin"

When the divisor is zero and the context is "how much flow passes through", the correct answer is zero — no flow was routed, no capacity was consumed, no contribution was made.

| File | Guard | Rationale |
|------|-------|-----------|
| `ExprNode.cs` (BinaryOp.Divide) | `right[i] != 0.0 ? left[i] / right[i] : 0.0` | Expression division: zero divisor means "no scaling" |
| `ExprNode.cs` (MOD function) | `Math.Abs(d) <= double.Epsilon ? 0d : Modulo(value[i], d)` | Modulus by zero: no remainder to compute |
| `EdgeFlowMaterializer.cs` | `totalWeight <= 0 ? 0d : weight / totalWeight` | Route weight fraction: zero total weight means no flow routed |
| `RouterFlowMaterializer.cs` | `totalWeight <= 0 ? totalWeight = weightRoutes.Count` | Same as above, for router paths |
| `ServiceWithBufferNode.cs` (Safe) | `double.IsFinite(v) ? v : 0d` | Queue accumulation: NaN/Infinity inflow treated as zero activity |
| `ClassContributionBuilder.cs` (Safe) | Multiple overloads guard index bounds and dict lookups | Contribution scaling: missing data means zero contribution |
| `ClassContributionBuilder.cs` (weight fraction) | `totalWeight <= 0 ? totalWeight = weightRoutes.Count` | Same pattern as EdgeFlowMaterializer |

**Key invariant**: Tier 1 sites never produce NaN or Infinity. The output is always a finite `double`.

### Tier 2: Return null — "Metric unavailable"

When a derived metric (utilization, latency) cannot be meaningfully computed because the denominator is zero or absent, the correct answer is "this metric does not exist for this bin" — represented as `null` in the nullable `double?` return type.

| File | Guard | Rationale |
|------|-------|-----------|
| `UtilizationComputer.cs` | `capacity is null \|\| capacity <= 0 → return null` | Utilization = served/capacity; zero or null capacity means utilization is undefined |
| `RuntimeAnalyticalEvaluator.cs` | `served <= 0 \|\| binMs <= 0 → return null` | Latency = (queue/served) * binDuration; zero served means no items processed, latency undefined |

**Key invariant**: Tier 2 sites return `double?`. Callers must handle `null` (typically by omitting the metric from output or displaying "N/A").

### Tier 3: NaN sentinel — "Data not provided"

When loading external data (CSV files, semantic constraint definitions), missing or unparseable values are represented as `double.NaN`. This sentinel propagates through expressions, alerting downstream consumers that the data was never supplied.

| File | Guard | Rationale |
|------|-------|-----------|
| `SemanticLoader.cs` | Missing constraint data → `double.NaN` array | Constraint data not provided in model directory |
| `CsvReader.cs` | Unparseable cell → `double.NaN` | CSV cell is empty or non-numeric |

**Key invariant**: Tier 3 NaN values are created at ingestion boundaries. They propagate through expression evaluation (FLOOR, CEIL, ROUND, MOD of NaN all return NaN per IEEE 754). The `Safe()` guard in ServiceWithBufferNode converts them to 0.0 before queue accumulation (Tier 1).

### Exception: Invalid PMF

A `Pmf` constructed with all-zero probabilities is a **programming error**, not a data condition. The correct response is to fail fast.

| File | Guard | Rationale |
|------|-------|-----------|
| `Pmf.cs` | `sum <= 0 → throw ArgumentException` | Cannot normalize a zero-sum distribution; caller has a bug |

## Design Rationale

### Why not full NaN propagation?

Some numeric systems (NumPy, R) propagate NaN through all operations, forcing the caller to handle it at the end. This works well for vectorized analytics where the user inspects results interactively.

FlowTime's queue-depth math is **cumulative**: `depth[t] = depth[t-1] + inflow[t] - outflow[t]`. A single NaN in bin 3 would make bins 3 through N all NaN, destroying the entire series. The `Safe()` guard (Tier 1) prevents this cascade by treating non-finite inflow as zero activity — the queue simply does not grow in that bin.

### Why not always return 0.0?

Utilization and latency are ratios that answer specific questions ("how busy is this resource?", "how long does a work item wait?"). Returning 0.0 when capacity is zero would falsely claim "this resource is 0% utilized" when the truth is "utilization is undefined because there is no resource." Returning `null` (Tier 2) allows the UI and API to distinguish "idle" from "not applicable."

### Why NaN at ingestion?

CSV files and semantic data may legitimately have missing values. Using `NaN` as a sentinel (Tier 3) allows expressions to propagate the "missing" signal without inventing data. The `Safe()` guard at the ServiceWithBufferNode boundary converts NaN to 0.0 before it enters cumulative math.

## Comparison with Other Systems

| System | div/0 | 0/0 | Missing data | Philosophy |
|--------|-------|-----|-------------|------------|
| Excel | `#DIV/0!` error | `#DIV/0!` error | Empty cell / `#N/A` | Error propagation, user fixes |
| NumPy | `inf` (with warning) | `nan` (with warning) | `np.nan` | IEEE 754 + warnings |
| Spark | `null` (SQL mode) or `inf` | `null` or `nan` | `null` | SQL null semantics |
| R | `Inf` | `NaN` | `NA` | Three distinct sentinels |
| SQL / dbt | `NULL` (most dialects) | `NULL` | `NULL` | Everything nullable |
| **FlowTime** | **0.0 or null (by tier)** | **0.0 (Tier 1)** | **NaN (Tier 3)** | **Tier-based: 0.0 → null → NaN** |

FlowTime's approach is closest to a combination of SQL (null for undefined metrics) and defensive numeric programming (0.0 for flow math), with IEEE 754 NaN reserved for the ingestion boundary.

## Adding New Division Sites

When introducing a new division, modulus, or ratio computation:

1. **Determine the tier.** Ask: "If the divisor is zero, what does that mean in the domain?"
   - "No activity / no flow" → **Tier 1**: return `0.0`
   - "Metric is undefined" → **Tier 2**: return `null` (use `double?`)
   - "Data was not provided" → **Tier 3**: use `double.NaN` sentinel
   - "Programming error" → **Exception**: throw `ArgumentException`

2. **Add a guard.** Place the check immediately before the division. Do not rely on downstream consumers to handle `Infinity` or `NaN`.

3. **Add a test.** Add a test case in `tests/FlowTime.Core.Tests/Safety/NaNPolicyTests.cs` under the appropriate tier section. Test the zero-divisor case and, where applicable, the NaN-input case.

4. **Update this document.** Add a row to the appropriate tier table with the file, guard expression, and rationale.
