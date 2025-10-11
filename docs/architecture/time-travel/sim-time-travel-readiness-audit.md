# FlowTime.Sim Time-Travel Readiness Audit (Post-Consolidation)

**Repository:** flowtime-vnext  
**Audit Date:** 2025-10-11  
**Auditor:** Codex (GPT-5)  
**Reference Architecture:** `docs/architecture/time-travel/time-travel-architecture-ch1..ch6`, planning roadmap & decisions  
**Scope Notes:** FlowTime Engine documentation in `docs/` is authoritative; `docs-sim/` content is excluded by request.

---

## Executive Summary

- **Status:** 🔴 **Not ready for M-03.x time-travel milestones.** FlowTime.Sim continues to target the legacy SIM-M-02.06 node-based schema and cannot emit models that satisfy the KISS time-travel contract (window/topology/semantics, template metadata, telemetry sources, or mode-aware validation).
- **Key blockers:** Missing `window` anchoring, topology & semantics mapping, telemetry file binding, template classification, and validation pathways. The current generation pipeline deliberately strips metadata that KISS treats as required.
- **Opportunities:** Core pieces—template parsing, deterministic provenance, service/CLI surfaces, and parameter substitution—are solid foundations that can be extended. Tests already cover many edge cases and can be evolved to protect the new schema.

---

## Current Implementation Snapshot

| Area | Observations | Evidence |
|------|--------------|----------|
| **Template model** | `Template` only models metadata, parameters, grid, nodes, outputs, RNG. No `window`, `topology`, `semantics`, or template classification. Nodes support `values` / `expression` / `pmf` but not `source`, `initial`, or semantics references. | `src/FlowTime.Sim.Core/Templates/Template.cs:9-112`, `TemplateNode` definition |
| **Generation pipeline** | `NodeBasedTemplateService` caches YAML, substitutes `${param}`, then `ConvertToEngineSchema` strips `metadata`/`parameters`, rewrites `expression→expr`, converts `source→series`, and ensures `schemaVersion: 1`. No additions for new sections. | `src/FlowTime.Sim.Core/Services/NodeBasedTemplateService.cs:277-360` |
| **Templates shipped** | Sample templates (e.g. `transportation-basic`) use inline arrays for const nodes, lack time anchoring, topology, semantics, or node kinds; comments reference SIM charter. | `templates/transportation-basic.yaml:1-83` |
| **Service & CLI** | `/api/v1/templates/*` and CLI verbs wrap the node-based service, optionally embedding provenance. Responses and stored models remain legacy schema; inputs allow arbitrary parameters but no mode selection. | `src/FlowTime.Sim.Service/Program.cs:240-315`, `src/FlowTime.Sim.Cli/Program.cs:252-305` |
| **Provenance** | `ProvenanceService` emits `source: flowtime-sim`, `generator: flowtime-sim/{version}`, hash-based `modelId`. Embedding injects comment `# Model provenance (SIM-M-02.07)` after `schemaVersion`. | `src/FlowTime.Sim.Core/Services/ProvenanceService.cs:19-63`, `ProvenanceEmbedder.cs:17-66` |
| **Tests** | Unit tests assert legacy expectations (nodes require `values`/`dependencies`, metadata removed in engine model). No coverage for `window`, topology validation, or telemetry-driven const nodes. | `tests/FlowTime.Sim.Tests/NodeBased/TemplateParserTests.cs:7-118`, `ModelGenerationTests.cs` (legacy assertions) |

---

## Alignment Against KISS Time-Travel Requirements

| KISS Requirement | Expected Behaviour | FlowTime.Sim Status |
|------------------|--------------------|---------------------|
| **Absolute window** (`window.start`, `timezone`) | Models anchor bins to real time; UI/API derive timestamps. | ❌ Absence of `window` type; only optional `grid.start` comment. (`TemplateGrid.Start` unused) |
| **Topology & semantics** | Nodes typed (`service\|queue\|router\|external`) with semantics mapping for arrivals/served/errors/etc, plus edges & UI hints. | ❌ No topology model, no semantics, no node kinds, no edges. |
| **Telemetry-friendly const nodes** | `source: "file://..."` to bind telemetry exports, support `values` only for synthetic/defaults. | ❌ Nodes expose `values` only; no `source` field, loader conversions, or manifest support. |
| **Explicit initial conditions** | Self-referential SHIFT requires `initial`. | ❌ No `initial` property; validators don't check SHIFT usage. |
| **Template classification & provenance** | `templateType`, `version`, `generator` enumerations align with cross-surface tooling; provenance embedded by default. | ⚠️ Provenance exists but uses legacy format (`# Model provenance` comment, generator string with version). Template metadata stripped during generation. |
| **Mode-based validation** | Simulation templates fail fast on missing data; telemetry templates warn. | ❌ Validation is permissive (`ValidateParametersAsync` returns success; TemplateParser only enforces legacy schema). No mode notion. |
| **Synthetic Gold generator** | Simulation output → Gold CSV to feed time-travel demos/tests. | ❌ No tooling; service/CLI produce only engine YAML. |

