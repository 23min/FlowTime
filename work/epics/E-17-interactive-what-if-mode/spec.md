# Epic: Interactive What-If Mode

**ID:** E-17
**Status:** future

## Goal

Enable live, interactive recalculation in FlowTime — change a parameter and see results update instantly across the entire model, like a spreadsheet.

## Context

After E-16, the engine will be a pure compiled evaluation surface: typed references, compiled descriptors, pure evaluators. The evaluation itself is already fast enough for live interaction — a typical graph (20 nodes, 1440 bins) evaluates in microseconds. What's missing is the plumbing: parameters aren't runtime-editable without recompilation, there's no server-side session to keep the compiled graph alive, and there's no push channel to the UI.

The circuit simulator analogy: SPICE compiles a netlist once, then allows parameter sweeps and interactive probing without re-reading the schematic. FlowTime should do the same.

## Scope

### In Scope

- Parameter identification in compiled graphs — which nodes are user-editable constants?
- Runtime parameter model — change parameter values without recompilation
- Server-side session management — keep compiled graph alive across requests
- Re-evaluation API — accept parameter changes, return updated results
- Push channel (WebSocket/SignalR) for live UI updates
- UI parameter controls — sliders, numeric inputs bound to model parameters
- Analytical re-evaluation through the pure Core evaluator after parameter change

### Out of Scope

- Optimization loops and automated parameter search (E-18)
- Headless CLI and pipeline embedding (E-18)
- Model topology changes at runtime (requires recompilation — that's fine)
- New analytical primitives
- Bin-by-bin / chunked evaluation (future: feedback simulation)

## Constraints

- Evaluation must feel instantaneous (< 50ms end-to-end for parameter change → UI update)
- No recompilation for parameter value changes — only for structural model changes
- The push channel must not require polling
- Parameter metadata (name, range, units, current value) must be derivable from the compiled graph

## Success Criteria

- [ ] User can change a model parameter via UI control and see all metrics, charts, and heatmaps update live
- [ ] Parameter changes do not trigger recompilation — only re-evaluation
- [ ] Compiled graph stays alive in a server-side session; no re-parse/re-compile per interaction
- [ ] Analytical results (cycle time, flow efficiency, warnings) update through the pure Core evaluator
- [ ] UI parameter controls are generated from model metadata, not hand-coded per template

## Milestones (sketch)

| ID | Title | Summary |
|----|-------|---------|
| m-E17-01 | Runtime Parameter Model | Identify parameters in compiled graph, expose as editable inputs, re-evaluate without recompile |
| m-E17-02 | Session & Push Channel | Server-side session management, WebSocket/SignalR push for live result delivery |
| m-E17-03 | UI Parameter Controls | Auto-generated sliders/inputs from model metadata, live-bound to evaluation results |

## Dependencies

- E-16 (Formula-First Core Purification) — must complete first

## References

- [work/epics/E-16-formula-first-core-purification/reference/formula-first-engine-refactor-plan.md](../E-16-formula-first-core-purification/reference/formula-first-engine-refactor-plan.md)
- SPICE interactive analysis modes as architectural precedent
