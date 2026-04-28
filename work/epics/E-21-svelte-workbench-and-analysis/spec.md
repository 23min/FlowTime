# Epic: Svelte Workbench & Analysis Surfaces

**ID:** E-21

## Goal

Transform the Svelte UI from a Blazor-parallel clone into the primary platform for expert flow analysis and Time Machine surfaces, using a workbench paradigm (topology as navigation + inspection panel) instead of the Blazor overlay approach.

## Context

The fork decision (2026-04-15) declared Svelte the platform for all new surfaces and Blazor maintenance-only. E-11 delivered M1-M4 + M6: scaffold, API layer, topology via dag-map, timeline with heatmap scrubbing, and run orchestration. E-17 added the what-if page with live parameter manipulation (200 vitest + 26 Playwright).

What exists in Svelte today:
- SvelteKit + shadcn-svelte + Tailwind v4, dark/light theme toggle
- Topology DAG via dag-map with heatmap overlays and timeline scrubbing
- Run orchestration (template cards, bundle reuse)
- What-if page (E-17 — WebSocket parameter panel with real-time topology updates)
- Routes: `/`, `/health`, `/run`, `/what-if`, `/time-travel/topology`, `/time-travel/dashboard`, `/time-travel/artifacts`, `/engine-test`

What's missing:
- Node/edge inspection (no way to click a node and see its metrics)
- Analysis surfaces for Time Machine APIs (sweep, sensitivity, goal-seek, optimize, validate — all built in E-18 but with no UI)
- Heatmap view (nodes-x-bins temporal pattern grid)
- Compact, information-dense layout (current shadcn defaults are too spacious for a technical workbench)
- Distinctive visual identity (current theme is generic shadcn)

The UI paradigm proposal (`work/epics/unplanned/ui-workbench/reference/ui-paradigm.md`) established the direction: topology as navigation surface (structure + one color dimension), workbench panel for depth (click-to-pin cards), and layered views (heatmap, decomposition, comparison). E-21 implements that paradigm and adds the first Time Machine analysis surfaces.

### Supersedes

- **E-11 M5** (Inspector & Feature Bar) — evolves into m-E21-01/02 workbench paradigm
- **E-11 M7** (Dashboard) — deferred; workbench + heatmap cover the same ground better
- **E-11 M8** (Polish) — absorbed into m-E21-08

E-11 remains paused at M6 as a completed historical track.

### Relationship to other unplanned epics

- **UI Workbench & Topology Refinement** (`work/epics/unplanned/ui-workbench/spec.md`) — m-E21-01 and m-E21-02 implement its goals (G1-G5)
- **UI Analytical Views** (`work/epics/unplanned/ui-analytical-views/spec.md`) — m-E21-06 delivers the heatmap view; decomposition and comparison views are future extensions
- **Expert Authoring Surface** (`work/epics/unplanned/expert-authoring-surface/spec.md`) — not in E-21 scope; depends on E-21's workbench and validation surface being in place

## Scope

### In Scope

- Compact design system replacing shadcn spacing defaults — information-dense, calm chrome, rich data-viz colors
- Workbench panel with click-to-pin node/edge cards, sparklines, metric display
- Metric selector and class filter replacing Blazor's 15-toggle feature bar
- dag-map library enhancements (click/hover events, selected state) — we own dag-map
- Analysis page with tabbed Time Machine surfaces: sweep, sensitivity, goal-seek, optimize
- Heatmap view (nodes-x-bins grid, first layered view)
- Validation surface with tiered diagnostic display and topology warning badges
- Dark/light theme with distinctive palette (calm chrome, vivid data colors)
- Visual polish pass for demo-ready quality

### Out of Scope

- Expert authoring surface (CodeMirror + inline lenses — separate future epic)
- Executive/BI dashboard (separate surface consuming FlowTime data, not this UI)
- Blazor changes beyond maintenance-mode contract alignment
- Model fitting UI (`POST /v1/fit` — blocked on Telemetry Loop & Parity)
- Chunked evaluation / streaming UI
- Backend API changes other than read-only run-adjacent endpoints (see Constraints). Authoring, orchestration, compute, write paths, telemetry sinks — all out of scope.
- Mobile/responsive layout
- E-15 telemetry ingestion UI (parallel track, not E-21 scope)
- dag-map layout engine changes (separate `ui-layout` epic)
- Decomposition view, comparison view, flow balance view (future layered views beyond heatmap)

