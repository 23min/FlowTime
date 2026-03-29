# FlowTime Roadmap — Updated 2026-03-24

This roadmap reflects the current state of FlowTime Engine + Sim and incorporates findings from the March 2026 engine deep review. It supersedes the previous version (2026-01-24). Architecture **epics** and milestone docs provide the implementation detail (see `work/epics/epic-roadmap.md`).

## Scope & Assumptions
- Engine remains responsible for deterministic execution, artifact generation, and `/state` APIs (see `docs/flowtime-engine-charter.md`).
- FlowTime.Sim owns template authoring, stochastic inputs, and template catalog endpoints.
- Product-level scope is summarized in `docs/flowtime-charter.md`.
- The engine deep review (`docs/architecture/reviews/engine-deep-review-2026-03.md`) is the primary input for immediate priorities.

## Delivered (Completed Epics)

9 epics completed. See `work/epics/completed/` for full specs.

- **Time Travel V1** — `/state`, `/state_window` APIs, telemetry capture/bundles, DLQ/backlog semantics.
- **Evaluation Integrity** — Compile-to-DAG contract, centralized model compiler.
- **Edge Time Bins** — Per-edge throughput/attempt volumes, conservation checks, UI overlays.
- **Classes & Routing** — Multi-class flows with class-aware routing and visualization.
- **Service With Buffer** — First-class `serviceWithBuffer` node type replacing legacy backlog.
- **MCP Modeling & Analysis** — Draft/validate/run/inspect loop, data intake, profile fitting, storage.
- **Engine Semantics Layer** — Stable `/state`, `/state_window`, `/graph` contracts.
- **UI Performance** — Input/paint/data lane separation, eliminated main-thread stalls.
- **Package Updates** — .NET 9 dependencies and MudBlazor updated (M-11.01, M-11.02).

## Immediate — Engine Correctness & Analytical Primitives

**Epic:** `work/epics/engine-correctness-and-analytics/spec.md`

The engine deep review found 3 P0 correctness bugs, engineering debt, documentation drift, and a missing analytical layer. This epic must complete before near-term feature epics can deliver their full value.

### Phase 0: Correctness Bugs (P0)
Fix before all other work:
1. **BUG-1** — Shared series mutation (clone outflow before dispatch)
2. **BUG-2** — Missing capacity dependency in ServiceWithBufferNode.Inputs
3. **BUG-3** — InvariantAnalyzer ignores dispatch schedules (false positive warnings)
4. Regression tests + end-to-end determinism test

### Phase 1: Engineering Foundation + Phase 2: Documentation Honesty (parallel)
- Cache topological order, precompute adjacency lists, enforce NaN/Infinity policy
- Fix PCG32 modulo bias, add router convergence guard, Series immutability fix
- Add expression function tests (MOD, FLOOR, CEIL, ROUND, STEP, PULSE)
- Update docs: 11 expression functions, downgrade constraint claims, clarify time-travel scope
- Locate/create model.schema.yaml, standardize JSON Schema meta-version

### Phase 3: Analytical Primitives (new)
The missing layer between the engine's volume/throughput capabilities and what downstream epics need:
1. **Bottleneck identification** — Cross-node utilization comparison, WIP accumulation detection
2. **Cycle time decomposition** — Per-stage queueTime + processingTime, flow efficiency metric
3. **WIP limit modeling** — Optional wipLimit on ServiceWithBufferNode (Kanban what-if)
4. **Variability preservation** — Preserve Cv from PMFs for Kingman's approximation
5. **Constraint enforcement** — Wire ConstraintAllocator into evaluation pipeline
6. **Starvation/blocking detection** — Flag starved and blocked bins

