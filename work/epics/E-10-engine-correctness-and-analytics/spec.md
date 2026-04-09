# Epic: Engine Correctness & Analytical Primitives

**ID:** E-10

## Goal

Fix known correctness bugs, harden engineering quality, and build the analytical primitives layer that enables downstream epics (Path Analysis, Anomaly Detection, Scenario Overlays, UI Analytical Views) to deliver their full value.

## Context

The March 2026 engine deep review (`docs/architecture/reviews/engine-deep-review-2026-03.md`) found:
- 3 P0 correctness bugs (shared series mutation, missing capacity dependency, dispatch-unaware invariant analyzer)
- Engineering debt (duplicate topo-sort, inconsistent NaN policy, PCG modulo bias, no convergence guard)
- Documentation drift (6 undocumented expression functions, overstated constraint claims)
- A missing **analytical layer** — FlowTime can answer "how much flow?" but not "where is the bottleneck?", "what is flow efficiency?", or "what if we set a WIP limit?"

The existing near-term epics (Path Analysis, UI Analytical Views, Anomaly Detection, Scenario Overlays) implicitly depend on these analytical capabilities. Without them, those epics can only work with raw throughput/queue data.

## Scope

### In Scope

**Phase 0 — Correctness (P0 bugs)**
1. Fix BUG-1: Clone outflow series before dispatch mutation
2. Fix BUG-2: Include CapacitySeriesId in ServiceWithBufferNode.Inputs
3. Fix BUG-3: Make InvariantAnalyzer.ValidateQueue dispatch-aware
4. Regression tests for all three bugs
5. End-to-end determinism test (same template + seed = bitwise-identical artifacts)

**Phase 1 — Engineering Foundation**
1. Cache topological order in Graph constructor
2. Precompute Topology adjacency lists
3. Define and enforce NaN/Infinity/div-by-zero policy (all 8 locations)
4. Resolve Series immutability (remove public setter, add Clone())
5. Fix PCG32.NextInt modulo bias
6. Add router convergence guard (max-iteration limit)
7. Add expression function tests (MOD, FLOOR, CEIL, ROUND, STEP, PULSE)
8. Remove #pragma warning disable, Console.WriteLine; type Parallelism properly

**Phase 2 — Documentation Honesty** (parallel with Phase 1)
1. Update expression-language-design.md with all 11 functions
2. Update expression-extensions-roadmap.md (6 candidates → shipped)
3. Downgrade constraint claims to "foundations laid"
4. Clarify time-travel scope as "artifact query"
5. Label catalog architecture doc as aspirational/draft
6. Locate/create model.schema.yaml
7. Standardize JSON Schema meta-version

**Phase 3 — Analytical Primitives**
1. Bottleneck identification: cross-node utilization comparison, WIP accumulation, system constraint flagging
2. Cycle time decomposition: per-ServiceWithBuffer queueTime + processingTime, flow efficiency
3. Analytical projection hardening bridge: move the current state-query analytical capability/computation surface into Core, stabilize snapshot/window outputs, and hand the remaining formula-first purification work to E-16
4. WIP limit modeling: optional wipLimit on ServiceWithBufferNode (block arrivals or divert to loss)
5. Variability preservation: preserve Cv alongside E[X] when compiling PMFs (Kingman's approximation)
6. Wire ConstraintAllocator into evaluation pipeline: declared constraints actually cap served per bin
7. Starvation/blocking detection helpers: flag bins with queue=0 but capacity>0, or arrivals=0 but upstream queue>0

### Out of Scope

- New epics (Path Analysis, Anomaly Detection, etc.) — this epic provides their foundation
- UI changes
- MCP changes (M-10.03 deferred to gaps.md)

## Constraints

- Phase 0 must complete before any other work
- Phase 1 and Phase 2 can run in parallel
- Phase 3 depends on Phase 1 (especially Series immutability fix for WIP limits)
- Remaining Phase 3 expansion resumes after E-16 completion in the order `p3d` -> `p3c` -> `p3b`
- All changes must maintain determinism — the end-to-end determinism test gates everything

## Success Criteria

- [ ] Zero known correctness bugs
- [ ] End-to-end determinism test passes
- [ ] Documentation matches code — no overstated claims
- [ ] FlowTime can answer: "Where is the bottleneck?", "What is flow efficiency?", "What if we set a WIP limit?", "How variable is this stage?"
- [ ] Existing test suite green, no regressions

## Risks & Open Questions

| Risk / Question | Impact | Mitigation |
|----------------|--------|------------|
| Series immutability change may break callers | High | BUG-1 fix provides the test; audit all Series.Values usage |
| WIP limit semantics (block vs divert) need design | Medium | Start with simplest option (divert to loss), iterate |
| ConstraintAllocator may need redesign for eval pipeline | Medium | `p3d` scope accounts for this |

## Milestones

| ID | Title | Status |
|----|-------|--------|
| m-ec-p0 | Phase 0: Correctness bugs + determinism test | complete |
| m-ec-p1 | Phase 1: Engineering foundation | complete |
| m-ec-p2 | Phase 2: Documentation honesty | complete |
| m-ec-p3a | Phase 3a: Cycle time & flow efficiency | complete |
| m-ec-p3a1 | Phase 3a.1: Analytical projection hardening | complete |
| m-ec-p3b | Phase 3b: WIP limits | approved |
| m-ec-p3c | Phase 3c: Variability (Cv + Kingman) | approved |
| m-ec-p3d | Phase 3d: Constraint enforcement | complete |

**Architecture gate:** `m-ec-p3a1` is the bridge milestone that moved the current analytical capability/computation surface into Core. E-16 owned the remaining formula-first purification work exposed by that review, and with E-16 complete Phase 3 resumes with `p3d` -> `p3c` -> `p3b`.

## References

- `docs/architecture/reviews/engine-deep-review-2026-03.md` — Full deep review
- `docs/architecture/reviews/engine-review-findings.md` — Initial review findings
- `docs/architecture/reviews/review-sequenced-plan-2026-03.md` — Sequenced plan (historical rationale)
- `work/gaps.md` — Deferred M-10.03 (depends on runtime constraint enforcement in `p3d`)
