# Gold-First KISS Architecture - Decision Log

**Branch:** gold-first-kiss  
**Status:** Complete

---

## Decision Record

### Q1: Capacity Handling Strategy ✅

**Decision:** **Option A - Capacity is Optional**

**Rationale:**
- Ship faster without forcing capacity estimation
- Accept visualization gaps (can't always show utilization)
- Honest about uncertainty when capacity unknown
- Observed `served` is ground truth for replay
- Teams not blocked by missing capacity data

**Implementation Notes:**
- Gold schema: `capacity_proxy` column is OPTIONAL (nullable)
- Engine: Works without capacity (uses observed served)
- API responses: Flag when capacity unavailable or inferred
- Templates: Can specify capacity assumptions for what-if scenarios
- Future (M-03.04+): Consider hybrid approach with inference

**Impact on Milestones:**
- M-03.00: Schema allows NULL capacity
- M-03.01: /state API handles missing capacity gracefully
- M-03.02: TelemetryLoader doesn't require capacity column
- M-03.03: Validation warnings if capacity missing but referenced

---

### Q2: Template Source Strategy ✅

**Decision:** **Option A - Hand-Authored Templates in Git**

**Rationale:**
- Ship faster (no Catalog_Nodes dependency)
- Version control with full git history
- Code review for topology changes
- Sufficient for demo (2-3 example templates)
- Clear path to add Catalog_Nodes later (M-04.00+)

**Implementation Notes:**
- Templates stored in `flowtime-vnext/templates/`
- Directory structure:
  - `templates/telemetry/` - For telemetry replay templates
  - `templates/simulation/` - For simulation templates (shared with Sim)
  - `templates/examples/` - Demo/tutorial templates
- Template format: YAML with {{parameter}} substitution
- Template versioning via git tags
- Future (M-04.00+): Add Catalog_Nodes discovery mode

**Impact on Milestones:**
- M-03.00: Define template schema format
- M-03.02: Implement template parser and parameter substitution
- M-03.02: Create 2-3 example templates (order-system, microservices)
- M-03.03: Template validation and error messages

---

### Q3: Validation Severity ✅

**Decision:** **Option C - Hybrid (Mode-Based Validation)**

**Rationale:**
- Telemetry mode: Warnings only (data is messy, don't block investigations)
- Simulation mode: Errors (data should be perfect, fail fast on bugs)
- Different use cases need different strictness levels
- Clear, simple rule: mode determines severity

**Implementation Notes:**
- Mode detection from `provenance.generator`:
  - `"telemetry-loader"` → permissive (warnings)
  - `"flowtime-sim"` → strict (errors)
  - `"manual"` → permissive (warnings)
- In **permissive mode**: ALL violations are warnings (including negative queue, malformed CSV, length mismatch)
- In **strict mode**: ALL violations are errors
- No per-rule configuration in M-03.03 (keep simple)
- Future (post-M-3): Add configurable validation rules if needed

**Impact on Milestones:**
- M-03.00: No validation yet (parser errors only)
- M-03.01: Basic validation (conservation, gaps)
- M-03.03: Full validation framework with mode-based severity
- M-03.03: Clear error messages and warnings in API responses

---

### Q4: Inference Algorithm Location ✅

**Decision:** **Option D - Defer to M-03.04+**

**Rationale:**
- Ship M-03.00-M-03.03 faster without inference complexity
- Focus on core time-travel capability (window, topology, APIs, telemetry)
- Inference is nice-to-have, not blocking
- Need user feedback to design algorithm correctly
- Can iterate in M-03.04 without blocking initial delivery

**Implementation Notes:**
- M-03.00-M-03.03: No capacity inference
- API responses: `capacity: null` when unavailable
- UI handling: Show "capacity: unknown" or hide utilization bar
- UI optional: Show `served/arrivals` ratio as proxy (with clear label: "throughput ratio, not utilization")
- M-03.04+: Implement in API layer (Option B from analysis)
- Future algorithm: Saturation method (~200 LOC in API layer)

**Clarification on Metrics:**
- `arrivals`: Work entering the node (from telemetry or queue)
- `served`: Work completed by the node
- `queue_depth`: Backlog (not same as arrivals)
- UI ratio: `served/arrivals` (what % of incoming work was completed)

**Impact on Milestones:**
- M-03.00: Schema allows null capacity
- M-03.01: /state API handles null capacity gracefully
- M-03.02: TelemetryLoader doesn't require capacity
- M-03.03: No inference implementation
- M-03.04: Add API-layer inference (post-P0)

---

### Q5: /state Endpoint Priority ✅

**Decision:** **Option A - /state in M-03.01 (Before TelemetryLoader), UI-First**

**Rationale:**
- Enable parallel work (UI integrates while backend builds telemetry)
- Core time-travel feature validated early
- Better testing with clean fixtures before real data
- Demo-ready sooner (Day 5 vs Day 8)
- Risk mitigation: time-travel works even if telemetry integration is delayed

**Key Insight:**
- Real ADX telemetry won't exist for a long time
- FlowTime itself will generate synthetic "Gold" telemetry from simulation
- Need synthetic gold telemetry fixtures for development and testing
- Self-hosting: FlowTime generates data that FlowTime then analyzes

**Implementation Notes:**
- M-03.00: Create test fixtures (3-4 example systems with CSV telemetry)
- M-03.00: Fixtures include: order-system, microservices, queue-heavy
- M-03.01: Implement /state and /state_window using fixtures
- M-03.01: UI integrates against fixture data
- M-03.02: TelemetryLoader (may initially load from FlowTime-generated CSVs, not ADX)
- Future: Helper tool to generate synthetic gold telemetry from simulation runs

**Milestone Sequence:**
```
M-03.00: Foundation + Fixtures
M-03.01: Time-Travel APIs (/state, /state_window)
M-03.02: TelemetryLoader + Templates
M-03.02.01: Simulation Run Orchestration
M-03.03: Validation + Polish
```

**Impact on Milestones:**
- M-03.00: Include fixture generation as deliverable
- M-03.01: Test with fixtures, not real ADX
- M-03.02: TelemetryLoader may load from files initially (ADX optional)
- M-03.02.01: Simulation orchestration ensures `/v1/runs` can create synthetic bundles without CLI involvement
- Post-M-3: Create synthetic gold telemetry generator tool

---

### Q6: Gold Schema Coordination ✅

**Decision:** **Option A (Simplified) with Pragmatic Flexibility**

**Rationale:**
- Gold team = FlowTime team (same people)
- Define minimal schema ourselves (we own it)
- Production has: App Insights (HTTP) + Service Bus (queues)
- TelemetryLoader handles optional columns (flexible)
- Can evolve schema based on learnings

**Schema Strategy:**
```sql
NodeTimeBin (Minimal + Pragmatic):
  REQUIRED: ts, node, arrivals, served, errors
  OPTIONAL: external_demand, queue_depth
  OPTIONAL: schema_version, extraction_ts, known_data_gaps
  REMOVED: capacity_proxy (per Q1), latency_*, utilization (engine derives)
```

**external_demand Handling:**
- Service Bus systems: `external_demand` = IncomingMessages (clear metric)
- HTTP services: `external_demand` = NULL or same as `arrivals` (no separate queue entry)
- Unknown buffer types: Defer until encountered, make column optional
- Decision: Make `external_demand` **optional**, use when available

**Telemetry Sources:**
- Service Bus: IncomingMessages (external_demand), CompletedMessages (arrivals), ActiveMessageCount (queue_depth)
- App Insights: Requests (arrivals), Successful responses (served), Failures (errors)
- Hybrid: Service Bus queue feeds into App Insights service

**Implementation Notes:**
- M-03.00: Define schema spec document
- M-03.00: Create synthetic gold generator (FlowTime simulation → Gold CSV)
- M-03.02: TelemetryLoader handles both Service Bus and App Insights sources
- M-03.02: Loader gracefully handles missing external_demand (common for HTTP)
- Future: Add new optional columns as new buffer types discovered

**Impact on Milestones:**
- M-03.00: Schema specification document + synthetic generator
- M-03.02: TelemetryLoader with flexible column handling
- M-03.03: Validation warns if external_demand missing (not error)

---

### Q7: Telemetry Generation Trigger ✅

**Decision:** **Option B - Explicit Generation Endpoint (No Auto-Capture)**

**Rationale:**
- Keeps simulation and telemetry replay concerns separated; no hidden side effects
- Operators choose when to create telemetry, matching future real-data flows
- Prevents filesystem path leakage in API responses by surfacing metadata only
- Easier to remove once live telemetry ingestion lands (isolated endpoint/UI action)
- Aligns with provenance goals: capture summary recorded alongside run metadata

**Implementation Notes:**
- Introduce `POST /v1/telemetry/captures`:
  - Accepts `{ source: { type: "run", runId }, output: { captureKey?|directory?, overwrite } }`
  - Runs `TelemetryGenerationService` to read canonical artifacts and emit bundle + `autocapture.json`
  - Enforces overwrite semantics (`overwrite: false` → 409 if bundle exists)
- `/v1/runs` remains focused on run creation (simulation or telemetry replay); **no** auto-generation path or `autoCapture` flag
- Run metadata now exposes telemetry summary block (available, generatedAtUtc, warningCount, optional sourceRunId) without directory paths
- Telemetry bundles always store `manifest.json` + metadata (`templateId`, `captureKey`, `sourceRunId`, `generatedAtUtc`, `parametersHash`)

**Impact on Milestones:**
- TT-M-03.17: Backend endpoint + service finalized; run metadata summary populated; golden responses updated
- TT-M-03.18: UI consumes telemetry summary to enable "Generate telemetry / Replay model / Replay telemetry" actions
- Ongoing: Telemetry capture guide, architecture docs, and decision log aligned to explicit trigger model

---

## Summary: All Decisions Complete ✅

**Milestones:** M-03.00 through M-03.03 (with M-03.02.01 covering simulation orchestration). TT‑M‑03.17 adds explicit telemetry generation; TT‑M‑03.18 polishes replay selection UI.

**Key Decisions:**
1. ✅ Capacity: Optional (Q1)
2. ✅ Templates: Git-based in flowtime-vnext/templates/ (Q2)
3. ✅ Validation: Mode-based (warnings for telemetry, errors for simulation) (Q3)
4. ✅ Inference: Defer to M-03.04+ (Q4)
5. ✅ /state Priority: M-03.01 before TelemetryLoader, with fixtures in M-03.00 (Q5)
6. ✅ Gold Schema: Simplified with flexible loader (Q6)
7. ✅ Telemetry Generation Trigger: Explicit endpoint (no auto-generation in `/v1/runs`) (Q7)

**Next Steps:**
1. Create detailed M-03.00 specification
2. Generate synthetic gold telemetry fixtures
3. Begin implementation

---
