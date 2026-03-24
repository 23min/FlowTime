# Release FT-M-05.15 — Series Semantics Metadata (Aggregation)

**Release Date:** 2026-01-15  
**Type:** Milestone delivery  
**Canonical Run:** TBD

## Overview

FT-M-05.15 adds optional series semantics metadata to `/state` and `/state_window` so the UI can display aggregation meaning (avg/p95/etc.) consistently for derived and telemetry-provided series.

## Key Changes

1. **Schema + contract support for series metadata**  
   Time-travel schema accepts optional `seriesMetadata` with aggregation/origin enums.  
   (`docs/schemas/time-travel-state.schema.json`, `tests/FlowTime.Api.Tests/StateResponseSchemaTests.cs`)
2. **API/DTO plumbing for metadata**  
   State responses include derived series metadata for latency/service/flow series.  
   (`src/FlowTime.API/Services/StateQueryService.cs`, `src/FlowTime.UI/Services/TimeTravelApiModels.cs`)
3. **UI provenance surfacing**  
   Inspector provenance tooltips render aggregation semantics when provided.  
   (`src/FlowTime.UI/Pages/TimeTravel/Topology.razor`, `work/epics/ui/metric-provenance.md`)
4. **Docs + guidance updates**  
   Template/telemetry guidance updated to describe semantics metadata usage.  
   (`docs/templates/template-authoring.md`)

## Tests

- `dotnet test --nologo --filter StateWindow_Response_AllowsSeriesMetadata`
- `dotnet test --nologo --filter SeriesMetadataIngestionTests`
- `dotnet test --nologo`

## Verification

- Manual UI check: aggregation label appears in provenance tooltips when metadata exists.
