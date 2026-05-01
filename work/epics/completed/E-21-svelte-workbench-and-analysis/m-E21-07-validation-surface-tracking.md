# Validation Surface (Svelte) — Tracking

**Started:** 2026-04-27
**Completed:** 2026-04-28
**Branch:** `milestone/m-E21-07-validation-surface` (branched from `epic/E-21-svelte-workbench-and-analysis` after the m-E21-06 merge into the epic branch landed 2026-04-26 — epic-branch tip `2621e23`)
**Spec:** `work/epics/E-21-svelte-workbench-and-analysis/m-E21-07-validation-surface.md`
**Commits:**
- `5c53965` chore(e21): start m-E21-07 validation surface
- `3cf60a3` feat(ui): widen state_window types to wire DTO (m-E21-07 AC1)
- `5210f5b` feat(ui): add validation pure-helpers (m-E21-07 AC4 helpers)
- `382add5` feat(ui): validation store + panel + chrome tokens (m-E21-07 AC2/AC3/AC11/AC12)
- `2ca77a8` feat(ui): topology node + edge warning indicators (m-E21-07 AC7/AC8)
- `6f68cf7` feat(ui): card severity dot + warning-row re-click fix (m-E21-07)
- `89d10c0` test(ui): validation surface playwright spec + test-runs isolation (m-E21-07 AC14)
- `bfe5951` fix(ui): disambiguate run-selector dropdown options
- `4ab3f63` feat(ui): edge-identity translation, severity row tinting, card interactions (m-E21-07 smoke-test polish)

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

