# Milestone: Run Orchestration

**ID:** m-svui-06
**Epic:** Svelte UI — Frontend Rewrite
**Status:** in-progress

## Goal

Add card-based template selection, run configuration, and model execution to the Svelte UI at `/run`.

## Context

M1-M4 delivered the SvelteKit scaffold, API clients, topology rendering with dag-map, and timeline with heatmap scrubbing. The `/run` route is currently a stub. The Sim API already exposes template listing (`GET /api/v1/templates`), template detail with parameters (`GET /api/v1/templates/{id}`), and run orchestration (`POST /api/v1/orchestration/runs`) including dry-run preview. The Engine API lists completed runs (`GET /v1/runs`). This milestone wires the Svelte UI to those endpoints.

## Acceptance Criteria

1. Templates render as cards in a responsive grid with title, description, domain icon, and version badge
2. Search input filters cards by name/description in real-time
3. Selecting a card fetches template detail and shows a configuration panel with: bundle reuse mode (Reuse/Regenerate/Fresh), RNG seed input, and collapsible "Advanced Parameters" section
4. Can execute a model run and see results (run ID, metadata, warnings, navigation links) when complete
5. Preview/dry-run mode shows the execution plan without creating a run
6. Loading, error, and empty states handled gracefully (skeletons, error cards, empty message)
7. No raw JSON parameter field visible by default (hidden in Advanced section)

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
