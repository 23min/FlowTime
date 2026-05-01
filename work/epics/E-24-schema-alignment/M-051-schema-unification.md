---
id: M-051
title: Schema Unification
status: done
parent: E-24
acs:
  - id: AC-1
    title: Schema describes the unified type exactly
    status: met
  - id: AC-2
    title: camelCase everywhere
    status: met
  - id: AC-3
    title: provenance block matches emitted shape
    status: met
  - id: AC-4
    title: grid.start declared
    status: met
  - id: AC-5
    title: nodes[].metadata declared
    status: met
  - id: AC-6
    title: nodes[].source resolved per M-049
    status: met
  - id: AC-7
    title: outputs[].as cascade resolved per M-049
    status: met
  - id: AC-8
    title: 'classes: minItems resolved per M-049'
    status: met
  - id: AC-9
    title: Leaked-state fields absent
    status: met
  - id: AC-10
    title: POST /v1/validate at Analyse reports val-err=0 on all twelve
    status: met
  - id: AC-11
    title: Schema readability pass complete
    status: met
  - id: AC-12
    title: docs/schemas/README.md rewritten
    status: met
  - id: AC-13
    title: Architecture docs audited
    status: met
  - id: AC-14
    title: Full .NET suite green
    status: met
---

## Goal

Rewrite `docs/schemas/model.schema.yaml` so it describes the unified type exactly. camelCase throughout. `provenance` is declared as a first-class block on the unified schema with camelCase keys. Every M-049 decision about `outputs[].as`, `classes: minItems`, `nodes[].source`, and provenance shape is applied. `docs/schemas/README.md` is rewritten to reflect the one-schema reality. Any architecture doc that described the two-schema world is corrected or archived.

## Context

After M-050, the unified type exists and Sim emits it directly. The schema must describe that unified type — not the old `SimModelArtifact` shape, not the old `ModelDefinition` shape, but the single type both sides now share. Every snake_case field in the `provenance` block is renamed to camelCase per the project rule ("JSON payloads and schemas use camelCase"). Every field M-049 decided to keep gets a consumer citation in its description. Every field M-049 decided to drop from emission is absent from the schema. The schema becomes readable top-to-bottom as the single structural contract.

## Acceptance criteria

### AC-1 — Schema describes the unified type exactly

**Schema describes the unified type exactly.** Every property of the unified type (post-m-E24-02) has a declaration in `docs/schemas/model.schema.yaml`. Every schema declaration maps to a field on the unified type. No schema field exists that the unified type does not declare; no unified-type field is undeclared in the schema.
### AC-2 — camelCase everywhere

