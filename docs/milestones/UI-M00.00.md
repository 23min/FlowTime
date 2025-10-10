# UI Milestone UI-M0 (Completed / Expanded)

## Scenarios

- See the numbers: Plot CSV outputs from a run to validate intuition and explain results.
- Example: Visualize demand vs served from the M0 example.

## Delivered Scope (vs original plan)

- Blazor WASM SPA.
- Line chart fed directly from API (CLI file fallback deferred).
- Structural graph table (node order, inputs, degrees) pulled forward.
- Micro-DAG SVG (compact visual of topology) pulled forward.
- Simulation mode toggle (deterministic stub) + persistence.
- Persisted preferences: theme, simulation flag, selected model.
- Model selector (static examples) instead of free-form editor (editor moves to UI-M1).

## Why it’s useful

- Early structural insight (table + micro-DAG) reduces debugging time.
- Deterministic simulation accelerates UI iteration without API latency.
- Persistence improves UX continuity.

## Acceptance Summary

- Run & graph calls succeed; chart + structure + micro-DAG render.
- Toggling simulation updates results instantly.
- Structural invariants test passes.

## Deferred (to UI-M1)

- In-browser model editor.
- Inline YAML validation.
- CLI CSV fallback loading.
