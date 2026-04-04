# Epic: Headless Pipeline and Optimization

**ID:** E-18
**Status:** future

## Goal

Make FlowTime usable as a pure callable function — embeddable in pipelines, optimization loops, model discovery workflows, and digital twin architectures. FlowTime becomes a headless evaluation engine that scripts and systems can drive programmatically.

## Context

FlowTime's engine is deterministic: given a model, parameters, and input series, it produces the same output every time. After E-16 purifies the engine into a compiled, typed, pure evaluation surface, treating it as a callable function becomes natural:

```
f(model, parameters, inputs) → outputs
```

This is the same relationship a circuit simulator (SPICE) has with its netlist: compile once, evaluate many times, vary parameters programmatically. SPICE built an entire ecosystem of analysis modes on this foundation — parameter sweeps, Monte Carlo, optimization, model fitting. FlowTime can do the same for queueing networks.

This epic owns the shared runtime parameter foundation used by both the programmable/headless layer and the interactive UI layer. The parameter model, override surface, and reevaluation API should be built once here and consumed by E-17, not implemented twice.

## The core insight

Every advanced use case is a composition of "call the evaluation function with different inputs":

| Use case | Loop shape |
|----------|-----------|
| **Parameter sweep** | Evaluate N times with different parameter values, compare outputs |
| **Optimization** | Vary parameters to minimize an objective function over FlowTime outputs |
| **Sensitivity analysis** | Perturb each parameter, measure output change (numerical gradient) |
| **Model discovery** | Fit model parameters to match observed telemetry (inverse modeling) |
| **Monte Carlo** | Sample parameters from distributions, evaluate N times, characterize output distribution |
| **Digital twin** | Continuously calibrate model from production telemetry, use for prediction and what-if |
| **Feedback simulation** | Evaluate chunk by chunk, let a controller (autoscaler, circuit breaker) adjust parameters between chunks |

## Scope

### In Scope

- **Headless CLI mode** — FlowTime as a pipeline-friendly command: model + params in, results out (JSON/CSV)
- **Shared runtime parameter foundation** — compiled parameter identities, override points, reevaluation API, and optional enrichment contract for template-authored parameter metadata reused by E-17
- **Iteration protocol** — keep compiled graph alive, accept new parameter sets per iteration without recompile
- **Parameter sweep** — evaluate over a grid of parameter values
- **Optimization** — find parameter values that minimize/maximize an objective subject to constraints
- **Model fitting** — given observed telemetry, calibrate model parameters to match (system identification)
- **Sensitivity analysis** — compute ∂output/∂parameter numerically
- **Pipeline SDK** — FlowTime.Core as an embeddable library with clean evaluation API
- **Chunked evaluation** — evaluate bins in chunks for feedback simulation (autoscaler, circuit breaker scenarios)

Real-telemetry fitting/optimization is part of this epic, but it is not the first cut: when E-18 consumes real telemetry rather than synthetic or fixture data, that branch should sit downstream of E-15's first dataset path and Telemetry Loop & Parity so calibration is grounded in measured replay drift.

### Out of Scope

- UI for interactive parameter exploration (E-17)
- WebSocket/SignalR push channel (E-17)
- New analytical primitives
- Rewriting the DAG evaluator foundation
- Chunked/stateful execution semantics in the first headless cut — treat them as a later layer once a dedicated streaming/stateful seam exists

## Execution Layers

To minimize risk, execute this epic in three layers:

1. **Foundation layer:** shared runtime parameter foundation + evaluation SDK + headless CLI / sidecar.
2. **Analysis layer:** parameter sweep, sensitivity, optimization, and fitting on top of the foundation.
3. **Stateful layer:** chunked evaluation and richer telemetry adapters only after a dedicated streaming/stateful execution seam exists.

## Analysis Modes (SPICE-inspired)

### Mode 1: Sweep

```bash
flowtime evaluate model.yaml --sweep "parallelism=1,2,4,8,16" --output sweep-results.json
```

Embarrassingly parallel. Produces a table of (parameter_value → key_metrics).

### Mode 2: Optimize

```bash
flowtime optimize model.yaml \
  --objective "min(avg(node.queue.queueTimeMs))" \
  --constraint "max(node.queue.utilization) < 0.8" \
  --vary "parallelism=1..32" "serviceRate=50..500" \
  --output optimal.json
```

Gradient-free optimization (Nelder-Mead, Bayesian) over FlowTime as the evaluation function.

### Mode 3: Fit

```bash
flowtime fit model.yaml --observed production-metrics.json \
  --fit-params "serviceRate,routingWeight" \
  --output calibrated-model.yaml
```

System identification: given real telemetry, find the parameter values that make the model match reality. Uses least-squares or similar fitting over the residual between predicted and observed series.

### Mode 4: Sensitivity

```bash
flowtime sensitivity model.yaml \
  --params "parallelism,serviceRate,arrivalRate" \
  --metric "avg(queueTimeMs)" \
  --perturbation 0.05 \
  --output sensitivity.json
```

Numerical gradient: perturb each parameter by ±5%, measure output change. Answers "which parameter has the most impact on latency?"

### Mode 5: Monte Carlo

```bash
flowtime montecarlo model.yaml \
  --distribution "serviceRate=normal(100,15)" \
  --distribution "arrivalRate=normal(80,10)" \
  --samples 1000 \
  --output distribution.json
```

Stochastic parameter variation. Answers "given uncertainty in our estimates, what's the 95th percentile of queue time?"

### Mode 6: Feedback / chunked evaluation

