# Service With Buffer Nodes — Architecture Proposal

**Epic:** Service With Buffer  
**Status:** 🚧 Draft proposal  
**Related:** `docs/milestones/CL-M-04.03.02.md` (Scheduled Dispatch & Flow Control), routing/classes epic, UI topology docs

---

## 1. Problem & Motivation

FlowTime models systems as a DAG of operational nodes evaluated on a fixed time grid. Many real systems combine three concerns at a single stage:

1. A **service / work center** (machines, people, processes).
2. A **buffer** in front of that service (queue, WIP lane, staging area).
3. A **control policy** for how and when the buffer is drained (capacity, cadence, SLAs, hysteresis).

Earlier iterations introduced `kind: backlog` as a way to expose backlog (queue depth) series and, in CL‑M‑04.03.02, to attach `dispatchSchedule` semantics. This made backlog nodes *behave like* services with buffers, but the naming and schema still framed them as passive stores.

We now intentionally **promote ServiceWithBuffer to a first-class node type** and remove backlog as a public concept:

> A ServiceWithBuffer node is an **operational service node** that owns an explicit queue/buffer and all associated service behavior (capacity, schedule, routing, SLAs).

This is a **breaking change** in the modeling surface:

- Templates must use `kind: serviceWithBuffer` for these nodes.
- The engine and UI must treat ServiceWithBuffer nodes as primary operational nodes, rendered like services with a small queue badge.
- Legacy `kind: backlog` is not supported going forward in templates or public APIs.

Examples where ServiceWithBuffer is the natural abstraction:

- Warehouse picker waves: orders accumulate in a staging buffer; pickers drain on discrete waves (e.g., every 4 bins, up to 80 orders).
- Shuttle buses: passengers accumulate; buses depart on a timetable with fixed capacity.
- Batch machines: WIP accumulates until a batch is started; the batch drains the buffer.
- Nightly ETL or billing: events accumulate; a job drains them on a schedule.
- Cloud messaging stages where you care primarily about **throughput and backlog**, and individual platform queue behaviors can be treated as part of a single "queue + consumer group" stage.

---

## 2. Node Taxonomy After This Epic

After this epic lands, the core node taxonomy is:

1. **Service (`kind: service`)**  
   - Operational node with arrivals, capacity, served, errors, routing.  
   - No explicit queue/buffer series; any waiting is implicit.

2. **ServiceWithBuffer (`kind: serviceWithBuffer`)**  
   - Operational node that *owns* a queue/buffer and all service behavior around it.  
   - First-class in schema, engine, analyzers, and UI.  
   - Has everything a `service` has **plus** explicit queue metrics and policies.

3. **Queue visual (`kind: queue` in topology)**  
   - Purely a visual/semantic shell around the queue metrics of a ServiceWithBuffer node.  
  - It does **not** own behavior; it points at a ServiceWithBuffer’s queue series.

In particular, for many cloud/message-queue systems the **ServiceWithBuffer node can represent the combined "queue + consumer" stage** when the main questions are about backlog and capacity. Queue visuals can be used to make the platform queue explicit, but all capacity/schedule/routing semantics stay on the ServiceWithBuffer.

For systems with a **large number of independently managed platform queues** (e.g., Azure Service Bus plus MQ, each with distinct scaling and limits) and rich telemetry on the queues themselves, models may instead:

- Represent each platform queue as a **queue visual or dedicated node** whose series come from telemetry (synthetic or real), and
- Model key consumer behaviors as `service` or `serviceWithBuffer` nodes consuming from those series.

The key guardrail remains: queues themselves do **not** grow their own service semantics in FlowTime; they are sources of level/flow series that Service/ServiceWithBuffer nodes consume and act upon.

There is **no public backlog node kind** after this epic. Internally, we may still name certain runtime components “backlog” in code, but the schema and user-facing docs only speak in terms of **ServiceWithBuffer**.

---

## 3. ServiceWithBuffer Semantics

### 3.1 State and I/O

