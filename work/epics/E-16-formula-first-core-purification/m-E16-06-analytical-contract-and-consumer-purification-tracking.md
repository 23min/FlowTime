# Tracking: m-E16-06 — Analytical Contract and Consumer Purification

**Started:** 2026-04-07
**Completed:** 2026-04-07
**Branch:** `milestone/m-E16-06-analytical-contract-and-consumer-purification`
**Spec:** `work/epics/E-16-formula-first-core-purification/m-E16-06-analytical-contract-and-consumer-purification.md`
**Preflight:** `dotnet build` green on 2026-04-07; `dotnet test tests/FlowTime.Api.Tests/FlowTime.Api.Tests.csproj --nologo` green on 2026-04-07; `dotnet test tests/FlowTime.UI.Tests/FlowTime.UI.Tests.csproj --nologo` green on 2026-04-07

## Acceptance Criteria

- [x] AC-1: `FlowTime.Contracts` and the current `/state`, `/state_window`, and `/graph` response shapes expose a compact authoritative fact surface sufficient to determine analytical behavior and node category without `kind + logicalType` inference.
- [x] AC-2: The explicit first-party consumer scope for this milestone is migrated to use engine-published facts: `TimeTravelMetricsClient`, `Dashboard.razor.cs`, `Topology.razor`, `GraphMapper`, `TopologyCanvas.razor.cs`, `TooltipFormatter`, and `wwwroot/js/topologyCanvas.js`.
- [x] AC-3: Old hint fields and targeted analytical heuristics are removed in the same forward-only cut once those consumers are migrated; runs, fixtures, and approved snapshots are regenerated rather than compatibility-layered.
- [x] AC-4: API/UI tests and a grep-based audit prove the targeted analytical classification helpers are deleted.
- [x] AC-5: Documentation and decision records are updated so E-10 Phase 3 can resume on the purified boundary.

## Initial Plan

| Phase | Scope | Test Strategy | Status |
|-------|-------|---------------|--------|
| 1 | Add red tests for the new contract fact surface on state and graph nodes | API serialization/endpoint tests for `category`, compact analytical facts, and explicit class-truth publication | completed |
| 2 | Publish the new contract facts from API projection without duplicating Core truth | Focused API service and endpoint tests over `/state`, `/state_window`, and `/graph` | completed |
| 3 | Migrate Blazor and canvas consumers off `kind + logicalType` heuristics | UI unit tests proving consumers read contract facts instead of local `IsServiceLike`/`Classify` helpers | completed |
| 4 | Delete old hints/helpers, regenerate approved outputs, and run wrap validation | Grep audit, targeted API/UI suites, then `dotnet build` and `dotnet test --nologo` | completed |

## Notes

- This milestone starts from the E16 continuation line after m-E16-05 completed on 2026-04-06.
- The agreed public shape is one source of truth per question: `category` for the coarse consumer-facing node family, a compact `analytical` fact object for identity/applicability, and no `kind + logicalType` heuristic path for behavioral decisions.
- `kind` may remain only as display/authored context while the milestone is in flight; it is not the behavioral truth surface.
- 2026-04-07: Current `/state`, `/state_window`, and `/graph` contracts now publish `category`, compact `analytical` facts, explicit `classTruth`, and state-level class catalog metadata while omitting `nodeLogicalType` from current response shapes.
- 2026-04-07: The named Blazor and canvas consumers now read contract facts instead of local `kind`/`logicalType` heuristics; the targeted helper audit is clean.
- 2026-04-07: Wrap validation is green: `dotnet build`, `dotnet test tests/FlowTime.Api.Tests/FlowTime.Api.Tests.csproj --nologo`, `dotnet test tests/FlowTime.UI.Tests/FlowTime.UI.Tests.csproj --nologo`, `dotnet test --nologo`, and the UI helper grep audit all succeeded.

## Wrap Summary

- Current state and graph contracts now expose authoritative category, analytical, and class-truth facts without the legacy `nodeLogicalType` hint field.
- The first-party Blazor and canvas consumers named in the milestone spec now consume those published facts instead of reconstructing analytical behavior from `kind + logicalType`.
- Full wrap validation is green, and E-10 Phase 3 can resume on the purified boundary.