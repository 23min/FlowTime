---
id: M-040
title: Sweep & Sensitivity Surfaces
status: done
parent: E-21
acs:
  - id: AC-1
    title: New /analysis route
    status: met
  - id: AC-2
    title: Run picker
    status: met
  - id: AC-3
    title: Tab bar
    status: met
  - id: AC-4
    title: Parameter selector
    status: met
  - id: AC-5
    title: Value range inputs
    status: met
  - id: AC-6
    title: Run sweep and render results
    status: met
  - id: AC-7
    title: Captured series filter
    status: met
  - id: AC-8
    title: Param multi-select
    status: met
  - id: AC-9
    title: Target metric picker + perturbation
    status: met
  - id: AC-10
    title: Run sensitivity and render results
    status: met
  - id: AC-11
    title: Vitest coverage for pure logic
    status: met
  - id: AC-12
    title: Playwright coverage
    status: met
---

## Goal

Deliver the first two Time Machine analysis surfaces in Svelte: parameter sweep and sensitivity analysis. These are new capabilities that Blazor never had — the headline proof that the fork delivers value.

## Context

E-18 shipped `POST /v1/sweep` and `POST /v1/sensitivity` against the Rust session engine. Until now there is no UI for either. This milestone introduces a new `/analysis` route with tabbed surfaces that let an expert:

1. Pick a run → parse its model for sweepable parameters (const nodes)
2. Run a sweep over a chosen parameter with a range of values → see result series per point
3. Run sensitivity analysis across multiple parameters against a target metric → see ranked gradients

The workbench paradigm established in M-038/02 sets the conventions: compact density, calm chrome with vivid data-viz colors, semantic tokens, `--ft-viz-*` palette, the shared `TimelineScrubber` and `Chart` components.

### API contracts (confirmed via code)

**POST /v1/sweep**
```json
Request:  { "yaml": "...", "paramId": "arrivals", "values": [10, 15, 20], "captureSeriesIds": ["served"] }
Response: { "paramId": "arrivals", "points": [ { "paramValue": 10, "series": { "served": [8, 8, 8, 8] } }, ... ] }
```

**POST /v1/sensitivity**
```json
Request:  { "yaml": "...", "paramIds": ["arrivals", "capacity"], "metricSeriesId": "queue.queueTimeMs", "perturbation": 0.05 }
Response: { "metricSeriesId": "queue.queueTimeMs", "points": [ { "paramId": "capacity", "baseValue": 50, "gradient": -2.35 }, ... ] }
```

**Parameter discovery**: clients parse the model YAML and collect nodes with `kind: const`. Their `id` is the parameter name; `values[0]` is a reasonable baseline.

## Acceptance criteria

### AC-1 — New /analysis route

**New `/analysis` route.** SvelteKit page at `ui/src/routes/analysis/+page.svelte`. Accessible from the sidebar under Tools. Compact layout consistent with the workbench paradigm.
### AC-2 — Run picker

**Run picker.** Dropdown at the top of `/analysis` to select a run. Defaults to the most recent run (same pattern as `/time-travel/topology`). Loading the model YAML for the selected run populates a param list.
### AC-3 — Tab bar

**Tab bar.** Four tabs: Sweep, Sensitivity, Goal Seek, Optimize. Only Sweep and Sensitivity are wired in this milestone; Goal Seek and Optimize render placeholder "coming in M-041" content. Tab state preserved in `localStorage` or URL query.
### AC-4 — Parameter selector

**Parameter selector.** Dropdown listing the run's const-node parameters discovered from the model YAML. Each option shows the parameter id and its baseline value. Empty state when no const nodes exist.
### AC-5 — Value range inputs

**Value range inputs.** Three inputs (from, to, step) compute the sweep values. A text input for "or custom (comma-separated)" supersedes from/to/step when non-empty. A live preview shows the final value list and count; disallow runs > 50 points with an inline warning (soft cap, still runnable).
### AC-6 — Run sweep and render results

**Run sweep and render results.** A "Run sweep" button calls `POST /v1/sweep`. While running, show a spinner. On result, render:
- A line chart: x = param value, y = selected output series aggregate (mean per point) — picked via a series selector populated from response keys.
- A per-point table: param value column + one column per captured series showing aggregate (mean) with a compact sparkline of that series across bins.
- Reasonable handling for errors (API 400/503) with inline error messages.
### AC-7 — Captured series filter

**Captured series filter.** Optional multi-select chip bar listing common series (`arrivals`, `served`, `errors`, `queue`, `utilization`, `flowLatencyMs`). Empty selection = capture all. Sends `captureSeriesIds` in the request.
### AC-8 — Param multi-select

**Param multi-select.** Chip-bar of all discovered const params. Clicking toggles selection. Defaults to all selected.
### AC-9 — Target metric picker + perturbation

**Target metric picker + perturbation.** A text input for the target series id (common ones offered as chips: `served`, `queue`, `flowLatencyMs`, `utilization`). A slider for perturbation (default 0.05, range 0.01–0.30).
### AC-10 — Run sensitivity and render results

**Run sensitivity and render results.** A "Run sensitivity" button calls `POST /v1/sensitivity`. On result, render a horizontal bar chart sorted by |gradient| descending, colored by sign (positive/negative), with numeric gradient labels and the base value shown per row. Empty/error states handled.
### AC-11 — Vitest coverage for pure logic

**Vitest coverage for pure logic.** New helpers (param discovery from YAML, sweep value range generator, aggregate/mean computation) have vitest tests with explicit branch coverage including error paths.
### AC-12 — Playwright coverage

**Playwright coverage.** New spec `svelte-analysis.spec.ts`: page loads, sweep can be configured and run against a real run, sensitivity can be configured and run. Graceful skip when infra is down.
## Technical Notes

- **YAML parsing in browser**: `js-yaml` is already a transitive dep via other libs, but we should explicitly add it. Alternative: the API could provide a `/v1/runs/{id}/params` endpoint that returns const-node ids + baselines. For this milestone, browser-parse with `js-yaml`; if it proves fragile, promote to a server endpoint in a later milestone.
- **Chart reuse**: existing `Chart.svelte` handles multi-series line data. Sweep result chart passes `{name: paramValue, values: [aggregate]}` per captured series — a new shape. Consider a dedicated `ParamSweepChart` wrapper that transposes sweep results into Chart's format.
- **Bar chart**: no current component. Build a simple horizontal-bar SVG in `SensitivityBarChart.svelte` — pure SVG with the viz palette (coral for negative, teal for positive gradients).
- **Analysis state**: small store `analysis.svelte.ts` to hold current run YAML, last sweep/sensitivity results, selected tab. Session-ephemeral.
- **Loading state**: use existing `Loader2` icon from lucide; debounce run-button clicks.

## Out of Scope

- Goal Seek + Optimize surfaces (M-041)
- Server-side parameter discovery endpoint
- Saving/re-running past analyses (history panel)
- Exporting sweep/sensitivity results
- Constraints on optimization (already out of scope globally per gaps.md)

## Dependencies

- M-038/02 (complete) — workbench paradigm, chart component, density tokens
- `POST /v1/sweep`, `POST /v1/sensitivity` — available on port 8081

## Coverage Notes

(Filled at wrap time.)
