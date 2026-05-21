# Roadmap

## E-0010 — Engine Correctness & Analytical Primitives (done)

### Goal

Fix known correctness bugs, harden engineering quality, and build the analytical primitives layer that enables downstream epics (Path Analysis, Anomaly Detection, Scenario Overlays, UI Analytical Views) to deliver their full value.

| Milestone | Title | Status |
|---|---|---|
| M-0054 | Engineering Foundation (Phase 1) | done |
| M-0055 | Phase 2 — Documentation Honesty | done |
| M-0056 | Phase 3a — Cycle Time & Flow Efficiency | done |
| M-0057 | Phase 3a.1 — Analytical Projection Hardening | done |
| M-0058 | Phase 3b — WIP Limits | done |
| M-0059 | Phase 3c — Variability Preservation (Cv + Kingman) | done |
| M-0060 | Phase 3d — Constraint Enforcement | done |

## E-0011 — Svelte UI — Parallel Frontend Track (done)

### Goal

Build a SvelteKit + shadcn-svelte application in parallel with the Blazor WebAssembly frontend, delivering a polished, modern UI for demos and future evaluation while keeping the existing .NET backend APIs untouched.

| Milestone | Title | Status |
|---|---|---|
| M-0061 | Project Scaffold & Shell | done |
| M-0062 | Run Orchestration | done |

## E-0012 — Dependency Constraints & Shared Resources (done)

### Goal

Model downstream dependencies (databases, caches, external APIs, shared services) as **constraints** that can limit throughput and introduce hidden backlog/latency. Preserve FlowTime’s minimal basis (arrivals/served/queue depth) while making coupling and bottlenecks visible.

| Milestone | Title | Status |
|---|---|---|
| M-0063 | Dependency Constraints Foundations | done |
| M-0064 | Dependency Constraints (Attached to Services) | done |
| M-0065 | MCP Dependency Pattern Enforcement | done |

## E-0013 — Path Analysis & Subgraph Queries (proposed)

_No milestones yet._

## E-0014 — Visualizations (Chart Gallery / Demo Lab) (cancelled)

_No milestones yet._

## E-0015 — Telemetry Ingestion, Topology Inference, and Canonical Bundles (proposed)

### Goal

Build the pipeline that takes real-world data — event logs, traces, sensor feeds — and produces the two things FlowTime needs: a `/graph` topology and Gold-format time-binned series. This epic owns ingestion, topology inference, validation, and bundle assembly.

_No milestones yet._

## E-0016 — Formula-First Core Purification (done)

### Goal

Purify FlowTime's execution boundary so semantic meaning and analytical truth are compiled into Core once and consumed as facts everywhere else. This epic turns the existing "spreadsheet for flows" mental model into an enforceable architecture: parser/compiler resolve references, the core evaluates pure vector formulas, and adapters and clients stop reconstructing domain meaning from strings.

| Milestone | Title | Status |
|---|---|---|
| M-0012 | Compiled Semantic References | done |
| M-0013 | Class Truth Boundary | done |
| M-0014 | Runtime Analytical Descriptor | done |
| M-0015 | Core Analytical Evaluation | done |
| M-0016 | Analytical Warning Facts and Primitive Cleanup | done |
| M-0017 | Analytical Contract and Consumer Purification | done |

## E-0017 — Interactive What-If Mode (done)

### Goal

Enable live, interactive recalculation in FlowTime — change a parameter and see results update instantly across the entire model, like a spreadsheet.

| Milestone | Title | Status |
|---|---|---|
| M-0018 | WebSocket Engine Bridge | done |
| M-0019 | Svelte Parameter Panel | done |
| M-0020 | Live Topology and Charts | done |
| M-0021 | Warnings Surface | done |
| M-0022 | Edge Heatmap | done |
| M-0023 | Time Scrubber | done |

## E-0018 — Time Machine (done)

### Goal