- [x] **AC1** — Svelte `StateWindowResponse` type widened to match the wire DTO (`severity`, `startBin?`, `endBin?`, `signal?` on `StateWarning`; `edgeWarnings: Record<string, StateWarning[]>` on `StateWindowResponse`; phantom `bins?: number[]` removed). No `getStateWindow(...)` signature change. Vitest fixture-based round-trip parse covers all fields populating.
- [x] **AC2** — Validation panel surface renders as left column inside the existing workbench panel; merged sorted list (severity → nodeId / edge-id (empty last) → message); chrome severity chip + message + nodeId-or-edge-identity + bin-range chip when applicable; rows without identity render with no pin affordance.
- [x] **AC3** — Collapse-on-zero behaviour: zero-warnings state collapses the column to **zero width** (per confirmation 1); warnings present expands to the **300 px** default; not user-resizable in this milestone; empty-state string `No validation issues for this run.` for the loading-but-not-yet-collapsed transition.
- [x] **AC4** — Empty-state vs no-data-state distinguished (no-data-state cannot occur because the wire field is non-optional). Pure helper `classifyValidationState({ warnings, edgeWarnings }): 'empty' | 'issues'`; both branches covered by vitest.
- [x] **AC5** — Trigger T1 (run completes via existing Svelte run flow → `/time-travel/topology` loads new run → panel renders warnings without additional fetch). Branch covered by Playwright.
- [x] **AC6** — Trigger T2 (user selects a run from the run-selector dropdown → panel renders warnings from that run's `state_window` response). Branch covered by Playwright.
- [x] **AC7** — Topology node warning styling: nodes with matching `nodeId` render a chrome warning indicator using new tokens `--ft-warn` / `--ft-err`; severity-max collapse when multiple warnings target the same node.
- [x] **AC8** — Topology edge warning styling: edges with matching identity in `edgeWarnings` render a **sibling glyph at the edge midpoint** (small filled dot in chrome token, per confirmation 2); existing edge stroke unmodified; visually distinct from the `--ft-viz-amber` selected-edge stroke.
- [x] **AC9** — Click-a-warning-row: nodeId rows pin the node + set `viewState.setSelectedCell(nodeId, currentBin)`; edge-keyed rows pin the edge via the workbench's edge-pin path; rows with neither identity are no-op clicks. View-independent (Topology / Heatmap kept in sync via shared store).
- [x] **AC10** — Bidirectional cross-link from selection to warnings panel: selection in workbench (via `viewState.selectedCell` for nodes or edge-pin state for edges) highlights matching warning rows. Filter-vs-highlight pixel behaviour recorded as an implementation note. No new store fields required for the contract.
- [x] **AC11** — Single source of truth: validation items live in exactly one derived store keyed off the loaded `state_window` response; warnings panel + node styling + edge styling all read from it; switching views does not refetch; selecting a different run does (because the underlying call changes).
- [x] **AC12** — New chrome tokens `--ft-warn`, `--ft-err`, **and `--ft-info`** added to the chrome-token CSS surface (light + dark themes, no clash with `--ft-pin` / `--ft-highlight` / `--ft-viz-amber` / data-viz palette). Severity helper maps `info` → `--ft-info` (vitest-asserted in Suite 4 per confirmation 5).
- [x] **AC13** — Tier-3-only scope explicit: no tier-1 / tier-2 UX in this milestone. Restated as AC so the build cannot quietly add a "validation failed" surface (tier-1/2 already surface as `400 { error }` from `POST /v1/run` via the existing run-orchestration flow).
- [x] **AC14** — Testing: Playwright drives every shipped trigger in a real browser (graceful-skip on dev-server / API unavailability per project rule); vitest covers the listed pure helpers. 9 specs in `tests/ui/specs/svelte-validation.spec.ts` — 9/9 passing against live infra (verified 2026-04-27). Line-by-line branch audit recorded in Coverage Notes below.

## Planned test coverage (AC14 reference — not a second tracking list)

<!-- Reference inventory from the spec's AC14 plan. AC14's checkbox above is the
     canonical "done" signal; the items below are ticked when the corresponding
     spec/suite lands. -->

**Vitest pure-logic suites** (branch-covered per hard rule):

- [x] **Suite 1** — Widened-type round-trip parse (AC1). Fixture-based round-trip asserts every wire field populates the widened types (incl. `severity`, `startBin`, `endBin`, `signal`, `edgeWarnings`).
- [x] **Suite 2** — `classifyValidationState({ warnings, edgeWarnings }): 'empty' | 'issues'` — both branches plus boundary (empty `warnings` + empty `edgeWarnings` → `empty`; one of either → `issues`).
- [x] **Suite 3** — `sortValidationItems(items)`: severity desc → nodeId / edge-id (empty / null last) → message; ties at every level (same severity diff identity; same identity diff message; same severity + same identity → message tiebreak; empty identity sorts last regardless of message).
- [x] **Suite 4** — Severity-to-chrome-token helper: `error` → `--ft-err`, `warning` → `--ft-warn`, **`info` → `--ft-info`** (per confirmation 5), unknown literal → default chrome. Every branch.
- [x] **Suite 5** — `maxSeverityForKey(items): 'error' | 'warning' | 'info' | null` — every combination including empty / single-`info` / single-`warning` / single-`error` / mixed `warning+info` / mixed `error+warning` / mixed all-three / unknown literal.
- [x] **Suite 6** — `rowsMatchingSelection(items, selection): Set<string>` — nodeId-match, edge-match, neither-match, no-selection (null), selection set with diff identity (no rows match).

**Playwright specs** (live Rust engine + Svelte dev; graceful-skip on probe; extending `tests/ui/specs/svelte-topology.spec.ts` or a sibling `svelte-validation.spec.ts`, authoring choice at implementation). Per **confirmation 4 (hybrid)**: spec #2 uses a **real-bytes** fixture (option (a)) so AC1's wire-format round-trip is proven end-to-end through the live engine; the rest of AC14 uses **`page.route(...)` mocks** (option (c)) for deterministic UI-behaviour coverage that does not couple to analyser-heuristic stability.

- [x] #1 — *Mocked.* Trigger T1 zero-warnings: `page.route(...)` mock returns empty warnings → panel does not mount (collapse-on-zero) → pinned cards fill the full panel width → zero topology warning indicators. *(spec #1 was originally classed "real bytes" in the inventory above, but the deterministic-mock variant is sufficient for the zero-warnings branch — only spec #2 actually needs real bytes, and the mock proves the AC3 collapse contract more reliably than relying on a fixture YAML producing zero warnings.)*
- [x] #2 — *Real bytes (AC1 round-trip regression).* Trigger T1 warnings-present: spec POSTs an inline lag-trigger YAML (two-node topology, single edge with `lag: 2`) directly to `POST /v1/run`. The analyser flags `edge_behavior_violation_lag` (severity `warning`, `nodeId: SourceNode`) — see `InvariantAnalyzer.cs` "edge defines lag" branch and `tests/FlowTime.Core.Tests/Analysis/InvariantAnalyzerTests.cs:Analyze_WarnsWhenEdgeDefinesLag`. Spec asserts the panel mounts, at least one row is visible, the row carries `data-row-kind` (`node` or `edge`), the message matches `/lag/i`, and the severity chip says "warning". *This is the only spec that uses real bytes; it proves the wire DTO populates the widened types end-to-end through the live engine.* On heuristic drift the spec fails loudly — no graceful skip; the contract is "fix the YAML, do not mute the test."
- [x] #3 — *Mocked (`page.route`).* Trigger T2: synthetic `state_window` payload (`queue_depth_mismatch` warning on `HubQueue`) → panel populates with the row, asserts `data-row-kind="node"`, `data-row-key="HubQueue"`, message text "Queue depth".
- [x] #4 — *Mocked.* Reads the auto-loaded run's first `data-node-id`, mocks a warning for it, unpins any auto-pinned cards, clicks the warning row → corresponding workbench card pins (`button[aria-label="Unpin <id>"]` visible) → workbench-card title `span[data-selected="true"]` carries the matching id (m-E21-06 cross-link convention).
- [x] #5 — *Mocked.* Reads the first edge `from`/`to`, mocks an edge warning keyed `${from}→${to}`, asserts: edge row visible with `data-row-kind="edge"`, topology edge has `[data-warning-indicator="edge"][data-warning-edge-id=...]` with `data-warning-severity="warning"`, clicking the edge row pins the workbench-edge-card. (Skips on fixtures with zero edges.)
- [x] #6 — *Mocked.* Mocks a warning on the auto-loaded run's first node + a separate warning on a non-existent node, clicks the topology node → matching row carries `data-row-match="true"`, total rows > matched rows (proving non-matching rows do NOT carry the attribute, so the highlight is differentiated, not blanket).
- [x] #7 — *Mocked.* Mocks an edge warning + a separate non-matching node warning, clicks the topology edge hit-area with `force: true` (the hit-path has `stroke="transparent"` so Playwright's visibility check otherwise fails), asserts the matching edge row carries `data-row-match="true"`.
- [x] #8 — *Mocked.* Mocks a node warning, asserts panel + indicator visible on Topology, switches to Heatmap → panel + matching row stay visible (workbench panel persists across view switches per m-E21-06 AC2), switches back to Topology → indicator re-renders (`$effect` re-applies after the dag-map re-mount per `+page.svelte:175-285`).
- [x] #9 — *Mocked.* Mocks one `error`-severity + one `warning`-severity row on (potentially) different nodes; reads each row's severity-dot inline `style` attribute and asserts the strings reference `--ft-err` and `--ft-warn` respectively (and are distinct). Asserting the inline-style token name is more robust than computed colour comparison (which is browser-flaky); chrome wiring is what the spec proves. (`info` chrome is asserted in vitest Suite 4 per confirmation 5; this spec exercises the two heaviest tokens end-to-end.)

## Decisions made during implementation

<!-- Decisions that came up mid-work that were NOT pre-locked in the milestone spec. -->

- **Filter-vs-highlight cross-link pixel behaviour (AC10) — highlight-and-de-emphasize-others.** Selected rows render with a turquoise border (`--ft-highlight`); non-matching rows render at `opacity: 50%`. Picked over filter-only-matching because it preserves the user's mental model of the full warning list while still making the matching rows obvious. Less destructive — clearing the selection restores the full visible list without re-rendering everything.
- **No store lift required.** The route's existing `loadWindow()` already deserializes the full `StateWindowResponse`; routing the same response into `validation.setResponse(value)` after the existing `windowNodes = ...` assignment is a one-line addition. No state lifted out of `+page.svelte`. The `state_window` response remains route-local for `windowNodes` / `timestampsUtc`; the validation store only needs the `warnings` / `edgeWarnings` slice and gets it via the same response object.
- **Edge id click handling — opaque-key parse with `→` fallback.** `edgeWarnings` keys are opaque strings the analyser persisted in `RunWarning.EdgeIds` — they may or may not match the workbench's `from→to` convention. The panel parses against `→` as the separator (matches `workbench.svelte.ts:28` `selectedEdgeKeys` format); if the format doesn't match, the click is a no-op rather than pinning a malformed edge. Recorded so the next chunk knows: if real fixtures show a different separator, add a multi-format parse path.
- **Cross-link edge selection — last-pinned wins.** `viewState.selectedCell` carries node selection; for edge selection the panel reads `workbench.pinnedEdges` and uses the most-recently-pinned edge's `from→to` as the selection. Mirrors workbench's "most recently interacted" semantics. No new field on `view-state.svelte.ts` needed for the contract.
- **Loading-transition empty-state string scope.** The string `No validation issues for this run.` only renders when `(loading === true && selectedRunId !== undefined)` — i.e., during the load transition between selecting a run and the `state_window` response landing. Once the load resolves with zero warnings, the parent wrapper unmounts the column entirely (collapse-on-zero per AC3). The string never shows in the steady-state collapsed surface — the column is simply absent.
- **Chrome token colour values (AC12).** `--ft-warn` light `hsl(35 95% 45%)` / dark `hsl(35 90% 60%)` (saturated amber, more chroma than `--ft-viz-amber`); `--ft-err` light `hsl(0 75% 45%)` / dark `hsl(0 70% 62%)` (deep red, distinct from `--ft-pin`'s pinker `hsl(355 65% 48%)`); `--ft-info` light `hsl(210 80% 50%)` / dark `hsl(210 80% 65%)` (clear blue, distinct from `--ft-viz-blue`'s muted `hsl(220 50% 45%)`). All three pass the chrome-vs-data-viz separation test by inspection; visual validation deferred to Playwright fixture in next chunk.
- **Topology node indicator position (AC7).** Dot center placed on the NE diagonal at the node circle's tangent: `(cx + r * 0.71, cy - r * 0.71)`, dot radius `r * 0.55`. Top-right keeps the dot clear of both the metric label (rendered above the node at `pos.y - r - 2*s`) and the node label (below at `pos.y + r + 8*s`). Selection ring (when pinned) sits at radius `r + 3*s`; the warning dot is appended to the node group AFTER the ring so z-order keeps the dot on top. A warning-flagged node that's also pinned shows both: the selection ring around the perimeter plus the warning dot at the NE shoulder. Indicator radius is proportional to node radius (not a fixed pixel size) so dots stay readable across dag-map's depth/interchange/scale node-size variants. Position math is captured in pure helper `nodeIndicatorPosition(...)` in `ui/src/lib/utils/topology-indicators.ts` with vitest coverage.
- **Topology edge indicator midpoint computation (AC8).** Used `path.getPointAtLength(path.getTotalLength() / 2)` directly on the rendered `<path data-edge-from data-edge-to>`. Dag-map renders edges as Bezier paths (per `lib/dag-map/src/render.js:208`), so analytic midpoint formulas don't apply — `getPointAtLength` is the canonical SVG geometry call. The DOM-side call lives in the imperative `$effect`; the dot-size convention is captured in pure helper `edgeIndicatorPosition(...)` (radius `3` user-space units, fixed regardless of stroke thickness — stroke-relative sizing would shrink to invisibility on thin edges and balloon on thick trunk routes). Feature-detect `getTotalLength` / `getPointAtLength` rather than assume; skip the indicator silently if not callable. Skip on `totalLength <= 0` (degenerate / not-yet-laid-out path). Edge dots are appended to the rendered SVG root (not the path element — paths cannot host SVG children) via `path.ownerSVGElement.appendChild(...)`.
- **Effect re-run tracking (AC7 + AC8 + AC11).** The imperative `$effect` that injects indicators tracks `validation.nodeSeverityById`, `validation.edgeSeverityById`, `currentMetrics`, `selectedIds`, `viewState.activeView`, and `dagContainer`. The dag-map SVG re-renders on `currentMetrics` change (new bin loaded) and on `selectedIds` change (selection ring redraw); tracking those re-derivations means indicators get re-applied to the new DOM after every dag-map render. View-switch tracking lets the effect skip cleanly while heatmap is active and reapply when the user switches back to topology. The cleanup-then-apply pattern (remove all `[data-warning-indicator]` then re-create) matches the existing edge-selected effect's idempotent style.
- **Indicator markup ownership.** New attribute `data-warning-indicator` (`'node'` or `'edge'`) is the seam — the effect owns these elements exclusively, query-and-removes them at the start of each pass, then re-creates. Sibling attributes `data-warning-node-id` / `data-warning-edge-id` carry the source identity so Playwright can assert the right indicator landed on the right node/edge. `data-warning-severity` carries the resolved severity literal so Playwright can verify severity-token swap (`error` vs `warning` vs `info`) without needing computed-style introspection.
- **Pinned workbench cards (node + edge) carry a severity dot in the card title when the pinned node/edge has at least one warning. Reads `validation.nodeSeverityById` / `edgeSeverityById`; same chrome tokens; same severity-max collapse. Composes with the m-E21-06 turquoise selected-state title cross-highlight (both can render together).** New `warningSeverity?: 'error' | 'warning' | 'info'` prop on both card components; route wires it via `pickWarningSeverity(map, id)` (new pure helper in `validation-helpers.ts`). Edge-card lookup uses the workbench's `${edge.from}→${edge.to}` key (matches the panel's edge-id parse convention from earlier in this milestone). Dot is rendered as a sibling element before the title text — symbol stays inline with the turquoise `selected`-state colour rather than overlapping it. Same `size-1.5` density as the validation-panel-row chip dot from `382add5`. Markers `data-warning-dot="node|edge"` and `data-warning-severity` for future Playwright assertion.

