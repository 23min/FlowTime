# FlowTime Engine: Sequenced Improvement Plan

**Author**: Independent senior review (March 2026)
**Purpose**: Provide an optimally sequenced plan that addresses correctness bugs, analytical gaps, and roadmap blind spots identified in the engine deep review. This document is designed to be reconciled with `ROADMAP.md` and `work/epics/epic-roadmap.md` in a follow-up session.
**Inputs**: `engine-review-findings.md` (2026-03-07), `engine-deep-review-2026-03.md` (2026-03-23), current roadmap, epic-roadmap, all milestone specs, engine charter, whitepaper, flow-theory-foundations reference.

---

## Executive Summary

The current roadmap is well-structured but has three problems:

1. **Bugs before features.** Three confirmed correctness bugs (shared series mutation, missing dependency in topo sort, false-positive invariant warnings) have no fix on any roadmap, epic, or milestone. They can produce silently wrong results.

2. **Infrastructure before analytics.** The near-term roadmap sequences dependency constraints, visualizations, and telemetry ingestion before any analytical primitives. This means FlowTime will get better data pipelines but still cannot answer "where is the bottleneck?" or "what is the p85 cycle time?"

3. **Five analytical gaps have no home.** WIP limits, variability decomposition, cycle time distributions, flow efficiency, and aging WIP are absent from all epics and milestones. These are not nice-to-haves; they are foundational flow analysis concepts.

This plan proposes **6 waves** that interleave bug fixes, analytical primitives, and the existing roadmap in an order that maximizes value at each step.

---

## Guiding Principles for Sequencing

1. **Correctness before features.** A wrong answer is worse than no answer. Fix bugs first.
2. **Analytical value early.** Users need insight, not just data. Add analysis primitives before building more infrastructure.
3. **Each wave is independently shippable.** Every wave produces a testable, valuable increment. No wave depends on a later wave.
4. **Respect the engine's identity.** FlowTime is flow-first, deterministic, grid-based. All additions must preserve the spreadsheet/DAG metaphor. No DES, no per-entity tracking, no non-deterministic analysis.
5. **Existing epics are not thrown away.** The planned epics (Path Analysis, Anomaly Detection, Overlays) are well-designed. This plan slots them in at the right time and fills the gaps around them.

---

## Wave 0: Correctness & Hygiene (1-2 weeks)

**Goal**: Fix all confirmed bugs and the immediate code quality issues identified in both reviews. This is prerequisite to everything else.

**Rationale**: BUG-1 (shared series mutation) can produce silently wrong results for any model with dispatch schedules where another node shares the same outflow. BUG-2 (missing topo dependency) is a non-deterministic ordering bug. These undermine the charter's headline claim of deterministic evaluation. Nothing should ship on top of a broken foundation.

### Items

| # | Item | Type | Effort | Source |
|---|------|------|--------|--------|
| W0.1 | **Fix BUG-1**: Clone outflow series before `DispatchScheduleProcessor.ApplySchedule` in `ServiceWithBufferNode.Evaluate()` | Bug fix | S | Deep review |
| W0.2 | **Fix BUG-2**: Include `CapacitySeriesId` in `ServiceWithBufferNode.Inputs` when dispatch schedule is present | Bug fix | S | Deep review |
| W0.3 | **Fix BUG-3**: Make `InvariantAnalyzer.ValidateQueue` dispatch-schedule-aware (apply same zeroing logic before comparison) | Bug fix | M | Deep review |
| W0.4 | **Make Series immutable or add explicit Clone discipline**: Either remove the public setter and use a builder pattern, or add `Series.Clone()` and enforce clone-before-mutate. The setter is the root cause of BUG-1. | Design fix | M | Deep review |
| W0.5 | **Cache topological order in Graph constructor**: Compute once in `ValidateAcyclic()`, store result, reuse in `Evaluate()`. Remove duplicate `TopologicalOrder()`. Build adjacency index. | Perf fix | S | Both reviews |
| W0.6 | **Define and enforce NaN/Infinity policy**: Choose one policy (recommended: NaN propagates, division-by-zero yields NaN, `Safe()` emits warning). Apply consistently across ExprNode, ServiceWithBufferNode, BinaryOpNode, LatencyComputer, UtilizationComputer, ClassMetricsAggregator. | Policy + code | M | Both reviews |
| W0.7 | **Add router convergence guard**: Max-iteration limit with descriptive error in `RouterAwareGraphEvaluator`. | Safety | S | Both reviews |
| W0.8 | **Fix PCG32.NextInt modulo bias**: Use rejection sampling or mark as internal. | Fix | S | Both reviews |
| W0.9 | **Add end-to-end determinism integration test**: Same template + same seed = bitwise-identical artifacts. | Test | S | Both reviews |
| W0.10 | **Add expression function tests**: Dedicated tests for MOD, FLOOR, CEIL, ROUND, STEP, PULSE including edge cases. | Test | M | Both reviews |

