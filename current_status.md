# FlowTime Current Status — 2026-04-13

This file captures session context for handoff. It reflects the state at end of
the m-E18-12 implementation session on the `epic/E-18-time-machine` branch.

---

## What just happened (m-E18-12)

Implemented multi-parameter optimization using the Nelder-Mead simplex algorithm.
All acceptance criteria from the milestone spec are met. 100% branch coverage
confirmed, including all five non-obvious algorithm paths:

| Path | How covered |
|------|-------------|
| Pre-loop convergence (iterations=0) | Bowl1D with tiny range [49,51], tolerance=0.1 |
| Reflection → accept (normal) | Bowl1D and QuadraticEvaluator (existing tests) |
| Expansion accepted | QuadraticEvaluator at known minimum |
| Expansion rejected → accept reflection | AbsEvaluator(target=90), range [0,200] |
| Outside contraction fail → shrink (line 116) | StepEvaluator(peak=50, valley=45), iter 1 |
| Inside contraction fail → shrink (line 128) | StepEvaluator(peak=50, valley=45), iter 2 |

**Test counts (final):**
- `OptimizeSpec`: 17 unit tests (validation: missing fields, mismatched ranges, etc.)
- `Optimizer`: 12 unit tests (algorithm correctness + all branch coverage)
- `OptimizeEndpointsTests`: 10 API tests (9 × 400 validation paths + 1 × 503 when engine disabled)
- Total `FlowTime.TimeMachine.Tests`: 192 tests
- Total `FlowTime.Api.Tests`: 260 tests

**New types delivered:**
- `OptimizeSpec` — paramIds, metricSeriesId, objective, searchRanges, tolerance, maxIterations
- `OptimizeResult` — Converged, Iterations, ParamValues, AchievedMetricMean
- `OptimizeObjective` — enum: Minimize / Maximize (internally always minimizes; Maximize negates metric)
- `SearchRange` — record: Lo, Hi (with validation Lo < Hi)
- `Optimizer` — Nelder-Mead simplex; injectable IModelEvaluator; IAsyncDisposable
- `POST /v1/optimize` endpoint

---

## Nothing committed yet

All of the following are staged/untracked but NOT committed:

**m-E18-12 implementation:**
- `src/FlowTime.TimeMachine/Sweep/OptimizeSpec.cs`
- `src/FlowTime.TimeMachine/Sweep/OptimizeResult.cs`
- `src/FlowTime.TimeMachine/Sweep/OptimizeObjective.cs`
- `src/FlowTime.TimeMachine/Sweep/SearchRange.cs`
- `src/FlowTime.TimeMachine/Sweep/Optimizer.cs`
- `src/FlowTime.API/Endpoints/OptimizeEndpoints.cs`
- `src/FlowTime.API/Program.cs` (endpoint registration)
- `tests/FlowTime.TimeMachine.Tests/Sweep/OptimizeSpecTests.cs`
- `tests/FlowTime.TimeMachine.Tests/Sweep/OptimizerTests.cs`
- `tests/FlowTime.Api.Tests/OptimizeEndpointsTests.cs`
- `work/epics/E-18-headless-pipeline-and-optimization/m-E18-12-optimization.md`

**New documentation:**
- `docs/notes/ui-optimization-explorer-vision.md` — aspirational optimization UI vision
- `docs/notes/model-discovery-path.md` — three-stage path from raw data to fitted model
- `work/epics/E-18-headless-pipeline-and-optimization/e18-gap-analysis.md` — thorough gap analysis

**Status surface updates:**
- `CLAUDE.md` — m-E18-12 added as complete with accurate test counts + gap analysis reference
- `ROADMAP.md` — E-18 section rewritten with accurate delivered/gaps/deferred split
- `work/epics/epic-roadmap.md` — same updates
- `docs/architecture/time-machine-analysis-modes.md` — optimization mode added

**Separate commit (not E-18 specific):**
- `README.md` — rewrite (commit separately from milestone work)

**Proposed commit message for m-E18-12:**
```
feat(sweep): m-E18-12 Multi-parameter Optimization — Nelder-Mead simplex

- Optimizer (Nelder-Mead, N params): all branches covered — expand/contract/
  shrink, pre-loop convergence, expansion rejection, both contraction-fail
  paths to shrink
- OptimizeSpec/OptimizeResult/OptimizeObjective/SearchRange types
- POST /v1/optimize; 503 when engine not enabled
- 29 unit tests (OptimizeSpec ×17, Optimizer ×12); 10 API tests
- docs/architecture/time-machine-analysis-modes.md updated
- e18-gap-analysis.md: thorough gap analysis against spec

Co-Authored-By: Claude Sonnet 4.6 <noreply@anthropic.com>
```

---

## E-18 gap analysis summary

**Status: in-progress.** Analysis complete at
`work/epics/E-18-headless-pipeline-and-optimization/e18-gap-analysis.md`.

### Delivered (9 milestones)

| ID | Title |
|----|-------|
| m-E18-01 | Parameterized Evaluation (Rust) — ParamTable, evaluate_with_params |
| m-E18-02 | Engine Session + Streaming Protocol (Rust) — persistent process, MessagePack |
| m-E18-06 | Tiered Validation — TimeMachineValidator, POST /v1/validate |
| m-E18-07 | Generator → TimeMachine rename; FlowTime.Generator deleted |
| m-E18-08 | ITelemetrySource + CanonicalBundleSource + FileCsvSource |
| m-E18-09 | Parameter Sweep — SweepSpec/SweepRunner, POST /v1/sweep |
| m-E18-10 | Sensitivity Analysis — SensitivityRunner, POST /v1/sensitivity |
| m-E18-11 | Goal Seeking — GoalSeeker (bisection), POST /v1/goal-seek *(not in original spec — added)* |
| m-E18-12 | Optimization — Optimizer (Nelder-Mead), POST /v1/optimize |

