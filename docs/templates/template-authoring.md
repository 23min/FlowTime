# Template Authoring Guide

Designing FlowTime templates follows a predictable structure: metadata and parameters at the top, grid/window configuration, topology semantics, and the computational nodes/outputs that back those semantics. This guide captures the current best practices so new templates land with the correct wiring, retry governance semantics, and analyzer coverage.

## Directory Structure

- Author templates under `templates/`. Each template lives in its own YAML file.
- Deterministic fixture bundles (used for API/UI tests) live under `fixtures/`.
- Telemetry capture and provenance live under the generated run directories (`data/runs/<runId>`), so templates **never** reference absolute paths on the author’s machine.

## Required Sections

```yaml
schemaVersion: 1
generator: flowtime-sim
metadata:
  id: supply-chain-multi-tier
  title: Multi-Tier Supply Chain
  description: …
  narrative: >
    A short narrative that explains the modeled system, its intent, and any
    assumptions that future readers should understand.
  version: 3.0.0
window:
  start: 2025-01-01T00:00:00Z
  timezone: UTC

parameters:
  - name: bins
    type: integer
    default: 288
    minimum: 12
    maximum: 288

grid:
  bins: ${bins}
  binSize: 5
  binUnit: minutes
```

- **metadata** must include a stable `id`, `title`, and semantic version. `generator` is always `flowtime-sim`.
- **metadata.narrative** is optional but recommended; it is propagated into run metadata so future readers understand the model context.
- **parameters** expose user-controlled knobs with proper typing and bounds.
- **grid/window** define the evaluation domain. Always specify `binUnit`.

## Classes (optional but recommended for multi-flow models)

- Declare classes under `classes` with stable `id` and a friendly `displayName`.
- When `classes` exist, every arrival entry must include a matching `classId`.
- When `classes` are omitted, templates run in single-class mode with an implicit wildcard (`*`).

Example:
```yaml
classes:
  - id: Order
    displayName: "Order Flow"
  - id: Refund
    displayName: "Refund Flow"

traffic:
  arrivals:
    - nodeId: Ingest
      classId: Order
      pattern:
        kind: constant
        ratePerBin: 20
    - nodeId: Ingest
      classId: Refund
      pattern:
        kind: constant
        ratePerBin: 5
```

## Topology Semantics

Each operational node belongs in `topology.nodes`. Semantics reference the model nodes that produce each time series:

```yaml
topology:
  nodes:
    - id: Delivery
      kind: service
      semantics:
        arrivals: queue_outflow
        served: deliveries_served
        errors: delivery_errors
        attempts: delivery_attempts
        failures: retry_failures
        exhaustedFailures: exhausted_failures
        retryEcho: retry_echo
        retryKernel: [0.0, 0.6, 0.3, 0.1]
        retryBudgetRemaining: retry_budget_remaining
        capacity: delivery_capacity
        processingTimeMsSum: delivery_processing_time_ms_sum
        servedCount: deliveries_served
        maxAttempts: ${maxDeliveryAttempts}
        exhaustedPolicy: "returns"
        aliases:
          attempts: "Delivery attempts"
          failures: "Failed retries"
          retryEcho: "Retry backlog"
```

### ServiceWithBuffer Nodes (Implicit Queue Ownership)

`kind: serviceWithBuffer` nodes now own both the queue/buffer and the service that drains it. When you declare one in `topology.nodes` you no longer create separate backlog helpers—the loader synthesizes the backing queue series for you.

```yaml
topology:
  nodes:
    - id: PickerWave
      kind: serviceWithBuffer
      semantics:
        arrivals: wave_stage_inflow
        served: picker_wave_release
        errors: wave_attrition
        queueDepth: picker_wave_backlog   # alias for the synthesized queue series
        capacity: wave_dispatch_capacity
        parallelism: 4                   # optional; number of concurrent workers
      dispatchSchedule:
        periodBins: ${wavePeriodBins}
        phaseOffset: ${wavePhaseOffset}
        capacitySeries: wave_dispatch_capacity
```

