---
id: m-E24-01-inventory-and-design-decisions
epic: E-24-schema-alignment
status: in-progress
depends_on:
completed:
---

# m-E24-01: Inventory and Design Decisions

## Goal

Doc-only milestone. Produce the design-decision baseline the rest of E-24 executes against. Every field currently on `SimModelArtifact` and `ModelDefinition` gets a recorded decision (keep / drop / move into `provenance`) with rationale. The six open questions in the epic spec are resolved. The unified type's home, name, and provenance shape are named. The output is the resolution plan that m-E24-02 through m-E24-05 land without further design debate.

## Context

The epic's Option E framing (unify `SimModelArtifact` and `ModelDefinition` into a single type, forward-only) is ratified at the strategy level. This milestone turns that strategy into actionable per-field decisions and answers every design question m-E24-02 will need before it can start. Before unification changes any code, the foundational frame requires a written decision per field — the "easy option vs foundationally-right option" discipline from the user's "absolutely correct, future-proof. No fixes because they are easy. Do the right thing." direction.

Input material:

- **Investigation output (agent `a5aa3dfe26394aff5`):** the `SimModelArtifact` purpose analysis that established the split was accidental, not designed. Findings embedded in the epic spec's Context section.
- **Survey output (agent `a07d52c12dcaf3538`):** the 16-row divergence table and full-shape audit in `work/epics/E-23-model-validation-consolidation/m-E23-01-schema-alignment-tracking.md` → "AC4 canary re-run, full-shape audit (2026-04-24)".
- **Uncommitted m-E23-01 schema edits on branch `milestone/m-E23-01-schema-alignment`:** three schema additions (`grid.start`, `nodes[].metadata`, `nodes[].source`) — re-examined here under the unification framework, not treated as commitments.

The inventory covers the full union of fields on `SimModelArtifact` and `ModelDefinition` plus every distinct shape the survey identified. Each row ends with a named decision; no row remains open.

## Acceptance Criteria

1. **Full-field inventory complete.** The milestone tracking doc contains a row-per-field table covering every public member of `SimModelArtifact` and `ModelDefinition`, plus every distinct shape the m-E23-01 survey identified. Each row names: field path, emitter / producer (Sim or Engine), consumer (file:line if any) or "no consumer" (with grep verification note), current schema declaration, classification (keep-as-is / rename / move-to-provenance / drop / open), and decision column with chosen side.
2. **The six epic-spec open questions resolved.** For each question in the epic spec's Open Questions table, the tracking doc records the decision, the rationale, and the rejected alternative(s). Minimum answers required:
   - Unified type home (`FlowTime.Core` vs `FlowTime.Contracts` vs new namespace).
   - Unified type name (does `ModelDefinition` stay, or does it get renamed).
   - `outputs[].as` semantics (optional + emitter omits, or required + emitter synthesizes with named convention).
   - `nodes[].source` forward contract (drop until E-15, or declare optional now).
   - Provenance shape (flat vs nested, specifically for `parameters`).
   - Canary variant for m-E24-05 (integration test vs fast unit-style vs both).
3. **Leaked-state fields have drop plans.** `window`, `generator`, top-level `metadata`, and top-level `mode` each have a decision row. Default disposition is "drop from emission" with grep evidence that no Engine consumer reads them. If any row deviates (e.g. "move into `provenance`"), the new location and reader are named.
4. **`SimModelArtifact` satellite types have disposition plans.** `SimNode`, `SimOutput`, `SimProvenance`, `SimTraffic`, `SimArrival`, `SimArrivalPattern` each have a row naming whether they merge into the unified type's equivalents (`NodeDefinition`, `OutputDefinition`, etc.) as-is, with changes, or get deleted entirely. Any field asymmetries between Sim-side and Engine-side types (e.g. `SimNode` has fields `NodeDefinition` lacks, or vice versa) are flagged with a keep / drop decision.
5. **`provenance` block is named in full.** The final list of fields on the unified `provenance` block is written out with camelCase keys. Each field has a consumer citation and a rationale for inclusion. `schemaVersion` duplicate is decided (default: drop from provenance — top-level owns). `mode` is decided (default: keep in provenance as traceability; drop from top-level emission since Engine does not read it).
6. **Uncommitted m-E23-01 schema edits re-examined.** `grid.start`, `nodes[].metadata`, `nodes[].source` each have explicit keep / modify / revert decisions with rationale:
   - `grid.start`: default keep (Engine consumer confirmed at `ModelParser.cs:62`). Verify under unification.
   - `nodes[].metadata`: default keep (`GraphService.ResolveDisplayKind`, `StateQueryService.cs:2162`, `RunArtifactWriter.cs:516` are all consumers). Verify.
   - `nodes[].source`: default revert (no consumer until E-15). Confirm E-15 starts aligned from this baseline.
7. **Forward-only disposition confirmed.** The tracking doc names every code path that might read the old two-type YAML shape (fixtures, sample bundles, test helpers, any bundle reader) and its disposition: regenerate, delete, or (rarely) document as historical reference. No compatibility reader survives.
8. **No production change.** Diff against `epic/E-24-schema-alignment` touches only this tracking doc, this milestone spec, and (optionally) reference-pointer updates in the epic spec. No edits to `src/`, `tests/`, `docs/schemas/`, or other code-bearing surfaces. Full `.NET` test suite is green throughout (untouched baseline).

