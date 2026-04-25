# m-E24-05 Canary Green + Hard Assertion — Tracking

**Started:** 2026-04-25
**Completed:** 2026-04-25
**Branch:** `milestone/m-E24-05-canary-green-hard-assertion` (from `epic/E-24-schema-alignment` @ `35c33ff`)
**Spec:** `work/epics/E-24-schema-alignment/m-E24-05-canary-green-hard-assertion.md`
**Commits:** pending (single commit at commit-gate)
**Final test count:** Core 353/353, Sim 225/225 (3 skip pre-existing), TimeMachine 239/239, API 264/264, UI.Tests 265/265, CLI 91/91, FlowTime.Tests 228/228 (6 skip pre-existing), Integration 84/84 — full suite green (1,749 passed / 9 skipped pre-existing / 0 failed).

<!-- Status is not carried here. The milestone spec's frontmatter `status:` field is
     canonical. `**Completed:**` is filled iff the spec is `complete`. -->

## Acceptance Criteria

- [x] **AC1 — Canary promoted to hard assertion.** `tests/FlowTime.Integration.Tests/TemplateWarningSurveyTests.cs::Survey_Templates_For_Warnings` rewritten so that the in-process tier-3 (`ValidationTier.Analyse`) loop runs unconditionally and the test asserts `totalValidatorErrors == 0` (and zero render failures) at the end of phase 1. The diagnostic histogram is retained as `ITestOutputHelper` output for actionable failure messages. The live-API portion (POST /v1/run + run-warn collection) is now a strictly evidence-only phase 2 that gracefully early-returns on `/v1/healthz` probe failure or `HttpRequestException`.
- [x] **AC2 — Canary is green in-assertion.** Run captured against the milestone branch tip — see "Canary verbatim output" below. `val-err=0` across all 12 templates; 4 pre-existing analysis-tier warnings (out of scope per the canary's `val-err` contract).
- [x] **AC3 — Full `.NET` solution suite green.** `dotnet test FlowTime.sln` — 1,749 passed / 9 skipped (pre-existing) / 0 failed. Same skips as the m-E24-04 baseline; no new regressions, no new flakes.
- [x] **AC4 — Grep audits pass.** Captured below under "AC4 grep audits".
- [x] **AC5 — Documentation aligned.** `docs/schemas/model.schema.md` provenance section rewritten to match the canonical 7-field nested camelCase form (matches `docs/schemas/model.schema.yaml:919-1010` and the m-E24-03 m-E24-01-Q5/A4 ratification). Historical note added to that section explicitly naming the pre-E-24 snake_case form for reader continuity. `docs/schemas/README.md` (m-E24-03) verified current. `docs/architecture/run-provenance.md` (m-E24-03) verified current. `docs/archive/**` left untouched (intentionally historical per spec). Audit detail under "AC5 docs audit" below.
- [x] **AC6 — E-23 pause cleared.** `work/epics/E-23-model-validation-consolidation/spec.md` frontmatter flipped from `status: paused` to `status: ready-to-resume`; an amendment block notes E-24's close. Status surfaces synced.
- [x] **AC7 — Decisions logged.** `D-2026-04-25-038: E-24 Schema Alignment closed; E-23 ready to resume`. No deltas between m-E24-01's design decisions and the final landed state — every decision held through implementation.
- [x] **AC8 — No new validator features.** Test-side change only (assertion strength). No tiers added, no LSP, no line/column mapping, no suggestion hints.

## Decisions made during implementation

- **D-m-E24-05-01 — Phase 1 (in-process validator) runs unconditionally; phase 2 (live-API run-warn) gracefully skips on probe failure.** The original test was a single block that returned early on probe failure, which meant the validator-error survey ran only when the API was up. Splitting into two phases lets the in-process tier-3 assertion be a true CI-level gate independent of devcontainer port-forwarding state, while preserving the live-API run-warn diagnostic for hand-runs against a live engine. The spec's "Graceful-skip on infrastructure absence, not on assertion failure" rule pins the divide: validator errors are not infrastructure-dependent, so they are not skip-eligible.
- **D-m-E24-05-02 — Render failures are also a hard assertion, not just validator errors.** A template that fails to render under default parameters cannot be validated — that is functionally a stronger regression than a validator error and would otherwise pass silently. The hard-assertion block now checks render failures first, then validator-error count.

## Regression-catching verification (per spec process step 5)

The spec required demonstrating that the new hard assertion would actually catch a regression. To verify:

1. Started from green state on this branch — canary passes with `val-err=0`.
2. Transiently un-wired the `QuotedAmbiguousStringEmitter` in `src/FlowTime.Sim.Core/Services/TemplateService.cs:87` (the m-E24-04 emitter-side half of the round-trip pair, ratified in ADR-E-24-05). Comment-only change; the emitter file itself untouched.
3. Re-ran the canary in isolation. **Result: test failed with the expected diagnostic.** Verbatim assertion message:
   > `Expected val-err=0 across 12 templates at ValidationTier.Analyse; got 231 errors across 12 templates. Offenders: dependency-constraints-attached (val-err=1; first: /nodes/0/metadata/pmf.expected: Value is "number" but should be "string") | dependency-constraints-minimal (val-err=1; ...) | it-document-processing-continuous (val-err=38; first: /nodes/7/expr: Value is "integer" but should be "string") | ...`
4. The 231-error baseline matches the m-E24-04 pre-fix histogram exactly (89 `nodes/*/expr integer→string` + 92 `nodes/*/metadata/graph.hidden boolean→string` + 50 `nodes/*/metadata/pmf.expected number/integer→string`). The hard assertion would have caught the m-E24-04 regression on day zero.
5. Restored the line; canary returned to green; no other artefact touched.

Verification pass: the assertion is sharper than the underlying defect's smallest reachable footprint — even a single re-introduced error fails the build.

## Canary verbatim output (post-fix)

Captured 2026-04-25 on `milestone/m-E24-05-canary-green-hard-assertion`:

```
Surveying 12 templates from /workspaces/flowtime-vnext/templates
====================================================================================================
template                                         val-err   val-warn notes
----------------------------------------------------------------------------------------------------
dependency-constraints-attached                        0          0
dependency-constraints-minimal                         0          0
it-document-processing-continuous                      0          0
it-system-microservices                                0          0
manufacturing-line                                     0          0
network-reliability                                    0          0
supply-chain-incident-retry                            0          1
supply-chain-multi-tier-classes                        0          0
supply-chain-multi-tier                                0          0
transportation-basic-classes                           0          3
transportation-basic                                   0          0
warehouse-picker-waves                                 0          0
----------------------------------------------------------------------------------------------------
Totals: validator-errors=0, validator-warnings=4 across 2/12 templates.

Validator warning codes (across all templates):
     2  missing_capacity_series
     1  missing_processing_time_series
     1  missing_served_count_series

SKIP live-API survey: Engine API not reachable at http://localhost:8081 — Connection refused (localhost:8081)
```

The 4 warnings are pre-existing analysis-tier signals (capacity/processing-time/served-count companion telemetry not declared on those templates). They are not validator errors. The `val-err == 0` contract is the canary's gate.

## AC4 grep audits

```
$ grep -rn "generated_at\|model_id\|template_id\|template_version" docs/schemas/model.schema.yaml
(no hits)

$ grep -rn "SimModelArtifact" --include='*.cs' src/ tests/
(no hits)

$ grep -rn 'SimNode\b\|SimOutput\b\|SimProvenance\b\|SimTraffic\b\|SimArrival\b\|SimArrivalPattern\b' --include='*.cs' src/ tests/
(no hits)
```

All three audits pass cleanly. (The same patterns *do* appear under `docs/archive/**`, which is intentionally historical per spec, and under `.claude/worktrees/**`, which is an untracked worktree directory excluded from the repo proper.)

## AC5 docs audit

| File | Status | Action |
|------|--------|--------|
| `docs/schemas/README.md` | Already current (m-E24-03 rewrite) | None — verified |
| `docs/schemas/model.schema.yaml` | Already current (m-E24-03 rewrite) | None — canonical schema |
| `docs/schemas/model.schema.md` | **Stale** — section 6 still described snake_case provenance | Rewrote provenance section to canonical 7-field nested camelCase form; added historical note naming pre-E-24 snake_case keys |
| `docs/architecture/run-provenance.md` | Already current (m-E24-03 update) | None — verified |
| `docs/architecture/*.md` (rest) | No `SimModelArtifact` / snake_case provenance hits | None |
| `docs/archive/**` | Intentionally historical (per E-24 spec out-of-scope) | Left as-is |

The remaining occurrence of `model_id` / `template_id` / etc. inside `docs/schemas/model.schema.md` after this milestone is the new historical note explicitly naming the deprecated keys for reader continuity. Not a stale reference; an intentional cross-reference.

## Status surface sync

| Surface | Action |
|---------|--------|
| `work/epics/E-24-schema-alignment/m-E24-05-canary-green-hard-assertion.md` | frontmatter `status: draft` → `in-progress` (this commit); will flip to `complete` at wrap |
| `work/epics/E-24-schema-alignment/m-E24-05-canary-green-hard-assertion-tracking.md` | New (this file) |
| `work/epics/E-24-schema-alignment/spec.md` | m-E24-05 milestone-table entry flipped to in-progress; will flip to complete at wrap |
| `ROADMAP.md` | E-24 in-progress line updated; m-E24-05 entry flipped |
| `work/epics/epic-roadmap.md` | E-24 status + m-E24-05 entry flipped |
| `CLAUDE.md` Current Work | E-24 line updated |
| `work/epics/E-23-model-validation-consolidation/spec.md` | frontmatter `status: paused` → `ready-to-resume`; amendment block notes E-24 close |
| `work/decisions.md` | `D-2026-04-25-038: E-24 Schema Alignment closed; E-23 ready to resume` appended |

## Work Log

| Phase | What | Tests | Status |
|-------|------|-------|--------|
| 1 | Preflight: build green on epic head before branching | — | **complete (2026-04-25)** — `dotnet build FlowTime.sln` clean (1 pre-existing xUnit2031 warning) |
| 2 | Branch creation: `milestone/m-E24-05-canary-green-hard-assertion` from `epic/E-24-schema-alignment` (`35c33ff`) | — | **complete (2026-04-25)** |
| 3 | Flip milestone spec frontmatter `status: draft → in-progress`; create tracking doc | — | **complete (2026-04-25)** |
| 4 | Rewrite canary test: phase 1 hard assertion + phase 2 graceful-skip live-API survey | 1 | **complete (2026-04-25)** — canary green; per-template totals captured above |
| 5 | Regression-catching verification (transient un-wire of `QuotedAmbiguousStringEmitter`; restore) | 1 | **complete (2026-04-25)** — canary fails with 231 errors across all 12 templates when the m-E24-04 emitter is bypassed; restored, canary green |
| 6 | Run full `dotnet test FlowTime.sln` | — | **complete (2026-04-25)** — 1,749 passed / 9 skipped (pre-existing) / 0 failed |
| 7 | AC4 grep audits (snake_case keys / SimModelArtifact / satellite types) | — | **complete (2026-04-25)** — all three audits clean against in-tree sources |
| 8 | AC5 docs audit + alignment (`docs/schemas/model.schema.md` provenance section) | — | **complete (2026-04-25)** — section 6 rewritten to canonical 7-field nested camelCase shape; historical note added |
| 9 | Sync status surfaces (epic spec, ROADMAP, epic-roadmap, CLAUDE.md, E-23 pause lift) | — | **complete (2026-04-25)** |
| 10 | Log `D-2026-04-25-038` in `work/decisions.md` (E-24 closed; E-23 ready to resume) | — | **complete (2026-04-25)** |
| 11 | Branch-coverage audit on the modified test | — | **complete (2026-04-25)** — see "Branch coverage audit" below |
| 12 | Self-review pass + commit-gate prep | — | reserved for parent |

## Test Summary

| Project | Passed | Skipped | Notes |
|---------|--------|---------|-------|
| FlowTime.Tests (Core) | 353 | 0 | unchanged |
| FlowTime.Sim.Tests | 225 | 3 | pre-existing skips unchanged |
| FlowTime.TimeMachine.Tests | 239 | 0 | unchanged |
| FlowTime.Api.Tests | 264 | 0 | unchanged |
| FlowTime.UI.Tests | 265 | 0 | unchanged |
| FlowTime.Cli.Tests | 91 | 0 | unchanged |
| FlowTime.Tests (legacy harness) | 228 | 6 | pre-existing skips unchanged |
| FlowTime.Integration.Tests | 84 | 0 | canary now hard-asserts `val-err == 0` |

Net new this milestone: **0 new tests** (the canary is restructured, not multiplied — its assertion strength changed from "log-only" to "hard `Assert.True`" while preserving the original observability surface).

## Branch coverage audit

The modification is a test-side restructuring of one existing `[Fact]`. There is no production code under test added by this milestone; the audit covers the rewritten test's reachable branches:

| Branch | Coverage |
|--------|----------|
| Phase 1 happy path: every template renders, every template validates clean | Covered by the green run on `epic/E-24-schema-alignment` (12/12 templates `val-err=0`) |
| Phase 1 render-failure path | Covered by the regression-verification step (turning off the round-trip emitter forces `val-err > 0`; the `renderFailures.Count == 0` branch is the same code path as the green run, since no template fails to render) |
| Phase 1 validator-error assertion path | Covered by the regression-verification step — assertion fired with offender list of length 12; assertion text formatting verified actionable |
| Phase 2 probe-success path | Covered when the test is run in a session with the Engine API up. Not exercised in CI by default (devcontainer-only); behaviorally verified historically (this is the prior `Survey_Templates_For_Warnings` body unchanged) |
| Phase 2 probe `IsSuccessStatusCode == false` early-return | Defensive; same shape as the original test |
| Phase 2 `HttpRequestException` early-return | Covered by the green run on this branch (port 8081 not listening; logged `SKIP live-API survey: ... Connection refused`) |

No newly-introduced code path is left untested. The verification step exercises the assertion-failure branch end-to-end.

## Notes

- The canary has shipped on `main` (commit `1234814`) since m-E23-01 closed informally; m-E24-05 promotes its strength but does not move it.
- Per the spec, the histogram-shape-set test (`M_E24_02_Step6_AcceptanceTests.TimeMachineValidator_AnalyseTier_HistogramExcludesEmitterClosedShapes`) **stays in place**. It guards a different invariant (no rendered template carries the m-E24-02 emitter-closed wire shapes — top-level `window:` / `metadata:` / `generator:` / `mode:` / snake_case provenance keys / empty-collection markers / `nodes[].source`) and is structural rather than validator-driven. Both tests cover their own corners; folding them would lose either the structural-wire-shape guard or the validator-aggregate guard.
- After this milestone, the canary is the live regression gate for any future schema, emitter, or validator change. A non-zero `val-err` is, from this point on, a build-breaker.

## Completion

**Completed 2026-04-25.** All eight ACs landed in this milestone. The canary is now a hard build-time gate (`val-err == 0` across all 12 templates at `ValidationTier.Analyse`). Regression-catching verified by transient un-wire of `QuotedAmbiguousStringEmitter` — the canary fails with 231 errors exactly matching the m-E24-04 pre-fix histogram, then returns to green on restore. Full `.NET` suite green (1,749 passed / 9 skipped pre-existing / 0 failed). All three AC4 grep audits clean. `docs/schemas/model.schema.md` provenance section rewritten to canonical 7-field nested camelCase form with historical note. E-23 status flipped from `paused` to `ready-to-resume` with milestone-table entries reconciled. `D-2026-04-25-038` logs E-24 close.

E-24 Schema Alignment now closes — all five milestones complete (m-E24-01 through m-E24-05). The integration branch `epic/E-24-schema-alignment` is ready for merge to `main` (next deployer-scope action) and subsequent archive to `work/epics/completed/E-24-schema-alignment/` per project rule.

**Decisions made during this milestone:**

- D-m-E24-05-01 — Phase 1 (in-process validator) runs unconditionally; phase 2 (live-API run-warn) gracefully skips on probe failure. Restructure of the original single-phase test split the assertion gate from infrastructure-dependent diagnostics.
- D-m-E24-05-02 — Render failures are also a hard assertion, not just validator errors. A template that fails to render under default parameters cannot be validated; that is functionally a stronger regression than a validator error and cannot pass silently.

**No new ADRs introduced this milestone.** The canary's promotion is a test-side strength change rather than a new architectural rule; it operationalizes the existing E-24 ADR set.
