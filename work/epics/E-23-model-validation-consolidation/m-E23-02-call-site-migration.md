---
id: m-E23-02-call-site-migration
epic: E-23-model-validation-consolidation
status: draft
depends_on: m-E23-01-schema-alignment
completed:
---

# m-E23-02: Call-Site Migration

## Goal

Switch every production call site that invokes `ModelValidator.Validate` to `ModelSchemaValidator.Validate`, and migrate any test that exercises `ModelValidator` in isolation. Audit error-message phrasing and update API contract / test assertions where needed. `ModelValidator.cs` is left on disk but reaches zero production call sites at this milestone's close; deletion lands in m-E23-03.

## Context

After m-E23-01 the schema accepts every currently-valid model (including `grid.start`). The two validators are now behaviorally compatible on the success path. Call-site migration is mostly mechanical — replace the type name at each call site — but one concern is real: error phrasing differs. `ModelValidator` returns flat strings like `"Grid must specify bins"`; `ModelSchemaValidator` returns JSON-schema-shaped messages like `/grid/bins: Required properties are missing: [bins]`. Tests that assert on exact phrasing and any UI that displays errors verbatim must be audited and updated.

This milestone intentionally stops before deletion so rollback is cheap. If something goes sideways, a single-commit revert takes the migration back to `ModelValidator` without losing the schema fix or the survey canary.

## Acceptance Criteria

1. **Production call sites migrated.** These four call sites (all the ones found by `grep -rn "ModelValidator\.Validate" --include="*.cs"`) pass through `ModelSchemaValidator.Validate` instead:
   - `src/FlowTime.API/Program.cs:657` (`POST /v1/run`).
   - `src/FlowTime.Cli/Program.cs:76` (Engine CLI `run` path).
   - `src/FlowTime.TimeMachine/Validation/TimeMachineValidator.cs:50` — the redundant delegation is removed; the existing `ModelSchemaValidator.Validate` call on line 46 is the whole of tier-1 after this milestone.
   - `tests/FlowTime.Integration.Tests/SimToEngineWorkflowTests.cs:38,120`.
2. **Test suite migrated.** The three `FlowTime.Tests/Schema/*` test files currently referencing `ModelValidator.Validate` (`TargetSchemaValidationTests`, `SchemaVersionTests`, `SchemaErrorHandlingTests`) are rewritten to assert against `ModelSchemaValidator`. Each individual assertion falls into one of: (a) kept as-is because `ModelSchemaValidator` already covers it identically, (b) updated to match the new error-message format, (c) relaxed to assert on semantic properties (`IsValid == false`, at least one error containing a specified substring or matching a specified pattern) when exact phrasing is not contract-meaningful, (d) deleted because it asserts an internal `ModelValidator` detail not reachable from `ModelSchemaValidator`. The tracking doc records which bucket each assertion fell into.
3. **Error-phrasing audit is recorded.** The tracking doc includes a before/after table for the representative invalid-model corpus (missing `bins`, missing `grid`, wrong `schemaVersion`, legacy top-level `arrivals`, legacy node-level `expression`, legacy `binMinutes`): `ModelValidator` message vs. `ModelSchemaValidator` message, plus a note on any downstream consumer (UI, CLI stdout, API response) that surfaces the string directly.
4. **API contract preserved.** `POST /v1/run` continues to return HTTP 400 with a `{ "error": "..." }` JSON body for invalid models. The `error` string may be differently phrased, but the shape — one JSON field, semicolon-joined on multi-error — is identical. API contract tests are green.
5. **CLI stderr output preserved.** `dotnet run --project src/FlowTime.Cli` on an invalid model continues to write `Model validation failed:` followed by one `  - <msg>` line per error, returning exit code 1. Tests (if any) and manual smoke are green.
6. **`ModelValidator` has zero production callers.** `grep -rn "ModelValidator\\.Validate\|ModelValidator\\b" --include="*.cs"` returns matches only inside `src/FlowTime.Core/Models/ModelValidator.cs` itself. No `.cs` file outside that one references the type by name.
7. **Full .NET suite green.** `dotnet test FlowTime.sln` passes. Survey test still reports zero template errors. No Svelte/Blazor/UI regressions (those surfaces are unmodified but are smoke-checked via the existing Playwright specs running against a live engine).
8. **Optional: latency delta recorded.** If measurable with reasonable effort (e.g., a single warm-run timing of `POST /v1/run` against a representative template before and after), the tracking doc records the `ModelValidator` → `ModelSchemaValidator` latency delta. Non-blocking; documented as informational.

## Constraints

- No change to `ModelValidator.cs` in this milestone. It stays on disk unreferenced outside its own file so a revert-migration is a single commit.
- No change to `docs/schemas/model.schema.yaml` — that was m-E23-01's territory. If a rule gap surfaces during the phrasing audit, treat it as an m-E23-01 scope escape and return to that milestone rather than fixing forward.
- Do not "improve" `ModelSchemaValidator` error messages to match `ModelValidator`'s phrasing. The shape is the JSON-schema shape. If a consumer surfaces raw strings, that consumer updates — not the validator.
- Do not introduce a `ModelValidator` → `ModelSchemaValidator` forwarding shim as a stepping stone. Direct call-site replacement only.

## Design Notes

- The `TimeMachineValidator.ValidateSchema` method currently invokes both validators and concatenates errors. After this milestone it invokes only `ModelSchemaValidator`. Confirm that the `TimeMachineValidator` tier-1 contract (schema-only checks) is semantically preserved — any test that sent a model with a legacy field like `arrivals` at root level and expected a tier-1 error must still receive a tier-1 error, because `ModelSchemaValidator` catches it via root `additionalProperties: false`.
- When updating test phrasing, prefer semantic assertions (`errors.Should().Contain(e => e.Contains("bins"))`) over exact-string assertions where the exact string is not the contract.
- The CLI and API both surface error strings to end users. Inspect the UIs (Blazor `ModelEditor.razor`, any Svelte form) to confirm no regex-parses the validator output. Spot-check via test, not just grep.

## Surfaces touched

- `src/FlowTime.API/Program.cs`
- `src/FlowTime.Cli/Program.cs`
- `src/FlowTime.TimeMachine/Validation/TimeMachineValidator.cs`
- `tests/FlowTime.Integration.Tests/SimToEngineWorkflowTests.cs`
- `tests/FlowTime.Tests/Schema/*` (three files rewritten)
- `work/epics/E-23-model-validation-consolidation/m-E23-02-call-site-migration-tracking.md` (new)

## Out of Scope

- Deleting `ModelValidator.cs` — that is m-E23-03.
- Any error-message rewording inside `ModelSchemaValidator`.
- Any schema edit.
- Any UI code change, unless the phrasing audit uncovers a regex-parse consumer, in which case the UI update is the minimum needed to preserve user-visible behavior.

## Dependencies

- m-E23-01 complete and merged into `epic/E-23-model-validation-consolidation` (schema accepts every current template).

## References

- Epic spec: `work/epics/E-23-model-validation-consolidation/spec.md`
- m-E23-01 spec and tracking doc (for the rule audit baseline)
- Tiered validator: `src/FlowTime.TimeMachine/Validation/TimeMachineValidator.cs`
- Current call sites as of 2026-04-23: `grep -rn "ModelValidator\\.Validate" --include="*.cs"` returns the four production sites and the five test files listed above
