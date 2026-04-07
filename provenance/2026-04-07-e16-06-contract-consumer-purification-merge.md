# Session: E16-06 Contract And Consumer Purification Merge

**Date:** 2026-04-07  
**Branch:** `milestone/m-E16-06-analytical-contract-and-consumer-purification`  
**Tracker:** none  
**PR:** none  
**Source:** session metadata unavailable in mounted workspace storage

## Summary

This session completed `m-E16-06` and wrapped the full E-16 Formula-First Core Purification epic. The work published authoritative analytical/category/class-truth facts on current state and graph contracts, deleted the named first-party UI heuristics that still inferred behavior from `kind + logicalType`, regenerated the affected approved outputs, and validated the end-to-end contract path with Core, API, and UI tests.

The session also identified broader Sim/catalog/storage boundary cleanup work that does not belong in E-16. That planning work was split onto a dedicated E-19 branch so the E16 merge candidate remained scoped to the milestone wrap.

## Work Done

- Published authoritative `category`, `analytical`, and `classTruth` facts on the current `/state`, `/state_window`, and `/graph` response shapes.
- Removed the legacy `nodeLogicalType` hint from current contract publication and updated schema/golden fixtures to the new fact surface.
- Migrated the named first-party Blazor and canvas consumers away from local analytical/category heuristics to the published contract facts.
- Added API, Core, and UI regressions covering contract serialization, endpoint payloads, consumer migration, and negative assertions proving `nodeLogicalType` is absent.
- Wrote the E16-06 tracking document, marked the milestone complete, marked E-16 complete, and lifted the E-10 Phase 3 gate across roadmap/current-work surfaces.
- Split the separate E-19 planning/ADR work into its own commit and branch so the E16 merge path stayed clean.

## Decisions & Rationale

- **One contract fact surface per consumer question:** Analytical behavior and node category now come from published contract facts rather than late `kind` or `logicalType` inference. This keeps the purified runtime boundary honest and prevents consumer drift.
- **Forward-only cleanup over compatibility layering:** The contract, goldens, and tests were updated in one cut instead of preserving the old hint field alongside the new fact surface.
- **Keep E19 planning separate from the E16 merge:** The newly discovered Sim/catalog/storage boundary work is real, but it is not part of the E16 analytical purification slice and should not be merged as part of that milestone wrap.

## Problems & Solutions

- **Problem:** The remaining first-party UI consumers still reconstructed analytical behavior and category from `kind + logicalType`, which would have left the E16 purification incomplete at the current contract boundary.
  **Solution:** Published authoritative facts from the API contracts, migrated the named consumers to those facts, and added deletion/absence assertions for the legacy helpers and hint field.
- **Problem:** E16 wrap work and broader E19 planning discoveries ended up in the same workspace diff.
  **Solution:** Wrapped and committed E16-06 on its milestone branch, then moved the E19 planning/ADR changes to a dedicated `milestone/m-E19-01-supported-surface-inventory` branch.

## Pipeline & Deployment

- Validation run: `dotnet build`
- Validation run: `dotnet test tests/FlowTime.UI.Tests/FlowTime.UI.Tests.csproj --nologo`
- Validation run: `dotnet test --nologo`

## Key Files

**Created:**
- `provenance/2026-04-07-e16-06-contract-consumer-purification-merge.md`
- `work/epics/E-16-formula-first-core-purification/m-E16-06-analytical-contract-and-consumer-purification-tracking.md`

**Modified:**
- `src/FlowTime.Contracts/TimeTravel/StateContracts.cs`
- `src/FlowTime.Contracts/TimeTravel/GraphContracts.cs`
- `src/FlowTime.API/Services/StateQueryService.cs`
- `src/FlowTime.API/Services/GraphService.cs`
- `src/FlowTime.UI/Pages/TimeTravel/Topology.razor`
- `src/FlowTime.UI/Components/Topology/TopologyCanvas.razor.cs`
- `src/FlowTime.UI/Services/TimeTravelMetricsClient.cs`
- `tests/FlowTime.Api.Tests/StateEndpointTests.cs`
- `tests/FlowTime.UI.Tests/TimeTravel/TopologyInspectorTests.cs`
- `work/epics/E-16-formula-first-core-purification/spec.md`
- `ROADMAP.md`
- `CLAUDE.md`

## Follow-up

- Push and merge `milestone/m-E16-06-analytical-contract-and-consumer-purification` to `main`.
- Resume E-10 Phase 3 with `m-ec-p3d` after the E16 merge lands.
- Push `milestone/m-E19-01-supported-surface-inventory` separately and continue the supported-surface inventory there.