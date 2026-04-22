---
generated_at: 2026-04-22T12:00:00Z
source_sha: 831b0a3
docs_tree_hash: 156272c8ec932ca67d2ce63d6b56c947030b501ed6f8967bfcf8ad048164fd73
generator: doc-gardening lint:full (bootstrap)
total_files: 93
---

# Docs Index

Machine-maintained catalog of all in-scope docs under `docs/` (excluding `docs/archive/` and `docs/releases/`). Owned by the `doc-gardening` skill — never hand-edit.

## docs/architecture/backpressure-pattern.md
sha: 7c8abec8
purpose: Model bounded queues and backpressure via WIP limits and SHIFT-based feedback control
covers: backpressure, WIP limits, SHIFT operator, feedback loops, queue overflow
references: wipLimit, wipOverflow, SHIFT, CLAMP, QueueRecurrence
authoritative_for: backpressure-modeling
last_verified: 2026-04-22
sections: WIP Limits (Push + Overflow) | SHIFT-Based Backpressure (Pull + Throttle) | Choosing Between the Two

## docs/architecture/class-dimension-decision.md
sha: c95ab4ae
purpose: Document decision to use single class per entity, with alternatives for multi-attribute systems
covers: class design, single-dimension, composite classes, labels, routing
references: classId, routes[], byClass series, labels, multi-dimensional alternatives
authoritative_for: class-dimension-design
last_verified: 2026-04-22
sections: Summary | Why a Single Class | Alternatives Considered | Modeling Guidance | Example (Food Delivery) | Implications

## docs/architecture/dag-map-evaluation.md
sha: 10f712c1
purpose: Evaluation of dag-map library extensions (custom renderers, edge labels, node dimensions)
covers: dag-map, visualization, layout engine, extension planning, prioritization
references: dag-map, renderNode, renderEdge, node dimensions, data attributes, input validation
authoritative_for: dag-map-extension-roadmap
last_verified: 2026-04-22
sections: Library Assessment | Proposed Extensions | Extensions to NOT Add | Implementation Priority | How FlowTime Would Use | Minor Issues to Fix | Conclusion

## docs/architecture/dag-map-parallel-lines-design.md
sha: fce98309
purpose: Design for rendering parallel colored lines on shared multi-class edges in metro-style DAG visualization
covers: dag-map, parallel lines, multi-class edges, metro visualization, layout
references: routes, parallel segments, route ordering, offset calculation, renderNode context
authoritative_for: dag-map-parallel-lines
last_verified: 2026-04-22
sections: Problem | How dag-map Routes Work Today | Proposed Design (Consumer-Provided Routes) | Parallel Line Rendering | Implementation Plan | Alternatives Considered | Open Questions | Example

## docs/architecture/dependencies-future-work.md
sha: c17bda26
purpose: Evaluate longer-term dependency modeling options (Options 3-5 resource pool, compiler expansion, retry loops)
covers: dependency modeling, resource allocation, compiler expansion, retry feedback, future roadmap
references: Option A/B current, Option 3/4/5 future, shared resource pools, allocation policies
authoritative_for: dependency-modeling-roadmap
last_verified: 2026-04-22
sections: Current State | Conceptual Guide | Option 3 Shared Resource Pool | Option 4 Compiler Expansion | Option 5 Delayed Feedback | Compatibility Guardrails | Roadmap Notes

## docs/architecture/dependency-ideas.md
sha: 724cf5e0
purpose: Aspirational exploration of dependency modeling plans with tradeoff analysis
covers: dependency modeling, shared bottlenecks, synchronous semantics, resource pools, retry policies
references: Plan 1-5 options, resource pools, call policies, composite classes, telemetry compatibility
authoritative_for: dependency-modeling-design-space
last_verified: 2026-04-22
sections: Dependency modeling must capture | Constraints to preserve | Alternative plans | Cleanest design for FlowTime | Retry loops | Backpressure modeling | Telemetry compatibility | Rules of thumb | Implementation path | Design decision summary

## docs/architecture/expression-language-design.md
sha: 24e6502a
purpose: Architect FlowTime expression parser and evaluator with recursive descent, series-based evaluation, temporal operators
covers: expression language, parser design, SHIFT, CONV, CLAMP, MIN, MAX, function architecture
references: ExprNode, recursive descent, series evaluation, SHIFT lag, CONV kernel, MOD/FLOOR/CEIL/ROUND/STEP/PULSE
authoritative_for: expression-language-architecture
last_verified: 2026-04-22
sections: Overview | System Architecture | Key Design Decisions | Performance Characteristics | Function Reference | Extension Points | Validation and Testing | Architectural Principles | Decision Log | Future Evolution Strategy

## docs/architecture/headless-engine-architecture.md
sha: e9379e6f
purpose: Design persistent engine session with parameterized evaluation and streaming protocol for interactive what-if
covers: headless engine, parameterization, message passing, streaming, parameter tables
references: ParamTable, evaluate_with_params, MessagePack, WebSocket, engine session, parameter schema
authoritative_for: headless-engine-design
last_verified: 2026-04-22
sections: Problem | Target | Design Principles | Layer 1 Parameterized Evaluation | Layer 2 Engine Session | Layer 3 Protocol | Layer 4 UI as Streaming Client | Implementation Sequence | Open Questions

## docs/architecture/matrix-engine.md
sha: 1176ab12
purpose: Rust implementation of FlowTime evaluation using column-store state matrix and ordered operation plan
covers: matrix engine, Rust, evaluation plan, column-major layout, bin-major evaluation
references: Op variants, Plan, ColumnMap, QueueRecurrence, ProportionalAlloc, DispatchGate
authoritative_for: matrix-engine-architecture
last_verified: 2026-04-22
sections: Overview | Data Representation | Evaluation Plan | Bin-Major Evaluation | Compilation Pipeline | Unified Topological Sort | Topology Synthesis | Derived Metrics | Invariant Analysis | Artifact Writing | CLI Interface | Module Structure | C# Engine Mapping | Test Coverage | Future Work

## docs/architecture/nan-policy.md
sha: 398e5f89
purpose: Define three-tier NaN/Infinity/division-by-zero policy for numerical safety in flow math
covers: NaN policy, division by zero, tier-1 zero return, tier-2 null, tier-3 NaN sentinel
references: Safe(), Tier 1/2/3, guard patterns, UtilizationComputer, ServiceWithBufferNode
authoritative_for: nan-policy
last_verified: 2026-04-22
sections: Introduction | Three-Tier Policy | Design Rationale | Comparison with Other Systems | Adding New Division Sites

## docs/architecture/retry-modeling.md
sha: c98a65b0
purpose: Comprehensive architecture for retry pattern modeling with dual edge types, temporal operators, and conservation laws
covers: retry modeling, dual edges (throughput/effort), CONV operator, terminal disposition, retry governance
references: attempts, failures, retryEcho, terminal edges, exhaustedFailures, CONV kernel, maxAttempts
authoritative_for: retry-modeling-architecture
last_verified: 2026-04-22
sections: Executive Summary | Reference Assets | Core Retry Modeling Concepts | Computational Architecture | Complex System Examples | Advanced Computational Considerations | Data Analysis and Visualization | Implementation Roadmap Alignment | Conclusion

## docs/architecture/rng-algorithm.md
sha: 339fa9e0
purpose: Select PCG-XSH-RR as the deterministic RNG for PMF sampling and reproducible large-scale DAG evaluation
covers: PCG-XSH-RR, RNG selection, determinism, reproducibility, statistical quality
references: Pcg32, seed, stream, NextDouble, LCG, permutation function, TestU01/PractRand
authoritative_for: rng-algorithm-selection
last_verified: 2026-04-22
sections: Executive Summary | Context | Algorithms Considered | Why PCG-XSH-RR | Implementation Details | Performance Benchmarks | Future Considerations | References | License | Version History

