# Release CL-M-04.03.01 — Router Nodes & Analyzer Coverage

**Release Date:** 2025-11-27  
**Type:** Milestone delivery (no version bump)  
**Canonical Runs:** `data/run_deterministic_40c00c5b` (transportation-basic-classes), `data/run_deterministic_6e2d40c6` (supply-chain-multi-tier-classes)

## Overview

CL-M-04.03.01 introduces first-class router nodes across the engine, templates, analyzers, and UI:

- Schema/engine support for `kind: router` nodes, including class-aware contributions and deterministic routing semantics.
- Transportation & supply-chain class templates now rely on routers rather than inline split expressions; regenerated runs maintain `classCoverage: "full"`.
- Analyzer/CLI/API surfaces emit router diagnostics (`router_missing_class_route`, `router_class_leakage`) so authors catch misconfigured routes immediately.
- UI polish: class chips on run cards/time-travel views respect router metadata, and router-only scaffolding expressions stay hidden when inspecting the full DAG to prevent misleading warnings.

## Key Changes

1. **Schema & Engine**
   - `docs/schemas/model.schema.yaml`, `ModelParser`, and `ClassContributionBuilder` now understand router nodes and recompute downstream contributions when routers override class flows.
   - New router-specific unit tests (`RouterClassContributionTests`) lock down conservation for both class-targeted routes and weight-based fallbacks.

2. **Templates & Docs**
   - `templates/transportation-basic-classes.yaml` and `templates/supply-chain-multi-tier-classes.yaml` define explicit routers (HubDispatchRouter, ReturnsRouter) with per-class routes.
   - Docs (`docs/templates/template-authoring.md`, `docs/templates/template-testing.md`) gained router sections covering schema expectations, analyzer usage, and CLI commands.
   - Deterministic artifacts refreshed (`run_deterministic_40c00c5b`, `run_deterministic_6e2d40c6`) showing `classCoverage: "full"`.

3. **Analyzer, CLI, API & UI**
   - `TemplateInvariantAnalyzer` now surfaces router diagnostics; CLI (`flowtime run`) prints warning summaries after each run, and `StateQueryService` logs manifest warnings the moment a run loads.
   - UI class chips, inspector metrics, and graph overlays ingest router metadata; expression-only scaffolding nodes are tagged `graph.hidden` so full-DAG view remains debuggable without phantom warnings.

## Tests

- `dotnet build --nologo`
- `dotnet test --nologo` *(perf suite skips remain expected: `FlowTime.Tests.Performance.M2PerformanceTests.*` and Sim expression smoke skips)*
- Targeted suites:
  - `dotnet test --filter RouterClassContributionTests --nologo`
  - `dotnet test --filter RouterTemplateRegressionTests --nologo`
  - `dotnet test --filter TemplateInvariantAnalyzerTests --nologo`
  - `dotnet test --filter FlowTime.UI.Tests --nologo`

## Known Issues / Follow-ups

- Perf benchmark skips (`FlowTime.Tests.Performance.M2PerformanceTests.*`) are still deferred to the broader epic 4 perf sweep.
- Router nodes will gain richer service-like semantics (capacity/service-time ownership, dedicated glyphs) in CL-M-04.03.02; current release focuses on deterministic class routing and diagnostics.

## Verification Artifacts

- Transportation router run: `data/run_deterministic_40c00c5b` (`transportation-basic-classes`, seed 4242).
- Supply-chain router run: `data/run_deterministic_6e2d40c6` (`supply-chain-multi-tier-classes`, seed 9876).

Both runs show `classCoverage: "full"` and no router warnings, confirming analyzer parity and UI readiness.
