---
id: M-046
title: Rule-Coverage Audit
status: done
parent: E-23
acs:
  - id: AC-1
    title: Full embedment inventory
    status: met
  - id: AC-2
    title: Per-rule disposition
    status: met
  - id: AC-3
    title: Schema additions land
    status: met
  - id: AC-4
    title: Adjunct additions land
    status: met
  - id: AC-5
    title: Coverage canary stays green
    status: met
  - id: AC-6
    title: Negative-case canary
    status: met
  - id: AC-7
    title: No call-site migration in this milestone
    status: met
  - id: AC-8
    title: No behaviour regression
    status: met
  - id: AC-9
    title: Audit doc committed
    status: met
---

## Goal

Prove — by file-and-line citation — that `docs/schemas/model.schema.yaml` plus `ModelSchemaValidator` (including any named adjuncts) together enforce every structural and semantic rule about the post-substitution model that any other component currently enforces. Identify every "embedded schema" — every place outside the canonical schema where model rules are re-encoded — and decide for each rule whether it (a) is already covered by the schema, (b) needs to move into the schema in this milestone, (c) needs to live as a named `ModelSchemaValidator` adjunct (alongside `ValidateClassReferences`), (d) is acceptable as parser/emitter logic with documented justification, or (e) is dead and can be dropped. No production code change in this milestone — output is a coverage-audit document that gates M-047. Schema or adjunct edits are landed only when the audit shows a rule would otherwise be lost.

## Context

E-23's spirit, restated post-E-24: **`model.schema.yaml` is the only declarative source of structural truth about the post-substitution model; `ModelSchemaValidator` is the only runtime evaluator.** Any rule encoded elsewhere is an "embedded schema" — a parallel place where model truth lives, and therefore a place that can drift.

E-24 closed two of the embedments: the *type* (`SimModelArtifact` + `ModelDefinition` collapsed to `ModelDto` in `FlowTime.Contracts`) and the *schema document* (`model.schema.yaml` rewritten top-to-bottom against `ModelDto`, hard-asserted at `val-err == 0` across the twelve templates). That is the foundation. The remaining embedments — the ones E-23 owns — are:

1. **`src/FlowTime.Core/Models/ModelValidator.cs`** (214 lines) — hand-rolled imperative validator with ~25 rules. It is the validator that actually runs on `POST /v1/run`, the Engine CLI, and inside `TimeMachineValidator` tier-1 today. Sample of rules it enforces:
   - `binMinutes is no longer supported` (legacy field rejection — line 108)
   - `bins must be between 1 and 10000` (range — line 123)
   - `binSize must be between 1 and 1000` (range — line 137)
   - `binUnit must be one of: minutes/hours/days/weeks` (enum — line 155)
   - `expression field is no longer supported, use expr instead` (legacy field — line 175)
   - `schemaVersion must be 1` (const — line 60)
   - Top-level `arrivals`/`route` rejection (legacy shape — lines 39, 43)
2. **`src/FlowTime.Core/Models/ModelParser.cs`** (733 lines) — parses YAML into `ModelDto` for the Engine. Any tolerated unknown field, any silently-applied default, any "if missing, do X" branch is an unwritten rule.
3. **`src/FlowTime.Sim.Core/Templates/SimModelBuilder.cs`** (458 lines) — emits `ModelDto`. E-24 closed leaked-state emission (`window`, `generator`, top-level `metadata`, top-level `mode`); this milestone re-checks for any *remaining* implicit emission contract (e.g., default values, conditional field omission) that would constitute an unwritten rule.
4. **`src/FlowTime.TimeMachine/Orchestration/RunOrchestrationService.cs`** and `src/FlowTime.Core/Graph/GraphService.cs` — post-parse paths that may apply rules (e.g., "node X must reference a defined class") which should either be schema-expressible or live as a named `ModelSchemaValidator` adjunct.

`ModelSchemaValidator` already runs schema evaluation plus the `ValidateClassReferences` adjunct. If a rule from the audit cannot be expressed declaratively in JSON Schema draft-07 (e.g., cross-reference checks), it lands as a sibling adjunct in `ModelSchemaValidator`. If it can be expressed, it lands in the schema. **There is no third option** — we do not retain a parallel imperative validator, and we do not leave rules implicit in parser/emitter code.

After this milestone, M-047 has a written contract: every rule has a single canonical home, and that home is reachable from `ModelSchemaValidator.Validate`. The call-site migration is then a mechanical type-name swap.

