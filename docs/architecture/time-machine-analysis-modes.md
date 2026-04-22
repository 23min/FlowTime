# Time Machine Analysis Modes

**Status:** Current — reflects m-E18-09/10/11/12 implementation
**Date:** 2026-04-13
**Context:** E-18 Time Machine. The sweep/sensitivity/goal-seek layer built on top of `RustEngineRunner`.

## Overview

The Time Machine's analysis modes answer SPICE-style programmatic questions about a model:

| Mode | Question answered | Milestone |
|------|------------------|-----------|
| **Sweep** | How do outputs change as I vary one parameter? | m-E18-09 |
| **Sensitivity** | Which parameter has the most impact on this metric? | m-E18-10 |
| **Goal Seek** | What parameter value achieves this target metric? | m-E18-11 |
| **Optimize** | What parameter values minimize/maximize this objective? | m-E18-12 |
| **Fit** | What parameter values best match observed telemetry? | future (needs Telemetry Loop & Parity) |
| **Monte Carlo** | Given uncertainty in inputs, what is the output distribution? | future |

Each mode is a composition of the one below it. Goal seek uses sweep. Sensitivity uses sweep. Optimizer uses the evaluator directly for multi-parameter patching. They all share one evaluation contract.

## Architecture

```
┌───────────────────────────────────────────────────────┐
│  Analysis Layer  (FlowTime.TimeMachine.Sweep)         │
│                                                       │
│  Optimizer   GoalSeeker    SensitivityRunner          │
│      │            └──────────────┘                    │
│      │                SweepRunner                     │
│      └──────────────────┘                             │
│              IModelEvaluator                          │
│                  │                                    │
│    SessionModelEvaluator  or  RustModelEvaluator      │
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
│  Execution Layer                                      │
│                                                       │
│  SessionModelEvaluator → flowtime-engine session      │
│    (persistent subprocess, MessagePack over stdio,    │
│     compile once, eval many)                          │
│                                                       │
│  RustModelEvaluator → RustEngineRunner                │
│    (flowtime-engine eval <model> --output <dir>,      │
│     fresh subprocess per eval, reads artifacts)       │
└───────────────────────────────────────────────────────┘
```

### Key design decisions

**`IModelEvaluator` as the seam.** All analysis modes depend on `IModelEvaluator`, not on a concrete runner. This makes every analysis class testable with a `FakeEvaluator` (inline private class that returns canned series). The two production implementations (session / per-eval) are registered behind the same seam when `RustEngine:Enabled=true`.

**Two production evaluators, one config switch.** `RustEngine:UseSession` (default `true`) selects the implementation:
- **`SessionModelEvaluator` (default).** Uses the `flowtime-engine session` protocol (MessagePack over stdin/stdout, see `engine/cli/src/session.rs`). Spawns one subprocess per HTTP request (Scoped lifetime), compiles the model once on the first call, then sends parameter overrides for every subsequent call. Designed for sweeps/optimization/fitting with many evaluations per request.
- **`RustModelEvaluator` (fallback).** Set `RustEngine:UseSession=false`. Spawns `flowtime-engine eval` as a fresh subprocess per evaluation point — each point pays full compile overhead but gets complete process isolation. Fits Azure Functions scenarios where each invocation is short-lived (see ROADMAP.md "Cloud Deployment").

Both paths return the same numeric values (they drive the same engine). Key shapes differ — the session path returns bare column-map IDs (`served`), the per-eval path returns artifact IDs (`served@SERVED@DEFAULT`). See `work/gaps.md` for the divergence note.

**Composition, not inheritance.** `SensitivityRunner` and `GoalSeeker` take a `SweepRunner` in their constructor. `Optimizer` takes `IModelEvaluator` directly — it patches multiple parameters simultaneously per evaluation, which `SweepRunner`'s 1D interface cannot express. `SweepRunner` owns the `IModelEvaluator`. No duplication of evaluation logic.

**`ConstNodePatcher` / `ConstNodeReader` for YAML mutation.** `ConstNodePatcher` replaces the target const node's values array; `ConstNodeReader` reads the current value. YamlDotNet representation model gives reliable DOM manipulation without regex fragility. Both operate on named const nodes only — expression nodes are not patchable. `SessionModelEvaluator` additionally uses `ConstNodeReader` to extract override values from patched YAMLs on every call after the initial compile.

