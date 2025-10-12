# FlowTime.Sim Schema & Validation Requirements

**Status:** Draft (reflects post-audit plan)  
**Last Updated:** 2025-10-11

This chapter specifies the KISS-aligned schema and validation rules FlowTime.Sim must implement to participate in time-travel milestones. It replaces the legacy SIM-M-02.06 schema assumptions and serves as the contract for template authors, CLI/service maintainers, and test writers.

---

## 1. Canonical Schema (Model YAML)

```yaml
schemaVersion: 1
generator: "flowtime-sim"          # Always embedded provenance
metadata:
  id: "order-system"
  title: "Order System (Time-Travel)"
  version: "1.1.0"
window:
  start: "2025-10-07T00:00:00Z"    # ISO 8601 UTC
  timezone: "UTC"
grid:
  bins: 288
  binSize: 5
  binUnit: "minutes"
topology:
  nodes:
    - id: "OrderService"
      kind: "service"              # service|queue|router|external
      group: "Orders"              # optional UI grouping
      ui:                          # optional visual hints (see DP-007)
        x: 120
        y: 260
      semantics:
        arrivals: "orders_arrivals"
        served: "orders_served"
        errors: "orders_errors"
        queue: null
        capacity: null
        external_demand: null
      initialCondition:
        queueDepth: null           # required for self-SHIFT queues
  edges:
    - id: "e1"
      from: "OrderService:out"
      to: "FulfillmentQueue:in"
nodes:
  - id: "orders_arrivals"
    kind: "const"
    source: "file://telemetry/OrderService_arrivals.csv"
  - id: "orders_served"
    kind: "expr"
    expr: "MIN(orders_arrivals, orders_capacity)"
    initial: null
outputs:
  - series: "*"
    exclude: ["temp_*"]
provenance:
  source: "flowtime-sim"
  generator: "flowtime-sim/1.0.0"
  generatedAt: "2025-10-07T14:30:00Z"
  templateId: "order-system"
  parameters:
    binSize: 5
    q0: 0
```

### 1.1 Field Requirements

| Section | Requirements | Notes |
|---------|--------------|-------|
| `schemaVersion` | Always `1` until Engine bumps. | FlowTime.Sim must track Engine agreement. |
| `metadata` | Must include `id`, human-readable `title`, and semantic version `version`. | `version` is mirrored in provenance (`templateVersion`). |
| `window` | Required. `start` in UTC ISO 8601, timezone fixed to `UTC`. | Drives `/state` timestamps. |
| `topology.nodes` | Required. Each node must define `kind`, `semantics`, and optional `ui`. | Semantics map to `nodes[*].id`. |
| `topology.edges` | Required when more than one node exists. | DAG validation applies. |
| `nodes` | Include `source` for telemetry-backed const nodes. Inline `values` allowed for synthetic defaults. | `initial` required for self-referential SHIFT. |
| `outputs` | Default to `*` with optional excludes. | Ensures artifacts align with Engine expectations. |
| `provenance` | Always embedded. `generator` string must begin with `flowtime-sim`; include `templateVersion` from `TemplateMetadata`. | Manual/human-generated paths can extend later. |

---

## 2. Template Representation (`Template` Classes)

The internal template model must surface the same information, not strip metadata before conversion.

Required changes:

- **`TemplateWindow`, `TemplateTopology`, `TopologyNode`, `TopologyEdge`, `NodeSemantics`, `InitialCondition`, `UIHint`** classes.
- **`TemplateNode` enhancements:** new `Source`, `Initial`, `SemanticRole` (if needed), `Kind`.
- **`TemplateOutput`** should distinguish between wildcard exports and explicit enumerations.
- **`TemplateMetadata`** preserved into generated models (no stripping) and extended with a required `Version` property (semantic version string) that surfaces in provenance.
- Introduce `TemplateMode` enumeration (`simulation`, `telemetry`) used for validation severity.

---

## 3. Validation Responsibilities

### 3.1 Shared Expression Library

- Consume the shared `FlowTime.Expressions` assembly (post Engine M-03.00 follow-up).
- FlowTime.Sim projects (service, CLI, tests) reference `FlowTime.Expressions`; a smoke test (`tests/FlowTime.Sim.Tests/Expressions/ExpressionLibrarySmokeTests.cs`) ensures the parser is wired until full adoption lands in SIM-M-03.
- Validate expressions during template parsing:
  - Unknown identifiers/functions → error.
  - SHIFT self-reference without `initial` → error.
  - Division by zero, negative SHIFT lag, etc. (as provided by shared rules).
- Ensure `dependencies` list (if retained) matches parsed references.

### 3.2 Topology & Semantics

| Check | Simulation Mode | Telemetry Mode |
|-------|-----------------|----------------|
| Node ID uniqueness | Error | Error |
| Kind-specific semantics (e.g., queue needs `queue`) | Error | Error |
| Missing semantics mapping | Error | Error |
| Edge references unknown node | Error | Error |
| Cycles in topology | Error | Error |
| Missing capacity semantics | Warning | Warning |
| Missing UI hints | Warning | Warning |

FlowTime.Sim should not attempt to infer layout or capacity; it only verifies structure.

### 3.3 Mode Handling

- `mode=simulation` (default) → strict validation, failures stop generation.
- `mode=telemetry` (future) → degrade certain cases to warnings (e.g., missing telemetry `source`), but still run shared expression and topology checks.
  - Mode value should be part of provenance for observability.

---

## 4. Provenance Rules

1. Always embed `provenance` after `schemaVersion`.
2. `generator` must follow `flowtime-sim/<semver>`.
3. Include `mode`, `templateVersion`, and hash-based `modelId`.
4. Continue writing JSON sidecar (`provenance.json`) for compatibility, but treat embedded YAML as source of truth.

---

## 5. Testing Expectations

| Test Type | Coverage |
|-----------|----------|
| Unit tests | Template serialization/deserialization, expression validation errors, topology validation. |
| Integration tests | Generate model → run through Engine fixtures → verify `/state` responses align with expectations. |
| Property tests (optional) | Randomized expressions/topologies to ensure validator catches invalid combinations. |

Fixtures in `templates/` must be upgraded to schema v1.1 and accompanied by matching `.http` or CLI scripts demonstrating generation.

---

## 6. Migration Strategy

1. **Introduce new template classes alongside legacy ones** to keep compilation green, then migrate nodes/services.
2. **Update NodeBasedTemplateService** to stop stripping metadata and to write the full schema.
3. **Deprecate legacy templates** by emitting warnings when schema v1.0 artifacts are generated; remove support in a later milestone.
4. **Document conversion guides** for teams with existing simulation templates (CLI command or script to upgrade YAML).

---

## 7. References

- `docs/architecture/time-travel/time-travel-architecture-ch2-data-contracts.md`
- `docs/architecture/time-travel/time-travel-architecture-ch3-components.md`
- `docs/architecture/time-travel/sim/sim-architecture-overview.md`
- FlowTime.Sim readiness audit (`sim-time-travel-readiness-audit.md`)
