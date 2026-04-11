# Epic: Interactive What-If Mode

**ID:** E-17
**Status:** future

## Goal

Enable live, interactive recalculation in FlowTime — change a parameter and see results update instantly across the entire model, like a spreadsheet.

## Context

After E-16, the engine will be a pure compiled evaluation surface: typed references, compiled descriptors, pure evaluators. The evaluation itself is already fast enough for live interaction. What's missing is one shared runtime parameter foundation: parameter identity and override points in compiled graphs, reevaluation APIs, and optional enrichment from authored template parameter metadata. Sessions, push delivery, and UI controls build on that foundation.

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
- Parameter metadata enrichment from authored template parameters when available (titles, ranges, defaults, descriptions)

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
- Parameter identity, override points, and deterministic reevaluation must be derivable from the compiled graph. Human-facing labels, ranges, defaults, and descriptions may be enriched from template parameter metadata when available.

## Success Criteria

- [ ] User can change a model parameter via UI control and see all metrics, charts, and heatmaps update live
- [ ] Parameter changes do not trigger recompilation — only re-evaluation
- [ ] Compiled graph stays alive in a server-side session; no re-parse/re-compile per interaction
- [ ] Analytical results (cycle time, flow efficiency, warnings) update through the pure Core evaluator
- [ ] UI parameter controls are generated from model metadata, not hand-coded per template

## Milestones

| ID | Title | Status | Summary |
|----|-------|--------|---------|
| m-E17-01 | WebSocket Engine Bridge | complete | .NET WebSocket proxy over persistent Rust `flowtime-engine session` subprocess; MessagePack compile/eval/get_series round-trip |
| m-E17-02 | Svelte Parameter Panel | complete | SvelteKit `/what-if` page with live-bound sliders, example model picker, series mini-charts, latency badge |
| m-E17-03 | Live Topology + Charts | complete | Dag-map topology graph with heatmap, per-series charts with hover tooltips, layout stability across tweaks |
| m-E17-04 | Warnings Surface | complete | Engine warnings flow through session protocol into banner, details panel, and topology node badges; capacity-constrained example model drives the demo loop |
| m-E17-05 | Edge Heatmap | in-progress | Color topology edges by their throughput series mean; wires up the already-present `edgeMetrics` prop in dag-map-view |
| m-E17-06 | Time Scrubber | pending | Bin-position slider switches heatmap (nodes + edges) from mean to per-bin value; vertical crosshair on all charts |

## Dependencies

- E-16 (Formula-First Core Purification) — must complete first
- Shared runtime parameter foundation — owned once and shared with E-18; E-17 consumes it rather than defining a second runtime parameter model

## References

- [work/epics/E-16-formula-first-core-purification/reference/formula-first-engine-refactor-plan.md](../E-16-formula-first-core-purification/reference/formula-first-engine-refactor-plan.md)
- SPICE interactive analysis modes as architectural precedent
