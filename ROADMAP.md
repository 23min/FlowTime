# Roadmap

## E-10 — Engine Correctness & Analytical Primitives (done)

### Goal

Fix known correctness bugs, harden engineering quality, and build the analytical primitives layer that enables downstream epics (Path Analysis, Anomaly Detection, Scenario Overlays, UI Analytical Views) to deliver their full value.

| Milestone | Title | Status |
|---|---|---|
| M-054 | Engineering Foundation (Phase 1) | done |
| M-055 | Phase 2 — Documentation Honesty | done |
| M-056 | Phase 3a — Cycle Time & Flow Efficiency | done |
| M-057 | Phase 3a.1 — Analytical Projection Hardening | done |
| M-058 | Phase 3b — WIP Limits | done |
| M-059 | Phase 3c — Variability Preservation (Cv + Kingman) | done |
| M-060 | Phase 3d — Constraint Enforcement | done |

## E-11 — Svelte UI — Parallel Frontend Track (done)

### Goal

Build a SvelteKit + shadcn-svelte application in parallel with the Blazor WebAssembly frontend, delivering a polished, modern UI for demos and future evaluation while keeping the existing .NET backend APIs untouched.

| Milestone | Title | Status |
|---|---|---|
| M-061 | Project Scaffold & Shell | done |
| M-062 | Run Orchestration | done |

## E-12 — Dependency Constraints & Shared Resources (done)

### Goal

Model downstream dependencies (databases, caches, external APIs, shared services) as **constraints** that can limit throughput and introduce hidden backlog/latency. Preserve FlowTime’s minimal basis (arrivals/served/queue depth) while making coupling and bottlenecks visible.

| Milestone | Title | Status |
|---|---|---|
| M-063 | Dependency Constraints Foundations | done |
| M-064 | Dependency Constraints (Attached to Services) | done |
| M-065 | MCP Dependency Pattern Enforcement | done |

## E-13 — Path Analysis & Subgraph Queries (proposed)

_No milestones yet._

## E-14 — Visualizations (Chart Gallery / Demo Lab) (cancelled)

_No milestones yet._

## E-15 — Telemetry Ingestion, Topology Inference, and Canonical Bundles (proposed)

### Goal

Build the pipeline that takes real-world data — event logs, traces, sensor feeds — and produces the two things FlowTime needs: a `/graph` topology and Gold-format time-binned series. This epic owns ingestion, topology inference, validation, and bundle assembly.

_No milestones yet._

## E-16 — Formula-First Core Purification (done)

### Goal

Purify FlowTime's execution boundary so semantic meaning and analytical truth are compiled into Core once and consumed as facts everywhere else. This epic turns the existing "spreadsheet for flows" mental model into an enforceable architecture: parser/compiler resolve references, the core evaluates pure vector formulas, and adapters and clients stop reconstructing domain meaning from strings.

| Milestone | Title | Status |
|---|---|---|
| M-012 | Compiled Semantic References | done |
| M-013 | Class Truth Boundary | done |
| M-014 | Runtime Analytical Descriptor | done |
| M-015 | Core Analytical Evaluation | done |
| M-016 | Analytical Warning Facts and Primitive Cleanup | done |
| M-017 | Analytical Contract and Consumer Purification | done |

## E-17 — Interactive What-If Mode (done)

### Goal

Enable live, interactive recalculation in FlowTime — change a parameter and see results update instantly across the entire model, like a spreadsheet.

| Milestone | Title | Status |
|---|---|---|
| M-018 | WebSocket Engine Bridge | done |
| M-019 | Svelte Parameter Panel | done |
| M-020 | Live Topology and Charts | done |
| M-021 | Warnings Surface | done |
| M-022 | Edge Heatmap | done |
| M-023 | Time Scrubber | done |

## E-18 — Time Machine (done)

### Goal

Make FlowTime usable as a pure callable function — embeddable in pipelines, optimization loops, model discovery workflows, and digital twin architectures. The **Time Machine** (`FlowTime.TimeMachine`) is a new first-class execution component that scripts, UIs, MCP servers, and AI agents can drive programmatically. It owns compile, tiered validation, evaluate, reevaluate with parameter overrides, and canonical artifact write.

FlowTime's execution component is an abstract machine in the BEAM / JVM sense: instructions (the compiled graph), state (the time grid plus accumulating series), deterministic topological stepping through time. "Time Machine" also aligns with the existing Blazor "Time Travel" UI feature — the Time Travel UI navigates runs the Time Machine produces — and the reevaluation semantics (rewind a compiled model, run it forward with different parameters) are literally time travel.

| Milestone | Title | Status |
|---|---|---|
| M-001 | Parameterized Evaluation | done |
| M-002 | Engine Session + Streaming Protocol | done |
| M-003 | Tiered Validation | done |
| M-004 | Generator Extraction → TimeMachine | done |
| M-005 | ITelemetrySource Contract | done |
| M-006 | Parameter Sweep | done |
| M-007 | Sensitivity Analysis | done |
| M-008 | Goal Seeking | done |
| M-009 | Multi-parameter Optimization | done |
| M-010 | SessionModelEvaluator | done |
| M-011 | .NET Time Machine CLI | done |

## E-19 — Surface Alignment & Compatibility Cleanup (done)

### Goal