### Commentary on Existing Roadmap

The ROADMAP.md "Immediate" section already acknowledges items W0.5, W0.6, W0.7, W0.8, W0.9, W0.10 but frames them as "before new feature work." The problem is that no milestone was created, no owner assigned, and 16 days later nothing was done. This plan makes them explicit deliverables.

Items W0.1-W0.4 (the bugs and Series immutability) are **not on any roadmap, epic, or milestone**. They are the most important items in this entire plan.

### Done Criteria

- All 3 bugs have regression tests and are fixed
- Determinism integration test passes
- NaN policy is documented and enforced in all node types
- Series either has no public setter or has explicit Clone semantics

---

## Wave 1: Documentation Truth (1 week)

**Goal**: Bring documentation in sync with code. Eliminate all "documentation says X, code does Y" discrepancies.

**Rationale**: Documentation drift erodes trust. Users (including AI agents using MCP) rely on docs to understand what the engine can and cannot do. When docs claim features that don't work (constraints) or omit features that do work (6 expression functions), the platform becomes unreliable as a source of truth.

### Items

| # | Item | Effort | Source |
|---|------|--------|--------|
| W1.1 | Update `expression-language-design.md` with all 11 shipped functions | S | Both reviews |
| W1.2 | Move MOD/FLOOR/CEIL/ROUND/STEP/PULSE from "candidate" to "shipped" in `expression-extensions-roadmap.md` | S | Both reviews |
| W1.3 | Downgrade dependency constraint claims from "shipped" to "foundations laid, enforcement pending" | S | Both reviews |
| W1.4 | Clarify time-travel scope: "run-artifact state queries, not temporal debugging or mid-run replay" | S | Both reviews |
| W1.5 | Label catalog architecture doc as aspirational/draft | S | March 7 review |
| W1.6 | Standardize JSON Schema meta-version (draft-07 vs 2020-12) | S | March 7 review |
| W1.7 | Locate or create missing `model.schema.yaml` | S | March 7 review |
| W1.8 | Merge `engine-deep-review-2026-03.md` from worktree to main docs, update ROADMAP.md references to include it | S | This plan |
| W1.9 | Update engine charter "Current State" section to reflect actual state (including known limitations) | S | This plan |

### Commentary

These are all small tasks individually. They could be done in a single documentation sprint. The key is doing them *before* starting new feature work, so the docs accurately represent the starting point.

---

## Wave 2: Analytical Primitives (2-3 weeks)

**Goal**: Add the missing analytical computations that transform FlowTime from a "flow volume engine" into a "flow analysis engine." These are all derived computations on existing series — no new node types, no new data model, no new APIs required.

**Rationale**: This is the biggest gap identified in the review. The engine computes arrivals, served, queue depth, utilization, and point-estimate latency. But it cannot answer the five most important questions in flow analysis:

