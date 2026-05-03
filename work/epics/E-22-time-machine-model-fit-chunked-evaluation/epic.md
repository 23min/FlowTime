---
id: E-22
title: Time Machine — Model Fit & Chunked Evaluation
status: proposed
---

## Goal

Close out the remaining Time Machine analysis modes — **model fitting** against real telemetry and **chunked evaluation** for feedback simulation — and crystallize the resulting surface as a clean embeddable **`FlowTime.Pipeline` SDK**. These are the last two analysis modes in the E-18 Time Machine architecture; delivering them completes the "FlowTime as a callable function" arc.

## Context

E-18 delivered 11 milestones covering parameterized evaluation, engine session protocol, tiered validation, parameter sweep, sensitivity, goal seek, N-parameter optimization, `SessionModelEvaluator`, and the .NET Time Machine CLI. All are on `main`. The analysis layer works end-to-end for synthetic runs.

Two scope items remained in E-18 and are explicitly carried into E-22:

- **Model Fit** (`FitSpec`/`FitRunner`/`POST /v1/fit`) — composes `ITelemetrySource` + `Optimizer` with residual as the objective. Infrastructure exists; the composition is the new work. **Blocked on** E-15 Telemetry Ingestion (first repeatable dataset path) and the Telemetry Loop & Parity epic (validated drift bounds — fitting against real telemetry without measured drift would produce falsely precise results).

- **Chunked Evaluation** (Mode 6) — bin-chunk evaluation for feedback simulation with external controllers. The Rust engine session (M-002) is the seam, but the chunk-step protocol on top of it is not designed.

A third scope item was decided (2026-04-20) to land alongside Fit and Chunked so the completed surface is exposed as a clean embeddable API:

- **`FlowTime.Pipeline` SDK wrapper** — thin project exposing `Sweep`, `Sensitivity`, `GoalSeek`, `Optimize`, `Fit`, `ChunkedEvaluate` as a programmatic embedding surface over the Time Machine internals. Callers today reach into `FlowTime.TimeMachine.Sweep.*` directly; the SDK crystallizes the external contract.

E-22 is sequenced per D-045 (Option A): `E-21 (active) → E-15 Telemetry Ingestion → Telemetry Loop & Parity → E-22`.

### Supersedes

Closes out E-18 placeholder `m-E18-XX Model Fit` and `m-E18-05 Chunked Evaluation`. E-18 itself remains the archived parent epic (11 delivered milestones recorded there as "complete"); E-22 is the forward-looking epic that completes its scope.

### Related

- **E-15 Telemetry Ingestion** (`work/epics/E-15-telemetry-ingestion/`) — hard prerequisite for Fit. Provides the first repeatable dataset path and replayable canonical bundle via Gold Builder → Graph Builder.
- **Telemetry Loop & Parity** (`work/epics/unplanned/telemetry-loop-parity/spec.md`) — hard prerequisite for Fit. Provides measured drift bounds between synthetic and replayed runs; without those bounds, fit quality cannot be meaningfully reported. Currently unnumbered; will take its own epic slot when scheduled.
- **E-21 Svelte Workbench & Analysis Surfaces** — builds UI for fit results once Fit's API contract is stable. Not in E-22 scope; E-21 milestone allocation is independent.

## Scope

### In Scope

- `FitSpec` + `FitRunner` + `POST /v1/fit` composing `ITelemetrySource` + `Optimizer` with residual objective (RMSE / MAE / configurable) against a user-selected series
- `flowtime fit` .NET CLI command, pipeable JSON-in/JSON-out, byte-compatible with `POST /v1/fit`
- Chunked evaluation protocol: stateful `chunk_step` session command on the Rust engine that advances a compiled plan by N bins, yielding partial results
- `ChunkSpec` + `ChunkRunner` + `POST /v1/chunked-eval` (final name TBD) driving the chunk-step protocol from .NET with external-controller integration
- `flowtime chunked-eval` .NET CLI command matching the API contract
- `FlowTime.Pipeline` project — thin embeddable SDK exposing `Sweep`, `Sensitivity`, `GoalSeek`, `Optimize`, `Fit`, `ChunkedEvaluate` as a clean programmatic API over the Time Machine internals; surfaces no HTTP, no CLI parsing, no artifact layout
- Migration of existing internal callers (the API endpoints, the .NET CLI commands) to the `FlowTime.Pipeline` SDK so the SDK is dogfooded from day one
- Test coverage matching E-18 standard: 100% branch coverage on pure runners; integration tests for Rust chunk-step protocol; API contract tests for `/v1/fit` and `/v1/chunked-eval`
- Documentation: `docs/architecture/time-machine-analysis-modes.md` extended with Fit and Chunked sections; `FlowTime.Pipeline` SDK embedding guide