## docs/architecture/run-provenance.md
sha: a8b57e4a
purpose: Define how Engine accepts and stores model provenance metadata for complete traceability from template to run
covers: provenance, model tracking, metadata storage, artifact registry, HTTP headers, backward compatibility
references: modelId, templateId, source, provenance.json, X-Model-Provenance header, inputHash
authoritative_for: run-provenance-architecture
last_verified: 2026-04-22
sections: Overview | Canonical Artifact Layout | The Provenance Gap | Architecture Requirements | API Changes | Implementation Overview | Backward Compatibility | Benefits | Testing Strategy | Design Choices | Related Documentation | Architecture Principles

## docs/architecture/supported-surfaces.md
sha: 3f227f73
purpose: Authoritative inventory of current supported, transitional, and deprecated API/UI surfaces for E-19 cleanup
covers: supported surfaces, API routes, Sim routes, contracts, schemas, templates, examples
references: POST /v1/runs, GET /v1/runs, Sim orchestration, templates, drafts, catalogs
authoritative_for: supported-surface-matrix
last_verified: 2026-04-22
sections: Shared Framing | Blazor / Svelte Support Policy | Decision Matrix | Explicit Open Questions | Raw Sweep Appendices

## docs/architecture/template-draft-model-run-bundle-boundary.md
sha: be959e29
purpose: Clarify distinction between template, draft, model, run, and bundle artifacts to prevent Sim path hardening
covers: artifact types, ownership, responsibility, current/target architecture, E-19/E-18 roadmap
references: templates/, drafts/, data/runs/, canonical artifacts, FlowTime.TimeMachine, time-machine validation
authoritative_for: artifact-boundary-definitions
last_verified: 2026-04-22
sections: Purpose | Problem | Terms | Responsibility Clarification | Current Sequence Diagram | Transitional | Target | Decision | Consequences

## docs/architecture/time-machine-analysis-modes.md
sha: 931c5d78
purpose: Document analysis modes (sweep, sensitivity, goal-seek, optimize) built on parameterized engine evaluation
covers: parameter sweep, sensitivity analysis, goal seeking, optimization, Nelder-Mead simplex
references: IModelEvaluator, SweepRunner, Optimizer, SessionModelEvaluator, ConstNodePatcher
authoritative_for: time-machine-analysis-modes
last_verified: 2026-04-22
sections: Overview | Architecture | API Surface | CLI Surface | YAML Mutation | Future modes | Related documents

## docs/architecture/ui-dag-loading-options.md
sha: 554b88ab
purpose: Evaluate loading strategies for UI DAG visualization (eager, lazy, chunked, virtual)
covers: UI performance, DAG loading, rendering strategies, chunk sizes, streaming
references: elkjs layout, virtual scrolling, chunking, progressive rendering
authoritative_for: ui-dag-loading-strategy
last_verified: 2026-04-22
sections: —

## docs/architecture/whitepaper.md
sha: f879472e
purpose: Engineering whitepaper summarizing FlowTime vision, architecture, and roadmap
covers: flow analysis, stochastic modeling, time-travel visualization, architecture overview
references: DAG, expressions, PMF, time grid, determinism, artifacts
authoritative_for: flowtime-whitepaper
last_verified: 2026-04-22
sections: —

## docs/architecture/reviews/engine-deep-review-2026-03.md
sha: 700c60b3
purpose: Deep technical review of Rust engine implementation and schema after M-02 completion
covers: engine review, Rust implementation, schema validation, error handling
references: matrix engine, Op variants, column map, invariant analysis
authoritative_for: engine-deep-review-findings
last_verified: 2026-04-22
sections: —

## docs/architecture/reviews/engine-review-findings.md
sha: ca9f7281
purpose: Summary of findings from engine and schema review
covers: engine quality, schema correctness, test coverage
references: engine tests, schema validation, warnings
authoritative_for: engine-review-summary
last_verified: 2026-04-22
sections: —

## docs/architecture/reviews/engine-review-sequenced-plan-2026-03.md
sha: 160a133f
purpose: Sequenced improvement plan based on engine review findings
covers: engine improvements, refactoring roadmap, test coverage gaps
references: engine milestones, test priorities
authoritative_for: engine-improvement-roadmap
last_verified: 2026-04-22
sections: —

## docs/architecture/reviews/review-sequenced-plan-2026-03.md
sha: 6399ff48
purpose: Integrate engine review findings into overall roadmap reconciliation
covers: engine review, roadmap alignment, E-19/E-18 coordination
references: E-18, E-19, engine improvement
authoritative_for: roadmap-reconciliation-plan
last_verified: 2026-04-22
sections: —

## docs/concepts/nodes-and-expressions.md
sha: bcc9a32a
purpose: Explain core mental model of nodes, time grid, series, and expression compilation in FlowTime
covers: nodes, expressions, time grid, series, graph evaluation, classes
references: INode, ConstSeriesNode, BinaryOpNode, NodeId, Graph, topological order
authoritative_for: node-and-expression-concepts
last_verified: 2026-04-22
sections: Mental model | The building blocks | Current nodes | Expressions and compilation

## docs/concepts/pmf-modeling.md
sha: ab7bd9c6
purpose: Guide to Probability Mass Functions for modeling uncertainty in arrivals, processing, and failure rates
covers: PMF, probability distributions, const series, profile weights, stochastic modeling
references: PMF sampling, Pmf class, expected value, variance, profile weights
authoritative_for: pmf-modeling-guide
last_verified: 2026-04-22
sections: Overview | What are PMFs | Mathematical Foundation | Example PMF | PMFs vs Const Series vs Profile Weights | Combining PMF and profile

## docs/development/branching-strategy.md
sha: 464c04ca
purpose: Document Git branching strategy with milestone-based naming and integration discipline
covers: Git branching, milestone tracking, integration, PR workflows
references: feature/, fix/, hotfix/ branches, milestone links, tracking files
authoritative_for: git-branching-strategy
last_verified: 2026-04-22
sections: —

## docs/development/devcontainer.md
sha: d6ad37a9
purpose: Devcontainer setup for FlowTime development environment
covers: devcontainer, Docker, development environment, extensions
references: .devcontainer, VS Code, Docker image
authoritative_for: devcontainer-setup
last_verified: 2026-04-22
sections: —

## docs/development/devcontainer-maintenance.md
sha: 2b654c35
purpose: Maintenance guide for Devcontainer configuration
covers: devcontainer maintenance, Docker updates, extension management
references: Dockerfile, devcontainer.json
authoritative_for: devcontainer-maintenance
last_verified: 2026-04-22
sections: —

## docs/development/development-setup.md
sha: ed28edd6
purpose: Complete setup guide for FlowTime development environment
covers: environment setup, dependencies, build configuration, local development
references: .NET SDK, Rust, Node.js, Docker, Visual Studio Code
authoritative_for: development-environment-setup
last_verified: 2026-04-22
sections: —

## docs/development/epics-and-milestones.md
sha: 38cd11ef
purpose: Define epic and milestone tracking structure for FlowTime development
covers: epics, milestones, tracking, documentation requirements
references: E-xx epics, M-xx milestones, work/ directory structure
authoritative_for: epic-and-milestone-structure
last_verified: 2026-04-22
sections: —

## docs/development/milestone-documentation-guide.md
sha: e709f0cb
purpose: Guide for writing milestone specification and tracking documentation
covers: milestone docs, spec format, acceptance criteria, tracking files
references: spec.md, tracking.md, ADR, milestone prompt template
authoritative_for: milestone-documentation-standards
last_verified: 2026-04-22
sections: —

