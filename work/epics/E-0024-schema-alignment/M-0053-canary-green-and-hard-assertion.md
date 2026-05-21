---
id: M-0053
title: Canary Green and Hard Assertion
status: done
parent: E-0024
acs:
  - id: AC-1
    title: Canary promoted to hard assertion
    status: met
  - id: AC-2
    title: Canary is green in-assertion
    status: met
  - id: AC-3
    title: Full .NET solution suite green
    status: met
  - id: AC-4
    title: Grep audits pass
    status: met
  - id: AC-5
    title: Documentation aligned
    status: met
  - id: AC-6
    title: E-0023 pause is cleared
    status: met
  - id: AC-7
    title: Decisions logged
    status: met
  - id: AC-8
    title: No new validator features
    status: met
---

## Goal

Close E-0024. Promote `TemplateWarningSurveyTests.Survey_Templates_For_Warnings` from an informational diagnostic to a hard-asserting regression guard: a non-zero `val-err` count fails the build. Run the full `.NET` solution suite to confirm zero regressions. Update any documentation that still describes the pre-E-0024 schema shape. E-0023 becomes ready to resume with a byte-trivial M-0047 + M-0048.

## Context

By this milestone:

- M-0050 unified `SimModelArtifact` + `ModelDefinition` into a single type and deleted the Sim-side satellite types.
- M-0051 realigned `docs/schemas/model.schema.yaml` to describe the unified type in camelCase and rewrote `docs/schemas/README.md`.
- M-0052 fixed `ParseScalar` in both validators.

The canary reported `val-err=0` at M-0052's wrap. This milestone makes that zero a permanent assertion and verifies every other test path is unaffected. After this milestone, schema-reality convergence is a live, enforced property of the build.

## Acceptance criteria

### AC-1 — Canary promoted to hard assertion

**Canary promoted to hard assertion.** `tests/FlowTime.Integration.Tests/TemplateWarningSurveyTests.cs` is modified so `Survey_Templates_For_Warnings` asserts `val-err == 0` for every template at `ValidationTier.Analyse`. The current informational logging (`Totals: validator-errors=..., ...`) is retained for diagnostic visibility, but a non-zero count now fails the assertion. Graceful-skip behavior when port 8081 is unreachable is preserved (follows the existing `SkipUnless` / health-probe pattern).
### AC-2 — Canary is green in-assertion

**Canary is green in-assertion.** Run the canary once against a live Engine API after M-0052's close. Confirm `val-err=0` across all twelve templates. The tracking doc captures the verbatim "Totals" output as evidence.
### AC-3 — Full .NET solution suite green

**Full `.NET` solution suite green.** `dotnet test FlowTime.sln` passes. All test assemblies report zero failures. Any pre-existing flakes (`RustEngine_CleansUpTempDirectory_OnFailure`, `SessionModelEvaluatorIntegrationTests.Dispose_TerminatesSubprocess` per M-0046 baselines) remain the same flakes with the same transient-timing signature — no new timing sensitivity introduced by E-0024.
### AC-4 — Grep audits pass

**Grep audits pass.** The following audits are captured in the tracking doc:
- `grep -rn "generated_at\|model_id\|template_id\|template_version" docs/schemas/model.schema.yaml` returns zero hits.
- `grep -rn "SimModelArtifact" --include='*.cs'` returns zero hits.
- `grep -rn "SimNode\b\|SimOutput\b\|SimProvenance\b\|SimTraffic\b\|SimArrival\b\|SimArrivalPattern\b" --include='*.cs'` returns zero hits.
### AC-5 — Documentation aligned

**Documentation aligned.** Any architecture doc (`docs/architecture/*`) that still describes the pre-E-0024 two-type / two-schema shape is updated to reflect the post-E-0024 unified reality. Historical descriptions move to `docs/archive/` if they are still useful as history, or are deleted. `docs/schemas/README.md` (rewritten in M-0051) is verified current. The tracking doc lists every audited file and its disposition.
### AC-6 — E-0023 pause is cleared

**E-0023 pause is cleared.** `work/epics/E-0023-model-validation-consolidation/spec.md` status flips from `paused` to `ready-to-resume` (or to `in-progress` — reviewer choice). The E-0023 spec's amendment notes E-0024's close and points to the canary green assertion as the entry condition for M-0047. Same status-surface sweep across `ROADMAP.md`, `work/epics/epic-roadmap.md`, and `CLAUDE.md`.
### AC-7 — Decisions logged

