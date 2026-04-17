# Vision: Optimization Explorer UI

**Status:** Aspirational — no implementation yet
**Context:** Complements E-17 Interactive What-If and E-18 Time Machine analysis modes

## The distinction from What-If

E-17 Interactive What-If gives you **manual exploration**: change a parameter value, the model
rerenders instantly. You are the optimizer — you decide what to try next. This is the right
tool for intuition-building and demos.

The Optimization Explorer is **machine-driven search**: you define a goal and a search space,
the engine finds the answer. You watch, guide, and interpret — but you do not turn the dials
manually. The two surfaces complement each other:

1. Use what-if to understand the landscape — is the function smooth? monotone? which parameters
   seem to matter?
2. Use the explorer to find the answer once you understand the landscape.
3. Use what-if again to verify the optimized result makes intuitive sense before acting on it.

## Aspired surfaces

Each corresponds to a Time Machine analysis mode (see `docs/architecture/time-machine-analysis-modes.md`).

### Sweep view

A chart showing how a metric varies as one parameter is swept across its range. Select a
parameter, drag a range slider, see the sweep curve rendered over the current model topology.
The slope of the curve is an intuitive sensitivity indicator — steep = this lever matters.

### Sensitivity panel

A ranked bar chart of ∂metric/∂param for all named parameters. Answers "which levers matter
most for this metric?" at a glance. The answer changes as the baseline model changes, so this
is a live read against the current parameter values — not a static report.

### Goal-seek widget

"I want queue utilization below 0.8. What arrival rate achieves this?" Enter a target value for
a metric, pick the parameter to adjust, see the bisection converge and return a suggested value.
An "apply" button updates the model in place so you can see the full consequence of the change
before committing.

### Optimization panel

Richer than goal-seek: multiple parameters, explicit search ranges, minimize or maximize. You
configure the search, the simplex runs, and the result is shown as:

- The optimal parameter values applied to the model
- The simplex trajectory overlaid on a 2D scatter (if two parameters) — where did it explore,
  where did it converge?
- Convergence status: how many iterations, did it converge or exhaust the budget?
- A before/after comparison of the topology heatmap — where did the metric improve, what
  changed elsewhere?

The goal is not just "here is the number" but "here is what the model looks like at the
optimum, and here is what changed."

## What makes this different from a generic optimization UI

FlowTime models are graphs. The interesting output is not just a scalar objective value but the
full state — node utilizations, queue depths, throughput rates, cycle times — at the optimal
parameter values. The UI can show all of that simultaneously because the engine returns the
complete model state, not just the metric. This is the advantage of owning the model: the
explorer can render the full topology at the optimum and let you inspect every edge and node.

## Relationship to the current engine

The Time Machine API (`POST /v1/optimize`, `/v1/sweep`, `/v1/sensitivity`, `/v1/goal-seek`)
already provides the computation. The UI surface is a matter of wiring these endpoints to
panels and visualizations. The parameter panel introduced in E-17 is the natural anchor point
— optimization results would feed back into the same parameter display and topology heatmap
that live what-if already uses.

## Related

- `docs/architecture/time-machine-analysis-modes.md` — current API surface
- `work/epics/E-18-headless-pipeline-and-optimization/spec.md` — Time Machine epic
- `work/epics/ui-workbench/reference/ui-paradigm.md` — broader UI architecture direction
