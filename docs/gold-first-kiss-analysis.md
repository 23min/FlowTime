# Gold-First KISS Architecture Analysis

**Branch:** gold-first-kiss  
**Base Commit:** 08a7fddc ("docs(transitions): add time-travel audit reports")  
**Status:** Planning Analysis  

---

## Executive Summary

This document analyzes the KISS (Keep It Simple, Stupid) architecture for FlowTime time-travel capability and maps it to a concrete implementation plan starting from milestone M3. The KISS approach simplifies the original Gold-First architecture by treating telemetry as simple file input rather than requiring a complex adapter with semantic inference.

**Key Finding:** The KISS architecture is implementable and addresses the core time-travel requirements, but requires clarification on several aspects and renumbering from M1-M4 to M3.x milestones.

---

## 1. Current State Assessment (From Audit Reports)

### 1.1 Engine Capabilities (M2.10)

**What Works Well:**
- ✅ Core evaluation engine (const/expr/pmf/SHIFT)
- ✅ TimeGrid with binSize/binUnit (NEW schema)
- ✅ Graph construction and topological sorting
- ✅ Artifact generation (run.json, series CSVs, manifest)
- ✅ 390/393 tests passing (3 failures are performance benchmarks only)
- ✅ POST /v1/run endpoint (model evaluation)
- ✅ POST /v1/graph endpoint (DAG topology)

**Critical Gaps:**
- ❌ NO /state endpoint (single-bin snapshot)
- ❌ NO /state_window endpoint (time-series slice)
- ❌ NO window.start (absolute time anchoring)
- ❌ NO topology section in model schema
- ❌ NO semantics mapping (arrivals/served/capacity/queue)
- ❌ NO derived metrics (utilization, latency_min)
- ❌ NO node kinds (service vs queue vs router)

### 1.2 Architecture Maturity

**Component Status:**

| Component | Status | Notes |
|-----------|--------|-------|
| Core Engine | ✅ Stable | Solid foundation, minimal changes needed |
| Model Parser | 🟡 Needs Extension | Add window, topology, semantics |
| API Layer | 🟡 Needs Addition | Add /state and /state_window |
| TimeGrid | 🟡 Needs Extension | Add StartTimeUtc field |
| Registry | ✅ Stable | Artifact storage works |
| CLI | ✅ Stable | No changes needed for KISS |
| Tests | ✅ Solid | Good coverage, TDD-ready |

---

## 2. KISS Architecture Overview

### 2.1 Core Philosophy

**Principle:** Treat telemetry as just another input format (CSV files). No adapters, no semantic inference, no dual-mode complexity.

**Architecture Flow:**
```
Telemetry (ADX) → TelemetryLoader (200 LOC) → CSV Files → Templates (YAML) → Engine → Artifacts
```

### 2.2 Key Simplifications vs Original Gold-First

| Aspect | Original Gold-First | KISS Approach | Impact |
|--------|-------------------|---------------|---------|
| **Telemetry Integration** | Complex GoldToModel adapter (~1000 LOC) | Simple TelemetryLoader (~200 LOC) | 80% code reduction |
| **Capacity Handling** | Required capacity_proxy in Gold | Optional, inference-based | Gold schema simpler |
| **Topology Source** | Generated from Catalog_Nodes table | Hand-authored templates in git | Version control friendly |
| **Mode Detection** | Dual mode (Gold vs Model) with switching | Single mode (unified) | No mode-specific logic |
| **Semantic Mapping** | Inferred from Gold columns | Explicit in templates | Clear, reviewable |
| **Validation** | Blocking (errors) at adaptation | Post-evaluation (warnings) | More resilient |
| **Initial Conditions** | Implicit q0=0 defaults | Explicit in expressions | Prevents confusion |

### 2.3 What KISS Includes

**In Scope:**
1. ✅ Window with absolute time (start timestamp)
2. ✅ Topology section with nodes/edges
3. ✅ Semantics mapping (arrivals → series_id)
4. ✅ Node kinds (service, queue, router, external)
5. ✅ File sources for const nodes
6. ✅ Explicit initial conditions
7. ✅ TelemetryLoader for ADX extraction
8. ✅ Template system with parameters
9. ✅ Post-evaluation validation (warnings)
10. ✅ CSV manifest with provenance