1. "Where is the bottleneck?" (requires cross-node comparison)
2. "What is the p85 cycle time?" (requires distribution estimation)
3. "How efficient is the flow?" (requires queue-time vs processing-time decomposition)
4. "What would happen with a WIP limit?" (requires back-pressure modeling)
5. "How much does variability contribute to wait time?" (requires Cv preservation)

All five are achievable within the current architecture.

### Items

| # | Item | Importance | Effort | Notes |
|---|------|-----------|--------|-------|
| W2.1 | **Bottleneck identification** | Critical | M | Compare utilization across all nodes per bin. Flag the node with highest utilization as the system constraint. Detect WIP accumulation patterns (growing queue upstream of high-utilization node). Emit as a derived series and/or analysis annotation on the run. This directly enables Theory of Constraints step 1. |
| W2.2 | **Cycle time decomposition** | Critical | M | For each ServiceWithBuffer node, compute: `queueTime = latency` (already exists), `processingTime = processingTimeMsSum / servedCount` (data exists). Report both as separate series. This is a small computation but a large analytical unlock. |
| W2.3 | **Flow efficiency computation** | High | S | `flowEfficiency[t] = processingTime[t] / (queueTime[t] + processingTime[t])`. Derived from W2.2 outputs. This is the core Flow Framework metric that FlowTime currently cannot produce. |
| W2.4 | **WIP limit modeling on ServiceWithBufferNode** | High | L | Add an optional `wipLimit` field to ServiceWithBufferNode. When queue depth reaches the limit, one of: (a) arrivals overflow to loss (lossy/bounded-buffer), (b) arrivals are blocked and create upstream back-pressure (pull-based). Start with option (a) as it preserves the DAG evaluation model — blocked arrivals don't require upstream modification. Option (b) requires multi-pass evaluation (similar to router convergence) and should be a follow-up. |
| W2.5 | **Variability preservation from PMFs** | High | M | Currently, PMFs are reduced to expected values at compile time. Instead, also preserve and propagate the coefficient of variation (Cv = sigma/mu) alongside the mean. This enables Kingman's approximation (`Wq approx (rho / (1-rho)) * ((Ca^2 + Cs^2) / 2) * meanServiceTime`) without requiring full Monte Carlo. Cv can be stored as a metadata series on each node. |
| W2.6 | **Starvation and blocking detection** | Medium | S | Per-bin flags: starvation when `queue[t] == 0 AND capacity[t] > 0 AND arrivals[t] == 0`; blocking when `arrivals[t] == 0 AND upstream queue > 0`. Emit as analysis annotations. |
| W2.7 | **Little's Law assumption validation** | Medium | S | Per window, check: (a) is WIP trending? (b) is throughput stable? (c) is the measurement window sufficient? Emit warnings when Little's Law assumptions are violated, so latency estimates are flagged as unreliable when they are. |

### Design Considerations

**Where do these live?** Following the engine charter principle "semantics are server-side," all of these should be computed in the engine (not UI, not MCP). They should be available via `/state` and `/state_window` like any other derived series.

**How do they interact with the existing architecture?**
- W2.1-W2.3, W2.6-W2.7 are **post-evaluation analysis** (like InvariantAnalyzer). They run after the DAG evaluates and annotate the results. No changes to the evaluation pipeline.
- W2.4 (WIP limits) changes `ServiceWithBufferNode` evaluation. The lossy variant is straightforward: `if queue > wipLimit, overflow = queue - wipLimit; loss += overflow; queue = wipLimit`. The pull-based variant is more complex and should be a separate milestone.
- W2.5 (variability) changes the model compiler to preserve Cv from PMFs and adds a metadata series. No evaluation pipeline changes.

**Effort estimate**: M = 3-5 days, L = 1-2 weeks, S = 1-2 days.

### Commentary on Existing Roadmap

