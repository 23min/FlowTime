# Capability Matrix (Snapshot)

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
| UI Graph | Structural table (order, degrees, roles) | Done | Visual DAG planned for later |
| Simulation Mode | Deterministic synthetic run + graph | Done | Toggle persisted (flag + query) |
| Synthetic Adapter (SYN-M0) | Read artifacts & produce series | Done (SYN-M0) | FileSeriesReader, RunArtifactAdapter complete |
| Backlog & Latency | Single-queue backlog + Little's Law latency | Planned (M7) | Not pulled forward in current roadmap |
| Testing | Core unit tests & API slice + CLI parity + artifact endpoints | Done | 33/33 tests passing including artifact validation |
| Docs | README, roadmap, node concepts, releases | Done | Complete documentation for all milestones |

This matrix will evolve; see `docs/ROADMAP.md` for full milestone detail.