**Out of Scope:**
1. ❌ Real-time telemetry (5-15 min lag acceptable)
2. ❌ Schema inference from telemetry patterns
3. ❌ Automatic topology discovery
4. ❌ Distributed execution (single-node)
5. ❌ Multi-tenant isolation in engine
6. ❌ Capacity estimation algorithms (manual/inferred only)
7. ❌ Interactive query rewrites (pre-defined models only)

---

## 3. Gap Analysis

### 3.1 Critical Question: Do /state Endpoints Exist in KISS?

**Answer:** YES, but not explicitly detailed in Ch1-6.

**Evidence from Ch2 (API Contracts section):**
- Section 2.4.2 references "GET /v1/state?ts={timestamp}"
- Section 2.4.3 references "GET /v1/state_window?start={ts}&end={ts}"

**Inference:** KISS architecture DOES include time-travel APIs, but:
- They're not in the M1-M4 milestone breakdown
- Implementation details are sketchy
- Need to clarify which milestone adds them

**Recommendation:** Add explicit milestone for /state endpoints (propose as M3.1 in renumbered plan)

### 3.2 Milestone Numbering Conflict

**Problem:** KISS uses M1-M4, but we're starting from M3.

**Proposed Renumbering:**

| KISS Original | Renumbered | Duration | Description |
|--------------|------------|----------|-------------|
| M1 | M3.0 | Foundation (window, topology, file sources, initial conditions) |
| M2 | M3.1 | Time-Travel APIs (/state, /state_window) |
| M3 | M3.2 | TelemetryLoader + Templates |
| M4 | M3.3 | Validation + Polish |

**Rationale:**
- M3.0 establishes schema foundation (aligns with Gold-First M3.0)
- M3.1 adds time-travel APIs (NEW, not in Gold-First M3.1)
- M3.2 integrates telemetry (similar to Gold-First M3.2 adapter)
- M3.3 handles validation/polish (replaces Gold-First M3.3 SLA metrics)

### 3.3 Schema Compatibility Question

**Issue:** KISS window/topology schema looks similar to Gold-First schema, but are they identical?

**Comparison:**

**Window (Identical):**
```yaml
# Both architectures:
window:
  start: "2025-10-07T00:00:00Z"
  timezone: "UTC"
```

**Topology (Minor Differences):**
```yaml
# KISS:
topology:
  nodes:
    - id: "OrderService"
      kind: "service"
      semantics:
        arrivals: "orders_arrivals"      # References series ID
        capacity: "orders_capacity"      # Optional

# Gold-First:
topology:
  nodes:
    - id: "OrderService"
      kind: "service"
      semantics:
        arrivals: "orders_arrivals_gold" # Suffix convention
        capacity: "capacity_inferred"    # Inference labeling
```

**Assessment:** Schemas are 95% compatible. Main difference is naming conventions, not structure.

### 3.4 Gold Schema Simplification

**KISS Simplification:**
```
NodeTimeBin (KISS):
  REQUIRED: ts, node, arrivals, served, errors
  OPTIONAL: external_demand, queue_depth
  REMOVED: capacity_proxy, latency_p50, latency_p95, utilization

NodeTimeBin (Original Gold-First):
  REQUIRED: ts, node, arrivals, served, errors, capacity_proxy
  OPTIONAL: queue_depth, latency_p50, etc.
```

**Impact:**
- ✅ Simpler Gold schema (less ETL work)
- ✅ No capacity estimation required
- ⚠️ Capacity inference needed for visualization
- ⚠️ Latency derived by engine (not pre-computed)

**Risk:** If Gold team already computed capacity_proxy, we're throwing away useful data.

**Mitigation:** Make capacity_proxy optional in KISS. If present, use it. If absent, infer.

### 3.5 Template System Complexity

**Question:** Is the template system (Ch3) more complex than the adapter it replaces?

**Template System (KISS M3.2):**
- ~300 LOC for template parser
- YAML templates with {{parameter}} substitution
- Include mechanism for shared definitions
- Parameter validation and defaults

**Gold Adapter (Original):**
- ~1000 LOC for KQL queries, transformation, topology mapping
- Runtime dependency on Catalog_Nodes table
- Semantic inference logic
- Dense fill and validation

**Assessment:** Template system is simpler (70% reduction) BUT adds operational burden:
- Templates must be authored by engineers
- Changes require git commits
- No automatic topology updates

**Tradeoff:** Operational burden vs code complexity. KISS chooses simpler code.

