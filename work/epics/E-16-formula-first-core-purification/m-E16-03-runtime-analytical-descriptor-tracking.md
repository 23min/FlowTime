# Tracking: m-E16-03 — Runtime Analytical Descriptor

**Started:** 2026-04-06
**Completed:** 2026-04-06
**Branch:** `milestone/m-E16-01-compiled-semantic-references` (continuation branch)
**Spec:** `work/epics/E-16-formula-first-core-purification/m-E16-03-runtime-analytical-descriptor.md`
**Preflight:** `dotnet build --no-restore` green; `dotnet test tests/FlowTime.Core.Tests/FlowTime.Core.Tests.csproj --nologo --no-restore --no-build` green

## Acceptance Criteria

- [x] AC-1: Runtime nodes carry a compiled analytical descriptor that captures: effective analytical identity, queue/service semantics, cycle-time applicability, warning applicability, queue-origin/source-node facts, node category, and resolved parallelism.
- [x] AC-2: Explicit `serviceWithBuffer` nodes and reference-resolved queue-backed nodes produce identical descriptors using typed references and real fixture shapes, not basename heuristics.
- [x] AC-3: Snapshot/window analytical paths, backlog warnings, flow-latency base composition, SLA helper logic, and internal state/graph projection paths consume the descriptor rather than reconstructing analytical identity from strings.
- [x] AC-4: `AnalyticalCapabilities` is deleted. Its capability flags are absorbed into the descriptor. Its computation methods now live on the descriptor-backed evaluator surface.
- [x] AC-5: Adapter-side logical-type inference helpers used for runtime analytical behavior are deleted.
- [x] AC-6: Core and targeted API tests prove parity for both explicit and reference-resolved cases.
- [x] AC-7: Node category (expression/constant/service/queue/dlq) is a compiled descriptor field. No downstream code re-derives it from `kind` strings.
- [x] AC-8: `dotnet build` and `dotnet test --nologo` are green.

## Delivery Summary

- Started E16-03 on the live continuation branch after confirming the pre-existing local `milestone/m-E16-03-runtime-analytical-descriptor` branch was stale and still aligned to the deprecated `work/milestones/` layout.
- Added compiled runtime analytical descriptor types plus a compiler helper that derives category, queue-semantics flags, queue-source facts, and typed parallelism facts from topology nodes and typed semantic references.
- `ModelParser.ParseMetadata()` now attaches compiled analytical descriptors to runtime topology nodes instead of leaving analytical identity as an adapter-only string-resolution concern.
- Added focused Core regressions covering explicit `serviceWithBuffer` descriptors, reference-resolved queue-backed service parity, file-backed no-inference behavior, and non-service topology categories.
- Replaced the `EffectiveKind` string bridge with typed `RuntimeAnalyticalIdentity` on the runtime descriptor and kept legacy `nodeLogicalType` projection derived from descriptor truth rather than adapter-side `kind`/`queueDepth` heuristics.
- `StateQueryService` snapshot/window/backlog/flow-latency paths and `GraphService` graph projection now consume `node.Analytical` plus `QueueSourceNodeId` for logical-type and dispatch-schedule behavior; the old API-side logical-type inference helpers were deleted.
- Extracted `RuntimeAnalyticalEvaluator` in Core, moved the surviving analytical math/metadata gates onto it, and deleted `AnalyticalCapabilities` instead of keeping a renamed bridge beside the descriptor.
- `InvariantAnalyzer`, `ModeValidator`, and `MetricsService` now consume compiled descriptor facts or descriptor-derived logical type for runtime analytical gating instead of re-classifying nodes from raw runtime `kind` strings.
- Tightened the project-rule source of truth to encode the E16 no-shim/no-heuristic/forward-only purity bar explicitly, then regenerated the derived assistant instruction surfaces with `bash .ai/sync.sh`.
- Focused Core descriptor/evaluator tests, focused API state/graph/metrics tests, repo-wide `dotnet build`, and repo-wide `dotnet test --nologo` are green for this slice.