## docs/development/milestone-prompt-template.md
sha: b83ec0ba
purpose: Template prompt for Claude Code sessions during milestone work
covers: milestone work, prompt structure, context setup, acceptance criteria
references: milestone ID, spec path, tracking file, ADR template
authoritative_for: milestone-prompt-template
last_verified: 2026-04-22
sections: —

## docs/development/milestone-rules-quick-ref.md
sha: 1a1d4d57
purpose: Quick reference for milestone documentation rules and conventions
covers: milestone docs, naming, structure, acceptance criteria
references: spec.md, tracking.md, milestone naming
authoritative_for: milestone-documentation-rules
last_verified: 2026-04-22
sections: —

## docs/development/milestone-session-guide.md
sha: 918adeb4
purpose: Session guide for executing milestone work with Claude Code
covers: milestone execution, session setup, work patterns, testing
references: spec.md, acceptance criteria, git workflow
authoritative_for: milestone-session-guide
last_verified: 2026-04-22
sections: —

## docs/development/release-ceremony.md
sha: 4f207881
purpose: Document release process and ceremony for new FlowTime versions
covers: release process, versioning, changelog, artifacts, deployment
references: version bumping, release notes, artifact publication
authoritative_for: release-ceremony-process
last_verified: 2026-04-22
sections: —

## docs/development/TEMPLATE-tracking.md
sha: 5ef55108
purpose: Template file for milestone implementation tracking and progress
covers: milestone tracking, completion status, test results, blocking issues
references: [MILESTONE-ID], acceptance criteria, test coverage
authoritative_for: —
last_verified: 2026-04-22
sections: —

## docs/development/ui-debug-mode.md
sha: da3174ff
purpose: Reference guide for FlowTime UI debug mode features and usage
covers: debug mode, developer tools, diagnostics, logging
references: debug flags, console output, performance profiling
authoritative_for: ui-debug-mode-guide
last_verified: 2026-04-22
sections: —

## docs/development/versioning.md
sha: a35519b3
purpose: Define versioning strategy for FlowTime releases
covers: semantic versioning, version format, milestone alignment
references: version numbers, release tags, prerelease versions
authoritative_for: versioning-strategy
last_verified: 2026-04-22
sections: —

## docs/flowtime-charter.md
sha: e3b2e4e8
purpose: Define FlowTime's charter, core mission, and principles for the overall platform
covers: charter, mission, principles, product vision, artifacts-centric
references: models, runs, artifacts, learn paradigm, execution engines
authoritative_for: flowtime-charter
last_verified: 2026-04-22
sections: —

## docs/flowtime-engine-charter.md
sha: 09e06f18
purpose: Define the Engine Charter (v2.0) for execution, evaluation, and artifact production
covers: engine charter, determinism, evaluation, artifact contracts, reproducibility
references: evaluation, grid, determinism, conservation, time grid
authoritative_for: engine-charter
last_verified: 2026-04-22
sections: —

## docs/flowtime.md
sha: 52953251
purpose: Top-level overview of FlowTime system, concepts, and usage
covers: FlowTime overview, concepts, architecture, getting started
references: engine, models, runs, artifacts, services
authoritative_for: flowtime-overview
last_verified: 2026-04-22
sections: —

## docs/flowtime-v2.md
sha: b2591287
purpose: Overview of FlowTime v2 architecture and components
covers: FlowTime v2, architecture, services, components
references: Engine v2, Sim v2, API v2, Time Machine
authoritative_for: flowtime-v2-overview
last_verified: 2026-04-22
sections: —

## docs/guides/CLI.md
sha: b5b2750d
purpose: Complete guide to FlowTime CLI tools (engine and sim)
covers: CLI, commands, workflows, examples, options
references: flowtime-engine, flowtime-cli, commands, options
authoritative_for: cli-guide
last_verified: 2026-04-22
sections: —

## docs/guides/deployment.md
sha: c07230fc
purpose: Deployment guide for FlowTime services
covers: deployment, Docker, Kubernetes, environment configuration, production setup
references: Docker images, configuration, service coordination
authoritative_for: deployment-guide
last_verified: 2026-04-22
sections: —

## docs/guides/MCP.md
sha: 643a87ea
purpose: Guide to FlowTime MCP (Model Context Protocol) integration for AI agents
covers: MCP, integration, AI agents, tools, protocol
references: MCP tools, resource types, prompt instructions
authoritative_for: mcp-integration-guide
last_verified: 2026-04-22
sections: —

## docs/guides/UI.md
sha: b21cdbca
purpose: Guide to FlowTime Blazor UI features and usage
covers: Blazor UI, graphs, time-travel, scenarios, dashboards
references: graph visualization, state windows, metrics
authoritative_for: blazor-ui-guide
last_verified: 2026-04-22
sections: —

## docs/modeling.md
sha: 8702d795
purpose: Navigation and index to FlowTime modeling documentation
covers: modeling docs, node types, expressions, PMFs, templates
references: concepts, nodes, topology, traffic, classes
authoritative_for: modeling-documentation-map
last_verified: 2026-04-22
sections: —

## docs/notes/crystal-ball-predictive-projection.md
sha: 9e7c5051
purpose: Exploratory note on predictive projection from observed traffic patterns
covers: traffic analysis, prediction, time series, forecasting
references: PMF learning, traffic patterns, extrapolation
authoritative_for: —
last_verified: 2026-04-22
sections: —

## docs/notes/expression-extensions-roadmap.md
sha: 4c2795dc
purpose: Roadmap for extending expression language with new functions and operators
covers: expression extensions, new functions, statistical operators
references: EMA, ABS, SQRT, POW, statistical functions
authoritative_for: expression-roadmap
last_verified: 2026-04-22
sections: —

## docs/notes/flowtime-vs-ptolemy-and-related-systems.md
sha: 36f402d6
purpose: Comparison of FlowTime with Ptolemy, SimPy, and other simulation systems
covers: comparative analysis, simulation systems, modeling paradigms
references: Ptolemy, SimPy, other systems, design choices
authoritative_for: —
last_verified: 2026-04-22
sections: —

## docs/notes/model-discovery-path.md
sha: a3ce61f1
purpose: Exploratory path for discovering models from observed telemetry
covers: model discovery, reverse engineering, telemetry analysis
references: inversion, fitting, parameter estimation
authoritative_for: —
last_verified: 2026-04-22
sections: —

## docs/notes/modeling-ideas.md
sha: 3114e7b2
purpose: Collection of modeling ideas and patterns for future consideration
covers: modeling patterns, extensions, techniques
references: various modeling approaches
authoritative_for: —
last_verified: 2026-04-22
sections: —

## docs/notes/modeling-queues-and-buffers.md
sha: 149fc4a6
purpose: Guide to modeling queues, buffers, and backpressure mechanisms
covers: queue modeling, buffer semantics, backpressure, retention
references: serviceWithBuffer, queue nodes, WIP limits
authoritative_for: queue-modeling-patterns
last_verified: 2026-04-22
sections: —

## docs/notes/predictive-systems-and-uncertainty.md
sha: ed28ec39
purpose: Exploration of predictive systems and uncertainty quantification
covers: prediction, uncertainty, probabilistic modeling, confidence
references: PMF, scenarios, sensitivity, distribution
authoritative_for: —
last_verified: 2026-04-22
sections: —

## docs/notes/ui-optimization-explorer-vision.md
sha: af7ed3c7
purpose: Vision for optimization explorer UI for parameter sweep and goal seek
covers: UI vision, optimization, explorers, visualization
references: sweep, goal seek, sensitivity, optimizer
authoritative_for: —
last_verified: 2026-04-22
sections: —

