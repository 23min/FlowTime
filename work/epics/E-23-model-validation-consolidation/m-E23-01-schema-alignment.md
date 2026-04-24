---
id: m-E23-01-schema-alignment
epic: E-23-model-validation-consolidation
status: draft
depends_on:
completed:
---

# m-E23-01: Schema Alignment

## Goal

Update `docs/schemas/model.schema.yaml` so every template under `templates/*.yaml`, rendered with default parameters, validates cleanly against `ModelSchemaValidator`. Commit the template-warning survey test as a reproducible regression canary. No production code changes in this milestone.

## Context

Survey evidence (see epic Context) shows every current template produces at least one `ModelSchemaValidator` error at `/grid/start: All values fail against the false schema`. The schema forbids `grid.start` via `additionalProperties: false` on `grid` (line 71 of `model.schema.yaml`), but Sim deliberately emits it (`SimModelBuilder.cs:34-36`) and the Engine reads it (`ModelParser.cs:62`). The schema is the defective surface. This milestone fixes it and adds any other rule currently enforced only by `ModelValidator` so that the two validators fully converge — a prerequisite for m-E23-02's call-site migration and m-E23-03's delete.

Zero call-site changes here. The output is a schema update, a rule-audit document, and a committed survey test. Everything currently calling `ModelValidator` keeps calling it until m-E23-02.

## Acceptance Criteria

1. **Rule audit is recorded.** A machine-reviewable table in the milestone tracking doc enumerates every rule that `ModelValidator.Validate` currently enforces (lines 37–88 of `src/FlowTime.Core/Models/ModelValidator.cs`). For each rule the audit records: the current `ModelValidator` implementation, the corresponding `ModelSchemaValidator` check (if any), and the resolution — either "already covered by schema", "covered after this milestone's schema update" with a pointer to the schema line, or "adjunct method added to `ModelSchemaValidator`" with a pointer to the method.
2. **Schema allows `grid.start` as an optional ISO-8601 string.** `docs/schemas/model.schema.yaml` declares `start` under `grid.properties` as `type: string` with `format: date-time` (or equivalent pattern matching `DateTime.TryParse(..., CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, ...)`). The property is not in `grid.required`.
3. **Schema retains every currently-enforced rule.** `schemaVersion: const: 1` remains; root-level `additionalProperties: false` remains (rejecting legacy top-level `arrivals`/`route`); any rule identified in AC1 as missing from the schema and expressible in JSON Schema draft-07 is added. Any rule not expressible is moved into `ModelSchemaValidator` as an adjunct, *not* kept in `ModelValidator`.
4. **Every current template validates clean.** Running `TemplateWarningSurveyTests.Survey_Templates_For_Warnings` reports `val-err=0` for all twelve templates when using `ValidationTier.Analyse`. The totals line shows `validator-errors=0`.
5. **The survey test is committed to the repository.** `tests/FlowTime.Integration.Tests/TemplateWarningSurveyTests.cs` is added to the integration test project, compiles, and runs end-to-end against a live Engine API (skipping gracefully if port 8081 is unreachable, matching the existing skip pattern).
6. **No production code change.** Diff against `epic/E-23-model-validation-consolidation` touches only `docs/schemas/model.schema.yaml`, the new test file, the tracking doc, and (if AC3 requires) `src/FlowTime.Core/Models/ModelSchemaValidator.cs` for adjunct rules. `src/FlowTime.Core/Models/ModelValidator.cs` is unchanged in this milestone.
7. **No behavior regression.** `dotnet test FlowTime.sln` is green (baseline + any new tests). UI suites not touched. `POST /v1/run` continues to accept every currently-valid template because `ModelValidator` — still the active `/v1/run` validator — is unmodified.

## Constraints

- `grid.start` addition is purely permissive. It must not tighten any other rule or alter the `grid` object's `additionalProperties: false` behavior for any field other than `start`.
- The schema change is expressed in YAML, not JSON, to match the existing file style.
- If a rule from AC1 requires a schema-draft feature newer than draft-07 (what the file currently declares via `$schema: "https://json-schema.org/draft-07/schema#"`), the rule moves to an adjunct method; do not bump the schema draft version in this milestone.
- Keep the 2026-04-23 Truth Discipline guard front-of-mind: if an adjunct must be added, verify at review time that the rule still has live callers once m-E23-03 completes. A rule moved to an adjunct that has no runtime callers after delete is dead and must not be carried forward.

## Design Notes

- The rule audit must cite line numbers in both `ModelValidator.cs` and `model.schema.yaml` so reviewers can verify by opening two files side by side — not by running the validator.
- The schema addition for `grid.start` should follow the existing `provenance.generated_at` shape (`type: string`, `format: date-time`) for consistency.
- The survey test is intentionally an integration-style diagnostic, not a contract assertion. Commit it as-is (graceful-skip on engine unavailability); do not convert it to a unit test or remove the live HTTP calls. The value is end-to-end coverage across Sim template render + Engine validate + Engine run.

## Surfaces touched

- `docs/schemas/model.schema.yaml`
- `tests/FlowTime.Integration.Tests/TemplateWarningSurveyTests.cs` (new commit of existing file)
- `work/epics/E-23-model-validation-consolidation/m-E23-01-schema-alignment-tracking.md` (new)
- Possibly `src/FlowTime.Core/Models/ModelSchemaValidator.cs` if any rule requires an adjunct

## Out of Scope

- Any edit to `ModelValidator.cs` — that belongs to m-E23-03.
- Any edit to call sites of either validator — that belongs to m-E23-02.
- Any edit to Sim's `grid.start` emission.
- Any edit to Blazor or Svelte UI.
- Schema changes beyond what AC1 identifies — no opportunistic cleanup, no style passes, no reformatting.

## Dependencies

- Epic `E-23-model-validation-consolidation` spec approved.
- Epic integration branch `epic/E-23-model-validation-consolidation` exists.

## References

- Epic spec: `work/epics/E-23-model-validation-consolidation/spec.md`
- Survey test: `tests/FlowTime.Integration.Tests/TemplateWarningSurveyTests.cs` (uncommitted on `milestone/m-E21-06-heatmap-view`)
- Schema: `docs/schemas/model.schema.yaml`
- Current ModelValidator: `src/FlowTime.Core/Models/ModelValidator.cs`
- Current ModelSchemaValidator: `src/FlowTime.Core/Models/ModelSchemaValidator.cs`