### 3.6 Capacity Inference Gap

**KISS Proposal:** Capacity is optional. When needed for visualization, infer from saturation patterns.

**Questions:**
1. What is the inference algorithm?
2. How is confidence labeled?
3. What happens when service never saturates?
4. Can users override inferred capacity?

**From KISS Ch1:**
```yaml
# Inference (for visualization only):
- id: capacity_inferred
  kind: expr
  expr: "infer_from_saturation(served_observed, queue_depth)"
```

**Problem:** `infer_from_saturation()` is NOT a defined operator.

**Gap:** Need to specify inference algorithm or make it a post-evaluation step (not in expressions).

**Recommendation:** 
- Option A: Add inference as API-layer post-processing (not engine concern)
- Option B: Make capacity truly optional (don't show in UI if unknown)
- Option C: Require capacity in templates (not optional)

### 3.7 Initial Conditions Enforcement

**KISS Principle:** Self-referencing SHIFT must have explicit initial conditions.

**Example:**
```yaml
# ✅ Valid:
- id: queue_depth
  kind: expr
  expr: "MAX(0, SHIFT(queue_depth, 1) + inflow - outflow)"
  initial: 5  # Explicit

# ❌ Invalid:
- id: queue_depth
  kind: expr
  expr: "MAX(0, SHIFT(queue_depth, 1) + inflow - outflow)"
  # Missing: initial field → Parse Error
```

**Questions:**
1. How does parser detect self-reference? (AST analysis)
2. What about indirect cycles? (A → B → A)
3. Can initial be an expression? (e.g., `initial: "q0_from_telemetry[0]"`)

**From KISS Ch5 (M3.0):**
- Parser analyzes SHIFT(node_id, k) in expression
- If node_id == current node AND k > 0 → Requires initial
- Initial can be scalar or reference to another series (not expression)

**Assessment:** Clear and implementable. Good engineering practice.

---

## 4. Architectural Decisions to Validate

### 4.1 Decision: Capacity is Optional

**KISS Claim:** "Capacity is unknowable until services hit saturation. All proxies are flawed."

**Counterargument:** 
- Configured capacity is known (e.g., K8s replicas × workers per pod)
- Max throughput is observable (historical peak)
- Service Bus has max-concurrent-calls setting

**Question for You:** Do you agree capacity should be optional, or should KISS require it?

**Impact of "Optional":**
- ✅ Simpler Gold schema
- ✅ Works for replays (observed served is truth)
- ❌ Hard to visualize bottlenecks without capacity
- ❌ What-if scenarios need capacity assumptions

**Recommendation:** Keep optional but provide clear guidance on inference methods.

### 4.2 Decision: Templates in Git (Not Generated)

**KISS Claim:** "Topology is design artifact requiring human judgment."

**Counterargument:**
- Catalog_Nodes table enables dynamic topology (new services auto-appear)
- Engineers maintain Catalog_Nodes anyway (for monitoring)
- Templates require duplicate maintenance (table + git)

**Question for You:** Should templates be:
- A) Hand-authored YAML in git (KISS proposal)
- B) Generated from Catalog_Nodes at request time (Gold-First proposal)
- C) Hybrid (base template + Catalog_Nodes enrichment)

**Impact:**
- A) More control, version history, but slower updates
- B) Automatic, but less control, no version history
- C) Best of both, but more complex

**Recommendation:** Start with A (KISS), add B in post-M3 if needed.

### 4.3 Decision: Validation as Warnings (Not Errors)

**KISS Claim:** "Real telemetry is messy. Blocking prevents investigation."

**Validation Examples:**
- Conservation: `arrivals - served ≈ queue[t] - queue[t-1]`
- Capacity realism: `capacity_inferred < 10 × MAX(served)`
- Gap detection: Missing bins in telemetry

**Question for You:** Should validation violations:
- A) Block run creation (errors)
- B) Allow run, emit warnings in manifest
- C) Configurable (strict vs permissive mode)

**Recommendation:** B (warnings) for telemetry replays, A (errors) for simulations.

### 4.4 Decision: Single Mode (No Gold vs Model Distinction)

**KISS Claim:** "Engine treats telemetry and simulated data identically."

**Implication:** No `mode: gold | model` field in API responses.

**Question:** How does UI know if data is observed vs modeled?

