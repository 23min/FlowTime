# Sequenced Plan: Engine Review Findings + Roadmap Reconciliation

> **Date:** 2026-03-23
> **Context:** Reconciles the engine deep review (`engine-deep-review-2026-03.md`) and prior review findings (`engine-review-findings.md`) with the current roadmap (`ROADMAP.md`) and epic roadmap (`epic-roadmap.md`).
> **Purpose:** Provide a single, prioritized sequence that accounts for both correctness fixes and planned feature work. Intended as input for roadmap update and epic scoping in a follow-up session.

---

## How to Read This Document

The review surfaced 22 action items across correctness bugs, engineering quality, and analytical gaps. The existing roadmap already has 10+ planned epics. This document:

1. Maps each review finding to existing roadmap items (or flags it as **new work**)
2. Proposes an optimal sequence that respects dependencies
3. Comments on each existing epic/roadmap item with review-informed perspective

---

## Part 1: Review Findings Mapped to Roadmap

### Already Covered by Roadmap

| Review Finding | Roadmap Item | Comment |
|---|---|---|
| Update expression docs (11 functions) | Immediate: Documentation Accuracy | Exact match. Roadmap item #1. |
| Downgrade constraint claims | Immediate: Documentation Accuracy | Exact match. Roadmap item #1. |
| Clarify time-travel scope | Immediate: Documentation Accuracy | Exact match. Roadmap item #1. |
| Cache topological order | Immediate: Engine Code Fixes | Exact match. Roadmap item #2. |
| Define NaN/Infinity policy | Immediate: Engine Code Fixes | Exact match. Roadmap item #2, though review expanded scope to 8 inconsistencies (vs 4 originally identified). |
| Fix PCG32 modulo bias | Immediate: Engine Code Fixes | Exact match. Roadmap item #2. |
| Add router convergence guard | Immediate: Engine Code Fixes | Exact match. Roadmap item #2. |
| Missing model.schema.yaml | Immediate: Schema Gaps | Exact match. Roadmap item #3. |
| End-to-end determinism test | Immediate: Test Coverage | Exact match. Roadmap item #4. |
| Expression function tests | Immediate: Test Coverage | Exact match. Roadmap item #4. |
| Constraint enforcement | Near-Term: Dependency Constraints | Partially covered. M-10.03 enforces patterns in MCP, but doesn't wire `ConstraintAllocator` into the eval pipeline. The review calls for actual enforcement during `Graph.Evaluate()`. |
| Path-level analysis | Near-Term: Path Analysis & Subgraph Queries | Good match. The review wants bottleneck attribution and path pain; the epic scopes exactly this. |

### NOT Covered by Current Roadmap (New Work)

| Review Finding | Priority | Proposed Placement |
|---|---|---|
| **BUG-1: Shared series mutation** | P0 | Insert before all other work. 1-line fix + test. |
| **BUG-2: Missing capacity dependency in Inputs** | P0 | Same. 2-line fix + test. |
| **BUG-3: InvariantAnalyzer ignores dispatch schedules** | P0 | Same. Moderate fix in ValidateQueue. |
| **Series immutability contradiction** | P1 | Bundle with BUG-1 fix or make standalone engineering task. |
| **Bottleneck identification** | P1 | New analytical capability. Could be a milestone under Path Analysis or Anomaly Detection. |
| **Cycle time decomposition / flow efficiency** | P1 | New analytical capability. Natural extension of LatencyComputer. |
| **WIP limit modeling** | P1 | New analytical capability. Extension to ServiceWithBufferNode. |
| **Variability preservation (Cv from PMFs)** | P2 | New analytical capability. Compile-time change in model compiler. |
| **Precompute Topology adjacency lists** | P2 | Performance fix. Bundle with topo-sort caching. |
| **Starvation/blocking detection** | P3 | New analytical capability. Pairs with anomaly detection. |
| **Aging WIP approximation** | P3 | New analytical capability. Novel approach needed. |

### Comment on "Already Planned" vs "Actually Ready"

The roadmap's "Immediate" section was defined 2+ weeks ago and **none of the items have been executed**. The review confirms every item is still relevant and adds three P0 bugs that should precede all of them. The roadmap's near-term epics are well-scoped but depend on the immediate fixes being complete.

---

## Part 2: Proposed Optimal Sequence

### Phase 0: Correctness (Before Everything Else)

