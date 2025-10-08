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
- Future (M3.4+): Consider hybrid approach with inference

**Impact on Milestones:**
- M3.0: Schema allows NULL capacity
- M3.1: /state API handles missing capacity gracefully
- M3.2: TelemetryLoader doesn't require capacity column
- M3.3: Validation warnings if capacity missing but referenced

---

### Q2: Template Source Strategy ✅

**Decision:** **Option A - Hand-Authored Templates in Git**

**Rationale:**
- Ship faster (no Catalog_Nodes dependency)
- Version control with full git history
- Code review for topology changes
- Sufficient for demo (2-3 example templates)
- Clear path to add Catalog_Nodes later (M4.0+)

**Implementation Notes:**
- Templates stored in `flowtime-vnext/templates/`
- Directory structure:
  - `templates/telemetry/` - For telemetry replay templates
  - `templates/simulation/` - For simulation templates (shared with Sim)
  - `templates/examples/` - Demo/tutorial templates
- Template format: YAML with {{parameter}} substitution
- Template versioning via git tags
- Future (M4.0+): Add Catalog_Nodes discovery mode

**Impact on Milestones:**
- M3.0: Define template schema format
- M3.2: Implement template parser and parameter substitution
- M3.2: Create 2-3 example templates (order-system, microservices)
- M3.3: Template validation and error messages

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
- No per-rule configuration in M3.3 (keep simple)
- Future (post-M3): Add configurable validation rules if needed

**Impact on Milestones:**
- M3.0: No validation yet (parser errors only)
- M3.1: Basic validation (conservation, gaps)
- M3.3: Full validation framework with mode-based severity
- M3.3: Clear error messages and warnings in API responses

---

### Q4: Inference Algorithm Location ✅

**Decision:** **Option D - Defer to M3.4+**

**Rationale:**
- Ship M3.0-M3.3 faster without inference complexity
- Focus on core time-travel capability (window, topology, APIs, telemetry)
- Inference is nice-to-have, not blocking
- Need user feedback to design algorithm correctly
- Can iterate in M3.4 without blocking initial delivery

**Implementation Notes:**
- M3.0-M3.3: No capacity inference
- API responses: `capacity: null` when unavailable
- UI handling: Show "capacity: unknown" or hide utilization bar
- UI optional: Show `served/arrivals` ratio as proxy (with clear label: "throughput ratio, not utilization")
- M3.4+: Implement in API layer (Option B from analysis)
- Future algorithm: Saturation method (~200 LOC in API layer)

**Clarification on Metrics:**
- `arrivals`: Work entering the node (from telemetry or queue)
- `served`: Work completed by the node
- `queue_depth`: Backlog (not same as arrivals)
- UI ratio: `served/arrivals` (what % of incoming work was completed)

**Impact on Milestones:**
- M3.0: Schema allows null capacity
- M3.1: /state API handles null capacity gracefully
- M3.2: TelemetryLoader doesn't require capacity
- M3.3: No inference implementation
- M3.4: Add API-layer inference (post-P0)

---

### Q5: /state Endpoint Priority ✅

**Decision:** **Option A - /state in M3.1 (Before TelemetryLoader), UI-First**

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
- M3.0: Create test fixtures (3-4 example systems with CSV telemetry)
- M3.0: Fixtures include: order-system, microservices, queue-heavy
- M3.1: Implement /state and /state_window using fixtures
- M3.1: UI integrates against fixture data
- M3.2: TelemetryLoader (may initially load from FlowTime-generated CSVs, not ADX)
- Future: Helper tool to generate synthetic gold telemetry from simulation runs

**Milestone Sequence:**
```
M3.0: Foundation + Fixtures
M3.1: Time-Travel APIs (/state, /state_window)
M3.2: TelemetryLoader + Templates
M3.3: Validation + Polish
```

**Impact on Milestones:**
- M3.0: Include fixture generation as deliverable
- M3.1: Test with fixtures, not real ADX
- M3.2: TelemetryLoader may load from files initially (ADX optional)
- Post-M3: Create synthetic gold telemetry generator tool

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
- M3.0: Define schema spec document
- M3.0: Create synthetic gold generator (FlowTime simulation → Gold CSV)
- M3.2: TelemetryLoader handles both Service Bus and App Insights sources
- M3.2: Loader gracefully handles missing external_demand (common for HTTP)
- Future: Add new optional columns as new buffer types discovered

**Impact on Milestones:**
- M3.0: Schema specification document + synthetic generator
- M3.2: TelemetryLoader with flexible column handling
- M3.3: Validation warns if external_demand missing (not error)

---

## Summary: All Decisions Complete ✅

**Milestones:** M3.0 through M3.3

**Key Decisions:**
1. ✅ Capacity: Optional (Q1)
2. ✅ Templates: Git-based in flowtime-vnext/templates/ (Q2)
3. ✅ Validation: Mode-based (warnings for telemetry, errors for simulation) (Q3)
4. ✅ Inference: Defer to M3.4+ (Q4)
5. ✅ /state Priority: M3.1 before TelemetryLoader, with fixtures in M3.0 (Q5)
6. ✅ Gold Schema: Simplified with flexible loader (Q6)

**Next Steps:**
1. Create detailed M3.0 specification
2. Generate synthetic gold telemetry fixtures
3. Begin implementation

---
