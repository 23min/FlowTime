# Release CL-M-04.03.02 — Scheduled Dispatch & Template Refresh

**Release Date:** 2025-12-01  
**Type:** Milestone delivery (no version bump)  
**Canonical CLI Artifacts:** `data/transportation-basic-classes-model.yaml`, `data/warehouse-picker-waves-model.yaml`

## Overview

CL-M-04.03.02 builds on the router/class work by making cadence-driven flows first-class citizens and closing the operational loop for template authors:

- Expression engine now supports MOD/FLOOR/CEIL/ROUND/STEP/PULSE so scheduled dispatch math stays inside YAML rather than external spreadsheets.
- Backlog nodes accept `dispatchSchedule` metadata, analyzers validate cadence correctness, and API+UI surfaces expose schedule chips/inspector details.
- Transportation-basic and the new warehouse-picker template highlight scheduled dispatch with regenerated analyzer artifacts.
- Operators can refresh template caches without restarts via the new `flow-sim refresh templates` command, FlowTime.API endpoint, and Run-page button.

## Key Changes

1. **Expression & Schema Updates**
   - `FlowTime.Expressions`, `ClassContributionBuilder`, and schema docs gained MOD/FLOOR/CEIL/ROUND/STEP/PULSE plus structured parameter extraction.
   - Scheduled dispatch schema snippets documented in `docs/templates/template-authoring.md`, and new tests (`ExpressionIntegrationTests`) lock coverage.

2. **Backlog Dispatch Semantics**
   - `dispatchSchedule` support threads through Sim model parsing, engine backlog execution, invariant analyzer, and CLI warnings.
   - `/v1/runs/{id}/graph` & `/v1/runs/{id}/state_window` responses now include dispatch metadata so UI chips can badge scheduled nodes.

3. **Template & UI Integration**
   - `templates/transportation-basic-classes.yaml` and `templates/warehouse-picker-waves.yaml` use scheduled backlogs; docs + README updated accordingly.
   - UI topology inspector shows schedule info, chips, and backlog nodes appear in full DAG mode for debugging.

4. **Template Cache Refresh Workflow**
   - `flow-sim refresh templates` clears the CLI cache and reloads YAML; `POST /v1/templates/refresh` (FlowTime.API) and `/api/v1/templates/refresh` (FlowTime-Sim) expose the same capability for services.
   - Time-Travel Run page includes a *Refresh templates* button tied to the API endpoint; doc updates explain when to use it.

## Tests

- `dotnet build`
- `dotnet test --nologo` *(perf benchmark skips remain expected)*
- Targeted suites:
  - `dotnet test --filter ExpressionIntegrationTests --nologo`
  - `dotnet test --filter ScheduledDispatchTests --nologo`
  - `dotnet test --filter TemplateInvariantAnalyzerTests --nologo`
  - `dotnet test --filter GraphServiceTests --nologo`
  - `dotnet test --filter WarehousePickerTemplate_DoesNotEmitServedExceedsArrivalsWarnings --nologo`
  - `dotnet test --filter TemplateEndpointsTests --nologo`
- CLI regeneration:
  - `dotnet run --project src/FlowTime.Sim.Cli -- generate --id transportation-basic-classes --out data/transportation-basic-classes-model.yaml`
  - `dotnet run --project src/FlowTime.Sim.Cli -- generate --id warehouse-picker-waves --out data/warehouse-picker-waves-model.yaml`

## Known Issues / Follow-ups

- Transportation model still emits a router class conservation warning for some bins; tracked for CL-M-04.04 along with the class coverage telemetry improvements.
- Warehouse picker queue still logs informational warnings (`processing_time_not_available`, `served_exceeds_arrivals`) when bursts deplete backlog faster than inflow. SB-M-01 (service-with-buffer) will address this by promoting backlog semantics to a first-class node type.
- Perf benchmark skips are deferred to the epic-wide perf sweep.

## Verification Artifacts

- `data/transportation-basic-classes-model.yaml` — Analyzer output captures router warnings and cadence metadata for the transportation template.
- `data/warehouse-picker-waves-model.yaml` — Confirms scheduled picker/backlog behavior and documents residual analyzer warnings for the service-with-buffer follow-up.
