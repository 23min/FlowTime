# Dependency Modeling: Future Work (Options 3–5)

This document evaluates longer‑term dependency modeling options against **current FlowTime capabilities** (M‑10.x) and the engine constraints (deterministic, DAG, grid‑aligned). It also records why these options are attractive, what they require, and how to keep today’s implementation compatible with them.

## Current State (M‑10.x)

**What exists now:**

- **Option A (M‑10.01):** Dependency as a node with **arrivals/served/errors** only (no queue, no capacity, no retries).
- **Option B (M‑10.02 planned):** Dependency as a **constraint** on a service node (capacity/throughput modifiers).
- **Edge semantics (M‑07.x):** Explicit **throughput** vs **effort** edges so retry load is engine‑owned, not UI‑derived.

**Constraints we must preserve:**

- **Deterministic evaluation on a time grid** (spreadsheet/DAG model).
- **No cycles** in the evaluation graph.
- **Minimal basis** (arrivals/served/queue) remains the core, with any inference made explicit.

**Current limitations (by design):**

- No **automatic retry feedback** loops (errors do not re‑enter arrivals).
- No **shared resource allocation** across multiple callers.
- No **compiler expansion** of “dependency call policies” into hidden subgraphs.

These limitations are acceptable in M‑10.x as long as they are **explicitly documented** and **not hidden in the UI**.

## Conceptual Guide (Why Dependencies Matter)

Even when systems are complex, you usually observe:

- **Arrivals** (requests, orders, items)
- **Served** (completed, delivered, acknowledged)
- **Queue depth** (backlog / wait)

These show **where** pressure accumulates. Dependency modeling adds the missing **why**:

1. **Capacity bottleneck / contention** — callers compete for a shared resource.
2. **Delay inflation → SLA breaches** — backlog translates into latency.
3. **Errors & retries → load amplification** — failure creates additional attempts.
4. **Retry exhaustion / loss** — work can be abandoned or sent to DLQ.

Any future dependency system that cannot express these four effects will struggle to explain root cause.

## Option 3 — Shared Resource Pool (Dependency as a Resource)

### What it is
Represent the dependency as a **resource pool** that allocates capacity across multiple callers. Callers submit **demand**; the pool grants **served** per caller.

### Why we want it
This is the best way to model **shared contention**:

- “DB capacity was 2000 calls/bin; Billing demanded 1600, Orders demanded 1200.”
- Cross‑service coupling becomes explainable instead of speculative.

### Advantages
- Models **shared bottlenecks** correctly.
- Explains **coupling** and capacity theft across services.
- Maps well to telemetry that has dependency‑level aggregates.

### Disadvantages
- Requires a **multi‑caller allocation** mechanism (policy).
- Adds a new dimension (caller/tenant/class) to dependency flows.
- More complex API surface: per‑caller demand/served, allocation details.

### Technical requirements
- A **resource allocator** (priority, weighted‑fair, proportional).
- Per‑caller series: `demand_by_caller`, `served_by_caller`.
- A schema to declare **allocation policy**.
- Engine changes to compute allocation deterministically per bin.

### Risks
- Complexity increase in the engine and state contracts.
- Poor UX if the resource node cannot be collapsed or summarized.
- Harder to validate without strong tests and provenance.

### Telemetry compatibility
Possible if telemetry can provide:

- Per‑caller call counts (or trace‑derived estimates), or
- Aggregate dependency metrics plus caller proportions (inferred).

Inference must be **explicit** and flagged.

---

## Option 4 — Compiler Expansion (Dependency Call Policies)

### What it is
Let templates define dependencies at a high‑level (e.g., `calls:` block), and **compile** that into an explicit subgraph (retry nodes, delays, DLQs, effort edges).

### Why we want it
Maintains a **clean modeling surface** while keeping engine semantics explicit and deterministic. This is the best UX for non‑experts.

### Advantages
- Keeps templates **simple and readable**.
- Ensures engine remains **pure** (everything is explicit after compilation).
- Enables consistent reuse of retry/backoff patterns.

### Disadvantages
- Requires a **model compiler** phase and clear lineage for debug.
- If expansion is opaque, users can’t trust or verify it.

### Technical requirements
- A **macro/expansion** framework in the model compiler (M‑06.02 foundation).
- A **lineage map** from template intent → generated nodes/edges/series.
- A contract for how retries, delays, and DLQs are expanded.

### Risks
- “Magic” expansions that users can’t explain.
- Template/spec drift if expansion rules change without versioning.

### Telemetry compatibility
High, as long as expansion is **deterministic** and telemetry series map to the generated graph.

---

## Option 5 — Delayed Feedback / Retry Loops

### What it is
Allow error → retry → arrivals loops **with explicit delay** so feedback is causal (e.g., retries occur at `t+1`).

### Why we want it
Captures **true retry storms** and upstream amplification without hand‑crafted approximations.

### Advantages
- Most faithful to real systems.
- Makes retry amplification visible as **flow feedback**.

### Disadvantages
- Cycles violate the **DAG constraint** unless handled specially.
- Requires evaluation changes (multi‑pass or delayed feedback primitives).

### Technical requirements
- A **delay/shift primitive** allowed in feedback paths.
- A **two‑phase evaluation** or special feedback handling to keep determinism.
- Explicit UI/provenance to mark “feedback” paths.

### Risks
- Harder to reason about correctness.
- Potential performance cost on large graphs.

### Telemetry compatibility
Depends on having retry latency distributions or retry timing signals.
If telemetry is missing, feedback becomes speculative unless clearly flagged.

---

## Compatibility Guardrails (Keep Future Options Easy)

To avoid blocking Options 3–5, keep these guardrails in M‑10.x:

1. **No UI‑only derivations.** All dependency signals must be in the engine/API so other clients (MCP, agents) are consistent.
2. **Explicit edge semantics.** Keep effort vs throughput edges; do not collapse them in UI logic.
3. **Provenance on derived series.** Always label origin (`explicit` vs `derived`) so future expansions remain auditable.
4. **Avoid cycles in templates.** Use retry load as **attempts** + service‑time inflation; do not re‑inject arrivals.
5. **Stable series IDs.** Don’t rename `arrivals/served/errors/attempts` in ways that break telemetry mapping.

These choices make it straightforward to later:

- Introduce a resource pool allocator (Option 3).
- Add compiler‑driven expansions (Option 4).
- Add delayed feedback primitives (Option 5).

---

## Roadmap Notes (Non‑Epic Items)

These are **future roadmap items**, not epics yet:

- **Shared Resource Pool (Option 3)**: introduce caller allocation and per‑caller served.
- **Compiler Expansion (Option 4)**: define `calls:` policies and expand into explicit nodes/edges.
- **Delayed Feedback (Option 5)**: add shift‑based feedback semantics or multi‑pass evaluation.

Until then, **Option A + Option B** remain the correct and engine‑pure way to model dependencies.
