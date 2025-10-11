# FlowTime Capability Matrix (Legacy)

> **Archived:** This capability snapshot reflects the post-M-02.06 charter foundation state. Superseded by the time-travel planning documents and current roadmap.

<!-- original content below -->

# FlowTime Capability Matrix

**Current Status:** Post-M-02.06 Charter Foundation  
**Focus:** M-02.x capabilities and current implementation state  

## Core Capabilities (Completed)

| Area | Capability | Status | Implementation |
|------|------------|--------|----------------|
| **Engine Core** | Deterministic grid evaluation, Series<T>, DAG processing | ✅ Done | M-0 foundation |
| **Expression Language** | Parser, references, mathematical operations (SHIFT, MIN, MAX, CLAMP) | ✅ Done | M-01.05 complete |
| **Artifacts System** | Structured output, JSON schemas, deterministic hashing | ✅ Done | M-1 + M-02.06 export |
| **CLI Interface** | Run evaluation, deterministic output, artifact generation | ✅ Done | Full CLI parity |
| **API Endpoints** | /healthz, /run, /graph, artifact serving | ✅ Done | Core API complete |
| **UI Foundation** | Basic visualization, API integration | ✅ Done | Legacy UI preserved |

## M-02.x Charter Capabilities (In Progress)

| Area | Capability | Status | Target Milestone |
|------|------------|--------|------------------|
| **Artifact Registry** | File-based registry, metadata indexing, discovery | 🚧 In Progress | M-02.07 |
| **Charter UI** | Models→Runs→Artifacts→Learn workflow | 📋 Planned | M-02.08 |
| **Compare System** | Cross-run comparison, delta computation | 📋 Planned | M-02.09 |
| **Registry API** | Enhanced endpoints, metadata enrichment | 📋 Planned | M-02.08 |

## Technical Foundation

- **Testing:** Comprehensive unit and integration tests
- **Determinism:** Full reproducibility with seeded runs  
- **Documentation:** Complete milestone and API documentation
- **Parity:** CLI and API produce identical results

---

> **Reference:** See [ROADMAP.md](ROADMAP.md) for complete development vision and [M-02.x coordination matrix](coordination-matrix.md) for current milestone details.
