# Epic: Formula-First Core Purification

**ID:** E-16
**Status:** Approved

## Goal

Purify FlowTime's execution boundary so semantic meaning and analytical truth are compiled into Core once and consumed as facts everywhere else. This epic turns the existing "spreadsheet for flows" mental model into an enforceable architecture: parser/compiler resolve references, the core evaluates pure vector formulas, and adapters and clients stop reconstructing domain meaning from strings.

## Context

E-10 established the right direction for engine correctness and analytical primitives, and `m-ec-p3a1` correctly identified that duplicated analytical logic in `StateQueryService` was a design problem rather than just a missing helper. We are now wrapping `m-ec-p3a1` as a bridge milestone: it moved the current analytical capability/computation surface into Core, but the review and plan pressure test confirmed that the full purification work is larger and should be owned entirely by E-16.

The deeper structural issue remains:

- runtime analytical identity is still inferred late from raw semantic strings
- `StateQueryService` still carries parser-style context so it can reconstruct meaning
- API contracts still expose hints (`kind`, `nodeLogicalType`) instead of authoritative analytical facts
- UI/client code still re-implements queue/service classification and node category heuristics

That means the current architecture is still vulnerable to the same class of drift that p3a1 was meant to fix. If we continue directly into the remaining E-10 Phase 3 work on top of that boundary, we will multiply the cleanup cost and likely accumulate more duct tape.

This work should therefore be treated as a dedicated epic, not hidden inside another correctness milestone. The point is not a rewrite. The point is to move semantic truth earlier into the compile/evaluate pipeline, make deletion of the current heuristics an explicit deliverable, and do it in a forward-only cut rather than layering on compatibility shims.

## Scope

### In Scope

- Typed semantic references in the compiled runtime model
- A compiled analytical descriptor on runtime nodes
- A pure Core analytical evaluator that owns emitted derived metrics and warning facts
- Purification of `/state`, `/state_window`, and `/graph` contracts so clients can consume authoritative analytical and categorical facts
- Removal of analytical identity reconstruction from `StateQueryService`
- Separation of real by-class truth from wildcard fallback projection
- Deletion audits and review gates that prevent reintroduction of heuristics

### Out of Scope

- New analytical primitives such as WIP limits, variability, or constraint enforcement themselves
- SIM/orchestration extraction and run packaging boundary work
- A rewrite of the DAG evaluator or expression language foundation
- Broad UI redesign unrelated to analytical truth and contract purity
- Changes to deterministic artifact semantics unless explicitly versioned

## Constraints

- No rewrite-from-scratch. This must be a strangler refactor around the existing deterministic DAG/compiler foundation.
- Forward-only migration. Existing runs, generated fixtures, and approved snapshots that depend on the old analytical/runtime boundary may be deleted and regenerated; no backward-compatibility shim is required.
- No new runtime behavior may depend on reparsing raw semantic strings after compile.
- Each milestone must delete an old heuristic path; do not preserve old inference paths once the replacement exists.
- Contract changes may remove or replace old hint fields in the same forward-only milestone when the named consumers for that milestone are migrated and tested.

## Success Criteria

- [ ] Runtime nodes carry compiled semantic references and an authoritative analytical descriptor.
- [ ] `StateQueryService` no longer parses raw semantic references or reconstructs analytical identity for runtime behavior.
- [ ] Core owns analytical evaluation, emitted derived keys, and warning eligibility facts for snapshot, window, and by-class outputs.
- [ ] API contracts publish authoritative analytical and node-category facts across current state and graph surfaces so first-party consumers stop classifying behavior from `kind + logicalType`.
- [ ] Fallback wildcard class data is explicit and distinguishable from real by-class truth.
- [ ] Remaining E-10 Phase 3 milestones can build on compiled facts instead of adapter heuristics.
- [ ] End-to-end pipeline validation proves Sim → Compiler → Runtime → API → Consumer works correctly: Sim-produced YAML (unchanged authoring surface) compiles through the new typed-reference compiler, evaluates correctly, and projects through purified contracts to consumers.

## End-to-End Validation Strategy

E-16 changes Core/Compiler/API but not Sim's authoring surface. The full pipeline must be validated end-to-end even though Sim code is not modified:

- **Sim → Compiler boundary:** Sim-produced YAML with raw string references compiles correctly through the new typed-reference compiler. At minimum, run all existing Sim templates through the new compiler and verify no regressions.
- **Compiler → Runtime boundary:** Compiled typed references produce the same evaluated series as the old raw-string path. Parity tests per milestone.
- **Runtime → API boundary:** Purified state projection (snapshot, window, by-class) produces the same analytical outputs. End-to-end API tests with `WebApplicationFactory<Program>`.
- **Runtime → Graph/API boundary:** Graph projection and current-state projection both consume compiled facts instead of re-deriving category or analytical identity from strings.
- **API → Consumer boundary:** First-party Blazor/JS consumers read from the new fact surface and produce the same behavior as the old `kind + logicalType` heuristic path.
- **Model / template boundary:** Typed parallelism and reference cleanup must validate through model DTOs, template substitution, and graph projection surfaces; E-16 is not treated as a Core/API-only refactor in implementation planning.
- **Integration test suite:** `tests/FlowTime.Integration.Tests` should include at least one scenario that exercises the full Sim-template → engine-run → state-query → contract-assertion path to guard against boundary drift.

