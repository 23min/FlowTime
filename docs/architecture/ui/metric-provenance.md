# Metric Provenance (UI)

This document defines how FlowTime surfaces **metric provenance** in the UI so modelers can understand *where values come from* and how they are computed.

## Goals

- Make metric calculations auditable without leaving the UI.
- Show **formulas**, **inputs**, **units**, and **gating rules** (schedules, availability).
- Keep the UI concise by default, with detail on demand.
- Avoid inventing new metrics; clarify existing ones.

## UX Principles

1. **Expandable detail per metric.** Each metric row can expand to reveal how the value is computed.
2. **Human-readable formulas.** Use the same names a modeler sees in templates (series IDs and aliases).
3. **Units everywhere.** Values and formulas must always specify units (%, ms, min).
4. **Gating rules are explicit.** If a metric only updates on schedule bins or is unavailable, the UI must say so.
5. **No hidden state.** If a metric is derived from a time window or last-event carry-forward logic, disclose it.

## Inspector Behavior

For each metric displayed in the inspector:

- **Default row:** value, unit, and status (ok / unavailable / no events).
- **Expanded provenance:**
  - **Formula:** e.g., `utilization = served / capacity`
  - **Inputs:** e.g., `served_airport`, `cap_airport`
  - **Gating:** e.g., `updated only on dispatch bins`
  - **Units:** e.g., `served (count/bin)`, `capacity (count/bin)`
  - **Notes:** e.g., "value carries forward when no events"

## Latency Semantics

Latency is frequently misunderstood, so the UI must clarify:

- **Queue latency (minutes):** derived from backlog depth and throughput; indicates waiting time in queue.
- **Service time (ms):** per-unit processing time derived from `processingTimeMsSum / servedCount`.
- **Total time in system (minutes):** `queue latency + service time` (converted to minutes).
- **Flow latency (ms):** end-to-end time across multiple nodes (when present).

The provenance view must explicitly show these relationships for queue-like nodes.

## Bin Dump Provenance

The bin dump is extended to include:

- **Value snapshot** for the selected bin.
- **Provenance block** containing formulas, inputs, and gating rules.
- **Units** for all values.

### UX Mode

- Default: download JSON file.
- ALT/CTRL: open a new browser tab with a readable JSON view.

## Non-Goals

- No new metrics or alternative models.
- No auto-anomaly detection or alerting.
- No graph layout changes (see follow-up milestone for focus view).

