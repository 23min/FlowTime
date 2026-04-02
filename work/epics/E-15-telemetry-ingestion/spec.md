# Epic: Telemetry Ingestion, Topology Inference, and Canonical Bundles

**ID:** E-15
**Status:** Proposed (capture is shipped; ingestion pipeline is not)

## Goal

Build the pipeline that takes real-world data — event logs, traces, sensor feeds — and produces the two things FlowTime needs: a `/graph` topology and Gold-format time-binned series. This epic owns ingestion, topology inference, validation, and bundle assembly.

## Context

FlowTime currently assumes topology is hand-built and telemetry fills series for known nodes. This works for synthetic models but blocks two capabilities:

1. **Domain-agnostic adoption** — FlowTime should work for any process with entities flowing through a system: IT microservices, business workflows, transit networks, logistics.
2. **Validation against real data** — Synthetic models validate correctness but not usefulness. Real data proves FlowTime's analytical primitives produce answers that matter.

The March 2026 dataset fitness research (`docs/architecture/dataset-fitness-and-ingestion-research.md`) identifies process mining event logs, distributed systems traces, and physical network telemetry as candidate validation datasets.

## Scope

### In Scope

**Gold Builder (raw data → binned facts)**
- Bronze → Silver → Gold pipeline: raw payloads → normalized events → binned facts per node per time window
- Mapping rules from raw telemetry to canonical node series
- Manifest generation and data quality warnings
- Class coverage metadata and gap-fill policy
- Support for multiple source types: event logs, time-series, OD pairs, API feeds

**Graph Builder (data → topology)**
- Topology inference from case traces (directly-follows graph)
- Topology inference from origin-destination pairs
- Topology join with external structure sources (GTFS, OSM, service registries)
- Edge confidence scoring and provenance metadata
- Human curation: accept/reject edges, pin known edges, merge/split nodes, annotate node kinds

**Bundle Assembly**
- Deterministic bundle generation (same inputs → same hashes)
- Schema validation against published schemas
- Canonical bundle format: `model.yaml`, `manifest.json`, `series/`, CSV files

**Validation Dataset Integration**
- Process mining event log ingestion (BPI Challenge format)
- At least one non-IT dataset path (transit, traffic, or logistics)

### Out of Scope
- Engine semantic derivations (utilization, latency, retry governance) — owned by engine
- Overlay scenarios and derived runs — owned by overlays epic
- UI workflows beyond topology rendering and basic data availability signals
- Real-time / streaming ingestion (batch-first)

## Current State (2026-03)

- **Capture exists**: API `/telemetry/captures` and CLI `flowtime telemetry capture` can generate telemetry bundles from existing runs.
- **Bundle contract exists**: `docs/schemas/telemetry-manifest.schema.json` defines the canonical manifest.
- **Gold schema defined**: Per node, per time bin: timestamp, node, flow, arrivals, served, errors, optional queue_depth/capacity_proxy.
- **TelemetryLoader service** (ADX/KQL or lake ingestion) is not implemented.
- **Graph Builder** does not exist.
- **No external dataset has been ingested** into FlowTime yet.

## Design Principles

- **Deterministic outputs**: same inputs yield same bundle hashes.
- **Schema-first**: ingestion must validate against published schemas.
- **No semantics**: ingestion is responsible for aggregation and alignment, not derived metrics (bottleneck ID, cycle time, etc. are engine-side).
- **Topology as artifact with provenance**: inferred edges carry confidence scores; the graph is versioned and curable, not a fixed truth.
- **Honest about gaps**: missing data, low-confidence edges, and inferred values are explicitly surfaced as warnings — never silently hidden.

## Canonical Bundle Contract

Bundles consist of:
- `model.yaml`
- `manifest.json` (window, grid, files, warnings)
- `series/index.json`
- CSV series files

Ingestion rules ensure:
- Stable series naming and units
- Consistent bin alignment and gap handling
- Explicit warnings for missing or low-quality data

## Dependencies

- **Engine semantics layer** (complete): ingestion outputs must align with canonical series semantics.
- **Phase 3 analytical primitives** (in progress): bottleneck ID, cycle time, WIP limits — these make ingested data interesting rather than just displayable.
- **dag-map spike**: informs how inferred topologies render in the UI.

## Suggested Milestone Decomposition

### M1: Gold Builder v1
- Ingest a single process mining event log (BPI Challenge 2012) into Gold format
- Bronze → Silver → Gold pipeline for event log sources
- Bin alignment, gap handling, data quality warnings
- Output: canonical bundle that FlowTime engine can consume

### M2: Graph Builder v1
- Infer topology from directly-follows relations in event log traces
- Output `/graph` with nodes, edges, confidence scores
- Human curation hooks (accept/reject/pin)
- First end-to-end demo: real dataset → topology + Gold → UI renders graph

### M3: External topology join
- Join telemetry with external structure source (GTFS, OSM, or service registry)
- Support OD-pair and physical-adjacency topology construction
- Second dataset path (transit or traffic)

### M4: Data quality and operational tooling
- Gap detection, zero-fill policies, class coverage metadata
- CLI/SDK wrappers for ingestion
- Ingestion diagnostics and provenance

### M5: TelemetryLoader service
- Service endpoint for batch ingestion from data lake / ADX / parquet sources
- Production-grade validation and bundle assembly

## Validation Datasets (Identified)

| Dataset | Domain | Why | Milestone |
|---------|--------|-----|-----------|
| BPI Challenge 2012 | Loan applications | Topology from traces, 13K cases, rework loops | M1, M2 |
| Road Traffic Fines | Municipal process | 150K cases, very high volume, clear bottlenecks | M1, M2 |
| PeMS + OpenStreetMap | Road traffic | Physical network, massive vehicle volume | M3 |
| MTA Ridership + GTFS | Transit passengers | Passengers as entities, incidents, schedule backbone | M3 |
| Alibaba Cluster Trace 2018 | Microservices | IT validation, millions of requests, trace-based topology | Future |

See `docs/architecture/dataset-fitness-and-ingestion-research.md` for the full evaluation and dataset fitness checklist.

## References

- `docs/architecture/dataset-fitness-and-ingestion-research.md` — Dataset fitness research
- `work/epics/completed/time-travel/telemetry-generation-explicit.md` — Existing telemetry capture
- `docs/reference/engine-capabilities.md` — Engine capabilities
- `docs/schemas/telemetry-manifest.schema.json` — Bundle manifest schema
- `work/epics/E-10-engine-correctness-and-analytics/spec.md` — Phase 3 analytical primitives (dependency)
