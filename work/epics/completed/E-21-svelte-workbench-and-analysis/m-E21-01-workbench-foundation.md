# Milestone: Workbench Foundation

**ID:** m-E21-01-workbench-foundation
**Epic:** E-21 — Svelte Workbench & Analysis Surfaces
**Status:** complete (merged to epic 2026-04-17)

## Goal

Establish the compact design system, implement dag-map click/hover events in the library, and build the workbench panel with click-to-pin node inspection — the foundation that every subsequent E-21 milestone builds on.

## Context

The Svelte UI has topology rendering (dag-map with heatmap mode), timeline scrubbing, run orchestration, and what-if parameter manipulation. But there is no way to click a node and inspect it. The layout uses shadcn-svelte's consumer-product defaults (generous padding, large text, wide sidebar) which waste space in a data-dense workbench.

This milestone delivers three things in sequence:
1. Compact design tokens that replace the spacious defaults
2. dag-map library events so nodes/edges are clickable
3. Workbench panel where clicked nodes show metrics and sparklines

### Prior art

- dag-map already emits `data-node-id` and `data-edge-from`/`data-edge-to` attributes on SVG elements (`lib/dag-map/src/render.js`)
- Topology page (`ui/src/routes/time-travel/topology/+page.svelte`) has timeline scrubbing and state API integration
- What-if page (`ui/src/routes/what-if/+page.svelte`) has real-time metric display via WebSocket
- `DagMapView.svelte` wraps dag-map with theme switching and metric mapping

## Acceptance Criteria

### Density system (AC1-AC5)

1. **Main content area padding reduced.** The root layout no longer applies blanket `p-6`. Page-level padding is context-dependent: topology/workbench pages use minimal padding (`p-1` or `p-2`), form/config pages may use moderate padding.

2. **Sidebar narrowed.** Expanded sidebar width ≤ 208px (from 280px). Collapsed width ≤ 40px. All nav items still readable and clickable.

3. **Compact design tokens defined in `app.css`.** Two token layers:
   - **Chrome tokens:** `--ft-bg`, `--ft-bg-elevated`, `--ft-border`, `--ft-text`, `--ft-text-muted`, `--ft-text-emphasis`. Calm values. Dark mode: near-black backgrounds (`hsl(220 10% 4%)`-range), subtle borders, muted gray text. Light mode: warm light backgrounds, subtle borders, dark text.
   - **Data-viz tokens:** `--ft-viz-teal`, `--ft-viz-pink`, `--ft-viz-coral`, `--ft-viz-blue`, `--ft-viz-green`, `--ft-viz-amber` plus sequential/diverging scale entry points. Vivid against both dark and light backgrounds.
   - **Spacing tokens:** `--ft-space-xs` (2px), `--ft-space-sm` (4px), `--ft-space-md` (6px), `--ft-space-lg` (8px), `--ft-space-xl` (12px). Tighter than the current 4/8/12/16/24px scale.
   - **Border radius:** `--ft-radius` at `0.25rem` or less (from `0.5rem`).
   - **Type:** working text size is `text-xs` (12px). Emphasis is `text-sm` (14px). Headers use `text-sm font-semibold` or `text-base`.

4. **shadcn component overrides applied.** Cards, buttons, inputs, and sidebar components use the compact tokens. No component uses raw `p-4`, `p-6`, `gap-4` etc. — spacing comes from the token scale.

5. **Existing pages still function.** What-if page, run orchestration, topology page, health page all render correctly with the new density. Visual audit confirms no layout breakage. Vitest and Playwright suites still pass.

### dag-map library events (AC6-AC8)

6. **`bindEvents()` exported from dag-map.** Given an SVG container element, `bindEvents(container, callbacks)` uses event delegation to fire:
   - `onNodeClick(nodeId, event)` — click on any `[data-node-id]` element
   - `onNodeHover(nodeId | null, event)` — mouseenter/mouseleave on node elements
   - `onEdgeClick(fromId, toId, event)` — click on any `[data-edge-from]` element
   - `onEdgeHover(fromId, toId | null, event)` — mouseenter/mouseleave on edge elements
   - Returns a cleanup function that removes all listeners.
   - Edge hit areas: edge paths are thin lines. `bindEvents` should set `pointer-events: stroke` and use a wider invisible stroke or a transparent hit-area overlay (≥ 8px clickable width) so edges are practically clickable.

7. **`selected` render option in dag-map.** `renderSVG(dag, layout, { ..., selected: Set<string> })` draws a selection indicator (ring, outline, or highlight) on nodes whose ID is in the set. The selection visual must compose correctly with heatmap mode (heatmap fills + selection ring, not one replacing the other).