**None of these items exist on any roadmap, epic, or milestone.** The Path Analysis epic (planned, no milestones) includes bottleneck attribution and end-to-end latency, but only at the *path* level, and only as part of a larger epic that also includes path queries, subgraph responses, and UI integration. The analytical primitives in Wave 2 are simpler, more foundational, and should ship before Path Analysis — they provide the per-node building blocks that Path Analysis will aggregate across paths.

**Relationship to Path Analysis**: Wave 2 is complementary, not competitive. Path Analysis needs per-node bottleneck scores, per-node cycle time decomposition, and per-node flow efficiency to produce meaningful path-level metrics. Doing Wave 2 first makes Path Analysis easier and better.

### Done Criteria

- Bottleneck identification produces a per-bin "constraint node" annotation
- Cycle time decomposition available as queueTime + processingTime series per ServiceWithBuffer node
- Flow efficiency computable from decomposed cycle time
- WIP limit (lossy mode) works on ServiceWithBuffer nodes with tests
- Cv is preserved from PMFs and available as metadata
- Starvation/blocking flags emitted as analysis annotations
- Little's Law assumption checks emit warnings when violated

---

## Wave 3: Existing Roadmap — Near-Term (continue as planned)

**Goal**: Execute the existing near-term roadmap items, now on a solid foundation.

**Rationale**: The existing near-term epics are sound. Dependency constraints, visualizations, and telemetry ingestion are all valuable. The difference is that they now build on a bug-free engine with analytical primitives already available.

### Items (from current ROADMAP.md)

| # | Item | Status | Commentary |
|---|------|--------|-----------|
| W3.1 | **M-10.03: MCP Dependency Pattern Enforcement** | Draft milestone exists | Proceed as planned. **However**, also wire `ConstraintAllocator` into the evaluation pipeline for Option B enforcement. M-10.03 currently only enforces patterns in MCP; the engine itself still silently ignores declared constraints. This is the long-standing gap noted in both reviews. |
| W3.2 | **Visualizations (Chart Gallery / Demo Lab)** | Epic defined, no milestones | Now more valuable because Wave 2 adds bottleneck identification, cycle time decomposition, and flow efficiency — all of which need visualization. The chart gallery should include: CFD-style charts, bottleneck heat maps, cycle time distribution histograms, flow efficiency trends, and WIP limit impact charts. |
| W3.3 | **Telemetry Ingestion + Canonical Bundles** | Epic defined, no milestones | Proceed as planned. No changes from the review. |

### Commentary on Existing Roadmap

The current roadmap puts these three items in sequence. That's fine, but **W3.1 needs a scope expansion**: add engine-side constraint enforcement, not just MCP-side pattern enforcement. The ConstraintAllocator is 67 lines of correct code with zero callers. Wiring it in is a small task with high trust impact.

### Done Criteria

- ConstraintAllocator called during evaluation for Option B constraints
- Chart gallery includes analytical primitive visualizations
- Telemetry ingestion produces canonical bundles

---

## Wave 4: Path Analysis + CFD Computation (from existing epic)

**Goal**: Implement the Path Analysis epic, enriched by the analytical primitives from Wave 2.

**Rationale**: Path Analysis is the highest-value analytical epic on the roadmap. With Wave 2's per-node primitives already in place, path-level analysis becomes a natural aggregation layer rather than a ground-up build.

### Items (from existing Path Analysis epic)

| # | Item | Notes |
|---|------|-------|
| W4.1 | **Path query contract + subgraph responses** | As designed in the epic README. |
| W4.2 | **Volume split + bottleneck attribution (v1)** | Now uses W2.1's per-node bottleneck scores as input. `pathBottleneck = argmax(nodeBottleneckScore)` along the path. |
| W4.3 | **Path pain + latency estimate (v1)** | Now uses W2.2's cycle time decomposition. `pathLatency = sum(queueTime + processingTime)` along the path, not just `sum(queue/served * binMinutes)`. |
| W4.4 | **CFD computation helper** | New item (not in the existing epic). Given a `/state_window`, compute CFD band widths, slopes, and approximate cycle times from horizontal distance between cumulative arrival and departure curves. This is a key visualization that the engine should support natively. |
| W4.5 | **UI + MCP integration** | As designed in the epic README. |

