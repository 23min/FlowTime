# Modeling Queues and Buffers Across Domains

> **Purpose:** Capture cross-domain modeling guidance for queues, buffers, and services. This document focuses on which FlowTime abstractions to use (plain `service` vs `serviceWithBuffer` vs visuals) in different domains, including cloud/message-queue systems.

---

## 1. Core Abstractions Recap

- **`service`** – Operational node with arrivals, capacity, served, errors, and routing. Waiting is **implicit**; there is no explicit queue/backlog series.
- **`serviceWithBuffer`** – Operational node that **owns** an explicit buffer and all service behavior:
  - Buffer stock $B_t$ and inflow/outflow series ($I_t$, $S_t$, optional $L_t$).
  - Capacity series $C_t$ and schedule gate $G_t$ (via `dispatchSchedule`).
  - Routing and per-class behavior (where modeled).
- **Queue visuals** – Topology elements that show queue/backlog metrics but **do not own behavior**. They point at a ServiceWithBuffer’s `queueDepth`.

Rule of thumb:

> If you care about **queue depth, cadence, or explicit draining policy**, model the stage as a `serviceWithBuffer`. Use plain `service` only when queueing is implicit and uninteresting.

### 1.1 ServiceWithBuffer Inspector Series Requirements

For inspector parity (service + ServiceWithBuffer), the API derives metrics when source series exist. These inputs should be emitted by templates or telemetry when you want the metric to show up:

- **Queue latency**: requires `queueDepth` + `served` for the same bins.
- **Utilization**: requires `capacity` (or capacitySeries) + `served`.
- **Service time**: requires `processingTimeMsSum` + `servedCount`.
- **Flow latency**: only shown when the series exists; it is not inferred from queue latency + service time.

If a required series is missing, the UI shows "No data" rather than fabricating values.

---

## 2. Kubernetes Services Reading Azure Service Bus Queues

### 2.1 Real-world picture

Consider a system where:

- Azure Service Bus queues hold messages with their own retry/dead-letter configuration.
- Kubernetes workloads (Deployments, Jobs) consume from one or more queues.
- Consumers can:
  - Defer messages back to the same queue (unlock / abandon).
  - Forward messages to other queues (fan-out / routing).

### 2.2 Recommended FlowTime mapping

For **capacity, backlog, and policy questions** ("how big do queues get?", "where are the bottlenecks?", "what is the effective throughput?"):

- Model each **queue + its consumer group** as a **`serviceWithBuffer`**:
  - Buffer (`queueDepth`) ≈ Service Bus queue length.
  - Arrivals ≈ upstream message arrivals into that queue.
  - Capacity/schedule ≈ consumer deployment size and any time-based throttling or cron-like behavior.
  - Routing from this node can represent messages forwarded to downstream queues.
- Use **queue visuals** to show the Service Bus queue explicitly in topology if helpful, but keep all behavior attached to the `serviceWithBuffer` node.

This mirrors the common "station with input queue" abstraction and keeps the **stock (queue)** and **valve (consumer behavior)** in one place.

### 2.3 When to separate queue and service conceptually

Sometimes the platform defines strong semantics on the queue itself (e.g., dead-lettering rules, invisibility timeout) that you want to reason about *separately* from consumer deployment behavior.

FlowTime guidance:

- **Still keep a single `serviceWithBuffer` node** for the main operational stage.
- Introduce **additional nodes or series** only when you need distinct flows, not new queue kinds:
  - Use extra series or supporting nodes to represent dead-letter flows or retry echos.
  - Represent "send to another queue" as normal routing from the consumer node to a downstream `serviceWithBuffer` that represents that next queue+consumer pair.

Avoid introducing independent "queue nodes" with their own service rules; that would blur ownership and drift toward DES.

---

## 3. Other Domain Patterns

### 3.1 Manufacturing / logistics

- **Workstations with WIP lanes, staging, or kitting areas** – Use `serviceWithBuffer` for each station that has meaningful WIP and dispatch/capacity policy.
- **Pure transforms / negligible waiting** – Use plain `service` when upstream and downstream buffers are not modeled and you only care about throughput through that step.

### 3.2 Call centers / human services

- **Agent pools handling queued work** – Model agent group + inbound queue as `serviceWithBuffer`.
- **Instant routing with no meaningful queue** – Use `service` when tasks are effectively processed as they arrive and you don't ask backlog questions.

### 3.3 Buses, batches, waves

- **Shuttle buses, picker waves, nightly jobs, batch ETL** – Always `serviceWithBuffer` with `dispatchSchedule` controlling $G_t$ so that draining occurs at discrete intervals.

---

## 4. Choosing Between `service` and `serviceWithBuffer`

Use these questions when modeling a stage:

1. **Do stakeholders care about queue depth/backlog at this stage?**
   - Yes → `serviceWithBuffer`.
   - No → consider plain `service`.
2. **Is there explicit dispatch or batch behavior (e.g., waves, cron jobs, buses)?**
   - Yes → `serviceWithBuffer` with `dispatchSchedule` or an equivalent schedule expression.
3. **Is capacity explicitly constrained and important to reason about?**
   - Yes, and backlog matters → `serviceWithBuffer`.
   - Yes, but backlog is negligible → either is possible; default to `serviceWithBuffer` if in doubt.

In many real systems (cloud queues, call centers, warehouses), the safe default is to model important stages as `serviceWithBuffer` and use plain `service` for glue or obviously unconstrained transforms.

---

## 5. Do I Need a Queue Node?

Use a **queue visual/node** when:

- You want to make a specific platform queue or staging area explicit in topology (e.g., a named Service Bus queue, a WIP lane), and
- You have a buffer/queue series to attach to it (from a `serviceWithBuffer` or telemetry), but
- You do **not** need that queue to own its own capacity/schedule/routing.

Rely on the **ServiceWithBuffer glyph alone** (no separate queue node) when:

- The stage is conceptually "queue + consumer" and you mainly care about backlog and throughput at that combined stage, or
- Introducing a separate queue node would add visual noise without clarifying anything for authors.

If in doubt, start with just `serviceWithBuffer` and add a queue visual later only when it helps readability or when you need to show a specific platform queue as a first-class telemetry-carrying entity.

---

## 6. Relationship to the Rest of the Docs

This note focuses specifically on **queues and buffers**. For a broader map of where modeling-related information lives in `docs/`, see `docs/modeling.md`.

This document lives under `docs/notes/` as a **modeling guidance note** and should be referenced from any future domain-specific guide on modeling cloud messaging systems.