**Answer from KISS:** Provenance indicates generator:
```json
{
  "provenance": {
    "generator": "telemetry-loader|flowtime-sim|manual"
  }
}
```

**Assessment:** Works, but UI needs to check provenance to determine data source.

**Recommendation:** Acceptable. Simpler than dual-mode logic.

---

## 5. Proposed Milestone Plan (M3.x Series)

### 5.1 M3.0: Foundation

**Goal:** Extend model schema and engine to support window, topology, file sources, and explicit initial conditions.

**Deliverables:**
1. Window section in model schema (start, timezone)
2. Topology section (nodes with kind + semantics, edges)
3. File source support for const nodes (`source: "file://path"`)
4. Initial condition enforcement for self-referencing SHIFT
5. TimeGrid.StartTimeUtc field
6. Updated model parser and validator
7. Backward compatibility (legacy models still work)

**Success Criteria:**
- AC1: Model with window parses successfully
- AC2: Topology nodes reference series via semantics
- AC3: Const node loads data from CSV file
- AC4: Self-referencing SHIFT without initial → Parse Error
- AC5: TimeGrid computes bin timestamps from StartTimeUtc

**Tests:**
- 15 unit tests (file sources, initial conditions, validation)
- 3 integration tests (end-to-end with new schema)
- 1 golden test (fixed model → consistent artifacts)

**Files Modified:**
- `src/FlowTime.Core/TimeGrid.cs` (add StartTimeUtc)
- `src/FlowTime.Core/Models/ModelSchema.cs` (add Window, Topology)
- `src/FlowTime.Core/Models/ModelParser.cs` (parse new sections)
- `src/FlowTime.Core/Models/ModelValidator.cs` (validate topology)
- `src/FlowTime.Core/Evaluator.cs` (handle file sources, initial conditions)
- `tests/FlowTime.Core.Tests/` (new test classes)

**Dependencies:** None (can start immediately)

**Risks:**
- Low: Core extensions, well-understood domain
- Mitigation: Start with parser (no runtime changes), add evaluator logic incrementally

---

### 5.2 M3.1: Time-Travel APIs

**Goal:** Implement /state and /state_window endpoints for bin-level querying with derived metrics.

**Deliverables:**
1. GET /v1/runs/{runId}/state?binIndex={idx}
2. GET /v1/runs/{runId}/state_window?start={idx}&end={idx}
3. Derived metrics computation (utilization, latency_min)
4. Node coloring rules (green/yellow/red based on thresholds)
5. API contracts documented
6. Integration tests for time-travel flows

