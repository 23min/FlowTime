---
id: m-E24-03-schema-unification
epic: E-24-schema-alignment
status: complete
depends_on: m-E24-02-unify-model-type
started: 2026-04-25
completed: 2026-04-25
---

# m-E24-03: Schema Unification

## Goal

Rewrite `docs/schemas/model.schema.yaml` so it describes the unified type exactly. camelCase throughout. `provenance` is declared as a first-class block on the unified schema with camelCase keys. Every m-E24-01 decision about `outputs[].as`, `classes: minItems`, `nodes[].source`, and provenance shape is applied. `docs/schemas/README.md` is rewritten to reflect the one-schema reality. Any architecture doc that described the two-schema world is corrected or archived.

## Context

After m-E24-02, the unified type exists and Sim emits it directly. The schema must describe that unified type — not the old `SimModelArtifact` shape, not the old `ModelDefinition` shape, but the single type both sides now share. Every snake_case field in the `provenance` block is renamed to camelCase per the project rule ("JSON payloads and schemas use camelCase"). Every field m-E24-01 decided to keep gets a consumer citation in its description. Every field m-E24-01 decided to drop from emission is absent from the schema. The schema becomes readable top-to-bottom as the single structural contract.

## Acceptance Criteria

1. **Schema describes the unified type exactly.** Every property of the unified type (post-m-E24-02) has a declaration in `docs/schemas/model.schema.yaml`. Every schema declaration maps to a field on the unified type. No schema field exists that the unified type does not declare; no unified-type field is undeclared in the schema.
2. **camelCase everywhere.** `grep -rn "generated_at\|model_id\|template_id\|template_version" docs/schemas/model.schema.yaml` returns zero hits. The `provenance` block uses `generatedAt`, `modelId`, `templateId`, `templateVersion` (and any other provenance keys per m-E24-01's final list).
3. **`provenance` block matches emitted shape.** Fields retained per m-E24-01: the final list. The shape (flat vs nested, specifically for `parameters`) matches m-E24-01's ratified shape. `additionalProperties: false` sits on the provenance block.
4. **`grid.start` declared.** `start` is declared under `grid.properties` as `type: string, format: date-time`, not in `grid.required`. Models authored without `start` continue to validate.
5. **`nodes[].metadata` declared.** Per m-E24-01's keep decision. Under `nodes[].properties`, `metadata` is `type: object, additionalProperties: {type: string}` with a description citing `GraphService.ResolveDisplayKind`, `StateQueryService.cs:2162`, and `RunArtifactWriter.cs:516` as consumers.
6. **`nodes[].source` resolved per m-E24-01.** If decision was "drop": absent from schema (and from emission per m-E24-02). If decision was "declare now as optional": present with forward-shape description cited. Default path is "drop."
7. **`outputs[].as` cascade resolved per m-E24-01.** If "Sim synthesizes `as`": schema retains `outputs[].required: [series, as]`. If "optional + emitter omits": schema uses `outputs[].required: [series]` with `as` optional. The chosen side is implemented here.
8. **`classes: minItems` resolved per m-E24-01.** Either `minItems: 1` stays and m-E24-02 landed the empty-list emitter guard, or `minItems` is removed. Consistent with m-E24-02's emitter behavior.
9. **Leaked-state fields absent.** Root `additionalProperties: false` remains. The root `properties` list contains exactly the fields the unified type serializes — no `window`, no top-level `mode`, no top-level `metadata`, no `generator` (unless m-E24-01 explicitly moved one of them into the unified submission type, which is not the default path).
10. **`POST /v1/validate` at Analyse reports `val-err=0` on all twelve templates except ParseScalar residuals.** Post-m-E24-03, run the canary. Every template in `templates/*.yaml` with default parameters validates clean under `ModelSchemaValidator.Validate(...)` except for the ~231 `ParseScalar` residuals that m-E24-04 fixes. Tracking doc records the full histogram.
11. **Schema readability pass complete.** Every property's description field includes a one-line consumer citation so a reviewer opening the schema understands who reads each field.
12. **`docs/schemas/README.md` rewritten.** The file describes the one-schema reality. The 2026-04-24 claim of two-schema parity is removed. Any reference to `SimModelArtifact` or the old two-type split is deleted or converted to historical-only language in `docs/archive/`.
13. **Architecture docs audited.** `docs/architecture/` entries that described the two-schema world are either corrected or archived. The tracking doc lists every audited file and its disposition.
14. **Full `.NET` suite green.** `dotnet test FlowTime.sln` passes. No regressions in `TimeMachineValidator`, `ModelSchemaValidator`, or schema-adjacent tests.

## Constraints

- **Schema changes in one cut.** No schema-draft bump, no multi-version acceptance, no snake_case aliases. The provenance rename is forward-only.
- **No new adjunct rules.** Every schema rule is either declarative JSON Schema (draft-07) or moved into `ModelSchemaValidator` as an adjunct following the existing `ValidateClassReferences` pattern if and only if it cannot be expressed declaratively. m-E24-01 flagged any such cases; if none, no adjuncts are added.
- **Description citations are precise.** "Consumer: `src/FlowTime.Core/Graphs/GraphService.cs:ResolveDisplayKind` reads `origin.kind`" — not "used by GraphService." Reviewers verify by opening the cited line.
- **No decorative fields.** If a field has no Engine consumer, it does not appear in the schema. Sim-only fields live (at all) only if they survived m-E24-02's drop pass, which should not happen under Option E.
- **Pre- and post-canary captured.** Tracking doc records the canary histogram from m-E24-02 close and m-E24-03 close. The delta is m-E24-03's contribution; the residual should be only the ParseScalar shapes.

## Design Notes

- Work order: (1) apply provenance camelCase rename; (2) walk the schema top-to-bottom and reconcile every property with the unified type's public members; (3) remove leaked-state fields if any still appear; (4) apply each m-E24-01 decision (outputs cascade, classes minItems, nodes source) in inventory order; (5) rewrite descriptions with consumer citations; (6) rewrite `docs/schemas/README.md`; (7) audit architecture docs.
- The schema file is YAML, not JSON (matches existing style). Preserve block-scalar style for multi-line descriptions.
- Canary run: use `TemplateWarningSurveyTests` against a live Engine API (`start-api` task). The test dumps per-template first-err and totals. Post-m-E24-03 the non-ParseScalar residuals should be 0; the ParseScalar residuals (~231) close in m-E24-04.
- If the canary surfaces a residual shape not in m-E24-01's inventory, stop — do not widen scope silently. File as a gap or amendment, escalate to reviewer.

## Surfaces touched

- `docs/schemas/model.schema.yaml` (rewrite)
- `docs/schemas/README.md` (rewrite)
- `docs/architecture/` entries that describe schemas (audit + update or archive per tracking doc)
- `work/epics/E-24-schema-alignment/m-E24-03-schema-unification-tracking.md` (new)
- Potentially `src/FlowTime.Core/Models/ModelSchemaValidator.cs` only if m-E24-01 identified a rule requiring an adjunct; default path is no code changes

## Out of Scope

- Code changes to the unified type — m-E24-02 owns.
- `ParseScalar` validator fix — m-E24-04 owns.
- Canary hard-assertion promotion — m-E24-05 owns.
- Any UI change.
- `Template` (authoring-time) schema changes.

## Dependencies

- m-E24-02 landed. The unified type exists, Sim emits it, Engine parses it. The schema now has a target to describe.
- m-E24-01 decisions ratified.

## References

- Epic spec: `work/epics/E-24-schema-alignment/spec.md`
- m-E24-01 tracking doc: `work/epics/E-24-schema-alignment/m-E24-01-inventory-and-design-decisions-tracking.md`
- m-E24-02 tracking doc: `work/epics/E-24-schema-alignment/m-E24-02-unify-model-type-tracking.md`
- m-E23-01 full-shape audit (histogram input): `work/epics/E-23-model-validation-consolidation/m-E23-01-schema-alignment-tracking.md` → "AC4 canary re-run, full-shape audit (2026-04-24)"
- Schema file: `docs/schemas/model.schema.yaml`
- Schema README (to be rewritten): `docs/schemas/README.md`
- Canary test: `tests/FlowTime.Integration.Tests/TemplateWarningSurveyTests.cs`
- Project rule (camelCase): `.ai-repo/rules/project.md` → Coding Conventions → "JSON payloads and schemas use camelCase"
