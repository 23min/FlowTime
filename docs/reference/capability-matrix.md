# Capability Matrix (Snapshot)

> **📋 Charter Notice**: This capability matrix reflects pre-charter milestones (M-00.00–M-02.06). For current development capabilities and roadmap, see [ROADMAP.md](../ROADMAP.md).

Legend: Done / Prototype / Partial / Planned.

| Area | Capability | Status | Notes |
|------|------------|--------|-------|
| Core Engine | Deterministic grid, Series<T>, DAG topo order | Done (M-00.00) | Cycle detection present |
| Nodes | const, basic expr (series * scalar, + scalar) | Done | Series-series & advanced built-ins in M-01.50 |
| Artifacts (Contracts Parity) | spec.yaml, run.json(source,grid+tz,align), manifest.json(rng), series/index.json, per-series hashes, placeholders (events, aggregates) | Done (M-01.00) | Deterministic hashing + JSON Schema validation |
| Expressions | Parser + refs + built-ins | ✅ Complete (M-01.50) | Full expression language with SHIFT, MIN, MAX, CLAMP |
| CLI | Evaluate YAML -> CSV + structured artifacts | Done (M-01.00) | Deterministic artifacts with hashing |
| CLI Flags | --deterministic-run-id, --seed, determinism | Done (M-01.00) | Full determinism support |
| API | /healthz, /run, /graph | Done (SVC-M-00.00) | Full API implementation with parity tests |
| API Artifacts | /runs/{runId}/index, /runs/{runId}/series/{seriesId} | Done (SVC-M-01.00) | Artifact serving via SYN-M-00.00 adapters |
| UI | Health/Run/Graph demo, dark theme, simulation toggle | Done (UI-M-00.00) | Complete SPA with API integration |
| UI Template Runner | Template gallery, dynamic forms, catalog selection, simulation workflow | Done (UI-M-01.00) | Full template-based simulation runner |
| UI Parameter Forms | JSON schema-driven forms with validation | Done (UI-M-01.00) | Auto-generated forms with type validation |
| UI Catalog Management | System catalog selection with metadata | Done (UI-M-01.00) | Visual catalog picker with capabilities |
| UI Real API Integration | HTTP services, real simulation execution, artifact-first patterns | Done (UI-M-02.00) ✅ | Complete FlowTime-Sim integration with series streaming |
| UI Graph | Structural table (order, degrees, roles) | Done | Visual DAG planned for later |
| Simulation Mode | Deterministic synthetic run + graph | Done | Toggle persisted (flag + query) |
| Synthetic Adapter (SYN-M-00.00) | Read artifacts & produce series | Done (SYN-M-00.00) | FileSeriesReader, RunArtifactAdapter complete |
| Backlog & Latency | Single-queue backlog + Little's Law latency | Planned (M-7) | Not pulled forward in current roadmap |
| Testing | Core unit tests & API slice + CLI parity + artifact endpoints | Done | 35/35 tests passing including enhanced artifact validation |
| Docs | README, roadmap, node concepts, releases | Done | Complete documentation for all milestones |

**Charter Status**: This matrix reflects completed pre-charter work. Current development follows [ROADMAP.md](../ROADMAP.md) with artifacts-centric milestones.
