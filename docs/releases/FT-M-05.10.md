# Release FT-M-05.10 — Sink Node Role (Success Terminal)

**Release Date:** 2026-01-07  
**Type:** Milestone delivery  
**Canonical Run:** TBD

## Overview

FT-M-05.10 introduces a metadata-only `nodeRole: sink` flag for terminal success nodes. The role is propagated through templates and graph contracts, rendered in the topology UI as a "Terminal" badge, and suppresses utilization/error-rate chips unless the corresponding series are explicitly provided. Engine behavior is unchanged; this is purely interpretive UI guidance.

## Key Changes

1. **Sink role metadata propagation**  
   Templates can declare `nodeRole: sink`, and the role is preserved in canonical models and graph responses.  
   (`docs/schemas/template.schema.json`, `src/FlowTime.Sim.Core/Templates/Template.cs`, `src/FlowTime.Sim.Core/Templates/SimModelBuilder.cs`, `src/FlowTime.Contracts/Dtos/ModelDtos.cs`, `src/FlowTime.Contracts/Services/ModelService.cs`, `src/FlowTime.Core/Models/ModelParser.cs`, `src/FlowTime.Core/Models/Node.cs`, `src/FlowTime.Contracts/TimeTravel/GraphContracts.cs`, `src/FlowTime.API/Services/GraphService.cs`)
2. **Topology UI sink badge**  
   Sink nodes render a "Terminal" badge in the inspector and topology overlays.  
   (`src/FlowTime.UI/Pages/TimeTravel/Topology.razor`, `src/FlowTime.UI/Components/Topology/GraphMapper.cs`)
3. **Metric suppression for sinks**  
   Utilization/error-rate chips are hidden for sink nodes unless those series exist.  
   (`src/FlowTime.UI/Pages/TimeTravel/Topology.razor`)
4. **Authoring guidance**  
   Added sink role documentation for template authors and modelers.  
   (`docs/templates/template-authoring.md`, `docs/notes/modeling-queues-and-buffers.md`)

## Tests

- `dotnet test --nologo tests/FlowTime.Sim.Tests/FlowTime.Sim.Tests.csproj --filter Template_With_Sink_NodeRole_Parses`
- `dotnet test --nologo tests/FlowTime.Sim.Tests/FlowTime.Sim.Tests.csproj --filter CanonicalModelWriter_Preserves_NodeRole`
- `dotnet test --nologo tests/FlowTime.Api.Tests/FlowTime.Api.Tests.csproj --filter GetGraphAsync_EmitsNodeRole`
- `dotnet test --nologo tests/FlowTime.UI.Tests/FlowTime.UI.Tests.csproj --filter FullyQualifiedName~SinkNode`

## Verification

- Manual UI check: a sink node shows a "Terminal" badge in the inspector.
- Manual UI check: utilization/error-rate chips are absent for sinks with no series, and appear when the series are present.
- API check: graph response includes `nodeRole: "sink"` for marked nodes.