**Success Criteria:**
- AC1: /state returns single-bin snapshot with all semantic fields
- AC2: /state_window returns dense arrays for requested range
- AC3: Utilization computed as `served / capacity` (if capacity present)
- AC4: Latency_min computed as `queue / served × binMinutes` (Little's Law)
- AC5: Color assigned based on node kind (utilization for service, latency for queue)

**Tests:**
- 10 unit tests (metric computation, coloring logic)
- 5 integration tests (API endpoint contracts)
- 2 golden tests (fixed run → consistent state responses)

**Files Modified:**
- `src/FlowTime.API/Program.cs` (add /state endpoints)
- `src/FlowTime.Core/Metrics/` (new namespace for derived metrics)
- `src/FlowTime.Core/Metrics/UtilizationComputer.cs`
- `src/FlowTime.Core/Metrics/LatencyComputer.cs`
- `src/FlowTime.Core/Metrics/ColoringRules.cs`
- `src/FlowTime.Contracts/StateResponse.cs` (new DTO)
- `tests/FlowTime.API.Tests/` (endpoint tests)

**Dependencies:** M3.0 (requires window and topology)

**Risks:**
- Medium: API design must be UI-friendly
- Mitigation: Mock UI consumption in tests, validate with UI team

---

### 5.3 M3.2: TelemetryLoader + Templates

**Goal:** Implement TelemetryLoader to extract ADX data and Template system to instantiate models.

**Deliverables:**
1. TelemetryLoader class (ADX connection, KQL queries, CSV writing)
2. Dense bin filling (zero-fill with warnings for gaps)
3. Manifest generation (provenance, checksums, warnings)
4. Template parser (YAML with {{parameter}} substitution)
5. Template repository (git-based, version-controlled)
6. CLI tool for telemetry extraction
7. Integration tests with test ADX cluster

**Success Criteria:**
- AC1: TelemetryLoader connects to ADX and executes KQL
- AC2: Missing bins detected and zero-filled with warning
- AC3: CSV files written with correct row counts
- AC4: Manifest includes provenance and warnings
- AC5: Template parser substitutes parameters correctly
- AC6: Instantiated model references telemetry CSV files

**Tests:**
- 20 unit tests (KQL generation, dense fill, template parsing)
- 5 integration tests (ADX connection, file I/O)
- 2 golden tests (fixed query → consistent CSV output)

**Files Created:**
- `src/FlowTime.Adapters.Telemetry/` (new project)
- `src/FlowTime.Adapters.Telemetry/TelemetryLoader.cs`
- `src/FlowTime.Adapters.Telemetry/KqlBuilder.cs`
- `src/FlowTime.Adapters.Telemetry/DenseFiller.cs`
- `src/FlowTime.Adapters.Telemetry/ManifestWriter.cs`
- `src/FlowTime.Templates/` (new project)
- `src/FlowTime.Templates/TemplateParser.cs`
- `src/FlowTime.Templates/ParameterResolver.cs`
- `templates/telemetry/` (directory for YAML templates)

**Dependencies:** M3.0 (requires file source support)

**Risks:**
- Medium: ADX authentication and permissions
- Mitigation: Use Managed Identity, document auth setup clearly

---

### 5.4 M3.3: Validation + Polish

**Goal:** Add post-evaluation validation, observability, and production-ready polish.

**Deliverables:**
1. Validation framework (conservation, capacity realism)
2. Warning collection and reporting
3. Structured logging (trace IDs, timing)
4. Performance benchmarks
5. Documentation (API guide, template authoring guide)
6. Deployment guide (ADX setup, config)

**Success Criteria:**
- AC1: Conservation violations detected and logged as warnings
- AC2: Capacity inference warnings include confidence levels
- AC3: All operations have structured logs with trace IDs
- AC4: Performance benchmark: 288-bin model evaluates in <500ms
- AC5: Documentation complete (API, templates, deployment)

**Tests:**
- 15 unit tests (validation rules, warning collection)
- 3 integration tests (end-to-end with validation)
- 1 performance test (large models)

**Files Modified:**
- `src/FlowTime.Core/Validation/` (new namespace)
- `src/FlowTime.Core/Validation/ConservationValidator.cs`
- `src/FlowTime.Core/Validation/CapacityValidator.cs`
- `src/FlowTime.Core/Logging/` (structured logging)
- `docs/api/` (API documentation)
- `docs/templates/` (template authoring guide)

**Dependencies:** M3.0, M3.1, M3.2 (integrates all)

**Risks:**
- Low: Polish and observability, no core functionality
- Mitigation: Focus on user-facing quality (error messages, docs)

---

## 6. Questions for Validation

Before proceeding, I need your input on these decisions:

### Q1: Capacity Handling

**Should capacity be:**
- A) Optional (KISS proposal) - infer when needed, label confidence
- B) Required in Gold schema - simpler, always available
- C) Hybrid - optional in Gold, required in templates

**Implications:**
- A: Simplest Gold schema, but inference complexity
- B: Always available, but forces capacity estimation in ETL
- C: Balanced, but more configuration

**Your Decision:** _______________

### Q2: Template Source

**Should topology come from:**
- A) Hand-authored templates in git (KISS proposal)
- B) Generated from Catalog_Nodes table (Gold-First proposal)
- C) Hybrid - base template + Catalog enrichment

**Implications:**
- A: Full control, version history, but manual updates
- B: Automatic, but less control
- C: Complex but flexible

**Your Decision:** _______________

### Q3: Validation Severity

**Should validation violations:**
- A) Be warnings (KISS proposal) - allow run with issues
- B) Be errors - block run on violations
- C) Configurable per validation rule

**Implications:**
- A: Resilient to messy data, but may hide issues
- B: Strict, but may block legitimate investigations
- C: Flexible, but more config complexity

**Your Decision:** _______________

### Q4: Inference Algorithm

**For capacity inference, should we:**
- A) Implement heuristic (max of served when queue > 0)
- B) Make it API-layer concern (not engine)
- C) Don't infer - require explicit capacity
- D) Defer to post-M3 (ship without inference)

**Implications:**
- A: Engine complexity, but self-contained
- B: API complexity, engine stays pure
- C: Simplest, but limits visualization
- D: Ship faster, add later

