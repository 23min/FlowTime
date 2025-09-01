# Capability Matrix (Snapshot)

Legend: Done / Prototype / Partial / Planned.

| Area | Capability | Status | Notes |
|------|------------|--------|-------|
| Core Engine | Deterministic grid, Series<T>, DAG topo order | Done (M0) | Cycle detection present |
| Nodes | const, basic expr (series * scalar, + scalar) | Done | Series-series & advanced built-ins in M1 |
| Artifacts (Contracts Parity) | run.json, manifest.json, index.json, series hashes | Planned (M1) | Hash stability + JSON Schema validation |
| Expressions | Parser + refs + built-ins | Planned (M1.5) | Current inline scalar ops only |
| CLI | Evaluate YAML -> CSV (deterministic) | Done | Will emit artifacts at M1.5 |
| CLI Flags | --no-manifest, --via-api | Planned | Manifest suppression & parity mode |
| API | /healthz, /run, /graph | Prototype (SVC-M0) | Future: GET /runs/{id}/index, /series/{id} (post M1.5) |
| UI | Health/Run/Graph demo, dark theme, simulation toggle | Prototype | Will read index.json later |
| UI Graph | Structural table (order, degrees, roles) | Prototype | Visual DAG planned |
| Simulation Mode | Deterministic synthetic run + graph | Done | Toggle persisted (flag + query) |
| Synthetic Adapter (SYN-M0) | Read artifacts & produce series | Planned | Depends on M1.5 artifact freeze |
| Backlog & Latency | Single-queue backlog + Little's Law latency | Planned (M7) | Not pulled forward in current roadmap |
| Testing | Core unit tests & API slice | Partial | Add artifact schema + hash determinism tests M1.5 |
| Docs | README, roadmap, node concepts | Partial | Contracts spec to land M1.5 |

This matrix will evolve; see `docs/ROADMAP.md` for full milestone detail.