### Commentary on Existing Epic

The existing Path Analysis epic README is well-designed. The main improvement is that Wave 2 provides the per-node building blocks, making the path-level computations more accurate and decomposable. The addition of W4.4 (CFD computation) fills a gap noted in the deep review: the engine has the data for CFDs but no explicit computation.

**Open questions from the epic** (section 11) should be resolved before milestone creation:
- Path volume: recommend min-cut for explicit paths, entry-edge for policy-based paths.
- Missing edge data: emit warnings (consistent with the "make wrongness visible" principle).
- Storage: derive on demand for v1; persist if performance requires it.

### Done Criteria

- Path queries answerable via dedicated analysis endpoint
- Bottleneck attribution uses per-node scores from Wave 2
- CFD computation available as a server-side derived analysis
- MCP can query path metrics authoritatively

---

## Wave 5: Scenario Overlays + Anomaly Detection (from existing epics)

**Goal**: Implement the two aspirational epics that together create the "what happened? → what if?" workflow.

**Rationale**: Anomaly Detection answers "what went wrong?" Scenario Overlays answer "what if we change X?" Together, they create the most powerful user workflow: detect a problem, test a fix, compare outcomes. Both epics are well-designed; they just need to be built.

### Items

| # | Item | Phase | Notes |
|---|------|-------|-------|
| W5.1 | **Anomaly detection: threshold-based anomaly detectors** | Anomaly Phase 1 | As designed in the epic README. Capacity outage, throughput collapse, backlog explosion, latency spike, error burst. Now also includes: bottleneck shift (W2.1 score changes), flow efficiency drop (W2.3 below threshold), WIP limit breach (W2.4). |
| W5.2 | **Pathology classification: rule-based patterns** | Anomaly Phase 2 | Retry storm, slow drain, masked downstream failure. As designed. |
| W5.3 | **Incident construction + clustering** | Anomaly Phase 2 | As designed. |
| W5.4 | **Overlay contracts + artifacts** | Overlay Phase 1 | As designed in the overlays doc. |
| W5.5 | **Overlay comparison API + UI** | Overlay Phase 2 | As designed. Now also supports: "what if we add a WIP limit?" (uses W2.4), "what if we reduce variability?" (uses W2.5 Cv). |
| W5.6 | **Story & Dashboard generation** | Anomaly Phase 3 | As designed. Template-based first, AI-enriched second. |

### Commentary on Existing Epics

Both epics are thoughtfully designed. The Anomaly Detection README is one of the best architecture documents in the repo — clear concepts, phased implementation, explicit scope. The Overlays doc is equally clean.

**The main improvement from this plan**: With Waves 0-4 already shipped, anomaly detection has richer signals to work with (bottleneck scores, flow efficiency, WIP limit status, variability metrics, starvation/blocking flags). And overlays can test more scenarios (WIP limits, variability changes) in addition to the originally planned capacity/parallelism/arrivals tweaks.

**Sequencing within Wave 5**: Anomaly Phases 1-2 and Overlay Phase 1 can run in parallel. Story & Dashboard (Anomaly Phase 3) and Overlay Comparison (Overlay Phase 2) depend on their respective Phase 1s.

### Done Criteria

- Anomalies detected and stored as first-class artifacts
- Pathologies classified from anomaly patterns
- Incidents clustered and surfaced
- Overlay-derived runs work with full provenance
- "Detect problem → test fix" workflow operational

---

## Wave 6: Advanced Capabilities (future)

**Goal**: Longer-term capabilities that extend the platform further.