### Spike: dag-map Library Evaluation (parallel with Phase 1+2)
Evaluate and extend the [dag-map](https://github.com/23min/dag-map) metro-map layout library for FlowTime topology rendering. ~2-3 days. Informs Visualizations and UI Layout Motors epics. See `docs/architecture/dag-map-evaluation.md`.

## Near-Term Epics

These depend on Phase 3 analytical primitives (except Telemetry Ingestion which is independent):

1. **Dependency Constraints & Shared Resources** (`work/epics/dependency-constraints/`)
   - Runtime constraint enforcement (depends on Phase 3.5). M-10.03 (MCP enforcement) deferred until runtime enforcement is in place. See `work/gaps.md`.

2. **Path Analysis & Subgraph Queries** (`work/epics/path-analysis/`)
   - Path-level queries, bottleneck attribution, dominant routes, path pain (depends on Phase 3.1, 3.2).

3. **Telemetry Ingestion, Topology Inference + Canonical Bundles** (`work/epics/telemetry-ingestion/`)
   - Gold Builder (raw data → binned facts) + Graph Builder (data → topology) + bundle assembly. Independent of Phase 3; can proceed in parallel. Process mining event logs (BPI Challenge) identified as first validation dataset. See `docs/architecture/dataset-fitness-and-ingestion-research.md`.

4. **Visualizations / Chart Gallery** (`work/epics/visualizations/`)
   - Role-focused charts with cycle time distributions, flow efficiency, bottleneck heat maps (depends on Phase 3.1, 3.2, 3.3). dag-map spike informs rendering approach.

## Svelte UI — Frontend Rewrite

**Epic:** `work/epics/svelte-ui/spec.md` | **Status:** planning

Replace the Blazor WebAssembly UI with SvelteKit + shadcn-svelte for demo-quality visuals. Independent of engine work — consumes existing APIs with zero backend changes. Initial scope: topology, timeline, dashboard, artifacts, run orchestration. Template Studio and Learning Center deferred. dag-map integration deferred (WIP).

7 milestones, estimated 10-13 weeks. See epic spec for details.

## Mid-Term / Aspirational

| Epic | Key Dependency | Notes |
|------|---------------|-------|
| **Scenario Overlays & What-If** | Phase 3.3 (WIP limits), 3.4 (variability) | Force-multiplied by analytical primitives |
| **Anomaly & Pathology Detection** | Phase 3.1 (bottleneck ID), 3.6 (starvation) | Leverages analytical primitives as building blocks |
| **Telemetry Loop & Parity** | Telemetry Ingestion | Synthetic/telemetry parity tooling |
| **UI Layout Motors** | dag-map spike | Pluggable layout engines behind stable contract |
| **Ptolemy-Inspired Semantics** | — | Conceptual guardrails for engine evolution |
| **Streaming & Subsystems** | Stable engine semantics | Long-term exploratory |

## Dependency Graph

```
Phase 0 (Bugs)
  |
  v
Phase 1+2 (Engineering + Docs)  ←→  dag-map spike [parallel]
  |
  v
Phase 3 (Analytical Primitives)
  |
  +--→ Dependency Constraints (needs 3.5)
  +--→ Path Analysis (needs 3.1, 3.2)
  +--→ Visualizations (needs 3.1, 3.2, 3.3 + dag-map results)
  +--→ Telemetry Ingestion (independent)
  |
  v
Scenario Overlays (needs 3.3, 3.4)
Anomaly Detection (needs 3.1, 3.6)
Telemetry Parity (needs Ingestion)
UI Layout Motors (needs dag-map spike)

Svelte UI (independent — runs parallel to all engine work)
```

## References
- `docs/architecture/reviews/engine-deep-review-2026-03.md` — Full engine deep review
- `docs/architecture/reviews/engine-review-findings.md` — Initial review findings (2026-03-07)
- `docs/architecture/reviews/review-sequenced-plan-2026-03.md` — Sequenced plan (historical rationale for this roadmap)
- `work/epics/epic-roadmap.md` — Architecture epics with links to specs
- `docs/architecture/dag-map-evaluation.md` — dag-map library evaluation
- `docs/architecture/whitepaper.md` — Engine vision + future primitives
- `docs/flowtime-engine-charter.md` — Engine remit and non-goals