```bash
flowtime simulate model.yaml --chunked --chunk-size 60 \
  --controller autoscaler.py \
  --output trace.json
```

Evaluate 60 bins, pass state to controller script, controller adjusts parameters, evaluate next 60 bins. Simulates closed-loop control.

## Telemetry as input and output

A critical capability: FlowTime should consume and produce telemetry in standard formats.

**Input:** Real production metrics (Prometheus, OpenTelemetry, CSV) as arrival/served/queue-depth series. The model's `file:` references already point to CSV data. Extending this to accept live metric queries or standard telemetry formats makes the pipeline real.

**Output:** Predicted series in the same formats. A calibrated FlowTime model's output can be compared directly against production dashboards.

**Roundtrip:** Observe → parity-check replay → calibrate → predict → compare → alert on divergence. This is the digital twin loop.

## Architecture

```
FlowTime.Core (E-16: pure compiled engine)
├── Compile(model) → CompiledGraph
├── Evaluate(graph, grid) → EvaluatedState
├── Analyze(state) → AnalyticalFacts
│
│  E-17/E-18 shared foundation:
├── IdentifyParameters(graph) → Parameter[]
├── Reevaluate(graph, param_overrides) → EvaluatedState
│
│  E-18 specific:
├── EvaluateChunk(graph, state_at_t, bins[t..t+n]) → state_at_t+n
└── ComputeObjective(state, objective_expr) → double

FlowTime.Headless (NEW)
├── CLI with pipeline-friendly I/O
├── Iteration protocol (keep graph alive)
└── Analysis mode dispatch (sweep, optimize, fit, sensitivity, montecarlo, chunked)

FlowTime.Pipeline (NEW — embeddable SDK)
├── Sweep(graph, param_grid) → results[]
├── Optimize(graph, objective, constraints, ranges) → optimal
├── Fit(graph, observed, fit_params) → calibrated
├── Sensitivity(graph, params, perturbation) → gradients
├── MonteCarlo(graph, distributions, N) → distribution[]
└── ChunkedEvaluate(graph, chunk_size, controller_fn) → trace
```

## Success Criteria

- [ ] FlowTime can be called as a headless CLI in a shell pipeline: `cat model.yaml | flowtime evaluate --params '{"parallelism":4}' | jq '.nodes.queue.derived.queueTimeMs'`
- [ ] Parameter sweeps produce correct comparative results without recompilation per evaluation
- [ ] An optimization loop can find parameter values that satisfy an objective + constraints
- [ ] Model fitting against parity-validated real telemetry produces a calibrated model that predicts within tolerance
- [ ] Chunked evaluation enables feedback simulation with external controller logic
- [ ] All evaluation modes use the pure Core engine — no adapter-side analytical computation

## Milestones (sketch)

| ID | Title | Summary |
|----|-------|---------|
| m-E18-01 | Shared Runtime Parameter Foundation & Evaluation SDK | One runtime parameter model for E-18 and E-17: compile, identify parameters, enrich metadata, re-evaluate with overrides |
| m-E18-02 | Headless CLI / Sidecar | Pipeline-friendly CLI with JSON I/O, iteration protocol, parameter override, sidecar-first integration path |
| m-E18-03 | Parameter Sweep & Sensitivity | Sweep mode, sensitivity analysis, comparative output |
| m-E18-04 | Optimization & Fitting | Objective-based optimization plus model fitting against parity-validated observed data |
| m-E18-05 | Chunked Evaluation | Bin-chunk evaluation for feedback simulation with external controllers, only after the stateful execution seam exists |
| m-E18-06 | Telemetry I/O | Standard telemetry format ingestion and emission (Prometheus, OTEL, CSV) |

## Risks & Open Questions

| Risk / Question | Impact | Mitigation |
|----------------|--------|------------|
| Optimization solver choice (Nelder-Mead vs Bayesian vs gradient-free) | Medium | Start with Nelder-Mead (simple, derivative-free), add Bayesian later |
| Model fitting convergence for complex topologies | High | Start with small models, add diagnostics for fit quality |
| Chunked evaluation requires a real stateful execution seam, not just the current `IStatefulNode` stubs | High | Defer chunked evaluation to the epic's stateful layer; do not block foundation or analysis layers on it |
| Objective expression language design | Medium | Start with simple predefined metrics, add expression support later |
| Telemetry format proliferation | Low | Start with CSV (already supported) and JSON, add OTEL later |

## Dependencies

- E-16 (Formula-First Core Purification) — must complete first
- E-17 (Interactive What-If Mode) consumes the shared runtime parameter foundation built here; it should not duplicate the runtime parameter model or reevaluation API
- Telemetry Loop & Parity for any optimization/fitting path that uses ingested real telemetry rather than synthetic or fixture data

## Analogies

FlowTime's relationship to these analysis modes is the same as SPICE's relationship to circuit analysis:
- The engine is the forward model (netlist → simulation)
- Every analysis mode is a different way of calling the forward model
- The engine doesn't need to know about optimization — it just evaluates purely
- The analysis framework wraps the engine with different calling patterns

## References

- SPICE analysis modes (.DC, .AC, .TRAN, .STEP, .MC, .OPTIM) as architectural precedent
- Control theory system identification (Ljung, "System Identification: Theory for the User")
- [work/epics/E-16-formula-first-core-purification/reference/formula-first-engine-refactor-plan.md](../E-16-formula-first-core-purification/reference/formula-first-engine-refactor-plan.md)
- [docs/research/flowtime-headless-integration.md](../../../docs/research/flowtime-headless-integration.md)