---

## Detailed Findings

### 1. Schema Gaps (Blocking)

1. **No `window` section:** Templates and generated models omit the absolute time anchor required by `/state` APIs and scrubber UI. `TemplateGrid` holds `BinSize`, `BinUnit`, and optional `Start`, but conversion drops even that. (`Template.cs:57-63`, `NodeBasedTemplateService.cs:277-360`)
2. **Missing topology + semantics:** The object model has no classes for topology nodes/edges or semantic bindings, and the generator never emits them. Without semantics the Engine cannot surface arrivals/served per component. (No references to `topology` or `semantics` in `src/FlowTime.Sim.Core`.)
3. **Node typing absent:** Architecture depends on `kind` (service/queue/router/external) to drive validation and UI. Templates only distinguish const/expr/pmf node types; there is no slot for operational kind or UI hints. (`TemplateNode` definition)
4. **Telemetry binding unsupported:** Const nodes require numeric arrays; there is no `source` or `file:` integration, and parameter substitution assumes arrays (`ParameterSubstitution` only replaces strings inside existing scalar/array fields). (`TemplateNode.Values`, `ParameterSubstitution.SubstituteInNode`)
5. **SHIFT initial conditions unchecked:** Validator enforces `dependencies` but not `initial`. Self-referential expressions can be emitted without the guard mandated by KISS. (`TemplateParser.ValidateNode`, absence of SHIFT analysis)
6. **Outputs still legacy:** Generation rewrites `source→series`, `filename→as`, but keeps per-output `id` and drops `exclude`. There is no enforcement of KISS default outputs (wildcard semantics). (`NodeBasedTemplateService.ConvertToEngineSchema`)

### 2. Template Authoring & Generation Pipeline

- **Metadata stripped deliberately:** `ConvertToEngineSchema` removes `metadata:` and `parameters:` sections entirely; KISS expects to retain provenance and descriptive fields for review. (`NodeBasedTemplateService.cs:299-337`)
- **No template versioning/features:** Comments in `NodeBasedTemplateService` still reference "SIM-M-02.06-CORRECTIVE". There is no branching or opt-in path for the KISS schema, so telemetry-driven templates cannot be authored.
- **Parameter substitution limited to string interpolation:** Works for arrays/numbers but cannot introduce new sections; hence authors cannot add `topology:` via placeholders without extending the object model. (`ParameterSubstitution.cs`)

### 3. Service & CLI Surfaces

- **REST API remains simulation-centric:** `/api/v1/templates/{id}/generate` returns legacy model YAML plus provenance JSON. Query flag `embed_provenance` defaults on, but provenance format predates KISS and lacks `generator: flowtime-sim` enum-style value (includes version). (`FlowTime.Sim.Service/Program.cs:264-314`)
- **No mode exposure:** There is no way to request `mode=simulation` vs `mode=telemetry`, nor to toggle validation behaviours described in planning decisions.
- **Storage layout incompatible with planned tooling:** Models written to `data/models/{templateId}/{hash}/model.yaml`; there are no manifests, telemetry exports, or schema negotiation—features expected for TelemetryLoader and synthetic gold workflows.

### 4. Testing & Quality Signals

- **Tests reinforce legacy schema:** `TemplateParserTests` expect `source`/`filename`, not `series`/`as`, and never assert on topology/window. (`tests/FlowTime.Sim.Tests/NodeBased/TemplateParserTests.cs`)
- **No guardrails for new requirements:** There are no failing tests indicating missing topology/semantics support; without deliberate breaking changes these regressions will go unnoticed.

