# Ptolemy Concepts & Relevance to FlowTime

## 1. Overview

This document summarizes key ideas from **Ptolemy II** (UC Berkeley) and analyzes how they relate to **FlowTime**.

At a high level, **Ptolemy II** is a platform for people who need to design and study **complex, concurrent, timing-sensitive systems** *before* they exist or before they are fully implemented:

- Typical users are **control/embedded/telecom/cyber-physical engineers** and researchers.
- They care about **scheduling, time semantics, coordination, and correctness** across mixed domains (continuous sensors, discrete controllers, networks, physical plants).
- They use Ptolemy to explore different **models of computation**, architectures, and timing assumptions, often long before there is production telemetry.

Concretely, Ptolemy II is a long-running research system for modeling and simulating **concurrent, heterogeneous systems** using **actor-oriented models**. Its main ideas:

- Systems are built from **actors** connected by typed **ports** and **channels**.
- A pluggable **director** defines the **model of computation** (MoC): Discrete-Event (DE), Discrete-Time (DT), Synchronous Dataflow (SDF), Process Networks (PN), Continuous Time (CT), modal/finite-state, etc.
- Models are **hierarchical**: actors can contain submodels, each with its own director.
- There is strong emphasis on **determinacy**, **scheduling**, and **static analysis** (especially in SDF-style domains).
- Advanced work (e.g., **PTIDES**) links model time to **real time** for distributed systems.

FlowTime has a different goal and a different primary audience:

- A **discrete-time, fixed-grid**, DAG-based engine.
- Focused on **flows through service systems**: queues, APIs, microservices, DLQs, data pipelines.
- Strong emphasis on **telemetry contracts** (gold/silver), time-travel exploration, and explainable metrics.
- Typical users are **SREs, platform/operations teams, and telemetry/observability engineers** who already have systems in production (or realistic telemetry) and want to **understand, replay, and stress those flows over time**.

Even so, several Ptolemy patterns are directly useful as **design inspiration** for FlowTime’s evolution:

- Treat **time semantics** as an explicit seam (directors).
- Use **hierarchy** and **subsystems** with clear port contracts.
- Introduce **modal models** for scenarios (Normal/Degraded/Outage/Recovery).
- Tighten **typed ports** and **multi-rate adapters**.
- Embrace **determinacy and static checks** as explicit contracts.

This document:

- Describes these Ptolemy concepts at a high level.
- Maps them to FlowTime’s current architecture and roadmap.
- Recommends where to **borrow** ideas and where to **draw the line**.
- Flags one or two places where it would be **unwise** not to plan ahead for future-proofing.

> This is a design exploration document only. It does not change any current FlowTime contracts or milestones.

---

## 2. Ptolemy in a Nutshell

Ptolemy II is organized around a few core ideas:

1. **Actor-Oriented Modeling**
   - Components (actors) communicate via **ports** and **channels**;
   - Ports are **typed**, and models are **graphs** of actors.

2. **Directors (Models of Computation)**
   - A **director** inside a composite actor defines the semantics of time and concurrency for that submodel.
   - Common directors: DE, DT, SDF, PN, CT, synchronous/reactive, FSM/modal, etc.
   - Different parts of a model can use different directors, connected via adapter actors.

3. **Hierarchy & Modal Models**
   - Composite actors contain submodels; each can have its own director.
   - **Modal models** pair an FSM with a submodel per state (e.g., Normal, Degraded, Outage).

4. **Determinacy, Scheduling, and Static Checks**
   - Some domains (like SDF) support compile-time scheduling, buffer size analysis, and deadlock detection.
   - Determinacy is emphasized: *same inputs ⇒ same outputs* under a given director.

5. **Real-Time Mapping (PTIDES)**
   - PTIDES (Programming Temporally Integrated Distributed Embedded Systems) links model time to physical time.
   - Provides conditions for when events can be safely executed/published in a distributed system.

FlowTime does **not** aim to become a general heterogeneous modeling lab like Ptolemy. Instead, the value is in selectively adopting patterns that:

- Improve clarity and explainability.
- Keep the design future-proof for streaming and refined semantics.
- Stay compatible with FlowTime’s telemetry-focused, discrete-time identity.

---

## 3. Current FlowTime Identity

Key characteristics of FlowTime today:

- **Discrete-Time on a Fixed Grid**
  - A global time window and bin size define a fixed grid.
  - All node metrics and telemetry are expressed per bin (gold contract).
  - Engine evaluation is deterministic and vectorized.

- **DAG Topology of Nodes**
  - Nodes represent services, queues, edges, adapters, etc.
  - Topology is a **DAG**; cycles are broken via delay/feedback mechanisms.

- **Flows & Classes**
  - Flows are modeled by **classes** (entity types), threaded through nodes.
  - Classes are orthogonal to labels (customer, region, environment).

- **Time-Travel & Telemetry Loop**
  - Time-travel APIs expose `/state`, `/state_window`, `/graph`, `/metrics` over canonical artifacts.
  - Synthetic telemetry defines gold contracts; real telemetry must conform to close the Loop.

- **Roadmap Directions**
  - Classes as flows (per-class node metrics, UI views).
  - EdgeTimeBin (per-edge flow facts, node–edge conservation, path analytics).
  - Engine-as-post-processing layer between telemetry stores and UIs.
  - Subsystems & zooming (hierarchical views, aggregated metrics).

Within this context, Ptolemy is not a blueprint but a **catalog of patterns**. The rest of this document matches these patterns to FlowTime’s architecture.

---

## 4. Directors: Time Semantics as a Seam

### 4.1 Ptolemy’s Directors

In Ptolemy, a **director** governs:

- How time advances (continuous vs discrete, fixed grid vs event-driven).
- How actors are scheduled (static SDF schedules vs dynamic events vs processes).
- What determinacy guarantees hold.

Each composite actor has one director, and different regions can use different directors connected via adapters.

### 4.2 FlowTime’s Implicit Director Today

FlowTime effectively has a single, implicit director:

- **DiscreteTimeDirector** (conceptually):
  - Time is a fixed grid of bins: `[start, start+Δt, …, end)`.
  - Nodes are evaluated per bin in topological order.
  - Metrics and telemetry are aggregated per bin.

This is a **good fit** for FlowTime’s goals:

- Capacity planning and backlog analysis.
- SLO risk per window.
- Telemetry aggregation and validation.

### 4.3 Recommendation: Make the DT Director an Explicit Concept

Even if no other directors are implemented soon, it is wise to:

- **Name** the current semantics explicitly in docs:
  - “Engine evaluation is governed by a DiscreteTimeDirector with binSize and binUnit.”
- **Architect internals** so that time/coordination semantics are a **pluggable seam**:
  - A small internal `IDirector` or equivalent that controls:
    - Advancing bins.
    - Ordering node evaluations.
    - Closing bins/windows.

Why this matters for the roadmap:

- Future **DE micro-modes** (sub-bin retries/timeouts) can be added as internal sub-directors without breaking schemas.
- Future **PTIDES-like streaming semantics** can decorate the DT director (e.g., bin watermarks, `isFinal`) rather than forcing a redesign.

**Not adopting a director seam would be unwise** if you expect:

- To add streaming/real-time semantics later.
- To experiment with sub-bin event dynamics in small parts of the model.

However, this can remain **internal and architectural**; no public API or schema changes are required today.

---

## 5. Hierarchy & Subsystems

### 5.1 Ptolemy’s Hierarchical Composite Actors

Ptolemy supports **hierarchical models**:

- Actors can contain submodels.
- Each composite can have its own director and ports.
- Hierarchy is a first-class way to manage complexity and mix computational domains.

### 5.2 FlowTime’s Subsystems

FlowTime’s subsystems design (in `docs/architecture/subsystems/README.md`) already mirrors some of these ideas:

- `model.subsystems` declares named subsystems.
- Nodes carry `subsystemId`.
- Subsystem-level views:
  - Zoomed-out: nodes represent subsystems; edges represent aggregated flows between them.
  - Zoomed-in: a single subsystem’s internal nodes and edges.
- Subsystems are also a natural unit for telemetry focus and validation.

### 5.3 Recommendation: Keep Subsystems Structural (for Now)

Ptolemy uses hierarchy to host different computational models; FlowTime should:

- Use subsystems primarily as **structural and visualization units**:
  - Group nodes logically.
  - Support zoomed-out/zoomed-in views.
  - Aggregate metrics by subsystem.
- Avoid per-subsystem directors for now:
  - All subsystems share the same DT time semantics.
  - This keeps the engine simpler and the telemetry contracts uniform.

You still benefit from Ptolemy’s **hierarchical modeling** without inheriting its full complexity.

