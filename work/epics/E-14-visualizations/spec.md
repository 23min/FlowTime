# Epic: Visualizations (Chart Gallery / Demo Lab)

**ID:** E-14

## Intent

Provide a dedicated UI page where we can prototype role-focused charts (exec, SRE, support) using FlowTime-derived metrics, and clearly show which insights come from FlowTime output versus raw telemetry.

## Goals

- Create a single "Visualization Lab" page with chart panels grouped by role.
- Support horizon/stacked charts for volume, queue depth, SLA, latency, retry volume, and utilization.
- Use bespoke SVG or canvas charts (no charting library dependencies) for full control.
- Allow quick toggles for flow/class selection and time window.
- Contrast FlowTime-derived views with raw telemetry views where available.
- Keep chart definitions and data wiring deterministic and repeatable for demos.
- Keep charting code isolated from other UI modules and fed only by API output (to validate the engine semantics surface).

## Non-Goals

- No new analytics pipeline; reuse existing engine and API outputs.
- No third-party charting library dependencies.

## Data Sources

- `/state_window` for per-bin node and edge metrics.
- Edge metrics for flow/attempt/retry volumes.
- Warnings/quality flags for contextual overlays.
- Raw telemetry comparisons only after Telemetry Ingestion is available.

## UI Concept (v1)

- Role tabs (Exec, SRE, Support) each with curated chart bundles.
- A simple control strip: window, flow/class, metric set.
- Chart cards with consistent titles, units, and provenance text.

## Milestones

- To be defined when the epic is scheduled.
