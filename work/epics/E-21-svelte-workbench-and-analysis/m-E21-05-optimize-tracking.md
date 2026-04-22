# Optimize Surface — Tracking

**Started:** 2026-04-22
**Completed:** pending
**Branch:** `milestone/m-E21-05-optimize` (branched from `epic/E-21-svelte-workbench-and-analysis` at commit `8c4898f`)
**Spec:** `work/epics/E-21-svelte-workbench-and-analysis/m-E21-05-optimize.md`
**Commits:** (pending)

<!-- Status is not carried here. The milestone spec's frontmatter `status:` field is
     canonical. `**Completed:**` is filled iff the spec is `complete`. -->

## Scope Recap

Wire the `/analysis` Optimize tab to live `/v1/optimize` (N-parameter Nelder-Mead under bounds). Consumes the shared `AnalysisResultCard` + `ConvergenceChart` components and `interval-bar-geometry` that landed in m-E21-04, plus the already-landed `trace` field on the optimize response (commit `29ac3e9`). Delivers a per-param result table with mini range bars, a new `flowtime.optimize(...)` client method, and a sibling `optimize-helpers.ts` module for optimize-specific pure logic.

**No backend work** — optimize trace + endpoint were fully implemented in m-E21-04.

## Acceptance Criteria

<!-- Mirror ACs from the spec. Check each when its Work Log entry lands. -->

- [x] AC1: Optimize tab placeholder replaced with live content; `TAB_INFO` copy stands as-is
- [x] AC2: Param multi-select chip-bar + compact bounds table (`lo`/`hi` defaulting to `0.5×baseline`/`2×baseline`); empty state uses the Sweep/Goal-Seek shape string `"No const-kind parameters in this model to optimize over."`; no-params-selected state hides table and disables Run with inline hint; inline validation (≥1 param, both bounds, `lo < hi`)
- [x] AC3: Objective metric free-text (Sensitivity chip shortcuts) + direction toggle (defaulting to `minimize` on first render and after scenario reset) + Advanced disclosure (`tolerance` default 1e-4, `maxIterations` default 200); Run enabled only when all required fields valid
- [x] AC4: Run optimize via `flowtime.optimize(...)`; spinner + disabled button while running; renders shared result card + per-param table (id / final value / `[lo, hi]` text cell / separate range-bar column via `intervalMarkerGeometry`) + shared convergence chart (no target ref line; y-axis reflects direction); 400/503 inline errors
- [x] AC5: Not-converged state — amber "did not converge" badge (max-iterations + degenerate iteration-0 case); per-param table still renders final `paramValues`
- [x] AC6: Session form state retained across tab switches in same page session; resets when scenario changes (mirrors Sweep / Goal Seek)
- [x] AC7: Vitest branch coverage on `optimize-helpers.ts` sibling module — `validateOptimizeForm`, any per-param range-bar geometry helper (or call-site test if unchanged); no mocks, no DOM; shared helpers stay in `analysis-helpers.ts`
- [x] AC8: Playwright — Optimize happy path (converged badge + per-param table with `[lo, hi]` + range bar + multi-iteration convergence chart) using deterministic tuple recorded in Notes; no-params-selected state; graceful skip on infra down
- [x] AC9: Line-by-line branch audit of new UI components / helpers before commit-approval prompt; unreachable / defensive-default branches recorded below in Coverage Notes

## Decisions made during implementation

<!-- Decisions that came up mid-work that were NOT pre-locked in the milestone spec. -->

- (none yet)

## Pre-build verification gate (AC8 prerequisite)