## docs/operations/telemetry-capture-guide.md
sha: 188e31f9
purpose: Guide to telemetry capture workflows and API usage
covers: telemetry capture, data ingestion, source management, validation
references: telemetry API, capture endpoints, series mapping
authoritative_for: telemetry-capture
last_verified: 2026-04-22
sections: —

## docs/performance/FT-M-05.06/README.md
sha: fcb91a42
purpose: Performance milestone documentation for M-05.06
covers: performance testing, benchmarks, results
references: performance metrics, baselines
authoritative_for: —
last_verified: 2026-04-22
sections: —

## docs/performance/FT-M-05.07/README.md
sha: 6272d9e4
purpose: Performance milestone documentation for M-05.07
covers: performance testing, Playwright automation, tracing
references: performance test suite, tracing infrastructure
authoritative_for: —
last_verified: 2026-04-22
sections: —

## docs/performance/FT-M-05.07/automation.md
sha: f5674d5f
purpose: Performance test automation setup and execution
covers: Playwright, test automation, CI/CD integration
references: playwright, test scripts, CI pipeline
authoritative_for: —
last_verified: 2026-04-22
sections: —

## docs/performance/FT-M-05.07/debugging.md
sha: 9cd80b16
purpose: Debugging guide for performance test failures
covers: debugging, profiling, trace analysis, troubleshooting
references: trace files, performance profiles, analysis tools
authoritative_for: —
last_verified: 2026-04-22
sections: —

## docs/performance/FT-M-05.07/playwright-plan.md
sha: 60676263
purpose: Plan for Playwright-based performance testing infrastructure
covers: Playwright, test scenarios, metrics collection, reporting
references: Playwright, performance metrics, test harness
authoritative_for: —
last_verified: 2026-04-22
sections: —

## docs/performance/FT-M-05.07/traces/README.md
sha: 9c45dcae
purpose: Documentation for performance trace data collection and analysis
covers: trace collection, analysis, metrics
references: trace files, analysis tools
authoritative_for: —
last_verified: 2026-04-22
sections: —

## docs/performance/M1.5-performance-report.md
sha: 87212e0e
purpose: Performance report for milestone M1.5
covers: performance metrics, benchmarks, analysis
references: baseline metrics, optimization results
authoritative_for: —
last_verified: 2026-04-22
sections: —

## docs/performance/M1.6-benchmarking-infrastructure.md
sha: 11bac5c3
purpose: Documentation of benchmarking infrastructure
covers: benchmarking, infrastructure, test harness, metrics
references: benchmark tools, test infrastructure
authoritative_for: benchmarking-infrastructure
last_verified: 2026-04-22
sections: —

## docs/performance/M1.6-performance-report.md
sha: dcb2bfc7
purpose: Performance report for milestone M1.6
covers: performance metrics, benchmarks, comparison
references: baseline metrics, performance trends
authoritative_for: —
last_verified: 2026-04-22
sections: —

## docs/performance/M1.6-performance-report-revised.md
sha: b532dd05
purpose: Revised performance report for M1.6 with additional analysis
covers: performance metrics, revised analysis, detailed results
references: performance data, analysis, recommendations
authoritative_for: —
last_verified: 2026-04-22
sections: —

## docs/performance/M2-pmf-performance-report.md
sha: 91f8c5c4
purpose: Performance report for PMF implementation in M2
covers: PMF performance, sampling, compilation, evaluation
references: PMF sampling, performance metrics
authoritative_for: —
last_verified: 2026-04-22
sections: —

## docs/performance/optimization-opportunities.md
sha: 8cb92e4b
purpose: Document identified optimization opportunities
covers: optimization, performance improvements, opportunities
references: bottlenecks, improvement strategies
authoritative_for: optimization-opportunities
last_verified: 2026-04-22
sections: —

## docs/performance/perf-log.md
sha: c55a6bc7
purpose: Log of performance testing and optimization work
covers: perf testing log, historical record, decisions
references: test runs, findings, decisions
authoritative_for: —
last_verified: 2026-04-22
sections: —

## docs/performance/TT-M-03.29-performance-report.md
sha: 4572094e
purpose: Performance report for time-travel milestone M-03.29
covers: time-travel performance, metrics, benchmarks
references: performance data, analysis
authoritative_for: —
last_verified: 2026-04-22
sections: —

## docs/reference/configuration.md
sha: 00514dec
purpose: Complete configuration reference for FlowTime services and engine
covers: configuration, environment variables, settings
references: AppSettings, engine config, API config
authoritative_for: configuration-reference
last_verified: 2026-04-22
sections: —

## docs/reference/contracts.md
sha: 1577a53d
purpose: Reference documentation for public API contracts and data shapes
covers: API contracts, request/response shapes, data formats
references: Run, State, Graph, Metrics, Artifacts contracts
authoritative_for: api-contract-reference
last_verified: 2026-04-22
sections: —

## docs/reference/data-formats.md
sha: 6f462184
purpose: Reference for data formats and file structures used by FlowTime
covers: data formats, CSV, JSON, YAML, serialization
references: CSV format, JSON schema, artifact structure
authoritative_for: data-format-reference
last_verified: 2026-04-22
sections: —

## docs/reference/engine-capabilities.md
sha: 66a580a5
purpose: Reference documentation of engine capabilities and supported operations
covers: engine features, expressions, nodes, operations, functions
references: SHIFT, CONV, MIN, MAX, CLAMP, MOD, FLOOR, CEIL, ROUND, STEP, PULSE
authoritative_for: engine-capabilities
last_verified: 2026-04-22
sections: —

## docs/reference/flow-theory-coverage.md
sha: 66b49c04
purpose: Reference of flow theory concepts covered in FlowTime
covers: flow theory, queueing, systems, theory coverage
references: Little's Law, queue theory, flow concepts
authoritative_for: flow-theory-coverage
last_verified: 2026-04-22
sections: —

## docs/reference/flow-theory-foundations.md
sha: 826cf209
purpose: Foundational flow theory concepts for understanding FlowTime
covers: flow theory fundamentals, queueing theory, capacity analysis
references: arrival rates, service rates, utilization, queues
authoritative_for: flow-theory-foundations
last_verified: 2026-04-22
sections: —

## docs/research/engine-rewrite-language-and-representation.md
sha: 64d2e28f
purpose: Research document on Rust engine rewrite and expression representation
covers: engine architecture, representation models, evaluation strategies
references: Rust implementation, Op types, expression compilation
authoritative_for: —
last_verified: 2026-04-22
sections: —

## docs/research/expert-authoring-surface.md
sha: 6b5541bd
purpose: Research on expert/advanced authoring surfaces for template composition
covers: expert authoring, composition, advanced users
references: composition patterns, templating
authoritative_for: —
last_verified: 2026-04-22
sections: —

## docs/research/flowtime-headless-integration.md
sha: 6a6080eb
purpose: Research on headless integration of FlowTime engine
covers: headless, integration, embedding, API
references: engine API, headless evaluation
authoritative_for: —
last_verified: 2026-04-22
sections: —

## docs/research/liminara.md
sha: 1eff1c9b
purpose: Research notes on Liminara modeling concepts and integration
covers: Liminara, modeling, integration, analysis
references: Liminara system, concepts
authoritative_for: —
last_verified: 2026-04-22
sections: —

## docs/schemas/model.schema.md
sha: fd8f13b9
purpose: Reference documentation for model YAML schema
covers: model schema, YAML structure, node types, topology
references: grid, nodes, topology, traffic, constraints
authoritative_for: model-schema-reference
last_verified: 2026-04-22
sections: —

