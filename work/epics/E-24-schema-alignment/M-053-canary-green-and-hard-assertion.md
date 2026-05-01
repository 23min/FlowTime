---
id: M-053
title: Canary Green and Hard Assertion
status: done
parent: E-24
---

## Goal

Close E-24. Promote `TemplateWarningSurveyTests.Survey_Templates_For_Warnings` from an informational diagnostic to a hard-asserting regression guard: a non-zero `val-err` count fails the build. Run the full `.NET` solution suite to confirm zero regressions. Update any documentation that still describes the pre-E-24 schema shape. E-23 becomes ready to resume with a byte-trivial M-047 + M-048.

## Context

By this milestone:

- M-050 unified `SimModelArtifact` + `ModelDefinition` into a single type and deleted the Sim-side satellite types.
- M-051 realigned `docs/schemas/model.schema.yaml` to describe the unified type in camelCase and rewrote `docs/schemas/README.md`.
- M-052 fixed `ParseScalar` in both validators.

The canary reported `val-err=0` at M-052's wrap. This milestone makes that zero a permanent assertion and verifies every other test path is unaffected. After this milestone, schema-reality convergence is a live, enforced property of the build.

## Acceptance Criteria

1. **Canary promoted to hard assertion.** `tests/FlowTime.Integration.Tests/TemplateWarningSurveyTests.cs` is modified so `Survey_Templates_For_Warnings` asserts `val-err == 0` for every template at `ValidationTier.Analyse`. The current informational logging (`Totals: validator-errors=..., ...`) is retained for diagnostic visibility, but a non-zero count now fails the assertion. Graceful-skip behavior when port 8081 is unreachable is preserved (follows the existing `SkipUnless` / health-probe pattern).
2. **Canary is green in-assertion.** Run the canary once against a live Engine API after M-052's close. Confirm `val-err=0` across all twelve templates. The tracking doc captures the verbatim "Totals" output as evidence.
3. **Full `.NET` solution suite green.** `dotnet test FlowTime.sln` passes. All test assemblies report zero failures. Any pre-existing flakes (`RustEngine_CleansUpTempDirectory_OnFailure`, `SessionModelEvaluatorIntegrationTests.Dispose_TerminatesSubprocess` per M-046 baselines) remain the same flakes with the same transient-timing signature — no new timing sensitivity introduced by E-24.
4. **Grep audits pass.** The following audits are captured in the tracking doc:
   - `grep -rn "generated_at\|model_id\|template_id\|template_version" docs/schemas/model.schema.yaml` returns zero hits.
   - `grep -rn "SimModelArtifact" --include='*.cs'` returns zero hits.
   - `grep -rn "SimNode\b\|SimOutput\b\|SimProvenance\b\|SimTraffic\b\|SimArrival\b\|SimArrivalPattern\b" --include='*.cs'` returns zero hits.
5. **Documentation aligned.** Any architecture doc (`docs/architecture/*`) that still describes the pre-E-24 two-type / two-schema shape is updated to reflect the post-E-24 unified reality. Historical descriptions move to `docs/archive/` if they are still useful as history, or are deleted. `docs/schemas/README.md` (rewritten in M-051) is verified current. The tracking doc lists every audited file and its disposition.
6. **E-23 pause is cleared.** `work/epics/E-23-model-validation-consolidation/spec.md` status flips from `paused` to `ready-to-resume` (or to `in-progress` — reviewer choice). The E-23 spec's amendment notes E-24's close and points to the canary green assertion as the entry condition for M-047. Same status-surface sweep across `ROADMAP.md`, `work/epics/epic-roadmap.md`, and `CLAUDE.md`.
7. **Decisions logged.** A new decision entry records E-24's close and notes any deltas between M-049's design decisions and the final landed state (there should be none if the milestones executed as planned; if any, they are documented as sub-decisions). Candidate ID: `D-2026-MM-DD-NNN: E-24 Schema Alignment closed; E-23 ready to resume`.
8. **No new validator features.** The canary's promotion is the only behavior change here. No new tiers, no line/column mapping, no suggestion hints. Out-of-scope work remains out.

## Constraints

- **Assertion replaces informational log, not augments it.** The test fails the build on non-zero `val-err`. If reviewers want the diagnostic log to survive, it survives as stdout; the assertion is the gate.
- **Zero-tolerance in the assertion.** `val-err == 0`. Not `val-err < 5`, not "every residual matches the C-defect shape" (the amended M-046 AC4 phrasing) — by the time M-053 runs, the C-defect is fixed by M-052. A template producing any validator error is a regression.
- **Graceful-skip on infrastructure absence, not on assertion failure.** If the API is unreachable, `Skip(...)` with a clear message is correct. If the API is reachable and a template reports non-zero `val-err`, the test fails. Do not use `Skip` to mask assertion failures.
- **No new test infrastructure.** The canary remains in `tests/FlowTime.Integration.Tests` using the existing health-probe-and-skip pattern shared with Rust engine integration tests. Do not move it to a new project or harness.
- **E-23 pause lift is coordinated.** Flip E-23 status only after M-053's assertion is green on a live run and the full suite is green. The status-surface sync is atomic — all surfaces (spec, tracking, roadmap, epic-roadmap, CLAUDE.md) flip in one pass.

## Design Notes

- The assertion pattern matches the project's integration-test idiom: `Assert.True(totals.ValErr == 0, $"Expected val-err=0, got {totals.ValErr}. Details: {totals}");` — include the template-by-template first-err detail in the message so a failure is actionable.
- Documentation updates: walk `docs/` for references to the snake_case provenance shape, the `SimModelArtifact`-on-the-wire pattern, or the two-schema world. Update what is still current. Archive or delete what is not.
- The E-23 status-surface update is a planner-scope operation (status-surface reconciliation); the actual M-047 restart is a separate start-milestone action in the next session.
- After this milestone, the stashed M-046 input material (on `milestone/m-E23-01-schema-alignment`) becomes historical — its content has been absorbed by E-24's milestones and the canary green state. The branch can be retired when E-23 resumes.

## Surfaces touched

- `tests/FlowTime.Integration.Tests/TemplateWarningSurveyTests.cs` (assertion promotion)
- `work/epics/E-23-model-validation-consolidation/spec.md` (status flip)
- `ROADMAP.md` (E-23 status flip, E-24 completion note)
- `work/epics/epic-roadmap.md` (E-23 + E-24 status sync)
- `CLAUDE.md` (Current Work section update)
- `work/decisions.md` (E-24 close entry)
- `docs/` (documentation alignment — specific files identified in M-053's tracking doc after audit)
- `work/epics/E-24-schema-alignment/m-E24-05-canary-green-hard-assertion-tracking.md` (new)

## Out of Scope

- `ModelValidator` deletion — remains with E-23 M-048.
- Starting or running M-047 — happens after E-24 closes.
- Any backward-compatibility work — forward-only per epic constraint.
- New validator features — out of epic scope.

## Dependencies

- M-052 landed. The canary reports `val-err=0` at M-052 close (informally); this milestone makes that formal.
- Every prior E-24 milestone (M-049 through M-052) wrapped.

## References

- Epic spec: `work/epics/E-24-schema-alignment/spec.md`
- Canary test: `tests/FlowTime.Integration.Tests/TemplateWarningSurveyTests.cs`
- E-23 spec: `work/epics/E-23-model-validation-consolidation/spec.md` — status flip target
- CLAUDE.md Current Work section
- Prior milestone tracking docs (M-049 through M-052)