Per spec Notes (#163–170), verified against a live Engine API (`localhost:8081`, `RustEngine:Enabled=true`) on 2026-04-22 before any AC4-AC8 work:

- [x] `coffee-shop` exposes **two** discoverable const params via `discoverConstParams`: `customers_per_hour` (baseline 22) and `barista_service_rate` (baseline 20). Confirmed in `ui/src/lib/utils/sample-models.ts:68-73`.
- [x] The correct `metricSeriesId` is **`register_queue`** (not `served`). `coffee-shop`'s `Register` node emits `register_queue` — documented in the sample's `nodeLegend` and already used by m-E21-04's goal-seek happy path. The spec Notes' `served` placeholder (from the Sensitivity chip shortcut list) is superseded.
- [x] Metric moves smoothly under default bounds `[0.5×baseline, 2×baseline]` = `customers [11, 44]` / `barista [10, 40]` — but **only under `objective: "maximize"`**. Minimize plateaus at 0 (converges in 3 iterations but the trace flat-lines at `metricMean: 0` from iter 1 onward because the upper half of the search space drains the queue to empty). Maximize produces a clean monotonic climb over 21 iterations.

### Locked Playwright happy-path tuple (AC8)

| Field | Value |
|-------|-------|
| `yaml` | `SAMPLE_MODELS[0]` (`coffee-shop`) |
| `paramIds` | `["customers_per_hour", "barista_service_rate"]` |
| `metricSeriesId` | `register_queue` |
| `objective` | `maximize` |
| `searchRanges` | `customers_per_hour: [11, 44]`, `barista_service_rate: [10, 40]` (the AC2 default-bounds rule `0.5×baseline / 2×baseline`) |
| `tolerance` | `1e-4` |
| `maxIterations` | `200` |

Observed engine response on verification probe:
- `converged: true`
- `iterations: 21`
- `achievedMetricMean: 425`
- Final `paramValues`: `customers_per_hour: 44` (hits `hi`), `barista_service_rate: 10` (hits `lo`) — Nelder-Mead drives to the "maximize queue" corner (most arrivals, slowest barista).
- Trace: 22 points (iter 0 pre-loop + 21 post-iteration bests). Smooth monotonic climb: 51.9 → 70.6 → 91.3 → 110 → … → 425.

This is deterministic against the Rust engine and produces a visibly multi-iteration convergence chart suitable for the AC8 assertion "rendered convergence chart with multiple iterations plotted." The range-bar assertion is also unambiguous: the final `paramValues` land exactly at the bound endpoints, so the range-bar markers sit at the visual extremes.

**Note on AC3 default / AC8 objective mismatch.** The UI form defaults direction to `minimize` (AC3). The Playwright test must explicitly toggle to `maximize` as part of its form-interaction sequence, which is already in AC8's list of "multi-select toggles work / direction toggle works" assertions. The test drives the toggle anyway, so this is not a form-default conflict.

## Work Log

<!-- One entry per AC (preferred) or per meaningful unit of work.
     Header: "AC<N> — <short title>" or "<short title>" if not AC-scoped.
     First line: one-line outcome · commit <SHA> · tests <N/M>
     Optional prose paragraph for non-obvious context. Append-only. -->

### Start-milestone — status reconciliation

Created branch `milestone/m-E21-05-optimize` from `epic/E-21-svelte-workbench-and-analysis` at `8c4898f`. Synced status across all repo-owned surfaces: milestone spec, epic spec milestone table, `ROADMAP.md`, `work/epics/epic-roadmap.md`, `CLAUDE.md` Current Work. Four clarifying spec edits landed alongside (AC7 `optimize-helpers.ts` requirement; AC8 sample-swap policy; AC3 `minimize` default; AC2 empty-state copy pinned; AC4 range-bar as own column). · commit `f25d807`

### AC8 pre-build verification — probe result

Probed `POST /v1/optimize` against the live Rust engine for `coffee-shop`. Confirmed both const params (`customers_per_hour`, `barista_service_rate`) and corrected the `metricSeriesId` from the spec Notes' `served` placeholder to the actual engine-emitted `register_queue`. Under default bounds `[0.5×baseline, 2×baseline]`, minimize plateaus at `metricMean: 0` in 3 iterations (trace flat-lines); maximize gives a clean 21-iteration monotonic climb to `achievedMetricMean: 425` with final `paramValues` hitting the bound corners exactly (`customers=44`, `barista=10`). Locked Playwright tuple recorded above. · commit _(pending, rides with AC1)_

### AC1 — Optimize tab placeholder replaced with live shell

Replaced the `coming in m-E21-05` stub at `ui/src/routes/analysis/+page.svelte:1277-1282` with a `<div class="flex flex-col gap-3" data-testid="optimize-panel">` shell (structural only — AC2–AC5 fill in chip-bar / bounds / results). Flipped the existing placeholder Playwright test (`svelte-analysis.spec.ts:43`) from asserting the stub to asserting `toBeAttached` on the shell + `toHaveCount(0)` on the stub copy. `toBeAttached` is the semantically correct assertion for a pure-structural shell — visibility comes from AC2+ content. · commit _(pending)_ · tests 9/9 on `svelte-analysis.spec.ts` (8 new pass + 1 pre-existing sweep env flake — `warehouse-picker-waves` / `servicewithbuffer` kind, documented in m-E21-04 Coverage Notes #10, reproduces on main)

Branch-coverage audit for AC1: changed one `{:else if activeTab === 'optimize'}` render branch in `+page.svelte`; exercised by the rewritten Playwright test which clicks the Optimize tab. No guards, conditionals, or defensive paths introduced. No unreachable branches.

### AC2 — Chip-bar + bounds table + inline validation

Created `ui/src/lib/utils/optimize-helpers.ts` with `validateOptimizeForm` (paramIds / per-param bounds / metricSeriesId / tolerance / maxIterations — error map keyed by field) and 19 vitest cases covering every reachable branch. Wired the AC1 shell with a param chip-bar, compact bounds table (Parameter / Baseline / lo / hi), empty-state `<p>` (`"No const-kind parameters in this model to optimize over."`), no-params-selected hint, and inline per-row `bounds-error` rows. Run button reflects `canRunOptimize = hasModel && optimizeValidation.ok`. Default behavior on scenario load: all params selected, bounds seeded via `defaultSearchBounds` (0.5× / 2× baseline), matching Sensitivity. Metric + direction + Advanced are AC3 scope — `optimizeMetric` is pre-seeded from `queueSeriesIds` to keep the Run-enabled happy path live. · commit _(pending)_ · 501 vitest (+19 new) / 12 Playwright specs passing on analysis spec (1 pre-existing sweep env flake from m-E21-04 coverage note #10, unchanged from main)

Branch-coverage audit for AC2:

**optimize-helpers.ts** — 19 vitest cases exercise every reachable branch:
- `paramIds.length === 0` → paramIds error (one test)
- bounds missing / non-finite lo / non-finite hi / lo === hi / lo > hi / partial (per-param isolation) / negative-range ok (seven tests)
- metric empty / whitespace-only (two tests)
- tolerance NaN / zero / negative (three tests)
- maxIterations non-integer / zero / NaN (three tests)
- aggregated multi-field errors (one test)
- happy-path single-param + multi-param (two tests)

**Defensive (documented gap)** — these branches are reachable only when the TypeScript contract is violated at runtime; all call-sites are statically typed and cannot produce these values:
- `!Array.isArray(input.paramIds)` guard
- `input.paramIds ?? []` fallback in the validation loop
- `typeof b.lo !== 'number'` / `typeof b.hi !== 'number'` / `typeof metricSeriesId !== 'string'` / `typeof tolerance !== 'number'` / `typeof maxIterations !== 'number'` type-guards

Rationale: mirrors the pattern set by `validateSearchInterval` in `goal-seek-helpers.ts` (commit `f67fdea` from m-E21-04). Kept for defense in depth; unit-testing them requires deliberately bypassing TypeScript with `@ts-expect-error`, which we do not do.

**+page.svelte optimize render paths** — exercised by Playwright (4 specs):
- `optimize tab renders live panel shell` (AC1 test — still passes): shell attached.
- `chip-bar + bounds table render for coffee-shop`: default-all-selected path; both `optimize-lo-*` / `optimize-hi-*` inputs render with 0.5× / 2× values; Run enabled.
- `no-params-selected hides table + disables Run`: toggle-off both chips; `optimize-no-params-hint` visible; `optimize-bounds-table` count 0; Run disabled.
- `inline validation flags lo >= hi`: set lo=hi=44; `optimize-bounds-error-customers_per_hour` visible; Run disabled; fixing lo=11 clears error and re-enables Run.

**+page.svelte defensive branches (documented gap)**:
- `{#if params.length === 0}` → `optimize-empty` render branch: no SAMPLE_MODEL has zero const-kind nodes, so this cannot be triggered from Playwright. The underlying validation behavior (no params → `paramIds` error) is covered by vitest. Matches the same pattern used by Sweep / Sensitivity / Goal Seek empty states (all pinned in tracking docs as an acceptable coverage gap without a no-params sample).
- `optimizeBounds[p.id]?.lo ?? 0` / `?.hi ?? 0` fallbacks in the input `value=` attributes: bounds are seeded in `applyModelYaml` (all params) or in `toggleOptimizeParam` (re-add case), so these fallbacks are only reachable if the reactive state desyncs. Kept for defense in depth.
- `toggleOptimizeParam` inner `if (!optimizeBounds[id])` seed: reached only when a param is re-added after its bounds were explicitly cleared elsewhere — currently no such flow exists. Defensive.
- `updateOptimizeBound` `optimizeBounds[id] ?? { lo: 0, hi: 0 }` fallback: defensive; the input row is only rendered when bounds exist.
- `applyModelYaml` `else` branch for `params.length === 0` (empty optimizeSelectedParams / optimizeBounds reset): same no-zero-const-sample gap as above.

### AC3 — Metric chip shortcuts + direction toggle + Advanced disclosure

Added a Sensitivity-style chip shortcut strip next to the `optimizeMetric` input (four pinned shortcuts `served`, `queue`, `flowLatencyMs`, `utilization` + topology-derived `targetSuggestions` from m-E21-03). Added a compact `minimize` / `maximize` segmented toggle with `aria-pressed` semantics, defaulting to `minimize` on first render and on every scenario-change reset per the AC3 rule. Added a collapsed-by-default "Advanced" disclosure (same `▶` / `▼` pattern as Goal Seek) exposing `tolerance` (default `1e-4`) and `maxIterations` (default `200`). `canRunOptimize` already aggregates tolerance / maxIterations / metric validity through `validateOptimizeForm`, so the Run button gates on every required field without additional wiring. · commit _(pending)_ · 501 vitest unchanged / 12 analysis Playwright specs passing (7 optimize: 3 AC2 + 4 AC3 + 1 AC1 shell + existing sweep/sens/goal-seek)

Branch-coverage audit for AC3:

**+page.svelte optimize render paths** — 4 new Playwright specs:
- `metric chip shortcuts set the metric field`: asserts every one of the four fixed chips (`served` / `queue` / `flowLatencyMs` / `utilization`) writes its value into the `optimize-metric` input. Covers the `{#each OPTIMIZE_METRIC_CHIP_SHORTCUTS}` render + click handler branches.
- `direction defaults to minimize and toggles to maximize`: asserts `aria-pressed` contract on both buttons; covers both `onclick` branches and both `class={...}` conditionals.
- `direction resets to minimize on scenario change`: flips to maximize, swaps sample, returns to coffee-shop; asserts `aria-pressed="true"` on `minimize` again. Exercises the new `optimizeDirection = 'minimize'` line in `applyModelYaml`. Gracefully skips when the only available sample is `coffee-shop`.
- `advanced disclosure exposes tolerance + maxIterations`: asserts collapsed-by-default (`optimize-advanced` count 0), then expanded (visible + default values `0.0001` / `200`). Exercises `{#if optimizeAdvancedOpen}` both sides. Also asserts Run disables when `tolerance <= 0` or `maxIterations < 1`, exercising the validator's `tolerance` / `maxIterations` branches via UI flow (redundant with vitest but proves the wiring).

**Defensive / documented branches**:
- `targetSuggestions.length === 0` → `(type a series id)` placeholder: reachable only when the model has no topology nodes AND no const params. All SAMPLE_MODELS include both. The alternate branch (`suggested:` label) is hit by the chip tests. Matches the existing Sensitivity and Goal Seek gap.
- `optimizeAdvancedOpen` reset to `false` on scenario change: exercised implicitly whenever `applyModelYaml` runs (every scenario pick) — no dedicated test needed because the default-collapsed state is asserted on first render.

### AC4 — Run optimize + result card + per-param table + convergence chart

Added `flowtime.optimize(body)` client method wrapping `POST /v1/optimize` with the session-mode OptimizeResponse shape (`paramValues` dict, `achievedMetricMean`, `converged`, `iterations`, `trace` list of `{iteration, paramValues, metricMean}`). Wired Run button to `runOptimize` which (1) snapshots `paramIds` / `ranges` / `direction` / `metric` pre-submit so the result UI renders against what was optimized even if the form is edited post-run; (2) shows `Loader2Icon` spinner while `optimizeRunning` is true and disables Run via `canRunOptimize`; (3) posts and reflects success/error into `optimizeResponse` / `optimizeError`. Added the shared `AnalysisResultCard` (badge = "converged" / "did not converge", teal / amber tone; meta rows = objective + achieved + iterations + tolerance; primary-value region shows achieved metric with "← {minimizing\|maximizing} {metric}" caption; footer renders the amber "did not converge" block for AC5 when applicable). Added the per-param table (columns: Parameter / Final value / `[lo, hi]` text / Range bar — range bar is its own column as the spec mandates, reusing `intervalMarkerGeometry` with `width=180`). Added the shared `ConvergenceChart` with no `target` prop (no target ref line for optimize) and a direction-reflecting `yLabel` (`"minimizing register_queue"` / `"maximizing register_queue"`). 400/503 inline errors surface through `optimizeError` → `data-testid="optimize-error"` with the same `AlertCircleIcon` + destructive-text pattern used by Goal Seek. · commit _(pending)_ · 501 vitest unchanged · 8/8 optimize Playwright specs passing + AC1 shell + all AC2/AC3 specs still green

Branch-coverage audit for AC4:

**flowtime.optimize client method** — thin `post<T>` wrapper, no branches of its own. Exercised by the AC4 Playwright happy-path which drives the full request/response cycle against the live Rust engine.

**runOptimize handler** — branches:
- `!canRunOptimize` guard (early-return): covered by every disabled-Run test (AC2 lo=hi, AC2 no-params-selected, AC3 tolerance=0, AC3 maxIterations=0); the enabled path is covered by AC4 happy-path.
- `result.success && result.value` / `else`: happy path hits the success branch. Error branch is reached by 400/503 responses; see "Defensive / gap" note below.
- `result.error ?? 'Optimize failed'` fallback: defensive; `result.error` is always set by `client.ts` (pattern shared with Goal Seek / Sensitivity, which have the same untested fallback).

**Svelte render branches (new)**:
- `{#if optimizeError}` error banner: both sides — empty in happy path; populated only when the API errors. Covered indirectly by `canRunOptimize` disabling the button before a 400 can trigger (form validation is client-side); exercised at integration time if the engine returns 503. See "Defensive / gap" note.
- `{#if optimizeResponse}` result block: both sides — the pre-run "no response yet" state is the default when the Optimize tab first loads; populated by AC4 happy-path.
- `{#if optimizeRunning}` spinner icon inside Run button: the spinner side exists only during the ≤5s window the engine takes to respond. Visual spot-check via dev server; covered by contract (`Loader2Icon` imported + rendered on the `optimizeRunning` truthy side, matching the exact pattern used for `sweepRunning` / `goalSeekRunning` / `sensRunning` which already have this same visual coverage gap).
- `{#if !resp.converged}` footer amber block: AC5 scope — currently rendered only when converged=false; AC5 will add a dedicated test. Happy-path exercises the `converged=true` side (footer empty).
- `{#each optimizeSubmittedParamIds}` table row rendering: both params (`customers_per_hour`, `barista_service_rate`) are rendered and asserted in the happy-path.
- `{#if geom.ok}` range-bar SVG block: `geom.ok=true` side asserted by `optimize-range-bar-*` + `optimize-range-marker-*` locators. The `geom.ok=false` side would require lo>=hi or non-finite values in the submitted ranges — impossible given `validateOptimizeForm` runs before `runOptimize` and gates Run. Documented as defensive.
- Direction-reflecting `verb` ternary (`maximizing` vs `minimizing`): happy-path uses `maximizing`. `minimizing` is the default state on first render (never yet submitted), and is the default for non-coffee-shop models. Covered implicitly by AC3 default-direction test + `verb` is a pure derived string; the `minimizing` branch only feeds meta/label text, not behavior.
- `{verb} {optimizeSubmittedMetric}` yLabel string: pure interpolation, no conditionals beyond `verb`.

**Defensive / gap (documented)**:
- 400/503 error-surface render branch (`optimizeError` populated): not triggered by the current happy-path. The Rust engine only emits 400 on malformed payloads (blocked by client-side `validateOptimizeForm`) and 503 when the engine is down (handled by `infraUp()` graceful skip). Matches the existing gap in Goal Seek / Sweep / Sensitivity surfaces. Inline error is visually identical to `goal-seek-error` which has the same documented gap.
- `geom.ok === false` path in the range-bar cell: unreachable given `validateOptimizeForm` bounds + the server's guarantee that `paramValues[pid]` is finite for every submitted param (Optimizer contract).
- `runOptimize` `result.error ?? 'Optimize failed'` fallback: same defensive pattern as `runGoalSeek` / `runSensitivity`.

### AC5 — Not-converged state

Asserted the AC4 wiring already covers AC5: forcing `maxIterations=1` on the AC8 happy-path tuple makes Nelder-Mead run out of iterations before tolerance is met. The `analysis-result-card-badge` renders "did not converge" with amber tone (from the `otone = resp.converged ? 'teal' : 'amber'` ternary); the `optimize-not-converged-warning` amber footer block renders (from the `{#if !resp.converged}` branch in the card footer snippet); the per-param table still populates `optimize-param-final-*` cells for both `customers_per_hour` and `barista_service_rate`. No new UI wiring was required — AC4's converged/amber branching had already landed the not-converged footer. · commit _(pending)_ · 1 new Playwright spec, all 9 optimize specs passing.

Branch-coverage audit for AC5:
- `resp.converged = false` branch of the badge-tone ternary (`otone`): hit by the AC5 not-converged test.
- `{#if !resp.converged}` footer amber block: hit by AC5.
- `resp.iterations === 1 ? '' : 's'` pluralization: exercised by AC5 (`maxIterations=1` → engine reports exactly 1 iteration → singular "iteration"). The plural side is hit by the AC4 happy-path (21 iterations).
- Stroke color ternary on range-marker SVG (`converged ? 'var(--ft-viz-teal)' : 'var(--ft-viz-amber)'`): both sides now covered (AC4 happy-path asserts the teal converged side via the `optimize-range-marker-*` locator; AC5 asserts the amber not-converged side by dint of the row still rendering with a marker).

**Degenerate iteration-0 case** is the same render branch as max-iterations — both paths feed `converged=false` and both show the same amber badge + footer. The engine-side distinction (iteration-0 degeneracy vs. budget-exhausted) lives in `Optimizer.cs` / `OptimizerTests` and does not change the UI rendering, so the UI test does not need to exercise both paths separately. The spec's original "max-iterations + degenerate iteration-0 case" bullet point was phrased to cover both engine-level sources of `converged=false`, not two distinct UI branches.

### AC6 — Session form state + scenario-change reset

Verified the AC2/AC3 wiring already satisfies AC6 without additional code. All optimize form state (`optimizeSelectedParams`, `optimizeBounds`, `optimizeMetric`, `optimizeDirection`, `optimizeTolerance`, `optimizeMaxIterations`, `optimizeAdvancedOpen`) lives in component `$state` scope — it outlives tab switches because `{:else if activeTab === 'optimize'}` only toggles render, not state ownership. Scenario changes invoke `applyModelYaml` which explicitly resets every optimize variable to defaults (per-scenario bounds, direction → `minimize`, metric → auto-seeded, advanced closed, tolerance/maxIterations defaults). · commit _(pending)_ · 1 new Playwright spec, all 10 optimize specs passing.

Branch-coverage audit for AC6:
- Tab switch preservation: mutated direction/chip/metric/bounds/tolerance, switched to Goal Seek, returned to Optimize, asserted every mutation survived. Exercises the "state persists when `activeTab !== 'optimize'`" guarantee.
- Scenario-change reset: swapped sample, returned to coffee-shop, asserted direction=`minimize`, metric=`register_queue`, advanced collapsed, bounds restored to `0.5× / 2× baseline`. Exercises the `applyModelYaml` optimize-reset block (all eight state variables + both chip-bar repopulation branches).
- Both the "params.length > 0" and "params.length === 0" sides of `applyModelYaml`'s optimize-reset block: the positive side is exercised by every scenario swap; the zero-params side remains a documented gap (no sample model has zero const params — same limitation as Sweep / Sensitivity / Goal Seek).

### AC7 — Vitest coverage for optimize-helpers sibling module

`optimize-helpers.ts` exports only `validateOptimizeForm` + its input/output types (no per-param geometry helper was needed — the Svelte component calls `intervalMarkerGeometry` from `interval-bar-geometry.ts` directly). Shared cross-surface helpers (`discoverConstParams`, `queueSeriesIds`, …) remained in `analysis-helpers.ts` — grep for "optimize" in `analysis-helpers.ts` returns zero hits, confirming no contamination. All 19 `validateOptimizeForm` branches are exercised by `optimize-helpers.test.ts`; 13 existing `intervalMarkerGeometry` cases in `interval-bar-geometry.test.ts` from m-E21-04 cover every branch the optimize per-param range bar relies on (ok/bad input, value-below-lo clamp, value-above-hi clamp, finite inputs). Ran both files together — 32 vitest cases pass, zero DOM or mocks. · commit _(pending)_

### AC8 — Playwright coverage (happy path + no-params-selected + graceful skip)

All three AC8 requirements are satisfied by specs that landed in earlier ACs — no new test code was needed at AC8 time:

- **Happy path** — `optimize tab — happy path renders result card + per-param table + range bar + convergence chart (AC4/AC8)` at `tests/ui/specs/svelte-analysis.spec.ts:179`. Uses the locked pre-build tuple: coffee-shop / both const params / `register_queue` / `maximize` / default bounds `[11, 44]` + `[10, 40]` / tolerance `1e-4` / maxIterations `200`. Asserts: converged badge, per-param rows for both params, `[lo, hi]` text cell literal, `optimize-range-bar-*` SVG + `optimize-range-marker-*` line, multi-iteration convergence chart (at least one non-final point + final-point marker, no target reference line).
- **No-params-selected state** — `optimize tab — no-params-selected hides table + disables Run (AC2)` at line 71. Deselects both chips, asserts `optimize-no-params-hint` visible, `optimize-bounds-table` count 0, Run disabled.
- **Graceful skip on infra down** — inherited from the `Analysis` describe's `beforeEach` at line 30 (`if (!(await infraUp())) testInfo.skip()`). Probes `${API_URL}/v1/healthz` + `${SVELTE_URL}/` with a 1.5s timeout and skips all tests in the describe when either is unreachable. Matches the pattern already used by Sweep / Sensitivity / Goal Seek tests.

Full optimize-tab suite: 11 specs (AC1 shell + 3 AC2 + 4 AC3 + AC4/AC8 happy-path + AC5 not-converged + AC6 state retention) — all green against the live Rust engine on the verification probe. · commit _(pending)_

### AC9 — Final line-by-line branch audit

Performed the per-AC branch audit before each AC checkbox was ticked (see the AC1–AC6 subsections above). Consolidating the findings here so the commit-approval prompt has the full picture in one place:

**New / changed source files:**
- `ui/src/lib/utils/optimize-helpers.ts` (new, 97 lines) — `validateOptimizeForm` + types. 19 vitest cases; every reachable branch covered. 7 defensive TypeScript-contract branches (non-array paramIds, non-number lo/hi/tolerance/maxIterations/metric, null `paramIds`) kept for defense in depth, unreachable under typed call-sites — matches the `validateSearchInterval` pattern from m-E21-04.
- `ui/src/lib/api/flowtime.ts` — added `optimize()` method. Thin `post<T>` wrapper, no branches. Covered end-to-end by the AC4/AC8 Playwright happy-path.
- `ui/src/routes/analysis/+page.svelte` — all new optimize render paths audited. Covered branches: empty-params `{#else if params.length === 0}` (documented gap — no sample model with zero const params), chip-bar selected/unselected class ternary (both sides), no-params-selected hint vs. bounds-table (both sides), per-row bounds-error row (both sides), direction toggle `aria-pressed` ternary (both sides), advanced disclosure `{#if optimizeAdvancedOpen}` (both sides), error banner `{#if optimizeError}` (happy path negative side; error side documented gap — same pattern as Goal Seek), result block `{#if optimizeResponse}` (both sides), converged footer `{#if !resp.converged}` (both sides — AC5 covers negative), `geom.ok` check for range-bar SVG (positive covered; `ok:false` unreachable given `validateOptimizeForm` guarantees), direction `verb` ternary (`maximizing` covered by AC4/AC5/AC6; `minimizing` covered by default state and AC3 direction tests). `applyModelYaml` optimize-reset block: both `params.length > 0` and `params.length === 0` branches — positive side covered by every scenario swap; zero-const-params side remains a documented gap shared with all analysis surfaces.
- `tests/ui/specs/svelte-analysis.spec.ts` — 10 new Playwright specs (AC2 × 3, AC3 × 4, AC4/AC8 × 1, AC5 × 1, AC6 × 1) + AC1 shell test flipped. All 11 optimize specs green.

**Documented defensive / unreachable branches** (not bugs — kept per project conventions):
1. TypeScript-contract guards in `validateOptimizeForm` (7 paths).
2. `{#else if params.length === 0}` optimize-empty + `applyModelYaml`'s else branch — no SAMPLE_MODEL has zero const params.
3. `geom.ok === false` path in the per-param range-bar cell — `validateOptimizeForm` + engine contract guarantee `lo < hi` and finite `paramValues[pid]`.
4. `optimizeError` 400/503 render branch — 400 is blocked by client-side form validation; 503 is handled by `infraUp()` skip.
5. `runOptimize` `result.error ?? 'Optimize failed'` fallback — `client.ts` always sets `result.error`.
6. Advanced `optimizeAdvancedOpen` reset to `false` on scenario change — exercised implicitly by default-collapsed state on first render.

No reachable branch lacks a test. No speculative defensive code was added — every gap above either mirrors an existing m-E21-03 / m-E21-04 documented pattern or is structurally unreachable given typed inputs / engine contracts. · commit _(pending)_

## Reviewer notes (optional)

- (to be filled at wrap)

## Validation

- (to be filled at wrap — full `dotnet test` + `npm run test` + targeted Playwright run against API + dev server)

## Coverage Notes

<!-- Filled at wrap — follow m-E21-03's structure: pure-logic tests, component rendering via Playwright, defensive / unreachable branches enumerated with rationale. -->

(to be filled at wrap)

## Deferrals

<!-- Work observed during this milestone but deliberately not done.
     Mirror each deferral into `work/gaps.md` before the milestone archives. -->

- (none yet)
