---
id: M-047
title: Call-Site Migration
status: done
parent: E-23
---

## Goal

Switch every production call site that invokes `ModelValidator.Validate` to `ModelSchemaValidator.Validate`, and migrate any test that exercises `ModelValidator` in isolation. Audit error-message phrasing and update API contract / test assertions where needed. `ModelValidator.cs` is left on disk but reaches zero production call sites at this milestone's close; deletion lands in M-048.

## Context

After M-046 the schema and `ModelSchemaValidator`'s adjuncts together cover every rule the embedment audit identified — by construction, every rule that `ModelValidator` enforces also fails `ModelSchemaValidator`'s evaluation, proven by the negative-case canary catalogue. The two validators are now behaviourally compatible on both the success path (E-24's `val-err == 0` canary) and the failure path (M-046's negative-case canary).

Call-site migration is mostly mechanical — replace the type name at each call site — but one concern is real: error phrasing differs. `ModelValidator` returns flat strings like `"Grid must specify bins"`; `ModelSchemaValidator` returns JSON-schema-shaped messages like `/grid/bins: Required properties are missing: [bins]`. Tests that assert on exact phrasing and any UI / CLI consumer that surfaces error strings verbatim must be audited and updated.

This milestone intentionally stops before deletion so rollback is cheap. If something goes sideways, a single-commit revert takes the migration back to `ModelValidator` without losing the M-046 audit, schema additions, or adjunct methods.

## Acceptance Criteria

1. **Call-site enumeration is fresh.** First action of the milestone: `grep -rn "ModelValidator\.Validate" --include="*.cs"` outside `.claude/worktrees/`. Record the live list in the tracking doc — line numbers must be the live ones at start-milestone time, not cached from this spec. Today's enumeration (subject to refresh at start) is three production sites and four test files:
   - `src/FlowTime.API/Program.cs` (`POST /v1/run` validation block)
   - `src/FlowTime.Cli/Program.cs` (Engine CLI `run` entry)
   - `src/FlowTime.TimeMachine/Validation/TimeMachineValidator.cs` (the redundant delegation alongside the existing `ModelSchemaValidator.Validate` call — remove the `ModelValidator` line, keep the `ModelSchemaValidator` line as the whole of tier-1 schema check)
   - `tests/FlowTime.Integration.Tests/SimToEngineWorkflowTests.cs`
   - `tests/FlowTime.Tests/Schema/TargetSchemaValidationTests.cs`
   - `tests/FlowTime.Tests/Schema/SchemaVersionTests.cs`
   - `tests/FlowTime.Tests/Schema/SchemaErrorHandlingTests.cs`
2. **Production call sites migrated.** Each production call site (the three above, post-refresh) passes through `ModelSchemaValidator.Validate` instead. The production diff is small — type name swap plus, where needed, error-shape adaptation at the response site.
3. **Test suite migrated.** The three `tests/FlowTime.Tests/Schema/*.cs` files referencing `ModelValidator.Validate` are rewritten to assert against `ModelSchemaValidator`. Each individual assertion falls into one of: (a) kept as-is because `ModelSchemaValidator` already covers it identically, (b) updated to match the new error-message format, (c) relaxed to assert on semantic properties (`IsValid == false`, at least one error containing a specified substring or matching a specified pattern) when exact phrasing is not contract-meaningful, (d) deleted because it asserts an internal `ModelValidator` detail not reachable from `ModelSchemaValidator`. The tracking doc records which bucket each assertion fell into. `SimToEngineWorkflowTests.cs` lines using `ModelValidator.Validate` are updated to `ModelSchemaValidator.Validate`.
4. **Error-phrasing audit recorded.** The tracking doc includes a before/after table for the representative invalid-model corpus (missing `bins`, missing `grid`, wrong `schemaVersion`, legacy top-level `arrivals`, legacy node-level `expression`, legacy `binMinutes`): `ModelValidator` message vs. `ModelSchemaValidator` message, plus a note on any downstream consumer (UI, CLI stdout, API response) that surfaces the string directly.
5. **API contract preserved.** `POST /v1/run` continues to return HTTP 400 with a `{ "error": "..." }` JSON body for invalid models. The `error` string may be differently phrased, but the shape — one JSON field, semicolon-joined on multi-error — is identical. API contract tests are green.
6. **CLI stderr output preserved.** `dotnet run --project src/FlowTime.Cli` on an invalid model continues to write `Model validation failed:` followed by one `  - <msg>` line per error, returning exit code 1. Tests (if any) and manual smoke are green.
7. **`ModelValidator` has zero production callers.** `grep -rn "ModelValidator\.Validate\|ModelValidator\b" --include="*.cs"` outside `.claude/worktrees/` returns matches only inside `src/FlowTime.Core/Models/ModelValidator.cs` itself. No `.cs` file outside that one references the type by name.
8. **Full .NET suite green.** `dotnet test FlowTime.sln` passes. Both canaries — E-24's `TemplateWarningSurveyTests` (`val-err == 0`) and M-046's `RuleCoverageRegressionTests` — stay green. No Svelte/Blazor/UI regressions (those surfaces are unmodified but smoke-checked via existing Playwright specs running against a live engine).
9. **Optional: latency delta recorded.** If measurable with reasonable effort (e.g., a single warm-run timing of `POST /v1/run` against a representative template before and after), the tracking doc records the `ModelValidator` → `ModelSchemaValidator` latency delta. Non-blocking; documented as informational.

