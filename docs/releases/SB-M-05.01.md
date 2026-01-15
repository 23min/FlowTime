# Release SB-M-05.01 — ServiceWithBuffer Node Type

**Release Date:** 2025-11-28  
**Type:** Milestone delivery (breaking schema change)  
**Key Fixtures:** `templates/warehouse-picker-waves.yaml`, `templates/transportation-basic-classes.yaml`

## Overview

SB-M-05.01 makes `kind: serviceWithBuffer` the canonical way to model services that own explicit queues. The schema, engine, analyzers, CLI, API, and UI all now agree on this node type, and the publicly documented `kind: backlog` spelling is retired. Canonical templates (transportation, warehouse picker waves, etc.) were migrated to the new kind, keeping their numerical behavior identical while surfacing proper backlog badges, chips, and schedule metadata in the topology view.

## Key Changes

1. **Schema & Engine**
   - `docs/schemas/model.schema.(yaml|json)` defines `serviceWithBuffer` with queue depth, loss, and schedule semantics; `backlog` is removed from the public contract.
   - Template loader, `SimModelBuilder`, and DTOs resolve the new kind into the former backlog execution path so existing runs remain numerically stable.
   - Schema and loader tests ensure the new shape validates and legacy inputs are rejected early.
2. **Docs & Templates**
   - Architecture and authoring docs now reference `docs/service-with-buffer/service-with-buffer-architecture.md`, explain schedule metadata, and remove backlog terminology.
   - `templates/warehouse-picker-waves.yaml`, `templates/transportation-basic-classes.yaml`, and supporting examples now emit ServiceWithBuffer nodes with proper queueDepth/dispatch series.
3. **Analyzer, CLI & UI**
   - Analyzer wording uses “service with buffer”; warnings about served>arrivals are suppressed for gated services.
   - `flow-sim generate --verbose` describes buffers, capacity, and dispatch cadence using the new terminology.
   - API `/graph` and `/state_window` surface `nodeLogicalType: "serviceWithBuffer"` plus dispatch metadata; the UI renders these nodes like services with queue badges, backlog trapezoid chips, and schedule panels, and the operational filter treats them as first-class services.

## Tests

- `dotnet build`
- `dotnet test --nologo` *(includes all suites; perf benchmark tests remain intentionally skipped as part of the standing perf sweep deferral)*

## Known Issues / Follow-ups

- ServiceWithBuffer nodes still require helper queueDepth series in templates; SB-M-05.02 will allow implicit declarations so modelers do not author hidden helpers.
- Queue latency remains `null` when dispatch schedules hold work (served=0, depth>0); SB-M-05.02 will add explicit “paused” semantics in analyzers, API, and UI.
- Warehouse picker example still emits informational analyzer warnings about bursty dispatch; this is tracked in SB-M-05.02/related backlog improvements.

## Verification Artifacts

- `templates/warehouse-picker-waves.yaml` (ServiceWithBuffer example with scheduled dispatch)
- `templates/transportation-basic-classes.yaml` (class-enabled transport example migrated to ServiceWithBuffer)
- Manual verification via `dotnet run --project src/FlowTime.Sim.Cli -- generate --id warehouse-picker-waves` and `--id transportation-basic-classes`
