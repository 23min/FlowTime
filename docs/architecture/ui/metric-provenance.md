# Metric Provenance (UI)

This document defines how FlowTime surfaces **metric provenance** in the UI so modelers can understand *where values come from* and how they are computed.

## Goals

- Make metric calculations auditable without leaving the UI.
- Show **formulas**, **inputs**, **units**, and **gating rules** (schedules, availability).
- Keep the UI concise by default, with detail on demand.
- Avoid inventing new metrics; clarify existing ones.

## UX Principles

1. **Tooltips per metric.** Inspector property rows and chart labels use tooltips to reveal how the value is computed.
2. **Human-readable formulas.** Use canonical series IDs (for example, `served`, `capacity`, `sla:completion`).
3. **Units everywhere.** Provenance details always show the metric unit (percent, ms, min, count).
4. **Gating rules are explicit.** If a metric is schedule-gated or unavailable, the UI must say so.
5. **Missing inputs are visible.** If required series are absent, the provenance view lists missing inputs.

## Inspector Behavior

For each inspector metric (properties or charts):

- **Default row:** chart sparkline and horizon overview.
- **Provenance tooltip:**
  - **Formula:** e.g., `utilization = served / capacity`
  - **Meaning:** short definition of what the metric represents
  - **Inputs:** e.g., `served`, `capacity`, `sla:completion`
  - **Units:** e.g., `percent`, `ms`, `min`, `count`
  - **Gating:** e.g., `Schedule adherence uses dispatch schedule bins only.`
  - **Missing:** list of missing series when inputs are unavailable

### Focus Chips and Mini-Sparklines

- **SLA focus** uses the `successRate` series for the mini-sparkline and the current-bin value to avoid mismatched labels.
- **Arrivals focus** (when selected) uses the `arrivals` series and shows volume coloring for the node mini-sparklines.
- **Router nodes** always show an Arrivals chart in the inspector; if the series is missing it renders a placeholder so Served/Arrivals stay aligned.

### Validation Loop

- **Automated parity check:** UI tests validate that inspector values match the `/state_window` series for the selected bin (success rate, error rate, arrivals/served, utilization, service/flow latency).
- **Manual cross-check:** use the bin dump + provenance popovers to confirm the exact series key and formula used for the displayed value.

### Series Semantics Metadata

When `/state` or `/state_window` provides `seriesMetadata`, the tooltip must include the aggregation semantics (for example, `Aggregation: avg` or `Aggregation: p95`) so telemetry-driven percentiles are explicit.

## Latency Semantics

Latency is frequently misunderstood, so the UI must clarify:

- **Queue latency (minutes):** sourced from `latencyMinutes` when available. When derived by the engine it is a per-bin average wait time.
- **Service time (ms):** derived from `processingTimeMsSum / servedCount` (per-bin average).
- **Flow latency (ms):** sourced from `flowLatencyMs` when available; when derived it is a per-bin average end-to-end latency.
- **Service latency (minutes):** for `kind=service`, `latencyMinutes` is telemetry- or template-defined (not engine-derived) when present.
- **Total time in system:** not shown as a standalone metric; provenance clarifies queue vs service time.

The provenance view must explicitly show these relationships for queue-like nodes.

If telemetry provides percentiles (p95/p99), the series name and meaning text must reflect the percentile (for example, `flowLatencyP95Ms` with meaning "P95 flow latency"), rather than overloading the generic latency description.

## Capacity + Parallelism Semantics

Capacity metrics also require explicit provenance:

- **Instances (parallelism):** number of concurrent workers/instances for a node. Sourced from the `parallelism` series or a scalar template value.
- **Effective capacity:** derived as `capacity × parallelism` when both are available. Used for utilization and overload checks.
- **Base capacity:** the raw `capacity` series or template value before parallelism is applied.

The provenance view must show the formula for effective capacity and the inputs used.

## Bin Dump Provenance

The bin dump is extended to include:

- **Value snapshot** for the selected bin.
- **Provenance bundle** with the node-kind catalog slice used for evaluation.
- **Available series** and **missing inputs** for each metric definition.

### UX Mode

- Default: download JSON file.
- ALT/CTRL/META: open a new browser tab with the JSON payload.

## Non-Goals

- No new metrics or alternative models.
- No auto-anomaly detection or alerting.
- No graph layout changes (see follow-up milestone for focus view).