- Set `queueDepth: self` (or omit the field) when you do not need a named alias for outputs. If you do provide a series id (`picker_wave_backlog` above) it becomes the exported queue depth without defining a separate `nodes:` entry.
- `dispatchSchedule` lives directly under the topology node and describes when the queue is allowed to drain. Use the same schema you already use inside nodes (`periodBins`, `phaseOffset`, optional `capacitySeries`).
- The loader rejects legacy backlog helpers. When you migrate older templates, delete the helper nodes and keep only the topology declaration.
- When ServiceWithBuffer nodes release work on a cadence, the UI/CLI surface their queue latency status (see *Queue Latency Semantics* below) so paused gates show up as badges rather than warnings.
- `parallelism` is optional and defaults to `1`. Use a literal number or a series id. Effective capacity uses `capacity * parallelism`.

**Inspector parity expectations (service + ServiceWithBuffer):**
- **Baseline inspector parity:** provide `arrivals`, `served`, `errors`, and `queueDepth` semantics for ServiceWithBuffer nodes.
- **Optional series for richer metrics:** emit `capacity` for utilization and `processingTimeMsSum` + `servedCount` for service time. If you want retry chips, emit `attempts`, `failures`, `retryEcho`, and `retryBudgetRemaining`; otherwise the UI omits retry metrics.
- **Parallelism/instances:** set `parallelism` when concurrent workers/vehicles matter. The UI renders instances and effective capacity when present.
- **Class chips:** per-class series for `arrivals`, `served`, `errors`, and `queueDepth` must exist to render class chips in the inspector. Router targets should point at queue inflow series so class routing is preserved.
- **Backlog age SLA:** requires queue age telemetry (e.g., oldest-age or age distribution). If telemetry cannot provide queue age, the API must mark backlog age SLA as unavailable and the UI must show "No data" (never infer from queue depth alone).
- **Queue depth invariant:** queue depth must satisfy `queueDepth[t] = queueDepth[t-1] + arrivals[t] - served[t] - loss[t]`. When you use dispatch schedules, gate `served` by the schedule (e.g., `capacity * gate`) so the recurrence holds. Violations surface as queue depth mismatch warnings.
- **Backlog health warnings:** the API emits growth/overload/age warnings based on queue depth trends. These are guardrails, not anomalies—avoid false positives by keeping the recurrence valid and providing the series needed for queue age when you want backlog age SLA.

### Series Semantics Metadata (Telemetry)

The engine stamps derived series metadata automatically, but telemetry producers should supply semantics explicitly when exporting a run:

- Use `seriesMetadata` in the time-travel state to declare `aggregation` (`avg`, `sum`, `min`, `max`, `p50`, `p95`, `p99`, `unknown`) and `origin` (`telemetry`, `model`, `derived`, `unknown`).
- Percentile feeds should use explicit series IDs (for example, `flowLatencyP95Ms`) and set `aggregation=p95` so the UI does not imply an average.
- If telemetry is already aggregated, set `origin=telemetry` to distinguish it from engine-derived averages.

### Queue & DLQ Nodes

`kind: queue` (visual shells) and `kind: dlq` (terminal buffers) share the same implicit DSL:

- Set `semantics.queueDepth: self` to let the loader synthesize the queue/buffer series automatically. You only provide the inflow/outflow/loss series; no helper nodes are required under `nodes:`.
- When you want a stable alias for CSV exports, set `queueDepth: some_alias`. The loader (`QueueNodeSynthesizer`) creates a ServiceWithBuffer node named `some_alias` so your outputs can keep existing filenames without hand-authoring the backing node.
- DLQs behave just like queues from a modeling standpoint: emit arrivals + loss/attrition series, and use `queueDepth: self` (or a named alias) to expose backlog chips/charts.
- Avoid mixing implicit and explicit queue implementations—the loader ignores explicit helper nodes when an alias already exists, so removing the helper is the supported path.

### Retry Governance Fields

TT‑M‑03.32 introduced three retry governance fields. When a service node supports retries:

- **`maxAttempts`** (literal or parameter) declares the policy. Use it whenever you emit `retryBudgetRemaining`.
- **`exhaustedFailures`** references the series capturing work that exceeded the budget. Connect this to a downstream DLQ/escalation node via a `terminal` edge:

  ```yaml
  edges:
    - id: delivery_to_returns
      from: Delivery:exhaustedFailures
      to: Returns:arrivals
      type: terminal
      measure: exhaustedFailures
  ```