## Design Direction

### Audience

| Audience | Surface | Relationship to E-21 |
|----------|---------|---------------------|
| Expert modeler | Svelte workbench | Primary user. Compact, data-dense, keyboard-aware. |
| Demo viewer | Same workbench | The demo IS the tool. No separate demo mode. Must look like a serious technical instrument. |
| Executive / BI | Separate surface (future) | Not this UI. Consumes FlowTime data exports/API. Out of scope. |

### Density

The shadcn-svelte defaults produce a consumer-product aesthetic: generous padding (`p-4`/`p-6`), large text, wide spacing, friendly rounded corners. A workbench needs the opposite: tight padding, smaller type, information density. Every pixel of chrome padding is a pixel not showing data.

Concrete changes from current state:
- Main content padding: `p-6` (24px) → `p-2` (8px) or context-dependent
- Sidebar: 280px → ~200px expanded, ~40px collapsed
- Border radius: `0.5rem` → `0.25rem` or less
- Working text size: `text-xs` (12px) as default body, `text-sm` (14px) for emphasis
- Component padding: tighten all shadcn component internals
- Spacing scale: use 2/4/6/8/12px steps, not 4/8/12/16/24px

### Color Architecture

Two separate token layers:

**Chrome tokens** (backgrounds, borders, text) — calm, restrained, few colors:
- Dark mode: near-black backgrounds (`hsl(220 10% 4%)`-range), subtle dark grid/border lines, muted light gray text (not bright white)
- Light mode: warm light gray backgrounds, subtle borders, dark text
- Minimal accent color usage in chrome — the frame should recede

**Data-viz tokens** (heatmaps, charts, sparklines, topology overlays) — where vibrancy lives:
- Hue families: teal/cyan, pink/magenta, coral/red, blue, green, gold/amber
- Designed for legibility against both dark and light backgrounds
- Consistent across all data surfaces (topology heatmap, sparklines, analysis charts, heatmap grid)
- Sequential scales for metric mapping (single-hue ramps)
- Diverging scales for comparison/delta views

Reference inspiration: o16g.com/data/ dark palette (see `reference-palette.png` alongside this spec). The goal is "quantitative instrument" not "friendly SaaS dashboard."

### Theming Iteration

The token architecture must make major theme changes easy:
- All spacing from semantic tokens, not inline Tailwind values
- Chrome and data-viz as independent palettes
- Theme presets possible by swapping one CSS import
- User will bring visual examples for palette iteration — the system must accommodate this without touching component code

## Constraints

- .NET 9 backend APIs are the source of truth. Svelte UI is primarily a consumer. Two explicit carve-outs are admitted: (1) per **D-2026-04-17-033**, E-21 may add **read-only run-adjacent endpoints** that strictly serve already-persisted run artifacts (e.g. `GET /v1/runs/{runId}/model`); (2) per **D-2026-04-21-034**, E-21 may extend existing compute-endpoint response shapes with **additive** fields that expose state the endpoint already computes internally (specifically the `trace` field on `/v1/goal-seek` and `/v1/optimize`). Authoring, orchestration, new compute endpoints, write-path endpoints, and non-additive shape changes to existing compute endpoints remain out of scope and need their own decision record if ever proposed.
- dag-map enhancements must remain general-purpose (not FlowTime-specific API)
- Existing what-if page (E-17) must continue working after workbench changes
- Existing run orchestration (E-11 M6) must continue working
- Playwright tests required for every milestone that ships UI changes
- Vitest for pure logic (helpers, store derivations, protocol encoding)
- Must work in existing devcontainer (Node 20, ports 5173/8081)

## Success Criteria

