---
id: M-067
title: Engine + Template Alignment
status: draft
parent: E-25
depends_on: [M-066]
acs:
  - id: AC-1
    title: Engine reflects the m-E25-01 chosen authority
    status: open
  - id: AC-2
    title: Affected shipped templates edited under default parameters
    status: open
  - id: AC-3
    title: ExpectedRunWarnings entries reset to zero
    status: open
  - id: AC-4
    title: Engine + template + baseline reset land in coordinated commits
    status: open
  - id: AC-5
    title: val-warn delta gate added alongside existing run-warn gate
    status: open
  - id: AC-6
    title: Both survey gates green simultaneously
    status: open
  - id: AC-7
    title: No coexistence of old and new authority paths
    status: open
  - id: AC-8
    title: edge_flow_mismatch warnings are zero across shipped templates
    status: open
  - id: AC-9
    title: TDD red-green for engine change
    status: open
  - id: AC-10
    title: Branch coverage on val-warn delta gate
    status: open
  - id: AC-11
    title: Full repo test suite green
    status: open
  - id: AC-12
    title: Phase 2 baseline canary stays green throughout
    status: open
  - id: AC-13
    title: G-032 closes
    status: open
---

## Goal

Implement the edge-flow authority chosen in M-066 inside the engine, edit every affected shipped template so the conservation invariant produces no warnings under default parameters, reset the corresponding `ExpectedRunWarnings` entries to zero, and extend `Survey_Templates_For_Warnings` to gate on `val-warn` delta in addition to its existing `run-warn` gate. After this milestone, every shipped template runs warning-clean and both survey gates are active simultaneously, locking the engine into the new authority before m-E25-03 pins golden fixtures on top.

## Context

M-066 ratifies a `D-NNN` (referenced below as `D-NNN (the m-E25-01 outcome)` until the actual id is allocated and filled into this spec at start time) that names one of three options for edge-flow authority: edge weights win, expr authority wins, or both-must-agree. This milestone implements that choice. The engine implementation footprint is the AC-5 estimate from M-066's decision; if the implementation overruns that estimate by more than a milestone's worth of work, the epic risk mitigation triggers a re-scope conversation rather than allowing this milestone to balloon.

The shipped-template set is enumerated by `Survey_Templates_For_Warnings` in `tests/FlowTime.Integration.Tests/TemplateWarningSurveyTests.cs` and totals 12 templates today. The 9 affected templates with non-zero `ExpectedRunWarnings` baselines are: `it-system-microservices` (1), `manufacturing-line` (1), `network-reliability` (1), `supply-chain-incident-retry` (1), `supply-chain-multi-tier` (2), `supply-chain-multi-tier-classes` (2), `transportation-basic` (1), `transportation-basic-classes` (8), `warehouse-picker-waves` (1). After this milestone all nine baselines are zero.