### Remaining work

**Buildable now (unblocked):**

1. **SessionModelEvaluator** — Compile-once bridge using the m-E18-02 session protocol.
   The `IModelEvaluator` seam already exists. `RustModelEvaluator` spawns
   `flowtime-engine eval` once per evaluation point (compile + eval each time).
   `SessionModelEvaluator` would compile once on first call, then send `eval` with
   parameter overrides for each subsequent point — eliminating per-point compile overhead.
   **Design:** First call sends `compile` to session → receives ParamTable (list of param IDs).
   Subsequent calls use `ConstNodeReader` to read param values from the patched YAML, then
   send `eval {paramId: value}` overrides. No changes to SweepRunner, SensitivityRunner,
   GoalSeeker, or Optimizer. Registered in DI to replace RustModelEvaluator.
   **User confirmed: "ok (1) seems very valuable and I want to do that."**

2. **.NET Time Machine CLI** — Add validate/sweep/sensitivity/goal-seek/optimize commands
   to `FlowTime.Cli`. `cat model.yaml | flowtime validate/sweep/...` surface.
   Mechanical: call TimeMachineValidator/SweepRunner/etc. with JSON I/O.
   **User said: "I guess we need to do (3) in this milestone."**
   (Interpreted as a follow-on milestone m-E18-14 after m-E18-13 SessionModelEvaluator.)

3. **Optimization constraints** — `ConstraintSpec` + penalty method inside Nelder-Mead loop.
   Explicitly deferred from m-E18-12. No owner milestone yet.
   Documented in gap analysis. Should be added to `work/gaps.md`.

**Blocked on prerequisites:**

4. **Model fitting** — `FitSpec`/`FitRunner`/`POST /v1/fit` composing ITelemetrySource + Optimizer
   to minimize residual against observed telemetry. Infrastructure exists; endpoint not assembled.
   Hard prerequisite: Telemetry Loop & Parity epic (not started).
   **Confirmed belongs to E-18** — the computation is E-18's analysis mode; E-15 owns the data
   ingestion and Telemetry Loop & Parity owns the validation harness.

**Explicitly deferred:**

- Chunked evaluation — needs stateful chunk-step session command in Rust engine
- Monte Carlo — sampling layer on top of IModelEvaluator; not started
- FlowTime.Pipeline SDK project — after fitting stabilizes
- FlowTime.Telemetry.* adapters (Prometheus, OTEL, BPI) — E-15 territory

---

## Key architectural insight (session-based evaluator)

The m-E18-02 session protocol gives compile-once/eval-many performance, but the .NET
analysis layer doesn't use it yet. `RustModelEvaluator` calls `flowtime-engine eval`
(stateless subprocess) which compiles the model on every evaluation point. For large
sweeps (100+ points) this is ~100–500ms of compile overhead per point.

The `IModelEvaluator` interface is the injection seam:

```csharp
public interface IModelEvaluator
{
    Task<IReadOnlyDictionary<string, double[]>> EvaluateAsync(
        string modelYaml, CancellationToken cancellationToken = default);
}
```

The interface receives already-patched YAML (after `ConstNodePatcher` applies overrides).
`SessionModelEvaluator` extracts param values from the patched YAML using `ConstNodeReader`,
then sends them as `eval` overrides to the persistent session. No interface change needed.

Session lifetime: one session per `SessionModelEvaluator` instance. The evaluator implements
`IAsyncDisposable` to clean up the session process. DI registration should be scoped or
transient depending on whether sessions should be shared across requests.

---

## What's next (planned order)

1. **Commit pending m-E18-12 work** (feat commit above)
2. **Commit README.md separately** (separate concern)
3. **m-E18-13: SessionModelEvaluator** — compile-once bridge
4. **m-E18-14: .NET Time Machine CLI commands**
5. **work/gaps.md entry for optimization constraints**
6. **Late E-18: Model fitting** — after Telemetry Loop & Parity + E-15 M1

---

## New docs created (aspirational)

**`docs/notes/ui-optimization-explorer-vision.md`** — Documents the aspired optimization
UI that would complement E-17's manual what-if:
- Sweep view: chart metric vs. parameter across range
- Sensitivity panel: ranked bar chart of ∂metric/∂param at current baseline
- Goal-seek widget: enter target, pick lever, bisection returns suggested value + "apply"
- Optimization panel: multi-param search, simplex trajectory, before/after topology heatmap

Key distinction: what-if = human optimizer (you turn the dials); optimization UI = machine
optimizer (you set the goal, the engine finds the answer).

**`docs/notes/model-discovery-path.md`** — Documents the three-stage model discovery path:
1. Gold Builder (E-15, not started): raw data → canonical telemetry bundles
2. Graph Builder (E-15, not started): topology inference + human curation
3. Parameter fitting (E-18 + Telemetry Loop & Parity): minimize residual

Also documents the relationship to process mining: process mining tools (ProM, Disco,
Celonis) work with event logs and produce process graphs + aggregate statistics. FlowTime
consumes those aggregate statistics as input. They answer different questions.

---

## Hard rule established this session

**100% branch coverage before claiming milestone complete.**

Every algorithm path — especially failure-mode paths (failed contraction, shrink, early
convergence) — must be explicitly traced to a test case. "It converges in the happy path"
is not sufficient. This applies to all logic, API, and data code in TDD workflow.

---

## Current branch

`epic/E-18-time-machine`

All work is on this branch. Nothing merged to main yet for E-18.
E-17, E-20, E-10, E-16, E-19 are all merged to main and archived.