---

## 6. Modal Models: Modes & Scenarios

### 6.1 Ptolemy’s Modal Models

Modal models pair a **finite state machine (FSM)** with submodels:

- States: Normal, Degraded, Outage, Recovery, etc.
- Each state can have its own submodel (or parameter pack).
- Transitions are driven by time, events, or conditions.

This is commonly used for systems with operating modes, faults, and reconfiguration.

### 6.2 FlowTime’s Scenario Needs

FlowTime already reasons about:

- Outages, degraded capacity, and recovery windows.
- Holiday calendars and special periods.
- Backfill/catch-up vs live processing.

Today these are expressed via:

- Template overlays.
- Different models/runs for different scenarios.

### 6.3 Recommendation: Plan a Small “Modes/Scenarios” Layer

Borrow the **modal model pattern** in a minimal form:

- A simple **Mode/Scenario layer** that:
  - Defines named modes (Normal, Degraded, Outage, Recovery).
  - Specifies **parameter packs** per mode (capacities, error rates, routing adjustments).
  - Uses simple schedules or FSMs to select the active mode per bin.

This can live entirely within FlowTime’s existing DT semantics:

- No change to bin-based contracts.
- No requirement to expose modes in APIs initially (they can just affect parameters).

This is **not urgent**, but planning for it will:

- Make scenario modeling more explicit, explainable, and testable.
- Align with Ptolemy’s modal models without requiring hybrid continuous/discrete semantics.

---

## 7. Typed Ports & Multi-Rate Adapters

### 7.1 Ptolemy’s Typed Ports

Ptolemy emphasizes **typed ports**:

- Ports have types (e.g., double, int, complex structures).
- Channels enforce compatibility.
- Implicit, magical conversions are discouraged.

### 7.2 FlowTime’s Implicit Port Shapes Today

FlowTime already has strong notions of **series and units**:

- Counts per bin (arrivals, served, errors).
- Rates (requests per minute, capacity per bin).
- Queue depth, latency, utilization, cost.
- Operators like RESAMPLE, DELAY, and rollups.

These are mostly enforced via schema and code conventions, but the **port shapes** are not yet fully formalized as types.

### 7.3 Recommendation: Tighten Port Types & Adapters (in Docs & Validation)

Without changing public APIs, FlowTime can:

- **Document port/series types explicitly**:
  - `Series[Bin, Count]`, `Series[Bin, Rate]`, `Series[Bin, LatencyMs]`, `Backlog[int]`, etc.
- **Standardize adapter nodes**:
  - RESAMPLE/AGGREGATE (e.g., 5m→1h).
  - HOLD/UPSAMPLE (e.g., 1h→5m).
  - RATE↔COUNT (given bin size).
  - DT↔DE bridges (future micro-modes).
- **Avoid implicit conversions**:
  - Any change in time resolution or unit should be an explicit node.

This is mostly a matter of **architecture and validation hygiene**:

- It reduces modeling errors.
- It makes graphs more explainable.
- It lays groundwork for future static checks (similar to SDF analysis, but in FlowTime’s simpler domain).

Again, this can be adopted at the **documentation and internal validation level** before any schema changes.

---

## 8. Determinacy & Static Checks

### 8.1 Ptolemy’s Determinacy Focus

Ptolemy domains often guarantee determinacy:

- Under a given director and well-formed model, repeated runs produce the same results.
- Some domains (e.g., SDF) offer compile-time checks for:
  - Deadlocks.
  - Buffer bounds.
  - Static schedules.

### 8.2 FlowTime’s Deterministic Execution

FlowTime already assumes and relies on:

- Deterministic evaluation given model + series.
- Deterministic time-travel state reconstruction.
- Conservative invariants (queue conservation, node–edge consistency, DLQ semantics).

### 8.3 Recommendation: Make Determinacy & Checks Explicit

Borrow the **ethos**, not the machinery:

- **State determinacy as a contract** in architecture docs:
  - “Given a fixed model, inputs, and time grid, FlowTime’s outputs are deterministic.”
- Continue to expand **static and runtime checks**:
  - Topology sanity (DAG, no missing nodes).
  - Node–edge and subsystem conservation (once EdgeTimeBin/subsystems are in place).
  - Soft vs hard validation modes.

This aligns with your existing trajectory (classes, EdgeTimeBin, subsystems) and reinforces FlowTime as a trustworthy semantics layer.

