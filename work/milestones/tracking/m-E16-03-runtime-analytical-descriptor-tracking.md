# Tracking: Runtime Analytical Descriptor

**Milestone:** m-E16-03-runtime-analytical-descriptor
**Branch:** milestone/m-E16-03-runtime-analytical-descriptor
**Started:** 2026-04-05
**Completed:** 2026-04-05
**Status:** complete

## Acceptance Criteria

- [x] AC1: Runtime nodes carry a compiled analytical descriptor that captures effective analytical identity, queue and service semantics, cycle-time applicability, warning applicability, queue-origin and source-node facts, node category, and resolved parallelism.
- [x] AC2: Explicit `serviceWithBuffer` nodes and reference-resolved queue-backed nodes produce identical descriptors using typed references and real fixture shapes.
- [x] AC3: Snapshot and window analytical paths, backlog warnings, flow-latency base composition, SLA helper logic, and internal state and graph projection paths consume the descriptor instead of reconstructing analytical identity from strings.
- [x] AC4: `AnalyticalCapabilities` is deleted and its capability flags are absorbed into the descriptor; computation methods move to the evaluator in the next milestone.
- [x] AC5: Adapter-side logical-type inference helpers used for runtime analytical behavior are deleted.
- [x] AC6: Core and targeted API tests prove parity for both explicit and reference-resolved cases.
- [x] AC7: Node category is a compiled descriptor field and no downstream code re-derives it from `kind` strings.
- [x] AC8: `dotnet build` and `dotnet test --nologo` are green.

## Implementation Log

| Phase | What | Tests | Status |
|-------|------|-------|--------|
| 1 | Preflight the merged epic baseline, audit descriptor and analytical-identity hotspots, and map the first deletions | baseline + audit | completed |
| 2 | Add the compiled runtime analytical descriptor and descriptor-compilation coverage in Core | targeted Core tests | completed |
| 3 | Move analytical consumers to descriptor facts and remove string-based runtime inference in Core, API, and UI category consumers | targeted Core/API/UI tests | completed |
| 4 | Validate parity, remove remaining bridge helpers, and record milestone notes | full build + full test suite | completed |
| 5 | Address review follow-up on logical-type propagation through inspector/provenance paths and remaining computed-category checks | focused UI/API regression tests + full build/test | completed |

## Test Summary

- **Baseline before branch creation:** `dotnet build` green and `dotnet test --nologo` green on 2026-04-05 from the merged E-16 epic baseline after the legacy stopwatch-based M15 expression-type gate was quarantined from default suite readiness.
- **Descriptor compilation RED/GREEN:** `dotnet test tests/FlowTime.Core.Tests/FlowTime.Core.Tests.csproj --nologo --filter AnalyticalDescriptorCompilationTests` → `8/8` passed after descriptor types/compiler/parser integration.
- **Core analytical parity:** `dotnet test tests/FlowTime.Core.Tests/FlowTime.Core.Tests.csproj --nologo --filter Analytical` → `117/117` passed after migrating computation and metadata tests to descriptor + evaluator.
- **Focused API parity:** targeted state/graph analytical API tests passed (`69/69`) after `StateQueryService` and `GraphService` switched to descriptor-driven projection.
- **UI regression coverage:** `dotnet test tests/FlowTime.UI.Tests/FlowTime.UI.Tests.csproj --nologo --filter TopologyHelpersTests` → `18/18` passed; `dotnet test tests/FlowTime.UI.Tests/FlowTime.UI.Tests.csproj --nologo --filter "TopologyInspectorTests|TopologyInspectorTabsTests|TopologySparklinesTests|TopologyHelpersTests"` → `87/87` passed; `dotnet test tests/FlowTime.UI.Tests/FlowTime.UI.Tests.csproj --nologo` → `275/275` passed after logicalType-only UI cleanup and review follow-up fixes.
- **Review follow-up parity:** `dotnet test tests/FlowTime.Api.Tests/FlowTime.Api.Tests.csproj --nologo --filter "GetStateWindow_LogicalTypeSwb_HasParityWithExplicitSwb|GetStateWindow_FullMode_IncludesComputedNodes"` → `2/2` passed after removing the remaining authored-kind computed filters.
- **Final gates on branch:** `dotnet build` green on 2026-04-05 and final `dotnet test --nologo` green on 2026-04-05 with `FlowTime.Expressions.Tests 55/55`, `FlowTime.Adapters.Synthetic.Tests 10/10`, `FlowTime.Core.Tests 256/256`, `FlowTime.UI.Tests 276/276`, `FlowTime.Integration.Tests 8/8`, `FlowTime.Cli.Tests 19/19`, `FlowTime.Tests 227/234` (`7` skipped legacy perf/example gates), `FlowTime.Sim.Tests 201/204` (`3` skipped smoke/example gates), and `FlowTime.Api.Tests 214/214`.

## Notes

- This milestone starts from `epic/E-16-formula-first-core-purification` after local merge commit `9e355e8` integrated `m-E16-02-class-truth-boundary`.
- Initial audit targets from the spec: `AnalyticalCapabilities`, `StateQueryService` analytical-identity helpers, queue-origin reconstruction, node-category reconstruction, and runtime parallelism resolution.
- First red tests should cover descriptor parity between explicit `serviceWithBuffer` nodes and reference-resolved queue-backed nodes, plus compiled node-category coverage for expression, constant, service, queue, DLQ, and router nodes.
- `AnalyticalCapabilities` is deleted. Runtime nodes now carry compiled `AnalyticalDescriptor` facts and analytical math moved to `AnalyticalEvaluator` as the bridge into m-E16-04.
- API projection helpers now use descriptor-derived identity/category facts; milestone-banned adapter helper names such as `DetermineLogicalType`, `TryResolveServiceWithBufferDefinition`, and `NormalizeKind` no longer exist in API services.
- UI category/service-like hotspots were updated to consume descriptor-derived `nodeLogicalType` rather than falling back to authored `kind` strings. Added a UI regression test proving `GraphMapper` no longer backfills `LogicalType` from `Kind`.
- Review on 2026-04-05 requested changes before wrap; the follow-up patch switched Time Travel inspector, provenance, tooltip inputs, sparkline missing-state logic, and remaining API/UI computed-category checks to resolved logical-type facts.
- Added regression tests for promoted `service -> serviceWithBuffer` inspector/provenance behavior and promoted computed nodes that rely on `nodeLogicalType` rather than authored `kind`.
- Final reviewer pass on 2026-04-05 approved wrap after the remaining graph `kinds` filter and topology canvas kind-based visual branches were switched to descriptor/logical-type truth.