**camelCase everywhere.** `grep -rn "generated_at\|model_id\|template_id\|template_version" docs/schemas/model.schema.yaml` returns zero hits. The `provenance` block uses `generatedAt`, `modelId`, `templateId`, `templateVersion` (and any other provenance keys per M-049's final list).
### AC-3 — provenance block matches emitted shape

**`provenance` block matches emitted shape.** Fields retained per M-049: the final list. The shape (flat vs nested, specifically for `parameters`) matches M-049's ratified shape. `additionalProperties: false` sits on the provenance block.
### AC-4 — grid.start declared

**`grid.start` declared.** `start` is declared under `grid.properties` as `type: string, format: date-time`, not in `grid.required`. Models authored without `start` continue to validate.
### AC-5 — nodes[].metadata declared

**`nodes[].metadata` declared.** Per M-049's keep decision. Under `nodes[].properties`, `metadata` is `type: object, additionalProperties: {type: string}` with a description citing `GraphService.ResolveDisplayKind`, `StateQueryService.cs:2162`, and `RunArtifactWriter.cs:516` as consumers.
### AC-6 — nodes[].source resolved per M-049

**`nodes[].source` resolved per M-049.** If decision was "drop": absent from schema (and from emission per M-050). If decision was "declare now as optional": present with forward-shape description cited. Default path is "drop."
### AC-7 — outputs[].as cascade resolved per M-049

**`outputs[].as` cascade resolved per M-049.** If "Sim synthesizes `as`": schema retains `outputs[].required: [series, as]`. If "optional + emitter omits": schema uses `outputs[].required: [series]` with `as` optional. The chosen side is implemented here.
### AC-8 — classes: minItems resolved per M-049

**`classes: minItems` resolved per M-049.** Either `minItems: 1` stays and M-050 landed the empty-list emitter guard, or `minItems` is removed. Consistent with M-050's emitter behavior.
### AC-9 — Leaked-state fields absent

**Leaked-state fields absent.** Root `additionalProperties: false` remains. The root `properties` list contains exactly the fields the unified type serializes — no `window`, no top-level `mode`, no top-level `metadata`, no `generator` (unless M-049 explicitly moved one of them into the unified submission type, which is not the default path).
### AC-10 — POST /v1/validate at Analyse reports val-err=0 on all twelve

**`POST /v1/validate` at Analyse reports `val-err=0` on all twelve templates except ParseScalar residuals.** Post-m-E24-03, run the canary. Every template in `templates/*.yaml` with default parameters validates clean under `ModelSchemaValidator.Validate(...)` except for the ~231 `ParseScalar` residuals that M-052 fixes. Tracking doc records the full histogram.
### AC-11 — Schema readability pass complete

**Schema readability pass complete.** Every property's description field includes a one-line consumer citation so a reviewer opening the schema understands who reads each field.
### AC-12 — docs/schemas/README.md rewritten

**`docs/schemas/README.md` rewritten.** The file describes the one-schema reality. The 2026-04-24 claim of two-schema parity is removed. Any reference to `SimModelArtifact` or the old two-type split is deleted or converted to historical-only language in `docs/archive/`.
### AC-13 — Architecture docs audited

**Architecture docs audited.** `docs/architecture/` entries that described the two-schema world are either corrected or archived. The tracking doc lists every audited file and its disposition.
### AC-14 — Full .NET suite green

**Full `.NET` suite green.** `dotnet test FlowTime.sln` passes. No regressions in `TimeMachineValidator`, `ModelSchemaValidator`, or schema-adjacent tests.
## Constraints

- **Schema changes in one cut.** No schema-draft bump, no multi-version acceptance, no snake_case aliases. The provenance rename is forward-only.
- **No new adjunct rules.** Every schema rule is either declarative JSON Schema (draft-07) or moved into `ModelSchemaValidator` as an adjunct following the existing `ValidateClassReferences` pattern if and only if it cannot be expressed declaratively. M-049 flagged any such cases; if none, no adjuncts are added.
- **Description citations are precise.** "Consumer: `src/FlowTime.Core/Graphs/GraphService.cs:ResolveDisplayKind` reads `origin.kind`" — not "used by GraphService." Reviewers verify by opening the cited line.
- **No decorative fields.** If a field has no Engine consumer, it does not appear in the schema. Sim-only fields live (at all) only if they survived M-050's drop pass, which should not happen under Option E.
- **Pre- and post-canary captured.** Tracking doc records the canary histogram from M-050 close and M-051 close. The delta is M-051's contribution; the residual should be only the ParseScalar shapes.

## Design Notes

- Work order: (1) apply provenance camelCase rename; (2) walk the schema top-to-bottom and reconcile every property with the unified type's public members; (3) remove leaked-state fields if any still appear; (4) apply each M-049 decision (outputs cascade, classes minItems, nodes source) in inventory order; (5) rewrite descriptions with consumer citations; (6) rewrite `docs/schemas/README.md`; (7) audit architecture docs.
- The schema file is YAML, not JSON (matches existing style). Preserve block-scalar style for multi-line descriptions.
- Canary run: use `TemplateWarningSurveyTests` against a live Engine API (`start-api` task). The test dumps per-template first-err and totals. Post-m-E24-03 the non-ParseScalar residuals should be 0; the ParseScalar residuals (~231) close in M-052.
- If the canary surfaces a residual shape not in M-049's inventory, stop — do not widen scope silently. File as a gap or amendment, escalate to reviewer.

## Surfaces touched

- `docs/schemas/model.schema.yaml` (rewrite)
- `docs/schemas/README.md` (rewrite)
- `docs/architecture/` entries that describe schemas (audit + update or archive per tracking doc)
- `work/epics/E-24-schema-alignment/m-E24-03-schema-unification-tracking.md` (new)
- Potentially `src/FlowTime.Core/Models/ModelSchemaValidator.cs` only if M-049 identified a rule requiring an adjunct; default path is no code changes

## Out of Scope

- Code changes to the unified type — M-050 owns.
- `ParseScalar` validator fix — M-052 owns.
- Canary hard-assertion promotion — M-053 owns.
- Any UI change.
- `Template` (authoring-time) schema changes.

## Dependencies

- M-050 landed. The unified type exists, Sim emits it, Engine parses it. The schema now has a target to describe.
- M-049 decisions ratified.

## References

- Epic spec: `work/epics/E-24-schema-alignment/spec.md`
- M-049 tracking doc: `work/epics/E-24-schema-alignment/m-E24-01-inventory-and-design-decisions-tracking.md`
- M-050 tracking doc: `work/epics/E-24-schema-alignment/m-E24-02-unify-model-type-tracking.md`
- M-046 full-shape audit (histogram input): `work/epics/E-23-model-validation-consolidation/m-E23-01-schema-alignment-tracking.md` → "AC4 canary re-run, full-shape audit (2026-04-24)"
- Schema file: `docs/schemas/model.schema.yaml`
- Schema README (to be rewritten): `docs/schemas/README.md`
- Canary test: `tests/FlowTime.Integration.Tests/TemplateWarningSurveyTests.cs`
- Project rule (camelCase): `.ai-repo/rules/project.md` → Coding Conventions → "JSON payloads and schemas use camelCase"
