# Heatmap View — Tracking

**Started:** 2026-04-23
**Completed:** pending
**Branch:** `milestone/m-E21-06-heatmap-view` (branched from `epic/E-21-svelte-workbench-and-analysis` at commit `b3b4a33`)
**Spec:** `work/epics/E-21-svelte-workbench-and-analysis/m-E21-06-heatmap-view.md`
**Commits:** _pending_

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

- [ ] **AC1** — View switcher renders above the canvas: new `<ViewSwitcher>` component, inline view array on topology route, `Alt+1` / `Alt+2` shortcuts, shadcn underline, initial view = Topology (not persisted across reloads).
- [ ] **AC2** — Heatmap replaces the canvas only: toolbar + scrubber + workbench persist; splitter / pins / scrubber preserved across view switches both directions.
- [ ] **AC3** — Heatmap grid renders correct `(N rows × B cols)` dimensions; row labels on left in active sort order; sparse bin-axis labels via `pickBinLabelStride`; absolute-time labels when `RunGrid.startTime` set, offset-from-start otherwise; tooltip shows both bin index + time.
- [ ] **AC4** — Three cell states with disambiguating tooltips: observed (including 0) / no-data-for-bin (hatched) / metric-undefined-for-node (row-level muted); classifier keys are per-(node, metric); non-observed cell click is a no-op.
- [ ] **AC5** — Shared full-window 99p-clipped color-scale normalization via `computeSharedColorDomain`; topology's per-bin normalization is replaced (straight swap, per ADR-m-E21-06-02).
- [ ] **AC6** — Row sort modes: topological (default) / node id / max desc / mean desc / variance desc; pinned-first modifier always on; deterministic tie-break by node id; sort mode preserved across metric switches.
- [ ] **AC7** — Class filter parity with topology (hide by default) + row-stability toggle ("Keep filtered rows", off by default, persisted in `localStorage` as `ft.heatmap.rowStability`); empty-state message when filter collapses grid to zero rows; pin state persists across filter toggle; composes with AC15 node-mode toggle.
- [ ] **AC8** — Click-an-observed-cell atomically pins node in workbench AND moves scrubber to that bin; non-observed cell click is no-op; unpin is via pin-glyph or workbench card close button.
- [ ] **AC9** — Scrubber-to-column highlight (two-way coupling): single pre-rendered overlay `<rect>` whose `x` updates on `currentBin` change; chrome color token (not data-viz); real-time during scrubber drag.
- [ ] **AC10** — Pinned-row glyph in the label gutter (Lucide icon, keyboard-focusable `<button>`, `aria-label="Unpin <id>"`); click / Enter / Space unpins. Combined with AC6 positional float.
- [ ] **AC11** — Bin-axis labels with auto-chosen stride targeting ~8–12 visible labels; sensible round units per `binSize × binUnit`; tooltip always shows both bin index + time (`Bin 42 · +03:30` or `Bin 42 · 12:30`).
- [ ] **AC12** — Accessibility baseline: keyboard nav (Tab enters grid, arrow keys move focus, Enter/Space pins + jumps, Escape exits); ARIA `role="grid"` + `role="row"` + `role="gridcell"` with `aria-label`; visible focus ring; tooltip-on-focus; one Playwright keyboard spec. Pattern encoding / high-contrast / SR polish deferred to m-E21-08.
- [ ] **AC13** — Shared view-state store at `ui/src/lib/stores/view-state.svelte.ts` holding `selectedMetric`, `activeClasses`, `currentBin`, `binCount`, `playing`, `pinnedNodes`/`pinnedEdges` (proxied to workbench store), `activeView`, `sortMode`, `rowStabilityOn`, `nodeMode`. Topology route refactored to consume it (no breaking changes to `workbench.svelte.ts` public surface).
- [ ] **AC14** — Testing: 13 Playwright critical-path specs + 5 vitest pure-logic suites, every reachable branch covered (line-by-line audit recorded below in Coverage Notes).
- [ ] **AC15** — Node-mode toggle `[ Operational | Full ]` in shared toolbar; state in `nodeMode` on view-state store; drives `mode` query param on `getStateWindow`; re-fetches on toggle change; persists in `localStorage` as `ft.view.nodeMode`; applies to topology and heatmap (shared-state parity asserted by Playwright spec #13); parity with Blazor UI's pre-existing operational-nodes toggle.

## Test subjects

**Playwright specs** (live Rust engine + Svelte dev; graceful-skip on probe; under `tests/ui/specs/svelte-heatmap.spec.ts`, authoring-choice at implementation):

- [ ] #1 — Loads a run; grid renders expected `(N × B)` dimensions.
- [ ] #2 — Three cell states render with correct disambiguating tooltips on a fixture known to exercise each.
- [ ] #3 — Click-an-observed-cell → pin + scrubber-jump (atomic single-click effect).
- [ ] #4 — Metric switch recolors cells AND reorders rows under max-desc sort.
- [ ] #5 — View-switch state preservation: pin on topology → heatmap shows pinned glyph + floated row → switch back → still pinned.
- [ ] #6 — Scrubber drag moves the column highlight in real time; clicking a cell jumps scrubber and highlight follows.
- [ ] #7 — Sort modes reorder rows (topological → node-id → max-desc → mean-desc).
- [ ] #8 — Class filter default = hide; row-stability toggle ON = dimmed rows at bottom; toggle OFF = hidden again.
- [ ] #9 — Empty-state message when class filter collapses grid to zero rows.
- [ ] (#10 is the graceful-skip requirement, not a standalone test.)
- [ ] #11 — Correctness via `data-value-bucket` (`low` / `mid` / `high`); bucket assertion only, no raw CSS-color parsing.
- [ ] #12 — Keyboard nav critical path (Tab → arrows → Enter → same observable effect as #3).
- [ ] #13 — Node-mode toggle: default `operational` shows N rows; `full` grows row count to include `expr`/`const`/`pmf`; computed-node rows row-level-muted under `utilization`; toggle back shrinks. Topology's visible node count = heatmap row count under each toggle state (shared-state parity).

**Vitest pure-logic suites** (branch-covered per hard rule):

- [ ] **Suite 1** — `heatmap-sort.ts` comparators: topological / id / max / mean / variance + pinned-first. Exercises empty / single / all-equal / pinned-mix / topological-sibling tie-break.
- [ ] **Suite 2** — `shared-color-domain.ts`: full-window 99p-clipped, excluding no-data / undefined / filtered. Exercises all-observed / mix / all-missing (domain empty) / single-value / 99p edge cases / class-filter exclusion.
- [ ] **Suite 3** — `cell-state.ts` classifier: observed (incl. 0) / no-data / row-level-muted. Exercises undefined-all-bins (row-level) / undefined-some-bins (per-cell) / zero-value (observed).
- [ ] **Suite 4** — `bin-label-stride.ts`: `pickBinLabelStride(columnPixelWidth, binSize, binUnit)`. Exercises 5-min wide → hourly / 5-min narrow → larger / hourly → daily / degenerate (0 cols, 1 bin, huge binSize).
- [ ] **Suite 5** — `view-state.svelte.test.ts`: metric set/get, class filter add/remove/clear, scrubber set/move, pin/unpin proxies, row-stability toggle persist, sort-mode set, active-view switch, node-mode toggle + localStorage persist.

## Decisions made during implementation

<!-- Decisions that came up mid-work that were NOT pre-locked in the milestone spec. -->

- (none yet)

## Work Log

<!-- One entry per AC (preferred) or per meaningful unit of work.
     Header: "AC<N> — <short title>" or "<short title>" if not AC-scoped.
     First line: one-line outcome · commit <SHA> · tests <N/M>
     Optional prose paragraph for non-obvious context. Append-only. -->

### Start-milestone — status reconciliation

Created branch `milestone/m-E21-06-heatmap-view` from `epic/E-21-svelte-workbench-and-analysis` at `b3b4a33`. Four spec-confirmations captured (listed above); spec's "Concerns surfaced during drafting" section replaced by the settled-scope block. Baselines recorded (501 ui-vitest, 304 dag-map, .NET green). Status surfaces reconciled across milestone spec, tracking doc (this file), epic spec milestone table, `ROADMAP.md`, `work/epics/epic-roadmap.md`, `CLAUDE.md` Current Work. · commit _pending (start commit)_

## Coverage Notes

<!-- Line-by-line branch audit per AC14, matching every reachable conditional branch
     in new UI + helpers to a test. Match m-E21-05's audit structure. Populate
     as each AC lands; final audit before commit-approval prompt. -->

- (to be filled during implementation)

## Reviewer notes (optional)

<!-- Things the reviewer should specifically examine — trade-offs, deliberate
     omissions, places where the obvious approach was rejected and why. -->

- Topology's visible color behavior changes under ADR-m-E21-06-02 (per-bin → full-window 99p-clipped). The change is a straight swap (no feature flag). Reviewer should verify topology still renders as expected for existing fixtures and that the Playwright view-switch preservation spec (#5) covers the cross-view parity that motivates this change.
- `<ViewSwitcher>` ships as a typed component with views listed inline on the topology route (ADR-m-E21-06-01). No manifest registry, no Svelte context API. Reviewer: confirm the abstraction footprint matches the two-view reality; the registry pattern graduates when a third asymmetric view lands (tracked in `work/gaps.md`).
- AC12 accessibility-baseline homework: findings on topology's existing keyboard/ARIA posture + m-E21-02 color-blind validation are filed as gaps, not retrofitted. Reviewer: confirm any gaps surfaced here ended up in `work/gaps.md`.

## Validation

- Baseline (pre-milestone): ui vitest 501, dag-map 304, .NET green.
- Wrap-milestone targets: ui vitest ≥ 501 + new suites; dag-map unchanged (302 expected — we don't touch dag-map this milestone); .NET unchanged (no backend work).
- All 13 Playwright specs green against the live Rust engine + Svelte dev server; graceful-skip verified.

## Deferrals

<!-- Work observed during this milestone but deliberately not done; mirror into
     work/gaps.md before archive. Initial deferrals already landed in gaps.md
     during Q&A on 2026-04-23; additional deferrals logged here as they surface. -->

- Fixed per-metric color ranges — filed.
- Per-row normalization toggle — filed.
- Current-bin-value sort mode — filed.
- Trend / slope sort mode — filed.
- View-registry graduation — filed.
- (additional deferrals to be appended as they surface during implementation)