**Scoped DI lifetime.** `IModelEvaluator`, `SweepRunner`, `SensitivityRunner`, `GoalSeeker`, and `Optimizer` are all registered as `Scoped`. This ties the session subprocess lifetime to the HTTP request: each analysis run gets its own session, disposed automatically when the request ends.

## API Surface

All four modes are exposed as minimal API endpoints registered on the `/v1` group.

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
  "iterations": 23,
  "trace": [
    { "iteration": 0, "paramValue": 10.0,  "metricMean": 0.42, "searchLo": 10.0,  "searchHi": 200.0 },
    { "iteration": 0, "paramValue": 200.0, "metricMean": 0.95, "searchLo": 10.0,  "searchHi": 200.0 },
    { "iteration": 1, "paramValue": 105.0, "metricMean": 0.71, "searchLo": 105.0, "searchHi": 200.0 },
    { "iteration": 2, "paramValue": 152.5, "metricMean": 0.81, "searchLo": 105.0, "searchHi": 152.5 }
  ]
}
```

`converged: false` when the target is not bracketed by the metric values at `searchLo`/`searchHi`, or when `maxIterations` is exhausted. In both cases `paramValue` is the best approximation found.

`trace` (added per D-2026-04-21-034) records the full bisection history: two `iteration: 0` entries for the initial boundary evaluations (`searchLo`, then `searchHi`), followed by one entry per bisection step with `iteration: 1..N`. Each bisection entry's `searchLo` / `searchHi` are the **post-step** bracket — the narrowed range going into the next iteration. `metricMean` is the unsigned mean at that `paramValue`. When `converged: false, iterations: 0` (target not bracketed), `trace` contains only the two boundary entries.

---

### `POST /v1/optimize`

Find the parameter values that minimize or maximize a metric mean using Nelder-Mead simplex.

**Request:**
```json
{
  "yaml": "...",
  "paramIds": ["arrivals", "capacity"],
  "metricSeriesId": "queue.utilization",
  "objective": "minimize",
  "searchRanges": {
    "arrivals":  { "lo": 10.0, "hi": 200.0 },
    "capacity":  { "lo": 1.0,  "hi": 32.0  }
  },
  "tolerance": 1e-4,
  "maxIterations": 200
}
```

`objective` is `"minimize"` or `"maximize"` (case-insensitive). `tolerance` and `maxIterations` are optional (defaults: 1e-4 and 200).

**Response (200):**
```json
{
  "paramValues": { "arrivals": 147.2, "capacity": 8.0 },
  "achievedMetricMean": 0.751,
  "converged": true,
  "iterations": 42,
  "trace": [
    { "iteration": 0, "paramValues": { "arrivals": 105.0, "capacity": 16.5 }, "metricMean": 0.902 },
    { "iteration": 1, "paramValues": { "arrivals": 128.4, "capacity": 12.1 }, "metricMean": 0.834 },
    { "iteration": 2, "paramValues": { "arrivals": 141.7, "capacity":  9.8 }, "metricMean": 0.790 }
  ]
}
```

`converged: false` when `maxIterations` is exhausted before the simplex f-value spread falls below `tolerance`. `paramValues` is always the best point found.

`trace` (added per D-2026-04-21-034) records the simplex's best vertex per iteration: one pre-loop entry as `iteration: 0` (post-sort, before the main loop), then one per main-loop iteration (post-sort) as `iteration: 1..N`. `paramValues` is the current best vertex (`simplex[0]`) and `metricMean` is its **unsigned** mean — the internal minimize-sign flip is reversed at record time so maximize runs still emit positive metrics. Trace length equals `iterations + 1` on every return path (pre-loop convergence → single entry; main-loop convergence or max-iterations exhausted → `iterations + 1`). The per-evaluation probe log (reflection / expansion / contraction / shrink intermediate vertices) is intentionally not exposed.

**Algorithm:** Nelder-Mead simplex (α=1, γ=2, ρ=0.5, σ=0.5). Initial simplex: midpoint of all ranges as v[0]; each subsequent vertex perturbs one dimension by +5% of its range. Convergence criterion: `|f(worst) − f(best)| < tolerance` where f is the signed metric mean (negated for maximize). All vertices are clamped to their search ranges throughout.

---

## Error responses (all modes)

| Status | Condition |
|--------|-----------|
| 400 | Missing or empty required fields |
| 503 | `RustEngine:Enabled=false` — engine not available |

## CLI Surface

All four analysis modes are also exposed through `FlowTime.Cli` as pipeable JSON-over-stdio commands. The request/response JSON is byte-compatible with the `/v1/` API bodies, so a spec file can be used interchangeably between the two surfaces.

```
flowtime validate     [<model.yaml>] [--tier schema|compile|analyse] [-o <path>]
flowtime sweep        [--spec <path>] [-o <path>] [--no-session] [--engine <path>]
flowtime sensitivity  [--spec <path>] [-o <path>] [--no-session] [--engine <path>]
flowtime goal-seek    [--spec <path>] [-o <path>] [--no-session] [--engine <path>]
flowtime optimize     [--spec <path>] [-o <path>] [--no-session] [--engine <path>]
```

All commands read from stdin by default (or `--spec <path>`/`-` for explicit stdin) and write to stdout (or `--output <path>`/`-o`). Pass `--help` after the command name for command-specific details.

**Exit codes:**

| Code | Meaning |
|------|---------|
| 0    | Success (`validate` treats valid models as 0) |
| 1    | Analysis produced an explicit failure (`validate` treats invalid models as 1 with JSON still on stdout) |
| 2    | Input error — missing required args, invalid JSON, engine binary not found |
| 3    | Engine / runtime error — session crash, protocol error, Rust-side compile error |

**Evaluator selection (analysis commands).** The CLI uses `SessionModelEvaluator` by default, same as the API. Pass `--no-session` to fall back to `RustModelEvaluator` (fresh subprocess per eval). Both paths compute identical numeric results; see note in D-2026-04-15 on key-shape differences.

**Engine binary resolution.** Precedence: `--engine <path>` flag → `FLOWTIME_RUST_BINARY` env var → `<solution-root>/engine/target/release/flowtime-engine` → `flowtime-engine` on `$PATH`.

**Pipeline example:**

```bash
cat sweep-spec.json | flowtime sweep | jq '.points[].series.served[0]'
cat opt.json | flowtime optimize | jq '.paramValues'
flowtime validate examples/hello/model.yaml --tier schema
```

The CLI is the canonical pipeline entry point for Azure Functions custom handlers, Container Apps jobs, scripted regression suites, and AI-assistant iteration — no HTTP server required. See ROADMAP.md "Cloud Deployment & Data Pipeline Integration" for the aspirational deployment shapes.

## YAML Mutation: how it works

`ConstNodePatcher.Patch(yaml, nodeId, value)` uses YamlDotNet's representation model:

1. Parse YAML into a `YamlStream` (document object model)
2. Walk the `nodes` sequence to find the entry with `id == nodeId` AND `kind == "const"`
3. Replace its `values` sequence with `[value, value, ..., value]` (same bin count)
4. Re-serialize to YAML string

Returns the original YAML unchanged if the node is not found or is not a const node.

`ConstNodeReader.ReadValue(yaml, nodeId)` reads the first-bin value of a const node, returning `null` for unknown/non-const nodes. Used by `SensitivityRunner` and `GoalSeeker` to determine the base parameter value before perturbation.

## Future modes

**Model fitting** — Minimizes residual between model output and observed telemetry. Hard dependency on Telemetry Loop & Parity epic (needed for measured drift bounds and replay consistency). Consumes `ITelemetrySource` to load observed data. *(Optimization as the inner loop is already available via `Optimizer`.)*

**Monte Carlo** — Samples parameters from distributions, evaluates N times, characterizes output distribution (mean, variance, percentiles). Pure composition of `SweepRunner` with a sampling layer.

## Related documents

- `docs/architecture/headless-engine-architecture.md` — engine session protocol, streaming design
- `work/epics/E-18-headless-pipeline-and-optimization/spec.md` — full epic spec
- `work/epics/E-18-headless-pipeline-and-optimization/m-E18-09-parameter-sweep.md`
- `work/epics/E-18-headless-pipeline-and-optimization/m-E18-10-sensitivity-analysis.md`
- `work/epics/E-18-headless-pipeline-and-optimization/m-E18-11-goal-seeking.md`
- `work/epics/E-18-headless-pipeline-and-optimization/m-E18-12-optimization.md`
