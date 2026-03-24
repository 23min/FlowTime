# FlowTime Engine Deep Review — March 2026

**Reviewer**: Senior flow-systems engineer (independent review)
**Scope**: Full engine, contracts, tests, and interaction surfaces
**Date**: 2026-03-23

---

## Executive Summary

FlowTime is a deterministic, discrete-time, graph-based compute engine for modeling and analyzing flows of work through systems. After a thorough review of the codebase (~62 C# source files in the engine, ~195 test files, 20+ architecture documents), this report evaluates:

1. **Is FlowTime solving the right problem?** Mostly yes — with important gaps.
2. **Is it solving the problem correctly?** The core is solid, but there are real bugs and design contradictions.
3. **Is the code doing what it claims?** Several implementation gaps between documentation and code.

**Overall Assessment**: The engine has a strong conceptual foundation and clean architecture. The spreadsheet metaphor (DAG of time-aligned series) is a genuinely good abstraction for flow modeling. However, the engine currently operates at the **volume/throughput layer** and lacks the semantic machinery needed for deeper flow analysis (cycle time distributions, aging WIP, bottleneck detection, Monte Carlo forecasting). There are also 3 confirmed correctness bugs, several design contradictions, and meaningful test coverage gaps.

**Verdict**: Solid B-. Excellent bones, clear vision, good engineering discipline. Needs targeted fixes for the bugs and a deliberate expansion of its analytical vocabulary to become a complete flow analysis platform.

---

## Part 1: Is FlowTime Solving the Right Problem?

### 1.1 Flow Theory Concept Coverage

I evaluated FlowTime against 12 foundational flow analysis concepts. For each, I rate **Importance** (how critical the concept is for flow analysis) and **FlowTime Coverage** (how well the engine captures it).

| # | Concept | Importance | Coverage | Rating | Notes |
|---|---------|-----------|----------|--------|-------|
| 1 | **Little's Law** (L = λW) | Critical | Partial | 5/10 | LatencyComputer implements queue/served*binMinutes — correct for point estimates. But no support for the four assumptions (steady state, consistent measurement, stable WIP age, sufficient window). No aggregate cycle time computation. |
| 2 | **Theory of Constraints** | Critical | Minimal | 2/10 | No bottleneck identification. ConstraintAllocator exists (67 lines) but has zero callers in evaluation. No drum-buffer-rope. No five focusing steps. Utilization thresholds in ColoringRules are hardcoded (70%/90%) but not linked to ToC analysis. |
| 3 | **Cumulative Flow Diagrams** | Critical | Good | 7/10 | Edge time bins provide the per-edge flow data needed for CFDs. The `/state_window` API returns windowed series. Conservation checks validate flow consistency. Missing: explicit CFD computation (band widths, slope analysis, approximate cycle time derivation from horizontal distance). |
| 4 | **Queueing Theory** | High | Partial | 4/10 | ServiceWithBufferNode models M/M/1-style queue accumulation. LatencyComputer derives waiting time. UtilizationComputer gives ρ. But no arrival rate variability (Cv²), no Kingman's approximation, no queue length distributions, no multi-server (M/M/c) models. Parallelism field exists but is typed as `object?` with unclear semantics. |
| 5 | **Flow Metrics (Kersten)** | High | Partial | 4/10 | Flow Velocity ≈ throughput (served per bin) — captured. Flow Load ≈ WIP (queue depth) — captured. Flow Time ≈ latency — partially captured (point estimate only, not distribution). Flow Efficiency (value-add time / total time) — not captured. Flow Distribution (by type) — classes support this partially but no built-in analysis. |
| 6 | **Bottleneck Analysis** | Critical | Minimal | 2/10 | Utilization is computed per-node. ColoringRules flags >90% utilization as red. But no cross-node comparison, no WIP accumulation pattern detection, no throughput-comparison heuristics, no automated bottleneck identification. The anomaly-detection epic is listed as "aspirational." |
| 7 | **WIP Limits** | High | None | 1/10 | Queue depth is tracked but no WIP limit enforcement. ServiceWithBufferNode accumulates unbounded queues. No pull-based / back-pressure mechanics. No Little's Law-based WIP limit recommendations. |
| 8 | **Variability Analysis** | High | None | 1/10 | PMFs model input variability for simulation, but the engine reduces PMFs to expected values immediately. No coefficient of variation (Cv) computation. No Kingman VUT decomposition. No variability impact analysis. |
| 9 | **Multi-Stage Pipeline** | High | Good | 7/10 | The DAG topology naturally models multi-stage pipelines. Edge metrics track flow between stages. ServiceWithBufferNode queues model inter-stage buffering. Missing: explicit stage-level cycle time decomposition (queue time vs. processing time per stage). |
| 10 | **Starvation & Blocking** | Medium | Minimal | 2/10 | ServiceWithBufferNode can show starvation (queue = 0, capacity unused). But no explicit starvation detection. No blocking detection (downstream full). No back-pressure propagation. DispatchSchedule can model periodic blocking but it's manual, not derived. |
| 11 | **Monte Carlo Simulation** | Medium | None | 0/10 | Not in scope per charter ("deterministic by default"). FlowTime.Sim generates synthetic data but does not run Monte Carlo forecasting. No probabilistic delivery date estimation. No percentile-based forecasts. This is a deliberate scope choice, not a bug. |
| 12 | **Aging WIP** | Medium | None | 0/10 | No per-item tracking (flow-first, not event-based). No aging threshold computation. No SLE (Service Level Expectation) percentile tracking. Queue depth is a scalar per bin — no way to know how long individual items have been waiting. |

### 1.2 What FlowTime Does Well

**The spreadsheet metaphor is genuinely excellent.** Modeling flows as DAGs of time-aligned series is:
- Intuitive (anyone who uses spreadsheets can understand it)
- Deterministic (same inputs = same outputs, always)
- Composable (add nodes/edges to extend the model)
- Analyzable (any series can be inspected, compared, overlaid)

**The conservation-checking InvariantAnalyzer is a standout feature.** Most flow tools don't verify that flow is conserved across the system. FlowTime's approach of emitting warnings when `arrivals ≠ served + errors + Δqueue` is genuinely valuable for catching modeling errors.

**Edge time bins are architecturally sound.** Having per-edge throughput/effort/terminal series with explicit metrics enables CFD-style analysis and path-level queries.

**Retry modeling via convolution is elegant.** The CONV operator with causal kernels correctly models temporal feedback without creating algebraic loops. This is a clean solution to a hard problem.

### 1.3 What FlowTime Is Missing (Critical Gaps)

**Gap 1: No Cycle Time Distributions**
The engine computes point-estimate latency (`queue/served * binMinutes`) but cannot produce cycle time distributions. In flow analysis, knowing the distribution shape (p50, p85, p95) is often more important than the mean. This is fundamental to SLE-based management.

**Gap 2: No Bottleneck Identification**
The engine computes per-node utilization but does not compare across nodes to identify the system bottleneck. In Theory of Constraints, identifying the constraint is step 1. FlowTime has the data but not the analysis.

**Gap 3: No WIP Limit Enforcement**
Queue depth grows without bound. There is no mechanism to model pull-based systems or back-pressure. In Kanban-style flow management, WIP limits are the primary control lever. Without them, FlowTime can model but cannot recommend.

**Gap 4: No Variability Decomposition**
The engine reduces all variability to expected values at compile time. The coefficient of variation — arguably the most important parameter in queueing theory — is never computed or preserved.

**Gap 5: No Flow Efficiency**
The ratio of value-add time to total cycle time is a key Flow Framework metric. FlowTime tracks processing time (ProcessingTimeMsSum) and queue depth but does not compute the ratio explicitly.

**Gap 6: Dependency Constraints Are Scaffolding Only**
The ConstraintAllocator exists but is never called during evaluation. Models can declare constraints; they are silently ignored at runtime. This is documented in the existing review findings but bears repeating: the feature does not work.

### 1.4 Assessment: Right Problem?

**Score: 7/10 — Solving a valuable problem, but the analytical vocabulary is too narrow.**

FlowTime correctly identifies that flows through systems need a compute engine. The spreadsheet/DAG approach is sound. But the engine currently operates primarily at the **volume layer** (arrivals, served, queue depth) and does not yet reach the **analytical layer** (bottleneck identification, cycle time distributions, variability decomposition, flow efficiency, WIP limit recommendations).

For a "compute engine for flows," I'd expect the output to directly answer questions like:
- "Where is the bottleneck?" — Not answerable today
- "What is the p85 cycle time?" — Not answerable today
- "What would happen if we set a WIP limit of 5?" — Not answerable today
- "How much of cycle time is queue time vs processing time?" — Partially answerable
- "Are arrivals steady enough for Little's Law to apply?" — Not answerable today

---

## Part 2: Is FlowTime Solving the Problem Correctly?

### 2.1 Confirmed Bugs (Verified by Code Reading)

#### BUG-1 [CRITICAL]: ServiceWithBufferNode Mutates Shared Memoized Series

**File**: `src/FlowTime.Core/Nodes/ServiceWithBufferNode.cs:52-56`
**File**: `src/FlowTime.Core/Dispatching/DispatchScheduleProcessor.cs:7-33`

```csharp
// ServiceWithBufferNode.Evaluate():
var outflow = getInput(outflowId);  // Returns MEMOIZED series from Graph
// ...
DispatchScheduleProcessor.ApplySchedule(
    dispatchSchedule.PeriodBins,
    dispatchSchedule.PhaseOffset,
    outflow,           // <-- MUTATES the memoized series in place!
    scheduleCapacity);
```

`DispatchScheduleProcessor.ApplySchedule` directly writes to `target[i] = 0d` (line 21), which mutates the `outflow` Series object. Since this Series was obtained via `getInput()` which returns the memoized result from `Graph.EvaluateInternal`, **any other node that references the same outflow series will see corrupted (zeroed) values**.

This bug is latent — it only manifests when:
1. A node has a dispatch schedule, AND
2. Another node also consumes the same outflow series

**Fix**: Clone the outflow series before passing to `ApplySchedule`:
```csharp
var outflowClone = new Series(outflow.ToArray());
DispatchScheduleProcessor.ApplySchedule(..., outflowClone, ...);
```

#### BUG-2 [HIGH]: ServiceWithBufferNode.Inputs Omits Dispatch Capacity Dependency

**File**: `src/FlowTime.Core/Nodes/ServiceWithBufferNode.cs:20-22`

```csharp
public IEnumerable<NodeId> Inputs => lossId.HasValue
    ? new[] { inflowId, outflowId, lossId.Value }
    : new[] { inflowId, outflowId };
```

When a dispatch schedule specifies a `CapacitySeriesId`, that series is fetched via `getInput(capacityId)` at line 47. But the `Inputs` property does not include `capacityId`. This means:

1. The topological sort does not know about this dependency
2. If the capacity series node is sorted AFTER this node, `getInput(capacityId)` will throw `KeyNotFoundException` because the memo doesn't have it yet
3. If the capacity node happens to be sorted before (by chance), it works — making this a non-deterministic ordering bug

**Fix**: Include the capacity series ID in `Inputs` when a dispatch schedule is present.

#### BUG-3 [MEDIUM]: InvariantAnalyzer.ValidateQueue Ignores Dispatch Schedules

**File**: `src/FlowTime.Core/Analysis/InvariantAnalyzer.cs` (ValidateQueue method)

The invariant analyzer validates queue depth using `Q[t] = max(0, Q[t-1] + inflow[t] - outflow[t] - loss[t])`. But ServiceWithBufferNode applies a dispatch schedule that zeros out outflow on non-dispatch bins. The invariant check does not replicate this transformation, so it will produce **false positive "queue_depth_mismatch" warnings** for any node with a dispatch schedule.

### 2.2 Design Contradictions

#### CONTRADICTION-1: Series Claims Immutability, Has Public Setter

**File**: `src/FlowTime.Core/Execution/Series.cs:4,22-23`

```csharp
/// <summary>
/// Immutable numeric series aligned to a TimeGrid.    // <-- LIE
/// </summary>
public double this[int t]
{
    get => data[t];
    set => data[t] = value;  // <-- MUTABLE
}
```

This isn't just a documentation issue — it's the root cause of BUG-1. If Series were truly immutable, `DispatchScheduleProcessor.ApplySchedule` couldn't corrupt memoized data. The entire design should commit to one direction: either Series is immutable (remove setter, return new copies) or it's explicitly mutable (update the doc, add clone-before-share discipline).

#### CONTRADICTION-2: Graph Runs Kahn's Algorithm Twice

**File**: `src/FlowTime.Core/Execution/Graph.cs:17-50, 81-106`

The constructor calls `ValidateAcyclic()` which implements Kahn's algorithm. Then every call to `Evaluate()` calls `TopologicalOrder()` which reimplements the same algorithm. Both iterate `O(N * E)` over all nodes. The topological order should be computed once in the constructor and cached.

#### CONTRADICTION-3: Constraint System Declared But Not Enforced

The charter, roadmap, and capabilities doc all reference dependency constraints as "shipped." `ConstraintAllocator.cs` exists with correct proportional allocation logic. But there are zero callers in the evaluation pipeline. Models can declare constraints; they are silently ignored. This was noted in the prior review findings but has not been addressed.

### 2.3 NaN/Infinity Policy Inconsistency

There is no consistent policy for handling non-finite values across the engine:

| Location | Behavior |
|----------|----------|
| `ExprNode` division | `0.0` on zero divisor (exact `!= 0.0` check) |
| `ExprNode` MOD | `0.0` when divisor `<= double.Epsilon` (~5e-324) |
| `ExprNode` CONV | Skips non-finite samples (`continue`) |
| `ServiceWithBufferNode.Safe()` | Converts NaN/Infinity to `0.0` |
| `BinaryOpNode` | No guard — NaN/Infinity propagates freely |
| `ClassMetricsAggregator` | Converts NaN to `null` |
| `LatencyComputer` | Returns `null` when served <= 0, but NaN queue → NaN result |
| `UtilizationComputer` | No guard — NaN/Infinity propagates |

**Impact**: A NaN introduced in one part of the graph can propagate unpredictably — zeroed in some paths, passed through in others, converted to null in yet others. This makes debugging difficult and could produce silently wrong results.

**Recommendation**: Define a formal policy. My suggestion: NaN should propagate (not be silently zeroed) so problems are visible. Division by zero should produce NaN (not 0.0). `Safe()` should emit a warning rather than silently converting.

### 2.4 Performance Issues

| Issue | Location | Impact |
|-------|----------|--------|
| O(N*E) topological sort | `Graph.cs:40-47, 95-102` | For a 500-node graph with avg 3 inputs, this is ~750K iterations per eval. Should use adjacency list. |
| Duplicate Kahn's on every eval | `Graph.cs:14, 64` | Topological order recomputed on every `Evaluate()` call. Should cache. |
| ClassContributionBuilder.Build called 4x | `InvariantAnalyzer.cs` | Same expensive computation repeated in 4 different methods. Cache result. |
| Per-line CSV writes | `RunArtifactWriter.cs` | 3 async writes per row (bin, comma, value). For 10K bins = 30K writes. Use StringBuilder. |
| Linear node lookup | `Topology.GetNode()` | O(N) per call via `FirstOrDefault`. Should use Dictionary. |
| Linear edge lookup | `Topology.GetOutgoing/IncomingEdges()` | O(E) per call via `Where`. Should precompute adjacency. |

### 2.5 Latency Formula: Correct But Limited

`LatencyComputer.Calculate(queue, served, binMinutes)` computes `(queue / served) * binMinutes`.

This is a valid point-estimate of **average waiting time** derived from Little's Law, where:
- L = queue (average WIP in the bin)
- λ = served / binMinutes (throughput rate)
- W = L / λ = queue / served * binMinutes

**Limitations**:
1. Returns null when served = 0, losing the signal that queue is growing with no output
2. Sensitive to bin granularity — a 1-hour bin averages away intra-hour dynamics
3. Assumes FIFO — if there's priority-based scheduling, this average is misleading
4. Point estimate only — no distribution information

This is acceptable for the current scope but should be documented as "approximate average latency under FIFO, derived from Little's Law point estimate."

---

## Part 3: Code Quality Assessment

### 3.1 Architecture Strengths

1. **Immutable records for topology** — Node, Edge, Topology are properly record-based
2. **DAG validation in constructor** — Cycles caught immediately
3. **Conservation-checking invariant analyzer** — Unusual and valuable
4. **Clean separation** — Contracts/Core/Sim/API layers have clear responsibilities
5. **Provenance tracking** — Every derived series labeled with origin
6. **Template → Model compilation** — Clean pipeline from authoring surface to execution
7. **Expression system** — 11 functions covering temporal ops (SHIFT, CONV), math, and pulses

### 3.2 Code Smells

1. **`#pragma warning disable CS8602`** in RunArtifactWriter — suppresses null-reference warnings globally
2. **`Console.WriteLine`** in ModelCompiler — library code should use ILogger
3. **`dynamic` typing** in RunArtifactWriter — bypasses compile-time safety despite having typed inputs
4. **`Parallelism` typed as `object?`** — should be a discriminated union (fixed int vs series ref)
5. **ShiftNode implements IStatefulNode with dead code** — history queue is never used
6. **PCG32.NextInt has modulo bias** — `NextUInt32() % range` biases when range doesn't divide 2^32

### 3.3 Test Coverage Assessment

**Quantity**: ~195 test files with ~44,800 lines of test code — impressive volume.

**Quality**: Good for happy paths, weak for edge cases and error paths.

| Area | Coverage | Quality | Notes |
|------|----------|---------|-------|
| Template parsing | High | Good | 33 tests in TemplateParserTests |
| Provenance | High | Excellent | 15 tests, culture-invariant, deterministic |
| Model compilation | Medium | Good | Derived node synthesis verified |
| Invariant analysis | Medium | Adequate | Template-level warnings tested |
| Expression functions | Low | Gap | MOD/FLOOR/CEIL/ROUND/STEP/PULSE lack dedicated tests |
| Dispatch schedules | Medium | Adequate | ApplySchedule tested |
| Router flow | Low | Weak | Limited multi-class scenarios |
| Edge flow materialization | Low | Weak | Complex routing untested |
| End-to-end determinism | None | Missing | Charter headline "same inputs → same outputs" has no bitwise test |
| Shared-series mutation | None | Missing | BUG-1 would be caught by a test where two nodes share outflow + dispatch |
| Performance regression | Minimal | Weak | Only Provenance <100ms benchmark |

**Critical missing tests**:
1. Two nodes consuming same outflow where one has a dispatch schedule (BUG-1)
2. Dispatch schedule with CapacitySeriesId (BUG-2)
3. End-to-end determinism: same template + seed → bitwise-identical artifacts
4. ExprNode with deeply nested expressions (stack overflow risk)
5. Graph with 500+ nodes (performance regression)
6. NaN/Infinity propagation through various node types

---

## Part 4: Theoretical Completeness Scorecard

### Required Data Properties for Flow Analysis

| Property | Status | Engine Location | Gap |
|----------|--------|----------------|-----|
| Arrival rate per stage | ✅ Captured | `Arrivals` series | — |
| Throughput per stage | ✅ Captured | `Served` series | — |
| WIP / Queue depth | ✅ Captured | `QueueDepth` series | No WIP limits |
| Error / failure rate | ✅ Captured | `Errors`, `Failures` series | — |
| Capacity per stage | ✅ Captured | `Capacity` series | — |
| Utilization | ✅ Derived | `UtilizationComputer` | No upper bound / negative guard |
| Average latency | ✅ Derived | `LatencyComputer` | Point estimate only |
| Retry volume | ✅ Modeled | CONV-based retry echo | Elegant implementation |
| Multi-class flows | ✅ Captured | `ByClass` dictionaries | No cross-class priority |
| Edge flow volumes | ✅ Captured | `EdgeFlowMaterializer` | Good for CFD construction |
| Cycle time distribution | ❌ Missing | — | Cannot produce p50/p85/p95 |
| Flow efficiency | ❌ Missing | — | No queue-time / processing-time ratio |
| Bottleneck identification | ❌ Missing | — | No cross-node analysis |
| Variability (Cv) | ❌ Missing | — | PMFs reduced to E[X] at compile |
| WIP limits / back-pressure | ❌ Missing | — | Queues grow unbounded |
| Aging WIP | ❌ Missing | — | No per-item time tracking |
| Monte Carlo forecast | ❌ Out of scope | — | Deliberate design choice |
| Starvation detection | ❌ Missing | — | Data exists, no analysis |
| Blocking detection | ❌ Missing | — | No downstream capacity check |
| Constraint enforcement | ❌ Scaffolded | `ConstraintAllocator` | Zero callers in eval |

### Concept-Level Ratings Summary

| Concept | Importance | FlowTime Rating | Verdict |
|---------|-----------|-----------------|---------|
| Little's Law | Critical | 5/10 | Point estimate only; no assumption validation |
| Theory of Constraints | Critical | 2/10 | Utilization threshold only; no identification |
| CFDs | Critical | 7/10 | Good data; missing explicit CFD derivation |
| Queueing Theory | High | 4/10 | Basic M/M/1; no variability decomposition |
| Flow Metrics (Kersten) | High | 4/10 | Velocity+Load captured; Time+Efficiency+Distribution partial |
| Bottleneck Analysis | Critical | 2/10 | Utilization coloring only; no system-level analysis |
| WIP Limits | High | 1/10 | Queue tracked but never limited |
| Variability | High | 1/10 | Reduced to expected values at compile |
| Multi-Stage Pipeline | High | 7/10 | DAG topology is natural fit |
| Starvation & Blocking | Medium | 2/10 | Observable in data; no detection |
| Monte Carlo | Medium | 0/10 | Out of scope (deliberate) |
| Aging WIP | Medium | 0/10 | Flow-first design cannot track item age |

---

## Part 5: Recommendations

### P0 — Fix Before Shipping Further

1. **Fix BUG-1**: Clone outflow series before dispatch schedule mutation in `ServiceWithBufferNode.Evaluate()`
2. **Fix BUG-2**: Include `CapacitySeriesId` in `ServiceWithBufferNode.Inputs`
3. **Fix BUG-3**: Make `InvariantAnalyzer.ValidateQueue` dispatch-schedule-aware
4. **Cache topological order** in `Graph` constructor (fixes O(N²) duplicate computation)
5. **Define and enforce NaN/Infinity policy** across all node types

### P1 — High-Value Analytical Extensions

6. **Add bottleneck identification**: Compare utilization across nodes, flag the node with highest utilization as the system constraint. Optionally detect WIP accumulation patterns (growing queue depth upstream of a node).

7. **Add cycle time decomposition**: For each ServiceWithBuffer node, compute `queueTime = latency` and `processingTime = processingTimeMsSum / servedCount`. Report both. Compute flow efficiency as `processingTime / (queueTime + processingTime)`.

8. **Add WIP limit modeling**: Allow nodes to declare a `wipLimit`. When queue depth reaches the limit, either stop arrivals (blocking) or divert to overflow (loss). This enables Kanban-style "what-if" analysis.

9. **Preserve variability information**: Instead of immediately reducing PMFs to expected values, optionally preserve the coefficient of variation (Cv = σ/μ) alongside the mean. This enables Kingman's approximation for wait time prediction without full Monte Carlo.

10. **Complete constraint enforcement**: Wire `ConstraintAllocator` into the evaluation pipeline so declared constraints actually cap `served` per bin.

### P2 — Engineering Quality

11. **Make Series truly immutable** (remove setter, return new copies in mutations) or add explicit Clone semantics
12. **Precompute Topology adjacency lists** (node lookup, edge lookup)
13. **Add end-to-end determinism integration test**
14. **Add expression function unit tests** (MOD, FLOOR, CEIL, ROUND, STEP, PULSE edge cases)
15. **Replace `Console.WriteLine`** with `ILogger` in ModelCompiler
16. **Remove `#pragma warning disable CS8602`** from RunArtifactWriter; fix null handling properly
17. **Type `Parallelism`** as a proper discriminated union

### P3 — Future Analytical Capabilities

18. **CFD computation helper**: Given a `/state_window`, compute band widths, slopes, and approximate cycle times from the geometry
19. **Starvation/blocking detection**: Flag bins where queue=0 but capacity>0 (starvation) or where arrivals=0 but upstream queue>0 (blocking)
20. **Path-level analysis**: Already on the roadmap — edge time bins make this feasible
21. **Aging WIP approximation**: Even without per-item tracking, approximate aging from queue depth trends (e.g., if queue monotonically increases for N bins, oldest item is at least N bins old)

---

## Part 6: Documentation vs Implementation Gap

| Claim | Documentation Says | Code Actually Does | Action |
|-------|-------------------|-------------------|--------|
| Expression functions | 4 shipped (SHIFT, MIN, MAX, CLAMP) | 11 shipped (adds CONV, MOD, FLOOR, CEIL, ROUND, STEP, PULSE) | Update docs |
| Dependency constraints | "Options A & B shipped" | Loaded, never enforced | Downgrade to "scaffolded" |
| Time-travel | "Feature-complete" | Read-only artifact queries | Clarify scope |
| Series | "Immutable" | Has public setter, mutated by DispatchScheduleProcessor | Fix code or docs |
| Catalog | Sophisticated system-catalog-as-SSOT | Simple YAML-backed classes | Label as aspirational |
| Router convergence | Implied stable | No iteration limit / convergence guard | Add guard |

---

## Conclusion

FlowTime has the bones of an excellent flow analysis platform. The DAG/spreadsheet metaphor is sound, the conservation-checking is genuinely innovative, and the engineering discipline (provenance, determinism, canonical artifacts) is above average.

The main risks are:

1. **BUG-1 (shared series mutation)** could produce silently wrong results in production models with dispatch schedules — this should be fixed immediately.

2. **The analytical vocabulary is too narrow** for FlowTime to serve as a complete "flow analysis compute engine." Without bottleneck identification, cycle time distributions, variability decomposition, and WIP limit modeling, users will still need external tools for the analysis that matters most.

3. **Documentation drift** creates a trust problem — the gap between what the docs claim and what the code does erodes confidence in the platform.

The path forward is clear: fix the bugs (P0), add the missing analytical primitives (P1), and keep the documentation honest. The foundation is strong enough to build on.
