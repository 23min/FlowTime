# E-18 Time Machine — Gap Analysis

**Date:** 2026-04-13
**Branch:** `epic/E-18-time-machine`
**Against:** `spec.md` (original) and `milestone-plan-v2.md` (Rust-engine replan)

---

## Delivered milestones

| ID | Title | Status | Notes |
|----|-------|--------|-------|
| m-E18-01 | Parameterized Evaluation (Rust) | ✅ complete | ParamTable, evaluate_with_params, compile-once eval-many in Rust |
| m-E18-02 | Engine Session + Streaming Protocol (Rust) | ✅ complete | Persistent process, MessagePack over stdin/stdout, session holds compiled plan |
| m-E18-06 | Tiered Validation | ✅ complete | TimeMachineValidator (schema/compile/analyse), POST /v1/validate, Rust validate_schema session command |
| m-E18-07 | Generator → TimeMachine | ✅ complete | Rename + restructure; FlowTime.Generator deleted; rg confirms zero residual references |
| m-E18-08 | ITelemetrySource Contract | ✅ complete | Interface + CanonicalBundleSource + FileCsvSource; 23 tests |
| m-E18-09 | Parameter Sweep (.NET) | ✅ complete | SweepSpec/SweepRunner/ConstNodePatcher, IModelEvaluator/RustModelEvaluator, POST /v1/sweep; 35 tests |
| m-E18-10 | Sensitivity Analysis (.NET) | ✅ complete | ConstNodeReader, SensitivitySpec/SensitivityRunner (central difference), POST /v1/sensitivity; 39 tests |
| m-E18-11 | Goal Seeking (.NET) | ✅ complete | GoalSeekSpec/GoalSeeker (bisection), POST /v1/goal-seek; 33 tests. **Not in original spec or v2 plan — added as intermediate mode.** |
| m-E18-12 | Optimization (.NET) | ✅ complete | OptimizeSpec/Optimizer (Nelder-Mead, N params), POST /v1/optimize; 29 unit + 10 API tests |

---

## Spec success criteria — status per criterion

| Criterion | Status | Detail |
|-----------|--------|--------|
| Time Machine callable as `.NET` CLI pipeline: `cat model.yaml \| flowtime evaluate` | ❌ not built | `FlowTime.Cli` has no validate/sweep/optimize/sensitivity commands. The Rust CLI (`flowtime-engine eval/validate/plan`) exists but is a lower-level tool, not the Time Machine pipeline CLI the spec describes. |
| Tiered validation via SDK, CLI, and **sidecar** | ⚠️ partial | SDK ✅ (`TimeMachineValidator`). Sidecar ✅ (Rust `validate_schema` session command). CLI ❌ (no .NET CLI surface). |
| Parameter sweeps without recompilation per evaluation | ⚠️ partial | The Rust engine session (m-E18-02) enables compile-once/eval-many. But `RustModelEvaluator` spawns `flowtime-engine eval` **once per point** — a fresh compile + eval per evaluation. The session-based evaluator that would use m-E18-02's protocol was not built. The `IModelEvaluator` seam exists for future substitution. |
| Optimization finds parameter values satisfying objective + **constraints** | ⚠️ partial | Nelder-Mead optimizer delivered (m-E18-12). Constraints (`max(utilization) < 0.8` etc.) not implemented — explicitly deferred in m-E18-12. |
| **Model fitting** against parity-validated real telemetry | ❌ not built | Hard prerequisite is Telemetry Loop & Parity epic, which is not started. `ITelemetrySource` and `Optimizer` (the inner loop) exist but `FitSpec`/`FitRunner`/`POST /v1/fit` are not assembled. |
| **Chunked evaluation** for feedback simulation | ❌ not built | Explicitly deferred in spec ("only after the stateful execution seam exists"). |
| FlowTime.Generator deleted; rg returns zero matches | ✅ done | m-E18-07 |
| Canonical run directory preserved, clear-text | ✅ done | Unchanged. |
| Canonical bundle + CanonicalBundleSource round-trip consistent | ⚠️ partial | ITelemetrySource/CanonicalBundleSource delivered (m-E18-08). Round-trip parity not validated — that is owned by the Telemetry Loop & Parity epic (not started). |
| ITelemetrySource with ≥2 implementations | ✅ done | CanonicalBundleSource + FileCsvSource (m-E18-08). |
| ITelemetrySink not introduced on speculation | ✅ done | Correctly deferred. |
| No external format code inside FlowTime.TimeMachine | ✅ done | Confirmed by `rg -i "prometheus\|opentelemetry\|otel" src/FlowTime.TimeMachine/` → zero matches. |
| POST /telemetry/captures still works after Generator deletion | ✅ done | TelemetryCaptureEndpoints.cs wired to TimeMachine; endpoint exists and is tested. |

