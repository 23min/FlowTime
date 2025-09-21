# FlowTime Capability Matrix

**Current Status:** Post-M2.6 Charter Foundation  
**Focus:** M2.x capabilities and current implementation state  

## Core Capabilities (Completed)

| Area | Capability | Status | Implementation |
|------|------------|--------|----------------|
| **Engine Core** | Deterministic grid evaluation, Series<T>, DAG processing | ✅ Done | M0 foundation |
| **Expression Language** | Parser, references, mathematical operations (SHIFT, MIN, MAX, CLAMP) | ✅ Done | M1.5 complete |
| **Artifacts System** | Structured output, JSON schemas, deterministic hashing | ✅ Done | M1 + M2.6 export |
| **CLI Interface** | Run evaluation, deterministic output, artifact generation | ✅ Done | Full CLI parity |
| **API Endpoints** | /healthz, /run, /graph, artifact serving | ✅ Done | Core API complete |
| **UI Foundation** | Basic visualization, API integration | ✅ Done | Legacy UI preserved |

## M2.x Charter Capabilities (In Progress)

| Area | Capability | Status | Target Milestone |
|------|------------|--------|------------------|
| **Artifact Registry** | File-based registry, metadata indexing, discovery | 🚧 In Progress | M2.7 |
| **Charter UI** | Models→Runs→Artifacts→Learn workflow | 📋 Planned | M2.8 |
| **Compare System** | Cross-run comparison, delta computation | 📋 Planned | M2.9 |
| **Registry API** | Enhanced endpoints, metadata enrichment | 📋 Planned | M2.8 |

## Technical Foundation

- **Testing:** Comprehensive unit and integration tests
- **Determinism:** Full reproducibility with seeded runs  
- **Documentation:** Complete milestone and API documentation
- **Parity:** CLI and API produce identical results

---

> **Reference:** See [ROADMAP.md](ROADMAP.md) for complete development vision and [M2.x coordination matrix](coordination-matrix.md) for current milestone details.
