---
id: m-E23-03-delete-model-validator
epic: E-23-model-validation-consolidation
status: draft
depends_on: m-E23-02-call-site-migration
completed:
---

# m-E23-03: Delete `ModelValidator`

## Goal

Delete `src/FlowTime.Core/Models/ModelValidator.cs` and any remaining tests that exist solely to exercise it. After this milestone, `ModelSchemaValidator.Validate` is the single model-YAML validator in the codebase.

## Context

After m-E23-02 `ModelValidator` has zero production callers and its dedicated test files have been rewritten to target `ModelSchemaValidator`. The type is dead weight — kept on disk only as a rollback seam during m-E23-02. This milestone removes that seam.

Per the 2026-04-23 Truth Discipline guard, retaining an unreferenced entry point "under the banner of keeping the existing surface stable" is explicitly disallowed. The Goal of this milestone is simply to enforce that guard on the `ModelValidator` case that motivated adding the guard in the first place.

## Acceptance Criteria

1. **File deleted.** `src/FlowTime.Core/Models/ModelValidator.cs` is removed from the repository. `git log -- src/FlowTime.Core/Models/ModelValidator.cs` shows the deletion commit as most recent.
2. **`ValidationResult` retained.** The `ValidationResult` class (currently defined at the bottom of `ModelValidator.cs`, lines 205–214) is moved to its own file `src/FlowTime.Core/Models/ValidationResult.cs` before the delete — it is shared between `ModelSchemaValidator`, `TimeMachineValidator`, and callers. The move is a pure relocation with no API change.
3. **Zero `ModelValidator` references.** `grep -rn "ModelValidator\\b" --include="*.cs"` returns zero hits across the whole repository except for comments or commit messages that reference it by name historically. No `using` statement, no method call, no type reference.
4. **Full test suite green.** `dotnet test FlowTime.sln` passes. `dotnet build FlowTime.sln` has no warnings about missing types (CS0246) and no warnings about unused usings introduced by the delete.
5. **Both canaries still green.** `TemplateWarningSurveyTests.Survey_Templates_For_Warnings` reports `val-err == 0` across all twelve templates at `ValidationTier.Analyse` (E-24 m-E24-05's hard-asserting survey canary), and `RuleCoverageRegressionTests` (m-E23-01's negative-case catalogue) reports green — every rule the audit claimed `ModelSchemaValidator` covers, it still catches after the delete. Both canaries are the final confirmation that nothing depends on `ModelValidator` post-deletion.
6. **Smoke verification across surfaces.** Manual smoke-run of: (a) `POST /v1/run` with a representative template via the Engine API, (b) the Engine CLI `dotnet run --project src/FlowTime.Cli` against the same template, (c) `POST /v1/validate` via the Time Machine surface, (d) one Blazor page that renders a validated model (e.g., Dashboard), (e) one Svelte page that validates a model indirectly (e.g., `/analysis` sweep configuration). All five behave identically to pre-delete.
7. **Epic close.** On merge to `main`, the E-23 epic status flips from `in-progress` to `complete` and the epic folder is archived under `work/epics/completed/E-23-model-validation-consolidation/`. `ROADMAP.md`, `work/epics/epic-roadmap.md`, and `CLAUDE.md` Current Work are updated to reflect completion.

## Constraints

- No functional change. The only allowed diffs are: (1) delete `ModelValidator.cs`, (2) move `ValidationResult` to its own file, (3) remove any now-unused `using` statements that referenced the deleted type, (4) remove any dedicated `ModelValidator`-only test files that survived m-E23-02.
- If step 3 or 4 requires more than trivial edits — e.g., an `.editorconfig` or `Directory.Build.props` rule references the deleted file — pause and check whether the original migration missed a call site, rather than forcing the delete forward.
- Rollback must remain a single `git revert` for at least the m-E23-03 merge commit. Keep the delete in one commit; do not mix in any other refactor.

## Design Notes

- `ValidationResult` belongs to the validators; moving it to `ValidationResult.cs` keeps `FlowTime.Core.Models` tidy post-delete. Namespace stays `FlowTime.Core` (same as today — `ModelValidator.cs` declares its types in that namespace, not `FlowTime.Core.Models`).
- Expect a handful of `using FlowTime.Core;` removals where the using was imported solely for `ModelValidator` and is now unused. Let the compiler find them.

## Surfaces touched

- `src/FlowTime.Core/Models/ModelValidator.cs` (deleted)
- `src/FlowTime.Core/Models/ValidationResult.cs` (new, content moved from deleted file)
- Possibly a small number of unused-using cleanups across the solution
- `work/epics/E-23-model-validation-consolidation/m-E23-03-delete-model-validator-tracking.md` (new)
- `ROADMAP.md`, `work/epics/epic-roadmap.md`, `CLAUDE.md` (epic-completion housekeeping)

## Out of Scope

- Any change to `ModelSchemaValidator.cs` — if the delete reveals `ModelSchemaValidator` is missing a rule the audit should have caught, treat that as an m-E23-01 scope escape (the negative-case canary should have caught it; if it didn't, the audit was incomplete).
- Any change to schema, API, CLI, UI, or Sim code.
- Any refactor of validation beyond the delete — no folding of `TimeMachineValidator.ValidateSchema` into `ModelSchemaValidator`, no introduction of new validation surfaces.

## Dependencies

- m-E23-02 complete and merged into `epic/E-23-model-validation-consolidation`.
- `ModelValidator` has zero production callers (m-E23-02 AC6 is the precondition for AC3 here).

## References

- Epic spec: `work/epics/E-23-model-validation-consolidation/spec.md`
- m-E23-02 tracking doc (final migration state)
- Truth Discipline guard (2026-04-23): `.ai-repo/rules/project.md` → `"'API stability' does not mean 'keep old functions around.'"`
