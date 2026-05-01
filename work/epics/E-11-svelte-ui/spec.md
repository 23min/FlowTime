# Epic: Svelte UI — Parallel Frontend Track

**ID:** E-11
**Status:** paused

## Goal

Build a SvelteKit + shadcn-svelte application in parallel with the Blazor WebAssembly frontend, delivering a polished, modern UI for demos and future evaluation while keeping the existing .NET backend APIs untouched.

## Context

The current Blazor UI (FlowTime.UI) is functionally complete but visually rough:
- 213+ `!important` CSS overrides fighting MudBlazor's styling
- Inconsistent spacing, typography, and elevation — no design system discipline
- 2,830 lines in a single `app.css` with ad-hoc values
- Hard-coded colors mixed with theme variables; fragile dark mode

The UI was generated iteratively with AI assistance and has accumulated CSS debt that makes incremental fixes risky — previous attempts to "fix spacing" caused cascading regressions. A visual-layer rewrite is safer than patchwork.

**Why Svelte:** After evaluating MudBlazor v9, Fluent UI Blazor, Radzen, Blazor Blueprint, and Tailwind-for-Blazor, the Svelte + shadcn-svelte stack was chosen for:
- shadcn-svelte provides the polished, modern aesthetic needed for impressive demos
- Svelte's compiler approach eliminates framework runtime overhead — ideal for the topology visualization (canvas + SVG)
- The timeline (HTML/SVG) maps naturally to Svelte's reactive SVG bindings
- The existing `topologyCanvas.js` (2,000+ LOC) can be reused directly — no IJSRuntime bridge needed
- Full access to the JS/TS component ecosystem (Tailwind, Radix-like primitives via Bits UI)
- The Blazor UI is already a standalone API client with no project references to the backend — clean parallel-surface addition without backend refactoring

**Design direction:** Left sidebar navigation (similar to Claude, ChatGPT, Azure Portal) with modern spacing, shadows, and transitions. Not the "MudBlazor default look" — a clean, enterprise-grade aesthetic via shadcn-svelte's design tokens and Tailwind.

Svelte is a parallel UI track, not a committed Blazor retirement plan. `FlowTime.UI` remains supported for debugging, operational use, and as a plan-B surface, so shared Engine/Sim contract changes must keep both UIs current.

## Scope

### In Scope

- SvelteKit application in `ui/` directory (alongside existing `src/FlowTime.UI/`)
- Layout shell: collapsible left sidebar, top bar with breadcrumbs/actions, theme toggle
- Time-Travel Topology page: timeline (SVG), canvas visualization, inspector, feature bar, overlays
- Time-Travel Dashboard: SLA metrics per node
- Time-Travel Artifacts: run list, detail view, file viewer
- Run Orchestration: model execution page
- Home page and Health page
- TypeScript API clients for FlowTime API (:8080) and FlowTime Sim API (:8090)
- Dark/light theme with system preference detection
- Reuse of existing JS: `topologyCanvas.js`, `horizonChart.js`, `theme.js` (adapted)
- DevContainer integration (port 5173 forwarding, dev scripts)

### Out of Scope

- Template Studio (TemplateRunner, gallery, dynamic forms) — deferred
- Learning Center (8 educational pages) — deferred
- ApiDemo page, Simulate page — deferred
- dag-map topology renderer — deferred (WIP in lib/dag-map, integrate last)
- Backend API changes — zero changes to FlowTime.API or FlowTime.Sim.Service
- Blazor UI removal or functional strip-down — `FlowTime.UI` remains a supported parallel surface and must stay aligned with current Engine/Sim behavior
- Mobile/responsive layout — desktop-first, responsive is a follow-up

## Constraints

- .NET 9 backend APIs are the source of truth — Svelte UI is a pure consumer
- Must work in the existing devcontainer (Node 20 already available)
- Canvas JS (`topologyCanvas.js`) reuse is mandatory — no rewrite of the rendering engine
- shadcn-svelte + Tailwind CSS v4 as the styling foundation
- TypeScript required (no plain JS)
- The Blazor UI must remain functional and reasonably current throughout — no breaking the existing app or allowing shared contract drift

## Success Criteria

- [ ] Svelte UI renders the topology page with timeline, canvas, inspector, and feature bar at feature parity with Blazor
- [ ] Dark/light theme works correctly across all pages
- [ ] All API integrations work against the existing FlowTime API and Sim API
- [ ] Demo workflow is smooth: navigate to artifacts, select a run, view topology, scrub timeline, inspect nodes
- [ ] Visual quality is a clear step up from the Blazor UI — clean spacing, consistent elevation, smooth transitions
- [ ] Lighthouse performance score >= 90 on the topology page
- [ ] No changes required to any backend project
- [ ] Shared Engine/Sim contract changes needed for Svelte do not leave Blazor behind on supported workflows

## Risks & Open Questions

