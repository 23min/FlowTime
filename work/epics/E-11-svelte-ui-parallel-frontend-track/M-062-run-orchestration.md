---
id: M-062
title: Run Orchestration
status: in_progress
parent: E-11
acs:
    - id: AC-1
      title: Templates render as cards in a responsive grid with title
      status: met
      tdd_phase: red
    - id: AC-2
      title: Search input filters cards by name/description in real-time
      status: met
    - id: AC-3
      title: Selecting a card fetches template detail and shows a configuration
      status: met
    - id: AC-4
      title: Can execute a model run and see results (run ID, metadata, warnings
      status: met
    - id: AC-5
      title: Preview/dry-run mode shows the execution plan without creating a run
      status: met
    - id: AC-6
      title: Loading, error, and empty states handled gracefully (skeletons, error
      status: met
    - id: AC-7
      title: No raw JSON parameter field visible by default (hidden in Advanced section)
      status: met
---

## Goal

Add card-based template selection, run configuration, and model execution to the Svelte UI at `/run`.

## Context

M1-M4 delivered the SvelteKit scaffold, API clients, topology rendering with dag-map, and timeline with heatmap scrubbing. The `/run` route is currently a stub. The Sim API already exposes template listing (`GET /api/v1/templates`), template detail with parameters (`GET /api/v1/templates/{id}`), and run orchestration (`POST /api/v1/orchestration/runs`) including dry-run preview. The Engine API lists completed runs (`GET /v1/runs`). This milestone wires the Svelte UI to those endpoints.

## Acceptance criteria

### AC-1 — Templates render as cards in a responsive grid with title

Templates render as cards in a responsive grid with title, description, domain icon, and version badge
### AC-2 — Search input filters cards by name/description in real-time

### AC-3 — Selecting a card fetches template detail and shows a configuration

Selecting a card fetches template detail and shows a configuration panel with: bundle reuse mode (Reuse/Regenerate/Fresh), RNG seed input, and collapsible "Advanced Parameters" section
### AC-4 — Can execute a model run and see results (run ID, metadata, warnings

Can execute a model run and see results (run ID, metadata, warnings, navigation links) when complete
### AC-5 — Preview/dry-run mode shows the execution plan without creating a run

### AC-6 — Loading, error, and empty states handled gracefully (skeletons, error

Loading, error, and empty states handled gracefully (skeletons, error cards, empty message)
### AC-7 — No raw JSON parameter field visible by default (hidden in Advanced section)
## Technical Notes

- Single page with state phases (selecting → configuring → running → success/error/preview), not sub-routes
- `POST /api/v1/orchestration/runs` is synchronous — blocks until complete, no polling needed
- Bundle reuse mapping: Reuse → `deterministicRunId:true, overwrite:false`; Regenerate → `deterministicRunId:true, overwrite:true`; Fresh → `deterministicRunId:false`
- Mode is inferred: telemetry when template has `captureKey`, simulation otherwise — shown as a read-only badge/indicator on the config panel (not a user choice, but visible)
- Domain icons: map template category/tags/title keywords to Lucide icons via utility function
- shadcn-svelte components needed: badge, collapsible, radio-group, alert
- Follow existing patterns: `$state`/`$derived` runes, `onMount` for data fetch, Card/Button/Input from shadcn-svelte

## Out of Scope

- Template parameter validation beyond basic type coercion
- Run history / run comparison
- Polling or WebSocket for long-running runs (API is synchronous)
- Template creation or editing
- Mobile/responsive layout optimization

## Dependencies

- M1 (scaffold), M2 (API layer), M4 (timeline — established API client patterns)
- Sim API running at port 8090 with templates available
