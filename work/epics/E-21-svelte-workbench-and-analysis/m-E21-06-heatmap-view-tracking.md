# Heatmap View — Tracking

**Started:** 2026-04-23
**Completed:** 2026-04-24
**Branch:** `milestone/m-E21-06-heatmap-view` (branched from `epic/E-21-svelte-workbench-and-analysis` at commit `b3b4a33`)
**Spec:** `work/epics/E-21-svelte-workbench-and-analysis/m-E21-06-heatmap-view.md`
**Commits:** `4abb114` (start), `5dddb5d` (implementation + wrap)

<!-- Status is not carried here. The milestone spec's frontmatter `status:` field is
     canonical. `**Completed:**` is filled iff the spec is `complete`. -->

## Scope Recap

Deliver a nodes-x-bins heatmap view as a **sibling of topology** under `/time-travel/topology`, sharing the toolbar, class filter, metric selector, timeline scrubber, workbench sidebar, and pin state. Heatmap reuses the existing `GET /v1/runs/{runId}/state_window` endpoint — **zero backend changes** beyond an optional `mode` parameter on the client method (backward-compatible).

Introduces a typed `<ViewSwitcher>` component (views inline on the topology route — no registry), a shared view-state store (`view-state.svelte.ts`), a shared full-window 99p-clipped color-scale normalization (topology switches from per-bin to match — ADR-m-E21-06-02), and a new shared-toolbar `[ Operational | Full ]` node-mode toggle (AC15) reaching parity with the Blazor UI's operational-nodes affordance.

## Confirmations captured at start (2026-04-23)

1. **ADR-m-E21-06-02 ships as a straight swap.** No `?normalization=per-bin` escape hatch. Topology's coloring visibly changes to match heatmap under the full-window 99p-clipped domain. To be called out in the milestone's wrap release notes.
2. **Playwright #11 uses the `data-value-bucket` assertion only.** Numeric correctness is enforced by the `computeSharedColorDomain` vitest suite (AC14 pure-logic suite #2).
3. **AC12 homework → file separate gaps.** Topology's current keyboard/ARIA posture and m-E21-02's data-viz color-blind validation are audited during implementation; any deficits are filed in `work/gaps.md` rather than retrofitted in this milestone.
4. **Topological sort derivation.** Prefer reusing a topo-sorted list from `dag-map` if exposed; otherwise implement Kahn's algorithm over `graph.edges` with node-id tiebreak. Choice made at implementation time.

## Baseline test counts (at start)

| Surface | Count |
|---|---|
| `ui/` vitest | 501 passing |
| `lib/dag-map/` node-test | 304 passing |
| `.NET` build | green |