8. **dag-map tests cover events and selection.** Unit tests (dag-map's existing test infrastructure) verify: `bindEvents` fires correct callbacks for node/edge clicks and hovers; `selected` set renders the selection indicator; selection composes with heatmap mode. dag-map version bumped and published (or linked via workspace protocol).

### Workbench panel (AC9-AC14)

9. **Topology page restructured as split layout.** The topology page shows the DAG in the upper area and the workbench panel in the lower area, separated by a resizable split (drag to resize, reasonable default like 60/40 or 65/35). When no nodes are pinned, the workbench shows a minimal empty state hint ("Click a node to inspect").

10. **Click-to-pin interaction.** Clicking a node in the topology DAG pins it to the workbench. The node appears with a selection indicator in the DAG (via `selected` set) and a card in the workbench. Clicking a pinned node again unpins it (removes card, removes selection indicator). Multiple nodes can be pinned simultaneously.

11. **Node card content.** Each workbench card shows:
    - Node ID and kind (service, queue, dlq, source, router, etc.)
    - Key metrics at the current timeline bin: utilization, queue depth, arrivals, served, errors, capacity — as available from the state API response
    - Sparkline showing the selected metric over the full time window (all bins)
    - Values formatted with appropriate precision (see `format.ts` utilities)
    - Compact layout using the density tokens — the card should fit meaningful content in ~180-220px width

12. **Timeline integration.** When the timeline scrubs (bin changes), all workbench card metric values update to the new bin. Sparklines show a position indicator (vertical line or dot) at the current bin.

13. **Cards dismissible.** Each card has a small close/unpin control. Dismissing a card removes the node from the `selected` set and the workbench.

14. **Auto-pin highest-utilization node on first load.** When a run loads and state data is available, the node with the highest utilization at bin 0 is auto-pinned to the workbench so it is never empty on first view. If utilization data is unavailable, skip auto-pin (empty state is acceptable).

### Cross-cutting (AC15-AC16)

15. **Playwright test coverage.** At least one Playwright spec covering: (a) topology loads and renders, (b) clicking a node opens a workbench card, (c) clicking the close control removes the card, (d) scrubbing the timeline updates card values. Specs skip gracefully if the API or dev server is unavailable.

16. **Vitest coverage for new pure logic.** Any new helper functions (metric extraction, card data shaping, sparkline data preparation) have vitest tests with branch coverage.

## Technical Notes

### Density system approach

- Introduce the `--ft-*` custom properties alongside the existing shadcn `--background`, `--foreground` etc. variables. The shadcn variables can initially alias the `--ft-*` tokens, keeping component library compatibility while allowing the token layer to diverge.
- The dark mode near-black should feel like the `.scratch/colors.png` reference: `hsl(220 10% 4%)` background, `hsl(220 8% 8%)` elevated, `hsl(220 6% 14%)` border. Text at `hsl(220 10% 65%)` for muted, `hsl(220 5% 85%)` for default.
- Light mode: `hsl(220 10% 97%)` background, `hsl(0 0% 100%)` elevated, `hsl(220 10% 88%)` border. Text at `hsl(220 10% 40%)` for muted, `hsl(220 10% 15%)` for default.
- Data-viz colors from `reference-palette.png` (epic folder) hue families: teal `#94E2D5`/`#2B8A8E`, pink `#F38BA8`/`#C45B4A`, coral `#EB6F92`, blue `#89B4FA`/`#3D5BA9`, green `#A6E3A1`/`#4A8C5C`, amber `#F9E2AF`/`#D4944C`. The dark-mode values are lighter/more vivid; light-mode values are darker/more saturated to maintain contrast.

### dag-map events

- dag-map already emits `data-node-id` on station `<g>` groups and `data-edge-from`/`data-edge-to` on edge `<path>` elements. `bindEvents()` delegates from the SVG container using these attributes.
- For edge hit areas: add an invisible wider stroke path (same shape, stroke-width 8-12px, `opacity: 0`, `pointer-events: stroke`) behind each visible edge path. This is a render-time addition in `render.js`, not a separate overlay.
- The `selected` visual should be a ring or glow around the node circle/rect, using a dedicated CSS class (`dag-map-selected`) so consumers can override the style. Default: 2px outline in the theme's `ink` color, offset by 2px.

### Workbench panel

- Implement the split as a CSS grid with `grid-template-rows` and a draggable splitter. No library dependency — a simple `mousedown` → `mousemove` → `mouseup` handler on a narrow divider element. Store the split ratio in `localStorage`.
- Node cards are a new Svelte component (`WorkbenchCard.svelte`). They consume the state API response that the topology page already fetches.
- Sparkline: reuse the existing `Sparkline.svelte` component. It already exists in `ui/src/lib/components/`.
- The workbench state (pinned node IDs) lives in a Svelte store so it persists across route navigations within the same session. Not persisted to `localStorage` (ephemeral per session).

### What-if page audit

- The what-if page (`/what-if`) uses its own layout with dag-map and parameter panels. It does NOT use the workbench. The density pass adjusts its spacing tokens but does not add workbench functionality to it. The what-if page is a standalone surface that predates the workbench paradigm.

## Out of Scope

- Edge cards (m-E21-02)
- Metric selector chip bar (m-E21-02)
- Class filter (m-E21-02)
- Analysis tab surfaces (m-E21-03/04)
- Heatmap view (m-E21-05)
- Validation/warning surfaces (m-E21-06)
- Final visual polish and dark mode QA (m-E21-07)
- Color palette iteration beyond the initial token values (user will bring examples for future iteration)
- dag-map layout engine changes (separate concern)
- Expert authoring surface

## Dependencies

- dag-map library (`lib/dag-map/`) — we own it; changes ship in this milestone
- E-18 Time Machine APIs available on port 8081 (already merged to main)
- E-17 what-if infrastructure (`/what-if` route, engine session API) — must not regress
- E-11 M6 run orchestration (`/run` route) — must not regress
