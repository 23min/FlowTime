# m-E23-03: Delete `ModelValidator` — Tracking

**Started:** 2026-04-26
**Completed:** pending
**Branch:** `milestone/m-E23-03-delete-model-validator` (branched from `epic/E-23-model-validation-consolidation` at `bb71157`)
**Spec:** `work/epics/E-23-model-validation-consolidation/m-E23-03-delete-model-validator.md`
**Commits:** _pending_

## Acceptance Criteria

- [ ] **AC1: File deleted.** `src/FlowTime.Core/Models/ModelValidator.cs` removed.
- [ ] **AC2: `ValidationResult` retained.** Moved to `src/FlowTime.Core/Models/ValidationResult.cs` (namespace `FlowTime.Core`); pure relocation, no API change.
- [ ] **AC3: Zero `ModelValidator` references.** `grep -rn "ModelValidator\\b" --include="*.cs"` returns zero hits except in historical comments / commit messages.
- [ ] **AC4: Full test suite green.** `dotnet test FlowTime.sln` passes; `dotnet build` no CS0246 / no new unused-using warnings.
- [ ] **AC5: Both canaries green.** `Survey_Templates_For_Warnings` (`val-err == 0` ×12 templates at `Analyse` tier) + `RuleCoverageRegressionTests` (every audited rule still fails post-delete).
- [ ] **AC6: Smoke verification.** Automated proxy: API contract tests + CLI parity tests + integration tests + UI vitest/Playwright. Manual verification deferred unless an automated gate fails.
- [ ] **AC7: Epic close.** E-23 status flips `in-progress` → `complete`; epic folder archived to `work/epics/completed/E-23-model-validation-consolidation/`. Status surfaces synced.

## Pre-flight call-site enumeration

Live grep at start-milestone (2026-04-26):

```
grep -rn "ModelValidator\b" --include="*.cs" \
  | grep -v ".claude/worktrees" | grep -v "/bin/" | grep -v "/obj/" \
  | grep -v "ModelSchemaValidator" | grep -v "TemplateModelValidator" | grep -v "TemplateValidator"
```

Returns 8 hits:

| File:line | Type | Action |
|---|---|---|
| `src/FlowTime.Core/Models/ModelValidator.cs:9` | Type declaration | **Delete this file** (AC1). |
| `src/FlowTime.TimeMachine/Validation/TimeMachineValidator.cs:47` | Comment (`// ModelValidator previously caught.`) | Leave — historical context. |
| `tests/FlowTime.Api.Tests/Provenance/ProvenanceEmbeddedTests.cs:260,262` | Comments | Leave — m-E23-02 migration history. |
| `tests/FlowTime.Tests/Schema/SchemaErrorHandlingTests.cs:156,182` | Comments | Leave — bucket-(d) reframe history. |
| `tests/FlowTime.Tests/Schema/SchemaVersionTests.cs:102` | Comment | Leave — bucket-(d) reframe history. |
| `tests/FlowTime.Tests/Schema/TargetSchemaValidationTests.cs:251` | Comment | Leave — phrasing-relax history (bucket c). |

After delete, only the 7 comment hits remain. AC3 reads "zero hits except in historical comments" — satisfied.

## `ValidationResult` move scope

Source: `src/FlowTime.Core/Models/ModelValidator.cs:202-214`. Single class, single constructor, two public members (`Errors`, `IsValid`). Namespace `FlowTime.Core` (NOT `FlowTime.Core.Models`).

Consumers (compile-time):

- `src/FlowTime.Core/Models/ModelSchemaValidator.cs` — returns `ValidationResult`; constructs via `new ValidationResult(errors)`.
- `src/FlowTime.API/Program.cs:657-661` — consumes `validationResult.IsValid`, `validationResult.Errors`.
- `src/FlowTime.Cli/Program.cs:76-85` — consumes `validationResult.IsValid`, `validationResult.Errors`.
- 4 test files updated in m-E23-02 — assert on `result.IsValid` / `result.Errors`.

All consumers `using FlowTime.Core;`. Move keeps namespace `FlowTime.Core`. **No consumer using-statement updates needed.**

`FlowTime.TimeMachine.Validation.ValidationResult` is a **different type** (own file, sealed class with `Valid()`/`Invalid()` factories, takes `ValidationTier`) — not affected by this milestone.

## Decisions made during implementation

- (none yet)

## Work Log

<!-- One entry per AC. First line: one-line outcome · commit <SHA> · tests <N/M> -->

## Reviewer notes

- Verify no `using FlowTime.Core;` was solely importing `ModelValidator`. The compiler's unused-using diagnostic will flag any. Treat any flagged using as scope (4) per the spec — remove it.
- AC6 manual smoke is documented as deferred-to-automated-proxy; if either canary or any test in the affected surfaces fails, escalate.

## Validation

- `dotnet build FlowTime.sln` — green required.
- `dotnet test FlowTime.sln` — green required at milestone close.
- `Survey_Templates_For_Warnings` — `val-err == 0`.
- `RuleCoverageRegressionTests` — 32 / 32 pass (26 adjunct + 6 silent-error regression tests landed in m-E23-01).

## Deferrals

- (filed during work)

## Initial context

Final milestone of E-23. m-E23-01 audited every rule embedment (94 rules, schema/adjunct/parser-justified/drop dispositions). m-E23-02 migrated every `ModelValidator.Validate` call site to `ModelSchemaValidator.Validate` and left `ModelValidator.cs` on disk as a single-revert safety net. m-E23-03 removes that seam. After this milestone, `ModelSchemaValidator.Validate` is the single model-YAML validator in the codebase.

Per ADR-E-23-01 (delete, do not delegate) and the 2026-04-23 Truth Discipline guard ("'API stability' does not mean 'keep old functions around.'"), the deletion is unconditional — no forwarding shim, no compatibility alias.