The Phase 2 baseline canary committed on 2026-05-01 hard-asserts `run-warn` count drift (upward or downward) per template. This milestone takes baselines downward to zero in deliberate, reviewed commits. The canary stays green throughout because each baseline change accompanies the engine + template change that justifies it in the same commit (the canary is designed for exactly this workflow — see the test's xmldoc).

The `val-warn` delta gate is a small extension to the existing test: track validator warnings per template alongside the analyser-warning baseline, and fail the build on any change. This gate addition is the bridge canary the epic calls out as keeping the surface honest until m-E25-03 lands the full golden canary.

## Acceptance criteria

### AC-1 — Engine reflects the m-E25-01 chosen authority

The engine code change implements the authority specified in `D-NNN (the m-E25-01 outcome)`. The cited decision id is filled into this spec, into the implementing PR's description, and into the relevant code comments at the changed sites in `src/FlowTime.Core/`. The change is *exactly* what the decision authorizes — no opportunistic refactoring of `InvariantAnalyzer`, no conservation-tolerance reshaping, no new analyser warning families. Per the epic's "Out of scope" — engine evolution beyond what the design call requires belongs to other epics.

### AC-2 — Affected shipped templates edited under default parameters

Every template enumerated in M-066's footprint analysis as needing edits is edited in `templates/` and runs warning-clean under default parameters at `ValidationTier.Analyse` (the tier the survey canary uses). The set of templates edited equals the set named by the decision's footprint analysis; no template not named in the decision is edited (any divergence is a re-scope conversation). The default-parameter assumption matches what the survey canary uses; the canary's enumeration is the authority.

### AC-3 — `ExpectedRunWarnings` entries reset to zero

For every entry in `ExpectedRunWarnings` (at `tests/FlowTime.Integration.Tests/TemplateWarningSurveyTests.cs:79`) sourced from the G-032 affected-templates table, the entry is removed from the dictionary (the test treats absence as expected-zero per its inline comment "All other templates default to 0"). After the edit, no template in the dictionary has a non-zero baseline unless individually justified with a comment naming the rationale and not stemming from the G-032 cluster — and any such residual is a deferred follow-up gap, not silent retention.

### AC-4 — Engine + template + baseline reset land in coordinated commits

The engine code change, the template edits, and the `ExpectedRunWarnings` reset land such that **at every commit boundary on the milestone branch the test suite is green**. Per the epic's Risks-table mitigation for the Phase 2/golden canary transition: "the implementation milestone lands the engine + template fix + baseline reset in coordinated commits". The mechanic that satisfies this AC: each commit either (a) edits engine + relevant template(s) + relevant `ExpectedRunWarnings` entries together, or (b) is a no-functional-change refactor with no change to canary expectations. Mixing engine change without baseline reset, or baseline reset without engine change, is forbidden.

### AC-5 — `val-warn` delta gate added alongside existing `run-warn` gate

`Survey_Templates_For_Warnings` is extended to also track `val-warn` count per template and fail on drift. The pattern mirrors the existing `run-warn` gate: a baseline dictionary (e.g., `ExpectedValWarnings`) — or a hard zero if the engine + template change reaches `val-warn == 0` for all templates — and an assertion that compares actual to expected with the same kind of clear before/after diagnostic message the existing gate produces. The xmldoc on the test class is updated to describe both gates.

### AC-6 — Both survey gates green simultaneously

After the engine + template + baseline + val-warn-gate changes are merged, `Survey_Templates_For_Warnings` reports both `run-warn` and `val-warn` gates green for all 12 shipped templates. This is a single test invocation that validates both surfaces at once; no separate test runs required.

### AC-7 — No coexistence of old and new authority paths

Per the epic's Constraints — "no coexistence window for the chosen authority". After this milestone, `grep -rn "<old-authority-marker>"` (where `<old-authority-marker>` is the symbol or feature flag specific to the rejected approach, named in M-066's decision) returns zero hits in production code. The engine has one authority, not two with a switch. Any compatibility shim retained must have an explicit deletion criterion captured inline in this milestone spec's Work log — the project's truth-discipline rule against tolerated alternate paths applies in full.

### AC-8 — `edge_flow_mismatch_*` warnings are zero across shipped templates

After the engine + template change, running every shipped template at default parameters produces zero `edge_flow_mismatch_incoming` warnings and zero `edge_flow_mismatch_outgoing` warnings. This is verified via `Survey_Templates_For_Warnings` (the survey aggregates both warning families into the `run-warn` count) and via direct inspection of one representative run artifact (e.g., `transportation-basic` re-run on the milestone branch) confirming the empty `warnings[]` shape that the pre-E-24 reference run `data/runs/run_20260424T150244Z_b2f4c995/run.json` carries.

### AC-9 — TDD red-green for engine change

The engine change follows the project TDD-by-default rule. For the chosen authority, write a failing test that pins the post-change behavior **before** modifying engine code; turn the test green via the engine change; refactor as needed without losing green. The PR diff shows the test added in or before the same commit as the implementation. The test lives where comparable engine-conservation tests already live (typically `tests/FlowTime.Core.Tests/Analysis/`).