- **`retryBudgetRemaining`** aggregates the unused attempt budget per bin. Example:

  ```yaml
  - id: retry_budget_capacity
    kind: expr
    expr: "delivery_base_load * ${maxDeliveryAttempts}"

  - id: retry_budget_remaining
    kind: expr
    expr: "MAX(0, retry_budget_capacity - delivery_attempts)"
  ```

Templates that declare `maxAttempts` **must** provide both `exhaustedFailures` and `retryBudgetRemaining` so analyzers and the UI stay consistent.

### Transit Delays (No Edge Lag)

Edges must remain pure connectivity. If you need to model a delay between two nodes (travel time, batching, handoff, etc.), insert an explicit **transit service** node between them:

- Use `kind: service` with `arrivals`, `served`, `processingTimeMsSum`, and `servedCount`.
- Do **not** add a queue unless the domain requires one.
- Keep edge definitions lag-free; time shifts belong in the transit node’s service time series.

## Refreshing the Template Cache

FlowTime-Sim and FlowTime.API cache template headers/metadata after the first request so listing and generation stay fast. When you edit YAML locally (or deploy updated templates to a shared instance) you no longer need to restart services to pick up the changes:

- **CLI:** run `dotnet run --project src/FlowTime.Sim.Cli -- refresh templates --templates-dir templates`. The command clears the cache, reloads all `*.yaml` under the specified directory, and prints the number of templates reloaded.
- **FlowTime.API / UI:** the Time-Travel “Run Model” page now includes a *Refresh templates* button. It calls `POST /v1/templates/refresh` on FlowTime.API, which clears the API’s cache, reloads the YAML on disk, and then re-queries FlowTime-Sim for the latest metadata. Use this before regenerating runs after a template update.
- **Sim API:** `POST /api/v1/templates/refresh` is available when running FlowTime-Sim as a standalone service (the same endpoint the CLI calls internally). Include it in operational runbooks so deployed environments can reload templates without downtime.

The refresh endpoints only flush the cache—they never delete or modify templates. If the reload fails, the previous cache remains empty, so the next list/generate call will surface the underlying parsing/validation error. Always rerun `flow-sim list templates` or the UI refresh button after editing YAML to make sure the analyzer + schema changes landed correctly.

### Terminal Nodes

Whenever work becomes unrecoverable, model an explicit destination (queue, DLQ service, manual triage). Provide aliases for arrivals/served/queue metrics so the UI surfaces domain-specific labels.

DLQs are now a first-class node kind:

```yaml
  - id: RejectedDLQ
    kind: dlq
    semantics:
      arrivals: dlq_inflow
      served: dlq_release
      errors: dlq_losses
      queueDepth: dlq_depth
      aliases:
        queue: "DLQ backlog"
```

- All inbound/outbound topology edges referencing a DLQ must be `type: terminal`. This keeps the analyzer happy and prevents DLQs from rejoining the primary throughput graph accidentally.
- DLQs behave like queues for telemetry (`queueDepth`, latency), but they never emit SLA colors—UI renders them with the dedicated DLQ badge instead of relying on aliases.
- Model DLQs as service-with-buffer nodes configured as pure backlogs: set `served` to a zero-valued series, route any cleanup/purge logic through the `loss` field, and point `errors` at whatever attrition metric you want surfaced in the UI. This matches real terminal buffers (everything accumulates unless you explicitly purge) and keeps analyzers from flagging “served exceeds arrivals.”
- Because `served == 0`, queue-latency analyzers emit informational warnings (“latency could not be computed”). This is expected and indicates the DLQ is behaving as a terminal sink rather than an operational queue.

### Sink Kind (Success Terminal)

Some systems end in a successful terminal (e.g., passengers arriving at an airport, packages delivered, customer receipt). These nodes should not surface misleading utilization or error-rate signals unless you explicitly emit them. Mark such nodes with a sink role:

```yaml
topology:
  nodes:
    - id: Airport
      kind: sink
      semantics:
        arrivals: airport_arrivals
        errors: airport_errors
```

- `kind: sink` is a real node kind. The engine derives `served = arrivals` if omitted and defaults `errors` to zero.
- Queue/capacity/retry semantics are not allowed for sinks.
- The UI renders a "Terminal" badge for sink nodes and suppresses utilization/error-rate chips unless the corresponding series are explicitly provided.