**Your Decision:** _______________

### Q5: /state Endpoint Priority

**In renumbered milestones, /state should be:**
- A) M3.1 (before TelemetryLoader) - UI needs it for demo
- B) M3.3 (after TelemetryLoader) - integration first
- C) Split: basic /state in M3.1, full features in M3.3

**Implications:**
- A: Enables UI demo quickly, but without telemetry
- B: Complete integration, but delays UI work
- C: Balanced, phased delivery

**Your Decision:** _______________

### Q6: Gold Schema Changes

**Should we coordinate with Gold team on:**
- A) Schema simplification (remove capacity_proxy) - KISS proposal
- B) Keep existing schema (make engine flexible)
- C) Extend schema (add external_demand)

**Implications:**
- A: Cleaner schema, but coordination effort
- B: No dependencies, adapter handles variations
- C: Richer data, but more ETL work

**Your Decision:** _______________

---

## 7. Risk Assessment

### 7.1 High Risks

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| ADX authentication/permissions issues | High | High | Use Managed Identity, thorough auth testing |
| Template system complexity creep | Medium | High | Keep parameter types simple (string, number, bool only) |
| Capacity inference unreliable | Medium | Medium | Label confidence clearly, allow manual override |

### 7.2 Medium Risks

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| File path security (directory traversal) | Low | High | Validate paths, restrict to model directory tree |
| CSV parsing edge cases (encoding, line endings) | Medium | Low | Use robust library (CsvHelper), test CRLF/LF |
| Large telemetry files (memory usage) | Low | Medium | Stream CSVs row-by-row, don't load all in memory |
| Validation false positives | Medium | Low | Make thresholds configurable, clear warning messages |

### 7.3 Low Risks

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| Backward compatibility issues | Low | Medium | Keep legacy model parsing, add tests |
| Initial condition type confusion | Medium | Low | Clear docs, good error messages |
| Template parameter errors | Low | Low | Validation at instantiation time |

---

## 8. Comparison: KISS vs Gold-First

### 8.1 Code Complexity

| Component | Gold-First | KISS | Reduction |
|-----------|-----------|------|-----------|
| Adapter/Loader | 1000 LOC | 200 LOC | 80% |
| Template System | 0 LOC | 300 LOC | +300 LOC |
| Engine Extensions | 400 LOC | 400 LOC | 0% |
| API Layer | 500 LOC | 500 LOC | 0% |
| **Total** | **1900 LOC** | **1400 LOC** | **26%** |

### 8.2 Operational Complexity

| Aspect | Gold-First | KISS | Winner |
|--------|-----------|------|---------|
| Gold Schema | Complex (many columns) | Simple (base observations) | KISS |
| Topology Management | Dynamic (Catalog_Nodes) | Static (git templates) | Gold-First |
| Capacity Handling | Required in Gold | Optional, inferred | KISS |
| Mode Detection | Dual mode logic | Single mode | KISS |
| Schema Evolution | Adapter versioning | Template versioning | Tie |

### 8.3 Feature Completeness

| Feature | Gold-First | KISS | Notes |
|---------|-----------|------|-------|
| Time-Travel APIs | ✅ | ✅ | Both have /state endpoints |
| Absolute Time | ✅ | ✅ | Both use window.start |
| Topology | ✅ | ✅ | Both have nodes/edges/semantics |
| Capacity | Required | Optional | KISS more flexible |
| SLA Metrics | Dedicated milestone (M3.3) | Post-evaluation | Gold-First more complete |
| Overlay Scenarios | Dedicated milestone (M3.4) | Not mentioned | Gold-First wins |
| Multi-class Flows | Future (M4.0+) | Not mentioned | Tie (both defer) |

**Assessment:** Gold-First is more feature-complete. KISS is simpler but missing:
- Explicit SLA metrics aggregation
- Overlay scenario support (Gold + modeled)
- Multi-flow handling

**Recommendation:** Start with KISS for M3.0-M3.3, add Gold-First features in M3.4+.

---

## 9. Recommendations

### 9.1 Adopt KISS with Modifications

**Recommendation:** Use KISS architecture as foundation with these changes:

