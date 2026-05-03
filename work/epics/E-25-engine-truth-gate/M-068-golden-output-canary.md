---
id: M-068
title: Golden-Output Canary
status: draft
parent: E-25
depends_on: [M-067]
acs:
  - id: AC-1
    title: Canary infrastructure lands as a sibling test class
    status: open
    tdd_phase: red
  - id: AC-2
    title: Fixture serialization format chosen and documented
    status: open
  - id: AC-3
    title: Per-fixture directory layout documented in docs/testing/golden-output-canary.md
    status: open
  - id: AC-4
    title: Numeric tolerance documented and applied
    status: open
    tdd_phase: red
  - id: AC-5
    title: Initial pinning across all 12 shipped templates
    status: open
    tdd_phase: red
  - id: AC-6
    title: Coverage equivalence with Survey_Templates_For_Warnings enforced
    status: open
    tdd_phase: red
  - id: AC-7
    title: Sanctioned regeneration workflow exists and is documented
    status: open
    tdd_phase: red
  - id: AC-8
    title: Deliberate-perturbation failure-mode test fires
    status: open
    tdd_phase: red
  - id: AC-9
    title: docs/testing/golden-output-canary.md committed
    status: open
  - id: AC-10
    title: Both Phase 2 gates and golden canary green simultaneously
    status: open
  - id: AC-11
    title: Branch coverage on canary infrastructure
    status: open
  - id: AC-12
    title: Full repo test suite green
    status: open
  - id: AC-13
    title: G-033 closes
    status: open
  - id: AC-14
    title: Epic E-25 spec references docs/testing/golden-output-canary.md as the canary contract
    status: open
---

## Goal

Stand up a golden-output canary that pins, for every shipped template, the engine's full output bundle (per-series per-bin numeric values within tolerance, full warning set exactly, run manifest summary fields) at a sanctioned baseline. Build the regeneration workflow, capture the initial fixtures, document the canary's contract, and prove the canary fires under deliberate engine perturbation. After this milestone, silent numeric drift inside a stable warning count cannot slip through CI.

## Context

The Phase 2 baseline canary committed on 2026-05-01 (D-053) hard-asserts per-template `run-warn` count drift. M-067 extends it with a `val-warn` delta gate. Between them they catch any change in the *number* of warnings per template. They do not catch numeric drift inside a stable warning count — a change in a series value, an off-by-one in lag arithmetic, a tolerance inversion that shifts every bin by 0.01% — none of those move the warning count, so none of them fire either gate. G-033 is the structural argument for closing this hole with a deterministic golden-output canary.

The canary's pinning target is the **observable engine output** (per-series per-bin values + warning set + run manifest summary fields), not the compiled DAG IR. The IR can change freely as long as the observable output stays equivalent. Per the epic Constraints — fixture serialization is a milestone-internal design choice with one constraint: pinned fixtures must produce reviewable PR diffs (JSON-with-stable-key-order is the strawman; CSV+JSON-warnings is an alternative; opaque-binary is forbidden).

The shipped-template set is the 12 templates enumerated by `Survey_Templates_For_Warnings` in `tests/FlowTime.Integration.Tests/TemplateWarningSurveyTests.cs`. Per the epic success criteria, the set of templates pinned by the golden canary must equal the set enumerated by the survey canary; coverage drift between them is itself a build failure. M-067 lands the engine in a state where every template runs warning-clean; this milestone captures that state as the golden baseline.

This milestone's sequencing rationale: building the canary on top of an engine whose output is now trustworthy means the pinned fixtures capture *truth-as-decided* rather than *truth-with-known-asterisks*. If the canary were built before M-067, every engine fix would tax the regeneration workflow as the team unwound the legacy state.

The architecture note describing the edge-flow authority decision is M-067's deliverable (or M-066's, depending on how the team chose to record). This milestone owns the testing-rigor documentation: `docs/testing/golden-output-canary.md`.

## Acceptance criteria

### AC-1 — Canary infrastructure lands as a sibling test class

A new test class lives in `tests/FlowTime.Integration.Tests/` (sibling to `TemplateWarningSurveyTests`), runs each shipped template at the same default parameters the survey canary uses, captures the engine output bundle, and compares it to a pinned fixture. The test is xUnit `[Theory]`-shaped with one test case per template, so a failure on one template does not mask drift on another. The test runs under `dotnet test FlowTime.sln` by default — no environment-variable opt-in required for the assertion path. (The regeneration path is opt-in per AC-7.)

### AC-2 — Fixture serialization format chosen and documented

The canary's fixture serialization format is decided inside this milestone and documented in `docs/testing/golden-output-canary.md`. The decision honors the epic Constraint: *fixtures produce reviewable PR diffs*. JSON-with-stable-key-order is the strawman; alternatives (CSV+JSON-warnings, separate-file-per-series, etc.) are acceptable if reviewability is preserved and the choice is justified. The format choice is recorded in the testing note with a short rationale paragraph.

