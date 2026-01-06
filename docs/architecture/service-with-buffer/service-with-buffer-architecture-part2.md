# ServiceWithBuffer Architecture Part 2: SLA, Batch Semantics, and Backlog Health

## Purpose

This document extends the ServiceWithBuffer architecture with domain-aware service-level semantics, batch/gated-release behavior, and backlog health signals. It is meant to be future-proof across template-driven runs and telemetry-driven runs, so the UI remains agnostic to the data origin and relies on a consistent API contract.

Part 1 defines the ServiceWithBuffer abstraction and core series. Part 2 defines how to interpret those series for service levels, queue health, and scale signals across domains.

## Scope

In scope:
- SLA semantics that remain meaningful for batch or gated-release systems.
- Backlog health signals and warning conditions.
- Parallelism and scaling representation (capacity and optional instance counts).
- Cross-domain modeling guidance that maps real-world signals to ServiceWithBuffer series.

Out of scope:
- UI-specific rendering decisions.
- Performance optimizations.
- New engine features that are unrelated to ServiceWithBuffer.

## Design Goals

- **Domain-aware but consistent:** Different domains use different SLA notions, but the underlying series should map to a common contract.
- **Batch-safe:** Metrics must remain meaningful between releases, not just at release bins.
- **Telemetry-ready:** Metrics must be derivable from typical telemetry fields (enqueue, dequeue, completion, instance count).
- **No false signals:** Avoid reporting 0% SLA just because a system completes in batches.

## Core Concepts

ServiceWithBuffer is a service stage that owns a queue/buffer and processes items with capacity and optional schedule gating. The core time-series inputs are:

- Arrivals: items entering the queue.
- Served: items completing service (or released in a batch).
- Queue depth: backlog size.
- Capacity: throughput potential (may be time-varying).
- Processing time sum/count: time spent servicing items.
- Optional schedule: windows when release/serve is allowed.

## SLA and Service Level Semantics

There is no single SLA standard across domains. FlowTime should support a small set of SLA classes that can be computed from telemetry or templates.

### 1) Completion SLA (Event-Based)

Definition: Percentage of completed items whose end-to-end latency is within a target threshold.

Why it matters:
- Common in logistics, healthcare, and cloud queues.
- Requires per-item timestamps (or accurate aggregated latency series).

Telemetry mapping:
- Arrival time or enqueue time.
- Completion time or dequeue time.
- SLA threshold per class or per node.

Batch compatibility:
- Completion SLA should be evaluated on completed items only.
- Between releases, the metric can be carried forward or marked as "no completion events".

### 2) Backlog Age SLA (Queue Risk)

Definition: Percentage of queued items whose queue age is within a target threshold.

Why it matters:
- Provides a continuous signal between releases.
- Indicates risk of SLA breaches before completion happens.

Telemetry mapping:
- Queue age distribution (p50/p95/max) or per-item age.
- If telemetry only provides queue depth, approximate risk cannot be derived reliably.

Batch compatibility:
- Remains meaningful even when served events are sparse.

### 3) Schedule Adherence (Batch/Gated Release)

Definition: Degree to which release events occur on schedule (or within tolerance).

Why it matters:
- For buses, waves, nightly jobs, dispatches, and clinic sessions.
- Reflects operational discipline even if backlog is high.

Telemetry mapping:
- Actual release timestamps compared to scheduled windows.
- Per-release lateness or early release deviation.

Batch compatibility:
- Works regardless of backlog; separate from throughput.

### 4) Throughput SLAs (Optional)

Definition: Percentage of bins where served meets or exceeds a target minimum.

Why it matters:
- Useful for capacity planning and SLO-style metrics.
- Works with sparse release patterns if defined per release window.

## Batch and Gated Release Semantics

Batch release systems should not be forced into per-bin SLA interpretations. Recommended behavior:

- **Completion SLA:** Evaluate on release bins, then carry forward until the next release.
- **Backlog risk:** Compute continuously from queue age distribution where possible.
- **Schedule adherence:** Show for each release and as a rolling compliance rate.

These three combined remove the "0% SLA between releases" artifact.

## Parallelism and Scaling

Parallelism is a capacity concept. It represents how many items can be processed concurrently or per unit time.

### Capacity and Instances

- **Capacity series** represents total throughput potential per bin.
- **Optional instance count** can be recorded as `instances` (or similar) for visibility.

Use cases:
- Kubernetes replicas -> capacity series and optional instance count series.
- Transport fleet size -> capacity series that scales with vehicles.
- Healthcare staffing -> capacity series driven by shift staffing.

### Auto-scaling

Auto-scaling is a policy that adjusts capacity based on backlog signals:

- Queue depth threshold triggers capacity increase.
- Backlog age threshold triggers capacity increase.
- Arrival rate vs capacity ratio triggers capacity increase.

Telemetry mapping:
- If telemetry exposes instance count, model the realized capacity series.
- For simulated templates, derive capacity from a policy expression.

## Backlog Health Signals

Backlog growth is a bottleneck signal. It should be visible and warn when sustained.

### Recommended Signals

- **Growth streak:** queueDepth increases for N consecutive bins.
- **Overload ratio:** arrivals / capacity > 1 for N consecutive bins.
- **Age risk:** queueAgeP95 exceeds SLA threshold for M bins.
- **Saturation:** queueDepth exceeds a max or soft limit.

### Warning Scenarios

1) **Sustained growth:** backlog rises for N bins with no compensating releases.
2) **Chronic overload:** arrivals consistently exceed capacity.
3) **Aging backlog:** queue age exceeds SLA target for multiple bins.

Warnings should identify:
- Node name.
- Time window.
- Primary signal (growth, overload, or age).

## Domain Guidance

### Transportation and Logistics

Common SLA definitions:
- On-time departure or on-time arrival.
- On-time delivery per shipment.
- Cutoff compliance.

Mapping to ServiceWithBuffer:
- Use schedule adherence for departures.
- Use completion SLA for deliveries.
- Use backlog age SLA for queue risk at depots.

Batch releases (buses, dispatch waves):
- Show schedule adherence and backlog age between releases.

### Healthcare

Common SLA definitions:
- Time to triage or time to treatment.
- Percent within target (e.g., 30 minutes).

Mapping to ServiceWithBuffer:
- Use completion SLA for treatment completion.
- Use backlog age SLA for waiting room risk.
- Use capacity series for staff counts by shift.

### Manufacturing and Warehousing

Common SLA definitions:
- Cycle time targets.
- Wave completion by deadline.

Mapping to ServiceWithBuffer:
- Use completion SLA for cycle time.
- Use schedule adherence for wave release.
- Use backlog age for WIP risk.

### Cloud Messaging and IT Systems

Common SLO/SLA definitions:
- Message latency percentiles.
- Queue age thresholds.
- Consumer lag.

Mapping to ServiceWithBuffer:
- Use completion SLA from enqueue to dequeue timestamps.
- Use backlog age for lag risk.
- Use capacity series from consumer replica counts or throughput limits.

## Continuous Processing (Non-Batch) Requirements

ServiceWithBuffer must also support continuous processing where arrivals and served are present in every bin (or nearly every bin). In these cases:

- Completion SLA should be evaluated per bin and over rolling windows (not only on release bins).
- Backlog age risk remains valid as a steady signal.
- Schedule adherence may be irrelevant and should be optional.
- Capacity and utilization should be smoother and represent steady throughput.

The engine and API must treat continuous scenarios as first-class, not just a special case of batch systems.

### Current Template Coverage

- `transportation-basic` includes a ServiceWithBuffer node (`HubQueue`) without `dispatchSchedule` (continuous), but it does not include classes.
- `transportation-basic-classes` includes ServiceWithBuffer nodes with `dispatchSchedule` (batch) and class coverage.
- `warehouse-picker-waves` is explicitly batch-oriented with `dispatchSchedule`.

There is no current template that exercises ServiceWithBuffer with both **continuous processing** and **class coverage**.

### Proposed Continuous Template (IT Document Processing)

Add a new template that represents a continuous IT pipeline with multiple document classes:

- **Scenario:** Incoming documents of multiple types are ingested, routed to specialized processors, and then merged into customer-facing output services.
- **Classes:** At least three document types (e.g., invoice, contract, support-ticket).
- **Nodes:** All processors are `serviceWithBuffer` to model queue + service in one place.
- **Routing:** A router directs classes to specialized processors; outputs are merged back to a customer-facing stage.
- **External dependencies:** Each service calls an external system (e.g., database or fraud check) modeled as a downstream service or retry target.
- **Retries and DLQ:** Each processor includes retry semantics and a DLQ target.
- **Continuous arrivals:** Arrivals are continuous (no dispatch schedule).

This template should be used as the reference for continuous ServiceWithBuffer validation with class coverage.

## Telemetry Alignment

For future telemetry parity, these inputs are preferred:

- Enqueue time, dequeue/complete time (for completion SLA).
- Queue depth and queue age distribution.
- Instance count and capacity constraints.
- Release schedule timestamps for gated systems.

If telemetry is missing queue age distribution, backlog age SLA cannot be computed accurately and should be marked as unavailable rather than invented.

### Queue Age Telemetry Gaps (Azure Service Bus and Similar Systems)