## docs/schemas/README.md
sha: ebf1c450
purpose: Overview of FlowTime schema documentation
covers: schema reference, documentation map
references: model schema, template schema, runtime schemas
authoritative_for: schemas-documentation-overview
last_verified: 2026-04-22
sections: —

## docs/schemas/template-schema.md
sha: b8d36991
purpose: Reference documentation for template YAML schema
covers: template schema, authoring, parameters, metadata
references: parameters, metadata, versioning
authoritative_for: template-schema-reference
last_verified: 2026-04-22
sections: —

## docs/templates/metric-alias-authoring.md
sha: f75ecec6
purpose: Guide to defining domain-specific metric aliases in templates
covers: metric aliases, semantic mapping, authoring, semantics
references: aliases, semantics, inspector chips
authoritative_for: metric-alias-authoring
last_verified: 2026-04-22
sections: —

## docs/templates/profiles.md
sha: 6f2db7e8
purpose: Guide to profile weights for time-series shaping in templates
covers: profiles, time patterns, weights, PMF composition
references: profile weights, seasonality, daily patterns
authoritative_for: profile-weights-guide
last_verified: 2026-04-22
sections: —

## docs/templates/template-authoring.md
sha: f37ac340
purpose: Comprehensive guide to authoring FlowTime templates
covers: template authoring, modeling patterns, best practices
references: topology, nodes, traffic, classes, constraints
authoritative_for: template-authoring-guide
last_verified: 2026-04-22
sections: —

## docs/templates/template-testing.md
sha: 06fa122e
purpose: Guide to testing templates for correctness and reproducibility
covers: template testing, validation, test patterns, fixtures
references: schema validation, contract testing
authoritative_for: template-testing-guide
last_verified: 2026-04-22
sections: —

## docs/ui/architecture.md
sha: 4f3bd7fc
purpose: Architecture documentation for FlowTime UI systems
covers: UI architecture, components, state management, rendering
references: Blazor, graph rendering, time-travel UI
authoritative_for: ui-architecture
last_verified: 2026-04-22
sections: —

## docs/ui/development-guide.md
sha: 8171253b
purpose: Development guide for FlowTime UI contributors
covers: UI development, components, tooling, workflow
references: Blazor, TypeScript, Svelte, build tools
authoritative_for: ui-development-guide
last_verified: 2026-04-22
sections: —

## docs/ui/layout.md
sha: 2a229a62
purpose: Layout and grid system documentation for FlowTime UI
covers: layout system, grids, responsive design, spacing
references: layout components, grid system
authoritative_for: ui-layout-reference
last_verified: 2026-04-22
sections: —

## docs/ui/route-architecture.md
sha: 62db595c
purpose: Documentation of UI routing and navigation architecture
covers: routing, navigation, page structure, URLs
references: routes, navigation paths, page hierarchies
authoritative_for: ui-route-architecture
last_verified: 2026-04-22
sections: —

## docs/ui/time-travel-visualizations.md
sha: d7681c24
purpose: Guide to time-travel visualization concepts and UI patterns
covers: time-travel, visualization, interaction patterns, animations
references: scrubber, bin navigation, state windows, charts
authoritative_for: time-travel-visualization-guide
last_verified: 2026-04-22
sections: —

## docs/ui/api-integration.md
sha: 68634872
purpose: Integration guide for FlowTime UI with API and backend services
covers: API integration, HTTP clients, service calls, patterns
references: REST API, service calls, data flow
authoritative_for: ui-api-integration-guide
last_verified: 2026-04-22
sections: —

## docs/ui/design-specification.md
sha: 4280987a
purpose: Complete design specification for FlowTime UI modules and functionality
covers: UI design, modules, features, workflows, interactions
references: graph explorer, run manager, scenario composer, PMF library
authoritative_for: ui-design-specification
last_verified: 2026-04-22
sections: —

---

## Reverse indexes

### by_topic

- `api-contract-reference` → docs/reference/contracts.md
- `artifact-boundary-definitions` → docs/architecture/template-draft-model-run-bundle-boundary.md
- `backpressure-modeling` → docs/architecture/backpressure-pattern.md
- `benchmarking-infrastructure` → docs/performance/M1.6-benchmarking-infrastructure.md
- `blazor-ui-guide` → docs/guides/UI.md
- `class-dimension-design` → docs/architecture/class-dimension-decision.md
- `cli-guide` → docs/guides/CLI.md
- `configuration-reference` → docs/reference/configuration.md
- `dag-map-extension-roadmap` → docs/architecture/dag-map-evaluation.md
- `dag-map-parallel-lines` → docs/architecture/dag-map-parallel-lines-design.md
- `data-format-reference` → docs/reference/data-formats.md
- `dependency-modeling-design-space` → docs/architecture/dependency-ideas.md
- `dependency-modeling-roadmap` → docs/architecture/dependencies-future-work.md
- `deployment-guide` → docs/guides/deployment.md
- `devcontainer-maintenance` → docs/development/devcontainer-maintenance.md
- `devcontainer-setup` → docs/development/devcontainer.md
- `development-environment-setup` → docs/development/development-setup.md
- `engine-capabilities` → docs/reference/engine-capabilities.md
- `engine-charter` → docs/flowtime-engine-charter.md
- `engine-deep-review-findings` → docs/architecture/reviews/engine-deep-review-2026-03.md
- `engine-improvement-roadmap` → docs/architecture/reviews/engine-review-sequenced-plan-2026-03.md
- `engine-review-summary` → docs/architecture/reviews/engine-review-findings.md
- `epic-and-milestone-structure` → docs/development/epics-and-milestones.md
- `expression-language-architecture` → docs/architecture/expression-language-design.md
- `expression-roadmap` → docs/notes/expression-extensions-roadmap.md
- `flow-theory-coverage` → docs/reference/flow-theory-coverage.md
- `flow-theory-foundations` → docs/reference/flow-theory-foundations.md
- `flowtime-charter` → docs/flowtime-charter.md
- `flowtime-overview` → docs/flowtime.md
- `flowtime-v2-overview` → docs/flowtime-v2.md
- `flowtime-whitepaper` → docs/architecture/whitepaper.md
- `git-branching-strategy` → docs/development/branching-strategy.md
- `headless-engine-design` → docs/architecture/headless-engine-architecture.md
- `matrix-engine-architecture` → docs/architecture/matrix-engine.md
- `mcp-integration-guide` → docs/guides/MCP.md
- `metric-alias-authoring` → docs/templates/metric-alias-authoring.md
- `milestone-documentation-rules` → docs/development/milestone-rules-quick-ref.md
- `milestone-documentation-standards` → docs/development/milestone-documentation-guide.md
- `milestone-prompt-template` → docs/development/milestone-prompt-template.md
- `milestone-session-guide` → docs/development/milestone-session-guide.md
- `model-schema-reference` → docs/schemas/model.schema.md
- `modeling-documentation-map` → docs/modeling.md
- `nan-policy` → docs/architecture/nan-policy.md
- `node-and-expression-concepts` → docs/concepts/nodes-and-expressions.md
- `optimization-opportunities` → docs/performance/optimization-opportunities.md
- `pmf-modeling-guide` → docs/concepts/pmf-modeling.md
- `profile-weights-guide` → docs/templates/profiles.md
- `queue-modeling-patterns` → docs/notes/modeling-queues-and-buffers.md
- `release-ceremony-process` → docs/development/release-ceremony.md
- `retry-modeling-architecture` → docs/architecture/retry-modeling.md
- `rng-algorithm-selection` → docs/architecture/rng-algorithm.md
- `roadmap-reconciliation-plan` → docs/architecture/reviews/review-sequenced-plan-2026-03.md
- `run-provenance-architecture` → docs/architecture/run-provenance.md
- `schemas-documentation-overview` → docs/schemas/README.md
- `supported-surface-matrix` → docs/architecture/supported-surfaces.md
- `telemetry-capture` → docs/operations/telemetry-capture-guide.md
- `template-authoring-guide` → docs/templates/template-authoring.md
- `template-schema-reference` → docs/schemas/template-schema.md
- `template-testing-guide` → docs/templates/template-testing.md
- `time-machine-analysis-modes` → docs/architecture/time-machine-analysis-modes.md
- `time-travel-visualization-guide` → docs/ui/time-travel-visualizations.md
- `ui-api-integration-guide` → docs/ui/api-integration.md
- `ui-architecture` → docs/ui/architecture.md
- `ui-dag-loading-strategy` → docs/architecture/ui-dag-loading-options.md
- `ui-debug-mode-guide` → docs/development/ui-debug-mode.md
- `ui-design-specification` → docs/ui/design-specification.md
- `ui-development-guide` → docs/ui/development-guide.md
- `ui-layout-reference` → docs/ui/layout.md
- `ui-route-architecture` → docs/ui/route-architecture.md
- `versioning-strategy` → docs/development/versioning.md

