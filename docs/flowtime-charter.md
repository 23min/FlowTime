# FlowTime Charter (v1.0)

**Date:** 2026-01-24  
**Audience:** Product, engineering, platform, and applied teams

---

## Purpose

FlowTime is a deterministic, time‑binned modeling and analysis platform for **flows**: work that moves through systems over time (orders, patients, requests, packets, cases). It turns models and telemetry into **canonical run artifacts** and stable APIs so humans and agents can reason about capacity, backlog, latency, and bottlenecks without hand‑stitching dashboards.

FlowTime exists to provide a **single, explainable, time‑based view** of complex systems — and a path from “what happened” to “what should we try next.”

---

## Who It’s For (Roles & Use Cases)

**Primary roles**
- **SRE / Reliability**: diagnose bottlenecks, backlog growth, retry storms, and capacity shortfalls.
- **Engineering / Platform**: validate system changes, understand flow impacts, and run what‑if scenarios.
- **Operations / Support**: explain why queues grew or SLAs dipped during specific windows.
- **Exec / Business Ops**: understand throughput, delay, and risk at a high level across flows.
- **Process Optimization / Process Mining**: compare “as‑is” vs “to‑be” flows and identify constraints.

**Representative use cases**
- Explain a throughput drop across a DAG with attributable bottlenecks.
- Quantify backlog risk and recovery time for key flows.
- Compare scenarios (capacity changes, routing changes, demand spikes).
- Validate whether telemetry and model behaviors reconcile.
- Provide agent‑ready summaries without manual dashboard stitching.

---

## Core Principles

- **Determinism over drift:** Same inputs → same outputs. No hidden mutation after evaluation.
- **Minimal basis, rich meaning:** Arrivals, served, queue depth are the backbone; derived metrics must be labeled with provenance.
- **Explainability first:** Make wrongness visible; warnings are first‑class artifacts.
- **Contracts over convenience:** APIs and schemas are stable; UIs/agents should not invent semantics.
- **Outcomes over operations:** High‑level workflows, not low‑level glue.

---

## What FlowTime Is

### 1) A modeling and replay engine
- Deterministic execution on a discrete time grid.
- Canonical run artifacts (`run.json`, `manifest.json`, `series/`).
- Stable `/state` and `/state_window` semantics for UIs and agents.

### 2) A simulation and template system
- FlowTime.Sim owns **template authoring** and stochastic inputs.
- Templates produce models; models produce runs.

### 3) A time‑travel UI
- Topology, inspector, and edge overlays for human interpretation.
- Focus filters and provenance, not ad‑hoc math.

### 4) An MCP server for agent workflows
- Modeling, analysis, and inspection tools designed around outcomes.
- No client‑side inference of engine semantics.

---

## What FlowTime Is Not

- **Not** a streaming/real‑time execution engine.
- **Not** a BI tool or charting library (it powers them).
- **Not** a telemetry storage system (it consumes and produces bundles).
- **Not** a probabilistic simulator by default (deterministic first).

---

## Current State (Jan 2026)

Shipped foundations:
- **Time‑travel V1**: `/state` + `/state_window`, DLQ/backlog semantics, canonical bundles.
- **Evaluation integrity**: compile‑to‑DAG, no post‑eval mutation.
- **Edge time bins**: per‑edge flows, retries, conservation checks, UI overlays.
- **MCP modeling + analysis**: draft → validate → run → inspect loop.
- **Engine semantics layer**: state/graph contracts as stable semantics.
- **Dependency constraints**:
  - Option A: dependency nodes in the topology (flow nodes).
  - Option B: constraint registry attached to services (resource constraints).

---

## Near‑Term Focus (Next Epics)

1. **Dependency Constraints** (follow‑up enforcement for MCP modeling patterns).  
2. **Visualizations**: chart gallery / demo lab for role‑focused analytics.  
3. **Telemetry Ingestion + Canonical Bundles**: loader + parity with synthetic runs.  
4. **Path Analysis**: path‑level queries from edge time bins.

See `work/epics/epic-roadmap.md` and `ROADMAP.md`.

---

## Long‑Term Direction (Aspirational)

- Telemetry parity and drift detection.
- Scenario overlays (what‑if).
- Anomaly/pathology detection.
- Streaming and subsystems.
- Ptolemy‑inspired semantic guardrails.

---

## References

- `docs/reference/engine-capabilities.md` — What is shipped.
- `work/epics/epic-roadmap.md` — Epic ordering and status.
- `docs/architecture/whitepaper.md` — Vision and foundations.
- `docs/flowtime-engine-charter.md` — Engine‑specific scope.
