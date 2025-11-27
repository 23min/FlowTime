# Architecture Epic Roadmap

This document provides an overview of architecture-level **epics** and how they relate to the broader roadmap (`docs/ROADMAP.md`). Each epic lives in its own folder under `docs/architecture/` and is implemented via milestones under `docs/milestones/`.

## How to Read This Document

- Each section below corresponds to an epic folder under `docs/architecture/`.
- Epics are ordered roughly in the sequence we intend to tackle them.
- For each epic, we list:
  - The folder path under `docs/architecture/`.
  - A short description of the goal.
  - Any known or active milestones.

This document should remain in sync with `docs/ROADMAP.md` (which gives the higher-level, product-facing view).

## Current Epics (Architecture-Level)

The list below enumerates current epics under `docs/architecture/` in an order that roughly matches the roadmap: completed foundation work, near‑term focus, then mid‑term / aspirational epics.

### Completed / Foundational Epics

#### Time Travel

- **Folder:** `docs/architecture/time-travel/`
- **Status:** V1 delivered (see `docs/architecture/time-travel/status-2025-11-23.md`).
- **Goal:** Provide deterministic, time-binned replay of runs with `/state` and `/state_window` APIs, telemetry capture/bundles, and DLQ/backlog semantics so we can reason about flows after the fact.

### Near-Term / In-Flight Epics

#### Classes & Routing

- **Folder:** `docs/architecture/classes/`
- **Goal:** Introduce class-aware routing and visualization so that templates and the UI can represent multi-class flows (e.g., priority tiers, customer segments) while preserving FlowTime's determinism and DAG semantics.
- **Notes:** Current milestone `CL-M-04.03.02` (scheduled dispatch & flow control primitives) sits within this broader epic.

#### Expression Extensions

- **Folder:** `docs/architecture/expression-extensions-roadmap.md`
- **Goal:** Extend the expression language with math, gating, and control-flow primitives (MOD/FLOOR/CEIL/ROUND/PULSE/STEP, IF, EMA/DELAY, router/autoscale helpers) needed by higher-level epics like autoscale, scheduled dispatch, and richer anomaly detection.
- **Notes:** Cross-cutting support epic; many milestones (including classes/routing and ServiceWithBuffer) depend on specific operators landing.

#### Service With Buffer

- **Folder:** `docs/architecture/service-with-buffer/`
- **Goal:** Replace the legacy backlog node surface with a first-class `serviceWithBuffer` node type that owns both service behavior and an explicit queue/buffer, with aligned schema, engine, analyzer, and UI support.
- **Key Milestones:**
  - `docs/milestones/SB-M-01-service-with-buffer.md` — Breaking introduction of `kind: serviceWithBuffer` and removal of `kind: backlog` from the public surface.

#### Edge Time Bins / Edge Metrics

- **Folder:** `docs/architecture/edge-time-bin/`
- **Goal:** Derive or surface per-edge throughput/attempt volumes and related metrics to support edge heat maps, conservation checks, and richer topology overlays.
- **Notes:** Called out as the first near‑term epic candidate in `docs/ROADMAP.md`. Depends on stable node semantics from classes and ServiceWithBuffer.

### Mid-Term / Aspirational Epics

#### Engine as Post-Processing & Semantics Layer

- **Folder:** `docs/architecture/engine-post-processing/`
- **Goal:** Treat the engine as a reusable post-processing and semantics layer between telemetry stores (e.g., ADX) and downstream UIs/BI tools.
- **Notes:** Listed in the "Mid-Term / Aspirational" section of the roadmap.

#### Flow-Aware Anomaly & Pathology Detection

- **Folder:** `docs/architecture/anomaly-detection/`
- **Goal:** Use the time-binned DAG model to detect incidents and recurring flow pathologies (retry storms, slow drains, stuck queues) and surface incident-focused stories and dashboards.
- **Notes:** Builds on time travel and engine post-processing; also benefits from richer node/edge metrics.

#### AI Analyst over the Digital Twin (MCP)

- **Folder:** `docs/architecture/ai/`
- **Goal:** Expose FlowTime graph and state APIs via MCP (and related interfaces) so AI agents can act as analysts over runs—answering questions, comparing scenarios, and drafting summaries in a structured way.
- **Notes:** Depends on stable `/state`/`/state_window` semantics and a clear engine post-processing contract.

#### Streaming & Subsystems

- **Folders:**
  - `docs/architecture/streaming/`
  - `docs/architecture/subsystems/`
- **Goal:** Explore how FlowTime's deterministic, time-binned DAG semantics extend into streaming scenarios and into modular subsystems, keeping a clear separation between the core engine and orchestrated subgraphs.
- **Notes:** Longer-term and exploratory; relies on the core engine semantics and node types being stable.

#### Ptolemy-Inspired Semantics & Directors (Conceptual Guardrails)

- **Folder:** `docs/architecture/ptolemy/`
- **Goal:** Keep time/coordination semantics explicit (e.g., a DiscreteTime director seam) and selectively borrow ideas like modal models, typed ports, and determinacy contracts to future-proof FlowTime’s engine while staying DT-first.
- **Notes:** Conceptual guardrails for later engine/epic work; fits the same time horizon as other aspirational epics.

## Keeping in Sync with the Roadmap

- `docs/ROADMAP.md` should reference this file as the place where architecture epics are enumerated.
- When adding or reordering epics:
  - Update this file to reflect the new or changed epic.
  - Update `docs/ROADMAP.md` to ensure the high-level roadmap remains consistent.

By keeping `docs/ROADMAP.md` and `docs/architecture/epic-roadmap.md` in sync, we maintain a clear line from high-level goals → epics → milestones.
