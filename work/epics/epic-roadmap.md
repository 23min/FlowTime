# Architecture Epic Roadmap

This document provides an overview of architecture-level **epics** and how they relate to the broader roadmap (`ROADMAP.md`). Each epic lives in its own folder under `docs/architecture/` and is implemented via milestones under `work/milestones/`.

## How to Read This Document

- Each section below corresponds to an epic folder under `docs/architecture/`.
- Epics are ordered roughly in the sequence we intend to tackle them.
- For each epic, we list:
  - The folder path under `docs/architecture/`.
  - A short description of the goal.
  - Any known or active milestones.

This document should remain in sync with `ROADMAP.md` (which gives the higher-level, product-facing view).

## Current Epics (Architecture-Level)

The list below enumerates current epics under `docs/architecture/` in an order that roughly matches the roadmap: completed foundation work, near‑term focus, then mid‑term / aspirational epics.

### Completed Epics

#### Time Travel

- **Folder:** `work/epics/completed/time-travel/`
- **Status:** V1 delivered (see `work/epics/completed/time-travel/status-2025-11-23.md`).
- **Goal:** Provide deterministic, time-binned replay of runs with `/state` and `/state_window` APIs, telemetry capture/bundles, and DLQ/backlog semantics so we can reason about flows after the fact.

#### UI Performance (Topology Input Latency)

- **Folder:** `work/epics/completed/ui-perf/`
- **Goal:** Make the Time-Travel Topology UI responsive under large graphs by enforcing strict input/paint/data lanes, splitting scene build from paint-only updates, and eliminating main-thread stalls from avoidable payload rebuilds and interop churn.
- **Key Docs:** `work/epics/completed/ui-perf/spec.md`
- **Notes:** Milestone `FT-M-05.06` is marked complete in tracking; remaining work is follow-up/perf validation if new regressions appear.

#### Classes & Routing

- **Folder:** `work/epics/completed/classes/`
- **Goal:** Introduce class-aware routing and visualization so that templates and the UI can represent multi-class flows (e.g., priority tiers, customer segments) while preserving FlowTime's determinism and DAG semantics.
- **Notes:** CL-M-04.01 through CL-M-04.04 are marked complete in tracking; epic is effectively closed unless new class feature work is scoped.

#### Service With Buffer

- **Folder:** `work/epics/completed/service-with-buffer/`
- **Goal:** Replace the legacy backlog node surface with a first-class `serviceWithBuffer` node type that owns both service behavior and an explicit queue/buffer, with aligned schema, engine, analyzer, and UI support.
- **Key Milestones:**
  - `work/milestones/completed/SB-M-05.01.md` — Breaking introduction of `kind: serviceWithBuffer` and removal of `kind: backlog` from the public surface.
- **Notes:** SB-M-05.01 through SB-M-05.04 are marked complete in tracking; epic is closed pending future expansion.

#### Evaluation Integrity (DAG/Spreadsheet Contract)

- **Folder:** `work/epics/completed/evaluation-integrity/`
- **Goal:** Enforce a strict DAG evaluation contract so no post-eval mutation is possible; all overrides apply before evaluation and derived class series always recompute in order.
- **Key Milestones:**
  - `work/milestones/completed/M-06.01-evaluation-integrity-dag-contract.md`
  - `work/milestones/completed/M-06.02-model-compiler.md`
- **Notes:** M-06.01 and M-06.02 are complete; compile-to-DAG is centralized in `FlowTime.Core` and shared across entry points.

#### Edge Time Bins / Edge Metrics

- **Folder:** `work/epics/completed/edge-time-bin/`
- **Goal:** Derive or surface per-edge throughput/attempt volumes and related metrics to support edge heat maps, conservation checks, and richer topology overlays.
- **Key Milestones:**
  - `work/milestones/completed/M-07.01-edge-time-bin-foundations.md`
  - `work/milestones/completed/M-07.02-edge-time-bin-conservation-quality.md`
  - `work/milestones/completed/M-07.03-edge-time-bin-ui-overlays.md`
  - `work/milestones/completed/M-07.04-edge-semantics-contract.md`
  - `work/milestones/completed/M-07.05-edge-derived-path-latency.md`
  - `work/milestones/completed/M-07.06-transit-node-modeling.md`
- **Notes:** M-07.01 through M-07.06 are complete; epic closed.

#### MCP Modeling and Analysis (AI-Assisted Workflow)

- **Folder:** `work/epics/completed/ai/`
- **Goal:** Deliver an MCP server that supports both modeling and analysis: draft templates in a working area, validate/generate/run via FlowTime.Sim and FlowTime API, and use analyst tools to inspect graph/state outputs for verification and iteration.
- **Key Milestones:**
  - `work/milestones/completed/M-08.01-mcp-server-poc.md`
  - `work/milestones/completed/M-08.02-mcp-modeling-draft-workflow.md`
  - `work/milestones/completed/M-08.03-mcp-data-intake-profile-fitting.md`
  - `work/milestones/completed/M-08.04-mcp-storage-abstraction.md`
  - `work/milestones/M-08.05-mcp-edge-metrics-support.md`
- **Notes:** M-08.01 through M-08.05 are complete; epic closed.

#### Engine Semantics Layer