Each milestone is individually shippable, but the final milestone (m-E16-06) must include a cross-cutting integration pass before declaring the epic complete.

## Risks & Open Questions

| Risk / Question | Impact | Mitigation |
|----------------|--------|------------|
| Compiler/runtime model refactor touches many files across Core and API | High | Sequence compiler-first slices, keep each milestone shippable, and use deletion lists to constrain scope |
| Contract purification may ripple into UI/client code, graph consumers, and golden tests | High | Keep the first-party consumer scope explicit and migrate all current state + topology consumers in the contract milestone |
| Existing templates and fixtures rely on loose reference shapes | Medium | Regenerate runs, fixtures, and approved snapshots forward-only rather than carrying compatibility fallbacks |
| Should the public contract expose the analytical descriptor directly or a smaller fact surface? | Medium | Decide in the contract milestone and ship one forward-only fact surface for the named current-state consumers |
| How much warning policy belongs in the analytical evaluator vs a separate analyzer package? | Medium | Resolve explicitly during the evaluator milestone and document the ownership boundary |

## Milestones

**Sequencing note:** `m-E16-01` through `m-E16-05` are the architecture gate between wrapped `m-ec-p3a1` and the rest of E-10 Phase 3. `m-E16-01` introduces typed references and parallelism typing; `m-E16-03` is the descriptor-driven deletion point for analytical-identity heuristics; `m-E16-06` publishes the final contract/consumer cut across state and graph surfaces.

| ID | Title | Summary | Depends On | Status |
|----|-------|---------|------------|--------|
| [m-E16-01-compiled-semantic-references](../../milestones/m-E16-01-compiled-semantic-references.md) | Compiled Semantic References | Replace raw runtime semantic strings with typed references and regenerate dependent runs/fixtures forward-only. | none | draft |
| [m-E16-02-class-truth-boundary](../../milestones/m-E16-02-class-truth-boundary.md) | Class Truth Boundary | Separate real by-class truth from wildcard fallback before descriptor and evaluator work depend on it. | m-E16-01 | draft |
| [m-E16-03-runtime-analytical-descriptor](../../milestones/m-E16-03-runtime-analytical-descriptor.md) | Runtime Analytical Descriptor | Compile authoritative analytical identity onto runtime nodes and delete adapter-side logical-type reconstruction. | m-E16-02 | draft |
| [m-E16-04-core-analytical-evaluation](../../milestones/m-E16-04-core-analytical-evaluation.md) | Core Analytical Evaluation | Move analytical values and emitted-series truth into a pure Core evaluator for snapshot, window, and by-class outputs. | m-E16-03 | draft |
| [m-E16-05-analytical-warning-facts-and-primitive-cleanup](../../milestones/m-E16-05-analytical-warning-facts-and-primitive-cleanup.md) | Analytical Warning Facts & Primitive Cleanup | Move warning facts into Core analyzers and finish analytical primitive ownership cleanup. | m-E16-04 | draft |
| [m-E16-06-analytical-contract-and-consumer-purification](../../milestones/m-E16-06-analytical-contract-and-consumer-purification.md) | Analytical Contract & Consumer Purification | Publish authoritative analytical facts and delete named current-state consumer heuristics in one forward-only cut. | m-E16-05 | draft |

**Forward-only rule:** old run directories, generated fixtures, and approved golden snapshots are not compatibility obligations for this epic. When the runtime boundary changes, regenerate them.

## Why This Is a Separate Epic

E-10 is still the right umbrella for correctness and analytical primitives, but it did not explicitly own the deeper architectural purification that p3a1 exposed. Treating this as a separate epic does three useful things:

1. It gives boundary cleanup explicit success criteria rather than burying it in feature work.
2. It makes deletion of heuristics and duplicate policy a first-class deliverable.
3. It prevents the remaining Phase 3 work (`p3d` -> `p3c` -> `p3b`) from normalizing an impure adapter/client boundary.

In short: E-10 found the wound, `m-ec-p3a1` stabilized the current bridge, and E-16 now owns closing it properly.

## References

- [reference/formula-first-engine-refactor-plan.md](reference/formula-first-engine-refactor-plan.md)
- [docs/concepts/nodes-and-expressions.md](../../../docs/concepts/nodes-and-expressions.md)
- [docs/architecture/expression-language-design.md](../../../docs/architecture/expression-language-design.md)
- [work/epics/E-10-engine-correctness-and-analytics/m-ec-p3a1-analytical-projection-hardening.md](../E-10-engine-correctness-and-analytics/m-ec-p3a1-analytical-projection-hardening.md)
- [work/epics/E-10-engine-correctness-and-analytics/spec.md](../E-10-engine-correctness-and-analytics/spec.md)
