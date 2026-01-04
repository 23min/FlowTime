# Release FT-M-05.08 — ServiceWithBuffer Inspector Consistency and Class Coverage

**Release Date:** 2026-01-04  
**Type:** Milestone delivery  
**Canonical Run:** `data/runs/run_20251214T151352Z_479f8f01`

## Overview

FT-M-05.08 aligns ServiceWithBuffer inspector metrics with service nodes, ensures class coverage parity where class series exist, and documents the API derivation rules so the UI is source-agnostic (templates vs. telemetry). It also addresses a regression where chip hover tooltips disappeared after overlay-only updates.

## Key Changes

1. **API-derived ServiceWithBuffer metrics**  
   Queue latency, utilization, and service time are derived at the API layer when the required input series exist, keeping the UI consistent without forcing template-only fixes. Missing inputs now render a clear “No data” placeholder.  
   (`src/FlowTime.API/Services/StateQueryService.cs`, `tests/FlowTime.Api.Tests`)
2. **Class coverage parity**  
   ServiceWithBuffer nodes downstream of routers now surface ByClass series where available, and inspector class chips render consistently for classed templates like `transportation-basic-classes`.  
   (`tests/FlowTime.Api.Tests`, `tests/FlowTime.UI.Tests`)
3. **Template + modeling alignment**  
   ServiceWithBuffer templates now emit capacity and processing time series where needed, and modeling/reference docs clarify the required series and derivation rules.  
   (`templates/*.yaml`, `docs/modeling.md`, `docs/notes/modeling-queues-and-buffers.md`, `docs/reference/engine-capabilities.md`)
4. **Inspector polish + regression fix**  
   Restored chip hover tooltips by refreshing chip hitboxes on overlay updates. Expression blocks now use the shared code-block styling with wrapping text, and dependency rows were refined for dark-mode contrast and tighter spacing.  
   (`src/FlowTime.UI/wwwroot/js/topologyCanvas.js`, `src/FlowTime.UI/Pages/TimeTravel/Topology.razor`, `src/FlowTime.UI/wwwroot/css/app.css`)

## Tests

- `dotnet build FlowTime.sln`
- `dotnet test --nologo` *(perf benchmarks skipped as expected)*

## Verification

- Manual UI check: ServiceWithBuffer inspector shows queue depth + latency + service metrics.
- Manual UI check: class chips appear for HubQueue + dispatch queues in `transportation-basic-classes`.
- Manual UI check: chip hover tooltips restore after overlay updates.
