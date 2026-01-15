# Sink Node Architecture (Terminal Success)

## Purpose

Sink nodes model **terminal success**: work has exited the modeled system successfully (delivered, arrived, accepted).
They are intentionally simpler than services and should not be treated as capacity- or queue-owning nodes.

## Core Semantics

- **Terminal success only**: a sink is the last node in a path.
- **served = arrivals** by definition.
- **errors = 0** by definition (unless an explicit refusal series is provided by telemetry; see below).
- **No queue/capacity**: sinks do not own queue depth, capacity limits, or retry semantics.

## Supported Metrics (Subset of Service)

Sinks may display the following when telemetry or modeled series exist:

- **Arrivals / Served** (same series; rendered as "Arrived" or "Completed").
- **Completion SLA** (based on served/arrivals).
- **Flow latency** (end-to-end or terminal segment), if provided.
- **Service time** at destination, if provided.
- **Schedule adherence**, if a schedule exists for arrivals.
- **Class mix** and class contributions (if class coverage exists).

These are terminal-friendly metrics and do not imply queue/throughput semantics.

## Optional Refused Arrivals (Terminal Rejections)

Some domains produce a terminal "refusal" or "rejected at destination" signal:

- This is **not** a retry/failure within the service chain.
- It should be modeled as a **distinct refused-arrivals series**.
- If present, it should appear as an explicit terminal metric, not as queue failures or retries.

This allows accurate reporting without turning the sink into a service node.

## Focus Chip Behavior (UI)

Sinks should respond to focus chips for **relevant** metrics:

- **SLA**: allowed
- **Error rate**: allowed (only if refused-arrivals series is provided)
- **Flow latency / Service time**: allowed when series exists
- **Queue / Utilization**: never displayed for sinks

This keeps sink behavior consistent with terminal semantics while still useful in the UI.

## Telemetry Mapping

- If telemetry emits explicit completion/delivery events, map them directly to the sink `arrivals/served` series.
- If telemetry does **not** provide completion events, do **not** infer a sink; treat the last service as a leaf node.
- If telemetry does not provide queue age distribution, backlog SLA is **unavailable** (do not invent it).

## Modeling Guidance

Use a sink when:

- A destination is conceptually distinct from the service path.
- You want completion SLA to be anchored at the destination, not the last service.
- You want to render terminal status without misusing queue/capacity fields.

Do **not** use a sink when:

- The node has its own queue, capacity, or retries.
- The node represents active processing rather than terminal acceptance.

## Related Documents

- `docs/architecture/service-with-buffer/service-with-buffer-architecture-part2.md`
- `docs/architecture/service-with-buffer/service-with-buffer-architecture.md`
