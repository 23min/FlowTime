---
id: M-015
title: Core Analytical Evaluation
status: done
parent: E-16
acs:
  - id: AC-1
    title: Core exposes an analytical evaluation surface for snapshot, window
    status: met
  - id: AC-2
    title: Core returns one consolidated internal analytical result surface with
    status: met
  - id: AC-3
    title: StateQueryService no longer computes analytical emission truth or
    status: met
  - id: AC-4
    title: flowLatencyMs computation moves to Core as a pure function
    status: met
  - id: AC-5
    title: Effective capacity computation (capacity x parallelism) has one owner
    status: met
  - id: AC-6
    title: Tests prove analytical evaluation against both real multi-class
    status: met
  - id: AC-7
    title: MetricsService and analogous analytical query surfaces consume the
    status: met
  - id: AC-8
    title: dotnet build and dotnet test --nologo are green.
    status: met
---

## Goal

Move analytical values, emitted-series truth, and graph-level analytical computation into a pure Core evaluation surface so the API projects analytical results instead of deciding them. This includes both per-node analytical evaluation (from `AnalyticalCapabilities`) and graph-level flow latency propagation (from `StateQueryService.ComputeFlowLatency`).

## Context

`AnalyticalCapabilities` moved current math into Core, but emitted-series truth is still partially computed in the adapter. That leaves the most important projection question — what should actually be emitted for this node/window/class — split across Core and API.

Additionally, `flowLatencyMs` — the cumulative flow latency from entry to a node through the queueing network — is currently computed in `StateQueryService.ComputeFlowLatency()` as a topological graph traversal with flow-volume-weighted accumulation. This is graph-level queueing theory (expected sojourn time through a network), not an adapter concern. The Core IS a graph engine; graph-level analytical computation belongs there.

Effective capacity computation (`capacity x parallelism`) is also currently split between `InvariantAnalyzer` (Core) and `StateQueryService.GetEffectiveCapacity` (API). It should have one owner in Core.

Metrics query resolution also still carries a duplicate analytical path: `MetricsService` first asks `StateQueryService` for a window, then falls back to `ResolveViaModelAsync()` and recomputes analytical behavior from the model when state-window resolution fails. That is still a second analytical execution path in the adapter and E-16 must delete it. E-16 stays forward-only here as well: legacy runs that depended on that path are removed and regenerated rather than supported through a special failure/upgrade mode.

**Supersedes D-016** for `flowLatencyMs`: that decision was a bridge for M-057 scope. E-16 is the full purification — graph-level analytical computation moves to Core.

## Acceptance criteria

### AC-1 — Core exposes an analytical evaluation surface for snapshot, window

Core exposes an analytical evaluation surface for snapshot, window, and by-class values driven by the compiled analytical descriptor and explicit class-truth boundary.
### AC-2 — Core returns one consolidated internal analytical result surface with

Core returns one consolidated internal analytical result surface with explicit nested sections for derived values, emitted-series truth, effective-capacity/utilization facts, and graph-level flow-latency outputs sufficient for projection.
### AC-3 — StateQueryService no longer computes analytical emission truth or

`StateQueryService` no longer computes analytical emission truth or per-node/per-class analytical math locally in the current state paths.
### AC-4 — flowLatencyMs computation moves to Core as a pure function

`flowLatencyMs` computation moves to Core as a pure function: `(compiledTopology, perNodeCycleTime[], edgeFlowVolume[]) → perNodeFlowLatency[]`. The adapter passes inputs, Core owns the graph traversal and accumulation.
### AC-5 — Effective capacity computation (capacity x parallelism) has one owner

Effective capacity computation (`capacity x parallelism`) has one owner in Core. The API's `GetEffectiveCapacity` / `GetParallelismValue` / `ComputeUtilizationSeries` delegation is replaced by Core evaluation.
### AC-6 — Tests prove analytical evaluation against both real multi-class

Tests prove analytical evaluation against both real multi-class fixtures and explicit fallback cases without conflating the two.
### AC-7 — MetricsService and analogous analytical query surfaces consume the

`MetricsService` and analogous analytical query surfaces consume the same Core evaluation surface instead of maintaining a second model-evaluation fallback path for analytical behavior; legacy runs that depended on the fallback path are removed or regenerated as part of the forward-only cut.
### AC-8 — dotnet build and dotnet test --nologo are green.

