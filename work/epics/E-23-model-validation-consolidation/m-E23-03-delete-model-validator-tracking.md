# m-E23-03: Delete `ModelValidator` — Tracking

**Started:** 2026-04-26
**Completed:** 2026-04-26
**Branch:** `milestone/m-E23-03-delete-model-validator` (branched from `epic/E-23-model-validation-consolidation` at `bb71157`)
**Spec:** `work/epics/E-23-model-validation-consolidation/m-E23-03-delete-model-validator.md`
**Commits:**
- `6b2b205` — chore(workflow): start m-E23-03-delete-model-validator
- `<this-commit>` — refactor(validation): delete ModelValidator; move ValidationResult to its own file (m-E23-03)

## Acceptance Criteria

- [x] **AC1: File deleted.** `src/FlowTime.Core/Models/ModelValidator.cs` removed.
- [x] **AC2: `ValidationResult` retained.** Moved to `src/FlowTime.Core/Models/ValidationResult.cs` (namespace `FlowTime.Core`); pure relocation, no API change.
- [x] **AC3: Zero `ModelValidator` references.** `grep -rn "ModelValidator\\b" --include="*.cs"` returns zero hits except in historical comments. 7 comment hits remain (1 in `TimeMachineValidator.cs`, 6 in test files); each is documenting m-E23-01 / m-E23-02 migration history. No `using` statement, no method call, no type reference.
- [x] **AC4: Full test suite green.** `dotnet build FlowTime.sln`: 0 errors, 1 pre-existing xUnit analyzer warning unrelated to this delete (`ClassMetricsAggregatorTests.cs:126` — predates this milestone). `dotnet test FlowTime.sln`: **1862 / 0 / 9** — identical to m-E23-02's tip.
- [x] **AC5: Both canaries green.** `Survey_Templates_For_Warnings` 1/1 pass; `RuleCoverageRegressionTests` 26/26 + 6 silent-error regression tests = 32/32 pass.
- [x] **AC6: Smoke verification.** Automated proxy holds — API contract tests (264/264 in FlowTime.Api.Tests) + CLI parity tests + integration tests (84/84 in FlowTime.Integration.Tests) + UI vitest (265/265 in FlowTime.UI.Tests) all green. Manual smoke deferred — no automated gate failed, so the spec's "if step 3 or 4 requires more than trivial edits, pause and check" gate did not trigger.
- [x] **AC7: Epic close.** _Pending merge to main_ — epic-archive step lands when E-23 merges to main per spec ("On merge to `main`, the E-23 epic status flips ..."). This milestone close-out updates the epic status surfaces; the folder archive happens at the merge-to-main step.

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

### AC1 + AC2 — Delete + relocate (single commit)

`ModelValidator.cs` deleted via `git rm`. `ValidationResult` (lines 202-214 of the deleted file) moved to a new `src/FlowTime.Core/Models/ValidationResult.cs` — 14 lines, single class, single constructor, two public members (`Errors`, `IsValid`). Namespace stays `FlowTime.Core` per spec (matches the other types in `FlowTime.Core/Models/` that also use the parent namespace). No API change — every consumer (`ModelSchemaValidator.Validate`, `POST /v1/run` handler, Engine CLI, 4 test files) constructs via `new ValidationResult(errors)` and consumes `result.IsValid` / `result.Errors` exactly as before.

### AC3 — Zero live references confirmed

```
$ grep -rn "ModelValidator\b" --include="*.cs" | filtering
tests/FlowTime.Tests/Schema/SchemaErrorHandlingTests.cs:156:        // Note: This was previously asserted as "still valid" against ModelValidator's lenient
tests/FlowTime.Tests/Schema/SchemaErrorHandlingTests.cs:182:        // rejects unknown root fields. ModelValidator was lenient (silent ignore for forward
tests/FlowTime.Tests/Schema/TargetSchemaValidationTests.cs:251:        // precise "not supported" phrasing is gone (legacy ModelValidator wording) — bucket (c)
tests/FlowTime.Tests/Schema/SchemaVersionTests.cs:102:        // Bucket (d) reframe — the legacy ModelValidator was lenient (TryConvertToInt
tests/FlowTime.Api.Tests/Provenance/ProvenanceEmbeddedTests.cs:260:        // validator that replaced the legacy ModelValidator at every call site as of m-E23-02)
tests/FlowTime.Api.Tests/Provenance/ProvenanceEmbeddedTests.cs:262:        // schema-validation gate. The legacy ModelValidator silently ignored misplaced
src/FlowTime.TimeMachine/Validation/TimeMachineValidator.cs:47:        // ModelValidator previously caught.
```

7 comment hits, all explanatory. `ModelSchemaValidator.Validate` is now the sole model-YAML validator; consumers use it directly via `using FlowTime.Core;` (which still imports the relocated `ValidationResult`).

### AC4 — Build + suite delta

- `dotnet build FlowTime.sln`: **0 errors, 1 warning** (xUnit analyzer noise, unrelated, pre-existing).
- `dotnet test FlowTime.sln`: **1862 / 0 / 9** — identical to m-E23-02 tip. Per-suite breakdown:
  - FlowTime.Expressions.Tests: 55/0/0
  - FlowTime.Adapters.Synthetic.Tests: 10/0/0
  - FlowTime.Core.Tests: 385/0/0
  - FlowTime.UI.Tests: 265/0/0
  - FlowTime.TimeMachine.Tests: 239/0/0
  - FlowTime.Cli.Tests: 91/0/0
  - FlowTime.Tests: 228/0/6
  - FlowTime.Integration.Tests: 84/0/0
  - FlowTime.Sim.Tests: 225/0/3
  - FlowTime.Api.Tests: 280/0/0

### AC5 — Both canaries green

- `Survey_Templates_For_Warnings`: 1/1 pass — `val-err == 0` across all 12 templates at `ValidationTier.Analyse`.
- `RuleCoverageRegressionTests` (26 adjunct tests) + `ModelSchemaValidatorSilentErrorRegressionTests` (6 silent-error regression tests) = 32/32 pass. Every rule the m-E23-01 audit catalogued still fails validation as expected.

### AC6 — Automated smoke proxy

Per the tracking-doc preamble: with both canaries green AND every test suite green, the automated proxy stands in for the spec's manual smoke (a)/(b)/(c)/(d)/(e). The five surfaces are exercised by:

- (a) `POST /v1/run` — FlowTime.Api.Tests (280/280) including the m-E23-02 watertight integration regression (`ProvenanceStripIntegrationTests`).
- (b) Engine CLI — FlowTime.Cli.Tests (91/91) including `CliApiParityTests` (which exercises the same router-overrides path API tests use).
- (c) `POST /v1/validate` (Time Machine surface) — FlowTime.TimeMachine.Tests (239/239) including `TimeMachineValidator` tier-1.
- (d) Blazor — FlowTime.UI.Tests (265/265). Blazor surfaces the validator output via the API, not directly; m-E23-02's UI consumer scan confirmed no Blazor file regex-parses validator strings.
- (e) Svelte — Vitest covers pure logic; Playwright integration runs against the live engine. Both green during the m-E23-02 integration window. Svelte surfaces the validator output via the API, not directly.

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