### AC-3 — Per-fixture directory layout and README

Each pinned template gets a directory at `tests/fixtures/golden-templates/<template-id>/` containing the serialized fixture (per AC-2's chosen format) plus a `README.md` naming **the parameter set used at capture, the capture date, the capture commit hash**. The README is human-readable and stands alone — a future engineer browsing the fixtures directory understands what each fixture pins without external context. The directory naming uses the same `<template-id>` convention as the survey canary's enumeration.

### AC-4 — Numeric tolerance documented and applied

The per-bin numeric comparison uses an explicit tolerance documented in `docs/testing/golden-output-canary.md` and in code comments at the comparison site. The starting candidate per the epic spec is **relative tolerance 1e-9**; this milestone may revisit empirically based on observed bin-to-bin variance under repeat runs of known-clean templates. Any per-template tolerance widening is documented with rationale in that template's fixture README. The tolerance must be tight enough that the deliberate-perturbation test (AC-8) fires; loose enough that benign engine work doesn't tax the regeneration workflow constantly.

### AC-5 — Initial pinning across all 12 shipped templates

A pinned fixture exists under `tests/fixtures/golden-templates/<template-id>/` for every template in `templates/*.yaml` enumerated by the survey canary (12 today: `dependency-constraints-attached`, `dependency-constraints-minimal`, `it-document-processing-continuous`, `it-system-microservices`, `manufacturing-line`, `network-reliability`, `supply-chain-incident-retry`, `supply-chain-multi-tier`, `supply-chain-multi-tier-classes`, `transportation-basic`, `transportation-basic-classes`, `warehouse-picker-waves`). All twelve canary cases are green at milestone close. Templates not in the survey enumeration are not in the canary either (the equivalence is enforced by AC-6).

### AC-6 — Coverage equivalence with `Survey_Templates_For_Warnings` enforced

The canary asserts at runtime — or via a sibling test — that the set of template ids it covers equals the set enumerated by `Survey_Templates_For_Warnings`. Drift in either direction (a template added to one but not the other) fails the build with a clear message naming the missing or extra ids. This is the "coverage drift is itself a build failure" success criterion from the epic spec, made concrete.

### AC-7 — Sanctioned regeneration workflow exists and is documented

An engineer can run a documented command (e.g., `dotnet test --filter "Category=GoldenOutput" -- RunSettings.UpdateFixtures=true`, or an equivalent shape — exact form is a milestone design choice) and re-capture the pinned bundles. The resulting diff is the PR-review artifact — reviewers read the fixture diff to validate the engine change. The opt-in flag must be explicit; an accidental invocation must not silently regenerate fixtures. The workflow is documented step-by-step in `docs/testing/golden-output-canary.md` including a worked example of a typical regeneration cycle (engine change → run regen → review diff → commit).

### AC-8 — Deliberate-perturbation failure-mode test fires

A unit test (e.g., `tests/FlowTime.Integration.Tests/GoldenOutputPerturbationTests.cs` or analogous location) deliberately perturbs the engine output — concretely: alters a per-bin value in a captured-bundle fixture by an amount larger than the AC-4 tolerance, or appends a fake warning to the warning set — and asserts the canary's comparison logic fires. This is the epic risk-table mitigation for "tolerance is set too loose": the test proves the canary catches what it claims to catch. Two perturbation cases minimum: one per-bin numeric drift, one warning-set drift.

### AC-9 — `docs/testing/golden-output-canary.md` committed

A new doc at `docs/testing/golden-output-canary.md` covers: (1) what the canary asserts and what it does not (it does not pin compilation IR; it does not pin per-bin variance below tolerance); (2) the fixture format (AC-2); (3) the regeneration workflow (AC-7) with worked example; (4) the meaning of a green build and a red build; (5) tolerance values (AC-4) with empirical rationale; (6) a brief note on the relationship to `Survey_Templates_For_Warnings` (the gates layer — survey catches count drift, golden catches numeric drift). The doc is reviewable by a contributor unfamiliar with the canary; it does not assume context from this epic spec.

### AC-10 — Both Phase 2 gates and golden canary green simultaneously

After this milestone merges, all three gates are active simultaneously: `Survey_Templates_For_Warnings`'s `run-warn` baseline gate, its `val-warn` delta gate (added in M-067), and the new golden-output canary. All three are green for all 12 shipped templates in `dotnet test FlowTime.sln`. There is no test case where one gate is loosened to accommodate another; per the epic Risk-table mitigation, "the implementation milestone lands ... in coordinated commits" applies here too — the golden fixtures are captured against the post-M-067 engine state, so the gates do not develop conflicting expectations.

### AC-11 — Branch coverage on canary infrastructure

Per project hard rule. Every reachable conditional branch in the canary's comparison logic, the regeneration-mode branch, and the coverage-equivalence assertion (AC-6) has a test. Concretely: comparison-pass path, comparison-fail path (covered by AC-8), regen-on path, regen-off path, coverage-equivalence-match path, coverage-equivalence-mismatch path. Line-by-line audit before the commit-approval prompt.

### AC-12 — Full repo test suite green

`dotnet test FlowTime.sln` is green at milestone close. UI suites untouched. No new skipped tests except those gated on infrastructure documented at the skip site (e.g., `FLOWTIME_E2E_TEST_RUNS=1` style). Engine API smoke against one representative template still returns a clean run (the canary itself exercises this for all 12 templates).

### AC-13 — G-033 closes

`work/gaps/G-033-tests-are-too-weak-…md` status moves to `done` with a reference to this milestone. The epic spec is updated so its "Supersedes / closes" section reflects the closure.

### AC-14 — Epic closure housekeeping complete

On merge to main: the epic frontmatter is promoted to `status: done` via `aiwf promote E-25 done`; a wrap artefact at `work/epics/E-25-engine-truth-gate/wrap.md` captures what shipped, the pinned-fixture catalog state (12 templates pinned, capture commit, capture date), and any deferred follow-ups; `ROADMAP.md` is regenerated via `aiwf render roadmap --write`. (Epic dirs stay in place under `work/epics/E-NN-<slug>/` regardless of status — aiwf v3's truth surface is the frontmatter, not the path. The pre-aiwf v1 `work/epics/completed/` convention does not apply.)

## Constraints

- **Reviewable PR diffs.** Per the epic Constraints — fixture format must produce meaningful PR diffs, not opaque binary. AC-2 codifies the choice; the choice is bound by this constraint.
- **Pinned artifact is observable output, not IR.** Per the epic Out of scope — the fixture pins per-series per-bin values + warning set + manifest summary fields. The compiled DAG IR may change freely as long as the observable output stays equivalent. The fixture format must not include IR (no compiled-graph dumps, no internal state captures).
- **One sanctioned parameter set per template.** Per the epic Out of scope — coverage of non-default parameter combinatorics is future scope. This milestone pins one parameter set per template (the same default the survey canary uses). Sweeps, randomized inputs, fuzz-style coverage do not land here.
- **Canonical capture environment is documented.** Per the epic Risk-table mitigation for cross-platform numeric variance — the milestone documents which environment is canonical for fixture capture (typically the project devcontainer or CI environment) and the testing note names it. Cross-platform variance is widened tolerance per-template only with empirical evidence, not pre-emptively.
- **No loosening of existing gates.** The Phase 2 `run-warn` gate and the M-067 `val-warn` delta gate stay exactly as they are. The golden canary is additive; it does not replace either.
- **Forward-only fixture format.** Once chosen, the fixture format does not develop a v1/v2 coexistence window. If the format proves wrong, regenerate all fixtures in a single change; do not maintain a parallel reader.
- **TDD by default.** Per project hard rule. The deliberate-perturbation tests (AC-8) double as the red test for the canary's comparison logic — write the perturbation test first, watch it fail (because comparison logic doesn't yet exist), implement comparison, watch it pass.
- **Branch coverage hard rule.** Per project hard rule and AC-11.
- **Project rules.** .NET 9 / C# 13; private fields camelCase without leading underscore; invariant culture; camelCase JSON; if the chosen format is JSON, key order must be stable across captures.

## Design Notes

- **Format-choice strawman: JSON-with-stable-key-order.** Stable key order means the same template captured twice produces byte-identical JSON. This is achievable by sorting object keys at serialize time and emitting arrays in deterministic order (e.g., series sorted by series-id). The PR-diff property follows directly. CSV+JSON-warnings is an alternative if per-series-per-bin values dominate the file size — but mixing two formats per fixture adds reader complexity. Recommend committing to one format unless empirical evidence justifies the split.
- **Capture command shape.** A reasonable shape: `dotnet test --filter "FullyQualifiedName~GoldenOutputCanary" -- RunConfiguration.TestSessionTimeout=600000 RunSettings.UpdateFixtures=true`. Alternative shapes (a sibling CLI tool, an MSBuild target) are acceptable; one-shot shell-runnable matters more than the exact form.
- **Coverage-equivalence assertion location.** Two reasonable homes: (a) a sibling test class `GoldenOutputCoverageEquivalenceTests` that runs alongside and reads both enumerations; (b) an assertion baked into the golden canary's setup that the union of templates it knows about matches `Survey_Templates_For_Warnings`'s enumeration. Either works; option (a) keeps the failure message clean.
- **Deliberate-perturbation test mechanic.** Two reasonable mechanics: (a) write a fixture-corruption helper that takes a captured fixture and applies a synthetic perturbation; assert the comparison logic flags it. (b) run the engine with a deliberately-modified template that produces a slightly-different output; assert the canary fails. Mechanic (a) is faster and tests the comparison logic directly; mechanic (b) tests the full pipeline. Recommend (a) for the AC-8 test; (b) is overkill.
- **Fixture-directory README template.** A short, three-section template:
  ```
  # <template-id>

  ## Parameters
  Default parameters as enumerated by Survey_Templates_For_Warnings.

  ## Capture
  - Date: 2026-MM-DD
  - Commit: <40-char sha>
  - Environment: devcontainer (linux-x64, .NET 9.x)
  - Tolerance: relative 1e-9 (default) | <per-template override + rationale>
  ```
  Apply consistently across all 12 fixtures so a future engineer can scan them at a glance.
- **`docs/testing/` is a new directory.** No existing testing-doc lives there today (testing notes today live inline in `CLAUDE.md` or in epic specs). Creating the directory in this milestone is intentional: testing-rigor documentation has a clear home going forward.

## Surfaces touched

- `tests/FlowTime.Integration.Tests/GoldenOutputCanary.cs` (new — the canary `[Theory]`)
- `tests/FlowTime.Integration.Tests/GoldenOutputPerturbationTests.cs` (new — the deliberate-perturbation failure-mode test)
- `tests/FlowTime.Integration.Tests/GoldenOutputCoverageEquivalenceTests.cs` (new, optional — the coverage-equivalence assertion if it lives separately)
- `tests/fixtures/golden-templates/<template-id>/` × 12 (new — one directory per shipped template, each with the serialized fixture and a README)
- `docs/testing/golden-output-canary.md` (new — the canary contract doc)
- `work/epics/E-25-engine-truth-gate/m-E25-03-golden-output-canary-tracking.md` (new — branch-coverage audit, fixture-format decision rationale, perturbation-test catalogue)
- `work/gaps/G-033-tests-are-too-weak-…md` (status to `done`, reference this milestone)
- `work/epics/E-25-engine-truth-gate/epic.md` (small edit — supersedes/closes update)
- `work/epics/E-25-engine-truth-gate/wrap.md` (new — epic closure artefact)
- `ROADMAP.md` (regenerated)

## Out of scope

- Coverage of non-default parameter combinatorics (sweeps, randomized inputs, fuzz-style coverage) — explicitly future scope per the epic Out of scope.
- Pinning intermediate compilation IR — explicitly future scope per the epic Out of scope.
- Cross-platform fixture capture (different OSes / architectures producing differently-pinned fixtures) — the canonical environment is documented per the epic Risk-table mitigation; no multi-environment capture matrix lands here.
- UI surfacing of the canary — failures appear in CI per the epic Out of scope.
- Historical run-artifact retention policy — moot once this canary lands per the epic Out of scope. The reproduction artifacts under `data/runs/` from the original investigation can be cleaned up in a follow-up if desired, but no policy is codified here.
- Engine code change (M-067 territory). The canary captures the post-M-067 state; it does not modify engine behavior.
- Architecture note for the edge-flow authority decision — M-066 / M-067 territory.

## Dependencies

- **M-067 merged**, with both survey gates green and the engine + template state warning-clean across all 12 shipped templates. Hard prerequisite — fixtures captured against pre-M-067 state would pin known-bad output.
- M-066 ratified `D-NNN`. (Inherited via M-067 dependency.)
- E-25 epic spec ratified.
- E-24 Schema Alignment closed (provides the per-edge `flowVolume` series the conservation invariant reads; the same series the golden canary pins).

## References

- Epic spec: `work/epics/E-25-engine-truth-gate/epic.md`
- Gap: `work/gaps/G-033-tests-are-too-weak-surveyed-output-only-canaries-cannot-detect-drift-need-deterministic-golden-output-assertions.md` — structural argument for the canary, proposed shape
- Decision: `work/decisions/D-053-testing-rigor-approach-phase-2-baseline-canary-first-full-golden-output-canon-deferred.md` — the deferred-canon scope this milestone picks up
- Phase 2 baseline canary: `tests/FlowTime.Integration.Tests/TemplateWarningSurveyTests.cs:79` (the `ExpectedRunWarnings` dictionary) and `:340-358` (the assertion shape this canary layers on top of)
- Shipped templates enumeration: `templates/*.yaml` (12 files; the canonical list)
- Pre-E-24 reference run (model of clean output): `data/runs/run_20260424T150244Z_b2f4c995/run.json`