**Rationale:** BUG-1 can produce silently wrong results. BUG-2 is a non-deterministic ordering bug. BUG-3 produces false positive warnings that erode trust. These must be fixed before any feature work or analytical extensions.

| # | Task | Effort | Dependency |
|---|---|---|---|
| 0.1 | Fix BUG-1: Clone outflow series before dispatch mutation | 30 min | None |
| 0.2 | Fix BUG-2: Include CapacitySeriesId in ServiceWithBufferNode.Inputs | 30 min | None |
| 0.3 | Fix BUG-3: Make InvariantAnalyzer.ValidateQueue dispatch-aware | 2-4 hrs | None |
| 0.4 | Add regression tests for all three bugs | 2-4 hrs | 0.1-0.3 |
| 0.5 | Add end-to-end determinism test (same template + seed = bitwise-identical artifacts) | 2-4 hrs | None |

**Deliverable:** All bugs fixed, regression tests passing, determinism guarantee verified.

### Phase 1: Engineering Foundation (Current Roadmap "Immediate")

**Rationale:** These are the existing roadmap items plus the Series immutability fix. They harden the engine for all subsequent work.

| # | Task | Effort | Dependency |
|---|---|---|---|
| 1.1 | Cache topological order in Graph constructor | 2 hrs | None |
| 1.2 | Precompute Topology adjacency lists (node + edge lookup) | 2-3 hrs | None |
| 1.3 | Define and enforce NaN/Infinity policy (all 8 locations) | 4-6 hrs | None |
| 1.4 | Resolve Series immutability: remove public setter, add Clone() | 4-6 hrs | 0.1 (BUG-1 fix) |
| 1.5 | Fix PCG32.NextInt modulo bias | 1 hr | None |
| 1.6 | Add router convergence guard (max-iteration limit) | 1-2 hrs | None |
| 1.7 | Add expression function tests (MOD, FLOOR, CEIL, ROUND, STEP, PULSE) | 3-4 hrs | None |
| 1.8 | Remove #pragma warning disable, Console.WriteLine, type Parallelism properly | 2-3 hrs | None |

**Deliverable:** Clean, correct, performant engine core. All known code smells resolved.

### Phase 2: Documentation Honesty

**Rationale:** Can run in parallel with Phase 1. Restores trust between docs and code.

| # | Task | Effort | Dependency |
|---|---|---|---|
| 2.1 | Update expression-language-design.md with all 11 functions | 1 hr | None |
| 2.2 | Update expression-extensions-roadmap.md (move 6 from candidate to shipped) | 30 min | None |
| 2.3 | Downgrade constraint claims to "foundations laid" | 30 min | None |
| 2.4 | Clarify time-travel scope as "artifact query" | 30 min | None |
| 2.5 | Label catalog architecture doc as aspirational/draft | 15 min | None |
| 2.6 | Locate/create model.schema.yaml | 1-2 hrs | None |
| 2.7 | Standardize JSON Schema meta-version (draft-07 vs 2020-12) | 1 hr | None |
| 2.8 | Update ROADMAP.md to reference this plan and the deep review | 30 min | None |

**Deliverable:** Documentation matches code. No overstated claims.

### Phase 3: Analytical Primitives (NEW — Not in Current Roadmap)

**Rationale:** The deep review identified that FlowTime operates at the volume/throughput layer but lacks the analytical layer. These primitives are the foundation for Path Analysis, Anomaly Detection, and Scenario Overlays. Without them, those epics cannot deliver their full value. This phase should be inserted between the current "Immediate" work and the "Near-Term" epics.

| # | Task | Effort | Dependency |
|---|---|---|---|
| 3.1 | **Bottleneck identification**: Cross-node utilization comparison, WIP accumulation pattern detection, system constraint flagging | 2-3 days | Phase 1 complete |
| 3.2 | **Cycle time decomposition**: Per-ServiceWithBuffer queueTime + processingTime. Flow efficiency = processingTime / (queueTime + processingTime) | 1-2 days | Phase 1 complete |
| 3.3 | **WIP limit modeling**: Optional wipLimit on ServiceWithBufferNode. When reached: block arrivals or divert to loss. Enables Kanban what-if analysis | 2-3 days | 1.4 (Series fix) |
| 3.4 | **Variability preservation**: Preserve Cv alongside E[X] when compiling PMFs. Enable Kingman's approximation for wait time prediction | 1-2 days | None |
| 3.5 | **Wire ConstraintAllocator into evaluation pipeline**: Declared constraints actually cap served per bin | 2-3 days | Phase 1 complete |
| 3.6 | **Starvation/blocking detection helpers**: Flag bins where queue=0 but capacity>0 (starvation) or arrivals=0 but upstream queue>0 (blocking) | 1-2 days | None |