### Out of Scope

- **Optimization constraints** (`--constraint "max(node.queue.utilization) < 0.8"`) — tracked in `work/gaps.md`; candidate for a later analysis-layer patch against `OptimizeSpec`/`GoalSeekSpec`/`FitSpec`
- **Monte Carlo** (Mode 5) — sampling parameters from distributions, characterizing output distribution. Lower priority than Fit; tracked in gaps
- **`FlowTime.Telemetry.*` direct-source adapters** (Prometheus, OpenTelemetry, BPI event logs) — E-15 Gold Builder covers the general batch path; adapters are narrower shortcuts to build only when a concrete client asks
- **Tiered validation parity across Sim UI / Blazor UI / Svelte UI / MCP / external agents** — validation surface work; closer to E-21 M-043 (Validation Surface) and future Blazor maintenance
- **Canonical bundle round-trip parity AC** (E-18 unchecked AC: capture baseline → replay bundle = same outputs modulo drift) — owned by Telemetry Loop & Parity, not E-22
- **Fit-result UI** — lands in E-21 (or a later Svelte milestone) once Fit's API is stable; E-22 delivers only the contract
- **Fitting algorithms beyond the existing optimizer** — Fit uses the existing Nelder-Mead `Optimizer` with a residual objective. Gradient-based or Bayesian fitters are future work
- **Time grid alignment across heterogeneous sources** — assumes E-15 Gold Builder presents series on a common grid; grid alignment belongs to E-15
- **Streaming / push UI for chunked evaluation** — chunked delivers a pull contract (caller drives chunk_step); WebSocket push for live simulation is a separate E-17-style track

## Constraints

- Builds on the existing Rust engine session protocol (M-002). No changes to the MessagePack framing; `chunk_step` is a new command alongside `compile`, `eval`, `patch`, `get_params`, `get_series`, `validate_schema`
- `FlowTime.Pipeline` is a pure SDK: no HTTP server, no CLI parsing, no artifact writing. It may compose services from other projects but its public API is in-process method calls returning strongly-typed results
- Fit against real telemetry is gated by the Telemetry Loop & Parity harness reporting drift within documented tolerance. A failing parity harness blocks Fit acceptance — reported fit residuals without parity validation are not trustworthy
- Byte-for-byte API ↔ CLI parity per the E-18 CLI convention: `flowtime fit < request.json` produces the same JSON as `POST /v1/fit`
- .NET 9 / C# 13; invariant culture; camelCase JSON payloads (project-rule: `project.md:43`)
- No reintroduction of the deprecated `FlowTime.Generator` project (D-032 Path B remains authoritative)

## Success Criteria

- [ ] `POST /v1/fit` accepts `(model YAML, telemetry source spec, target series, parameter overrides to fit, residual metric)` and returns a fitted parameter set plus fit quality (residual, iterations, convergence flag)
- [ ] `flowtime fit` CLI command produces byte-identical output to `POST /v1/fit` for equivalent inputs
- [ ] Fit results against a parity-validated telemetry fixture produce residuals within documented tolerance; the tolerance bound is set by the Telemetry Loop & Parity harness output
- [ ] Rust engine session responds to `chunk_step { bins: N }` command by advancing the compiled plan N bins and returning partial series; state persists across calls within a session
- [ ] `POST /v1/chunked-eval` drives the chunk-step protocol from .NET, exposing either a polling pull or a request/response-per-chunk model (choice is a design task for the milestone)
- [ ] External controller integration demonstration: a fixture controller consumes chunk outputs, writes back parameter patches, and the next chunk reflects the patch — end-to-end test, not just unit coverage
- [ ] `FlowTime.Pipeline` project compiles as a standalone library; its public API exposes `Sweep`, `Sensitivity`, `GoalSeek`, `Optimize`, `Fit`, `ChunkedEvaluate` as strongly-typed methods
- [ ] All `/v1/*` API endpoints added by E-18 are rewritten to call through `FlowTime.Pipeline` rather than `FlowTime.TimeMachine.*` internals directly; the SDK is dogfooded from day one
- [ ] All Time Machine CLI commands (`flowtime validate/sweep/sensitivity/goal-seek/optimize/fit/chunked-eval`) are rewritten to call through `FlowTime.Pipeline`
- [ ] 100% branch coverage on new pure runners (`FitRunner`, `ChunkRunner`); integration tests cover the Rust chunk-step protocol; API contract tests cover `/v1/fit` and `/v1/chunked-eval`
- [ ] `docs/architecture/time-machine-analysis-modes.md` updated with Fit and Chunked Evaluation sections reflecting the shipped behavior
- [ ] `FlowTime.Pipeline` embedding guide documents a concrete use case (e.g., hosted in a notebook, in an Azure Function, in a script) with a complete working example
- [ ] On epic completion, E-18's `m-E18-05 Chunked Evaluation` and `m-E18-XX Model Fit` placeholder rows are struck through; E-18 epic status flips to `complete` and the epic is archived under `work/epics/completed/`

