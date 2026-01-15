# Release FT-M-05.09 — ServiceWithBuffer SLA + Backlog Health Signals

**Release Date:** 2026-01-07  
**Type:** Milestone delivery  
**Canonical Run:** `data/runs/run_20251214T151352Z_479f8f01`

## Overview

FT-M-05.09 establishes a SLA taxonomy for ServiceWithBuffer (completion, backlog age, schedule adherence), makes SLA semantics batch-safe, and adds backlog health warnings so sustained queue risk is explicit. It also introduces a continuous, classed ServiceWithBuffer template and aligns queue invariants for dispatch queues to eliminate false warnings.

## Key Changes

1. **SLA taxonomy + batch-safe carry-forward**  
   SLA payloads now include explicit kinds and `unavailable` status when telemetry inputs are missing. Batch dispatch SLA is carried forward between releases instead of showing misleading 0% plateaus.  
   (`src/FlowTime.Contracts/TimeTravel/StateContracts.cs`, `src/FlowTime.API/Services/StateQueryService.cs`, `src/FlowTime.UI/Pages/TimeTravel/Topology.razor`)
2. **Backlog health warnings**  
   Node-local backlog warnings (growth streak, overload ratio, age risk) are derived in the API and surfaced in the inspector with clear time windows.  
   (`src/FlowTime.API/Services/StateQueryService.cs`, `src/FlowTime.UI/Pages/TimeTravel/Topology.razor`)
3. **Queue invariant alignment**  
   Dispatch queue series were aligned so queue depth matches inflow/outflow accumulation, removing false invariant warnings.  
   (`templates/transportation-basic-classes.yaml`, `tests/FlowTime.Sim.Tests/Templates/RouterTemplateRegressionTests.cs`)
4. **Continuous classed ServiceWithBuffer template**  
   Added a continuous, classed IT document processing template with retry/DLQ semantics to validate SLA/backlog rules beyond batch systems.  
   (`templates/it-document-processing-continuous.yaml`, `tests/FlowTime.Sim.Tests/Templates/ContinuousServiceWithBufferTemplateTests.cs`)
5. **Topology UI stability fixes**  
   Restored the feature-bar toggle and ensured initial sparklines refresh in operational mode.  
   (`src/FlowTime.UI/wwwroot/js/topologyCanvas.js`, `src/FlowTime.UI/Pages/TimeTravel/Topology.razor`)

## Tests

- `dotnet build`  
- `dotnet test --nologo` *(timed out; failure: `FlowTime.Tests.Performance.M2PerformanceTests.Test_PMF_Mixed_Workload_Performance`)*  
- `dotnet test --nologo tests/FlowTime.Tests/FlowTime.Tests.csproj --filter FullyQualifiedName~M15PerformanceTests`

## Verification

- Manual UI check: SLA labels show completion/backlog/schedule kind and “unavailable” when inputs are missing.
- Manual UI check: backlog warnings appear for sustained growth/overload/age risk windows.
- Manual UI check: sparklines render on first load in operational mode.
