# Release FT-M-05.14 — Topology Focus View (Provenance Drilldown)

**Release Date:** 2026-01-14  
**Type:** Milestone delivery  
**Canonical Run:** TBD

## Overview

FT-M-05.14 adds a topology focus view to isolate a selected node and its provenance chain. The UI now provides a focus toggle with a contextual Focus panel, upstream-only filtering by default, optional downstream inclusion, compact relayout, and preserved view state between full and focus modes.

## Key Changes

1. **Focus toggle + panel controls**  
   A focus switch is available in the topology top bar (with tooltip guidance), and a Focus panel appears below Flows to include downstream successors.  
   (`src/FlowTime.UI/Pages/TimeTravel/Topology.razor`, `src/FlowTime.UI/wwwroot/css/app.css`)
2. **Upstream-first filtering with optional downstream**  
   Focus view filters to the selected node and its predecessors, optionally expanding downstream via the Focus panel.  
   (`src/FlowTime.UI/Components/Topology/TopologyCanvas.razor.cs`)
3. **Compact relayout + caching**  
   Filtered subgraphs are re-laid out via the existing GraphMapper layout and cached per focus target for responsiveness.  
   (`src/FlowTime.UI/Components/Topology/TopologyCanvas.razor.cs`)
4. **Viewport state preservation**  
   Focus view maintains a separate pan/zoom snapshot and restores the full view state on exit.  
   (`src/FlowTime.UI/Pages/TimeTravel/Topology.razor`)
5. **Focus view tests + tracking updates**  
   Added UI tests for focus toggle behavior, filtering, relayout, and viewport preservation; milestone docs updated.  
   (`tests/FlowTime.UI.Tests/TimeTravel/TopologyFocusViewTests.cs`, `tests/FlowTime.UI.Tests/TimeTravel/TopologyCanvasRenderTests.cs`, `work/milestones/completed/FT-M-05.14-topology-focus-view.md`, `work/milestones/tracking/FT-M-05.14-tracking.md`)

## Tests

- `dotnet test --nologo tests/FlowTime.UI.Tests/FlowTime.UI.Tests.csproj --filter Topology_FocusView_TogglesOnSelectedNode`
- `dotnet test --nologo tests/FlowTime.UI.Tests/FlowTime.UI.Tests.csproj --filter Topology_FocusView_DefaultsToUpstreamOnly`
- `dotnet test --nologo tests/FlowTime.UI.Tests/FlowTime.UI.Tests.csproj --filter Topology_FocusView_RelayoutsFilteredGraph`
- `dotnet test --nologo tests/FlowTime.UI.Tests/FlowTime.UI.Tests.csproj --filter Topology_FocusView_PreservesFullGraphState`
- `dotnet test FlowTime.sln --nologo`

## Verification

- Manual UI check: supply chain template focus view behaves as expected.
- Manual UI check: transportation template focus view behaves as expected.