- [ ] Clicking a node in the topology opens a workbench card with metrics and sparkline
- [ ] All five Time Machine analysis modes are accessible from the UI (sweep, sensitivity, goal-seek, optimize, validate)
- [ ] Heatmap view shows nodes-x-bins temporal patterns with shared color scale
- [ ] Layout is visibly more compact than current shadcn defaults — no "air"
- [ ] Dark and light themes both work with calm chrome and vivid data colors
- [ ] A non-technical viewer watching a demo finds the tool visually serious and information-rich
- [ ] Existing what-if and run orchestration pages still function correctly
- [ ] dag-map click/hover events are implemented in the library, not as wrapper hacks

## Risks & Open Questions

| Risk / Question | Impact | Mitigation |
|----------------|--------|------------|
| Compact density feels cramped or hard to read | Med | Start with defined density tokens; adjust after user testing. Semantic tokens make changes cheap. |
| dag-map library changes take longer than expected | Med | dag-map click/hover can be wired externally as a fallback while library work proceeds. Don't block workbench on it. |
| Color palette needs multiple iterations to land | Low | Token architecture makes palette swaps cheap. User will bring examples. |
| Analysis API calls are slow for large sweeps (session evaluator helps) | Med | Show progress/loading state. `SessionModelEvaluator` (m-E18-13) is the default and is fast for moderate sweeps. |
| Heatmap SVG performance with many nodes x bins | Low | 20 nodes x 100 bins = 2000 rects, well within SVG limits. Add row virtualization if measured problems appear. |
| What-if page assumes current spacing/layout | Med | Audit what-if page during density pass. It may need minor layout adjustments. |

## Milestones

| ID | Title | Summary | Status |
|----|-------|---------|--------|
| m-E21-01-workbench-foundation | Workbench Foundation | Density system, dag-map events (library), topology as navigation (one color dimension), workbench panel with click-to-pin node cards | **complete** (merged 2026-04-17) |
| m-E21-02-metric-selector-edge-cards | Metric Selector & Edge Cards | Metric chip bar, edge click-to-pin, edge cards, class filter | **complete** (merged 2026-04-17) |
| m-E21-03-sweep-sensitivity | Sweep & Sensitivity Surfaces | `/analysis` route with tabs, sweep config + results, sensitivity bar chart | **complete** (merged 2026-04-17; ultrareview follow-ups 2026-04-20) |
| m-E21-04-goal-seek | Goal Seek Surface | Goal-seek panel, shared convergence chart + result card, `trace` on `/v1/goal-seek` and `/v1/optimize` (per D-2026-04-21-034) | **complete** (2026-04-22) |
| m-E21-05-optimize | Optimize Surface | N-param Nelder-Mead surface reusing shared convergence chart + result card from m-E21-04; per-param range table | **complete** (2026-04-22) |
| m-E21-06-heatmap-view | Heatmap View | Nodes-x-bins grid, row sorting, click-to-jump, view switcher (topology/heatmap), shared full-window color scale, shared node-mode toggle | **complete** (2026-04-24) |
| m-E21-07-validation-surface | Validation Surface (Svelte) | Consumer-side type widening on `state_window` warnings; validation panel as left column inside workbench panel; topology node + edge warning indicators; workbench-card warning surfaces; bidirectional cross-link via shared view-state store; Playwright real-bytes fixture for AC1 round-trip. No backend work. | **complete** (2026-04-28) |
| m-E21-08-polish | Visual Polish & Dark Mode QA | Transitions, elevation audit, dark mode audit, loading skeletons, accessibility | not started |

## ADRs

- ADR-E21-01: New epic E-21 rather than resuming E-11. E-11 was "clone Blazor in Svelte." E-21 is "build the workbench paradigm + new surfaces." E-11's remaining milestones (M5/M7/M8) are absorbed with different scope.
- ADR-E21-02: dag-map click/hover events implemented in the library (we own dag-map) rather than external wrapper hacks. Cleaner API, reusable for non-FlowTime consumers.
- ADR-E21-03: Analysis surfaces use tabbed interface at `/analysis` route rather than separate routes. Tabs enable comparison and reduce navigation.
- ADR-E21-04: Dashboard (E-11 M7) deferred. Workbench + heatmap view cover per-node overview better than a dedicated dashboard page.
- ADR-E21-05: Density pass is m-E21-01 scope, not polish. Building on spacious defaults and compressing later cascades through every component.