### AC-10 — Branch coverage on `val-warn` delta gate

The `val-warn` delta gate addition (AC-5) hits the project's branch-coverage hard rule: every reachable conditional branch added to `TemplateWarningSurveyTests.cs` has a test. Concretely: the gate's "matches expected" path is exercised by the green case; the "drift detected" path is exercised by an inverted-fixture probe (e.g., a unit test that calls into the helper with an injected wrong-baseline and asserts the gate fails with the expected message). Line-by-line audit per the project's hard-rule precedes the commit-approval prompt.

### AC-11 — Full repo test suite green

`dotnet test FlowTime.sln` is green at milestone close. UI suites untouched (this milestone introduces no UI work). No new skipped tests except those explicitly gated on infrastructure that is documented at the skip site. The Engine API smoke (`POST /v1/run` against one representative template) returns warning-clean, matching what the survey canary asserts.

### AC-12 — Phase 2 baseline canary stays green throughout

The existing `run-warn` baseline gate is never loosened. Baselines transition from their 2026-05-01 capture values directly to zero (or to absence from the dictionary) in the same commit that edits the corresponding template + ships the engine change. The canary never reports a state where a baseline was raised, even temporarily.

### AC-13 — G-032 closes

`work/gaps/G-032-…md` status moves from `addressed` (the M-066 partial-close state) to `done` with a reference to this milestone. The epic spec is updated so its "Supersedes / closes" section reflects the closure.

## Constraints

- **D-NNN cite-or-fail.** Every code site in `src/FlowTime.Core/` changed by this milestone carries a comment naming the `D-NNN` that authorized the change. The point is to make the engine self-explain its authority choice to a future reader who walks `git blame`.
- **Forward-only template regeneration.** All affected shipped templates land their edits in this milestone — no "some templates new form, some old form" coexistence. Per the epic Constraints.
- **No coexistence window for the chosen authority.** Per AC-7, the rejected option is not retained as a tolerated alternate path. No feature flag, no compile-time switch, no "preserve for backwards compat" mode unless the decision explicitly authorizes it (and even then with a stated deletion milestone).
- **Phase 2 baseline canary stays green throughout.** Per AC-12 and the epic Constraints.
- **TDD by default.** Per project hard rule. AC-9 codifies it for the engine change; AC-10 codifies it for the test infrastructure change.
- **Branch coverage hard rule.** Per project hard rule and AC-10 — every reachable conditional branch added by this milestone needs a test before declaring done; line-by-line audit before the commit-approval prompt.
- **Project rules.** .NET 9 / C# 13; private fields camelCase without leading underscore; invariant culture; camelCase JSON; no reintroduction of deprecated schema fields.

## Design Notes

- **`val-warn` delta gate baseline shape.** If the engine + template change reaches `val-warn == 0` for all 12 templates, the gate is a hard zero — the test asserts `valWarnCount == 0` for every template with no baseline dictionary needed. If any template legitimately retains a non-zero `val-warn` count after the engine + template change (e.g., a deliberate validator warning that survives the authority change), introduce an `ExpectedValWarnings` dictionary mirroring `ExpectedRunWarnings`'s shape, with each non-zero entry carrying a one-line rationale comment. Decision lives inside this milestone; either shape is acceptable.
- **Coordinated-commit shape.** A clean shape is: commit 1 — engine code + TDD test (AC-9); commit 2 — first batch of template edits + corresponding `ExpectedRunWarnings` resets (e.g., `transportation-basic` and its `-classes` sibling); commit 3 — second batch, etc.; final commit — `val-warn` delta gate + branch-coverage tests. The constraint AC-4 enforces is *test-suite green at every boundary*, not "single commit". Splitting across commits is encouraged for reviewability.
- **D-NNN id substitution.** This spec carries the placeholder `D-NNN (the m-E25-01 outcome)`; at milestone start, replace every occurrence with the actual ratified id from M-066. At planning time the id does not yet exist.
- **Template edit pattern.** For option 1 (edge weights win), each affected template moves its expr-layer split arithmetic into edge weights. For option 2 (expr authority wins), no template edits land at all (the engine change makes the templates correct as-is) — and the AC-2 list collapses to empty. For option 3 (both-must-agree), templates may need both surfaces explicit; the M-066 footprint analysis specifies exactly which templates change. Review the footprint analysis before starting edits.
- **Smoke run for AC-8.** A simple `dotnet run --project src/FlowTime.Cli` invocation against `templates/transportation-basic.yaml` at default parameters, reading the produced `data/runs/<run-id>/run.json` for an empty `warnings[]` shape, is sufficient evidence. The existing survey test is the structural authority; the smoke is for human verification.

