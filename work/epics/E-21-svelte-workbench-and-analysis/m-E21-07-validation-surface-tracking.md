# Validation Surface (Svelte) — Tracking

**Started:** 2026-04-27
**Completed:** pending
**Branch:** `milestone/m-E21-07-validation-surface` (branched from `epic/E-21-svelte-workbench-and-analysis` after the m-E21-06 merge into the epic branch landed 2026-04-26 — epic-branch tip `2621e23`)
**Spec:** `work/epics/E-21-svelte-workbench-and-analysis/m-E21-07-validation-surface.md`
**Commits:** pending

<!-- Status is not carried here. The milestone spec's frontmatter `status:` field is
     canonical. `**Completed:**` is filled iff the spec is `complete`. -->

## Scope Recap

Surface the per-run validation/analytics warnings that the Engine already attaches to every `state_window` response into the Svelte workbench. The work is **consumer-side only** — no new endpoint, no `POST /v1/run` shape change, no run-artifact persistence change. The Svelte client's `StateWarning` type today is a thinner shape than the wire DTO (`severity`, `startBin`, `endBin`, `signal` missing; `edgeWarnings` map not declared) so the data arrives and is silently dropped during deserialization. AC1 widens the type to mirror `TimeTravelStateWarningDto` exactly; AC2-AC10 build the panel + indicators + cross-link on top of that.

The validation panel renders as a **left column inside the existing workbench panel** on `/time-travel/topology` (the bottom region under the splitter — same region that holds pinned cards today). Pinned `WorkbenchCard` / `WorkbenchEdgeCard` items continue to fill the rest of the panel to the right. When the loaded run has zero warnings, the column collapses to zero (or a minimal collapsed-header glyph). Topology nodes + edges gain chrome warning indicators driven from the same shared `state_window` source as the panel. Bidirectional cross-link via the existing `view-state.svelte.ts` store — no new store, no new persisted state on the contract side.

Tier-3 only — tier-1 / tier-2 schema and compile errors reject the run at `POST /v1/run` with `400 { error }` and never reach this surface (matches Blazor).

## Confirmations captured at start (2026-04-27)

The spec called out five items to "settle at start-milestone confirmation." All five were settled by the human at start-handoff before implementation began (same precedent as m-E21-05 / m-E21-06).

1. **Collapsed/expanded panel widths + resize behaviour (AC3) — A.** Zero-width collapsed; 300 px expanded default; **not** user-resizable in this milestone. *Rationale: smallest surface; preserves the clean-run pinned-card layout exactly; resize is m-E21-08 polish if needed.*
2. **Topology edge indicator visual treatment (AC8 + Design Notes) — A.** Sibling glyph at the edge midpoint (small filled dot in chrome token); existing edge stroke unmodified. *Rationale: composes cleanly with `--ft-viz-amber` selected-edge stroke; mirrors m-E21-06 sibling-overlay precedent; trivial midpoint geometry.*
3. **Severity indicator style (Design Notes) — B.** Coloured dot on the topology indicator; dot + word label on panel rows. *Rationale: topology has tight chrome budget; panel rows have full width and benefit from non-colour severity readout; matches Blazor chip convention.*
4. **Playwright "warnings present" fixture mechanism (Design Notes) — D.** Hybrid: option (a) real-bytes fixture as the primary spec for AC1 round-trip / Trigger T1 warnings-present; option (c) `page.route(...)` mocks for the rest of AC14 (edge-case UI behaviour, severity-mix, cross-link). *Rationale: one spec proves bytes flow through the real backend (the AC1 regression); deterministic mocks cover edge-case UI behaviour without coupling tests to analyser-heuristic stability.*
5. **Ship `--ft-info` this milestone, or defer (AC12) — C.** Ship `--ft-info` now; vitest assertion that the severity helper maps `info` → `--ft-info`. *Rationale: type widening at AC1 already declares the `info` literal; chrome surface stays consistent; vitest-only proof avoids hunting for an `info` Playwright fixture this milestone.*

## Baseline test counts (at start)

To be captured by the build during the AC1 preflight (matches the m-E21-06 precedent of capturing actuals as baseline rather than chasing CLAUDE.md history).

| Surface | Count |
|---|---|
| `ui/` vitest | (pending preflight) |
| `lib/dag-map/` node-test | (pending preflight — reference: 304 passing at m-E21-06 wrap) |
| `.NET` build | (pending preflight) |

## Acceptance Criteria

<!-- Mirror ACs from the spec. Check each when its Work Log entry lands. -->