Make FlowTime usable as a pure callable function — embeddable in pipelines, optimization loops, model discovery workflows, and digital twin architectures. The **Time Machine** (`FlowTime.TimeMachine`) is a new first-class execution component that scripts, UIs, MCP servers, and AI agents can drive programmatically. It owns compile, tiered validation, evaluate, reevaluate with parameter overrides, and canonical artifact write.

FlowTime's execution component is an abstract machine in the BEAM / JVM sense: instructions (the compiled graph), state (the time grid plus accumulating series), deterministic topological stepping through time. "Time Machine" also aligns with the existing Blazor "Time Travel" UI feature — the Time Travel UI navigates runs the Time Machine produces — and the reevaluation semantics (rewind a compiled model, run it forward with different parameters) are literally time travel.

| Milestone | Title | Status |
|---|---|---|
| M-0001 | Parameterized Evaluation | done |
| M-0002 | Engine Session + Streaming Protocol | done |
| M-0003 | Tiered Validation | done |
| M-0004 | Generator Extraction → TimeMachine | done |
| M-0005 | ITelemetrySource Contract | done |
| M-0006 | Parameter Sweep | done |
| M-0007 | Sensitivity Analysis | done |
| M-0008 | Goal Seeking | done |
| M-0009 | Multi-parameter Optimization | done |
| M-0010 | SessionModelEvaluator | done |
| M-0011 | .NET Time Machine CLI | done |

## E-0019 — Surface Alignment & Compatibility Cleanup (done)

### Goal

Tighten the remaining non-analytical legacy and compatibility surfaces after E-0016 so FlowTime exposes current Engine/Sim contracts consistently across first-party UI, Sim, docs, schemas, and examples without carrying stale fallback layers or stripping supported Blazor capability.

| Milestone | Title | Status |
|---|---|---|
| M-0024 | Supported Surface Inventory, Boundary ADR & Exit Criteria | done |
| M-0025 | Sim Authoring & Runtime Boundary Cleanup | done |
| M-0026 | Schema, Template & Example Retirement | done |
| M-0027 | Blazor Support Alignment | done |

## E-0020 — Matrix Engine (done)

### Goal

Replace the C# object-graph evaluation engine with a Rust-based column-store + evaluation-plan engine. The new engine reads the same YAML model files, produces identical output artifacts, and ships as a standalone CLI binary (`flowtime-engine`). This is the foundation for E-0017 (Interactive What-If) and E-0018 (Time Machine).

| Milestone | Title | Status |
|---|---|---|
| M-0028 | Scaffold, Types, and Parsers | done |
| M-0029 | Compiler and Core Evaluator | done |
| M-0030 | Topology and Sequential Ops | done |
| M-0031 | Routing and Constraints | done |
| M-0032 | Derived Metrics and Analysis | done |
| M-0033 | Artifacts, CLI, and Integration | done |
| M-0034 | .NET Subprocess Bridge | done |
| M-0035 | Full Parity Harness | done |
| M-0036 | Per-Class Decomposition and Edge Series | done |
| M-0037 | Artifact Sink Parity | done |

## E-0021 — Svelte Workbench & Analysis Surfaces (done)

### Goal

Transform the Svelte UI from a Blazor-parallel clone into the primary platform for expert flow analysis and Time Machine surfaces, using a workbench paradigm (topology as navigation + inspection panel) instead of the Blazor overlay approach.

| Milestone | Title | Status |
|---|---|---|
| M-0038 | Workbench Foundation | done |
| M-0039 | Metric Selector & Edge Cards | done |
| M-0040 | Sweep & Sensitivity Surfaces | done |
| M-0041 | Goal Seek Surface | done |
| M-0042 | Optimize Surface | done |
| M-0043 | Heatmap View | done |
| M-0044 | Validation Surface (Svelte) | done |
| M-0045 | Visual Polish & Dark Mode QA | done |

## E-0022 — Time Machine — Model Fit & Chunked Evaluation (proposed)

### Goal

