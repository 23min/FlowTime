# Release FT-M-05.13.1 — Class Filter Dimming Gap (Transportation Classes)

**Release Date:** 2026-01-15  
**Type:** Milestone delivery  
**Canonical Run:** TBD

## Overview

FT-M-05.13.1 fixes class filter dimming in the Transportation classes template by propagating class series for topology service-with-buffer semantics and adding analyzer coverage warnings.

## Key Changes

1. **Topology class propagation for service-with-buffer semantics**  
   Classed arrivals now propagate to served/errors series for topology service-with-buffer nodes.  
   (`src/FlowTime.Core/Artifacts/ClassContributionBuilder.cs`)
2. **Analyzer warnings for class coverage gaps**  
   Invariant analysis flags topology nodes that have classed arrivals but missing served/errors class series.  
   (`src/FlowTime.Core/Analysis/InvariantAnalyzer.cs`)
3. **Core regression tests**  
   Added coverage tests for class propagation and analyzer warnings.  
   (`tests/FlowTime.Core.Tests/`)

## Tests

- `dotnet test --nologo tests/FlowTime.Core.Tests/FlowTime.Core.Tests.csproj --filter ClassContributionBuilder_PropagatesServiceWithBufferTopologyClasses`
- `dotnet test --nologo tests/FlowTime.Core.Tests/FlowTime.Core.Tests.csproj --filter InvariantAnalyzer_WarnsOnTopologyClassCoverageGaps`
- `dotnet test --nologo`

## Verification

- Manual UI check: Airport class filter no longer dims LineAirport and Airport nodes.