**Decisions logged.** A new decision entry records E-0024's close and notes any deltas between M-0049's design decisions and the final landed state (there should be none if the milestones executed as planned; if any, they are documented as sub-decisions). Candidate ID: `D-2026-MM-DD-NNN: E-0024 Schema Alignment closed; E-0023 ready to resume`.
### AC-8 — No new validator features

**No new validator features.** The canary's promotion is the only behavior change here. No new tiers, no line/column mapping, no suggestion hints. Out-of-scope work remains out.
## Constraints

- **Assertion replaces informational log, not augments it.** The test fails the build on non-zero `val-err`. If reviewers want the diagnostic log to survive, it survives as stdout; the assertion is the gate.
- **Zero-tolerance in the assertion.** `val-err == 0`. Not `val-err < 5`, not "every residual matches the C-defect shape" (the amended M-0046 AC4 phrasing) — by the time M-0053 runs, the C-defect is fixed by M-0052. A template producing any validator error is a regression.
- **Graceful-skip on infrastructure absence, not on assertion failure.** If the API is unreachable, `Skip(...)` with a clear message is correct. If the API is reachable and a template reports non-zero `val-err`, the test fails. Do not use `Skip` to mask assertion failures.
- **No new test infrastructure.** The canary remains in `tests/FlowTime.Integration.Tests` using the existing health-probe-and-skip pattern shared with Rust engine integration tests. Do not move it to a new project or harness.
- **E-0023 pause lift is coordinated.** Flip E-0023 status only after M-0053's assertion is green on a live run and the full suite is green. The status-surface sync is atomic — all surfaces (spec, tracking, roadmap, epic-roadmap, CLAUDE.md) flip in one pass.

## Design Notes

- The assertion pattern matches the project's integration-test idiom: `Assert.True(totals.ValErr == 0, $"Expected val-err=0, got {totals.ValErr}. Details: {totals}");` — include the template-by-template first-err detail in the message so a failure is actionable.
- Documentation updates: walk `docs/` for references to the snake_case provenance shape, the `SimModelArtifact`-on-the-wire pattern, or the two-schema world. Update what is still current. Archive or delete what is not.
- The E-0023 status-surface update is a planner-scope operation (status-surface reconciliation); the actual M-0047 restart is a separate start-milestone action in the next session.
- After this milestone, the stashed M-0046 input material (on `milestone/m-E23-01-schema-alignment`) becomes historical — its content has been absorbed by E-0024's milestones and the canary green state. The branch can be retired when E-0023 resumes.

## Surfaces touched

- `tests/FlowTime.Integration.Tests/TemplateWarningSurveyTests.cs` (assertion promotion)
- `work/epics/E-0023-model-validation-consolidation/spec.md` (status flip)
- `ROADMAP.md` (E-0023 status flip, E-0024 completion note)
- `work/epics/epic-roadmap.md` (E-0023 + E-0024 status sync)
- `CLAUDE.md` (Current Work section update)
- `work/decisions.md` (E-0024 close entry)
- `docs/` (documentation alignment — specific files identified in M-0053's tracking doc after audit)
- `work/epics/E-0024-schema-alignment/m-E24-05-canary-green-hard-assertion-tracking.md` (new)

## Out of Scope

- `ModelValidator` deletion — remains with E-0023 M-0048.
- Starting or running M-0047 — happens after E-0024 closes.
- Any backward-compatibility work — forward-only per epic constraint.
- New validator features — out of epic scope.

## Dependencies

- M-0052 landed. The canary reports `val-err=0` at M-0052 close (informally); this milestone makes that formal.
- Every prior E-0024 milestone (M-0049 through M-0052) wrapped.

## References

- Epic spec: `work/epics/E-0024-schema-alignment/spec.md`
- Canary test: `tests/FlowTime.Integration.Tests/TemplateWarningSurveyTests.cs`
- E-0023 spec: `work/epics/E-0023-model-validation-consolidation/spec.md` — status flip target
- CLAUDE.md Current Work section
- Prior milestone tracking docs (M-0049 through M-0052)
