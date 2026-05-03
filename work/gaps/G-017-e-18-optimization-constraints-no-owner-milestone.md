---
id: G-017
title: E-18 Optimization Constraints (no owner milestone)
status: open
---

### Why this is a gap

`OptimizeSpec` has no constraint field. The E-18 spec describes constraints as:

```
--constraint "max(node.queue.utilization) < 0.8"
```

This was explicitly deferred out of M-009 because the Nelder-Mead implementation
did not need it to meet the milestone acceptance criteria, and it adds non-trivial
complexity to the simplex inner loop.

### Design notes

Implementation approach: **penalty method** inside the Nelder-Mead loop. When a
candidate point violates a constraint, add a large penalty to its objective value
so the simplex naturally avoids the infeasible region. This does not require
fundamental changes to the optimizer — add `ConstraintSpec` to `OptimizeSpec`,
evaluate constraints after each `IModelEvaluator.EvaluateAsync` call, sum
penalties into the returned metric value.

Alternative: **projection** — clamp candidate points back into the feasible region
before evaluation. Simpler to implement but loses some search flexibility near
constraint boundaries.

### Resolution path

A future E-18 milestone (no ID assigned yet) should:
1. Add `ConstraintSpec` — expression (metric series ID), comparator (`<`, `>`), threshold
2. Evaluate constraints after each evaluator call; add penalty if violated
3. Surface constraint satisfaction in `OptimizeResult` (were all constraints satisfied at optimum?)

### Status

Not scheduled. No owner milestone. Tracked here pending planning.

### Reference

- `src/FlowTime.TimeMachine/Sweep/OptimizeSpec.cs`
- `src/FlowTime.TimeMachine/Sweep/Optimizer.cs`
- `work/epics/E-18-headless-pipeline-and-optimization/m-E18-12-optimization.md` (deferred note)
- `work/epics/E-18-headless-pipeline-and-optimization/e18-gap-analysis.md` (gap #4)

---
