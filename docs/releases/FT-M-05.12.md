# Release FT-M-05.12 - Metric Provenance & Audit Trail

**Release Date:** 2026-01-12  
**Type:** Milestone delivery  
**Canonical Run:** TBD

## Overview

FT-M-05.12 adds metric provenance to the time-travel UI so users can understand where values come from. Inspector properties and chart labels now reveal formulas, inputs, meanings, units, and gating notes via popovers, and bin dumps can open in a new tab with provenance payloads. The topology focus chips and mini-sparklines align with SLA/Arrivals series to avoid mismatched labels, and router inspectors now surface Arrivals alongside Served.

## Key Changes

1. **Inspector provenance popovers**  
   Metric properties and chart labels reveal formula, inputs, meaning, units, and gating rules.  
   (`src/FlowTime.UI/Pages/TimeTravel/Topology.razor`, `src/FlowTime.UI/Services/MetricProvenanceCatalog.cs`, `src/FlowTime.UI/wwwroot/css/app.css`)
2. **Bin dump provenance payload + new-tab modifier**  
   ALT/CTRL opens a browser tab with the JSON + provenance bundle.  
   (`src/FlowTime.UI/wwwroot/js/topologyCanvas.js`, `src/FlowTime.UI/Pages/TimeTravel/Topology.razor`)
3. **SLA/Arrivals focus alignment**  
   SLA mini-sparklines use `successRate`; Arrivals focus shows arrival volumes with a dedicated legend entry.  
   (`src/FlowTime.UI/Components/Topology/TopologyCanvas.razor.cs`, `src/FlowTime.UI/wwwroot/js/topologyCanvas.js`, `src/FlowTime.UI/Components/Topology/ColorScale.cs`)
4. **Router arrivals in inspector**  
   Router nodes always surface Arrivals (placeholder if missing) to keep volume metrics aligned with Served.  
   (`src/FlowTime.UI/Pages/TimeTravel/Topology.razor`)
5. **Documentation updates**  
   Provenance UX and semantics are documented in the UI architecture notes and the milestone spec.  
   (`docs/architecture/ui/metric-provenance.md`, `docs/milestones/completed/FT-M-05.12-metric-provenance.md`)

## Tests

- `dotnet build`
- `dotnet test --nologo`