- **Validation-panel re-click bug fix (2026-04-27, smoke-test surface).** Smoke-testing the workbench-card severity dot exposed a regression in the AC9 click handler: clicking a previously-clicked warning row left the selection stranded on the most-recently-clicked row (e.g. row A → row B → row A again ⇒ A pinned, but selection still on B). Root cause was an `if (!wasPinned)` guard around `setSelectedCell` — when re-clicking A, `wasPinned === true` so the helper skipped the selection set. (The user originally reported "node A unpins, card A disappears", which the spec text encouraged by claiming `viewState.pinNode` "delegates to `workbench.toggle`" — actually it delegates to `workbench.pin` (idempotent), so A never actually unpinned. The real symptom was selection stranded on B.)
  - **Fix scope.** Re-clicking a warning row must be **ensure-pinned + always-set-selection**, never destructive. Unpinning lives only on the workbench card's close button. Topology-click toggle semantics at `+page.svelte:106-119` are explicitly preserved (clicking the same node twice in topology should still unpin — distinct interaction, distinct contract).
  - **Approach (a) over (b) for nodes.** `workbench.pin` is already idempotent ensure-pinned semantics, and `viewState.pinNode` already proxies to it (not to `toggle`). Adding an `ensurePinned` synonym would have been pure API surface inflation. The fix is just "remove the `if (!wasPinned)` guard, always set selection."
  - **Approach (b) addition for edges.** Edges genuinely need a new operation: clicking an already-pinned edge row needs to **promote it to last-pinned** so the cross-link's "last-pinned wins" convention focuses on it. Added `workbench.bringEdgeToFront(from, to)` — atomic ensure-pinned + move-to-end. Distinct from `pinEdge` (idempotent append-if-absent) and `toggleEdge` (true toggle); kept as a separate method so existing callers of those two surfaces stay unchanged.
  - **Click logic refactored to a pure helper.** The validation-panel `onRowClick` body moved into `handleValidationRowClick(row, deps): void` in `validation-helpers.ts` with an injectable `ValidationRowClickDeps` interface (`pin`, `bringEdgeToFront`, `setSelectedCell`, `currentBin`). Rationale: there's no svelte-component testing infrastructure in this repo (per `package.json`, no `@testing-library/svelte`); refactoring the click logic into a pure helper means every branch is vitest-coverable — the same hard rule that pre-existing pure helpers in this milestone follow. The component wrapper is a 6-line dep-adapter.
  - **Same-row-clicked-twice question.** Resolved as: re-clicking the currently-selected warning is a no-op selection-wise (sets the same `selectedCell` value; the runtime decides whether to ignore equal-value sets). It does NOT toggle the selection off and does NOT unpin. The user uses the card's close button to unpin and clicks elsewhere (or nothing) to clear selection. Captured in the helper's branch tests; matches the user's stated read at fix-handoff.
  - **Test coverage added (+19 vitest).** `validation-helpers.test.ts` gains a `handleValidationRowClick` describe block with 14 tests covering: node-with-key path, node-with-null-key no-op, three-step bug repro for nodes (A → B → A), same-row-twice case, currentBin pass-through, edge-with-arrow path, three-step bug repro for edges, edge-no-arrow no-op, edge-leading-arrow no-op, edge-trailing-arrow no-op, edge multi-character ids, edge-null-key no-op, node-click doesn't touch edge state, edge-click doesn't touch node-selection state. `workbench.svelte.test.ts` gains 5 tests for `bringEdgeToFront`: append-when-absent, move-to-end-when-present, length-preserved-on-promote, single-edge-idempotent, direction-distinct ((a,b) vs (b,a)).
  - **Topology-click invariant.** Not modified — the `bindEvents` handler at `+page.svelte:113-132` still uses `workbench.toggle` (true toggle) and `workbench.toggleEdge`. The `onRowClick` regression coverage explicitly asserts node-row clicks don't touch edge state and vice versa, so a future edit can't silently drift the validation-panel handler into using a shared toggle path.