## Risks & Open Questions

| Risk / Question | Impact | Mitigation |
|-----------------|--------|------------|
| Telemetry Loop & Parity epic has not been scheduled; without it Fit cannot be meaningfully validated | **Hard block** | Telemetry Loop & Parity must be scheduled and completed before Fit AC can be closed. E-22 does not start Fit until TLP ships |
| E-15 first-dataset-path timeline is uncertain | High | E-15 is on the critical path per Option A. E-22 planning proceeds; E-22 implementation is gated on E-15 delivering at least one end-to-end canonical bundle dataset |
| Chunked evaluation semantics for stateful nodes (e.g., queues carrying over bin boundaries) need design; the current `IStatefulNode` stubs may not suffice | Medium | First milestone of chunked scope is a design spike: document chunk-boundary state-transfer semantics before implementation |
| Residual metric choice (RMSE vs. MAE vs. weighted) may not generalize across queueing topologies | Medium | Start with RMSE as default; make the residual a configurable strategy in `FitSpec`; document tradeoffs |
| `FlowTime.Pipeline` SDK surface may be over-scoped if designed before external embedders give feedback | Medium | Keep the SDK minimal: expose only the methods that existing API/CLI callers need. Defer Monte Carlo, direct-source adapters, and any speculative surface |
| Rust `chunk_step` may reveal gaps in the stateful execution seam that require refactoring inside the engine core, not just protocol work | Medium | Scope a Rust-side design spike first; if the seam needs reshaping, split into a foundation milestone before the .NET chunk runner |
| Pipeline SDK rewrite of existing API/CLI callers may introduce regressions | Medium | Dogfood migration is done one endpoint at a time with full existing test coverage asserting behavior identity before and after |

## Milestones

Sequencing: Fit first (unblocks the largest downstream set — E-15 dataset path leads directly into fit validation). Chunked second (independent Rust protocol work; runs after Fit only to avoid Rust-engine contention on a single milestone branch). Pipeline SDK third so it crystallizes against the final surface including Fit and Chunked.

| ID | Title | Status | Depends on |
|----|-------|--------|-----------|
| m-E22-01-model-fit | Model Fit | not started | E-15 first dataset path complete; Telemetry Loop & Parity harness validated |
| m-E22-02-chunked-evaluation | Chunked Evaluation | not started | m-E22-01 (sequencing only; no hard code dependency); M-002 session protocol (already delivered) |
| m-E22-03-pipeline-sdk | `FlowTime.Pipeline` SDK Wrapper | not started | m-E22-01 + m-E22-02 complete (SDK reflects the final surface) |

## ADRs

- (none yet — ADRs will be captured under `work/decisions.md` as they arise during milestone planning)

## References

- E-18 epic spec: `work/epics/E-18-headless-pipeline-and-optimization/spec.md`
- E-18 gap analysis: `work/epics/E-18-headless-pipeline-and-optimization/e18-gap-analysis.md`
- Analysis modes architecture: `docs/architecture/time-machine-analysis-modes.md`
- Telemetry Loop & Parity: `work/epics/unplanned/telemetry-loop-parity/spec.md`
- E-15 Telemetry Ingestion: `work/epics/E-15-telemetry-ingestion/`
- Option A delivery sequence: `work/decisions.md` → D-045
- Headless engine architecture: `docs/architecture/headless-engine-architecture.md`