Tighten the remaining non-analytical legacy and compatibility surfaces after E-16 so FlowTime exposes current Engine/Sim contracts consistently across first-party UI, Sim, docs, schemas, and examples without carrying stale fallback layers or stripping supported Blazor capability.

| Milestone | Title | Status |
|---|---|---|
| M-024 | Supported Surface Inventory, Boundary ADR & Exit Criteria | done |
| M-025 | Sim Authoring & Runtime Boundary Cleanup | done |
| M-026 | Schema, Template & Example Retirement | done |
| M-027 | Blazor Support Alignment | done |

## E-20 — Matrix Engine (done)

### Goal

Replace the C# object-graph evaluation engine with a Rust-based column-store + evaluation-plan engine. The new engine reads the same YAML model files, produces identical output artifacts, and ships as a standalone CLI binary (`flowtime-engine`). This is the foundation for E-17 (Interactive What-If) and E-18 (Time Machine).

| Milestone | Title | Status |
|---|---|---|
| M-028 | Scaffold, Types, and Parsers | done |
| M-029 | Compiler and Core Evaluator | done |
| M-030 | Topology and Sequential Ops | done |
| M-031 | Routing and Constraints | done |
| M-032 | Derived Metrics and Analysis | done |
| M-033 | Artifacts, CLI, and Integration | done |
| M-034 | .NET Subprocess Bridge | done |
| M-035 | Full Parity Harness | done |
| M-036 | Per-Class Decomposition and Edge Series | done |
| M-037 | Artifact Sink Parity | done |

## E-21 — Svelte Workbench & Analysis Surfaces (done)

### Goal

Transform the Svelte UI from a Blazor-parallel clone into the primary platform for expert flow analysis and Time Machine surfaces, using a workbench paradigm (topology as navigation + inspection panel) instead of the Blazor overlay approach.

| Milestone | Title | Status |
|---|---|---|
| M-038 | Workbench Foundation | done |
| M-039 | Metric Selector & Edge Cards | done |
| M-040 | Sweep & Sensitivity Surfaces | done |
| M-041 | Goal Seek Surface | done |
| M-042 | Optimize Surface | done |
| M-043 | Heatmap View | done |
| M-044 | Validation Surface (Svelte) | done |
| M-045 | Visual Polish & Dark Mode QA | done |

## E-22 — Time Machine — Model Fit & Chunked Evaluation (proposed)

### Goal

Close out the remaining Time Machine analysis modes — **model fitting** against real telemetry and **chunked evaluation** for feedback simulation — and crystallize the resulting surface as a clean embeddable **`FlowTime.Pipeline` SDK**. These are the last two analysis modes in the E-18 Time Machine architecture; delivering them completes the "FlowTime as a callable function" arc.

_No milestones yet._

## E-23 — Model Validation Consolidation (done)

### Goal

Make `docs/schemas/model.schema.yaml` the **only declarative source of structural truth** about the post-substitution model, and `ModelSchemaValidator` the **only runtime evaluator**. Eliminate every "embedded schema" — every place outside the canonical schema where model rules are re-encoded. After E-23 closes:

- One schema. Declared in `model.schema.yaml`.
- One validator. `ModelSchemaValidator.Validate`, with named adjuncts (alongside `ValidateClassReferences`) for any rule JSON Schema draft-07 cannot express.
- Zero parallel imperative validators. `ModelValidator.cs` is deleted.
- Every rule has exactly one canonical home. No silent rules in parsers, emitters, or post-parse orchestration paths.

| Milestone | Title | Status |
|---|---|---|
| M-046 | Rule-Coverage Audit | done |
| M-047 | Call-Site Migration | done |
| M-048 | Delete `ModelValidator` | done |

## E-24 — Schema Alignment (done)

### Goal

Unify FlowTime's post-substitution model representation. One C# type. One YAML schema. One validator. `SimModelArtifact` is **deleted**. Sim builds the unified model type directly; the Engine accepts and parses the same type. Every field has exactly one declaration site. `TemplateWarningSurveyTests` reports `val-err=0` across all twelve templates at `ValidationTier.Analyse`, promoted to a hard build-time assertion. `ModelValidator` deletion (E-23) then becomes a mechanical cleanup.

| Milestone | Title | Status |
|---|---|---|
| M-049 | Inventory and Design Decisions | done |
| M-050 | Unify Model Type | done |
| M-051 | Schema Unification | done |
| M-052 | Parser/Validator Scalar-Style Fix | done |
| M-053 | Canary Green and Hard Assertion | done |

## E-25 — Engine Truth Gate — Edge-Flow Authority + Golden-Output Canary (proposed)

### Goal

Resolve the engine-correctness investigation surfaced during E-21 dogfooding (G-032 + G-033) and lock down testing rigor before further engine evolution. Concretely: make a defensible design call on edge-flow authority (expr nodes vs. topology edge weights), align engine + shipped templates so the conservation invariant is clean, and promote the lightweight `Survey_Templates_For_Warnings` baseline canary into a strict per-template **golden-output** canary that compares numeric series + warning sets at a sanctioned baseline.

| Milestone | Title | Status |
|---|---|---|
| M-066 | Edge-Flow Authority Decision | draft |
| M-067 | Engine + Template Alignment | draft |
| M-068 | Golden-Output Canary | draft |