**Deliverable:** FlowTime can answer: "Where is the bottleneck?", "What is flow efficiency?", "What if we set a WIP limit?", "How variable is this stage?"

### Phase 4: Near-Term Epics (Current Roadmap, Resequenced)

With analytical primitives in place, the near-term epics become more valuable:

| # | Epic | Roadmap Status | Review Comment |
|---|---|---|---|
| 4.1 | **Dependency Constraints follow-up (M-10.03)** | In-flight | Now also includes wiring ConstraintAllocator (from 3.5). MCP pattern enforcement is good but insufficient without runtime enforcement. |
| 4.2 | **Path Analysis & Subgraph Queries** | Planned | Now benefits from bottleneck identification (3.1) and cycle time decomposition (3.2). Can deliver bottleneck attribution, path pain, and dominant route analysis with the analytical primitives as inputs. |
| 4.3 | **Visualizations (Chart Gallery / Demo Lab)** | Planned | Now has richer data to visualize: cycle time distributions, flow efficiency, bottleneck heat maps, WIP limit impact. Without Phase 3, this epic would only visualize throughput/queue charts. |
| 4.4 | **Telemetry Ingestion + Canonical Bundles** | Planned | Independent of Phase 3. Can proceed in parallel. |

### Phase 5: Mid-Term Epics (Current Roadmap, Unchanged Sequence)

| # | Epic | Review Comment |
|---|---|---|
| 5.1 | **Telemetry Loop & Parity** | Depends on 4.4 (Telemetry Ingestion). No change. |
| 5.2 | **Scenario Overlays & What-If** | Now dramatically more powerful with WIP limits (3.3) and variability (3.4). What-if scenarios can test "what if we add a WIP limit of 5?" or "what if Cv doubles?" |
| 5.3 | **Anomaly & Pathology Detection** | Now has starvation/blocking detection (3.6) and bottleneck identification (3.1) as building blocks. Phase 1 of the anomaly epic can leverage these rather than building from scratch. |
| 5.4 | **Ptolemy-Inspired Semantics** | No change. Conceptual guardrails. |
| 5.5 | **Streaming & Subsystems** | No change. Long-term exploratory. |

---

## Part 3: Commentary on Existing Epics

### Completed Epics — All Verified Sound

The deep review confirmed that the following delivered epics are architecturally sound:
- **Time Travel V1**: `/state` and `/state_window` work correctly. Scope should be clarified (artifact query, not temporal debugging) but the implementation is solid.
- **Edge Time Bins**: Per-edge flow data is well-designed. Conservation checks are a standout feature. This is the foundation for Path Analysis.
- **Evaluation Integrity**: DAG evaluation contract is correct (modulo the three bugs). The compile-to-DAG pipeline is clean.
- **MCP Modeling & Analysis**: Well-scoped. The draft/validate/run/inspect loop is well designed.
- **Engine Semantics Layer**: Stable contracts. Good separation of concerns.
- **Classes & Routing**: Multi-class support works. Router flow has limited test coverage but the architecture is sound.

### Near-Term Epics — Commentary

**Dependency Constraints (M-10.03)**
The roadmap scopes this as "pattern enforcement in MCP." The review reveals a deeper problem: `ConstraintAllocator` has zero callers in the evaluation pipeline. Constraints are declared in models but silently ignored at runtime. M-10.03 should be expanded to include runtime enforcement, not just MCP guidance. Without this, the feature is essentially non-functional.

**Visualizations (Chart Gallery)**
Good idea, but timing matters. If this ships before Phase 3 (analytical primitives), the charts can only show throughput/queue depth — the same data already visible in the topology view. With Phase 3, the gallery can show cycle time distributions, flow efficiency trends, bottleneck heat maps, and WIP limit impact curves. **Recommendation:** Start scoping the gallery now, but prioritize Phase 3 so the gallery has rich data to visualize.

