# Capability Matrix (Early Snapshot)

| Area | Capability | Status | Notes |
|------|------------|--------|-------|
| Core Engine | Deterministic grid, Series<T>, DAG topo order | Done (M0) | Cycle detection present |
| Nodes | const, expr (series * scalar, + scalar) | Done | expr2 (series-series) planned M1+ |
| Expressions | Scalar ops (+,*) | Partial | Parser & extended funcs later |
| CLI | Evaluate YAML -> CSV | Done | `--via-api` future flag |
| API | /healthz, /run, /graph | Prototype | Structure mapping returned to UI |
| UI | Health/Run/Graph demo, dark theme, simulation toggle | Prototype | Structural table; charts pending |
| UI Graph | Structural table (order, degrees, roles) | New | Visual DAG planned |
| Simulation Mode | Deterministic synthetic run + graph | Done | Toggle persisted (flag + query) |
| Testing | Core unit tests | Partial | UI abstraction tests WIP |
| Docs | README, roadmap, node concepts | Partial | Structural graph documented |

Legend: Done / Prototype / Partial / Planned.

This matrix will evolve; see `docs/ROADMAP.md` for milestone detail.
