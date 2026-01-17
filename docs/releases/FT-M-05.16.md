# Release FT-M-05.16 — Topology Inspector Tabs

**Release Date:** 2026-01-14  
**Type:** Milestone delivery  
**Canonical Run:** TBD

## Overview

FT-M-05.16 introduces a tabbed inspector so charts are always immediately visible and other inspector sections are organized into dedicated tabs.

## Key Changes

1. **Tabbed inspector layout**  
   Charts, properties, dependencies, warnings, and expression details are grouped into tabs with Charts as the default.  
   (`src/FlowTime.UI/Pages/TimeTravel/Topology.razor`)
2. **Tab state behavior**  
   Tab selection persists while the inspector remains open and resets on close.  
   (`src/FlowTime.UI/Pages/TimeTravel/Topology.razor`)
3. **UI tests for tab behavior**  
   Added tests covering default tab, content mapping, and reset behavior.  
   (`tests/FlowTime.UI.Tests/TimeTravel/TopologyInspectorTabsTests.cs`)

## Tests

- `dotnet test --nologo tests/FlowTime.UI.Tests/FlowTime.UI.Tests.csproj --filter Topology_InspectorTabs_DefaultsToCharts`
- `dotnet test --nologo tests/FlowTime.UI.Tests/FlowTime.UI.Tests.csproj --filter Topology_InspectorTabs_PreservesSelectionWhileOpen`
- `dotnet test --nologo tests/FlowTime.UI.Tests/FlowTime.UI.Tests.csproj --filter Topology_InspectorTabs_ResetsOnClose`
- `dotnet test --nologo`

## Verification

- Manual UI check: charts are the first view and tab selection resets after closing the inspector.