**Rationale**: These are genuine "nice to haves" that build on the complete analytical platform from Waves 0-5. They are listed here for completeness and to show where the roadmap's existing aspirational items fit.

### Items

| # | Item | Source | Notes |
|---|------|--------|-------|
| W6.1 | **WIP limit pull-based mode (back-pressure)** | Wave 2 follow-up | Multi-pass evaluation for upstream blocking. Complex but enables Kanban-style modeling. |
| W6.2 | **Cycle time distribution estimation** | Deep review gap | Approximate distributions from queue depth time series using distributional Little's Law or bin-level variation. Not a substitute for per-item tracking, but provides p50/p85/p95 approximations. |
| W6.3 | **Aging WIP approximation** | Deep review gap | Approximate item age from queue depth trends. If queue monotonically increases for N bins, oldest item is at least N bins old. Heuristic but useful. |
| W6.4 | **Monte Carlo forecasting** | Whitepaper "Future Directions" | Use preserved Cv (W2.5) and PMF shapes to run N stochastic evaluations and produce confidence intervals. Opt-in, non-default mode. |
| W6.5 | **Shared Resource Pool (Option 3)** | Dependencies future work | Multi-caller allocation for shared dependencies. |
| W6.6 | **Compiler Expansion (Option 4)** | Dependencies future work | High-level `calls:` policies compiled to explicit subgraphs. |
| W6.7 | **Delayed Feedback (Option 5)** | Dependencies future work | Shift-based retry feedback loops. |
| W6.8 | **Telemetry Loop & Parity** | Existing roadmap | Synthetic vs telemetry matching within tolerances. |
| W6.9 | **Streaming & Subsystems** | Existing roadmap | Exploratory. |
| W6.10 | **Ptolemy-Inspired Semantics** | Existing roadmap | Conceptual guardrails for future engine evolution. |

---

## Visual Summary: Wave Sequencing

```
Wave 0: Correctness & Hygiene          [1-2 weeks]
  |  Fix 3 bugs, Series immutability, NaN policy,
  |  topo cache, convergence guard, determinism test
  v
Wave 1: Documentation Truth             [1 week]
  |  Sync all docs with code reality
  v
Wave 2: Analytical Primitives           [2-3 weeks]    <-- NEW (not on any roadmap)
  |  Bottleneck ID, cycle time decomposition,
  |  flow efficiency, WIP limits, Cv preservation,
  |  starvation/blocking, Little's Law validation
  v
Wave 3: Near-Term Roadmap               [existing timeline]
  |  M-10.03 + constraint enforcement,
  |  Visualizations, Telemetry Ingestion
  v
Wave 4: Path Analysis + CFDs            [existing epic, enriched]
  |  Path queries, bottleneck attribution,
  |  path pain, CFD computation, UI/MCP
  v
Wave 5: Overlays + Anomaly Detection    [existing epics, enriched]
  |  Anomaly detectors, pathologies, incidents,
  |  overlay contracts, comparison, stories
  v
Wave 6: Advanced Capabilities           [future]
     Pull-based WIP, cycle time distributions,
     aging WIP, Monte Carlo, shared resources,
     compiler expansion, delayed feedback
```

---

## What Changes from the Current Roadmap

| Current Roadmap Sequence | This Plan's Sequence | Why |
|--------------------------|---------------------|-----|
| 1. Dependency Constraints follow-up | **0. Bug fixes first** | Bugs can produce wrong results; everything else is moot if the engine is incorrect. |
| 2. Visualizations | **1. Documentation truth** | Docs should be accurate before building features on top of them. |
| 3. Telemetry Ingestion | **2. Analytical primitives** | The engine needs to *analyze*, not just *compute*. This is the biggest value gap. |
| 4. Path Analysis | **3. Then existing near-term roadmap** | Dependency constraints, viz, and ingestion proceed as planned, but on a solid foundation. |
| 5. (aspirational) Overlays | **4. Path Analysis** (enriched) | Path analysis is more valuable with per-node analytical primitives already available. |
| 6. (aspirational) Anomaly Detection | **5. Overlays + Anomaly Detection** | Together these create the detect→test workflow. |
| — | **6. Advanced capabilities** | Long-term items, both new and existing. |

