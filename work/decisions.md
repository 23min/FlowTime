# Decisions

Shared decision log for active architectural and technical decisions.

<!-- Format:
## D-YYYY-MM-DD-NNN: <short title>
**Status:** active | superseded | withdrawn
**Context:** <why this decision was needed>
**Decision:** <what was decided>
**Consequences:** <what follows from this>
-->

## D-2026-03-30-001: dag-map for Svelte UI topology rendering
**Status:** active
**Context:** M3 originally planned to wrap topologyCanvas.js (10K LOC from Blazor UI). Initial integration worked but was rough — canvas sizing issues, requires overlay payload before draw, and the approach duplicates the Blazor rendering code.
**Decision:** Use dag-map library instead. dag-map is our own library with a general-purpose flow visualization roadmap. Extend dag-map with features needed by FlowTime (heatmap mode, click events, hover) rather than wrapping the Blazor-specific canvas JS.
**Consequences:** M4 (timeline) now depends on dag-map heatmap mode being implemented first. dag-map features must remain general-purpose, not FlowTime-specific. topologyCanvas.js stays in Blazor UI only.

## D-2026-03-30-002: pnpm for Svelte UI package management
**Status:** active
**Context:** Root repo uses npm (for Playwright tests). The `ui/` project needed a package manager.
**Decision:** Use pnpm — aligns with shadcn-svelte documentation conventions, already installed in devcontainer (v10.33).
**Consequences:** `ui/` has pnpm-lock.yaml, not package-lock.json. init.sh runs `pnpm install --frozen-lockfile` for ui/.

## D-2026-03-30-003: Manual shadcn-svelte initialization
**Status:** active
**Context:** `shadcn-svelte init` CLI is interactive-only (prompts for preset selection), cannot be run non-interactively in CI or automation.
**Decision:** Manually create `components.json`, `utils.ts`, and `app.css` theme variables. Add components individually via `yes | pnpm dlx shadcn-svelte add <component>`.
**Consequences:** Works in non-TTY environments. Must manually keep components.json aligned with shadcn-svelte schema on upgrades.

## D-2026-03-30-004: Pin bits-ui to 2.15.0
**Status:** active
**Context:** bits-ui 2.16.4 has broken dist/types.js — references `../bits/pin-input/pin-input.svelte.js` and `./attributes.js` which don't exist in the published package.
**Decision:** Pin bits-ui to 2.15.0 until the issue is fixed upstream.
**Consequences:** Check for fix on bits-ui releases periodically. Can unpin when 2.16.5+ ships.

## D-2026-03-30-005: dag-map lineGap default for single-route graphs
**Status:** active
**Context:** dag-map's lineGap (parallel line offset at shared nodes) defaults to 5px. For auto-discovered routes, this causes the trunk to wobble even when there's only one visual route.
**Decision:** Default lineGap to 0 when routes are auto-discovered (not consumer-provided). Only use non-zero lineGap when consumer explicitly provides multiple routes.
**Consequences:** Single-route graphs render with straight trunks. Multi-route flow layouts still get parallel line separation.

## D-2026-03-30-006: Svelte UI heatmap uses derived.utilization from state API
**Status:** active
**Context:** The FlowTime state API returns metrics at multiple levels: `metrics.*` (raw), `derived.*` (computed), `byClass.*` (per-class). Needed to pick the right field for heatmap coloring.
**Decision:** Use `derived.utilization` as primary heatmap metric, `derived.throughputRatio` as fallback. Other focus metrics (SLA, error rate, queue depth) to be added via a metric selector chip.
**Consequences:** Heatmap works end-to-end for utilization. Need to add metric selector for other derived fields.

## D-2026-03-31-001: Fix P0 engine bugs before further Svelte UI work
**Status:** active
**Context:** Engine deep review found 3 P0 bugs (shared series mutation, missing capacity dependency, dispatch-unaware invariant). Svelte UI shows data from these APIs — incorrect engine data means incorrect visualization.
**Decision:** Prioritize Phase 0 bug fixes (BUG-1, BUG-2, BUG-3) before continuing Svelte UI M4 completion or M5/M6.
**Consequences:** Svelte UI work pauses briefly. Engine correctness gates all downstream work.