1. **Renumber Milestones:** M1→M3.0, M2→M3.1, M3→M3.2, M4→M3.3
2. **Swap M3.1 and M3.2:** Implement /state APIs before TelemetryLoader (enables UI demo)
3. **Add M3.4:** Overlay scenarios (borrowed from Gold-First)
4. **Make Capacity Optional:** Use inference when needed, label confidence
5. **Hybrid Templates:** Start with git templates, add Catalog_Nodes enrichment in M4
6. **Validation as Warnings:** For telemetry replays, errors for simulations
7. **Defer Inference:** Ship M3.0-M3.3 without capacity inference, add in M3.4

### 9.2 Modified Milestone Plan

| Milestone | Duration | Description | Priority |
|-----------|----------|-------------|----------|
| M3.0 | Foundation (window, topology, file sources, initial conditions) | P0 |
| M3.1 | Time-Travel APIs (/state, /state_window, derived metrics) | P0 |
| M3.2 | TelemetryLoader + Templates | P0 |
| M3.3 | Validation + Polish | P0 |
| M3.4 | Overlay Scenarios + Capacity Inference | P1 |

### Implementation Sequence

1. **Decision Phase**:
   - Resolve Q1-Q6 architectural questions
   - Obtain stakeholder approval
   - Document decisions

2. **Planning Phase**:
   - Create detailed M3.0 specification
   - Set up development environment
   - Prepare fixtures

3. **Implementation Phase**:
   - M3.0 → M3.1 → M3.2 → M3.3
   - Continuous testing
   - Documentation updates

4. **Demo Phase**:
   - End-to-end demo
   - Retrospective
   - Next milestone planning

---

## 11. Recommendation

The KISS architecture is a **sound, implementable approach** that simplifies telemetry integration while preserving time-travel capability. The proposed M3.0-M3.3 milestones deliver the core P0 capability.

### 9.3 Next Steps

**Immediate Actions:**
1. **Decision Phase** (1 day):
   - Review this analysis document
   - Answer Q1-Q6 validation questions
   - Approve modified milestone plan
   
2. **Planning Phase** (1 day):
   - Create detailed M3.0 specification document
   - Define acceptance criteria per AC
   - Set up test fixtures and golden data
   
3. **Implementation Phase** (10 days):
   - Execute M3.0 → M3.3 milestones
   - Daily standups, incremental commits
   - Merge to main after each milestone
   
4. **Demo Phase** (1 day):
   - Integrate with UI
   - Run end-to-end telemetry replay
   - Document lessons learned

**Total Timeline:** 13 days (2.5 weeks)

---

## 10. Open Questions

These questions require your input before proceeding:

1. **Q1-Q6 from Section 6:** Need decisions on capacity, templates, validation, inference, priorities, Gold schema

2. **ADX Access:** Do we have test ADX cluster credentials? Need connection string and Managed Identity setup.

3. **Template Repository:** Should templates live in:
   - Same repo as engine (`/templates`)
   - Separate repo (`flowtime-templates`)
   - Shared config repo

4. **Provenance Requirements:** What fields are mandatory for audit/compliance?
   - extraction_ts
   - source (ADX cluster URI)
   - loader_version
   - user_id / request_id?

5. **Performance Targets:** Are these acceptable?
   - TelemetryLoader: <5s for 288 bins
   - Model evaluation: <500ms for 288 bins
   - /state API: <50ms per request
   - /state_window: <200ms for 144 bins

6. **Backward Compatibility:** Should M3.0 still support M2.10 models without window/topology?
   - Recommendation: Yes (add tests)

7. **Multi-Repository Coordination:** Is FlowTime-Sim repo affected?
   - Need to align template formats
   - Ensure Sim generates KISS-compatible models

---

## 11. Conclusion

The KISS architecture is a **sound, implementable approach** that simplifies telemetry integration while preserving time-travel capability. The proposed M3.0-M3.3 milestones are achievable in 10 days with a small team.

**Key Strengths:**
- ✅ 80% code reduction in adapter/loader
- ✅ Simpler Gold schema
- ✅ Version-controlled templates
- ✅ Clear separation of concerns
- ✅ Testable without ADX

**Key Tradeoffs:**
- ⚠️ Manual template authoring (vs automatic generation)
- ⚠️ Capacity inference complexity (vs required in Gold)
- ⚠️ Missing overlay scenarios (vs Gold-First M3.4)

**Recommendation:** **Proceed with KISS for M3.0-M3.3, add Gold-First features in M3.4+.**

**Next Action:** Answer validation questions Q1-Q6 to finalize plan.

---

**End of Analysis Document**
