# Backpressure Pattern

FlowTime supports two complementary mechanisms for modeling bounded queues:

1. **WIP Limits** — engine-enforced push with overflow routing
2. **SHIFT-based backpressure** — model-expressed pull with upstream throttling

## WIP Limits (Push + Overflow)

A `wipLimit` on a `serviceWithBuffer` topology node caps the queue depth per bin. Excess items overflow to a configurable target:

- `wipOverflow: "loss"` (default) — overflow items are tracked but removed from the system.
- `wipOverflow: "<nodeId>"` — overflow is routed as additional inflow to the target queue node.

Cascading is supported: the target can itself have a `wipLimit` and overflow to another node. The compiler validates that the overflow routing graph is acyclic.

### When to use

- Kanban WIP caps, connection pool limits, message queue max depth
- Scenarios where excess work is diverted to a dead-letter queue or secondary path
- Time-varying limits (via series reference) for scheduled capacity changes

### Example

```yaml
topology:
  nodes:
    - id: MainQueue
      kind: serviceWithBuffer
      wipLimit: 100
      wipOverflow: DLQ
      semantics:
        arrivals: incoming_requests
        served: processed
        errors: errors
        queueDepth: main_queue_depth

    - id: DLQ
      kind: serviceWithBuffer
      semantics:
        arrivals: dlq_base_arrivals
        served: dlq_processed
        errors: dlq_errors
        queueDepth: dlq_queue_depth
```

When `main_queue_depth` exceeds 100, the overflow becomes additional inflow to the DLQ.

## SHIFT-Based Backpressure (Pull + Throttle)

Backpressure throttles upstream production based on downstream queue state. FlowTime expresses this using the `SHIFT` function, which reads a series value from a previous time bin (t-1), introducing a propagation delay that models real-world feedback latency.

### Pattern

```
effective_arrivals = raw_arrivals * (1 - SHIFT(pressure, 1))
queue_depth        = serviceWithBuffer(effective_arrivals, served)
pressure           = CLAMP(queue_depth / max_queue, 0, 1)
```

- `pressure` is derived from the downstream queue state: 0 when empty, 1 when full.
- `SHIFT(pressure, 1)` reads the previous bin's pressure, creating a one-bin feedback delay.
- At t=0, SHIFT returns 0 (no prior data), so full upstream arrives — modeling ramp-up.
- As the queue fills, pressure rises, throttling upstream arrivals. The system converges to equilibrium.

### How it works

The dependency chain `effective_arrivals → queue_depth → pressure → (SHIFT) → effective_arrivals` forms a cycle. The engine handles this via **feedback subgraph evaluation**:

1. The expression compiler classifies `SHIFT(pressure, 1)` as a *lagged* reference (not a same-bin dependency).
2. The graph's topological sort uses only same-bin edges — no cycle detected.
3. Nodes in the feedback loop are evaluated **bin-by-bin**: at each bin t, SHIFT reads pressure[t-1] which was already computed in the previous bin iteration.
4. Non-feedback nodes are evaluated series-at-a-time as usual.

This is structurally identical to how `QueueRecurrence` works in the matrix evaluation model — both are sequential operations reading from previous bins of shared mutable state.

### Constraint

Every back-edge in a feedback loop must go through `SHIFT(series, lag)` with `lag >= 1`. Same-bin algebraic cycles (without SHIFT) are rejected — they represent unsolvable circular dependencies. The SHIFT lag models real-world propagation latency.

### When to use

- TCP-style flow control where senders slow based on receiver queue depth
- Kafka consumer lag management where production rate adapts to consumer backlog
- Kanban pull systems where upstream work starts only when downstream capacity opens
- Circuit breaker patterns where failure rate throttles incoming requests
- Any system where downstream state feeds back to upstream behavior with a time delay

### Example: cross-node feedback

```yaml
nodes:
  - id: raw_arrivals
    kind: const
    values: [100, 100, 100, 100, 100, 100, 100, 100]

  - id: served_capacity
    kind: const
    values: [20, 20, 20, 20, 20, 20, 20, 20]

  # Upstream throttles based on PREVIOUS bin's queue pressure
  - id: effective_arrivals
    expr: "raw_arrivals * (1 - SHIFT(pressure, 1))"

  - id: downstream_served
    expr: "served_capacity"

  # Pressure signal: how full is the downstream queue (0..1)
  - id: pressure
    expr: "CLAMP(queue_depth / 50, 0, 1)"

topology:
  nodes:
    - id: Downstream
      kind: serviceWithBuffer
      initialCondition:
        queueDepth: 0
      semantics:
        arrivals: effective_arrivals
        served: downstream_served
        queueDepth: queue_depth
```

At t=0, pressure is 0 → full arrivals (100), queue jumps to 80, pressure saturates at 1.0. At t=1-2, pressure=1 → arrivals throttled to 0, queue drains by 20/bin. At t=3, pressure drops to 0.8 → arrivals recover to 20, matching outflow. Queue stabilizes at 40.

### Example: signal-driven throttling (no feedback)

When the pressure signal is independent (not derived from the queue), no feedback cycle exists. This models external signals like monitoring data or SLA triggers:

```yaml
nodes:
  - id: raw_arrivals
    kind: const
    values: [100, 100, 100, 100, 100, 100]

  - id: served_capacity
    kind: const
    values: [20, 20, 20, 20, 20, 20]

  # Downstream health signal: 1.0=healthy, degrades over time
  - id: capacity_signal
    kind: const
    values: [1.0, 1.0, 0.5, 0.2, 0.2, 0.2]

  # Upstream reads PREVIOUS bin's signal — one-bin propagation delay
  - id: effective_arrivals
    expr: "raw_arrivals * SHIFT(capacity_signal, 1)"

  - id: downstream_served
    expr: "served_capacity"

topology:
  nodes:
    - id: Downstream
      kind: serviceWithBuffer
      semantics:
        arrivals: effective_arrivals
        served: downstream_served
        queueDepth: queue_depth
```

At t=0, SHIFT returns 0 (no prior data), so no arrivals. At t=1, the signal reads 1.0 from t=0, allowing full arrivals. At t=3, the signal reads 0.5 from t=2, halving effective arrivals. The queue grows more slowly as the signal degrades.

### Key constraint

`SHIFT(series, lag)` with `lag >= 1` is required. The one-bin delay models real-world propagation latency — backpressure signals are never instantaneous. The series referenced by SHIFT must be independent of the downstream queue to avoid a topological cycle.

## Choosing Between the Two

| Concern | WIP Limit | SHIFT Backpressure |
|---------|-----------|-------------------|
| Enforcement | Engine-automatic | Model-expressed |
| Overflow handling | Configurable (loss, DLQ, cascade) | No overflow — upstream slows |
| Feedback delay | Instant (same bin) | One bin lag minimum |
| Signal source | Queue depth (implicit) | Queue-derived or external (explicit) |
| Feedback loops | No (push only) | Yes (via bin-by-bin evaluation) |
| Complexity | Declare on topology node | Write expression formulas |
| Best for | Hard capacity limits | Adaptive flow control |

Both can be combined: a WIP limit catches burst overflow while SHIFT-based backpressure provides steady-state flow control.