`dotnet build` and `dotnet test --nologo` are green.
## Guards / DO NOT

- **DO NOT** leave `flowLatencyMs` in the adapter. It is graph-level queueing theory — expected sojourn time through a network — not an orchestration concern. The Core is a graph engine; this computation belongs there.
- **DO NOT** leave partial computation in the adapter "for convenience." If the adapter computes any analytical value, that is a regression.
- **DO NOT** keep `GetEffectiveCapacity()` or `ComputeUtilizationSeries()` in `StateQueryService`. Effective capacity (with parallelism) is a flow algebra concept.
- **DO NOT** keep `MetricsService.ResolveViaModelAsync()` as a second analytical execution path once the unified Core evaluator exists.
- **DO NOT** introduce an "unsupported legacy run" mode or regeneration hint path for analytical fallback cleanup. Delete old runs and regenerate future-version runs instead.
- **DO NOT** make the evaluator depend on adapter types. The evaluator takes compiled descriptors + raw series data and returns analytical results. The adapter projects those results into DTOs.
- **DO NOT** use ad hoc dictionaries or flag tuples for results. Prefer explicit result types and one consolidated internal result surface with nested sections over many partial policy-bearing result fragments.
- **DO NOT** make `flowLatencyMs` depend on implicit node ordering. The Core evaluator should perform explicit topological sorting or receive an explicit order.

## Deletion Targets

| Target | Location | Why |
|--------|----------|-----|
| `ComputeFlowLatency()` | StateQueryService.cs:2748 | Graph-level analytical computation moves to Core |
| `BuildIncomingFlowEdges()` | StateQueryService.cs:2867 | Supporting method for flowLatencyMs — moves to Core |
| `BuildEdgeFlowVolumeLookup()` | StateQueryService.cs:2921 | Supporting method for flowLatencyMs — moves to Core |
| `GetEffectiveCapacity()` | StateQueryService.cs:2644 | Effective capacity moves to Core |
| `GetParallelismValue()` | StateQueryService.cs:2666 | Parallelism resolution moves to Core (compiler) |
| `ComputeUtilizationSeries()` | StateQueryService.cs:2595 | Utilization is derived from effective capacity — Core |
| `ConvertClassMetrics()` local computation | StateQueryService.cs:1011 | Class analytical math moves to Core evaluator |
| `ResolveViaModelAsync()` fallback | MetricsService.cs | Duplicate adapter-side analytical execution path must be deleted |
| `AnalyticalCapabilities.ComputeBin/Window/ClassBin/ClassWindow` | AnalyticalCapabilities.cs (if not deleted in M-014) | These methods become the evaluator |
| Per-node analytical math in snapshot/window builders | StateQueryService.cs | Replaced by Core evaluator calls |

## Test Strategy

- **Evaluator unit tests:** Pure function tests — given descriptor + raw series data, assert correct analytical results for snapshot, window, and by-class.
- **flowLatencyMs graph tests:** Given a small compiled topology with known cycle times and edge flow volumes, assert correct cumulative latency propagation.
- **Effective capacity tests:** Given capacity series + parallelism (constant and time-varying), assert correct effective capacity and utilization.
- **Adapter-is-projector tests:** Assert that `StateQueryService` calls Core evaluator and maps results to DTOs without computing any analytical values locally.
- **Single analytical path tests:** Assert that metrics queries and state-window queries consume the same Core evaluator surface and do not diverge through an alternate model-fallback path.
- **Multi-class vs fallback tests:** Evaluator tests must include both real multi-class and fallback-only fixtures as separate test cases.
- **Parity tests:** End-to-end API tests prove the same analytical outputs as before the migration.

## Technical Notes

- Keep pure math helpers where useful, but move policy composition out of the adapter.
- Prefer one consolidated internal analytical result model with nested sub-results over many partial adapter-composed results.
- Warning/analyzer facts are split into the next milestone so this slice stays independently shippable.
- The flowLatencyMs evaluator should take explicit topological order or compute it from the compiled graph. Do not rely on implicit iteration order.
- `flowLatencyMs` uses flow-volume-weighted accumulation: `upstream = sum(flow[i] * predLatency[i]) / sum(flow[i])`. This is the right formula — preserve it.

## Out of Scope

- Warning/analyzer fact publication (M-016)
- Public contract changes (M-017)
- Client migration (M-017)

## Dependencies

- [M-014](M-014.md)
