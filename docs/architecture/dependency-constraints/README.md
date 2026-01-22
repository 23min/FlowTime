# Dependency Constraints & Shared Resources

## Goal

Model downstream dependencies (databases, caches, external APIs, shared services) as **constraints** that can limit throughput and introduce hidden backlog/latency. Preserve FlowTime’s minimal basis (arrivals/served/queue depth) while making coupling and bottlenecks visible.

## Problem Statement

Real systems fail or degrade because of **shared dependencies**:
- A DB saturates and multiple services slow down together.
- A downstream API throttles and creates hidden backlog upstream.
- Latency rises even when local service capacity is available.

Without dependency modeling, FlowTime can explain *what* slowed down but not *why*.

## Principles

- **Effects over mechanisms:** Model observable constraints rather than internal implementation details.
- **Effective capacity:** Capture limiting factors (dependency capacity) even when internal queues are not directly observable.
- **Earned complexity:** Start with minimal signals; add inference only when needed.
- **Make wrongness visible:** Derived/inferred signals must be labeled and warnings surfaced.
- **Minimal basis, explicit mechanisms:** Arrivals/served/queue depth show *where* pressure accumulates; explicit mechanism models explain *why* it accumulates (e.g., retries, throttling, batching).

## Core Concept

A dependency is represented as a **resource constraint** that consumes load and emits completions.

Two compatible representations:

### Option A — Dependency as a Node (M-10.01)
Model the dependency as a node in the graph with arrivals/served:

```
Service A -> Dependency DB -> Service A -> Next Hop
```

- Works well for shared dependencies.
- Increases graph complexity; best with grouping/overlay UI.
- **M-10.01 MVP contract:** dependency nodes require `arrivals` + `served` (optional `errors`), no queue, no capacity, no retry semantics.
- **Option A canonical pattern (enforced):**
  - Dependency node represents **external call outcomes** only (arrivals/served/errors).
  - Upstream retry pressure is represented on **effort edges** and caller attempts.
  - Flow latency can propagate across effort edges **only when flow volume is present**.
  - Dependency latency is modeled explicitly (if needed) via its own processing-time series.

### Option B — Dependency as a Constraint on a Service (M-10.02)
Attach dependency constraints to a service node:

- Service capacity is limited by dependency capacity.
- Fewer nodes; simpler UI.
- Still supports shared constraints via a shared resource object.

Both options can coexist; the engine should support both.

## MCP Pattern Helpers (Option A)

To avoid ambiguous modeling, MCP should expose **pattern helpers** that emit the canonical Option A structure:

```
CreateDependencyNodeSeries(
  callerNode: "ProducerB",
  dependencyNode: "DependencyDb",
  callDurationMs: 180000,
  errorSeriesId: "dependency_errors",
  retryKernel: [0.6, 0.3, 0.1]   // optional, to derive retry pressure
)
```

This helper must:
- create the dependency node and effort edge
- derive caller attempts and processing time inflation
- keep the model acyclic
- tag derived series as `origin: derived`

If callers want more advanced behavior (shared allocation, feedback loops), that is **out of scope** for Option A and should be deferred to future milestones.

## Signal Model (Minimal Basis)

Dependencies still use the minimal basis:

- **Arrivals:** calls started to the dependency.
- **Served:** calls completed successfully.
- **Latency:** optional, used as a saturation indicator.
- **Errors/Throttles:** optional, used as a constraint signal.

Queue depth is typically inferred or marked as unknown.

## Inference Levels (Incremental)

**Level 0:** No dependency nodes. Service throughput only.  
**Level 1:** Dependency node with arrivals/served derived from telemetry.  
**Level 2:** Shared dependency with allocation across upstream services.  
**Level 3 (optional):** Latency-based inference of hidden backlog pressure.

## Edge Semantics (Dependency Load)

Dependencies rely on explicit **edge semantics**:

- **Throughput edges**: successful flow to downstream services.
- **Effort edges**: attempt load to dependencies.

This epic depends on the edge semantics contract (M-07.04) so that dependency load is explicit and validated.

## MVP Warnings (M-10.01)

- `missing_dependency_arrivals`: dependency arrivals were not provided.
- `missing_dependency_served`: dependency served was not provided.

These are emitted as **info** warnings and indicate that dependency load/utilization cannot be computed.

## UI Expectations

- Dependencies should be visually distinct (resource layer or node type).
- Service inspectors should show whether the service is **dependency-limited**.
- Shared dependencies should show capacity allocation or proportional load.

## Telemetry Compatibility

Telemetry ingestion should map observed dependency calls into arrivals/served for dependency nodes or effort edges. Where signals are inferred, provenance must mark `origin: derived` and expose warnings.

## Success Criteria (Epic-Level)

- Shared dependencies can be represented without breaking the minimal basis.
- Dependency load and coupling are visible in the topology and API.
- Inference is explicit and warnings make uncertainty visible.