## Implementation Log

| Phase | What | Tests | Status |
|-------|------|-------|--------|
| 1 | Map the descriptor seams: `AnalyticalCapabilities`, API logical-type inference, graph/state projection classification, and compiler/runtime node shapes | Search + focused descriptor compilation tests | complete |
| 2 | Introduce compiled runtime descriptor facts in Core and prove parity for explicit vs reference-resolved queue-backed nodes | `dotnet test tests/FlowTime.Core.Tests/FlowTime.Core.Tests.csproj --nologo --no-restore --filter "FullyQualifiedName~RuntimeAnalyticalDescriptorCompilerTests\|FullyQualifiedName~TopologyTests\|FullyQualifiedName~ModelCompilerParityTests\|FullyQualifiedName~SemanticLoaderTests\|FullyQualifiedName~ModeValidatorTests"` | complete |
| 3 | Switch API state/graph analytical paths to descriptor consumption and delete runtime logical-type inference helpers while leaving `AnalyticalCapabilities` removal for the remaining milestone cleanup | `dotnet test tests/FlowTime.Api.Tests/FlowTime.Api.Tests.csproj --nologo --filter "FullyQualifiedName~GraphServiceTests\|FullyQualifiedName~MetricsServiceTests\|FullyQualifiedName~StateEndpointTests"` | complete |
| 4 | Tighten framework/project purity guidance to match the E-16 no-shim/no-heuristic boundary and run broader validation | `bash .ai/sync.sh`; `dotnet build --no-restore` | complete |
| 5 | Pull the analytical evaluator extraction forward, delete `AnalyticalCapabilities`, replace `EffectiveKind` with typed descriptor identity, and re-run validation | `dotnet test tests/FlowTime.Core.Tests/FlowTime.Core.Tests.csproj --no-restore --nologo --filter "FullyQualifiedName~RuntimeAnalytical\|FullyQualifiedName~ModeValidatorTests\|FullyQualifiedName~TopologyTests\|FullyQualifiedName~SemanticLoaderTests"`; `dotnet test tests/FlowTime.Api.Tests/FlowTime.Api.Tests.csproj --no-restore --nologo --filter "FullyQualifiedName~StateQueryService\|FullyQualifiedName~GraphService\|FullyQualifiedName~MetricsService"`; `dotnet build`; `dotnet test --no-build --nologo` | complete |
| 6 | Replace the last runtime `kind`-based analytical gates in Core/API, close AC-7, and re-run validation | `dotnet test tests/FlowTime.Core.Tests/FlowTime.Core.Tests.csproj --nologo --no-restore --filter "FullyQualifiedName~FlowTime.Core.Tests.Analysis.InvariantAnalyzerTests\|FullyQualifiedName~FlowTime.Core.Tests.TimeTravel.ModeValidatorTests\|FullyQualifiedName~FlowTime.Core.Tests.Bugs.Phase0BugTests"`; `dotnet test tests/FlowTime.Api.Tests/FlowTime.Api.Tests.csproj --nologo --no-restore --filter "FullyQualifiedName~FlowTime.Api.Tests.Services.MetricsServiceTests"`; `dotnet build`; `dotnet test --no-build --nologo --verbosity minimal` | complete |

## Notes

- E16 remains forward-only: regenerated runs, fixtures, and approved outputs are preferred over compatibility bridges when the runtime analytical boundary changes.
- This slice starts from the post-E16-02 state where typed semantic references and explicit class-truth metadata are already in place.
- AC-7 is now closed: runtime analytical consumers use compiled descriptor facts or descriptor-derived logical type, while the remaining raw `kind` reads in this area are limited to authored `NodeDefinition` helpers and out-of-scope UI cleanup owned by later milestones.
- The intentionally untracked `docs/flowtime.md` and `docs/flowtime-v2.md` files remain outside this milestone scope.