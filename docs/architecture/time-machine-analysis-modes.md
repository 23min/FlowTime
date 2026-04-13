# Time Machine Analysis Modes

**Status:** Current — reflects m-E18-09/10/11 implementation
**Date:** 2026-04-13
**Context:** E-18 Time Machine. The sweep/sensitivity/goal-seek layer built on top of `RustEngineRunner`.

## Overview

The Time Machine's analysis modes answer SPICE-style programmatic questions about a model:

| Mode | Question answered | Milestone |
|------|------------------|-----------|
| **Sweep** | How do outputs change as I vary one parameter? | m-E18-09 |
| **Sensitivity** | Which parameter has the most impact on this metric? | m-E18-10 |
| **Goal Seek** | What parameter value achieves this target metric? | m-E18-11 |
| **Optimize** | What parameter values minimize/maximize this objective? | future |
| **Fit** | What parameter values best match observed telemetry? | future (needs Telemetry Loop & Parity) |
| **Monte Carlo** | Given uncertainty in inputs, what is the output distribution? | future |

Each mode is a composition of the one below it. Goal seek uses sweep. Sensitivity uses sweep. Optimization uses sweep. They all share one evaluation contract.

## Architecture

```
┌───────────────────────────────────────────────────────┐
│  Analysis Layer  (FlowTime.TimeMachine.Sweep)         │
│                                                       │
│  GoalSeeker          SensitivityRunner                │
│       └──────────────────┘                            │
│              SweepRunner                              │
│                  │                                    │
│           IModelEvaluator                             │
│                  │                                    │
│         RustModelEvaluator                            │
└───────────────────────────────────────────────────────┘
                   │
┌───────────────────────────────────────────────────────┐
│  YAML Mutation Layer  (FlowTime.TimeMachine.Sweep)    │
│                                                       │
│  ConstNodePatcher       ConstNodeReader               │
│  (write a value)        (read a value)                │
└───────────────────────────────────────────────────────┘
                   │
┌───────────────────────────────────────────────────────┐
│  Execution Layer  (FlowTime.Core.Execution)           │
│                                                       │
│  RustEngineRunner                                     │
│  flowtime-engine eval <model.yaml> --output <dir>     │
└───────────────────────────────────────────────────────┘
```

### Key design decisions

**`IModelEvaluator` as the seam.** All analysis modes depend on `IModelEvaluator`, not on `RustEngineRunner` directly. This makes every analysis class testable with a `FakeEvaluator` (inline private class that returns canned series). The concrete `RustModelEvaluator` is registered only when `RustEngine:Enabled=true`.

**Composition, not inheritance.** `SensitivityRunner` and `GoalSeeker` take a `SweepRunner` in their constructor. `SweepRunner` owns the `IModelEvaluator`. No duplication of evaluation logic.

**`ConstNodePatcher` / `ConstNodeReader` for YAML mutation.** Each evaluation point is a fresh subprocess call with a YAML copy where the target const node's values array is replaced. YamlDotNet representation model gives reliable DOM manipulation without regex fragility. Both classes operate on named const nodes only — expression nodes are not patchable.

**One subprocess per evaluation point.** The current implementation spawns `flowtime-engine eval` once per point (compile + eval each time). This trades compile-once efficiency for implementation simplicity. A future session-based evaluator (compile once, eval N times via the `flowtime-engine session` protocol) can be substituted by implementing `IModelEvaluator` without changing any analysis class.

## API Surface

All three modes are exposed as minimal API endpoints registered on the `/v1` group.

### `POST /v1/sweep`

Evaluate a model at N values of one const-node parameter.

**Request:**
```json
{
  "yaml": "...",
  "paramId": "arrivals",
  "values": [10, 20, 30, 40, 50],
  "captureSeriesIds": ["arrivals", "queue.utilization"]
}
```

**Response (200):**
```json
{
  "paramId": "arrivals",
  "points": [
    { "paramValue": 10.0, "series": { "arrivals": [...], "queue.utilization": [...] } },
    { "paramValue": 20.0, "series": { ... } }
  ]
}
```