- [ ] **AC1** — Svelte `StateWindowResponse` type widened to match the wire DTO (`severity`, `startBin?`, `endBin?`, `signal?` on `StateWarning`; `edgeWarnings: Record<string, StateWarning[]>` on `StateWindowResponse`; phantom `bins?: number[]` removed). No `getStateWindow(...)` signature change. Vitest fixture-based round-trip parse covers all fields populating.
- [ ] **AC2** — Validation panel surface renders as left column inside the existing workbench panel; merged sorted list (severity → nodeId / edge-id (empty last) → message); chrome severity chip + message + nodeId-or-edge-identity + bin-range chip when applicable; rows without identity render with no pin affordance.
- [ ] **AC3** — Collapse-on-zero behaviour: zero-warnings state collapses the column to **zero width** (per confirmation 1); warnings present expands to the **300 px** default; not user-resizable in this milestone; empty-state string `No validation issues for this run.` for the loading-but-not-yet-collapsed transition.
- [ ] **AC4** — Empty-state vs no-data-state distinguished (no-data-state cannot occur because the wire field is non-optional). Pure helper `classifyValidationState({ warnings, edgeWarnings }): 'empty' | 'issues'`; both branches covered by vitest.
- [ ] **AC5** — Trigger T1 (run completes via existing Svelte run flow → `/time-travel/topology` loads new run → panel renders warnings without additional fetch). Branch covered by Playwright.
- [ ] **AC6** — Trigger T2 (user selects a run from the run-selector dropdown → panel renders warnings from that run's `state_window` response). Branch covered by Playwright.
- [ ] **AC7** — Topology node warning styling: nodes with matching `nodeId` render a chrome warning indicator using new tokens `--ft-warn` / `--ft-err`; severity-max collapse when multiple warnings target the same node.
- [ ] **AC8** — Topology edge warning styling: edges with matching identity in `edgeWarnings` render a **sibling glyph at the edge midpoint** (small filled dot in chrome token, per confirmation 2); existing edge stroke unmodified; visually distinct from the `--ft-viz-amber` selected-edge stroke.
- [ ] **AC9** — Click-a-warning-row: nodeId rows pin the node + set `viewState.setSelectedCell(nodeId, currentBin)`; edge-keyed rows pin the edge via the workbench's edge-pin path; rows with neither identity are no-op clicks. View-independent (Topology / Heatmap kept in sync via shared store).
- [ ] **AC10** — Bidirectional cross-link from selection to warnings panel: selection in workbench (via `viewState.selectedCell` for nodes or edge-pin state for edges) highlights matching warning rows. Filter-vs-highlight pixel behaviour recorded as an implementation note. No new store fields required for the contract.
- [ ] **AC11** — Single source of truth: validation items live in exactly one derived store keyed off the loaded `state_window` response; warnings panel + node styling + edge styling all read from it; switching views does not refetch; selecting a different run does (because the underlying call changes).
- [ ] **AC12** — New chrome tokens `--ft-warn`, `--ft-err`, **and `--ft-info`** added to the chrome-token CSS surface (light + dark themes, no clash with `--ft-pin` / `--ft-highlight` / `--ft-viz-amber` / data-viz palette). Severity helper maps `info` → `--ft-info` (vitest-asserted in Suite 4 per confirmation 5).
- [ ] **AC13** — Tier-3-only scope explicit: no tier-1 / tier-2 UX in this milestone. Restated as AC so the build cannot quietly add a "validation failed" surface (tier-1/2 already surface as `400 { error }` from `POST /v1/run` via the existing run-orchestration flow).
- [ ] **AC14** — Testing: Playwright drives every shipped trigger in a real browser (graceful-skip on dev-server / API unavailability per project rule); vitest covers the listed pure helpers. Line-by-line branch audit recorded in Coverage Notes below.

## Planned test coverage (AC14 reference — not a second tracking list)

<!-- Reference inventory from the spec's AC14 plan. AC14's checkbox above is the
     canonical "done" signal; the items below are ticked when the corresponding
     spec/suite lands. -->

**Vitest pure-logic suites** (branch-covered per hard rule):

- [ ] **Suite 1** — Widened-type round-trip parse (AC1). Fixture-based round-trip asserts every wire field populates the widened types (incl. `severity`, `startBin`, `endBin`, `signal`, `edgeWarnings`).
- [ ] **Suite 2** — `classifyValidationState({ warnings, edgeWarnings }): 'empty' | 'issues'` — both branches plus boundary (empty `warnings` + empty `edgeWarnings` → `empty`; one of either → `issues`).
- [ ] **Suite 3** — `sortValidationItems(items)`: severity desc → nodeId / edge-id (empty / null last) → message; ties at every level (same severity diff identity; same identity diff message; same severity + same identity → message tiebreak; empty identity sorts last regardless of message).
- [ ] **Suite 4** — Severity-to-chrome-token helper: `error` → `--ft-err`, `warning` → `--ft-warn`, **`info` → `--ft-info`** (per confirmation 5), unknown literal → default chrome. Every branch.
- [ ] **Suite 5** — `maxSeverityForKey(items): 'error' | 'warning' | 'info' | null` — every combination including empty / single-`info` / single-`warning` / single-`error` / mixed `warning+info` / mixed `error+warning` / mixed all-three / unknown literal.
- [ ] **Suite 6** — `rowsMatchingSelection(items, selection): Set<string>` — nodeId-match, edge-match, neither-match, no-selection (null), selection set with diff identity (no rows match).

**Playwright specs** (live Rust engine + Svelte dev; graceful-skip on probe; extending `tests/ui/specs/svelte-topology.spec.ts` or a sibling `svelte-validation.spec.ts`, authoring choice at implementation). Per **confirmation 4 (hybrid)**: spec #2 uses a **real-bytes** fixture (option (a)) so AC1's wire-format round-trip is proven end-to-end through the live engine; the rest of AC14 uses **`page.route(...)` mocks** (option (c)) for deterministic UI-behaviour coverage that does not couple to analyser-heuristic stability.

- [ ] #1 — *Real bytes.* Trigger T1 zero-warnings: run a model with no warnings → column collapses → pinned cards fill the full panel width → zero topology warning indicators.
- [ ] #2 — *Real bytes (AC1 round-trip regression).* Trigger T1 warnings-present: run a deliberately-broken fixture YAML in `tests/ui/fixtures/` that `TimeMachineValidator.Analyse` flags (or a `StateQueryService.BuildWarnings` triggering model) → column expands → row lists `nodeId`, `message`, severity chip → corresponding topology node renders a warning indicator with the right severity token. *This is the only spec that must use real bytes; it proves the wire DTO populates the widened types end-to-end.*
- [ ] #3 — *Mocked (`page.route`).* Trigger T2: synthetic `state_window` payload for a selected run → column populates from that response.
- [ ] #4 — *Mocked.* Click a node-attributed warning row → corresponding workbench card pins → workbench-card title cross-highlights (m-E21-06 selected-state convention).
- [ ] #5 — *Mocked.* Click an edge-attributed warning row → corresponding `WorkbenchEdgeCard` pins → topology edge styled per AC8 (sibling midpoint dot).
- [ ] #6 — *Mocked.* Click a topology node that has warnings → warnings panel highlights / filters to its rows (AC10).
- [ ] #7 — *Mocked.* Click a topology edge that has warnings → warnings panel highlights / filters to its rows (AC10).
- [ ] #8 — *Mocked.* Validation panel persists across view switch (Topology → Heatmap → Topology); node + edge indicators re-render correctly when switching back.
- [ ] #9 — *Mocked.* Two distinct severities render with distinct chrome tokens (synthetic payload exercises both `warning` and `error`; `info` chrome is asserted in vitest Suite 4 per confirmation 5).

## Decisions made during implementation

<!-- Decisions that came up mid-work that were NOT pre-locked in the milestone spec. -->

- (none yet)

## Implementation notes

- **Filter-vs-highlight cross-link pixel behaviour (AC10).** Recorded here once chosen during build. Spec leaves this as an implementation note — both readings (filter-only-matching, highlight-and-de-emphasize-others) satisfy the contract "selection → matching warning rows are visually distinguished."
- **Resize-handle behaviour (AC3).** N/A this milestone — confirmation 1 locked the panel as **not user-resizable** (zero collapsed / 300 px expanded). Filed as a m-E21-08 polish candidate if the fixed width proves cramped during build.
- **Selected warning-id field (AC10).** The contract derives the highlight set from existing store fields (`viewState.selectedCell` for nodes; existing pin state for edges). If a `selectedWarningId` field genuinely surfaces as needed during build, it lives in `view-state.svelte.ts` alongside `selectedCell` and is named at that time. Recorded here.

## Coverage Notes

<!-- Line-by-line branch audit of the new UI + helpers against tests, populated at wrap. -->

- (pending — runs before commit-approval prompt per the hard rule)

## Reviewer notes (optional)

- (pending)

## Validation

- (pending — full-suite `dotnet test FlowTime.sln`, `cd ui && pnpm test`, `cd ui && pnpm exec vitest run` for the new suites, Playwright spec run on `tests/ui/specs/svelte-validation.spec.ts` or sibling)

## Deferrals

<!-- Work that was observed during this milestone but deliberately not done.
     Mirror each deferral into work/gaps.md before the milestone archives. -->

- **Heatmap-row warning badges.** The widened type from AC1 carries `edgeWarnings` and this milestone consumes it for topology edge styling and panel rows; the heatmap row gutter does not get warning badges this milestone. Cheap follow-up the type widening enables — picked up in m-E21-08 polish or a dedicated follow-up.
- **Edit-time validation / live `POST /v1/validate`.** No live validation against an in-progress text edit (Svelte UI has no editor today). When the expert-authoring epic lands, the live `/v1/validate` endpoint is already available.
- **Tier-1 / tier-2 error UX.** Schema and compile errors reject the run at `POST /v1/run` with `400 { error }`; inventing a "validation failed before run could start" UX is a separate milestone with its own backend decision.
- **Severity / per-tier filter toggles, code-grouping, node/edge grouping, validation history per run, validation diff between runs, re-run-from-UI button, inline rich-message rendering.** All explicitly out of scope per the spec's *Out of Scope* section.
