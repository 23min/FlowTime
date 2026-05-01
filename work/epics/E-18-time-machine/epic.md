---
id: E-18
title: Time Machine
status: active
---

> **Naming note.** This epic was originally filed as "Headless Pipeline and Optimization." The component is now named `FlowTime.TimeMachine` (the Time Machine). The directory path `work/epics/E-18-headless-pipeline-and-optimization/` is preserved for historical stability; cross-doc references use that path. The decision is recorded in `work/decisions.md` and in `work/epics/E-19-surface-alignment-and-compatibility-cleanup/m-E19-01-supported-surface-inventory.md` (A6 + shared framing).

## Goal

Make FlowTime usable as a pure callable function — embeddable in pipelines, optimization loops, model discovery workflows, and digital twin architectures. The **Time Machine** (`FlowTime.TimeMachine`) is a new first-class execution component that scripts, UIs, MCP servers, and AI agents can drive programmatically. It owns compile, tiered validation, evaluate, reevaluate with parameter overrides, and canonical artifact write.

FlowTime's execution component is an abstract machine in the BEAM / JVM sense: instructions (the compiled graph), state (the time grid plus accumulating series), deterministic topological stepping through time. "Time Machine" also aligns with the existing Blazor "Time Travel" UI feature — the Time Travel UI navigates runs the Time Machine produces — and the reevaluation semantics (rewind a compiled model, run it forward with different parameters) are literally time travel.

## Context

FlowTime's engine is deterministic: given a model, parameters, and input series, it produces the same output every time. After E-16 purifies the engine into a compiled, typed, pure evaluation surface, treating it as a callable function becomes natural:

```
f(model, parameters, inputs) → outputs
```

This is the same relationship a circuit simulator (SPICE) has with its netlist: compile once, evaluate many times, vary parameters programmatically. SPICE built an entire ecosystem of analysis modes on this foundation — parameter sweeps, Monte Carlo, optimization, model fitting. FlowTime can do the same for queueing networks.

This epic owns the shared runtime parameter foundation used by both the programmable Time Machine layer and the interactive UI layer. The parameter model, override surface, and reevaluation API should be built once here and consumed by E-17, not implemented twice.

## The core insight

Every advanced use case is a composition of "call the evaluation function with different inputs":

| Use case | Loop shape |
|----------|-----------|
| **Parameter sweep** | Evaluate N times with different parameter values, compare outputs |
| **Optimization** | Vary parameters to minimize an objective function over FlowTime outputs |
| **Sensitivity analysis** | Perturb each parameter, measure output change (numerical gradient) |
| **Model discovery** | Fit model parameters to match observed telemetry (inverse modeling) |
| **Monte Carlo** | Sample parameters from distributions, evaluate N times, characterize output distribution |
| **Digital twin** | Continuously calibrate model from production telemetry, use for prediction and what-if |
| **Feedback simulation** | Evaluate chunk by chunk, let a controller (autoscaler, circuit breaker) adjust parameters between chunks |

## Scope

### In Scope

- **Time Machine CLI mode** — FlowTime as a pipeline-friendly command: model + params in, results out (JSON/CSV)
- **Shared runtime parameter foundation** — compiled parameter identities, override points, reevaluation API, and optional enrichment contract for template-authored parameter metadata reused by E-17
- **Iteration protocol** — keep compiled graph alive, accept new parameter sets per iteration without recompile
- **Tiered validation as a first-class operation** — schema / compile / analyse tiers callable from the same Time Machine surface as compile/evaluate/reevaluate. Client-agnostic: Sim UI, Blazor UI, Svelte UI, MCP servers, external AI agents, tests, and CI are all first-class callers on equal footing. Detailed requirement below in *Tiered validation (required scope)*. Originates from E-19 M-024 decision A6.
- **Parameter sweep** — evaluate over a grid of parameter values
- **Optimization** — find parameter values that minimize/maximize an objective subject to constraints
- **Model fitting** — given observed telemetry, calibrate model parameters to match (system identification)
- **Sensitivity analysis** — compute ∂output/∂parameter numerically
- **Pipeline SDK** — `FlowTime.Core` as the embeddable evaluation library, surfaced via `FlowTime.TimeMachine` as the first-class execution component with a clean API
- **Chunked evaluation** — evaluate bins in chunks for feedback simulation (autoscaler, circuit breaker scenarios)

Real-telemetry fitting/optimization is part of this epic, but it is not the first cut: when E-18 consumes real telemetry rather than synthetic or fixture data, that branch should sit downstream of E-15's first dataset path and Telemetry Loop & Parity so calibration is grounded in measured replay drift.

### Out of Scope

- UI for interactive parameter exploration (E-17)
- WebSocket/SignalR push channel (E-17)
- New analytical primitives
- Rewriting the DAG evaluator foundation
- Chunked/stateful execution semantics in the first Time Machine cut — treat them as a later layer once a dedicated streaming/stateful seam exists