**Key difference**: Waves 0-2 are inserted before the existing near-term roadmap. This delays dependency constraint follow-up, visualizations, and telemetry ingestion by approximately 4-6 weeks. The tradeoff is:

- **Cost**: 4-6 weeks delay on existing near-term items.
- **Benefit**: When those items ship, they ship on a correct, analytically capable engine with honest documentation. Visualizations are more valuable because there are more things to visualize. Path Analysis is easier because the building blocks exist. Anomaly Detection has richer signals. The overall platform quality at every subsequent wave is higher.

---

## Commentary on Existing Epics

### Path Analysis & Subgraph Queries
**Verdict: Well-designed. Keep as-is, but slot after Wave 2.**

The epic README is solid. The bottleneck attribution formula (`binding = 1(Q[t-1] > 0) OR utilization approx 1`) is correct. Path pain (`sum of backlog-hours`) is a good interpretable metric. End-to-end latency estimate (`sum of per-node Little's Law estimates`) is pragmatic.

**Improvement**: With Wave 2's cycle time decomposition, path latency can be `sum(queueTime + processingTime)` per node, which is more accurate than `sum(queue/served * binMinutes)` alone.

**Open questions** (from the epic):
- Path volume definition: recommend **min-cut** for explicit paths, **entry-edge** for policy-based paths.
- Missing edge data: **emit warnings** and mark metrics as approximate (consistent with "make wrongness visible").
- Storage: **derive on demand** for v1; the computation is lightweight if per-node analytical primitives are cached.

### Anomaly & Pathology Detection
**Verdict: Excellent design. Best architecture doc in the repo. Keep as-is, slot into Wave 5.**

The phased approach (anomalies -> pathologies -> incidents -> stories -> AI enrichment) is exactly right. The pathology patterns (retry storm, slow drain, masked downstream failure) are well-chosen.

**Improvement**: With Wave 2's analytical primitives, anomaly detection gains additional signals:
- **Bottleneck shift**: the system constraint moved from node A to node B (detected via W2.1).
- **Flow efficiency drop**: efficiency falls below threshold (detected via W2.3).
- **WIP limit breach**: queue exceeds declared WIP limit (detected via W2.4).
- **Variability spike**: Cv increases (detected via W2.5).
- **Starvation event**: downstream starved while upstream has inventory (detected via W2.6).

These enrich the anomaly vocabulary without changing the architecture.

### Scenario Overlays & What-If Runs
**Verdict: Clean design. Keep as-is, slot into Wave 5 alongside Anomaly Detection.**

The overlay descriptor approach (minimal patch on inputs, new derived run with provenance) is correct. The validation rules (shape, type, domain, scope checks) are appropriate.

**Improvement**: With Wave 2's WIP limits and Cv preservation, overlays can test richer scenarios:
- "What if we set a WIP limit of 5 on the billing queue?"
- "What if arrival variability halved (Cv from 0.8 to 0.4)?"

These are high-value what-if questions that the current overlay scope (parallelism, capacity, arrivals) cannot express. The overlay descriptor schema should be extended to include `wipLimit` and `variability` targets.

### Dependency Constraints & Shared Resources
**Verdict: Foundations are solid. Follow-up needs scope expansion.**

Option A and Option B are both well-designed. The problem is that Option B's `ConstraintAllocator` is never called during evaluation. M-10.03 (MCP Pattern Enforcement) enforces patterns in the MCP modeling surface but does not wire the allocator into the engine.

