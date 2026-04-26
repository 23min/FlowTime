# m-E23-01: Rule-Coverage Audit — Tracking

**Started:** 2026-04-26
**Completed:** pending
**Branch:** `milestone/m-E23-01-rule-coverage-audit` (branched from `epic/E-23-model-validation-consolidation` at `9cae437`)
**Spec:** `work/epics/E-23-model-validation-consolidation/m-E23-01-rule-coverage-audit.md`
**Commits:** _pending_

## Acceptance Criteria

- [ ] **AC1: Full embedment inventory.** Machine-reviewable table enumerating every rule encoded in (a) `ModelValidator.cs`, (b) `ModelParser.cs`, (c) `SimModelBuilder.cs`, (d) `RunOrchestrationService.cs` + `GraphService.cs`. Each entry: file:line range, rule in plain English, current `model.schema.yaml` expression form (if any).
- [ ] **AC2: Per-rule disposition.** Every rule classified as exactly one of: schema-covered, schema-add, adjunct, parser-justified, drop. Each disposition cited (line numbers / rationale / "no live consumer" note).
- [ ] **AC3: Schema additions land.** Every `schema-add` rule declared in `docs/schemas/model.schema.yaml` with a `# rule from ModelValidator.cs:N — added m-E23-01` citation comment.
- [ ] **AC4: Adjunct additions land.** Every `adjunct` rule implemented as a named method on `ModelSchemaValidator` (sibling to `ValidateClassReferences`), invoked from `Validate`, with at least one negative-case unit test.
- [ ] **AC5: Coverage canary stays green.** `TemplateWarningSurveyTests.Survey_Templates_For_Warnings` continues to report `val-err == 0` across all twelve templates at `ValidationTier.Analyse` after schema/adjunct additions.
- [ ] **AC6: Negative-case canary.** New `tests/FlowTime.Core.Tests/Schema/RuleCoverageRegressionTests.cs` (or analogous location) feeds a deliberately-invalid model snippet for each non-trivial rule; asserts `ModelSchemaValidator.Validate(...).IsValid == false` plus error-substring containment.
- [ ] **AC7: No call-site migration in this milestone.** `grep -rn "ModelValidator\.Validate" --include="*.cs"` returns the same call sites at end-of-milestone as at start. `ModelValidator.cs` unchanged.
- [ ] **AC8: No behaviour regression.** `dotnet test FlowTime.sln` green; UI suites untouched.
- [ ] **AC9: Audit doc committed.** This tracking doc contains the full embedment table, per-rule disposition, schema-edit / adjunct-edit citations, and the negative-case test catalogue. Reviewable at PR time without running any tool.

## Decisions made during implementation

<!-- Decisions that came up mid-work that were NOT pre-locked in the milestone spec.
     For each: what was decided, why, and a link to a decision record if one was
     opened. If no new decisions arose, say "None — all decisions are pre-locked
     in the milestone spec." -->

- (none yet)

## Work Log

<!-- One entry per AC (preferred). First line: one-line outcome · commit <SHA> · tests <N/M> -->

(work-log entries land as ACs are completed)

## Reviewer notes (optional)

- The audit is the deliverable. Schema and adjunct edits are by-products of doing the audit honestly. Reviewers should verify that every `schema-add` row in the embedment table corresponds to a real schema diff, every `adjunct` row corresponds to a real method on `ModelSchemaValidator`, and every `parser-justified` row carries a written rationale (not just an assertion).
- Cross-repo grep for `ModelValidator` callers in sibling checkouts (`flowtime-sim-vnext`, etc.) is part of AC1's scope. If callers exist, the milestone notes them; m-E23-02 absorbs or coordinates deletion.

## Validation

- `dotnet build FlowTime.sln` — green required at every commit.
- `dotnet test FlowTime.sln` — green at milestone close.
- `TemplateWarningSurveyTests.Survey_Templates_For_Warnings` — `val-err == 0` (E-24's hard-asserting canary stays green).
- `RuleCoverageRegressionTests` (new) — every audited rule has at least one negative-case test that fires.
- Build cost: default `dotnet build` unaffected; analyzer-enabled build (`/p:RoslynatorAnalyze=true`) ~50 s.

## Deferrals

<!-- Work observed during this milestone but deliberately not done. Mirror into work/gaps.md. -->

- (filed during work)

## Initial context

This milestone replaces the original `m-E23-01-schema-alignment` milestone. The original was largely absorbed by E-24 m-E24-03 (schema rewrite) and m-E24-05 (canary commit + hard-assertion promotion). The unowned piece — the rule-by-rule audit ensuring `ModelSchemaValidator` + the schema together cover every rule `ModelValidator` enforces — becomes the new m-E23-01 focus. Slug change resolves collision with E-24's "schema-alignment" slug.

The pre-rescope branch `milestone/m-E23-01-schema-alignment` and `stash@{0}` are obsolete (their content was absorbed by E-24 milestones); flagged for retirement. This milestone branches fresh from post-E-24 `epic/E-23-model-validation-consolidation` (which == `main` at `9cae437`).

**Spirit (per epic spec rewrite 2026-04-26):** `model.schema.yaml` is the only declarative source of structural truth; `ModelSchemaValidator` is the only runtime evaluator. Eliminate every "embedded schema" — every place outside the canonical schema where model rules are re-encoded. E-24 closed the type and schema-document embedments; E-23 closes the rule-evaluator embedment. This milestone is the prep work that makes m-E23-02's call-site migration safe (every rule has a known canonical home before any call site flips).