---

## Decision Points

1. **Template compatibility strategy**  
   - **Decision:** Break legacy compatibility; FlowTime.Sim will adopt the KISS time-travel schema exclusively, accepting breaking changes to templates, services, and tests.  
   - **Implications:** Aggressive schema evolution is allowed as long as accompanying tests catch regressions before integration. Migration effort focuses on new schema rather than shims.

2. **Template authoring workflow**  
   - **Decision:** Long term, UI and FlowTime.Sim will collaborate to generate templates so authors do not handcraft YAML. For now, AI assist produces templates that are committed to source control. Shared `includes/` style composition is deferred until after the M-3/time-travel milestone.  
   - **Implications:** Current work should enable programmatic template creation while keeping files reviewable; refactor for includes later.

3. **Telemetry binding integration**  
   - **Decision:** FlowTime.Sim will emit `file://` bindings for telemetry-backed const nodes; TelemetryLoader remains responsible for preparing the referenced files/manifests so Sim stays loosely coupled.  
   - **Implications:** Templates/validators must recognize `file://` URIs, and documentation should point to TelemetryLoader for manifest generation. Future loader enhancements can evolve without requiring Sim changes.

4. **Provenance representation**  
   - **Decision:** Always embed provenance in generated models; FlowTime.Sim should identify itself as the generator (e.g., `generator: "flowtime-sim"` with version carried separately). The `manual` category remains undefined until a human-authored path emerges.  
   - **Implications:** Update provenance embedder to match enum expectations while preserving version metadata and ensuring downstream components rely on embedded provenance.

5. **Validation responsibilities**  
   - **Decision:** FlowTime.Sim should perform maximal validation before emitting models, covering syntactic structure and semantic coherence where feasible (e.g., node references, SHIFT requirements). Sim remains simulation-focused—telemetry availability is out of scope and should be enforced by Engine ingestion layers.  
   - **Implications:** Enhance validators to fail fast on schema issues without attempting DAG construction; document division between simulation semantics vs telemetry ingestion.

6. **Synthetic Gold ownership**  
   - **Decision:** FlowTime.Sim will not handle Gold generation or ingestion. Engine tooling remains responsible for converting simulation output into Gold telemetry and for consuming Gold datasets (real or synthetic).  
   - **Implications:** Remove Gold-related expectations from Sim roadmap; ensure documentation points to Engine utilities for Gold workflows.

7. **Topology layout hints**  
   - **Decision:** Layout hints should be derived post-processing (e.g., by UI or downstream tooling) so users can parameterize layouts. FlowTime.Sim should not attempt to compute or bake in static coordinates during model generation.  
   - **Implications:** Templates/interface should allow UI-provided hints or overrides; Sim focuses on semantics rather than visualization geometry.

8. **Template versioning**  
   - **Decision:** Templates will declare a semantic version under `TemplateMetadata.version`, and provenance must surface the same value.  
   - **Implications:** Generation code, CLI/service responses, and tests must require the version field so consumers can track template evolution alongside schema changes.

---

## Suggested Next Steps

1. **Define the FlowTime.Sim KISS schema extensions** (window/topology/semantics/templateType) in code alongside backward compatibility strategy; introduce data structures and validation covering new sections.
2. **Upgrade generation pipeline** to preserve metadata, add topology, permit telemetry `source` bindings, enforce SHIFT `initial`, and embed provenance in the new canonical format.
3. **Introduce mode awareness** (simulation vs telemetry) in services/CLI, including stricter validation for simulation and warning surfaces for telemetry.
4. **Extend templates & fixtures** under `templates/` to demonstrate time-travel-ready authoring; migrate tests to assert new schema pieces and prevent regressions.
5. **Plan synthetic telemetry tooling integration**, deciding whether FlowTime.Sim will ship CSV export/gold manifests or delegate to Engine utilities.
6. **Document migration strategy** for existing users (CLI/service endpoints) so they understand the schema change, provenance updates, and new validation flow ahead of M-03.00.

With these steps FlowTime.Sim can align with the authoritative KISS architecture and provide the simulation side of the time-travel experience without reintroducing the complexity the consolidation set out to remove.

See the companion architecture package under `docs/architecture/time-travel/sim/` for the evolving design, schema specification, and implementation plan that operationalize these recommendations.