## Constraints

- **Decisions are recorded, not deferred.** Every row ends with a named resolution side. "Punt to E-15" is acceptable only when the decision itself is "defer the contract to E-15 and remove any forward-declared schema entry in the meantime" — i.e. a decision, not a question.
- **"Easy option" is named even when rejected.** Every row documents what the reflex fix would have been (most commonly "schema-widen and accept the current emission"), so the reviewer and future readers see what we explicitly chose not to do. Without the rejected option on record, the epic loses the "right, not easy" frame.
- **Unification is the baseline.** Every decision assumes the unified type exists. Rows do not leave open the possibility of preserving two types — Option A has been ratified rejected at the epic level. Rows can still debate where fields live inside the unified type (top-level vs provenance), but not whether to maintain two types.
- **Forward-only is the baseline.** Every decision assumes no compatibility reader. Rows cannot resolve to "keep both shapes during a migration window" — migration windows are out of scope per the epic's constraints.
- **Citation discipline.** Every claim that a field has a consumer includes a `file:line` pointer. Claims that a field has no consumer include a `grep` command so reviewers can reproduce. Claims that a field is load-bearing include a reader line that exercises it.
- **No emoji, no icons, no time estimates.** Plain tables, plain prose.

## Design Notes

- Drafting order:
  1. Read `SimModelArtifact.cs` and `ModelDefinition.cs` side-by-side. List every public member on each.
  2. Build the unified-field inventory as the union, flagging each as "shared" (present on both with matching shape), "Sim-only" (present only on `SimModelArtifact`), or "Engine-only" (present only on `ModelDefinition`).
  3. For every Sim-only field, check Engine consumers with `rg` against `src/FlowTime.Core` and `src/FlowTime.TimeMachine`. No consumer → default decision "drop from emission."
  4. For every Engine-only field, check Sim producers. If Sim never emits, the field remains optional in the unified type (Engine-computed or Engine-default).
  5. Named decisions: unified type home, unified type name, provenance shape.
  6. Satellite type disposition: one row per Sim-side satellite type (`SimNode` / `SimOutput` / `SimProvenance` / `SimTraffic` / `SimArrival` / `SimArrivalPattern`).
  7. Open-question answers: each of the six epic-spec questions gets its row.
  8. Re-examine the three uncommitted m-E23-01 schema edits under the unification framework.
  9. Forward-only disposition: enumerate every consumer of the old two-type YAML shape and name its fate.
  10. Consistency check: walk the inventory and the satellite list; confirm no field is orphaned, duplicated, or contradicted.
- The m-E23-01 tracking doc's histogram (count, shape, category) is the authoritative source for which shapes surfaced in the canary. The inventory here extends those shapes into resolution columns.
- The "easy option" for most Sim-only fields is "declare in the schema as optional pass-through." The "foundationally-right option" under Option E is "drop from emission because no Engine consumer reads it." Use this as the default frame; deviate only when there is a consumer.

## Surfaces touched

- `work/epics/E-24-schema-alignment/m-E24-01-inventory-and-design-decisions-tracking.md` (new, on milestone start)
- (optionally) `work/epics/E-24-schema-alignment/spec.md` — update references or ADR text if m-E24-01 ratifies a candidate ADR

## Out of Scope

- Any edit to `docs/schemas/model.schema.yaml` — that belongs to m-E24-03.
- Any edit to `SimModelArtifact`, `SimModelBuilder`, `ModelDefinition`, `ModelParser`, or emitter / parser logic — that belongs to m-E24-02.
- Any edit to `ModelSchemaValidator` or `TemplateSchemaValidator` — that belongs to m-E24-04.
- Any change to `TemplateWarningSurveyTests.cs` — that belongs to m-E24-05.
- Re-running the survey or the full-shape audit — the m-E23-01 data is the input.
- Writing the unified type. This milestone decides what to write; m-E24-02 writes it.

## Dependencies

- Epic `E-24-schema-alignment` spec approved (Option E framing ratified).
- Epic integration branch `epic/E-24-schema-alignment` exists.
- Access to the m-E23-01 tracking doc (held on branch `milestone/m-E23-01-schema-alignment` as stashed / uncommitted input material).
- Access to the `SimModelArtifact` purpose investigation output (embedded in the epic spec Context section).

## References

- Epic spec: `work/epics/E-24-schema-alignment/spec.md` (Option E framing, the six open questions)
- Investigation: agent `a5aa3dfe26394aff5` — `SimModelArtifact` purpose analysis; findings embedded in epic spec Context
- Survey evidence: `work/epics/E-23-model-validation-consolidation/m-E23-01-schema-alignment-tracking.md` → "AC4 canary re-run, full-shape audit (2026-04-24)"
- Survey agent: `a07d52c12dcaf3538`
- Uncommitted m-E23-01 schema edits: `docs/schemas/model.schema.yaml` at branch `milestone/m-E23-01-schema-alignment` (stashed)
- Decision precedent: D-2026-04-24-035 (E-23 ratification), D-2026-04-24-036 (E-23 pause, E-24 creation, Option E ratified within E-24 planning)
- Truth Discipline guards: `.ai-repo/rules/project.md`