---

## Unbuilt scope from spec and v2 plan

### 1. m-E18-03 — Rust-native sweep (v2 plan)

The v2 milestone plan included `m-E18-03: Parameter Sweep` as a Rust-native `flowtime-engine sweep` batch mode: N evaluations via compile-once/eval-many without any .NET layer. This was never built. Instead, m-E18-09 built a .NET sweep that calls `flowtime-engine eval` as a fresh subprocess per point.

**Impact:** For small sweeps (≤ 20 points) the overhead is acceptable. For large sweeps (100+ points) each spawn is ~100–500ms of compile overhead. The `IModelEvaluator` interface makes a future `SessionModelEvaluator` a drop-in replacement without changing `SweepRunner`, `SensitivityRunner`, `GoalSeeker`, or `Optimizer`.

**Remaining work:** Implement `SessionModelEvaluator : IModelEvaluator` that connects to the Rust engine session protocol (m-E18-02), compiles once, and calls `eval` with parameter overrides. No analysis class changes required.

### 2. Session-based evaluator (.NET → Rust session bridge)

The Rust engine session (m-E18-02) is built and working. The .NET analysis layer does not use it — `RustModelEvaluator` calls `flowtime-engine eval` (stateless, one compile per point) rather than `flowtime-engine session` (stateful, compile once). The compile-once/eval-many performance benefit of m-E18-01 and m-E18-02 is therefore not yet accessible from the .NET analysis modes.

**Remaining work:** `SessionModelEvaluator : IModelEvaluator` — wraps a persistent engine session process, sends `compile` once, sends `eval` with YAML patch per evaluation point.

### 3. Model fitting (m-E18-04 / m-E18-05 in v2)

Requires: (a) `FitSpec` / `FitRunner` / `POST /v1/fit` that wires `ITelemetrySource` + `Optimizer` with residual as objective, and (b) Telemetry Loop & Parity epic as hard prerequisite for validated results.

**Remaining work:** Blocked on Telemetry Loop & Parity. The infrastructure (`ITelemetrySource`, `Optimizer`) exists but the composition does not.

### 4. Optimization constraints

The spec describes `--constraint "max(node.queue.utilization) < 0.8"`. `OptimizeSpec` has no constraint field. Explicitly deferred in m-E18-12.

**Remaining work:** `ConstraintSpec` + constraint evaluation inside the Nelder-Mead loop (penalty method or projection). Future milestone.

### 5. Monte Carlo

Mode 5 in the spec. Not in v2 milestone plan but in original epic scope. Requires sampling parameters from distributions, evaluating N times, characterizing output distribution (mean, variance, percentiles). Would compose `IModelEvaluator` with a sampling layer.

**Remaining work:** Not started. Lower priority than fitting.

### 6. Chunked evaluation

Mode 6 in the spec. Explicitly deferred: "only after a dedicated streaming/stateful execution seam exists." The Rust engine session (m-E18-02) is the seam, but the chunk-step protocol on top of it is not designed.