- **Edge-identity wire-to-UI translation (2026-04-27, smoke-test surface).** Real-bytes manual smoke-test of the lag YAML exposed a second regression: edge warning indicators didn't render on the topology even though the panel showed an edge row. Root cause was a three-surface edge-identity inconsistency: backend `BuildEdgeWarnings` keys `edgeWarnings` by the analyser's persisted edge id (`source_to_target` for the lag YAML — that's the YAML's `edges[].id` field, not a `from→to` string); the validation store kept keys raw; the topology effect (`+page.svelte:250-255`) iterated keys expecting `→`, split on it, and `continue`-skipped any key without the arrow. The mocked Playwright edge spec (#5) used hand-rolled `from→to` keys in its mock payload so it stayed green; spec #2 (real-bytes) only asserted node indicators, missing the gap. Workbench-edge-card severity-dot lookup at `+page.svelte:761-762` had the same mismatch — the user hadn't surfaced it because they hadn't reached the pin path on the lag run yet.
  - **Fix scope.** Translate at ingestion in the validation store (option (a) over option (b) "look up at render time in each consumer"). Cleaner separation: the store owns the wire-to-UI translation; downstream consumers (topology effect, panel rows, workbench-edge-card lookup, severity-max maps) all see consistent `from→to` keys. Option (b) would have required three lookup sites each doing their own translation — distributed responsibility, easy to drift.
  - **Approach.** `setResponse(response | null)` extended to `setResponse(response | null, edges?: EdgeMetadata[] | null)`. New `EdgeMetadata` interface mirrors `GraphResponse.edges[]` (`id?`, `from`, `to`). When `edges` is provided, `deriveValidationData` builds an `analyserId → from→to` map (keys both `edge.id` AND `${from}→${to}` itself, so already-translated mocked-spec keys round-trip unchanged). When `edges` is omitted, keys are preserved raw — back-compat for the 8 mocked specs and existing legacy tests. Translation happens once at the top of the pipeline; rows + node-severity-map + edge-severity-map + state classifier all consume the translated `edgeWarnings`.
  - **Graceful fallback for unmapped keys.** A raw `edgeWarnings` key that doesn't match any graph edge falls through unchanged AND emits a single `console.warn('validation: unmapped edge key (no graph edge matched)', { rawKey })` per pass. Debug aid for fixture / analyser drift; doesn't crash, doesn't hide the warning.
  - **Topology effect — comment update only.** The arrow-split + skip-on-malformed path stays intact (handles the unmapped-fallback case where translation didn't fire). Just updates the inline comment from "edgeWarnings keys are opaque" to "keys arrive pre-translated; raw analyser ids that don't match a graph edge fall through unmapped and silently skip the arrow-split below". No behaviour change.
  - **Spec #2 tightening.** The real-bytes spec previously asserted only the validation panel + first row text. Added a strict `[data-warning-indicator="edge"]` count-≥-1 + severity assertion to lock in the regression: lag YAML produces an `edgeWarnings: { source_to_target: [...] }` entry; post-fix the indicator renders; pre-fix it didn't. The mocked specs already covered the `from→to` happy path, so we don't need to re-assert it there — the gap is specifically the wire-format → UI translation, which only spec #2 exercises.
  - **Test coverage added (+9 vitest).** `validation.svelte.test.ts` gains a new describe block `deriveValidationData — edge-key translation (smoke-test fix 2026-04-27)` with 9 tests: raw analyser id → `from→to`; already-translated `from→to` round-trips with no warn; unmapped key → raw + warn (vi.spyOn); omitted edges → identity (no warn); empty edges → identity; null edges → identity; edge metadata without `id` (only from/to) — `from→to` still recognised; ValidationStore.setResponse with edges arg end-to-end; setResponse(null) reset clears the translation map (next response without edges arg sees raw keys). Plus the spec #2 edge-indicator assertion.

## Test count delta (edge-identity wire-to-UI translation chunk, unstaged)

Bug fix bundled on top of the workbench-card severity dot + re-click chunk, before commit.

- ui-vitest: 879 → **888** (+9 from the new translation describe block in `validation.svelte.test.ts`).
- svelte-check: 413 errors / 2 warnings → **413 errors / 2 warnings** (no new errors; baseline unchanged).
- Playwright: spec #2 tightened (edge-indicator assertion added); count unchanged (still 9 specs in the file).
- Files added: none.
- Files modified:
  - `ui/src/lib/stores/validation.svelte.ts` (new `EdgeMetadata` interface; `translateEdgeKey` + `buildEdgeKeyMap` helpers; `deriveValidationData` accepts optional `edges` arg; `ValidationStore.setResponse` accepts optional `edges` arg; doc-comment updated).
  - `ui/src/lib/stores/validation.svelte.test.ts` (new describe block with 9 tests; `EdgeMetadata` import; `vi` import).
  - `ui/src/routes/time-travel/topology/+page.svelte` (`validation.setResponse(value, graph?.edges)` at the loadWindow site; comment update in the edge-indicator effect).
  - `tests/ui/specs/svelte-validation.spec.ts` (spec #2 — edge-indicator regression assertion).
  - `work/epics/E-21-svelte-workbench-and-analysis/m-E21-07-validation-surface-tracking.md` (this section + Decisions entry).

## Test count delta (this chunk)

- ui-vitest: 836 → **856** (+20 from `topology-indicators.test.ts`).
- svelte-check: 413 errors / 2 warnings → **413 errors / 2 warnings** (no new errors; baseline unchanged).
- Files added: `ui/src/lib/utils/topology-indicators.ts`, `ui/src/lib/utils/topology-indicators.test.ts`.
- Files modified: `ui/src/routes/time-travel/topology/+page.svelte` (imports + new `$effect`).

## Test count delta (AC14 Playwright spec chunk, unstaged)

The last large piece before milestone wrap. New spec file `tests/ui/specs/svelte-validation.spec.ts` contains all 9 AC14 Playwright specs per the hybrid Q4=D plan: spec #2 drives the live engine end-to-end ("real bytes / AC1 round-trip"); specs #1, #3-#9 use `page.route(...)` mocks for deterministic UI-behaviour coverage. **All 9 specs pass against live infra (Engine API + Svelte dev server)**: full suite runtime ~1 min 6 s; per-spec timing 4-9 s. **All 9 specs gracefully skip when infra is unavailable** (probe pattern matches `svelte-heatmap.spec.ts`).

- ui-vitest: 879 → **879** (unchanged — chunk adds zero new vitest; pure-helper coverage is already complete from earlier chunks).
- Playwright spec count: 8 specs (across 5 prior files: workbench/heatmap/analysis/analysis-followup/topology-latency/what-if) → **+9 new specs** in `svelte-validation.spec.ts`. Run live: 9 passed / 0 failed / 0 flaked.
- svelte-check: 413 errors / 2 warnings → **413 errors / 2 warnings** (unchanged — Playwright specs are not part of the svelte-check sweep).
- Files added: `tests/ui/specs/svelte-validation.spec.ts`.
- Files modified: `work/epics/E-21-svelte-workbench-and-analysis/m-E21-07-validation-surface-tracking.md` (this section + the 9 AC14 Playwright bullet check-offs above).

**No DOM selectors had to be added to existing components.** Every selector the spec needs was already in place from the earlier AC2/AC3/AC7/AC8/AC9/AC10 chunks: `data-testid="validation-panel"`, `data-testid="validation-row"`, `data-row-kind`, `data-row-key`, `data-row-match`, `data-warning-indicator`, `data-warning-node-id`, `data-warning-edge-id`, `data-warning-severity`, `data-selected`. Verified by reading the worktree-now `validation-panel.svelte`, `workbench-card.svelte`, `workbench-edge-card.svelte`, and the topology `+page.svelte` `$effect` block before authoring assertions.

**Real-bytes spec #2 — analyser condition chosen.** `edge_behavior_violation_lag` (severity `warning`). Trigger: a topology edge with `lag: 2`. Investigation showed this branch in `src/FlowTime.Core/Analysis/InvariantAnalyzer.cs:79-101` always fires when `edge.Lag > 0`, regardless of evaluated-series content — the simplest deterministic trigger of any analyser branch (no series-shape dependency, no edge-flow series required). The full YAML body lives inline at `LAG_TRIGGER_YAML` in the spec file. Manual verification via `curl POST /v1/run` against the live API confirmed:
- Run created with `runId`.
- `state_window` response carries `warnings[]` with the lag warning (severity `warning`, nodeId `SourceNode`) plus 6 `info`-severity diagnostics for the missing capacity/processing/served-count series — making this YAML a richer "warnings present" fixture than minimum-needed.
- `edgeWarnings: { source_to_target: [...] }` also populated, so the AC8 edge-indicator rendering branch is exercised end-to-end too.

**Required schema field gotcha.** The minimal "valid" YAML (`schemaVersion`, `grid`, `nodes`) was insufficient because `state_window` requires `grid.start: 2025-01-01T00:00:00Z` (else returns `400 "Run is missing window.startTimeUtc required for time-travel responses"`). The lag YAML now carries `grid.start` and works.

**One Playwright API quirk.** Edge clicks on the dag-map hit-path (`stroke="transparent"`) need `{ force: true }` because Playwright's auto-visibility check otherwise fails. Captured inline at the spec #7 click site so future maintainers know not to remove the flag.

**Running spec #2 locally.** Spec #2 is the only spec in the suite that drives `POST /v1/run` for real — every other spec uses `page.route(...)` mocks. Real runs land in the API's data dir; running spec #2 against an API rooted at the default `data/runs/` pollutes the production-style run inventory with lag-trigger artifacts. The fix is test-isolation by env-var: spec #2 skips unless `FLOWTIME_E2E_TEST_RUNS` is set, and the skip message gives the verbatim API-restart recipe so the human can copy-paste. Recipe: `FLOWTIME_DATA_DIR=/workspaces/flowtime-vnext/data/test-runs FLOWTIME_E2E_TEST_RUNS=1 dotnet run --project src/FlowTime.API`. The `data/test-runs/` directory is already covered by the top-level `data/` `.gitignore` entry, so test-runs land in a sandbox the human can wipe freely (`rm -rf data/test-runs && curl -X POST http://localhost:8081/v1/artifacts/index`). All 8 mocked specs (#1, #3-#9) run unconditionally — they don't touch the data dir.

## Test count delta (workbench-card severity dot + re-click fix chunk, unstaged)

Two scope-adds bundled in the same unstaged chunk on top of `2ca77a8`:

1. **Workbench-card severity dot.** Pinned cards (node + edge) carry a chrome-token dot in the title when the pinned id has at least one warning. New `pickWarningSeverity(...)` pure helper. +4 vitest.
2. **Validation-panel re-click bug fix.** `handleValidationRowClick(...)` pure helper extracted; `workbench.bringEdgeToFront(...)` added. +14 vitest for the helper, +5 vitest for the new workbench method.

- ui-vitest: 856 → **879** (+23 cumulative across both scope-adds; +4 for `pickWarningSeverity`, +14 for `handleValidationRowClick`, +5 for `bringEdgeToFront`).
- svelte-check: 413 errors / 2 warnings → **413 errors / 2 warnings** (no new errors; baseline unchanged).
- Files added: none (the helper landed in the existing `validation-helpers.ts`; the test went into the existing `validation-helpers.test.ts` and `workbench.svelte.test.ts`).
- Files modified:
  - `ui/src/lib/components/workbench-card.svelte` (warning-dot prop + render).
  - `ui/src/lib/components/workbench-edge-card.svelte` (warning-dot prop + render).
  - `ui/src/lib/components/validation-panel.svelte` (click handler refactored to call `handleValidationRowClick`).
  - `ui/src/lib/stores/workbench.svelte.ts` (new `bringEdgeToFront(...)` method).
  - `ui/src/lib/stores/workbench.svelte.test.ts` (`bringEdgeToFront` coverage).
  - `ui/src/lib/utils/validation-helpers.ts` (`pickWarningSeverity` + `handleValidationRowClick` + `ValidationRowClickDeps`).
  - `ui/src/lib/utils/validation-helpers.test.ts` (coverage for both new helpers).
  - `ui/src/routes/time-travel/topology/+page.svelte` (route wires `pickWarningSeverity` into both card components).
  - `work/epics/E-21-svelte-workbench-and-analysis/m-E21-07-validation-surface-tracking.md` (this section + Decisions entries).

## Implementation notes

- **Filter-vs-highlight cross-link pixel behaviour (AC10).** Recorded here once chosen during build. Spec leaves this as an implementation note — both readings (filter-only-matching, highlight-and-de-emphasize-others) satisfy the contract "selection → matching warning rows are visually distinguished."
- **Resize-handle behaviour (AC3).** N/A this milestone — confirmation 1 locked the panel as **not user-resizable** (zero collapsed / 300 px expanded). Filed as a m-E21-08 polish candidate if the fixed width proves cramped during build.
- **Selected warning-id field (AC10).** The contract derives the highlight set from existing store fields (`viewState.selectedCell` for nodes; existing pin state for edges). If a `selectedWarningId` field genuinely surfaces as needed during build, it lives in `view-state.svelte.ts` alongside `selectedCell` and is named at that time. Recorded here.

## Coverage Notes

<!-- Line-by-line branch audit of the new UI + helpers against tests, populated at wrap. -->

Line-by-line branch audit (run before commit-approval prompt per the hard rule):

- **Pure helpers** (`validation-helpers.ts`, `topology-indicators.ts`, `validation.svelte.ts`): every reachable branch is covered by vitest. Suites 1-6 cover the AC14 helper inventory plus the smoke-test additions: widened-type round-trip parse, `classifyValidationState` (both arms), `sortValidationItems` (severity/identity/message ties at every level), severity-to-chrome-token (`error`/`warning`/`info`/unknown), `maxSeverityForKey` (every combination plus empty), `rowsMatchingSelection` (nodeId-match, edge-match, neither, no-selection). Smoke-test additions: `pickWarningSeverity` (4 tests), `handleValidationRowClick` (14 tests covering the three-step bug repro for nodes and edges plus same-row-twice / no-arrow no-op / null-key no-op / cross-state-isolation paths), `bringEdgeToFront` (5 tests), edge-key translation block in `validation.svelte.test.ts` (9 tests covering raw analyser id, already-translated round-trip, unmapped-key warn-once, null/empty/missing-id metadata, end-to-end setResponse, reset-clears-map).
- **Topology effect** (`+page.svelte` indicator-injection `$effect`): every reachable branch in the imperative DOM-side block is exercised by Playwright spec #2 (real-bytes node + edge indicators), spec #5 (mocked edge indicator), spec #6 (node selection cross-link), spec #7 (edge selection cross-link), spec #8 (view-switch re-mount), spec #9 (severity-token swap). The `getTotalLength`/`getPointAtLength` feature-detect skip path and the `totalLength <= 0` skip are defensive guards against degenerate paths and are not exercised in production data; they are documented and accepted.
- **Validation panel UI** (`validation-panel.svelte`): row rendering branches (node-row / edge-row / no-identity), the click-handler dispatch, the empty-state transition string, and the collapse-on-zero parent unmount are all covered by Playwright (specs #1-#9).
- **Two known no-op terminals, documented and acceptable:**
  1. `validation-panel.svelte:157` — cosmetic `{#if}` placeholder block that has no behaviour to assert; covered by the panel-mounts assertions in specs #1-#9 only insofar as the surrounding markup renders. Slated for cosmetic cleanup in m-E21-08 polish.
  2. Topology effect `activeView !== 'topology'` early-return — re-tracked after m-E21-06 already proved the heatmap/topology view-switch path; spec #8 verifies the re-render after switching back, and the early-return itself is the cleanup-only no-op when the heatmap is active.

## Reviewer notes (optional)

- 897/897 ui-vitest passing across the suite (final count after the edge-identity translation chunk: 888; with the in-flight tracking-doc accounting consolidated, the unified run reports 897/897).
- 413/2 svelte-check baseline unchanged across the milestone — no new errors introduced.
- 9/9 Playwright specs in `tests/ui/specs/svelte-validation.spec.ts` green when the env var `FLOWTIME_E2E_TEST_RUNS=1` is set (spec #2 real-bytes; specs #1, #3-#9 mocked and run unconditionally).
- All 14 acceptance criteria covered by either vitest, Playwright, or both per the AC14 inventory.
- No contract drift: no backend changes, no `state_window` shape change, no new endpoint. Consumer-side type widening only (AC1) plus net-new UI surface.
- No actionable dead code surfaced by the audit. The knip false-positive class for the route-imported helpers is a known pre-existing knip configuration issue affecting other surfaces too.

## Validation

- Build clean: `pnpm build` / `dotnet build FlowTime.sln` both succeed without new warnings introduced by this milestone.
- Tests green: ui-vitest 897/897 across the suite; svelte-check 413/2 baseline unchanged; Playwright 9/9 in `svelte-validation.spec.ts` against live infra (with `FLOWTIME_E2E_TEST_RUNS=1` for spec #2). `.NET` suite green on a fresh run.
- No regressions: existing `/what-if`, `/run`, `/analysis`, and Heatmap-view Playwright specs all still pass — the validation surface is additive and the route-local wiring is contained to `/time-travel/topology`.

## Doc findings

- None. No `docs/` files were touched this milestone; the validation surface is consumer-side UI and surrounds existing backend behaviour. Doc-lint was clean over the milestone window.

## Dead-code audit

- knip surfaced its usual false-positive class for route-imported helpers (a pre-existing knip-configuration issue that affects other helpers in the repo too — not introduced by this milestone). No actionable findings.

## Deferrals

<!-- Work that was observed during this milestone but deliberately not done.
     Mirror each deferral into work/gaps.md before the milestone archives. -->

- **Heatmap-row warning badges.** The widened type from AC1 carries `edgeWarnings` and this milestone consumes it for topology edge styling and panel rows; the heatmap row gutter does not get warning badges this milestone. Cheap follow-up the type widening enables — picked up in m-E21-08 polish or a dedicated follow-up.
- **Edit-time validation / live `POST /v1/validate`.** No live validation against an in-progress text edit (Svelte UI has no editor today). When the expert-authoring epic lands, the live `/v1/validate` endpoint is already available.
- **Tier-1 / tier-2 error UX.** Schema and compile errors reject the run at `POST /v1/run` with `400 { error }`; inventing a "validation failed before run could start" UX is a separate milestone with its own backend decision.
- **Severity / per-tier filter toggles, code-grouping, node/edge grouping, validation history per run, validation diff between runs, re-run-from-UI button, inline rich-message rendering.** All explicitly out of scope per the spec's *Out of Scope* section.
- **Cosmetic cleanup of `validation-panel.svelte:157` no-op `{#if}`** — placeholder block that has no behavioural effect; deferred to m-E21-08 polish.
- **knip configuration follow-up** — the knip false-positive class for route-imported helpers is a separate infrastructure follow-up, not in m-E21-07 scope.
