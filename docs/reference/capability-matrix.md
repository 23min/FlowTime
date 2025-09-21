# Capability Matrix (Snapshot)

> **📋 Charter Notice**: This capability matrix reflects pre-charter milestones (M0-M2.6). For current development capabilities and roadmap, see [Charter Roadmap](../milestones/CHARTER-ROADMAP.md) milestones M2.7-M2.9.

Legend: Done / Prototype / Partial / Planned.

| Area | Capability | Status | Notes |
|------|------------|--------|-------|
| Core Engine | Deterministic grid, Series<T>, DAG topo order | Done (M0) | Cycle detection present |
| Nodes | const, basic expr (series * scalar, + scalar) | Done | Series-series & advanced built-ins in M1.5 |
| Artifacts (Contracts Parity) | spec.yaml, run.json(source,grid+tz,align), manifest.json(rng), series/index.json, per-series hashes, placeholders (events, gold) | Done (M1) | Deterministic hashing + JSON Schema validation |
| Expressions | Parser + refs + built-ins | ✅ Complete (M1.5) | Full expression language with SHIFT, MIN, MAX, CLAMP |
| CLI | Evaluate YAML -> CSV + structured artifacts | Done (M1) | Deterministic artifacts with hashing |
| CLI Flags | --deterministic-run-id, --seed, determinism | Done (M1) | Full determinism support |
| API | /healthz, /run, /graph | Done (SVC-M0) | Full API implementation with parity tests |
| API Artifacts | /runs/{runId}/index, /runs/{runId}/series/{seriesId} | Done (SVC-M1) | Artifact serving via SYN-M0 adapters |
| UI | Health/Run/Graph demo, dark theme, simulation toggle | Done (UI-M0) | Complete SPA with API integration |
| UI Template Runner | Template gallery, dynamic forms, catalog selection, simulation workflow | Done (UI-M1) | Full template-based simulation runner |
| UI Parameter Forms | JSON schema-driven forms with validation | Done (UI-M1) | Auto-generated forms with type validation |
| UI Catalog Management | System catalog selection with metadata | Done (UI-M1) | Visual catalog picker with capabilities |
| UI Real API Integration | HTTP services, real simulation execution, artifact-first patterns | Done (UI-M2) ✅ | Complete FlowTime-Sim integration with series streaming |
| UI Graph | Structural table (order, degrees, roles) | Done | Visual DAG planned for later |
| Simulation Mode | Deterministic synthetic run + graph | Done | Toggle persisted (flag + query) |
| Synthetic Adapter (SYN-M0) | Read artifacts & produce series | Done (SYN-M0) | FileSeriesReader, RunArtifactAdapter complete |
| Backlog & Latency | Single-queue backlog + Little's Law latency | Planned (M7) | Not pulled forward in current roadmap |
| Testing | Core unit tests & API slice + CLI parity + artifact endpoints | Done | 35/35 tests passing including enhanced artifact validation |
| Docs | README, roadmap, node concepts, releases | Done | Complete documentation for all milestones |

**Charter Status**: This matrix reflects completed pre-charter work. Current development follows [Charter Roadmap](../milestones/CHARTER-ROADMAP.md) with artifacts-centric milestones M2.7-M2.9.
