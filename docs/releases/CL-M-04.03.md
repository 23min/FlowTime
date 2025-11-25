# CL-M-04.03 — UI Class-Aware Visualization

## Summary
- Surfaced class metadata everywhere it matters: run cards, dashboards, topology, and node inspectors now list classes, coverage status, and per-class metrics pulled from `run.json`/`series/index.json`.
- Added a reusable class selector (URL-synced, keyboard accessible) plus dimming logic so KPIs, sparklines, and topology canvases focus on the selected flows; inspector chips highlight the active selection and expose arrivals/served/errors per class.
- Introduced the legend-aligned “Flows” overlay in topology, giving each class a compact chip that shares the focus-chip visual language; removed redundant toolbar chrome so selectors no longer steal canvas space.
- Analyzer harness + regenerated sample runs (`supply-chain-multi-tier-classes`, `transportation-basic-classes`) validate end-to-end class coverage before hand-off.

## Tests
- `dotnet build`
- `dotnet test --filter TopologyClassFilterTests --nologo`
- `dotnet test --nologo`
  - Note: `FlowTime.Tests.Performance.M2PerformanceTests.Test_PMF_Mixed_Workload_Performance` still fails in the dev sandbox (known perf gap tracked for epic-level closure); all other suites green, perf/legacy example skips remain expected.

## Notes
- Telemetry/CLI exports retain existing behavior; topology CSV download button was removed from the UI (CLI continues to cover that path) to keep the canvas uncluttered.
- Analyzer harness logs captured in the milestone tracker for `run_20251125T155445Z_74e60979` and `run_20251125T155501Z_0cc3f7e6`; no `class_totals_mismatch` events observed after the final instrumentation pass.