| Risk / Question | Impact | Mitigation |
|----------------|--------|------------|
| topologyCanvas.js integration complexity — canvas callbacks, viewport state | High | M3 is dedicated to this; spike early if needed |
| shadcn-svelte component gaps (missing something MudBlazor had) | Med | Bits UI primitives allow building custom components; Tailwind fills gaps |
| Svelte 5 runes learning curve | Med | Start with simple pages (M1-M2) before tackling complex state (M3) |
| Two UIs running in parallel increases cognitive overhead | Low | Clear folder separation (`ui/` vs `src/FlowTime.UI/`); Blazor UI is frozen, not actively developed |
| dag-map integration timing unclear | Low | Explicitly deferred; topology canvas works standalone |

## Milestones

| ID | Title | Summary | Status |
|----|-------|---------|--------|
| m-svui-01-scaffold | Project scaffold & shell | SvelteKit + shadcn-svelte + Tailwind + layout shell with sidebar | **done** |
| m-svui-02-api-and-pages | API layer & simple pages | TypeScript API clients, stores, Home, Health, Artifacts pages | **done** |
| m-svui-03-topology | Topology via dag-map | dag-map SVG rendering with dark/light theme, run selector | **done** |
| m-svui-04-timeline | Timeline & playback | SVG timeline, bin scrubbing, dag-map heatmap mode for metric overlays | **done** |
| m-svui-05-inspector | Inspector & feature bar | Node inspector with sparklines, feature bar overlay settings, legend | not started |
| m-svui-06-run-orchestration | Run orchestration | Card-based template selection, bundle reuse, domain icons | not started |
| m-svui-07-dashboard | Dashboard & class views | SLA dashboard, class selector, per-node metric panels | not started |
| m-svui-08-polish | Visual polish & dark mode QA | Transitions, consistent elevation, dark mode audit, accessibility pass | not started |

## ADRs

- ADR-SVUI-01: Chose SvelteKit + shadcn-svelte over Blazor Blueprint, Fluent UI Blazor, and Tailwind-for-Blazor. Rationale: demo-quality visuals, Svelte's SVG/canvas strengths, escape from Blazor component library limitations. See conversation context.
- ADR-SVUI-02: ~~Reuse existing canvas JS~~ **Superseded.** Use dag-map library for topology rendering instead of topologyCanvas.js. dag-map is our own library with a general-purpose flow visualization roadmap (heatmap, variant explorer, Sankey). topologyCanvas.js stays in Blazor only. Extending dag-map keeps features reusable for non-FlowTime consumers.
- ADR-SVUI-03: `ui/` at repo root (not inside `src/`) to keep clear separation from the .NET solution structure. The devcontainer already has Node 20.
- ADR-SVUI-04: Use pnpm (not npm) for `ui/` — aligns with shadcn-svelte conventions, already installed in devcontainer.
- ADR-SVUI-05: Manual shadcn-svelte init (CLI is interactive-only in non-TTY). Created components.json + utils.ts manually, components added via `yes | pnpm dlx shadcn-svelte add`.
- ADR-SVUI-06: Pinned bits-ui to 2.15.0 — v2.16.4 has broken dist/types.js exports (missing attributes.js, pin-input references).

## Milestone Details

### M1 — Project Scaffold & Shell (m-svui-01-scaffold)
**Goal:** Standing SvelteKit app with sidebar layout and theme toggle.

Deliverables:
- SvelteKit project in `ui/` with TypeScript, Tailwind v4, shadcn-svelte initialized
- Root layout with collapsible left sidebar (nav groups: Time Travel, Tools)
- Top bar with breadcrumb area and theme toggle (dark/light/system)
- Empty route stubs for all in-scope pages
- DevContainer: port 5173 forwarded, `npm run dev` works
- Design tokens defined: spacing scale (4/8/12/16/24/32/48px), elevation levels, transition durations

Acceptance criteria:
- `npm run dev` serves the app at localhost:5173
- Sidebar collapses/expands with smooth transition
- Theme toggle persists to localStorage and respects system preference
- All route stubs render without errors

**Estimated effort:** 1 week

### M2 — API Layer & Simple Pages (m-svui-02-api-and-pages)
**Goal:** Data flows from APIs to UI. Simple pages are functional.

Deliverables:
- TypeScript API client modules (`lib/api/flowtime.ts`, `lib/api/sim.ts`)
- Shared types generated from or aligned with existing API contracts
- Svelte stores for: run list, selected run, health status, theme, feature flags
- Home page with navigation panels
- Health page showing both API statuses (green/red indicators)
- Artifacts list page with run cards, filtering, selection
- Artifact detail page with metadata display
- File viewer for artifact contents

Acceptance criteria:
- Health page shows live status from both APIs (or graceful error when API is down)
- Artifacts list loads and displays runs from the API
- Selecting a run navigates to detail view
- API error states show user-friendly messages

**Estimated effort:** 1.5-2 weeks