## Acceptance criteria

### AC-1 — Full embedment inventory

**Full embedment inventory.** A machine-reviewable table in the milestone tracking doc enumerates every rule encoded in: (a) `ModelValidator.cs`, (b) `ModelParser.cs` (tolerations, silent defaults, conditional-omission rules), (c) `SimModelBuilder.cs` (post-E-24 — any remaining implicit emission contract), (d) `RunOrchestrationService.cs` and `GraphService.cs` post-parse model checks. Each entry cites file + line range, the rule in plain English, and the expression form (if any) currently in `model.schema.yaml`.
### AC-2 — Per-rule disposition

**Per-rule disposition.** For every rule, the table records exactly one of:
- **schema-covered** — already declared in `model.schema.yaml`, with a line-number citation.
- **schema-add** — expressible in JSON Schema draft-07 and added to `model.schema.yaml` in this milestone, with a line-number citation to the new declaration.
- **adjunct** — not expressible declaratively; added to `ModelSchemaValidator` as a named method alongside `ValidateClassReferences`, with a line-number citation to the new method.
- **parser-justified** — left in parser/emitter code with a written rationale tying it to a `ModelDto` invariant or a YamlDotNet behavior that does not surface as a structural rule (e.g., scalar-style coercion is parser concern, not a model rule). The rationale is written into the audit doc, not just stated.
- **drop** — has no live consumer and is removed in this milestone, with a one-line note on why dropping is safe.
### AC-3 — Schema additions land

**Schema additions land.** Any rule classified `schema-add` is added to `docs/schemas/model.schema.yaml` in this milestone. Each addition is accompanied by a one-line citation comment in the schema (`# rule from ModelValidator.cs:N — added m-E23-01`) so future readers can trace the provenance.
### AC-4 — Adjunct additions land

**Adjunct additions land.** Any rule classified `adjunct` is added to `ModelSchemaValidator` as a named method invoked from `Validate`. Each adjunct method has at least one unit test that exercises the rule in isolation (red → green from a deliberately-violating model snippet).
### AC-5 — Coverage canary stays green

**Coverage canary stays green.** After all schema and adjunct additions, `TemplateWarningSurveyTests.Survey_Templates_For_Warnings` continues to report `val-err == 0` across all twelve templates at `ValidationTier.Analyse`. The canary is the regression guard — adding rules must not break currently-valid templates.
### AC-6 — Negative-case canary

**Negative-case canary.** A new test (`tests/FlowTime.Core.Tests/Schema/RuleCoverageRegressionTests.cs` or equivalent location) feeds a deliberately-invalid model snippet for each non-trivial rule covered by the audit and asserts `ModelSchemaValidator.Validate(...).IsValid == false`. This is the proof that `ModelSchemaValidator` actually catches every rule the audit claims it covers, before M-047 routes production traffic through it.
### AC-7 — No call-site migration in this milestone

**No call-site migration in this milestone.** `grep -rn "ModelValidator\.Validate" --include="*.cs"` outside `.claude/worktrees/` returns the same call sites at the end of the milestone as at the start. The only allowed production-code diffs are: schema edits in (3), adjunct additions in (4), unit tests in (4) and (6). `ModelValidator.cs` is unchanged in this milestone — it is still the validator on the `/v1/run` and CLI paths.
### AC-8 — No behaviour regression

**No behaviour regression.** `dotnet test FlowTime.sln` is green (baseline + the new negative-case canary). UI suites untouched. `POST /v1/run` continues to accept every currently-valid template because `ModelValidator` — still the active `/v1/run` validator — is unmodified.
### AC-9 — Audit doc committed

**Audit doc committed.** The tracking doc `m-E23-01-rule-coverage-audit-tracking.md` lives in the epic folder and contains the full embedment table, per-rule disposition, schema-edit / adjunct-edit citations, and the negative-case test catalogue. Reviewable at PR time without running any tool.
## Constraints