## Execution Layers

To minimize risk, execute this epic in three layers:

1. **Foundation layer:** shared runtime parameter foundation + evaluation SDK + tiered validation + Time Machine CLI / sidecar.
2. **Analysis layer:** parameter sweep, sensitivity, optimization, and fitting on top of the foundation.
3. **Stateful layer:** chunked evaluation and richer telemetry adapters only after a dedicated streaming/stateful execution seam exists.

## Tiered validation (required scope)

**Origin.** This requirement was decided in E-19 M-024 as decision A6. E-19 retires the existing `POST /api/v1/drafts/validate` endpoint (Sim-private, mislabeled, unused by any UI, only exercised by tests) and records a hard dependency that E-18 must expose validation as a first-class, client-agnostic Time Machine operation alongside compile, evaluate, reevaluate, parameter override, and artifact write.

**Principle.** Validation — answering *"is this YAML a correct FlowTime model?"* — is a first-class, client-agnostic operation. `FlowTime.Core` owns the authoritative answer. The Time Machine surfaces it. No single client is privileged as the validation host. Sim UI, Blazor UI, Svelte UI, MCP servers, external AI agents, tests, and CI are all first-class callers on equal footing.

**Three tiers.** The Time Machine must expose all three of the following through its in-process SDK, CLI, and sidecar protocol, with consistent request and response shapes:

- **Tier 1 — schema.** YAML parses, JSON schema validates, class references resolve. Cheap, no compile. Intended for per-keystroke editor feedback and per-iteration AI inner-loop feedback. Backed by `FlowTime.Core.Models.ModelSchemaValidator`.
- **Tier 2 — compile.** Model compiles into a `Graph`: topology resolves, dependencies resolve, expression nodes compile. No execution. Catches structural errors. Backed by `FlowTime.Core.Compiler.ModelCompiler` + `FlowTime.Core.Models.ModelParser`.
- **Tier 3 — analyse.** Full invariant analysis: compile + deterministic evaluation + invariant checks (capacity, conservation, runtime warnings). Catches semantic issues that only emerge after evaluation. Backed by `FlowTime.Sim.Core.Analysis.TemplateInvariantAnalyzer` composed into the Time Machine (the analyzer logic is correct; only its hosting moves).

**Why tiered is required, not optional.** Without cheap tiers, every client needing "just check this" pays the full evaluate cost. That:
- Breaks AI inner-loop performance (agents generating candidate models iterate thousands of times).
- Makes editor-time UX expensive (IDE red-squiggles on every keystroke is unacceptable if each keystroke compiles and evaluates a graph).
- Discourages clients from validating at all, which is worse than cheap validation.

Validation and compile-only are natural siblings of compile-then-evaluate. They share the front end of the pipeline and differ only in where they stop.

**Client list (none privileged):**
- **Sim UI** (Blazor, Svelte) — template authoring, editor-time feedback.
- **Blazor UI, Svelte UI** — editor-time feedback on inline YAML, pre-run "check" action.
- **MCP servers** — expose `validate_model` (and friends) as tools for Claude and other models to call.
- **External AI agents** — programmatic inner loop: generate → validate (tier 1 or 2) → refine → validate (tier 3) → run.
- **Tests, CI** — pre-run well-formedness gates, regression checks on model fixtures.
- **The Time Machine itself** — compile operations share the tier-2 path.

**What this milestone does not design.** Concrete wire format (JSON shape of the validation response, error envelope, line/column mapping) is a Foundation Layer implementation detail. What must be true is that the three tiers exist, are callable via all three surfaces (SDK, CLI, sidecar), and treat every client the same.

**Library pieces preserved by E-19 for E-18 to compose:**
- `FlowTime.Core.Models.ModelSchemaValidator` (tier 1)
- `FlowTime.Core.Models.ModelValidator` (tier 2 adjacent — schemaVersion/grid/structure + legacy field detection)
- `FlowTime.Core.Compiler.ModelCompiler`, `FlowTime.Core.Models.ModelParser` (tier 2)
- `FlowTime.Sim.Core.Analysis.TemplateInvariantAnalyzer`, `InvariantAnalyzer` (tier 3 logic)

These stay intact. E-19 only removes the HTTP wrapper on Sim; the validation capability itself moves forward to compose into the Time Machine.

## Analysis Modes (SPICE-inspired)

### Mode 1: Sweep

```bash
flowtime evaluate model.yaml --sweep "parallelism=1,2,4,8,16" --output sweep-results.json
```

Embarrassingly parallel. Produces a table of (parameter_value → key_metrics).

### Mode 2: Optimize

```bash
flowtime optimize model.yaml \
  --objective "min(avg(node.queue.queueTimeMs))" \
  --constraint "max(node.queue.utilization) < 0.8" \
  --vary "parallelism=1..32" "serviceRate=50..500" \
  --output optimal.json
```