- **Folder:** `work/epics/completed/engine-semantics-layer/`
- **Goal:** Define the engine as the semantics layer that turns canonical bundles into stable `/state`, `/state_window`, and `/graph` contracts for downstream consumers.
- **Key Docs:** `work/epics/completed/engine-semantics-layer/spec.md`
- **Key Milestones:**
  - `work/milestones/M-09.01-engine-semantics-layer.md`
- **Notes:** M-09.01 is complete; epic closed pending follow-up contract hardening.

### Near-Term / In-Flight Epics

#### Dependency Constraints & Shared Resources

- **Folder:** `work/epics/dependency-constraints/`
- **Goal:** Model downstream dependencies (databases, caches, external APIs) as resource constraints so shared bottlenecks and coupling are visible while preserving the minimal arrivals/served basis.
- **Notes:** Depends on explicit edge semantics/metrics (M-07.04) so effort vs throughput load is engine-owned and validated. Follow-up milestone M-10.03 enforces dependency pattern selection in MCP modeling. Future roadmap items (not yet epics) include shared resource pools, compiler expansion of call policies, and delayed feedback semantics (see `docs/architecture/dependencies-future-work.md`).

#### Visualizations (Chart Gallery / Demo Lab)

- **Folder:** `work/epics/visualizations/`
- **Goal:** Provide a dedicated UI space to prototype and compare role-focused charts (exec, SRE, support) using FlowTime-derived metrics, with a clear contrast between FlowTime output and raw telemetry where available.
- **Notes:** Best scheduled after engine semantics and dependency constraints; can start with synthetic runs and later add raw telemetry comparisons once ingestion is available.

#### Telemetry Ingestion and Canonical Bundles

- **Folder:** `work/epics/telemetry-ingestion/`
- **Goal:** Transform raw telemetry into canonical FlowTime bundles with validated manifests and stable series naming.
- **Key Docs:** `work/epics/telemetry-ingestion/spec.md`
- **Notes:** Not started; depends on stable bundle schemas and series semantics so ingestion outputs align with engine contracts.

#### Path Analysis & Subgraph Queries

- **Folder:** `work/epics/path-analysis/`
- **Goal:** Define path-level queries and derived metrics (dominant routes, bottlenecks, path pain) for UI and MCP consumption, based on edge time bins.
- **Notes:** Depends on EdgeTimeBin and Classes as Flows; should expose a server-side contract so clients do not derive path semantics locally.

### Mid-Term / Aspirational Epics

#### Telemetry Loop & Parity

- **Folder:** `work/epics/telemetry-loop-parity/`
- **Goal:** Ensure synthetic runs and telemetry replays match via parity tooling, tolerances, and drift reporting.
- **Key Docs:** `work/epics/telemetry-loop-parity/spec.md`
- **Notes:** Depends on Telemetry Ingestion and the Engine Semantics Layer.

#### Scenario Overlays & What-If Runs

- **Folder:** `work/epics/overlays/`
- **Goal:** Create first-class overlay runs (what-if scenarios) that derive from baseline runs via validated patches (parallelism, capacity, arrivals, schedules) and preserve deterministic provenance for comparison in the UI.
- **Key Docs:** `work/epics/overlays/overlays.md`
- **Notes:** This epic consolidates overlay concepts previously scattered across the time-travel decision log and the engine charter. It pairs naturally with flow-aware anomaly/pathology detection, enabling immediate "what if" mitigation testing.

#### Flow-Aware Anomaly & Pathology Detection

- **Folder:** `work/epics/anomaly-detection/`
- **Goal:** Use the time-binned DAG model to detect incidents and recurring flow pathologies (retry storms, slow drains, stuck queues) and surface incident-focused stories and dashboards.
- **Notes:** Builds on time travel and the engine semantics layer; also benefits from richer node/edge metrics.

#### Streaming & Subsystems

- **Folders:**
  - `work/epics/streaming/`
  - `work/epics/subsystems/`
- **Goal:** Explore how FlowTime's deterministic, time-binned DAG semantics extend into streaming scenarios and into modular subsystems, keeping a clear separation between the core engine and orchestrated subgraphs.
- **Notes:** Longer-term and exploratory; relies on the core engine semantics and node types being stable.

#### Ptolemy-Inspired Semantics & Directors (Conceptual Guardrails)

- **Folder:** `work/epics/ptolemy/`
- **Goal:** Keep time/coordination semantics explicit (e.g., a DiscreteTime director seam) and selectively borrow ideas like modal models, typed ports, and determinacy contracts to future-proof FlowTime’s engine while staying DT-first.
- **Notes:** Conceptual guardrails for later engine/epic work; fits the same time horizon as other aspirational epics.

#### UI Layout Motors (Pluggable Layout Engines)

- **Folder:** `work/epics/ui-layout/`
- **Goal:** Decouple topology layout from rendering so different layout “motors” (client, server, hybrid) can be plugged in behind a stable `LayoutInput -> LayoutResult` contract with signatures for caching and reproducibility.
- **Key Docs:** `work/epics/ui-layout/spec.md`
- **Notes:** This epic complements UI perf work by making layout an explicit scene-build concern (not tied to per-bin updates).

## Keeping in Sync with the Roadmap

- `ROADMAP.md` should reference this file as the place where architecture epics are enumerated.
- When adding or reordering epics:
  - Update this file to reflect the new or changed epic.
  - Update `ROADMAP.md` to ensure the high-level roadmap remains consistent.

By keeping `ROADMAP.md` and `work/epics/epic-roadmap.md` in sync, we maintain a clear line from high-level goals → epics → milestones.
