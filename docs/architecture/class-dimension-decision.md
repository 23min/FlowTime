# Class Dimension Decision (Single Class per Entity)

## Summary

FlowTime models **one class per entity**. A class is the primary flow type for routing, SLAs, and per-class metrics. This document explains the decision, the alternatives considered, and how to model systems that need multiple attributes.

**Related docs:**
- [Classes as Flows Architecture](classes/README.md)
- [Engine Capabilities](../reference/engine-capabilities.md)
- [Subsystems & Filters](subsystems/README.md)

---

## Decision

Each entity in the system carries a **single `classId`** at a time. Class is the first-class dimension used for:
- Traffic definitions (`classId` on arrivals)
- Routing (`routes[].classes`)
- Per-class metrics (`byClass` series)
- Per-class SLAs and analysis

This keeps FlowTime’s core guarantees simple and deterministic while preserving the ability to model multiple flows through a shared topology.

---

## Why a Single Class

A single class dimension keeps these properties tractable and reliable:

- **Determinism and conservation**: node- and class-level totals reconcile cleanly.
- **Series footprint**: metrics scale linearly with classes, not combinatorially.
- **Routing semantics**: routers and analyzers use a single class key without complex predicate logic.
- **UI and API simplicity**: class filters are single-dimension and can be displayed/compared directly.

This aligns with FlowTime’s focus on **gold telemetry** and reproducible aggregated runs.

---

## Alternatives Considered

### 1) Multi-dimensional classes (e.g., service level + courier mode)

**Pros**
- Expressive modeling when multiple attributes drive routing or SLA.
- Avoids lossy projections when attributes are truly orthogonal.

**Cons**
- Requires composite keys or structured class vectors in all schemas.
- Multiplies metrics and storage (cross-product of dimensions).
- Complicates routing rules, analyzers, and UI filtering.
- Increases the risk of partial coverage and inconsistent data.

**Status**: Not supported today. Would require a dedicated epic.

### 2) Composite classes (encode multiple attributes in one classId)

Example: `priority_bike`, `standard_car`.

**Pros**
- Works with current schema and tooling.
- Allows attribute-driven routing and SLAs.

**Cons**
- Can explode as dimensions/values grow.
- Harder to filter or compare a single attribute across classes.

**Status**: Supported as a modeling strategy when the cross-product is small.

### 3) Labels / tags

Labels describe **context** (customer, region, environment, channel). They are **not** a routing dimension. Labels are intended for segmentation and filtering when telemetry supports it.

**Pros**
- Keep topology and routing stable.
- Avoid class explosion.

**Cons**
- Labels do **not** affect routing or SLAs.
- Labels are not yet a first-class runtime or schema feature.

**Status**: Conceptual and planned; not enforced by current templates or run schemas.

---

## Modeling Guidance

Use **classes** for attributes that **change system behavior**:
- Routing paths
- Service policies
- SLAs and retries

Use **labels** for attributes that **do not change behavior**, only reporting:
- Region, environment, customer tier, channel

If you need two behavior-driving attributes, choose:

- **Composite classes** if the attribute set is small and stable.
- **Multi-dimensional classes** only if you are willing to expand the platform (future epic).

---

## Example (Food Delivery)

If service level and courier mode both change routing and SLA:

- **Composite classes**: `priority_bike`, `priority_car`, `standard_bike`, `standard_car`
- **Labels**: `region=eu`, `weather=rain`

This keeps routing logic on classes while preserving context on labels.

---

## Implications

- FlowTime supports **multi-class flows**, but **single class per entity**.
- UI class filters are single-dimension and remain stable.
- Adding multi-dimensional classes would require schema changes, analyzer changes, telemetry changes, and UI changes.