## Constraints

- No change to `ModelValidator.cs` in this milestone. It stays on disk unreferenced outside its own file so a revert-migration is a single commit.
- No schema or adjunct addition — that was M-046's territory. If a rule gap surfaces during the phrasing audit, treat it as an M-046 scope escape and return to that milestone rather than fixing forward.
- Do not "improve" `ModelSchemaValidator` error messages to match `ModelValidator`'s phrasing. The shape is the JSON-schema shape. If a consumer surfaces raw strings, that consumer updates — not the validator.
- Do not introduce a `ModelValidator` → `ModelSchemaValidator` forwarding shim as a stepping stone. Direct call-site replacement only (per ADR-E-23-01).

## Design Notes

- The `TimeMachineValidator.ValidateSchema` method currently invokes both validators and concatenates errors. After this milestone it invokes only `ModelSchemaValidator`. Confirm that the `TimeMachineValidator` tier-1 contract (schema-only checks) is semantically preserved — any test that sent a model with a legacy field like `arrivals` at root level and expected a tier-1 error must still receive a tier-1 error, because `ModelSchemaValidator` catches it via root `additionalProperties: false`.
- When updating test phrasing, prefer semantic assertions (`errors.Should().Contain(e => e.Contains("bins"))`) over exact-string assertions where the exact string is not the contract.
- The CLI and API both surface error strings to end users. Inspect the UIs (Blazor `ModelEditor.razor`, any Svelte form) to confirm no regex-parses the validator output. Spot-check via test, not just grep.

## Surfaces touched

- `src/FlowTime.API/Program.cs`
- `src/FlowTime.Cli/Program.cs`
- `src/FlowTime.TimeMachine/Validation/TimeMachineValidator.cs`
- `tests/FlowTime.Integration.Tests/SimToEngineWorkflowTests.cs`
- `tests/FlowTime.Tests/Schema/TargetSchemaValidationTests.cs`
- `tests/FlowTime.Tests/Schema/SchemaVersionTests.cs`
- `tests/FlowTime.Tests/Schema/SchemaErrorHandlingTests.cs`
- `work/epics/E-23-model-validation-consolidation/m-E23-02-call-site-migration-tracking.md` (new)

## Out of Scope

- Deleting `ModelValidator.cs` — that is M-048.
- Any error-message rewording inside `ModelSchemaValidator`.
- Any schema or adjunct addition.
- Any UI code change, unless the phrasing audit uncovers a regex-parse consumer, in which case the UI update is the minimum needed to preserve user-visible behaviour.

## Dependencies

- M-046 complete and merged into `epic/E-23-model-validation-consolidation` (audit done; schema and adjuncts cover every rule).

## References

- Epic spec: `work/epics/E-23-model-validation-consolidation/spec.md`
- M-046 spec and tracking doc (rule audit + negative-case canary catalogue)
- Tiered validator: `src/FlowTime.TimeMachine/Validation/TimeMachineValidator.cs`
- ADR-E-23-01 (delete, do not delegate) in epic spec