**Remaining work:** Deferred. Needs a stateful chunk-step session command in the Rust engine.

### 7. FlowTime.Pipeline embeddable SDK project

The spec architecture shows a new `FlowTime.Pipeline` project as a thin wrapper over the Time Machine exposing `Sweep`, `Optimize`, `Fit`, `Sensitivity`, `MonteCarlo`, `ChunkedEvaluate` as a clean programmatic API. Not built. The current surface is `FlowTime.TimeMachine.Sweep.*` directly, which works but is not the clean SDK layer the spec describes.

**Remaining work:** Depends on whether the analysis modes stabilize first. Low priority until Fit is delivered.

### 8. FlowTime.Telemetry.* adapter projects

Prometheus, OpenTelemetry, BPI event log `ITelemetrySource` implementations. Not built.

These are **direct-source bypasses** of the E-15 Gold Builder pipeline: they implement `ITelemetrySource` directly against a live source (e.g., PromQL queries, OTEL collector, BPI event log) without writing a canonical bundle to disk. They are alternative entry points, not part of E-15 scope. E-15 provides the general batch path (raw → Gold → `CanonicalBundleSource` → `ITelemetrySource`); adapters are narrower shortcuts for clients already on specific telemetry stacks. Either path feeds the same `ITelemetrySource` interface, so Fit/optimization code is source-agnostic.

**Remaining work:** Deferred. Build when a concrete client need surfaces (e.g., a Prometheus-native customer). Until then, E-15 Gold Builder covers the general case.

### 9. .NET Time Machine CLI

The spec success criterion: `cat model.yaml | flowtime evaluate --params '{"parallelism":4}' | jq ...`. `FlowTime.Cli` has no Time Machine commands. The Rust CLI (`flowtime-engine eval/validate/plan`) works at a lower level. No `dotnet flowtime validate/sweep/optimize` commands exist.

**Remaining work:** Add Time Machine commands to `FlowTime.Cli` that call `TimeMachineValidator`, `SweepRunner`, etc. with JSON I/O. Relatively mechanical once the analysis modes are stable.

---

## What was added that is NOT in the spec

**m-E18-11 Goal Seeking** — bisection-based 1D parameter-to-target-metric search. Not in original spec or v2 plan. Added as a natural intermediate capability between sensitivity (which tells you gradient) and optimization (which searches N-D space). Correctly fills a gap in the analysis ladder.

---

## Verdict

E-18 is **in-progress**. The analysis layer is substantially complete: sweep, sensitivity, goal-seek, and optimization are all built, tested to 100% branch coverage, and exposed as API endpoints. The Rust foundation (parameterized evaluation + session protocol) is solid.

The remaining work splits into three buckets:

**Blocked on prerequisites:**
- Model fitting — blocked on Telemetry Loop & Parity

**Buildable now (independent work):**
- Session-based evaluator (`SessionModelEvaluator`) — unlocks compile-once performance for all analysis modes
- .NET Time Machine CLI commands
- Optimization constraints

**Explicitly deferred by spec:**
- Chunked evaluation
- Monte Carlo
- FlowTime.Pipeline SDK project
- FlowTime.Telemetry.* adapters (direct-source bypasses of E-15's Gold Builder path)

---

## Recommended next milestones

| Priority | Milestone | Unblocked? |
|----------|-----------|------------|
| High | `SessionModelEvaluator` — session-based evaluator bridging m-E18-02 to the .NET analysis layer | Yes |
| High | Model fitting (`FitSpec`/`FitRunner`/`POST /v1/fit`) | No — blocked on Telemetry Loop & Parity |
| Medium | .NET CLI commands for validate/sweep/sensitivity/goal-seek/optimize | Yes |
| Medium | Optimization constraints (penalty method) | Yes |
| Low | Monte Carlo | Yes |
| Deferred | Chunked evaluation | No — needs stateful session design |
| Deferred | FlowTime.Pipeline SDK | After fitting stabilizes |