- **Schema-first preference.** When a rule is expressible in JSON Schema draft-07, it belongs in the schema, not as an adjunct. The schema already declares enums (`binUnit`), ranges (via `minimum`/`maximum`), const values (`schemaVersion: 1`), and additionalProperties policies. A rule should only become an adjunct when JSON Schema draft-07 *cannot* express it (cross-field references, conditional rules dependent on runtime data).
- **No schema-draft bump.** The schema declares `$schema: "https://json-schema.org/draft-07/schema#"`. If a rule cannot be expressed in draft-07 but could be expressed in draft-2020-12, it lands as an adjunct, not a draft bump. Draft migration is a separate concern.
- **Parser-justified is a written justification, not a default.** If a rule lands in `parser-justified`, the audit doc must answer: "why is it not a model rule?" Acceptable answers are narrow — typically "this is a YAML representation concern, not a model concern" (e.g., `ParseScalar` style handling, fixed by E-24 M-052). "It's hard to move" is not a justification.
- **No reintroduction of deprecated fields.** `binMinutes` rejection stays. Legacy `arrivals`/`route` rejection stays. Legacy node-level `expression` rejection stays. These rules must have a home in the schema by milestone close.
- **Cross-repo grep included.** The audit grep extends to sibling checkouts visible from this workspace (treated read-only). If `ModelValidator` is referenced from a sibling (`flowtime-sim-vnext`, MCP, etc.), the audit notes it; M-047 either absorbs the migration or coordinates deletion with the sibling.

## Design Notes

- The audit is structured as one table-row per rule, not per file. A rule like "`bins` must be between 1 and 10000" gets one row even if it is enforced at multiple sites today; the disposition column then says where it ends up post-milestone.
- For `ModelValidator.cs`, expect 20–25 rule rows (rough count from a glance at the file). For the parser and emitter, expect 5–15 rows each. For orchestration / graph services, expect 0–5 rows.
- The negative-case canary tests are small and focused. One short model snippet per rule, deliberately violating the rule, asserting the validator catches it. Total file size under 500 lines.
- Adjunct methods follow `ValidateClassReferences`'s shape: a `private static` method on `ModelSchemaValidator` that reads `ModelDto` (post-parse) and adds errors to the result. They run after JSON-schema evaluation, not before.
- The tracking doc is the deliverable. The schema and adjunct edits are by-products of doing the audit honestly.

## Surfaces touched

- `work/epics/E-23-model-validation-consolidation/m-E23-01-rule-coverage-audit-tracking.md` (new)
- `docs/schemas/model.schema.yaml` (additions only, with citation comments)
- `src/FlowTime.Core/Models/ModelSchemaValidator.cs` (adjunct additions only — no rewiring of existing logic)
- `tests/FlowTime.Core.Tests/Schema/RuleCoverageRegressionTests.cs` (new, or analogous filename)

## Out of Scope

- Any edit to `ModelValidator.cs`, any call-site migration, any deletion — M-047 and M-048 territory.
- Any edit to Sim's emission shape beyond what the audit identifies as a rule. E-24 closed leaked-state emission; the audit only revisits the emitter if a rule is unwritten there.
- Any UI change. Blazor and Svelte surfaces stay on their current contracts.
- Rewording or restructuring of `ModelSchemaValidator`'s existing error messages — error-phrasing is M-047's concern.
- Schema-draft migration (draft-07 → draft-2020-12).

## Dependencies

- Epic E-23 spec ratified (this milestone is the audit gate; without it M-047 risks silent rule loss).
- E-24 Schema Alignment closed (cleared 2026-04-25, `D-2026-04-25-038`).
- Epic integration branch `epic/E-23-model-validation-consolidation` exists and is based on `main` post-E-24 merge.

## References

- Epic spec: `work/epics/E-23-model-validation-consolidation/spec.md`
- E-24 closure: `work/decisions.md` → `D-2026-04-25-038`
- Truth Discipline (2026-04-23): `.ai-repo/rules/project.md` → `"'API stability' does not mean 'keep old functions around.'"` + `"Do not let adapter/UI projection become the only place where semantics exist"` + `"Do not restate a canonical contract in many places from memory"`
- Current ModelValidator: `src/FlowTime.Core/Models/ModelValidator.cs` (214 lines)
- Current ModelSchemaValidator: `src/FlowTime.Core/Models/ModelSchemaValidator.cs` (263 lines)
- Current ModelParser: `src/FlowTime.Core/Models/ModelParser.cs` (733 lines)
- Current SimModelBuilder: `src/FlowTime.Sim.Core/Templates/SimModelBuilder.cs` (458 lines)
- Schema: `docs/schemas/model.schema.yaml`
- Canary: `tests/FlowTime.Integration.Tests/TemplateWarningSurveyTests.cs` (committed and hard-asserting per E-24 M-053)
