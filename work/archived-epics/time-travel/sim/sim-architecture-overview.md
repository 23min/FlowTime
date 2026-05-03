# FlowTime.Sim Time-Travel Architecture Overview

**Status:** In Planning  
**Last Updated:** 2025-10-11  
**Audience:** FlowTime.Sim maintainers, Engine architects, tooling teams

---

## 1. Purpose & Scope

This document translates the KISS time-travel architecture into FlowTime.Sim responsibilities. It describes how the simulation surface must evolve so that simulation templates, CLI/service tooling, and generated models participate in the unified time-travel experience without reintroducing Gold-First complexity.

Focus areas:

1. Schema alignment — Templates produced by FlowTime.Sim must emit the exact window/topology/semantics contract consumed by Engine M-03.x APIs.
2. Validation — FlowTime.Sim should fail fast on invalid models using the same expression and topology rules as Engine, preventing drift between surfaces.
3. Provenance & observability — Generated artifacts must embed provenance that cross-SKU tooling can inspect.
4. Authoring experience — Templates should remain deterministic, reviewable artifacts in source control even while the UI/AI assist experiences mature.

---

## 2. Current Baseline

FlowTime.Sim currently targets the SIM-M-02.06 node-based schema:

- Templates comprise `metadata`, `parameters`, `grid`, `nodes`, and `outputs`.
- Expressions are validated syntactically (non-empty string), not semantically.
- Model generation strips metadata and outputs only the minimal YAML needed for Engine M-02.10.
- Provenance is optional and stored externally, with an embed flag for consumers.

The readiness audit (2025-10-11) highlighted critical gaps for time-travel:

- Missing `window` and `topology` sections and semantics mapping.
- No support for telemetry-backed const nodes, initial conditions, or node kinds.
- Mode awareness (simulation vs telemetry) is absent.
- Validation remains permissive and inconsistent with Engine checks.

---

## 3. Target Architecture Principles

| Principle | Description | Implication for FlowTime.Sim |
|-----------|-------------|------------------------------|
| **Single Mode Evaluation** | Engine treats telemetry and simulation identically. | FlowTime.Sim must emit full window/topology/semantics so Engine does not require custom code paths. |
| **Explicit Contracts** | All schema requirements are version-controlled. | Template generation must preserve metadata and emit canonical sections; no silent dropping of information. |
| **Deterministic Artifacts** | Model generation is repeatable and auditable. | CLI/service must embed provenance and produce consistent hashes for identical inputs. |
| **Shared Validation** | Expression and topology validation is common across surfaces. | Adopt the shared expression parser/validator and align topology checks with Engine. |
| **Loose Coupling to Telemetry** | Sim remains agnostic to telemetry loaders. | Templates may reference telemetry via `source` URIs, but FlowTime.Sim does not generate or manage Gold manifests. |

---

## 4. Future System Context

```
+----------------------+        +----------------------------+
| FlowTime.Sim CLI/API |        | FlowTime Engine (M-03.x)     |
|  - Template registry |        |  - /v1/runs                |
|  - Provenance        |        |  - /state, /state_window   |
|  - Validation        |        |  - Shared expression lib   |
+----------+-----------+        +--------------+-------------+
           |                                      ^
| model.yaml (schemaVersion: 1, time-travel format) |
           v                                      |
+----------------------+        +----------------------------+
| Source Control       | <----> | Time-Travel UI / Tooling   |
|  - templates         |        |  - Layout, hints           |
|  - fixtures          |        |  - User-driven parameters  |
+----------------------+        +----------------------------+
```

Key touchpoints:

- **Shared expression library** — extracted from Engine and consumed by FlowTime.Sim for consistent parsing/validation.
- **Topology semantics** — FlowTime.Sim becomes the source of truth for mapping node semantics in generated models; Engine simply consumes.
- **Provenance channel** — Always embedded inside model YAML so Engine and UI can reason about generator, parameters, and schema version without external files.

---

## 5. Deliverables Breakdown

1. **Schema support inside `Template` model**
   - Add `Window`, `Topology`, `Semantic` classes (mirror Engine contract).
   - Extend `TemplateNode` with `Source`, `Initial`, and `Kind` semantics and accept `file://` telemetry bindings (per DP-003).
   - Preserve metadata and parameters when exporting Engine-ready YAML, including a required `TemplateMetadata.version` (DP-009).

2. **Validation overhaul**
   - Use shared expression parser to detect self-referential SHIFT, invalid functions, etc.
  - Enforce presence of `initial` for stateful nodes; confirm semantics refer to existing node IDs.
  - Introduce mode concept (simulation vs telemetry) to drive validation severity.

3. **Service & CLI updates**
   - Embed provenance by default; respond with canonical schema.
   - Store models alongside provenance/manifests using new schema version.
   - Add API surface for template metadata (including topology coverage).

4. **Test suite upgrades**
   - Expand unit tests to cover window/topology serialization.
   - Add integration tests verifying generated models run through Engine M-03.x fixtures.
   - Introduce regression suites leveraging shared expression tests.

---

## 6. Interaction with Engine Roadmap

- **Post-M-03.00 follow-up:** Engine extracts the shared expression library and publishes it for reuse (`time-travel-planning-roadmap.md`, Post-M-03.00 follow-up #1). FlowTime.Sim cannot finalize validation upgrades until this artifact exists.
- **M-03.01 Engine APIs:** FlowTime.Sim schemas must be ready so that fixtures used by `/state` and `/state_window` rely on accurate semantics.
- **Telemetry loader (M-03.02):** FlowTime.Sim templates will be consumed by tooling that binds telemetry constants; ensuring template flexibility now avoids rework later.

---

## 7. Open Questions

| Topic | Decision Reference | Owner | Status |
|-------|--------------------|-------|--------|
| Telemetry coupling | DP-003 — FlowTime.Sim emits `file://` bindings; TelemetryLoader prepares referenced assets. | Architecture | Resolved |
| Layout hints | DP-007 — layout provided post-compute by UI/tooling; Sim does not bake coordinates. | UI + Sim | Resolved |
| Template versioning | DP-009 — Templates declare `TemplateMetadata.version`; provenance surfaces the same value. | Sim leads | Resolved |

Open questions feed into the decision log once resolved.

---

## 8. Next Steps

1. Finish the shared expression library extraction on the Engine side.
2. Start schema/model refactor in FlowTime.Sim (Template classes, generation pipeline).
3. Update CLI/service endpoints and tests to the new schema.
4. Document validation and publish integration fixtures.
5. Feed learnings back into the decision log and roadmap.