### by_symbol

- `.NET SDK` → docs/development/development-setup.md
- `.devcontainer` → docs/development/devcontainer.md
- `ABS` → docs/notes/expression-extensions-roadmap.md
- `ADR` → docs/development/milestone-documentation-guide.md
- `ADR template` → docs/development/milestone-prompt-template.md
- `API config` → docs/reference/configuration.md
- `API v2` → docs/flowtime-v2.md
- `AppSettings` → docs/reference/configuration.md
- `Artifacts contracts` → docs/reference/contracts.md
- `BinaryOpNode` → docs/concepts/nodes-and-expressions.md
- `Blazor` → docs/ui/architecture.md, docs/ui/development-guide.md
- `CEIL` → docs/reference/engine-capabilities.md
- `CI pipeline` → docs/performance/FT-M-05.07/automation.md
- `CLAMP` → docs/architecture/backpressure-pattern.md, docs/reference/engine-capabilities.md
- `CONV` → docs/reference/engine-capabilities.md
- `CONV kernel` → docs/architecture/expression-language-design.md, docs/architecture/retry-modeling.md
- `CSV format` → docs/reference/data-formats.md
- `ColumnMap` → docs/architecture/matrix-engine.md
- `ConstNodePatcher` → docs/architecture/time-machine-analysis-modes.md
- `ConstSeriesNode` → docs/concepts/nodes-and-expressions.md
- `DAG` → docs/architecture/whitepaper.md
- `DispatchGate` → docs/architecture/matrix-engine.md
- `Docker` → docs/development/development-setup.md
- `Docker image` → docs/development/devcontainer.md
- `Docker images` → docs/guides/deployment.md
- `Dockerfile` → docs/development/devcontainer-maintenance.md
- `E-18` → docs/architecture/reviews/review-sequenced-plan-2026-03.md
- `E-19` → docs/architecture/reviews/review-sequenced-plan-2026-03.md
- `E-xx epics` → docs/development/epics-and-milestones.md
- `EMA` → docs/notes/expression-extensions-roadmap.md
- `Engine v2` → docs/flowtime-v2.md
- `ExprNode` → docs/architecture/expression-language-design.md
- `FLOOR` → docs/reference/engine-capabilities.md
- `FlowTime.TimeMachine` → docs/architecture/template-draft-model-run-bundle-boundary.md
- `GET /v1/runs` → docs/architecture/supported-surfaces.md
- `Graph` → docs/concepts/nodes-and-expressions.md, docs/reference/contracts.md
- `IModelEvaluator` → docs/architecture/time-machine-analysis-modes.md
- `INode` → docs/concepts/nodes-and-expressions.md
- `JSON schema` → docs/reference/data-formats.md
- `LCG` → docs/architecture/rng-algorithm.md
- `Liminara system` → docs/research/liminara.md
- `Little's Law` → docs/reference/flow-theory-coverage.md
- `M-xx milestones` → docs/development/epics-and-milestones.md
- `MAX` → docs/reference/engine-capabilities.md
- `MCP tools` → docs/guides/MCP.md
- `MIN` → docs/reference/engine-capabilities.md
- `MOD` → docs/reference/engine-capabilities.md
- `MOD/FLOOR/CEIL/ROUND/STEP/PULSE` → docs/architecture/expression-language-design.md
- `MessagePack` → docs/architecture/headless-engine-architecture.md
- `Metrics` → docs/reference/contracts.md
- `NextDouble` → docs/architecture/rng-algorithm.md
- `Node.js` → docs/development/development-setup.md
- `NodeId` → docs/concepts/nodes-and-expressions.md
- `Op types` → docs/research/engine-rewrite-language-and-representation.md
- `Op variants` → docs/architecture/matrix-engine.md, docs/architecture/reviews/engine-deep-review-2026-03.md
- `Optimizer` → docs/architecture/time-machine-analysis-modes.md
- `Option 3/4/5 future` → docs/architecture/dependencies-future-work.md
- `Option A/B current` → docs/architecture/dependencies-future-work.md
- `PMF` → docs/architecture/whitepaper.md, docs/notes/predictive-systems-and-uncertainty.md
- `PMF learning` → docs/notes/crystal-ball-predictive-projection.md
- `PMF library` → docs/ui/design-specification.md
- `PMF sampling` → docs/concepts/pmf-modeling.md, docs/performance/M2-pmf-performance-report.md
- `POST /v1/runs` → docs/architecture/supported-surfaces.md
- `POW` → docs/notes/expression-extensions-roadmap.md
- `PULSE` → docs/reference/engine-capabilities.md
- `ParamTable` → docs/architecture/headless-engine-architecture.md
- `Pcg32` → docs/architecture/rng-algorithm.md
- `Plan` → docs/architecture/matrix-engine.md
- `Plan 1-5 options` → docs/architecture/dependency-ideas.md
- `Playwright` → docs/performance/FT-M-05.07/playwright-plan.md
- `Pmf class` → docs/concepts/pmf-modeling.md
- `ProportionalAlloc` → docs/architecture/matrix-engine.md
- `Ptolemy` → docs/notes/flowtime-vs-ptolemy-and-related-systems.md
- `QueueRecurrence` → docs/architecture/backpressure-pattern.md, docs/architecture/matrix-engine.md
- `REST API` → docs/ui/api-integration.md
- `ROUND` → docs/reference/engine-capabilities.md
- `Run` → docs/reference/contracts.md
- `Rust` → docs/development/development-setup.md
- `Rust implementation` → docs/research/engine-rewrite-language-and-representation.md
- `SHIFT` → docs/architecture/backpressure-pattern.md, docs/reference/engine-capabilities.md
- `SHIFT lag` → docs/architecture/expression-language-design.md
- `SQRT` → docs/notes/expression-extensions-roadmap.md
- `STEP` → docs/reference/engine-capabilities.md
- `Safe()` → docs/architecture/nan-policy.md
- `ServiceWithBufferNode` → docs/architecture/nan-policy.md
- `SessionModelEvaluator` → docs/architecture/time-machine-analysis-modes.md
- `Sim orchestration` → docs/architecture/supported-surfaces.md
- `Sim v2` → docs/flowtime-v2.md
- `SimPy` → docs/notes/flowtime-vs-ptolemy-and-related-systems.md
- `State` → docs/reference/contracts.md
- `Svelte` → docs/ui/development-guide.md
- `SweepRunner` → docs/architecture/time-machine-analysis-modes.md
- `TestU01/PractRand` → docs/architecture/rng-algorithm.md
- `Tier 1/2/3` → docs/architecture/nan-policy.md
- `Time Machine` → docs/flowtime-v2.md
- `TypeScript` → docs/ui/development-guide.md
- `UtilizationComputer` → docs/architecture/nan-policy.md
- `VS Code` → docs/development/devcontainer.md
- `Visual Studio Code` → docs/development/development-setup.md
- `WIP limits` → docs/notes/modeling-queues-and-buffers.md
- `WebSocket` → docs/architecture/headless-engine-architecture.md
- `X-Model-Provenance header` → docs/architecture/run-provenance.md
- `[MILESTONE-ID]` → docs/development/TEMPLATE-tracking.md
- `acceptance criteria` → docs/development/milestone-session-guide.md, docs/development/TEMPLATE-tracking.md
- `aliases` → docs/templates/metric-alias-authoring.md
- `allocation policies` → docs/architecture/dependencies-future-work.md
- `analysis` → docs/performance/M1.6-performance-report-revised.md, docs/performance/TT-M-03.29-performance-report.md
- `analysis tools` → docs/performance/FT-M-05.07/debugging.md, docs/performance/FT-M-05.07/traces/README.md
- `arrival rates` → docs/reference/flow-theory-foundations.md
- `artifact publication` → docs/development/release-ceremony.md
- `artifact structure` → docs/reference/data-formats.md
- `artifacts` → docs/architecture/whitepaper.md, docs/flowtime-charter.md, docs/flowtime.md
- `attempts` → docs/architecture/retry-modeling.md
- `baseline metrics` → docs/performance/M1.5-performance-report.md, docs/performance/M1.6-performance-report.md
- `baselines` → docs/performance/FT-M-05.06/README.md
- `benchmark tools` → docs/performance/M1.6-benchmarking-infrastructure.md
- `bin navigation` → docs/ui/time-travel-visualizations.md
- `bottlenecks` → docs/performance/optimization-opportunities.md
- `build tools` → docs/ui/development-guide.md
- `byClass series` → docs/architecture/class-dimension-decision.md
- `call policies` → docs/architecture/dependency-ideas.md
- `canonical artifacts` → docs/architecture/template-draft-model-run-bundle-boundary.md
- `capture endpoints` → docs/operations/telemetry-capture-guide.md
- `catalogs` → docs/architecture/supported-surfaces.md
- `charts` → docs/ui/time-travel-visualizations.md
- `chunking` → docs/architecture/ui-dag-loading-options.md
- `classId` → docs/architecture/class-dimension-decision.md
- `classes` → docs/modeling.md, docs/templates/template-authoring.md
- `column map` → docs/architecture/reviews/engine-deep-review-2026-03.md
- `commands` → docs/guides/CLI.md
- `composite classes` → docs/architecture/dependency-ideas.md
- `composition patterns` → docs/research/expert-authoring-surface.md
- `concepts` → docs/modeling.md, docs/research/liminara.md
- `configuration` → docs/guides/deployment.md
- `conservation` → docs/flowtime-engine-charter.md
- `console output` → docs/development/ui-debug-mode.md
- `constraints` → docs/schemas/model.schema.md, docs/templates/template-authoring.md
- `contract testing` → docs/templates/template-testing.md
- `dag-map` → docs/architecture/dag-map-evaluation.md
- `daily patterns` → docs/templates/profiles.md
- `data attributes` → docs/architecture/dag-map-evaluation.md
- `data flow` → docs/ui/api-integration.md
- `data/runs/` → docs/architecture/template-draft-model-run-bundle-boundary.md
- `debug flags` → docs/development/ui-debug-mode.md
- `decisions` → docs/performance/perf-log.md
- `design choices` → docs/notes/flowtime-vs-ptolemy-and-related-systems.md
- `determinism` → docs/architecture/whitepaper.md, docs/flowtime-engine-charter.md
- `devcontainer.json` → docs/development/devcontainer-maintenance.md
- `distribution` → docs/notes/predictive-systems-and-uncertainty.md
- `drafts` → docs/architecture/supported-surfaces.md
- `drafts/` → docs/architecture/template-draft-model-run-bundle-boundary.md
- `elkjs layout` → docs/architecture/ui-dag-loading-options.md
- `engine` → docs/flowtime.md
- `engine API` → docs/research/flowtime-headless-integration.md
- `engine config` → docs/reference/configuration.md
- `engine improvement` → docs/architecture/reviews/review-sequenced-plan-2026-03.md
- `engine milestones` → docs/architecture/reviews/engine-review-sequenced-plan-2026-03.md
- `engine session` → docs/architecture/headless-engine-architecture.md
- `engine tests` → docs/architecture/reviews/engine-review-findings.md
- `evaluate_with_params` → docs/architecture/headless-engine-architecture.md
- `evaluation` → docs/flowtime-engine-charter.md
- `execution engines` → docs/flowtime-charter.md
- `exhaustedFailures` → docs/architecture/retry-modeling.md
- `expected value` → docs/concepts/pmf-modeling.md
- `expression compilation` → docs/research/engine-rewrite-language-and-representation.md
- `expressions` → docs/architecture/whitepaper.md
- `extrapolation` → docs/notes/crystal-ball-predictive-projection.md
- `failures` → docs/architecture/retry-modeling.md
- `feature/` → docs/development/branching-strategy.md
- `findings` → docs/performance/perf-log.md
- `fitting` → docs/notes/model-discovery-path.md
- `fix/` → docs/development/branching-strategy.md
- `flow concepts` → docs/reference/flow-theory-coverage.md
- `flowtime-cli` → docs/guides/CLI.md
- `flowtime-engine` → docs/guides/CLI.md
- `git workflow` → docs/development/milestone-session-guide.md
- `goal seek` → docs/notes/ui-optimization-explorer-vision.md
- `graph explorer` → docs/ui/design-specification.md
- `graph rendering` → docs/ui/architecture.md
- `graph visualization` → docs/guides/UI.md
- `grid` → docs/flowtime-engine-charter.md, docs/schemas/model.schema.md
- `grid system` → docs/ui/layout.md
- `guard patterns` → docs/architecture/nan-policy.md
- `headless evaluation` → docs/research/flowtime-headless-integration.md
- `hotfix/ branches` → docs/development/branching-strategy.md
- `improvement strategies` → docs/performance/optimization-opportunities.md
- `input validation` → docs/architecture/dag-map-evaluation.md
- `inputHash` → docs/architecture/run-provenance.md
- `inspector chips` → docs/templates/metric-alias-authoring.md
- `invariant analysis` → docs/architecture/reviews/engine-deep-review-2026-03.md
- `inversion` → docs/notes/model-discovery-path.md
- `labels` → docs/architecture/class-dimension-decision.md
- `layout components` → docs/ui/layout.md
- `learn paradigm` → docs/flowtime-charter.md
- `matrix engine` → docs/architecture/reviews/engine-deep-review-2026-03.md
- `maxAttempts` → docs/architecture/retry-modeling.md
- `metadata` → docs/schemas/template-schema.md
- `metrics` → docs/guides/UI.md
- `milestone ID` → docs/development/milestone-prompt-template.md
- `milestone links` → docs/development/branching-strategy.md
- `milestone naming` → docs/development/milestone-rules-quick-ref.md
- `milestone prompt template` → docs/development/milestone-documentation-guide.md
- `model schema` → docs/schemas/README.md
- `modelId` → docs/architecture/run-provenance.md
- `models` → docs/flowtime-charter.md, docs/flowtime.md
- `multi-dimensional alternatives` → docs/architecture/class-dimension-decision.md
- `navigation paths` → docs/ui/route-architecture.md
- `node dimensions` → docs/architecture/dag-map-evaluation.md
- `nodes` → docs/modeling.md, docs/schemas/model.schema.md, docs/templates/template-authoring.md
- `offset calculation` → docs/architecture/dag-map-parallel-lines-design.md
- `optimization results` → docs/performance/M1.5-performance-report.md
- `optimizer` → docs/notes/ui-optimization-explorer-vision.md
- `options` → docs/guides/CLI.md
- `other systems` → docs/notes/flowtime-vs-ptolemy-and-related-systems.md
- `page hierarchies` → docs/ui/route-architecture.md
- `parallel segments` → docs/architecture/dag-map-parallel-lines-design.md
- `parameter estimation` → docs/notes/model-discovery-path.md
- `parameter schema` → docs/architecture/headless-engine-architecture.md
- `parameters` → docs/schemas/template-schema.md
- `performance data` → docs/performance/M1.6-performance-report-revised.md, docs/performance/TT-M-03.29-performance-report.md
- `performance metrics` → docs/performance/FT-M-05.06/README.md, docs/performance/FT-M-05.07/playwright-plan.md, docs/performance/M2-pmf-performance-report.md
- `performance profiles` → docs/performance/FT-M-05.07/debugging.md
- `performance profiling` → docs/development/ui-debug-mode.md
- `performance test suite` → docs/performance/FT-M-05.07/README.md
- `performance trends` → docs/performance/M1.6-performance-report.md
- `permutation function` → docs/architecture/rng-algorithm.md
- `playwright` → docs/performance/FT-M-05.07/automation.md
- `prerelease versions` → docs/development/versioning.md
- `profile weights` → docs/concepts/pmf-modeling.md, docs/templates/profiles.md
- `progressive rendering` → docs/architecture/ui-dag-loading-options.md
- `prompt instructions` → docs/guides/MCP.md
- `provenance.json` → docs/architecture/run-provenance.md
- `queue nodes` → docs/notes/modeling-queues-and-buffers.md
- `queue theory` → docs/reference/flow-theory-coverage.md
- `queues` → docs/reference/flow-theory-foundations.md
- `recommendations` → docs/performance/M1.6-performance-report-revised.md
- `recursive descent` → docs/architecture/expression-language-design.md
- `release notes` → docs/development/release-ceremony.md
- `release tags` → docs/development/versioning.md
- `renderEdge` → docs/architecture/dag-map-evaluation.md
- `renderNode` → docs/architecture/dag-map-evaluation.md
- `renderNode context` → docs/architecture/dag-map-parallel-lines-design.md
- `resource pools` → docs/architecture/dependency-ideas.md
- `resource types` → docs/guides/MCP.md
- `retryEcho` → docs/architecture/retry-modeling.md
- `route ordering` → docs/architecture/dag-map-parallel-lines-design.md
- `routes` → docs/architecture/dag-map-parallel-lines-design.md, docs/ui/route-architecture.md
- `routes[]` → docs/architecture/class-dimension-decision.md
- `run manager` → docs/ui/design-specification.md
- `runs` → docs/flowtime-charter.md, docs/flowtime.md
- `runtime schemas` → docs/schemas/README.md
- `scenario composer` → docs/ui/design-specification.md
- `scenarios` → docs/notes/predictive-systems-and-uncertainty.md
- `schema validation` → docs/architecture/reviews/engine-review-findings.md, docs/templates/template-testing.md
- `scrubber` → docs/ui/time-travel-visualizations.md
- `seasonality` → docs/templates/profiles.md
- `seed` → docs/architecture/rng-algorithm.md
- `semantics` → docs/templates/metric-alias-authoring.md
- `sensitivity` → docs/notes/predictive-systems-and-uncertainty.md, docs/notes/ui-optimization-explorer-vision.md
- `series evaluation` → docs/architecture/expression-language-design.md
- `series mapping` → docs/operations/telemetry-capture-guide.md
- `service calls` → docs/ui/api-integration.md
- `service coordination` → docs/guides/deployment.md
- `service rates` → docs/reference/flow-theory-foundations.md
- `serviceWithBuffer` → docs/notes/modeling-queues-and-buffers.md
- `services` → docs/flowtime.md
- `shared resource pools` → docs/architecture/dependencies-future-work.md
- `source` → docs/architecture/run-provenance.md
- `spec path` → docs/development/milestone-prompt-template.md
- `spec.md` → docs/development/milestone-documentation-guide.md, docs/development/milestone-rules-quick-ref.md, docs/development/milestone-session-guide.md
- `state windows` → docs/guides/UI.md, docs/ui/time-travel-visualizations.md
- `statistical functions` → docs/notes/expression-extensions-roadmap.md
- `stream` → docs/architecture/rng-algorithm.md
- `sweep` → docs/notes/ui-optimization-explorer-vision.md
- `telemetry API` → docs/operations/telemetry-capture-guide.md
- `telemetry compatibility` → docs/architecture/dependency-ideas.md
- `template schema` → docs/schemas/README.md
- `templateId` → docs/architecture/run-provenance.md
- `templates` → docs/architecture/supported-surfaces.md
- `templates/` → docs/architecture/template-draft-model-run-bundle-boundary.md
- `templating` → docs/research/expert-authoring-surface.md
- `terminal edges` → docs/architecture/retry-modeling.md
- `test coverage` → docs/development/TEMPLATE-tracking.md
- `test harness` → docs/performance/FT-M-05.07/playwright-plan.md
- `test infrastructure` → docs/performance/M1.6-benchmarking-infrastructure.md
- `test priorities` → docs/architecture/reviews/engine-review-sequenced-plan-2026-03.md
- `test runs` → docs/performance/perf-log.md
- `test scripts` → docs/performance/FT-M-05.07/automation.md
- `time grid` → docs/architecture/whitepaper.md, docs/flowtime-engine-charter.md
- `time-machine validation` → docs/architecture/template-draft-model-run-bundle-boundary.md
- `time-travel UI` → docs/ui/architecture.md
- `topological order` → docs/concepts/nodes-and-expressions.md
- `topology` → docs/modeling.md, docs/schemas/model.schema.md, docs/templates/template-authoring.md
- `trace files` → docs/performance/FT-M-05.07/debugging.md, docs/performance/FT-M-05.07/traces/README.md
- `tracing infrastructure` → docs/performance/FT-M-05.07/README.md
- `tracking file` → docs/development/milestone-prompt-template.md
- `tracking files` → docs/development/branching-strategy.md
- `tracking.md` → docs/development/milestone-documentation-guide.md, docs/development/milestone-rules-quick-ref.md
- `traffic` → docs/modeling.md, docs/schemas/model.schema.md, docs/templates/template-authoring.md
- `traffic patterns` → docs/notes/crystal-ball-predictive-projection.md
- `utilization` → docs/reference/flow-theory-foundations.md
- `variance` → docs/concepts/pmf-modeling.md
- `various modeling approaches` → docs/notes/modeling-ideas.md
- `version bumping` → docs/development/release-ceremony.md
- `version numbers` → docs/development/versioning.md
- `versioning` → docs/schemas/template-schema.md
- `virtual scrolling` → docs/architecture/ui-dag-loading-options.md
- `warnings` → docs/architecture/reviews/engine-review-findings.md
- `wipLimit` → docs/architecture/backpressure-pattern.md
- `wipOverflow` → docs/architecture/backpressure-pattern.md
- `work/ directory structure` → docs/development/epics-and-milestones.md
