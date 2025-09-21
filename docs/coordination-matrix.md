# M2.x Coordination Matrix

**Purpose:** Coordinate current M2.x milestone development across Engine, Sim, and UI  
**Scope:** Focus on current charter implementation (M2.6-M2.9)  
**Reference:** See [ROADMAP.md](ROADMAP.md) for complete development vision

---

## Active M2.x Milestones

| Milestone | Engine (FlowTime-Engine) | Sim (FlowTime-Sim) | UI (FlowTime.UI) | Acceptance Criteria |
|-----------|--------------------------|-------------------|------------------|-------------------|
| **M2.6 — Charter Foundation** ✅ | Charter paradigm adopted; export system for artifacts | — | Charter transition planning; legacy UI preserved | Charter workflows defined; export system produces structured artifacts |
| **M2.7 — File-Based Registry** | Directory structure for artifacts; `index.json` with run metadata; discovery CLI commands | Utilize same registry structure for sim artifacts; produce compatible metadata | Browse/filter runs by metadata; basic search | Registry enables artifact discovery; metadata schema consistent across tools |
| **M2.8 — Charter UI Integration** | Enhanced artifact endpoints; metadata enrichment; registry API | — | Charter workflow: Models→Runs→Artifacts→Learn | UI follows charter paradigm; incremental adoption without breaking changes |
| **M2.9 — Contextual Compare** | Cross-run comparison within artifact collections; metadata-driven grouping; delta computations | Provide comparison datasets; ensure deterministic output for cross-validation | Compare interface integrated with registry browser | Contextual comparisons leverage registry metadata; results reproducible |

---

## Foundation (Completed)

| Milestone | Status | Key Deliverables |
|-----------|--------|------------------|
| **M0 — Core Foundations** | ✅ | Grid evaluation; Series<T>; DAG processing; CLI; basic API endpoints |
| **M1 — Contracts & Artifacts** | ✅ | Artifact generation; JSON schemas; deterministic hashing; CLI/API parity |
| **M1.5 — Expression Engine** | ✅ | Expression parser; reference resolution; mathematical operations |

---

> **Note:** Post-M2.x planning available in the comprehensive [ROADMAP.md](ROADMAP.md). This matrix focuses on current implementation priorities.