### Router Nodes

Use `kind: router` when a single upstream queue feeds multiple downstream legs and you need to preserve class-level metrics without sprinkling percentage math across expressions. Routers behave like lightweight services: they accept a queue input, emit routed totals, and produce diagnostic series (`arrivals`, `served`, `errors`, `capacity`, `processingTimeMsSum`, `servedCount`).

Authoring guidance:

1. **Point routes at the true demand nodes.** Router targets should be the series that downstream queues/services already reference in their semantics (e.g., `airport_dispatch_queue_demand`, `restock_inflow`). This keeps the topology unchanged while letting the router overwrite those series with real routed totals.
2. **Zero-out downstream expressions.** Set each router target node’s `expr` to `"0"` (or remove the expression entirely if the loader synthesizes it). Routers populate the target series via evaluation overrides, so no manual `%` math remains in the template.
3. **Class routes first, weights second.** Provide `classes: [...]` for routes that siphon specific classes; omit `classes` and use `weight` when a route should consume the remaining traffic proportionally. Weights are normalized automatically.
4. **Hide scaffolding nodes.** Any helper expressions that only exist to surface router metadata can be tagged with `metadata.graph.hidden: "true"` so CLI/API callers don’t see plumbing artifacts when expanding the graph.

Example:

```yaml
- id: HubDispatchRouter
  kind: router
  inputs:
    queue: hub_dispatch
  routes:
    - target: airport_dispatch_queue_demand
      classes: [Airport]
    - target: downtown_dispatch_queue_demand
      classes: [Downtown]
    - target: industrial_dispatch_queue_demand
      classes: [Industrial]

- id: airport_dispatch_queue_demand
  kind: expr
  expr: "0"        # router overrides populate this series
```

The analyzer treats router-generated series as authoritative, so downstream nodes automatically satisfy class-conservation checks (e.g., `router_class_leakage`). When you remove percentage expressions, rerun `flow-sim generate` followed by `flowtime run` to refresh canonical artifacts and ensure warnings disappear.

```yaml
  - id: HubDispatchRouter
    kind: router
    semantics:
      arrivals: hub_dispatch_router_arrivals
      served: hub_dispatch_router_served
      capacity: hub_dispatch_capacity
    inputs:
      queue: hub_dispatch
    routes:
      - target: airport_dispatch_queue_demand
        classes: [Airport]
      - target: downtown_dispatch_queue_demand
        classes: [Downtown]
      - target: industrial_dispatch_queue_demand
        weight: 1    # catch-all (unnamed classes split by weight)
```

- **`inputs.queue`** references the node that supplies arrivals. The router duplicates that series for downstream consumption, so no additional expression nodes are required for per-route splits.
- **Routes with `classes`** capture 100% of those classes; they do not need weights. **Routes without `classes`** split the remaining classes proportionally using `weight`.
- Always provide router semantics (`arrivals`, `served`, `capacity`, etc.) so the UI can surface node-level metrics and analyzers can validate conservation.
- Analyzer expectations:
  - Every router must route all classes — either via explicit `classes` entries or through weighted fallbacks. If a class never finds a route, the CLI emits `router_missing_class_route`.
  - Conservation must hold per bin (`router_class_leakage`). When you see this warning, confirm each downstream target consumes the exact series exported by the router.
- Router nodes are optional, but they eliminate fragile expressions like `hub_dispatch * 0.3` and keep class chips aligned with topology flows.

### Scheduled Dispatch ServiceWithBuffer Nodes

Cadence-driven queues (picker waves, shuttle departures, batch jobs) are now authored entirely from the topology node—no helper backlog nodes are required. Declare the ServiceWithBuffer node plus its schedule and the loader will synthesize the queue depth series automatically:

```yaml
topology:
  nodes:
    - id: PickerWave
      kind: serviceWithBuffer
      semantics:
        arrivals: wave_stage_inflow
        served: picker_wave_release
        errors: wave_attrition
        queueDepth: picker_wave_backlog
        capacity: wave_dispatch_capacity
        aliases:
          queue: "Staged backlog"
      initialCondition:
        queueDepth: 0
      dispatchSchedule:
        periodBins: ${wavePeriodBins}
        phaseOffset: ${wavePhaseOffset}
        capacitySeries: wave_dispatch_capacity
```

