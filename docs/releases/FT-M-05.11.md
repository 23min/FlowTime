# Release FT-M-05.11 — Sink Node Kind (Terminal Success Semantics)

**Release Date:** 2026-01-09  
**Type:** Milestone delivery  
**Canonical Run:** TBD

## Overview

FT-M-05.11 introduces a first-class `kind: sink` node so terminal success has explicit engine semantics. Sinks now terminate arrivals as served, suppress queue/capacity semantics, and expose completion SLA and schedule adherence without misusing service nodes.

## Key Changes

1. **Schema + parser support for sink nodes**  
   Sink nodes are allowed in templates, and invalid queue/capacity/retry fields are rejected.  
   (`src/FlowTime.Sim.Core/Templates/TemplateValidator.cs`, `tests/FlowTime.Sim.Tests/NodeBased/TemplateParserTests.cs`)
2. **Engine semantics for terminal success**  
   Sink defaults derive `served = arrivals`, zero errors, and disallow queue/capacity/retry semantics.  
   (`src/FlowTime.Sim.Core/Templates/SinkNodeSynthesizer.cs`, `tests/FlowTime.Sim.Tests/Templates/SinkNodeTemplateTests.cs`)
3. **Completion SLA support in outputs**  
   State window responses emit completion series for sinks and align SLA expectations for terminal nodes.  
   (`tests/FlowTime.Api.Tests/StateEndpointTests.cs`)
4. **UI rendering + metric suppression for sinks**  
   Sink badges render in topology, and sink tooltips/inspector rows suppress queue/capacity/retry metrics.  
   (`src/FlowTime.UI/Pages/TimeTravel/Topology.razor`, `src/FlowTime.UI/Components/Topology/TopologyCanvas.razor.cs`)
5. **Template alignment + docs**  
   Transportation sinks now represent terminal destinations, with updated documentation for sink guidance.  
   (`templates/transportation-*.yaml`, `docs/templates/template-authoring.md`, `docs/architecture/service-with-buffer/sink-node-architecture.md`)

## Tests

- `dotnet test --nologo tests/FlowTime.Sim.Tests/FlowTime.Sim.Tests.csproj --filter SinkNode_RejectsQueueCapacityFields`
- `dotnet test --nologo tests/FlowTime.Sim.Tests/FlowTime.Sim.Tests.csproj --filter SinkNode_ServedEqualsArrivals`
- `dotnet test --nologo tests/FlowTime.Sim.Tests/FlowTime.Sim.Tests.csproj --filter SinkNode_RejectsRetryFields`
- `dotnet test --nologo tests/FlowTime.Api.Tests/FlowTime.Api.Tests.csproj --filter GetStateWindow_SinkNode_EmitsCompletionSeries`
- `dotnet test --nologo`

## Verification

- Manual UI check: sinks render as terminal nodes with SLA/completion semantics and no queue/capacity rows.
