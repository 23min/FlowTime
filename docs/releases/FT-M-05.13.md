# Release FT-M-05.13 - ServiceWithBuffer Parallelism + Capacity Backlog

**Release Date:** 2026-01-14  
**Type:** Milestone delivery  
**Canonical Run:** TBD

## Overview

FT-M-05.13 adds parallelism to service-with-buffer nodes so effective capacity scales with instances/workers. Utilization and backlog warnings now use effective capacity, and the UI surfaces instances, base capacity, and effective capacity with provenance. Additional regression tests ensure parallelism reduces utilization and queue depth for the same arrivals/capacity inputs.

## Key Changes

1. **Parallelism semantics + effective capacity**  
   Effective capacity is computed as `capacity × parallelism`, used for utilization and backlog overload analysis.  
   (`src/FlowTime.API/Services/StateQueryService.cs`, `src/FlowTime.Core/Analysis/InvariantAnalyzer.cs`)
2. **UI exposure for instances and effective capacity**  
   Node chips show instances when >1, and inspector rows include instances + effective capacity with provenance.  
   (`src/FlowTime.UI/Pages/TimeTravel/Topology.razor`, `src/FlowTime.UI/wwwroot/js/topologyCanvas.js`, `src/FlowTime.UI/Services/MetricProvenanceCatalog.cs`)
3. **Schema + graph updates**  
   Time-travel state schema includes parallelism and graph responses include normalized parallelism semantics.  
   (`docs/schemas/time-travel-state.schema.json`, `src/FlowTime.API/Services/GraphService.cs`, `tests/FlowTime.Api.Tests/Golden/graph-run_graph_fixture.json`)
4. **Behavioral regression coverage**  
   New tests compare baseline vs parallelism runs to ensure utilization halves and queue depth drops.  
   (`tests/FlowTime.Api.Tests/StateEndpointTests.cs`)

## Tests

- `dotnet build`
- `dotnet test --nologo` (timed out after 120s; all completed tests passed; performance tests skipped)