### M3 — Topology via dag-map (m-svui-03-topology)
**Goal:** Topology page renders run graph using the dag-map library.

Deliverables:
- `DagMapView.svelte` component wrapping dag-map's `dagMap()` function
- Graph API integration: fetch `/v1/runs/{id}/graph`, strip port-qualified edge IDs
- Dark/light theme-reactive rendering (transparent paper, shadcn-matched colors)
- Run selector dropdown on topology page
- API types for graph, state snapshot, state window (foundation for M4)

Acceptance criteria:
- Selecting a run renders its topology as a dag-map SVG
- Dark/light theme toggle updates the graph colors
- All route stubs still render without errors

**Completed:** 2026-03-30

### M4 — Timeline & Playback (m-svui-04-timeline)
**Goal:** Scrub through time bins with full playback controls.

**Dependency:** dag-map heatmap mode (per-node/edge metric coloring). Must be implemented in dag-map first — see dag-map ROADMAP.md "Heatmap mode" vision item.

Deliverables:
- dag-map heatmap mode: `metrics: Map<nodeId, { value, label }>` + color gradient
- `Timeline.svelte` component with SVG track, tick marks, labels
- Range input for bin scrubbing with pointer indicator
- Playback controls: play/pause, next/previous bin, loop toggle
- Speed selector chips (0.5x, 1x, 2x, 4x)
- Focus metric chips (Arrivals, SLA, Utilization, etc.)
- Bin change fetches state from API, maps metrics to dag-map heatmap

Acceptance criteria:
- Dragging the timeline scrubs through bins and updates node colors via dag-map heatmap
- Play button auto-advances bins at the selected speed
- Loop wraps around at the end
- Changing focus metric updates the color overlay

**Estimated effort:** 2-3 weeks (includes dag-map heatmap mode)

### M5 — Inspector & Feature Bar (m-svui-05-inspector)
**Goal:** Node inspection and overlay configuration.

Deliverables:
- `Inspector.svelte` — slide-in panel showing selected node details
- Sparkline charts (reuse horizonChart.js or rebuild in SVG)
- Metric blocks with provenance tooltips
- Constraint badges and warning indicators
- Dependency list for selected node
- `FeatureBar.svelte` — collapsible left panel with overlay settings
- Metric selection, color basis, edge quality filters
- Class visibility toggles
- Color scale configuration
- Legend overlay component

Acceptance criteria:
- Clicking a node opens the inspector with its metrics
- Sparklines show metric history for the selected node
- Feature bar toggles control what the canvas displays
- Changing color basis updates canvas overlay in real-time
- Inspector can be pinned open or dismissed

**Estimated effort:** 1.5-2 weeks

### M6 — Run Orchestration (m-svui-06-run-orchestration)
**Goal:** Card-based template selection with rich metadata and bundle reuse options.

Deliverables:
- Card grid for template selection: name, domain icon, version, short description
- Search/filter bar for the card grid
- Selected card expands to show: full description, bundle reuse toggle, RNG seed
- Bundle reuse options: Reuse (default) / Regenerate / Fresh run
- Run execution with state tracking (pending → running → complete)
- Preview/dry-run mode ("Preview" button, clearer than "Plan Model")
- Raw parameters field hidden (too debug-oriented) — accessible via "Advanced" expandable section if needed
- Domain icons: inferred from template category or metadata

Acceptance criteria:
- Templates render as cards with description and domain icon
- Search filters cards by name/description
- Selecting a card shows run options (reuse mode, seed)
- Can execute a model and see results when complete
- Loading, error, and empty states handled gracefully
- No raw JSON parameter field visible by default

### M7 — Dashboard & Class Views (m-svui-07-dashboard)
**Goal:** SLA dashboard and class-based metric views.

Deliverables:
- Dashboard page with per-node SLA indicators
- Class selector component (multi-select filter)
- Per-node metric summary panels
- Class-filtered topology view

Acceptance criteria:
- Dashboard shows SLA status for all nodes in a run
- Class filter correctly hides/shows nodes
- Metric panels update with class selection
- Loading and error states are handled gracefully

### M8 — Visual Polish & Dark Mode QA (m-svui-08-polish)
**Goal:** Demo-ready visual quality.

Deliverables:
- Consistent transitions on all interactive elements (150ms/300ms standard)
- Elevation system audit: panels, overlays, modals at correct shadow levels
- Dark mode full audit: every page, every component, every overlay
- Focus-visible states on all interactive elements
- Loading skeletons for data-heavy pages
- Empty state illustrations/messages
- Final spacing audit against the design token scale

Acceptance criteria:
- Dark mode has no visual glitches on any in-scope page
- All interactive elements have visible hover and focus states
- No jarring layout shifts on page load
- Sidebar collapse/expand is smooth
- A non-technical stakeholder finds the demo workflow visually polished

**Estimated effort:** 1 week