**Telemetry Ingestion**
Independent of the review findings. Can proceed on its own timeline. The one connection: ingested telemetry should preserve variability information (Cv) if Phase 3.4 is implemented, so the ingestion format should be designed with this in mind.

**Path Analysis**
This epic is well-scoped in `docs/architecture/path-analysis/README.md` and `docs/architecture/gaps.md`. The review adds that bottleneck attribution (which path contains the bottleneck?) and cycle time decomposition per stage are natural outputs. With Phase 3.1 (bottleneck ID) and 3.2 (cycle time decomposition), Path Analysis becomes significantly more valuable.

### Mid-Term Epics — Commentary

**Scenario Overlays**
The review's WIP limit modeling (3.3) and variability preservation (3.4) are force multipliers for this epic. A "what-if" scenario that can only change arrival rates and parallelism is useful. One that can also test WIP limits, capacity constraints, and variability changes is transformative. **Recommendation:** Ensure Phase 3 ships before Scenario Overlays.

**Anomaly & Pathology Detection**
The four-phase design in `docs/architecture/anomaly-detection/README.md` is solid. The review adds that Phase 1 (anomaly detection) should leverage the starvation/blocking detection helpers (3.6) and bottleneck identification (3.1) as building blocks rather than reimplementing from scratch. The pathology classifier (Phase 2) can use cycle time decomposition (3.2) to identify slow-drain patterns (growing queue time with stable processing time).

**UI Layout Motors**
This epic is directly relevant to the SVG topology proposal (see separate document). The pluggable layout engine contract (`LayoutInput -> LayoutResult`) is exactly the abstraction needed to swap between Canvas and SVG renderers. **Recommendation:** If the SVG epic proceeds, it should implement the Layout Motors contract as its first milestone.

---

## Part 4: Dependency Graph

```
Phase 0 (Correctness)
  |
  v
Phase 1 (Engineering)  <-->  Phase 2 (Docs) [parallel]
  |
  v
Phase 3 (Analytical Primitives)
  |
  +---> Phase 4.1 (Constraints)    -- depends on 3.5
  +---> Phase 4.2 (Path Analysis)  -- depends on 3.1, 3.2
  +---> Phase 4.3 (Visualizations) -- depends on 3.1, 3.2, 3.3
  +---> Phase 4.4 (Telemetry)      -- independent
  |
  v
Phase 5.1 (Telemetry Parity) -- depends on 4.4
Phase 5.2 (Scenario Overlays) -- depends on 3.3, 3.4
Phase 5.3 (Anomaly Detection) -- depends on 3.1, 3.6
Phase 5.4 (Ptolemy)           -- independent
Phase 5.5 (Streaming)         -- independent
```

---

## Part 5: Effort Estimates Summary

| Phase | Effort | Calendar (1 developer) |
|---|---|---|
| Phase 0: Correctness | 1-2 days | Week 1 |
| Phase 1: Engineering | 3-4 days | Week 1-2 |
| Phase 2: Documentation | 1 day | Week 1 (parallel) |
| Phase 3: Analytical Primitives | 2-3 weeks | Week 3-5 |
| Phase 4: Near-Term Epics | 4-8 weeks | Week 6-13 |
| Phase 5: Mid-Term Epics | 8-16 weeks | Week 14+ |

**Total to analytical completeness (Phase 0-3):** ~4-5 weeks.
**Total to near-term epic completion (Phase 0-4):** ~13 weeks.

---

## Part 6: What This Plan Changes About the Roadmap

1. **Inserts Phase 0** (bug fixes) before everything. The current roadmap has no awareness of BUG-1/2/3.
2. **Inserts Phase 3** (analytical primitives) between "Immediate" and "Near-Term." The current roadmap jumps from code hygiene directly to feature epics, missing the analytical layer that makes those epics truly valuable.
3. **Expands M-10.03** (Dependency Constraints) to include runtime enforcement, not just MCP pattern guidance.
4. **Resequences Visualizations** to depend on analytical primitives — otherwise it ships charts that can only show what the topology already shows.
5. **Connects Phase 3 to Phase 5** — Scenario Overlays and Anomaly Detection become dramatically more powerful with analytical primitives as building blocks.

The existing roadmap structure and epic organization are sound. This plan doesn't change the architecture — it fills the analytical gap between the engine's current volume-layer capabilities and the analytical-layer capabilities that downstream epics implicitly require.