Gradient-free optimization (Nelder-Mead, Bayesian) over FlowTime as the evaluation function.

### Mode 3: Fit

```bash
flowtime fit model.yaml --observed production-metrics.json \
  --fit-params "serviceRate,routingWeight" \
  --output calibrated-model.yaml
```

System identification: given real telemetry, find the parameter values that make the model match reality. Uses least-squares or similar fitting over the residual between predicted and observed series.

### Mode 4: Sensitivity

```bash
flowtime sensitivity model.yaml \
  --params "parallelism,serviceRate,arrivalRate" \
  --metric "avg(queueTimeMs)" \
  --perturbation 0.05 \
  --output sensitivity.json
```

Numerical gradient: perturb each parameter by ±5%, measure output change. Answers "which parameter has the most impact on latency?"

### Mode 5: Monte Carlo

```bash
flowtime montecarlo model.yaml \
  --distribution "serviceRate=normal(100,15)" \
  --distribution "arrivalRate=normal(80,10)" \
  --samples 1000 \
  --output distribution.json
```

Stochastic parameter variation. Answers "given uncertainty in our estimates, what's the 95th percentile of queue time?"

### Mode 6: Feedback / chunked evaluation

```bash
flowtime simulate model.yaml --chunked --chunk-size 60 \
  --controller autoscaler.py \
  --output trace.json
```

Evaluate 60 bins, pass state to controller script, controller adjusts parameters, evaluate next 60 bins. Simulates closed-loop control.

## Telemetry integration

Telemetry is an adapter concern that lives **outside** the Time Machine's pure execution scope, with one exception: writing the canonical bundle format is a Time Machine core capability (see below). Everything else — ingesting Prometheus metrics, OpenTelemetry traces, custom event logs, real-world capture feeds — lives in adapter projects that depend on the Time Machine, not inside it.

### Two distinct artifact kinds

- **Canonical run directory** (`data/runs/<runId>/model/`, `series/`, `run.json`) — the Time Machine's internal operational truth for a run. Always written unconditionally on every run. **Clear-text, debuggable, inspectable.** Used by the Query API, Time Travel UI, run listings, and human debugging. This is FlowTime's authoritative in-place run record.
- **Canonical bundle** (`model.yaml`, `manifest.json`, `series/index.json`, CSV files) — a portable, interchange-oriented artifact defined by the E-15 canonical bundle schema (`docs/schemas/telemetry-manifest.schema.json`). Written on demand, not on every run. **Different shape from the canonical run directory, and intentionally so**: the bundle's purpose (portable telemetry interchange, telemetry loop participation, archival) is distinct from the run directory's purpose (debugging, internal operational truth). The bundle format may evolve independently of the run directory format.

The canonical run directory and the canonical bundle are **not two representations of the same thing**. They share content (both contain the model and the series) but their shapes, purposes, and lifecycles differ. Both are preserved.

### The telemetry loop (established vocabulary)

See the `telemetry-loop-parity` epic at `work/epics/telemetry-loop-parity/spec.md` for the authoritative definition. In brief:

> 1. Model and Sim produce a **baseline run** (canonical run directory on disk).
> 2. **Telemetry capture** generates a canonical bundle from that baseline (Time Machine core capability).
> 3. **Telemetry replay** creates a new run from the bundle (`ITelemetrySource` implementation for the canonical bundle, fed into the Time Machine as input).
> 4. Outputs of the baseline run and the replay run are compared for **parity** (owned by the Telemetry Loop & Parity epic).

The loop has three distinct primary use cases:

1. **Specification / bootstrap.** You have a model but no real telemetry yet. Capture generates telemetry in the canonical format; the generated telemetry becomes the instrumentation specification for the real system — "these are the series at this cadence that the real system must emit so a replay produces the same result."
2. **Self-consistency testing.** You have a model and a capture+replay pair. Round-trip the model through the loop and verify parity. Drift is a bug in capture, replay, or the Time Machine's determinism.
3. **AI iteration / model fitting.** An AI agent proposes a candidate model. Capture generates telemetry from the candidate. Compare the generated telemetry to **real observed telemetry** from production. Adjust the model and iterate. The Time Machine provides the forward model; the loop provides the comparison surface.

All three use cases require the capture and replay sides to be **round-trip consistent** for the canonical bundle format, modulo documented tolerances (owned by the Telemetry Loop & Parity epic).

### Contract asymmetry: `ITelemetrySource` yes, `ITelemetrySink` deferred