Close out the remaining Time Machine analysis modes — **model fitting** against real telemetry and **chunked evaluation** for feedback simulation — and crystallize the resulting surface as a clean embeddable **`FlowTime.Pipeline` SDK**. These are the last two analysis modes in the E-0018 Time Machine architecture; delivering them completes the "FlowTime as a callable function" arc.

_No milestones yet._

## E-0023 — Model Validation Consolidation (done)

### Goal

Make `docs/schemas/model.schema.yaml` the **only declarative source of structural truth** about the post-substitution model, and `ModelSchemaValidator` the **only runtime evaluator**. Eliminate every "embedded schema" — every place outside the canonical schema where model rules are re-encoded. After E-0023 closes:

- One schema. Declared in `model.schema.yaml`.
- One validator. `ModelSchemaValidator.Validate`, with named adjuncts (alongside `ValidateClassReferences`) for any rule JSON Schema draft-07 cannot express.
- Zero parallel imperative validators. `ModelValidator.cs` is deleted.
- Every rule has exactly one canonical home. No silent rules in parsers, emitters, or post-parse orchestration paths.

| Milestone | Title | Status |
|---|---|---|
| M-0046 | Rule-Coverage Audit | done |
| M-0047 | Call-Site Migration | done |
| M-0048 | Delete `ModelValidator` | done |

## E-0024 — Schema Alignment (done)

### Goal

Unify FlowTime's post-substitution model representation. One C# type. One YAML schema. One validator. `SimModelArtifact` is **deleted**. Sim builds the unified model type directly; the Engine accepts and parses the same type. Every field has exactly one declaration site. `TemplateWarningSurveyTests` reports `val-err=0` across all twelve templates at `ValidationTier.Analyse`, promoted to a hard build-time assertion. `ModelValidator` deletion (E-0023) then becomes a mechanical cleanup.

| Milestone | Title | Status |
|---|---|---|
| M-0049 | Inventory and Design Decisions | done |
| M-0050 | Unify Model Type | done |
| M-0051 | Schema Unification | done |
| M-0052 | Parser/Validator Scalar-Style Fix | done |
| M-0053 | Canary Green and Hard Assertion | done |

## E-0025 — Engine Truth Gate — Edge-Flow Authority + Golden-Output Canary (active)

### Goal

Resolve the engine-correctness investigation surfaced during E-0021 dogfooding (G-0032 + G-0033) and lock down testing rigor before further engine evolution. Concretely: make a defensible design call on edge-flow authority (expr nodes vs. topology edge weights), align engine + shipped templates so the conservation invariant is clean, and promote the lightweight `Survey_Templates_For_Warnings` baseline canary into a strict per-template **golden-output** canary that compares numeric series + warning sets at a sanctioned baseline.

| Milestone | Title | Status |
|---|---|---|
| M-0066 | Flow-Authority Policy Spike | done |
| M-0067 | Engine + Template Alignment | draft |
| M-0068 | Golden-Output Canary | draft |
| M-0069 | Schema + Compile + Analyse Enforcement | draft |

## E-0026 — Local + CI Test Discipline and Static Analysis (proposed)

### Goal

Bring FlowTime's static-analysis and test-discipline posture up to current best practice across .NET, Rust, and the Svelte UI. Concretely: turn on the toolchain-native quality gates that already exist (analyzers, warnings-as-errors, formatters, clippy, supply-chain advisories), seed the high-leverage test types the codebase does not yet use (property-based, snapshot, mutation, architecture), and make the existing CLAUDE.md process discipline (TDD, branch coverage, truth precedence) machine-checkable rather than self-policed. The result: `main` cannot break silently; new milestones inherit a CI surface that catches the kind of drift CLAUDE.md currently asks reviewers to catch by hand.

| Milestone | Title | Status |
|---|---|---|
| M-0070 | Tier 1: Toolchain hardening and CI gates | draft |
| M-0071 | Tier 2: Property snapshot mutation and architecture testing | draft |
| M-0072 | Tier 3: Fuzzing coverage floor and supply chain scan | draft |

