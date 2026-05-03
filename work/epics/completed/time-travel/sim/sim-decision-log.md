# FlowTime.Sim Time-Travel Decision Log

This log records architecture and planning decisions specific to FlowTime.Sim as it adopts the KISS time-travel architecture. It complements the Engine decision log (`time-travel-architecture-ch6-decision-log.md`).

| ID | Date | Decision | Context | Status |
|----|------|----------|---------|--------|
| **DP-001** | 2025-10-11 | Break legacy template compatibility; FlowTime.Sim will adopt the KISS schema exclusively. | Readiness audit highlighted missing window/topology/semantics and permissive validation. | Accepted |
| **DP-002** | 2025-10-11 | Templates remain source-controlled artifacts generated via UI/AI assist; shared `includes/` is deferred until after time-travel milestones. | Maintains reviewability while tooling matures. | Accepted |
| **DP-003** | 2025-10-11 | FlowTime.Sim will emit `file://` bindings for telemetry-backed const nodes; TelemetryLoader remains responsible for preparing the referenced files/manifests. | Keeps Sim simple while allowing future loader evolution. | Accepted |
| **DP-004** | 2025-10-11 | Always embed provenance in generated models; `generator` begins with `flowtime-sim`. | Ensures downstream services can reason about model origin without side files. | Accepted |
| **DP-005** | 2025-10-11 | FlowTime.Sim performs maximal syntactic & semantic validation; telemetry availability remains Engine responsibility. | Keeps simulation focus while preventing invalid models. | Accepted |
| **DP-006** | 2025-10-11 | FlowTime.Sim does not generate or consume Gold telemetry; Engine owns synthetic/real Gold workflows. | Avoids scope creep and preserves KISS separation. | Accepted |
| **DP-007** | 2025-10-11 | Topology layout hints are provided post-compute (UI/tooling); FlowTime.Sim does not bake static coordinates. | Enables user-driven layout adjustments. | Accepted |
| **DP-008** | 2025-10-11 | Shared expression parser/validator will be extracted from Engine post-M-03.00 and consumed by FlowTime.Sim. | Guarantees consistent validation across surfaces. | Accepted (dependency) |
| **DP-009** | 2025-10-11 | Templates will declare an explicit semantic version under `TemplateMetadata.version`; provenance must surface the same value. | Enables consumers to track template evolution alongside schema changes. | Accepted |

Add new entries as decisions are made; update status when pending items are resolved.