- **`ITelemetrySource` is introduced by E-18 m-E18-01b** as the input contract. (m-E18-01a extracts the concrete `CanonicalBundleSource` reader from Generator without an interface; 01b lifts it to implement `ITelemetrySource` and adds a second implementation.) Multiple implementations from day one of 01b:
  - `CanonicalBundleSource` — reads canonical bundles (the telemetry loop's replay step). Concrete class created in 01a, lifted to implement `ITelemetrySource` in 01b.
  - `FileCsvSource` — reads `file:`-referenced CSV data (already what Core does today for model inputs). Extracted to implement `ITelemetrySource` in 01b.
  - Future: `FlowTime.Telemetry.Prometheus`, `FlowTime.Telemetry.Otel`, `FlowTime.Telemetry.BpiEventLog`, and other real-world ingestion adapters — **direct-source bypasses** of the E-15 Gold Builder pipeline. They implement `ITelemetrySource` against a live source without writing a canonical bundle to disk, and are alternatives to E-15's general path (raw → Gold → `CanonicalBundleSource`), not part of E-15 scope. Deferred until a concrete client need surfaces.
  - The contract must carry enough metadata to round-trip the canonical bundle format losslessly (modulo documented precision/format drift). This is a non-trivial design task in m-E18-01b, not a throwaway tiny interface.
- **`ITelemetrySink` is explicitly deferred.** Only one sink format exists — the canonical bundle — and writing it is a Time Machine core capability, not a pluggable adapter. Canonical bundle writing is always done via a concrete `CanonicalBundleWriter` inside the Time Machine, not behind an interface. An `ITelemetrySink` interface will be introduced only when a second sink format is required (speculative — Prometheus push, OTEL emit, custom external system push). Per the "don't create abstractions for one-time operations" principle, no sink interface is built on speculation.

### Determinism boundary is at the source

Live telemetry adapters (Prometheus queries, OTEL streams, Kafka consumers) are non-deterministic by nature. FlowTime's determinism story requires the Time Machine to see deterministic inputs. Resolution: **adapters snapshot live data at a well-defined point and expose the snapshot through `ITelemetrySource`**. The Time Machine sees only the snapshot, not the live feed. Snapshot provenance (source, query, timestamp, hash) is recorded in the run artifacts so the run can be reproduced exactly.

This matches today's capture-directory flow, generalised: the adapter can produce the snapshot however it wants.

### Milestone ownership

- **m-E18-01a** (Path B core cut) extracts the concrete canonical bundle writer and concrete `CanonicalBundleSource` reader from Generator into the Time Machine, alongside the execution-pipeline extraction. No `ITelemetrySource` interface yet — the reader is a concrete class. This is enough to enable the telemetry loop end-to-end over the canonical bundle format using today's existing capture and replay code, just rehosted.
- **m-E18-01b** (Tiered Validation & Telemetry Source Contract) introduces `ITelemetrySource` as the formal interface. Lifts `CanonicalBundleSource` to implement it. Adds `FileCsvSource` as a second implementation. Tiered validation lands in this milestone.
- **M-003** (reshaped from "Telemetry I/O" to "Telemetry Ingestion Source Adapters") delivers source-only adapters for real-world formats. Depends on the `ITelemetrySource` contract from 01b. No Time Machine changes. No sinks. Specific formats (Prometheus, OTEL, BPI event logs, GTFS, …) chosen when the milestone is scheduled, not now.
- **m-E18-04** (Optimization & Fitting) depends on the Telemetry Loop & Parity epic being complete — optimization against real telemetry requires measured drift bounds. This is a hard prerequisite, not a soft one.

### Non-goals for E-18

- **No real-world format sinks.** FlowTime does not generate Prometheus-format or OTEL-format telemetry from runs. If ever needed, add later as optional downstream adapters behind `ITelemetrySink` when it exists.
- **No parity harness.** Drift measurement, tolerance rules, CI gating, regression reporting all belong to the Telemetry Loop & Parity epic.
- **No topology inference from ingested telemetry.** Owned by E-15's Graph Builder.
- **No Gold Builder pipeline.** Owned by E-15.

## Architecture

```
FlowTime.Core (E-16: pure compiled engine; unchanged by E-18)
├── ModelSchemaValidator.Validate(yaml) → ValidationResult         (tier 1)
├── ModelCompiler.Compile(model) → CompiledModel                   (tier 2 front)
├── ModelParser.ParseModel(compiled) → (TimeGrid, Graph)           (tier 2 back)
├── Graph.Evaluate(grid) → EvaluatedState
├── Analyze(state) → AnalyticalFacts
│
│  E-17/E-18 shared foundation:
├── IdentifyParameters(graph) → Parameter[]
├── Reevaluate(graph, param_overrides) → EvaluatedState
│
│  E-18 specific:
├── EvaluateChunk(graph, state_at_t, bins[t..t+n]) → state_at_t+n
└── ComputeObjective(state, objective_expr) → double

FlowTime.TimeMachine (NEW — execution component)
├── ValidateSchema(yaml) → Result                                  (tier 1, from Core)
├── ValidateCompile(yaml) → Result                                 (tier 2, from Core)
├── Analyse(yaml) → Result                                         (tier 3, composes TemplateInvariantAnalyzer)
├── Compile(yaml) → CompiledGraphHandle
├── Evaluate(handle, params, seed) → Run
├── Reevaluate(handle, overrides) → Run
├── WriteRunDirectory(run, path) → RunId                           (canonical run dir, always written, clear-text)
├── WriteCanonicalBundle(run) → BundlePath                         (canonical bundle, on demand, for telemetry loop)
├── ITelemetrySource interface                                     (input contract, multiple implementations)
├── CanonicalBundleSource : ITelemetrySource                       (replay side of the telemetry loop)
├── FileCsvSource : ITelemetrySource                               (file: references in models)
├── CLI with pipeline-friendly I/O
├── Iteration protocol (keep graph alive across many evaluate/reevaluate calls)
├── Sidecar protocol (optional) — long-lived process driven over a wire protocol
└── Analysis mode dispatch (sweep, optimize, fit, sensitivity, montecarlo, chunked)

FlowTime.Pipeline (NEW — embeddable SDK; thin layer over the Time Machine)
├── Sweep(graph, param_grid) → results[]
├── Optimize(graph, objective, constraints, ranges) → optimal
├── Fit(graph, observed, fit_params) → calibrated
├── Sensitivity(graph, params, perturbation) → gradients
├── MonteCarlo(graph, distributions, N) → distribution[]
└── ChunkedEvaluate(graph, chunk_size, controller_fn) → trace

FlowTime.Telemetry.* (NEW — adapter projects, source-only, real-world ingestion)
├── FlowTime.Telemetry.Prometheus  : ITelemetrySource              (future, m-E18-06 / E-15)
├── FlowTime.Telemetry.Otel        : ITelemetrySource              (future, m-E18-06 / E-15)
└── FlowTime.Telemetry.BpiEventLog : ITelemetrySource              (future, m-E18-06 / E-15)

FlowTime.Generator (DELETED in E-18)
└── Execution code → FlowTime.TimeMachine
└── Telemetry-generation code → canonical bundle writer + CanonicalBundleSource (in TimeMachine or its own adapter)
└── See "Generator migration" below

ITelemetrySink (DEFERRED — not introduced until a second sink format exists)
└── Canonical bundle writing is a concrete Time Machine capability, not a pluggable adapter.
```

## Core and Time Machine relationship

Core and Time Machine are strictly layered. The dependency direction is **Time Machine → Core, never reverse.**

- **`FlowTime.Core` is the library of pure deterministic operations.** In BEAM/JVM terms it is the instruction set and execution kernel as a library: `ModelSchemaValidator` (tier 1), `ModelCompiler` + `ModelParser` (tier 2), `Graph.Evaluate` (the execution kernel), expression compilation, analytical facts, and invariant analyzer rules. No HTTP, no orchestration, no storage, no client awareness. Core does not know what a "client" is.
- **`FlowTime.TimeMachine` is the hosted machine.** It loads programs (YAML models → compiled graphs via Core), drives them through time (via Core's `Graph.Evaluate`), manages iteration and reevaluation protocols, handles parameter identity and override, writes canonical artifacts, and exposes the whole thing as a client-agnostic API with three surfaces (SDK, CLI, sidecar). The Time Machine is where the abstract machine's hosting concerns live: state lifetimes, run identity, RNG seeding, artifact layout, and multi-client API shapes.
- **The Time Machine composes; it never reimplements.** If the Time Machine needs a pure computational primitive that Core is missing, the primitive is added to Core as a pure library function, not to the Time Machine as a parallel implementation. This preserves Core's invariants and prevents two sources of truth for any given computation.
- **Core remains pure and stable.** Nothing added by E-18 gives Core HTTP, orchestration, storage, or client awareness. Core stays UI-less, host-less, and I/O-less beyond YAML parsing.

## Generator migration (Path B: extraction and deletion)

**Origin.** This is recorded as decision D-032 and referenced from E-19 M-024's shared framing (item 3). The forward fate of `FlowTime.Generator` was previously left implicit.

**Current state (pre-E-18).** `FlowTime.Generator` is the shared orchestration layer used by both `FlowTime.Sim.Service` and `FlowTime.API`. It owns `RunOrchestrationService`, `RunArtifactWriter`, deterministic run ID logic, RNG seeding, and dry-run/plan mode. Sim.Service does not reference `FlowTime.Core` directly — only via Generator. API references both Core and Generator.

**Problem.** Generator's responsibilities — compile, evaluate, artifact write, run IDs, RNG seeding, dry-run — overlap the Time Machine's scope almost completely. Keeping Generator alive alongside the Time Machine would create two shared orchestration layers doing the same pipeline, which violates the no-coexistence discipline established in E-16 and E-19.

**Decision (Path B — extraction and deletion).** In E-18 m-E18-01a (the dedicated Path B cut), the following is done in a single milestone:

1. **Extract execution-pipeline code into `FlowTime.TimeMachine`** (new project):
   - `RunOrchestrationService` → Time Machine's Compile + Evaluate + ArtifactWrite operations (split along the tier boundary)
   - `RunArtifactWriter` → Time Machine's canonical run directory writer (always written, unchanged from today's clear-text layout — preserved for debugging value)
   - `RunDirectoryUtilities`, `RunOrchestrationContractMapper` → Time Machine supporting infrastructure
   - Deterministic run ID logic → Time Machine's run identity service
   - RNG seeding → Time Machine's parameter and run configuration
   - Dry-run / plan mode → Time Machine's tier 2 validation (compile-only)
   - Simulation-mode code → Time Machine's evaluate path

2. **Extract telemetry-generation code into the canonical bundle adapter** (new project `FlowTime.Telemetry.Bundle`, or as part of the Time Machine if design review prefers):
   - `TelemetryBundleBuilder`, `TelemetryBundleOptions` → canonical bundle writer. Not behind `ITelemetrySink` (which is deferred) — a concrete writer. Called when the Time Machine is asked to produce a bundle.
   - `TelemetryCapture`, `TelemetryGenerationService` → canonical bundle writing orchestration, combined with the writer.
   - `CaptureManifestWriter` → canonical bundle manifest production (part of the writer).
   - `RunArtifactReader` (under today's Generator `Capture/`) → `CanonicalBundleSource`, the replay-side `ITelemetrySource` implementation for the canonical bundle format.
   - `GapInjector`, `GapInjectorOptions` → realism transform applied inside the canonical bundle adapter (or deferred to the Telemetry Loop & Parity epic if the transform is about driving parity tolerance tests rather than realistic bundle generation). The final location is an m-E18-01a decision.

3. **Update callers**:
   - `FlowTime.Sim.Service` replaces its `FlowTime.Generator` reference with a `FlowTime.TimeMachine` reference. Sim.Service now depends on Time Machine for execution and keeps its own authoring/template responsibilities.
   - `FlowTime.API` replaces its `FlowTime.Generator` reference with `FlowTime.TimeMachine` (or drops the dependency entirely if API only needs Core-level reads).
   - The existing `POST /telemetry/captures` API endpoint and `flowtime telemetry capture` CLI surface are re-wired to call the extracted capture capability at its new home. The public surface does not change as part of Path B; only the implementation moves.

4. **Delete `FlowTime.Generator`** in the same milestone. No stranded empty project. No "Generator and Time Machine coexist" window.

**No coexistence window.** Path B deliberately rejects a transition state in which both `FlowTime.Generator` and `FlowTime.TimeMachine` exist in the tree. The extraction and deletion happen in one milestone cut, matching the E-16 no-coexistence pattern. If a milestone cannot extract cleanly in one pass, the correct response is to resize the milestone, not to introduce a coexistence shim.

**Relationship with Sim's transitional execution host.** During E-19 (before E-18 ships), `FlowTime.Sim.Service`'s `/api/v1/orchestration/runs` endpoint remains the transitional execution host and routes through `FlowTime.Generator`. When E-18 completes the Path B migration, that endpoint is deleted in favour of the UI calling the Time Machine directly by default. A temporary thin facade is allowed only if a concrete technical migration constraint is documented in the owning E-18 milestone, with explicit removal criteria. That choice is made in the E-18 milestone that deletes Generator, not in E-19.

**What Path B does not do.** Path B does not require any change to `FlowTime.Core`, the canonical run directory layout (`data/runs/<runId>/model/`, `series/`, `run.json`), the canonical run.json contract, the canonical bundle schema (`docs/schemas/telemetry-manifest.schema.json`), or the analytical surfaces purified by E-16. Those all stay unchanged. Path B is a project-boundary refactor, not a contract or data refactor. Canonical run directories stay clear-text and debuggable. Canonical bundles stay in their E-15-defined shape (which may evolve independently of the run directory if a future milestone decides the bundle needs a different shape for interchange purposes, but that is not Path B's concern).

## Success Criteria

- [ ] The Time Machine can be called as a CLI in a shell pipeline: `cat model.yaml | flowtime evaluate --params '{"parallelism":4}' | jq '.nodes.queue.derived.queueTimeMs'`
- [ ] The Time Machine exposes tiered validation (schema / compile / analyse) via SDK, CLI, and sidecar, callable identically from Sim UI, Blazor UI, Svelte UI, MCP servers, external AI agents, tests, and CI. No client is privileged.
- [ ] Tier 1 (schema) returns validation results without compiling the model. Tier 2 (compile) returns without executing. Tier 3 (analyse) returns full invariant analysis. Each tier has a consistent request/response shape.
- [ ] Parameter sweeps produce correct comparative results without recompilation per evaluation
- [ ] An optimization loop can find parameter values that satisfy an objective + constraints
- [ ] Model fitting against parity-validated real telemetry produces a calibrated model that predicts within tolerance
- [ ] Chunked evaluation enables feedback simulation with external controller logic
- [ ] All evaluation modes use the pure Core engine through the Time Machine — no adapter-side analytical computation, no Sim-private execution path
- [ ] `FlowTime.Generator` is deleted. Its responsibilities are extracted into `FlowTime.TimeMachine` (and the canonical bundle adapter project) in a single milestone cut with no coexistence window. Sim.Service and API reference `FlowTime.TimeMachine` (or Core directly) instead of Generator. `rg "FlowTime\.Generator" src/ tests/` returns zero matches.
- [ ] The canonical run directory (`data/runs/<runId>/model/`, `series/`, `run.json`) is preserved unchanged. It remains clear-text and debuggable as today.
- [ ] The canonical bundle format (`model.yaml`, `manifest.json`, `series/`, CSV files) is produced by the Time Machine's canonical bundle writer and consumed by `CanonicalBundleSource`, and the two are round-trip consistent: capturing a baseline run and replaying its bundle produces the same outputs modulo documented drift tolerances owned by the Telemetry Loop & Parity epic.
- [ ] `ITelemetrySource` is defined and has at least two implementations at milestone completion: `CanonicalBundleSource` and `FileCsvSource` (for `file:`-referenced CSV model inputs).
- [ ] `ITelemetrySink` is **not** introduced on speculation. It is documented as deferred until a second sink format is required.
- [ ] `FlowTime.TimeMachine` contains no external-telemetry-format-specific code (no Prometheus, OTEL, BPI-format parsing or emission). All external format knowledge lives in adapter projects under `FlowTime.Telemetry.*`. `rg -i "prometheus|opentelemetry|otel" src/FlowTime.TimeMachine/` returns zero matches.
- [ ] The existing `POST /telemetry/captures` API endpoint and `flowtime telemetry capture` CLI command continue to work after Generator deletion, backed by the extracted canonical bundle writer. Their public request/response contracts are unchanged.

## Milestones

Plan v2 (2026-04-10): once the Rust engine (E-20) became the evaluation path, the original C#-Core-centric milestone plan was reshaped. The current milestone structure lives here. `milestone-plan-v2.md` documents the remapping from v1 → v2.

| ID | Title | Status | Summary |
|----|-------|--------|---------|
| M-001 | Parameterized Evaluation (Rust) | **complete** (merged to main 2026-04-10) | `ParamTable` in Plan. Compiler extracts tweakable parameters from const nodes, traffic arrivals, WIP limits. `evaluate_with_params(plan, overrides)` pure function. Parameter metadata (id, kind, default, bounds). Foundation for everything that follows. |
| M-002 | Engine Session + Streaming Protocol (Rust) | **complete** (merged to main 2026-04-10) | `flowtime-engine session` persistent CLI mode. Length-prefixed MessagePack over stdin/stdout. Commands: `compile`, `eval`, `patch`, `get_params`, `get_series`, `validate_schema`. Session holds compiled Plan + current state. |
| M-003 | Tiered Validation | **complete** (merged to main) | `TimeMachineValidator` (schema / compile / analyse tiers); `POST /v1/validate`; Rust `validate_schema` session command. Satisfies E-19 M-024 A6 (D-030). |
| M-004 | FlowTime.TimeMachine Extraction (Path B) | **complete** (merged to main) | `FlowTime.TimeMachine` project created; `FlowTime.Generator` deleted outright. Path B, no coexistence window. Per D-032. |
| M-005 | Telemetry Source Contract | **complete** (merged to main) | `ITelemetrySource` interface + `CanonicalBundleSource` + `FileCsvSource`. 23 tests. `ITelemetrySink` explicitly **not** introduced — see D-033. |
| M-006 | Parameter Sweep | **complete** (merged to main) | `SweepSpec`/`SweepRunner`/`ConstNodePatcher`; `IModelEvaluator` / `RustModelEvaluator`; `POST /v1/sweep`. 35 tests. |
| M-007 | Sensitivity Analysis | **complete** (merged to main) | `ConstNodeReader`; `SensitivitySpec`/`SensitivityRunner` (central difference); `POST /v1/sensitivity`. 39 tests. |
| M-008 | Goal Seeking | **complete** (merged to main) | `GoalSeekSpec`/`GoalSeeker` (bisection); `POST /v1/goal-seek`. 33 tests. (Added 2026-04; not in original plan.) |
| M-009 | Multi-parameter Optimization | **complete** (merged to main) | `OptimizeSpec`/`Optimizer` (Nelder-Mead, N parameters); `POST /v1/optimize`. 29 unit + 10 API tests. |
| M-010 | SessionModelEvaluator | **complete** (merged to epic 2026-04-15) | Persistent `flowtime-engine session` subprocess; MessagePack over stdin/stdout; compile-once/eval-many. `RustEngine:UseSession` config switch (default true); `RustModelEvaluator` retained as fallback. 44 new tests. |
| M-011 | .NET Time Machine CLI | **complete** (merged to epic 2026-04-15) | `flowtime validate/sweep/sensitivity/goal-seek/optimize` as pipeable JSON-over-stdio commands byte-compatible with `/v1/` endpoints. `--no-session` fallback. 72 CLI unit + 10 integration tests. |
| m-E18-XX | Model Fit | **planned** — blocked on E-15 + Telemetry Loop & Parity | `FitSpec`/`FitRunner`/`POST /v1/fit` composing `ITelemetrySource` + `Optimizer`. Infrastructure exists; assembly requires telemetry ingestion (E-15) and parity harness first. |
| m-E18-05 | Chunked Evaluation (Mode 6) | **deferred** — after discovery pipeline works end-to-end | Bin-chunk evaluation for feedback simulation with external controllers. Requires a real stateful execution seam. Sequenced after Model Fit per Option A (D-045). |

### Deferred from v1 (not on current critical path)

These v1 milestones were superseded or deferred when the Rust engine became the evaluation path. Some have since been re-admitted under different IDs (M-003, M-004, M-005 above).

- **m-E18-01a** Generator extraction — superseded by **M-004** (same outcome, different entry point).
- **m-E18-01b** Tiered validation & telemetry source contract — split across **M-003** (validation) and **M-005** (telemetry source contract).
- **m-E18-01c** Runtime parameter foundation — replaced by **M-001** (Rust-native, not C#).
- **m-E18-04** Optimization & Fitting as a single milestone — split into **M-008** (goal seek), **M-009** (N-parameter optimize), and **m-E18-XX** (model fit).
- **Telemetry Ingestion Source Adapters** (v1 M-003 idea) — moved to **E-15** scope; not an E-18 milestone.

## Risks & Open Questions

| Risk / Question | Impact | Mitigation |
|----------------|--------|------------|
| Optimization solver choice (Nelder-Mead vs Bayesian vs gradient-free) | Medium | Start with Nelder-Mead (simple, derivative-free), add Bayesian later |
| Model fitting convergence for complex topologies | High | Start with small models, add diagnostics for fit quality |
| Chunked evaluation requires a real stateful execution seam, not just the current `IStatefulNode` stubs | High | Defer chunked evaluation to the epic's stateful layer; do not block foundation or analysis layers on it |
| Objective expression language design | Medium | Start with simple predefined metrics, add expression support later |
| Telemetry format proliferation | Low | Start with CSV (already supported) and JSON, add OTEL later |

## Dependencies

- **E-16 Formula-First Core Purification** — must complete first. Provides the pure compiled engine that the Time Machine hosts.
- **E-19 M-024 Supported Surface Inventory** — provides the A6 tiered-validation requirement, the Path B Generator extraction commitment (D-032), the telemetry-as-adapter framing (D-033), and the Time Machine naming decision (D-031) that this epic builds on.
- **E-15 Telemetry Ingestion** — provides the canonical bundle schema (`docs/schemas/telemetry-manifest.schema.json`) that this epic's `CanonicalBundleSource` and canonical bundle writer must conform to. The schema already exists; this is an alignment dependency rather than a sequencing dependency.
- **E-17 Interactive What-If Mode** consumes the shared runtime parameter foundation built here; it should not duplicate the runtime parameter model or reevaluation API.
- **Telemetry Loop & Parity** (`work/epics/telemetry-loop-parity/spec.md`, currently unnumbered) — **hard prerequisite for m-E18-04 (Optimization & Fitting)**. Optimization and fitting against real telemetry require measured drift bounds, which only the parity harness can provide. Soft dependency for m-E18-01a through m-E18-03 (those milestones can ship without parity automation, but the loop's existence shapes the contract design in 01b).

## Analogies

FlowTime's relationship to these analysis modes is the same as SPICE's relationship to circuit analysis:
- The engine is the forward model (netlist → simulation)
- Every analysis mode is a different way of calling the forward model
- The engine doesn't need to know about optimization — it just evaluates purely
- The analysis framework wraps the engine with different calling patterns

## References

- SPICE analysis modes (.DC, .AC, .TRAN, .STEP, .MC, .OPTIM) as architectural precedent
- Control theory system identification (Ljung, "System Identification: Theory for the User")
- [work/epics/E-16-formula-first-core-purification/reference/formula-first-engine-refactor-plan.md](../E-16-formula-first-core-purification/reference/formula-first-engine-refactor-plan.md)
- [docs/research/flowtime-headless-integration.md](../../../docs/research/flowtime-headless-integration.md)