- `periodBins` is required and represents the number of bins between dispatch events.
- `phaseOffset` shifts the first dispatch bin. The engine normalizes negative offsets modulo `periodBins`, so you can align departures with a specific clock tick.
- `capacitySeries` (optional) caps the per-dispatch volume. Reference an existing capacity node (e.g., `cap_airport`) or a dedicated expression. When omitted, releases equal whatever backlog exists on dispatch bins.
- Analyzer warnings:
  - `dispatch_capacity_missing` when `capacitySeries` references an undefined node.
  - `dispatch_missing_served_series` if the node omits `served`.
  - `dispatch_never_releases` when arrivals exist but the cadence never fires. This usually means `phaseOffset` and `periodBins` don’t cover the evaluated window.
- FlowTime-Sim CLI prints a summary of every `dispatchSchedule` when you run `flowtime-sim generate --verbose ...`. Use it to confirm cadence metadata without spelunking through YAML.
- When a dispatch gate is closed while backlog remains, the engine exposes `queueLatencyStatus: paused_gate_closed`. The UI renders a “Paused” badge and CLI output explains that latency is undefined because the schedule is holding work. You no longer see the generic “latency could not be computed” warning for these cases.

### Queue Latency Semantics

Queue latency is now accompanied by a status descriptor so operators know *why* the metric may be missing. Every `/state` and `/state_window` payload includes `metrics.queueLatencyStatus` for queue-like nodes:

- `null` — latency is fully computable (standard steady-state queues).
- `paused_gate_closed` — backlog exists but the dispatch gate is closed. The UI renders a “Paused (gate closed)” badge and CLI output suppresses the older generic warning. Latency remains `null` because no work left the queue during those bins.

Authoring guidance:

- Ensure ServiceWithBuffer nodes always declare `served` plus the relevant dispatch schedule. The loader/engine will handle status calculation; templates do **not** emit distinct warning series.
- When you expect paused status (e.g., nightly batches), mention it in the template README so consumers know the behavior is intentional.

## Computational Nodes and Outputs

- Keep computational nodes (`const`, `expr`, `pmf`, `serviceWithBuffer`, `router`) in the `nodes:` section outside topology.
- Every semantic reference must point to a node defined here.
- Add outputs for the time series you want to export (CSV) or inspect:

  ```yaml
  outputs:
    - series: deliveries_served
      as: deliveries.csv
    - series: exhausted_failures
      as: exhausted_failures.csv
    - series: retry_budget_remaining
      as: retry_budget_remaining.csv
  ```

- Expressions support arithmetic plus helper functions: `SHIFT`, `CONV`, `MIN`, `MAX`, `CLAMP`, `MOD`, `FLOOR`, `CEIL`, `ROUND`, `STEP`, and `PULSE`. Use `STEP(value, threshold)` to produce a 0/1 gating signal and `PULSE(periodBins, phaseOffset?, amplitude?)` when modeling cadence-driven dispatch (e.g., bus departures). `MOD`/`FLOOR`/`CEIL`/`ROUND` mirror their mathematical counterparts and can be combined with `PULSE`/`STEP` to build bursty patterns without precomputed arrays.

## Analyzer Expectations

The invariant analyzers (engine and sim) enforce:

- Non-negative arrivals/served/errors/attempts/failures/exhausted/budget series.
- `served <= arrivals`, `failures <= attempts`, `exhaustedFailures <= failures`.
- Queue/backlog conservation.
- Presence of `exhaustedFailures` and `retryBudgetRemaining` whenever `maxAttempts` is declared.

Violations surface as warnings in CLI output and will prevent templates from passing CI.

## Recommended Workflow

1. Edit the template YAML under `templates/`.
2. Generate a model to validate invariants:

   ```bash
   dotnet run --project src/FlowTime.Sim.Cli -- generate \
     --id supply-chain-multi-tier \
     --templates-dir templates \
     --out /tmp/supply-chain.yaml
   ```

   The CLI prints analyzer warnings (if any) immediately after generation.

3. Update fixtures (`fixtures/<template-id>/…`) so deterministic tests cover the new metrics.
4. Run `dotnet test FlowTime.sln` before opening a PR.

Documenting these steps keeps retries, terminal edges, and telemetry contracts consistent across all domains.