## Surfaces touched

- `src/FlowTime.Core/Analysis/InvariantAnalyzer.cs` (likely — under option 2; possibly under option 3) and/or other engine sites the M-066 footprint analysis identifies
- `templates/*.yaml` (under option 1 or 3 — the affected subset from G-032's table)
- `tests/FlowTime.Integration.Tests/TemplateWarningSurveyTests.cs` (`ExpectedRunWarnings` reset; `val-warn` delta gate)
- `tests/FlowTime.Core.Tests/Analysis/` (new TDD test for the engine change)
- `work/gaps/G-032-…md` (status to `done`, reference this milestone)
- `work/epics/E-25-engine-truth-gate/epic.md` (small edit — supersedes/closes update)
- `ROADMAP.md` (regenerated via `aiwf render roadmap --write`)

## Out of scope

- Golden-output canary infrastructure or fixtures (m-E25-03).
- Refactors of `InvariantAnalyzer` beyond what the chosen authority requires (per epic Out of scope).
- Conservation tolerance reshaping (per epic Out of scope).
- New analyser warning families (per epic Out of scope).
- Engine performance work for the chosen authority (per epic Out of scope; followed up in a separate engine-performance epic if needed).
- Migration tooling for user-authored templates outside `templates/` (M-066 AC-6 may file this as a deferred gap if applicable; the tool is not built here).
- UI changes (Svelte or Blazor) — none required by this epic.
- Promotion of the `D-NNN` to ADR — that decision lives in M-066's closeout if it happens at all.

## Dependencies

- **M-066 ratified `D-NNN`** with status `accepted`. Hard prerequisite — this milestone cannot start without the decision in place.
- E-25 epic spec ratified.
- Phase 2 baseline canary committed (2026-05-01) — provides the `run-warn` gate this milestone extends.
- E-24 Schema Alignment closed (2026-04-25) — provides per-edge `flowVolume` series emission that the conservation invariant now reads.

## References

- Epic spec: `work/epics/E-25-engine-truth-gate/epic.md`
- Decision: `D-NNN (the m-E25-01 outcome)` — replace with actual id at milestone start
- Gap: `work/gaps/G-032-transportation-basic-regressed-edge-flow-mismatch-incoming-3-after-e-24-unification.md` — affected-templates table; canonical source for AC-2 and AC-3 enumerations
- Survey canary: `tests/FlowTime.Integration.Tests/TemplateWarningSurveyTests.cs:79` (the `ExpectedRunWarnings` dictionary) and `:340-358` (the assertion shape this milestone extends)
- Analyser source: `src/FlowTime.Core/Analysis/InvariantAnalyzer.cs:323-335` (incoming-edge conservation), `:309-321` (outgoing-edge conservation)
- Pre-E-24 reference run (clean baseline): `data/runs/run_20260424T150244Z_b2f4c995/run.json`
- Post-E-24 reproduction case: `data/runs/run_20260428T165413Z_6ed5974e/run.json`