---

## 9. Real-Time & Streaming (PTIDES Inspiration)

### 9.1 Ptolemy’s PTIDES

PTIDES connects model time and real time:

- Defines conditions under which it is safe to execute/publish events in a distributed system.
- Uses **watermarks** and timing constraints to ensure correctness.

### 9.2 FlowTime’s Streaming Aspirations

FlowTime’s engine-as-post-processing doc already references:

- Batch runs today.
- Potential future streaming/near-real-time mode:
  - Incremental updates per bin.
  - `isFinal` / completeness hints per window.

### 9.3 Recommendation: Keep a Simple PTIDES-Like Hook

For now, it is sufficient to:

- Plan to expose **bin completeness** in APIs:
  - `isFinal` or `watermarkBinIndex` in `/state_window` and `/metrics`.
- Treat this as **a future extension of the DT director** rather than a separate domain.

Not planning any real-time mapping at all would be risky **only if** you expect strong streaming/real-time demands; however, a small watermark/completeness mechanism is enough to keep options open.

---

## 10. What Would Be Unwise to Ignore

Based on current architecture and roadmap, the following Ptolemy-inspired ideas are important enough that **ignoring them entirely would be unwise** for future-proofing:

1. **Director Seam (Time Semantics as a Pluggable Concept)**
   - Even if you stay DT-only for a long time, structuring the engine around a `DiscreteTimeDirector` concept will:
     - Make streaming, DE micro-modes, and PTIDES-style watermarks easier to add later.
     - Keep model schemas stable while the engine’s internals evolve.

2. **Typed Ports & Explicit Adapters**
   - As classes, EdgeTimeBin, subsystems, and telemetry contracts grow, the risk of subtle unit/time-shape bugs increases.
   - Having clear port types and explicit adapters in docs and validation will:
     - Reduce modeling errors.
     - Make models more explainable.
     - Help enforce conservation and compatibility rules.

Other Ptolemy ideas (modal models/modes, hierarchy per subsystem, compile-time checks, PTIDES watermarks) are all valuable but **can be introduced incrementally** as the roadmap unfolds, without forcing an early redesign.

---

## 11. Alignment with Existing Roadmap Items

Here is how the major Ptolemy-inspired ideas map to existing FlowTime roadmap directions:

- **Classes as Flows (CL-M-04.xx)**
  - Aligns with typed ports (per-class node metrics) and determinacy.
  - No major Ptolemy-driven redesign needed; classes fit cleanly into DT semantics.

- **EdgeTimeBin Foundations (ETB-M-05.xx)**
  - Natural place to strengthen typed ports and conservation checks.
  - Subsystem-level flows (later epic) can be defined as aggregates over EdgeTimeBins.

- **Subsystems & Zooming**
  - Already aligned with Ptolemy-style hierarchy.
  - Should remain DT-only for now; no multiple directors per subsystem required.

- **Engine-as-Post-Processing Layer**
  - Good match for determinacy and typed ports.
  - Future streaming/watermarks can borrow ideas from PTIDES without changing the core contracts.

- **Future Modes/Scenarios Epic (not yet formalized)**
  - Direct beneficiary of modal model ideas.
  - Can remain within DT semantics while switching parameter packs over time.

---

## 12. Summary

FlowTime and Ptolemy have different missions:

- Ptolemy: a rich lab for heterogeneous models of computation.
- FlowTime: a telemetry-first, discrete-time engine for flows through service systems.

However, several of Ptolemy’s **patterns** are highly relevant:

- **Directors** → treat time semantics as a pluggable seam (DT today, others later).
- **Hierarchy** → subsystems and zoomed views, already in FlowTime’s architecture.
- **Modal models** → a natural way to express outages, degraded modes, scenarios.
- **Typed ports & adapters** → make shapes/units/time explicit, avoid implicit magic.
- **Determinacy & checks** → reinforce FlowTime as a trustworthy semantics layer.
- **PTIDES-like watermarks** → future streaming completeness hints.

The immediate recommendation is to **capture these influences architecturally** (as this document does) and to:

- Make the DT director concept explicit internally.
- Incrementally strengthen typed ports and adapters in docs and validation.

All other Ptolemy-inspired ideas can be adopted **gradually** via small, focused epics (modes, compile-time checks, streaming readiness) without destabilizing the current roadmap or public contracts.