`captureSeriesIds` is optional — omit to receive all series. All series dictionaries use case-insensitive keys.

---

### `POST /v1/sensitivity`

Compute ∂(mean metric)/∂param for each named parameter using central difference.

**Request:**
```json
{
  "yaml": "...",
  "paramIds": ["arrivals", "capacity", "parallelism"],
  "metricSeriesId": "queue.queueTimeMs",
  "perturbation": 0.05
}
```

`perturbation` is optional (default 0.05 = ±5%). Must be in (0, 1) exclusive.

**Response (200):**
```json
{
  "metricSeriesId": "queue.queueTimeMs",
  "points": [
    { "paramId": "arrivals",    "baseValue": 100.0, "gradient": 2.34 },
    { "paramId": "capacity",    "baseValue":  50.0, "gradient": -1.12 },
    { "paramId": "parallelism", "baseValue":   4.0, "gradient":  0.08 }
  ]
}
```

Points are sorted by `|gradient|` descending (most impactful first). Parameters not found in the model (non-const or absent nodes) are omitted. Zero-base parameters produce `gradient: 0.0`.

---

### `POST /v1/goal-seek`

Find the const-node parameter value that drives a metric to a target, via bisection.

**Request:**
```json
{
  "yaml": "...",
  "paramId": "arrivals",
  "metricSeriesId": "queue.utilization",
  "target": 0.8,
  "searchLo": 10.0,
  "searchHi": 200.0,
  "tolerance": 1e-6,
  "maxIterations": 50
}
```

`tolerance` and `maxIterations` are optional (defaults: 1e-6 and 50).

**Response (200):**
```json
{
  "paramValue": 147.3,
  "achievedMetricMean": 0.800001,
  "converged": true,
  "iterations": 23
}
```

`converged: false` when the target is not bracketed by the metric values at `searchLo`/`searchHi`, or when `maxIterations` is exhausted. In both cases `paramValue` is the best approximation found.

---

## Error responses (all modes)

| Status | Condition |
|--------|-----------|
| 400 | Missing or empty required fields |
| 503 | `RustEngine:Enabled=false` — engine not available |

## YAML Mutation: how it works

`ConstNodePatcher.Patch(yaml, nodeId, value)` uses YamlDotNet's representation model:

1. Parse YAML into a `YamlStream` (document object model)
2. Walk the `nodes` sequence to find the entry with `id == nodeId` AND `kind == "const"`
3. Replace its `values` sequence with `[value, value, ..., value]` (same bin count)
4. Re-serialize to YAML string

Returns the original YAML unchanged if the node is not found or is not a const node.

`ConstNodeReader.ReadValue(yaml, nodeId)` reads the first-bin value of a const node, returning `null` for unknown/non-const nodes. Used by `SensitivityRunner` and `GoalSeeker` to determine the base parameter value before perturbation.

## Future modes

**Optimization** — Nelder-Mead simplex over `SweepRunner`. Minimizes or maximizes a scalar objective (mean of a metric series) subject to parameter bounds. No telemetry dependency for pure-model optimization.

**Model fitting** — Minimizes residual between model output and observed telemetry. Hard dependency on Telemetry Loop & Parity epic (needed for measured drift bounds and replay consistency). Consumes `ITelemetrySource` to load observed data.

**Monte Carlo** — Samples parameters from distributions, evaluates N times, characterizes output distribution (mean, variance, percentiles). Pure composition of `SweepRunner` with a sampling layer.

## Related documents

- `docs/architecture/headless-engine-architecture.md` — engine session protocol, streaming design
- `work/epics/E-18-headless-pipeline-and-optimization/spec.md` — full epic spec
- `work/epics/E-18-headless-pipeline-and-optimization/m-E18-09-parameter-sweep.md`
- `work/epics/E-18-headless-pipeline-and-optimization/m-E18-10-sensitivity-analysis.md`
- `work/epics/E-18-headless-pipeline-and-optimization/m-E18-11-goal-seeking.md`