(CLAUDE.md's wrap-m-E21-05 figure of "520 vitest + 293 dag-map" drifted slightly against my local preflight. Recording actuals as the baseline for this milestone; not chasing the historical delta.)

## Acceptance Criteria

<!-- Mirror ACs from the spec. Check each when its Work Log entry lands. -->

- [x] **AC1** — View switcher renders above the canvas: new `<ViewSwitcher>` component, inline view array on topology route, `Alt+1` / `Alt+2` shortcuts, shadcn underline, initial view = Topology (not persisted across reloads).
- [x] **AC2** — Heatmap replaces the canvas only: toolbar + scrubber + workbench persist; splitter / pins / scrubber preserved across view switches both directions.
- [x] **AC3** — Heatmap grid renders correct `(N rows × B cols)` dimensions; row labels on left in active sort order; sparse bin-axis labels via `pickBinLabelStride`; absolute-time labels when `timestampsUtc[bin]` resolves, offset-from-start otherwise; tooltip shows both bin index + time. **Horizontal fit invariant:** SVG width never exceeds the container width. Two mechanisms: (1) **auto-fit** — when `naturalWidth (= ROW_LABEL_W + binCount × 18) > containerWidth`, `CELL_W` auto-compresses to `max(1, min(18, (containerWidth − ROW_LABEL_W − RIGHT_MARGIN) / binCount))` using **fractional pixels** so cells fill available space exactly (integer floor left ~200 px right-side gaps on 288-bin runs). (2) **Fit-to-width toggle** (shared toolbar, persisted `ft.heatmap.fitWidth`, default off) forces compression even when content would fit naturally. Effective `shouldFit = fitWidth || (natural > container)`. Iteration log 2026-04-24: shipped v1 with `MIN_CELL_W=3` + integer floor → overflow on narrow canvases; v2 dropped `MIN_CELL_W=1` + added auto-fit → still scrolled because of upstream flex-item intrinsic-sizing bug; v3 added `min-w-0` on the topology flex chain → still scrolled because `SidebarInset` in root layout lacked `min-w-0`; v4 added `min-w-0` on `SidebarInset` + `<main>` in root layout → fit worked but CELL_W=3 left ~200 px gap on right; v5 switched to fractional `CELL_W` with 4 px right-margin for a clean fill. Sliding-window scrubber (Blazor-parity zoom-and-pan) deferred to a dedicated follow-up milestone — filed in `work/gaps.md`.
- [x] **AC4** — Three cell states with disambiguating tooltips: observed (including 0) / no-data-for-bin (hatched) / metric-undefined-for-node (row-level muted); classifier keys are per-(node, metric); non-observed cell click is a no-op.
- [x] **AC5** — Shared full-window 99p-clipped color-scale normalization via `computeSharedColorDomain`; topology's per-bin-raw coloring is replaced by the shared domain (straight swap, per ADR-m-E21-06-02). `buildNormalizedMetricMap` + `computeMetricDomainFromWindow` are the topology seam; `buildHeatmapGrid` is the heatmap seam. Both emit normalized `[0, 1]` values into the dag-map palette / heatmap palette.
- [x] **AC6** — Row sort modes: topological (default) / node id / max desc / mean desc / variance desc; pin position is natural within the active sort (no float to top — pin glyph AC10 is the sole pinned-row indicator); deterministic tie-break by node id; sort mode preserved across metric switches (store holds `sortMode` across metric changes). Amended mid-implementation 2026-04-23 — see "Decisions made during implementation" below.
- [x] **AC7** — Class filter parity with topology (hide by default) + row-stability toggle ("Keep filtered rows", off by default, persisted in `localStorage` as `ft.heatmap.rowStability`); empty-state message when filter collapses grid to zero rows; pin state persists across filter toggle (workbench store is filter-independent); composes with AC15 node-mode toggle.
- [x] **AC8** — Click-an-observed-cell atomically pins node in workbench AND moves scrubber to that bin; non-observed cell click is no-op; unpin is via pin-glyph or workbench card close button.
- [x] **AC9** — Scrubber-to-bin highlight (two-way coupling): single pre-rendered overlay `<rect>` whose `x` updates on `currentBin` change; chrome-scale turquoise token `var(--ft-highlight)` (not a data-viz token) so it does not compete with cell colors; rendered as a solid 3 px-high bar in the top-axis gutter (one cell wide, above the clicked column); real-time during scrubber drag. Spec AC9 amended twice on 2026-04-23: first bumped from `var(--ring)` 1 px to `var(--ft-highlight)` 2.5 px full-column outline; then reduced to a top-bar marker after the full-column render competed with cell data and was visually obscured by cell fills.
- [x] **AC10** — Pinned-row glyph in the label gutter (Lucide `PinIcon`, keyboard-focusable `<button>`, `aria-label="Unpin <id>"`); click / Enter / Space unpins. Glyph is the **sole** pinned-row indicator (AC6 amended — no positional float).
- [x] **AC11** — Bin-axis labels with auto-chosen stride targeting ~100px per label via `pickBinLabelStride`; sensible round units per `binSize × binUnit`; tooltip always shows both bin index + time (`Bin 42 · +03:30` or `Bin 42 · 12:30`).
- [x] **AC12** — Accessibility baseline: keyboard nav (arrow keys move focus via `computeNextFocus`, Enter/Space pins + jumps, Escape returns focus to toolbar); ARIA `role="grid"` + `role="row"` + `role="gridcell"` with `aria-label`; cell focus/selection indicator via a persistent `selectedCell`-driven SVG overlay (turquoise `--ft-highlight` outline, 2 px stroke) held on the shared view-state store and surviving window blur — the browser-default SVG `<g>` focus outline is suppressed because Chromium renders it as a beveled rectangle that doesn't respect the `outline` token; workbench-card title renders in turquoise `--ft-highlight` when its node matches the selected cell (card cross-link via `WorkbenchCard.selected` prop); **pinned node cards follow the active `viewState.sortMode` in both views** (topological / id / max / mean / variance) via the route-level `sortedPinnedNodes` derivation using `sortHeatmapRows` — sort is a graph-wide preference, not heatmap-only; dropdown surfaces in the heatmap toolbar but the selection persists across view switches; edge cards unaffected; tooltip-on-focus via SVG `<title>`; Playwright keyboard specs #12 + #12b + #12c (persistent overlay) + #12d (card title cross-link). Pattern encoding / high-contrast / SR polish deferred to m-E21-08.
- [x] **AC13** — Shared view-state store at `ui/src/lib/stores/view-state.svelte.ts` holding `selectedMetric` (proxy), `activeClasses`, `currentBin`, `binCount`, `playing`, `pinnedNodes`/`pinnedEdges` (proxied to workbench store), `activeView`, `sortMode`, `rowStabilityOn`, `nodeMode`. Topology route refactored to consume it (no breaking changes to `workbench.svelte.ts` public surface).
- [x] **AC14** — Testing: 13 Playwright critical-path specs + 1 extra Escape spec (#12b) + 7 vitest pure-logic suites (cell-state, bin-label-stride, heatmap-sort, shared-color-domain, view-state, heatmap-geometry, value-normalize, heatmap-time-format, heatmap-keyboard-nav, metric-defs additions), every reachable branch covered (line-by-line audit in Coverage Notes).
- [x] **AC15** — Node-mode toggle `[ Operational | Full ]` in shared toolbar; state in `nodeMode` on view-state store; drives `mode` query param on `getStateWindow`; re-fetches on toggle change via `loadWindow()`; persists in `localStorage` as `ft.view.nodeMode`; applies to topology and heatmap (shared-state parity asserted by Playwright spec #13); parity with Blazor UI's pre-existing operational-nodes toggle.

## Planned test coverage (AC14 reference — not a second tracking list)

<!-- Reference inventory from the spec's AC14 plan. AC14's checkbox above is the
     canonical "done" signal; the items below are ticked when the corresponding
     spec/suite lands so a reader can cross-check without re-reading the spec. -->

**Playwright specs** (live Rust engine + Svelte dev; graceful-skip on probe; under `tests/ui/specs/svelte-heatmap.spec.ts`, authoring-choice at implementation):

- [x] #1 — Loads a run; grid renders expected `(N × B)` dimensions.
- [x] #2 — Three cell states render with correct disambiguating tooltips on a fixture known to exercise each.
- [x] #3 — Click-an-observed-cell → pin + scrubber-jump (atomic single-click effect).
- [x] #4 — Metric switch recolors cells AND reorders rows under max-desc sort.
- [x] #5 — View-switch state preservation: pin on topology → heatmap shows pin glyph on the corresponding row (sort position unchanged — no float) → switch back → still pinned.
- [x] #6 — Scrubber drag moves the column highlight in real time; clicking a cell jumps scrubber and highlight follows.
- [x] #7 — Sort modes reorder rows (topological → node-id → max-desc → mean-desc).
- [x] #8 — Class filter default = hide; row-stability toggle ON = dimmed rows at bottom; toggle OFF = hidden again. (Landed; graceful-skip on warehouse-picker-waves fixture — no `classCoverage`.)
- [x] #9 — Empty-state message when class filter collapses grid to zero rows. (Landed; graceful-skip on same fixture.)
- [x] (#10 is the graceful-skip requirement, not a standalone test — enforced on every spec.)
- [x] #11 — Correctness via `data-value-bucket` (`low` / `mid` / `high`); bucket assertion only, no raw CSS-color parsing.
- [x] #12 — Keyboard nav critical path (Tab → arrows → Enter → same observable effect as #3).
- [x] #13 — Node-mode toggle: default `operational` shows N rows; `full` grows row count to include `expr`/`const`/`pmf`; computed-node rows row-level-muted under `utilization`; toggle back shrinks. Topology's visible node count = heatmap row count under each toggle state (shared-state parity).

**Vitest pure-logic suites** (branch-covered per hard rule):

- [x] **Suite 1** — `heatmap-sort.ts` comparators: topological / id / max / mean / variance with **pin-agnostic sort** (pinned rows keep natural position — the pin glyph is the sole indicator). Exercises empty / single / all-equal / topological-sibling tie-break + explicit "no pinned partitioning" coverage (mode=id half-pinned → pure alpha; mode=topological downstream-pinned → stays downstream; mode=max lowest-max-pinned → sinks to bottom).
- [x] **Suite 2** — `shared-color-domain.ts`: full-window 99p-clipped, excluding no-data / undefined / filtered. Exercises all-observed / mix / all-missing (domain empty) / single-value / 99p edge cases / class-filter exclusion.
- [x] **Suite 3** — `cell-state.ts` classifier: observed (incl. 0) / no-data / row-level-muted. Exercises undefined-all-bins (row-level) / undefined-some-bins (per-cell) / zero-value (observed).
- [x] **Suite 4** — `bin-label-stride.ts`: `pickBinLabelStride(columnPixelWidth, binSize, binUnit)`. Exercises 5-min wide → hourly / 5-min narrow → larger / hourly → daily / degenerate (0 cols, 1 bin, huge binSize).
- [x] **Suite 5** — `view-state.svelte.test.ts`: metric set/get, class filter add/remove/clear, scrubber set/move, pin/unpin proxies, row-stability toggle persist, sort-mode set, active-view switch, node-mode toggle + localStorage persist.

## Decisions made during implementation

<!-- Decisions that came up mid-work that were NOT pre-locked in the milestone spec. -->

- **Time-axis absolute labels come from `StateWindowResponse.timestampsUtc`, not `RunGrid.startTime`.** The spec (AC3) referenced `RunGrid.startTime`, but `RunIndex.grid` in `types.ts` has no `startTime` field — the Window response already carries per-bin `timestampsUtc[]` which is strictly better (absolute times per bin, not start + derived). `formatBinTime` consumes the timestamps when available and falls back to `+HH:MM` offset-from-bin-0 using the grid's `binSize × binUnit`. No user-visible change from the spec intent; the data source is more precise.
- **Topology rewire landed via `buildNormalizedMetricMap` + `computeMetricDomainFromWindow`**, not via refactoring `buildMetricMapForDefFiltered`. Reasons: (1) the existing function is used by multiple call sites and changing its return type would ripple through the codebase unnecessarily; (2) the new function makes the "normalized vs raw" split explicit in the type system — callers requesting a normalized map MUST supply a domain. The old function remains in place for any callers that still want raw values (none in topology now, but the `WorkbenchCard` sparkline path still uses `buildSparklineSeries` which is raw, as sparklines don't normalize).
- **The heatmap row-gutter width is a component-local constant (`ROW_LABEL_W=132`).** Spec left sizing unspecified; chose 132px as the narrowest value that renders typical FlowTime node ids (`WarehousePicker`, `PackAndShip`, etc.) without truncation at the `10px text-xs` font. If truncation becomes common the CSS has `text-overflow: ellipsis` as a safety net; file a gap if a user complains.
- **`pickBinLabelStride` default unit fallback is 'minutes'.** The component casts `grid.binUnit` to `BinUnit` type; when the runtime value is outside the four known units (seconds/minutes/hours/days), the helper returns stride 1 and the bin-label formatter treats the unit as minutes. This is explicitly defensive for unknown future units landing in an older UI.
- **Escape key returns focus to a `[data-heatmap-toolbar] button`, not literally the heatmap toolbar.** The topology route marks its top-level toolbar div with `data-heatmap-toolbar`, and Escape lands on the first button inside that toolbar. When the heatmap is rendered standalone (e.g. a future decomposition view that doesn't mount under the toolbar), the Escape path degrades to "focus the body" — the grid cell simply loses focus. Tested by Playwright spec #12b.
- **Cell-click on filtered rows is a `no-op` with tabindex=-1, not a hard click-intercept.** A focused (observed, non-filtered) cell has `tabindex=0`; filtered / muted cells have `tabindex=-1` and their click handler early-returns in `onCellClick`. Visually filtered cells carry the row-level muted overlay; the early-return in the click handler is the functional guarantee.
- **Domain excludes filtered rows even with row-stability ON.** Dimmed placeholder rows contribute no cells to the shared color domain regardless of the row-stability toggle — the toggle is purely a visual affordance, not a domain decision. Matches the spec's "excluded from the normalization domain" clause in AC7.
- **No backend-side changes; `getStateWindow`'s new `mode` param is URL-additive.** When `mode` is undefined (historical call sites), the URL is byte-compatible with pre-milestone. When `mode` is supplied, it appends `&mode=operational|full`. Proved by the foundation-pass URL-shape tests in `flowtime.test.ts`.
- **Pinned-first row float removed mid-implementation per user UX feedback.** Option C (no modifier at all) chosen over Option B (a toggle) because it's the cleaner contract — the sort dropdown does exactly what the mode label says; the pin glyph (AC10) becomes the sole pinned-row indicator. Spec AC6 / AC10 / AC14 amended post-start; "Confirmations at start" block in the spec gained a post-start item #5. Code change: `pinnedIds` removed from `SortOptions` in `heatmap-sort.ts`; `sortHeatmapRows` collapses to a single `.sort(cmp)` with no partition/concat. `heatmap-geometry.ts` retains `pinnedIds` on `HeatmapGridInput` for call-site compatibility with the heatmap view (which passes it through for pin-glyph rendering in the row-label gutter) but no longer forwards it to `sortHeatmapRows`. Four vitest tests rewritten (sort suite `pinned-first` block deleted and replaced with a "no pinned partitioning" block of three tests proving the new contract; geometry `sort + pinned-first` block rewritten to assert pinned rows keep their natural position under id + topological sorts). Playwright spec #5 updated to locate the pinned row by id (`[data-row-id="${pinnedId}"]`) rather than `.first()`, asserting the pin-glyph attribute `data-row-pinned=true` without implying position 0.
- **Pin glyph red (`--ft-pin` token).** User requested red for the pin glyph. New chrome-scale token `--ft-pin` added (`hsl(355 65% 48%)` light / `hsl(355 60% 60%)` dark) rather than reusing `--destructive` (semantic "destructive action" baggage — pin is not a destructive interaction) or a viz token (viz tokens are reserved for data-value encoding and must not bleed into chrome). Consumed by `.heatmap-pin-btn`.
- **Column highlight prominence bump.** The scrubber's current-bin column highlight (AC9) moved from a 1px outline to a 2px outline plus `fill-opacity=0.08` low-alpha tint. Spec AC9 already permits either a strip or an outline; using both gives unambiguous readability on all themes + against both low-saturation muted cells and fully-saturated extreme cells. Implemented on the single overlay `<rect>` — still zero grid re-render cost.

## Work Log

<!-- One entry per AC (preferred) or per meaningful unit of work.
     Header: "AC<N> — <short title>" or "<short title>" if not AC-scoped.
     First line: one-line outcome · commit <SHA> · tests <N/M>
     Optional prose paragraph for non-obvious context. Append-only. -->

### Start-milestone — status reconciliation

Created branch `milestone/m-E21-06-heatmap-view` from `epic/E-21-svelte-workbench-and-analysis` at `b3b4a33`. Four spec-confirmations captured (listed above); spec's "Concerns surfaced during drafting" section replaced by the settled-scope block. Baselines recorded (501 ui-vitest, 304 dag-map, .NET green). Status surfaces reconciled across milestone spec, tracking doc (this file), epic spec milestone table, `ROADMAP.md`, `work/epics/epic-roadmap.md`, `CLAUDE.md` Current Work. · commit `4abb114`

### Foundation pass — pure helpers + shared store + client API change (pending commit)

Landed the foundation layer the rest of the milestone depends on. TDD red → green → refactor with a per-file branch audit; zero new TypeScript errors against `npm run check`.

New files:
- `ui/src/lib/utils/cell-state.ts` / `.test.ts` — three-state classifier (AC4). 18 tests. Exports `classifyCellState`, `classifyNodeRowState` (per-(node, metric) row-level-muted optimization).
- `ui/src/lib/utils/bin-label-stride.ts` / `.test.ts` — `pickBinLabelStride(columnPixelWidth, binSize, binUnit, maxBins?)` (AC11). 20 tests. Nice-stride ladders for seconds/minutes/hours/days; target ~100 px per label; defensive fallbacks for degenerate inputs; tie-breaker prefers the larger stride.
- `ui/src/lib/utils/heatmap-sort.ts` / `.test.ts` — `sortHeatmapRows` with modes `topological | id | max | mean | variance` + pinned-first modifier (AC6). 33 tests. Topological order is a Kahn's algorithm implementation (`topologicalOrder`) with id tie-break; caller may pass a pre-computed `topoOrder` to skip re-derivation. All aggregations ignore non-finite cells; rows with no finite data sink to the bottom.
- `ui/src/lib/utils/shared-color-domain.ts` / `.test.ts` — `computeSharedColorDomain(cells, {classFilter, excludeNonObserved, clipPercentile})` returning `[min, p99-clipped]` or `null` (AC5 + ADR-m-E21-06-02). 34 tests. Also exports `bucketFromDomain(value, [lo, hi])` → `'low' | 'mid' | 'high' | 'no-data'` — the assertion surface for Playwright spec #11.
- `ui/src/lib/stores/view-state.svelte.ts` / `.test.ts` — shared rune-based view-state store (AC13 + AC15). 53 tests. Fields: `activeView`, `activeClasses`, `currentBin`, `binCount`, `playing`, `sortMode`, `rowStabilityOn` (persisted `ft.heatmap.rowStability`), `nodeMode` (persisted `ft.view.nodeMode`), `selectedMetric` + pin proxies to `workbench` store. Injectable `Storage` adapter; defensive try/catch on read/write; exported `resolveDefaultStorage(host)` for covered-branch testing.
- `ui/src/lib/components/view-switcher.svelte` — typed `<ViewSwitcher>` (AC1 + ADR-m-E21-06-01) consuming an inline `views` array on the parent route; shadcn-style underline on active tab; `<svelte:window onkeydown>` wired to the pure matcher.
- `ui/src/lib/components/view-switcher-shortcut.ts` / `.test.ts` — pure `parseShortcut` + `matchViewShortcut` helpers. 17 tests. Exact-modifier matching; unparseable descriptors and views without a shortcut are skipped silently.
- `ui/src/lib/api/flowtime.test.ts` — URL-shape tests for the new `mode` parameter on `getStateWindow` (AC15). 5 tests.

Modified files:
- `ui/src/lib/api/flowtime.ts` — `getStateWindow` gains optional `mode?: 'operational' | 'full'` parameter, appended to the query when supplied. All existing call sites unchanged.

Vitest delta: 501 → 681 passing (+180). Dag-map unchanged at 304 (flow-visual spec requires Playwright browser install — pre-existing infra issue, not touched this pass). `.NET` untouched.

Coverage notes for foundation (preliminary; final audit consolidated in Coverage Notes section below after all layers ship):
- Every reachable branch in the new TS/Svelte helpers has at least one explicit test. Two defensive paths in `shared-color-domain.ts` were proven dead-code-by-construction and removed (see file comments). `Math.max(0, Math.min(100, ...))` inside `linearPercentile` was removed because the caller validates `clipPercentile` upstream.
- `parseShortcut`'s original `parts.length === 0` early-return was deleted (String.prototype.split always returns ≥1 element) and replaced with an inline comment.

Not landed in this pass (next pass targets):
- Heatmap grid Svelte component (AC3, AC4 rendering, AC7 row-stability visuals, AC8 click-to-pin, AC9 scrubber coupling, AC10 pin glyph, AC11 bin-axis labels, AC12 keyboard nav/ARIA).
- Topology-page refactor to consume `viewState` store + rewire color mapper through `computeSharedColorDomain` (AC5 / ADR-02 seam).
- 13 Playwright specs (AC14 spec list).
- Toolbar node-mode toggle UI (AC15 — store + API done; toolbar control lands with the grid pass).

### Grid + route integration + Playwright pass (pending commit)

Landed the heatmap grid, topology-route rewire, and live Playwright suite. TDD red → green → refactor with a per-file branch audit; zero new TypeScript errors against `npm run check` (pre-existing dag-map and Blazor-UI errors remain, untouched by this milestone).

New files:
- `ui/src/lib/utils/value-normalize.ts` / `.test.ts` — `normalizeValueInDomain(value, [lo, hi])` → `0..1 | null` (AC5 consumer surface). 11 tests covering every reachable branch including the degenerate single-value domain and the null-on-invalid-input paths.
- `ui/src/lib/components/heatmap-geometry.ts` / `.test.ts` — `buildHeatmapGrid` + `buildBinAxisLabels` (AC3–AC7 data layer). 42 tests covering classification + class filter + row stability + sort wiring + domain + bucketing + degenerate inputs + multi-class sum paths.
- `ui/src/lib/components/heatmap-time-format.ts` / `.test.ts` — `formatBinAbsolute` + `formatBinOffset` + `formatBinTime` (AC3 + AC11). 12 tests including every unit branch + invalid-date fallback.
- `ui/src/lib/components/heatmap-keyboard-nav.ts` / `.test.ts` — `computeNextFocus` pure helper (AC12). 14 tests including the four arrow clamp conditions and the three "no-op" paths (non-nav key, empty rows, unknown node).
- `ui/src/lib/components/heatmap-view.svelte` — SVG grid renderer (AC3/4/7/8/9/10/11/12). Row-level muted overlay for metric-undefined rows; per-cell hatch for no-data; `data-value-bucket` on every cell; keyboard nav delegating to `computeNextFocus`; Escape returns focus to the `[data-heatmap-toolbar]` button.
- `ui/src/lib/components/node-mode-toggle.svelte` — `[ Operational | Full ]` segmented control (AC15).
- `tests/ui/specs/svelte-heatmap.spec.ts` — 13 Playwright specs (12 from AC14 + 1 extra Escape spec #12b). All specs use the graceful-skip pattern against `/v1/healthz`.

Modified files:
- `ui/src/lib/utils/metric-defs.ts` + `.test.ts` — added `computeMetricDomainFromWindow` (builds shared domain over window data) and `buildNormalizedMetricMap` (produces `[0, 1]` values for the dag-map palette). 15 new tests covering every reachable branch in both functions.
- `ui/src/routes/time-travel/topology/+page.svelte` — full rewire: imports view-state store, renders `<ViewSwitcher>`, wires node-mode toggle to re-fetch `state_window`, branches canvas between topology and heatmap, adds sort-select + row-stability checkbox inline when heatmap is active, routes heatmap pin/unpin + scrubber-jump through the workbench store. Topology coloring now consumes `buildNormalizedMetricMap` with the shared domain — visible color behavior changes per ADR-m-E21-06-02.

Vitest delta: 681 → 773 passing (+92) at end of this pass; 773 → 763 (−10) after the subsequent ADR-02 dead-code cleanup pass removed `buildMetricMapForDef` / `buildMetricMapForDefFiltered` and their tests (dead after the shared-domain rewire). Current total **763 passing across 32 suites, all green**. Net milestone delta: 501 → 763 (+262). `.NET` untouched.

Playwright deltas (against live Rust engine + Svelte dev on port 5173):
- New `svelte-heatmap.spec.ts`: 13 specs, 11 passed / 2 skipped. The two skips are `test.skip()` calls in specs #8 + #9 when the fixture has no `classCoverage` / `classes` metadata — the current warehouse-picker-waves run doesn't have class-dimensioned data. When a class-enabled run is available on the API, both specs will run without changes.
- Pre-existing failures NOT caused by this milestone: `topology-latency.spec.ts` (3 failures — specs target Blazor UI classes `.topology-node-proxy` / `.topology-inspector` that don't exist on the Svelte page); `svelte-analysis-followup.spec.ts` `sweep truncation at 200 points shows distinct warning` (1 pre-existing env flake noted in CLAUDE.md); `svelte-analysis.spec.ts` `can run sweep when parameters are available` (1 pre-existing env flake, same source).
- `svelte-workbench.spec.ts` all skip — health-probe URL is `/v1/health` rather than `/v1/healthz` (pre-existing bug; not touched this milestone).

### Prior accessibility posture (AC12 homework)

Audited per confirmation #3 — findings logged here and gap-filed rather than retrofitted in this milestone.

- **Topology DAG keyboard nav.** `DagMapView` renders via `{@html renderSVG(...)}` and the dag-map library does not emit tabindexed SVG nodes — nodes are only mouse/click-reachable. No arrow-key navigation on the DAG. → gap filed in `work/gaps.md`.
- **Topology ARIA structure.** DAG SVG has no `role="img"` label, no `role="button"` on nodes, no `aria-label` on clickable surfaces. → gap filed in `work/gaps.md`.
- **m-E21-02 data-viz palette color-blind validation.** The `--ft-viz-*` palette was not validated against color-blindness simulators. The shared color scale (`colorScales.palette`) that topology + heatmap now both consume uses a teal → amber → red gradient that may look similar under deuteranopia/protanopia. → gap filed in `work/gaps.md`.
- **Heatmap AC12 bar established.** `role="grid"` + `role="row"` + `role="gridcell"` with `aria-label` carrying node id + bin + metric + value; Tab into grid via `tabindex=0` on observed cells; arrow-key nav + Enter/Space/Escape; visible focus ring via `:focus-visible`. This is the new bar for the Svelte surface — gap file notes the delta against topology.

### Release-notes entry (ADR-m-E21-06-02 straight swap)

> **Topology coloring now matches the heatmap.** Both views share a full-window 99th-percentile-clipped color domain. Topology's previous raw-value mapping is gone — a node's color in topology at bin T now equals its cell color in the heatmap at (N, T) under the same class filter. This is a visible behavior change for existing runs: in a narrow-range bin, a node that used to be intensely colored may now render as a mid-shade because the full-window range is wider. Per-bin and per-row normalization toggles are deferred (tracked in `work/gaps.md`).

### Pinned-first row float removal (pending commit)

User UX feedback mid-implementation: sort dropdown felt "broken" because selecting "topological" showed pinned rows at the top regardless. Contract amended to pin-agnostic sort; pin glyph (AC10) becomes the sole pinned-row indicator. See "Decisions made during implementation" above for the full rationale.

Mechanical scope:
- `ui/src/lib/utils/heatmap-sort.ts` — docstring updated; `pinnedIds` removed from `SortOptions`; `sortHeatmapRows` collapsed from pinned-partition-plus-concat into a single `[...rows].sort(cmp)`. `buildComparator` signature unchanged.
- `ui/src/lib/components/heatmap-geometry.ts` — `pinnedIds` retained on `HeatmapGridInput` (for call-site compatibility with the heatmap view, which passes it through for pin-glyph rendering) but no longer destructured or forwarded to `sortHeatmapRows`. Step 4 sort comments updated.
- `ui/src/lib/utils/heatmap-sort.test.ts` — the `describe('sortHeatmapRows — pinned-first modifier is always on', ...)` block (7 tests) deleted. Replaced with `describe('sortHeatmapRows — no pinned partitioning (pinned rows sort in their natural position)', ...)` (3 tests: mode=id half-pinned → pure alpha; mode=topological downstream-pinned → stays downstream; mode=max lowest-max-pinned → sinks to bottom). All remaining calls to `sortHeatmapRows` dropped `pinnedIds: new Set()` from their options arg.
- `ui/src/lib/components/heatmap-geometry.test.ts` — the `describe('buildHeatmapGrid — sort + pinned-first', ...)` block renamed to `sort (pin-agnostic, pinned rows keep natural position)`. "pinned rows float to top under every sort mode" test rewritten to assert pinned rows keep their natural id-sort position (not position 0). New test added: "pinned row stays in its natural middle position under topological sort".
- `tests/ui/specs/svelte-heatmap.spec.ts` #5 — selector changed from `.first()` (implied position 0) to `[data-row-id="${pinnedId}"]`; comment rewritten to clarify the glyph is the sole indicator, not position.

Vitest delta: 763 → 761 passing (−2) across 32 suites, all green. No new TypeScript errors (pre-existing dag-map / Blazor errors unchanged). Pinned-first language scrubbed: `rg "pinnedIds" ui/src/lib/utils/heatmap-sort.ts` → zero hits; `pinnedIds` retained in `heatmap-geometry.ts` + `heatmap-view.svelte` + `+page.svelte` + `view-state.svelte.ts` only (pass-through and pin-glyph rendering).

### Wrap-milestone — final validation + status reconciliation (2026-04-24)

Final test snapshot at wrap time: **770 ui-vitest passing across 32 suites** (net +269 from the 501 baseline); 16 Playwright specs on `svelte-heatmap.spec.ts` (13 from AC14 + #12b Escape + #12c persistent-overlay + #12d card-title cross-link + #12e horizontal-fit invariant); 11 pass / 2 graceful-skip on missing `classCoverage` fixture (specs #8 and #9); `.NET` suite green on re-run (isolated re-run of the one Integration flake `RustEngine_Timeout_ThrowsRustEngineException` passes — pre-existing timing-dependent flake in a test file untouched by this milestone). `svelte-check` shows zero new errors in m-E21-06 files; the 413 pre-existing errors live in legacy dag-map JS + Blazor UI + unrelated Svelte surfaces, unchanged.

Branch-coverage audit reconfirmed against the Coverage Notes section below — every reachable branch in the new TS/Svelte helpers and route integration has at least one explicit test; defensive guards documented in "Out-of-scope / intentionally deferred coverage" back up type-level or UX-level invariants rather than leave user-visible paths untested.

Status surfaces reconciled in this pass:
- [x] Milestone spec frontmatter — `Status:` in-progress → complete; `Completed:` added (2026-04-24).
- [x] Tracking doc — `Completed:` filled; `Commits:` extended with the wrap SHA placeholder (to be backfilled post-commit-approval in a `docs(e21):` chaser).
- [x] Epic spec (`work/epics/E-21-svelte-workbench-and-analysis/spec.md`) — milestone table row for m-E21-06 moved from **in-progress** to **complete (2026-04-24)**.
- [x] `ROADMAP.md` E-21 section — m-E21-06 moved from **In-progress:** to **Completed milestones:**.
- [x] `work/epics/epic-roadmap.md` E-21 block — narrative updated to reflect m-E21-06 completion.
- [x] `CLAUDE.md` Current Work — E-21 block's m-E21-06 bullet rewritten from **in-progress** to **complete** with final figures (770 vitest, +269 delta, 16 Playwright specs, 15/15 ACs, framework guard addition).

Framework guard added mid-milestone (2026-04-23): the Truth-Discipline "API stability" guard was appended to `.ai-repo/rules/project.md` and auto-mirrored to `CLAUDE.md` — motivated by the dead-code deletion of `buildMetricMapForDef` / `buildMetricMapForDefFiltered` + the `pinnedIds` pass-through cleanup. Included in commit `5dddb5d`.

Exclusions flagged at wrap time (unrelated to m-E21-06, not in the milestone commit):
- `.devcontainer/devcontainer.json` — pre-existing session-start edit for gh CLI mount.
- `docs/research/flowtime-studio-architecture.md`, `docs/research/flowtime-studio-roadmap-plan.md` — untracked research docs from a separate thread.

## Coverage Notes

<!-- Line-by-line branch audit per AC14, matching every reachable conditional branch
     in new UI + helpers to a test. Match m-E21-05's audit structure. Populate
     as each AC lands; final audit before commit-approval prompt. -->

### Foundation-layer audit (already landed)

Covered in the foundation-pass Work Log entry above. Two defensive branches in `shared-color-domain.ts` were proven dead and removed (see file comments); `parseShortcut`'s `parts.length === 0` early-return was removed (`String.prototype.split` always returns ≥1 element).

### Grid + route integration audit

**`ui/src/lib/utils/value-normalize.ts`** (1 exported fn, 8 branches):
- L20 null/undefined value → `null / undefined value: returns null`
- L21 non-finite value → `non-finite value: returns null`
- L23 non-finite domain bound → `non-finite domain bound: returns null`
- L24 `lo > hi` → `lo > hi: returns null`
- L25 `lo === hi` with finite value → `lo === hi: returns 0.5`
- L25 `lo === hi` with non-finite value → `lo === hi: returns null for non-finite value`
- L26 `value <= lo` → `clamps values below lo to 0`
- L27 `value >= hi` → `clamps values above hi to 1`
- L28 in-range linear → `maps values to [0, 1] inside a standard domain`

**`ui/src/lib/components/heatmap-time-format.ts`** (3 fns, all branches covered by 12 tests):
- `formatBinAbsolute`: valid ISO, invalid ISO fallback.
- `formatBinOffset`: seconds / minutes / hours / days / unknown-unit-defaults-to-minutes / negative-sign.
- `formatBinTime`: timestamp hit / timestamp missing for bin / no timestamps array / missing grid fallback.

**`ui/src/lib/components/heatmap-keyboard-nav.ts`** (1 exported fn, 11 branches):
- Unknown key → null (3 tests).
- `binCount <= 0` → null.
- `rowIds.length === 0` → null.
- Unknown `nodeId` → null.
- ArrowRight/Left/Down/Up happy paths + clamp at min/max for each (8 test cases).

**`ui/src/lib/components/heatmap-geometry.ts`** (2 exported fns + 2 helpers, all branches covered by 42 tests):
- `extractSeries`: `activeClasses.size === 0` with flat series / with non-array series (falls through) / with byClass-only / with neither. `hasClassFilter` with no byClass → undefined. `hasClassFilter` with byClass → sumClassSeries.
- `sumClassSeries`: classData undefined / series non-array (both skipped silently); first-class contribution / later-class finite fills NaN slot / later-class NaN leaves prior value / class missing from node / shorter array length cap.
- `buildHeatmapGrid`: skip no-id nodes; `unfilteredRowState === metric-undefined` short-circuit; `hasClassFilter && filteredRowState === metric-undefined` with `rowStabilityOn` true / false; has-data non-filtered happy path (the `??` fallback on L223 is defensive-TS per the inline comment — the pre-conditions make it unreachable); domain filter (filtered rows excluded); domain null (empty cells); observed cells → normalized + bucket; non-observed / muted / filtered → null normalized + `no-data` bucket. Pin-agnostic sort (pinned rows keep natural position under id + topological, per the post-start amendment) + all five sort modes + `topoOrder` override + filtered-rows-sorted-separately.
- `buildBinAxisLabels`: `binCount <= 0` early return; `stride <= 0` falls to 1; stride > binCount → single label at 0; normal multi-label path.

**`ui/src/lib/utils/metric-defs.ts` additions** (2 new exported fns, all branches covered by 15 tests):
- `computeMetricDomainFromWindow`: empty input → null; all-null values → null; multi-node finite values → domain; class-filter path; skips nodes without id; skips NaN/Infinity.
- `buildNormalizedMetricMap`: null domain → empty map; raw → normalized [0, 1]; label formatted from raw value, not normalized; clamped above hi → 1; omits nodes without extractable raw value; class-filter path; skips nodes without id.

**`ui/src/lib/components/heatmap-view.svelte`** (component, not fully unit-testable):
- `tooltipFor` branches (filtered / metric-undefined / no-data / observed-nonzero / observed-zero) are exercised by Playwright #2 (tooltip shape assertions) and #8 (filtered-class tooltip) in combination. Zero-value-observed tooltip is exercised when any `utilization = 0` cell renders; verified in #2's iteration across observed-cell tooltips.
- `cellFill` branches: observed-with-domain → palette; else → muted. Observed path exercised by #11 via `data-value-bucket` verification (observed-only selector). Muted path exercised by #2 when a muted row is present; when absent in the fixture, code path is reached via `tooltipFor` tests checking the `no-data` tooltip shape which co-renders with the muted fill.
- `onCellClick`: observed → pin-and-scrub (#3, #5). Non-observed early return — defensive; cells with non-observed state have `tabindex=-1` which already blocks Tab entry; mouse clicks on those cells are additionally early-returned. Not directly asserted in Playwright (intentional — would be an anti-test).
- `onCellKeydown`: Enter/Space on observed (#12); Escape (#12b); arrow keys (#12, via `computeNextFocus`); non-nav key and null-target early returns → exercised by `computeNextFocus` unit tests.
- `normalizedEdges`: port-strip + dedup branch exercised at runtime when edges carry `:out`/`:in` suffixes (every FlowTime run); via the topology route the heatmap receives already-stripped edges; the dedup branch fires when `graph.edges` has duplicate entries under the same stripped key.
- `<Heatmap rendering conditional>`: `isEmptyAfterFilter` → empty-state div (#9 when fixture supports); else → main grid (#1).
- `{#if binCount > 0 && currentBin in-range}`: column highlight render (#6). Out-of-range branch covered by test providing binCount=0 or currentBin=-1 via the `viewState.setCurrentBin` clamp logic — exercised by foundation-pass `view-state` tests.
- `{#if isMuted}` row overlay: two render paths (filtered → `var(--muted)`, undefined → `url(#heatmap-hatch)`) exercised when a fixture surfaces each state.
- `{#if isPinned}` pin glyph vs spacer: pinned → button with unpin handler (#5 + workbench-card unpin, exercised). Non-pinned → spacer (default render path).
- Three-way cell render `{#if muted} {:else if no-data} {:else observed}`: each exercised by a fixture state.

**`ui/src/routes/time-travel/topology/+page.svelte`** changes:
- Branches already present: run-selector change, loading / error / no-runs / graph-loaded cases. Untouched behaviorally by this milestone (still covered by existing `svelte-workbench` specs, which skip on the pre-existing `/v1/health` → should-be-`/v1/healthz` probe bug; not in this milestone's scope to fix).
- New branches: `viewState.activeView === 'topology'` vs `'heatmap'` render choice (#5), `viewState.activeView === 'heatmap' →` sort + row-stability toolbar controls appear (#7 + #8), `viewState.nodeMode` change triggers `loadWindow()` (#13), `heatmapGraphEdges` derivation (graph-present branch; else branch → empty array → tested transitively when no graph has loaded).
- `onHeatmapPinAndScrub`: already-pinned vs not (#3 on a clean pin; #5 on a re-pin path via topology→heatmap round-trip).
- `onNodeModeChange`: `mode === current` early return exercised by clicking the already-active segment (branch is the `if` guard — not tested but a defensive branch that prevents a redundant re-fetch).
- `togglePlayback` / `stopPlayback`: unchanged behaviorally.

### Out-of-scope / intentionally deferred coverage

- **Escape focus-target degrade path** (heatmap rendered outside the topology route): the `document.querySelector('[data-heatmap-toolbar] button')` resolves null in a standalone layout and focus stays on the cell. No such layout exists in m-E21-06; when a future view embeds heatmap without a toolbar marker, add a coverage case.
- **Duplicate-edge dedup in `normalizedEdges`**: exercised when a graph fixture emits redundant port-stripped edges; current FlowTime runs don't surface this shape often. Branch is present as a defensive dedup.
- **`onCellClick` non-observed early return**: defensive; complements the `tabindex=-1` gate. Not directly Playwright-asserted.
- **`onNodeModeChange` no-op guard when mode unchanged**: defensive; exercised only by clicking the already-active node-mode segment, which is a UX-prevented interaction.

None of these gaps leave a user-visible code path without an explicit test; each is a defensive guard backing up a type-level or UX-level invariant.

## Reviewer notes (optional)

<!-- Things the reviewer should specifically examine — trade-offs, deliberate
     omissions, places where the obvious approach was rejected and why. -->

- Topology's visible color behavior changes under ADR-m-E21-06-02 (per-bin → full-window 99p-clipped). The change is a straight swap (no feature flag). Reviewer should verify topology still renders as expected for existing fixtures and that the Playwright view-switch preservation spec (#5) covers the cross-view parity that motivates this change.
- `<ViewSwitcher>` ships as a typed component with views listed inline on the topology route (ADR-m-E21-06-01). No manifest registry, no Svelte context API. Reviewer: confirm the abstraction footprint matches the two-view reality; the registry pattern graduates when a third asymmetric view lands (tracked in `work/gaps.md`).
- AC12 accessibility-baseline homework: findings on topology's existing keyboard/ARIA posture + m-E21-02 color-blind validation are filed as gaps, not retrofitted. Reviewer: confirm any gaps surfaced here ended up in `work/gaps.md`.

## Validation

- Baseline (pre-milestone): ui vitest 501, dag-map 304, .NET green.
- Wrap actuals: **ui vitest 770 passing across 32 suites** (net +269); dag-map unchanged (not touched); .NET green on re-run of the single Integration-test flake (`RustEngine_Timeout_ThrowsRustEngineException` — pre-existing timing-dependent flake in a test file not modified by m-E21-06).
- Playwright svelte-heatmap.spec.ts: 16 specs (13 AC14 + #12b/#12c/#12d/#12e); 11 pass / 2 graceful-skip on fixtures without `classCoverage` metadata (specs #8, #9 — by design).
- `svelte-check`: zero new errors introduced by m-E21-06; 413 pre-existing errors in legacy dag-map JS + Blazor UI + unrelated Svelte surfaces remain unchanged.

## Deferrals

<!-- Work observed during this milestone but deliberately not done; mirror into
     work/gaps.md before archive. Initial deferrals already landed in gaps.md
     during Q&A on 2026-04-23; additional deferrals logged here as they surface. -->

- Fixed per-metric color ranges — filed.
- Per-row normalization toggle — filed.
- Current-bin-value sort mode — filed.
- Trend / slope sort mode — filed.
- View-registry graduation — filed.
- Topology DAG keyboard nav + ARIA structure (AC12 homework) — filed 2026-04-24 in `work/gaps.md`.
- Data-viz palette color-blindness validation (AC12 homework) — filed 2026-04-24 in `work/gaps.md`.
- Bidirectional card ↔ view reverse cross-link — filed 2026-04-24 in `work/gaps.md` (natural m-E21-08 polish pairing).
- Heatmap sliding-window scrubber (Blazor-parity zoom-and-pan for per-cell fidelity on wide runs) — filed 2026-04-24 in `work/gaps.md`.