A ServiceWithBuffer node is an operational node with the following conceptual series (per bin index $t$):

- $I_t$: arrivals into the buffer.
- $B_t$: buffer/queue level entering bin $t$.
- $C_t$: nominal service capacity during bin $t$.
- $G_t$: schedule gate in $\{0, 1\}$ (derived from `dispatchSchedule` or always 1 if unscheduled).
- $S_t$: served volume during bin $t$.
- $L_t$: loss / attrition (optional).

Update rule:

- $S_t = \min(B_t, C_t) \cdot G_t$
- $B_{t+1} = B_t + I_t - S_t - L_t$

Key points:

- **Queue behavior is owned by the ServiceWithBuffer**, not by the queue visual.
- Schedules (cadence), capacities, and SLAs are all properties of the ServiceWithBuffer node.

### 3.2 Dispatch Schedule

CL‑M‑04.03.02 defines scheduled dispatch semantics. ServiceWithBuffer integrates that as a first-class property:

- `dispatchSchedule` encapsulates period, phase, and capacity override or gating.
- The typical time-based schedule is:
  - $G_t = 1$ if $(t - \\text{phase}) \\bmod \\text{period} = 0$, else $0$.

This is interpreted strictly as **service availability**: the service drains the queue only on bins where $G_t = 1$.

### 3.3 Service Properties

ServiceWithBuffer nodes expose the same family of properties as `service` nodes, plus explicit buffer and schedule:

- Service-like properties:
  - `capacitySeries` or equivalent.
  - Class-aware routing / contribution details.
  - SLAs, error handling, etc. (where supported).

- Buffer/queue properties:
  - Named queue/backlog series (for topology `queueDepth`).
  - Loss/attrition series if applicable.

- Schedule properties:
  - `dispatchSchedule` (time-based, possibly expression-driven using MOD/PULSE etc.).

This combination makes ServiceWithBuffer the canonical home for any behavior that involves both queue depth and service policy.

---

## 4. Front-End Treatment

### 4.1 Topology Rendering

Requirements for `src/FlowTime.UI` after this epic:

- ServiceWithBuffer nodes are rendered as **operational nodes**, visually in the same family as `service` nodes.
- Each ServiceWithBuffer node carries a **small queue badge** (isoceles trapezoid, same visual motif used for queues today):
  - The main node glyph indicates "this is a work center".
  - The badge indicates "this work center has an explicit buffer".
- Queue chips/overlays: any queue metrics (depth, backlog sparkline, status color) are anchored to the ServiceWithBuffer node, not to a separate standalone backlog node.

### 4.2 Queue Nodes in Topology

- `kind: queue` nodes in topology become purely **visual/semantic wrappers** around the queue of a ServiceWithBuffer:
  - They point at the ServiceWithBuffer’s `queueDepth`/buffer series.
  - They do not introduce independent scheduling or capacity.
- Over time, we may collapse some patterns so that many models can be expressed with just ServiceWithBuffer + edges, but this epic does not remove `kind: queue`; it just makes ownership explicit.

### 4.3 Tooltips and Chips

- Tooltips for ServiceWithBuffer must clearly explain:
  - Queue depth, arrival/served rates.
  - Dispatch schedule in human terms (e.g., "Dispatch every 6 bins, capacity 80").
- Schedule chips use the same information available from CL‑M‑04.03.02 UI work, but attached to the ServiceWithBuffer node.

---

## 5. Schema Shape (High-Level)

At the schema level (`docs/schemas/model.schema.yaml`), ServiceWithBuffer is a **distinct `kind`** with its own section. Roughly:

```yaml
nodes:
  - id: PickerWave
    kind: serviceWithBuffer
    semantics:
      arrivals: wave_stage_inflow
      served: picker_wave_release
      errors: wave_attrition
      queueDepth: picker_wave_backlog
      loss: wave_attrition
      capacitySeries: wave_dispatch_capacity
      dispatchSchedule:
        periodBins: ${wavePeriodBins}
        phaseOffset: ${wavePhaseOffset}
        # or expression-based reference using PULSE/MOD
```

