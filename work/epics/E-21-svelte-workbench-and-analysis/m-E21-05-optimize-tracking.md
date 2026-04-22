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

- [ ] AC1: Optimize tab placeholder replaced with live content; `TAB_INFO` copy stands as-is
- [ ] AC2: Param multi-select chip-bar + compact bounds table (`lo`/`hi` defaulting to `0.5×baseline`/`2×baseline`); empty state uses the Sweep/Goal-Seek shape string `"No const-kind parameters in this model to optimize over."`; no-params-selected state hides table and disables Run with inline hint; inline validation (≥1 param, both bounds, `lo < hi`)
- [ ] AC3: Objective metric free-text (Sensitivity chip shortcuts) + direction toggle (defaulting to `minimize` on first render and after scenario reset) + Advanced disclosure (`tolerance` default 1e-4, `maxIterations` default 200); Run enabled only when all required fields valid
- [ ] AC4: Run optimize via `flowtime.optimize(...)`; spinner + disabled button while running; renders shared result card + per-param table (id / final value / `[lo, hi]` text cell / separate range-bar column via `intervalMarkerGeometry`) + shared convergence chart (no target ref line; y-axis reflects direction); 400/503 inline errors
- [ ] AC5: Not-converged state — amber "did not converge" badge (max-iterations + degenerate iteration-0 case); per-param table still renders final `paramValues`
- [ ] AC6: Session form state retained across tab switches in same page session; resets when scenario changes (mirrors Sweep / Goal Seek)
- [ ] AC7: Vitest branch coverage on `optimize-helpers.ts` sibling module — `validateOptimizeForm`, any per-param range-bar geometry helper (or call-site test if unchanged); no mocks, no DOM; shared helpers stay in `analysis-helpers.ts`
- [ ] AC8: Playwright — Optimize happy path (converged badge + per-param table with `[lo, hi]` + range bar + multi-iteration convergence chart) using deterministic tuple recorded in Notes; no-params-selected state; graceful skip on infra down
- [ ] AC9: Line-by-line branch audit of new UI components / helpers before commit-approval prompt; unreachable / defensive-default branches recorded below in Coverage Notes

## Decisions made during implementation

<!-- Decisions that came up mid-work that were NOT pre-locked in the milestone spec. -->

- (none yet)

## Pre-build verification gate (AC8 prerequisite)

Per spec Notes (#163–170), before writing the Playwright happy-path spec:

- [ ] Verify `coffee-shop` sample exposes **two** discoverable const params via `discoverConstParams` (expected: `customers_per_hour` + `barista_service_rate` from `ui/src/lib/utils/sample-models.ts`).
- [ ] Verify the chosen `metricSeriesId` (`served` or namespaced equivalent like `Register.served`) is present in the Rust engine's actual output for `coffee-shop`.
- [ ] Verify the metric moves monotonically under `[0.5×baseline, 2×baseline]` for both params so Nelder-Mead converges within `maxIterations: 200`.
- [ ] If any of the above fails: **swap the sample** (pick an alternate from `SAMPLE_MODELS` whose metric is monotonic) and record the replacement tuple below. Do **not** soften AC8 to a not-converged assertion — AC5 already owns that rendering.

Chosen tuple: _(to be recorded at verification time)_

## Work Log

<!-- One entry per AC (preferred) or per meaningful unit of work.
     Header: "AC<N> — <short title>" or "<short title>" if not AC-scoped.
     First line: one-line outcome · commit <SHA> · tests <N/M>
     Optional prose paragraph for non-obvious context. Append-only. -->

### Start-milestone — status reconciliation

Created branch `milestone/m-E21-05-optimize` from `epic/E-21-svelte-workbench-and-analysis` at `8c4898f`. Synced status across all repo-owned surfaces: milestone spec, epic spec milestone table, `ROADMAP.md`, `work/epics/epic-roadmap.md`, `CLAUDE.md` Current Work. Four clarifying spec edits landed alongside (AC7 `optimize-helpers.ts` requirement; AC8 sample-swap policy; AC3 `minimize` default; AC2 empty-state copy pinned; AC4 range-bar as own column). · commit _(pending)_

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