Some queueing platforms (including Azure Service Bus) do not expose **oldest message age** or a full queue age distribution by default. This creates a real gap for backlog age SLA:

- **Why this matters:** Backlog age SLA depends on the age distribution (or at least p95/max age). Without that, FlowTime cannot reliably compute "items at risk of breaching SLA" between releases.
- **What we must not do:** Do not fabricate backlog age SLA from queue depth alone. That would produce misleading "risk" signals and hurt trust.

#### Practical Options (Telemetry-Safe)

1) **Emit oldest-age or age distribution from telemetry**  
   - Capture oldest message age via explicit instrumentation (e.g., consumer peek or administrative API where available).
   - Emit p50/p95/max queue age as time-binned series alongside queue depth.

2) **Producer/consumer timestamps**  
   - If producers emit enqueue time and consumers emit completion time, compute backlog age from the oldest unserved enqueue timestamp.
   - This can be approximated if per-item telemetry is available or can be sampled.

3) **Fallback to backlog growth warnings**  
   - When age distribution is missing, use **growth streak**, **overload ratio**, and **sustained backlog** as proxy risk signals.
   - These are weaker signals but still meaningful and are consistent with the anomaly-detection epic’s principles.

4) **Explicitly mark backlog age SLA as unavailable**  
   - The UI should show "No data" rather than a misleading age SLA.

This gap should be documented in any telemetry integration plan. If a platform cannot emit queue age, FlowTime should still compute completion SLA (when completion timestamps exist) but mark backlog age SLA as unavailable.

## Coordination with Future Epics

This document intentionally avoids implementing full anomaly detection or edge-level analytics.

- **Anomaly Detection (docs/architecture/anomaly-detection/README.md)**  
  We only define minimal backlog health *signals* (growth streak, overload ratio, aging) as local node warnings. We do **not** define incident clustering, baselines, or multi-node pathology logic. Those belong to the anomaly detection epic.

- **EdgeTimeBin (docs/architecture/edge-time-bin/README.md)**  
  All backlog and SLA semantics here are **node-centric** and remain valid even before EdgeTimeBin exists. We should not require per-edge flows or routing analytics to compute the baseline metrics in this document.

Guardrails:
- No cross-node anomaly correlation in this milestone or follow-up.
- No new edge-level data requirements introduced here.
- All metrics stay compatible with the existing time-bin model.

## Consistency and Validation

To prevent inconsistent metrics:

- Queue depth should satisfy: prior depth + arrivals - served - errors (plus optional attrition).
- If this invariant fails, mark the node with a warning for model/telemetry alignment.
- Metrics derived from missing series should be explicitly marked as "No data".

## Future Enhancement: Success Terminal (Sink) Nodes

Some domains benefit from an explicit **success terminal** to represent work leaving the modeled system (e.g., delivered to customer, arrived at airport). This is different from a DLQ: a sink is a **successful exit**, not a failure path.

### Why Introduce a Sink

- Clarifies intent: avoids modeling a destination as a capacity-limited service that produces "errors" when the model should simply terminate work.
- Improves SLA interpretation: completion SLA can anchor at the sink instead of overloading leaf services.
- UI clarity: a sink chip/badge distinguishes "terminal success" from ordinary leaf services.

### Semantics (Proposed)

- A sink does not own capacity or queue semantics.
- `served = arrivals`, `errors = 0` by definition.
- No retries, no queue, no latency metrics unless provided as explicit telemetry.
- A sink is compatible with either templates or telemetry; it is optional and only used when terminal success is explicitly modeled.

### Telemetry Mapping

- If telemetry emits explicit "completion/delivered" events, the sink can be mapped directly to those series.
- If telemetry does not expose completion events, a sink should not be inferred. The UI should simply treat the last service as a leaf node.

### Implementation Path (Low-Risk)

1. **Metadata-first**: Introduce a `nodeRole: sink` (or similar) flag on existing `service` nodes.
2. **UI-only semantics**: Render a sink badge and suppress error-rate chips unless `errors` is explicitly provided.
3. **Engine remains unchanged**: No new behavior or schema required in phase 1.

If the pattern proves valuable, a dedicated `kind: sink` can be added later.

## Open Questions

- How to represent queue age distribution without per-item telemetry.
- How to define SLA in mixed-mode systems (continuous service with periodic releases).
- How to standardize schedule adherence across domains with different dispatch semantics.

## Next Steps

- Add an explicit SLA taxonomy to the API contract (completion SLA, backlog risk, schedule adherence).
- Add backlog health warnings based on sustained growth and overload ratios.
- Update templates and telemetry mappings to support queue age distribution where possible.