Important:

- `kind: backlog` is **not** accepted in new templates.
- The ServiceWithBuffer semantics are documented as part of the public engine surface, alongside `service`.
- `queueDepth` may be omitted or set to `self` if you do not need a named alias; otherwise the loader creates the queue series for you using the identifier you provide (no helper nodes required).
- Dispatch schedules belong to the topology node. The loader synthesizes any hidden queue/loss series so templates only carry the operational semantics you care about.

Exact field names are to be finalized in the milestone spec, but the intent is clear: ServiceWithBuffer owns both the queue series and the service behavior.

---

## 6. Migration and Breaking Change Scope

This epic accepts breaking changes in:

- Template YAML: templates must be updated from `kind: backlog` to `kind: serviceWithBuffer`, and any accompanying field renames that clarify ownership.
- UI expectations: topology rendering should treat ServiceWithBuffer as the primary operational node where previously backlog + queue composition was used.
- API responses: `/graph` and `/state_window` should identify these nodes as `serviceWithBuffer` in node-type fields.
- Queue latency semantics: `/state` responses now include `queueLatencyStatus`. When a dispatch gate suppresses release but backlog exists, the status is `paused_gate_closed` so operators see an explicit “paused” badge instead of a generic warning.

We explicitly **do not** preserve backward compatibility for the `backlog` kind at the schema level. Any internal compatibility shims (e.g., loader accepting backlog while tests migrate) are considered temporary tooling, not part of the public contract.

---

## 7. Interaction with CL‑M‑04.03.02

CL‑M‑04.03.02 adds:

- Expression primitives (MOD/FLOOR/CEIL/ROUND/STEP/PULSE).
- `dispatchSchedule` semantics for the node currently called backlog.

This epic **relabels and reshapes** that node as ServiceWithBuffer:

- The math and scheduled-dispatch behavior defined in CL‑M‑04.03.02 remain valid and are **owned by ServiceWithBuffer**.
- Any remaining mentions of "backlog node" in CL‑M‑04.03.02 documentation should be updated to "ServiceWithBuffer node" as that work is completed.

---

## 8. Future Extensions

Once ServiceWithBuffer is in place as a first-class node type, it becomes the natural home for:

- Priority queue policies (per-class or per-priority service ordering).
- Class-specific capacities or SLAs.
- Hysteresis/scaling policies (e.g., only dispatch when backlog exceeds a threshold).

Those behaviors are **out of scope for the initial milestone**, but the design intentionally reserves ServiceWithBuffer as the extension point.

---

## 9. Implementation Plan (High-Level)

The detailed breakdown lives in `docs/milestones/SB-M-05.01.md`, but at a high level we will:

1. Update schema and loader to introduce `kind: serviceWithBuffer` and remove `kind: backlog` from the public contract.
2. Update engine code to treat ServiceWithBuffer as the owner of queue + schedule semantics.
3. Update UI to render ServiceWithBuffer as an operational node with a queue badge and schedule chips.
4. Update analyzers and CLI wording to consistently speak about "services with buffers".

All template and test updates will be done in lockstep with these changes.

---

## 10. Queue Latency Semantics (SB‑M‑05.02)

ServiceWithBuffer nodes expose queue latency plus a status descriptor so downstream tools can distinguish “latency not computable” from “latency intentionally paused.” Queue-like nodes emit `queueLatencyStatus` in `/state` payloads:

- `null` when latency is computable.
- `paused_gate_closed` when backlog is present, served volume is zero, and the dispatch gate is closed for that bin.

UI/CLI expectations:

- The topology canvas and inspector show a “Paused (gate closed)” badge for affected bins. CLI warnings no longer spam “latency could not be computed”; they reference the paused status instead.
- Analyzer messages map to the new status (`queue_latency_gate_closed`) so template authors know the behavior is intentional.

This semantics layer ensures scheduled dispatch nodes remain observable even when the queue intentionally withholds work.