**Required change**: Add an explicit milestone (call it M-10.04 or fold into M-10.03) that wires `ConstraintAllocator.Allocate()` into the evaluation pipeline when Option B constraints are declared. This is a small code change (the allocator logic is already correct) but has large trust implications.

---

## Performance Issues (Track Separately)

These were identified in the reviews but are not on the critical path. They should be tracked as tech debt and addressed opportunistically:

| Issue | Location | Impact | When to Fix |
|-------|----------|--------|-------------|
| O(N*E) topological sort | `Graph.cs` | Slow for large graphs | **Wave 0** (W0.5 covers this) |
| Duplicate Kahn's on every eval | `Graph.cs` | Redundant computation | **Wave 0** (W0.5 covers this) |
| ClassContributionBuilder called 4x | `InvariantAnalyzer.cs` | Redundant computation | Wave 2 or 3 |
| Per-line CSV writes | `RunArtifactWriter.cs` | Slow artifact generation | Wave 3 (telemetry ingestion will stress this) |
| Linear node lookup | `Topology.GetNode()` | O(N) per call | Wave 4 (path analysis will stress this) |
| Linear edge lookup | `Topology.GetOutgoing/IncomingEdges()` | O(E) per call | Wave 4 (path analysis will stress this) |

---

## Estimated Timeline

| Wave | Duration | Cumulative |
|------|----------|-----------|
| Wave 0: Correctness | 1-2 weeks | 1-2 weeks |
| Wave 1: Documentation | 1 week | 2-3 weeks |
| Wave 2: Analytical Primitives | 2-3 weeks | 4-6 weeks |
| Wave 3: Near-Term Roadmap | (existing timeline) | varies |
| Wave 4: Path Analysis | (existing timeline) | varies |
| Wave 5: Overlays + Anomaly | (existing timeline) | varies |
| Wave 6: Advanced | (future) | — |

Waves 0-2 add approximately 4-6 weeks of work before the existing near-term roadmap. This is the cost of building on a correct, analytically capable foundation. The alternative — building visualizations and telemetry ingestion on top of an engine with known bugs and no analytical primitives — will cost more in rework and lost trust.

---

## Relationship to Existing Documents

| Document | Action |
|----------|--------|
| `ROADMAP.md` | Update to reference this plan. Insert Waves 0-2 before "Near-Term Focus." |
| `work/epics/epic-roadmap.md` | Add "Analytical Primitives" as a new near-term epic between Engine Semantics and Dependency Constraints. |
| `docs/architecture/reviews/engine-review-findings.md` (March 7) | All items still open. Cross-reference with this plan's Wave 0 and Wave 1. |
| `docs/architecture/reviews/engine-deep-review-2026-03.md` (March 23) | Merge from worktree to main. Cross-reference with this plan. |
| `work/epics/path-analysis/spec.md` | No changes needed. Wave 4 implements it with enrichments from Wave 2. |
| `work/epics/anomaly-detection/spec.md` | No changes needed. Wave 5 implements it with enrichments from Wave 2. |
| `work/epics/overlays/overlays.md` | Minor update: add `wipLimit` and `variability` to overlay descriptor targets after Wave 2 ships. |
| `work/epics/dependency-constraints/spec.md` | Add note about engine-side enforcement (not just MCP-side). |
| `docs/flowtime-engine-charter.md` | Update "Current State" and "Near-Term Engine Work" sections to reflect this plan. |

---

## Conclusion

FlowTime has excellent engineering foundations: the spreadsheet/DAG metaphor, deterministic evaluation, conservation checking, and provenance tracking are all genuinely good. The 84 completed milestones show a disciplined team that knows how to ship.

The three bugs need to be fixed immediately. The analytical primitives need to be built next. Then the existing roadmap can proceed — better informed, better tested, and analytically richer at every step.

The path from "flow volume engine" to "flow analysis platform" is clear, achievable with the current architecture, and worth the 4-6 week investment before resuming the existing roadmap.
