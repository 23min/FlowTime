# FT-M-05.05 — Router Flow Solidification

## Goal

Eliminate the legacy percentage split expressions in class-enabled templates so routers become the authoritative source of downstream inflows. This keeps class coverage aligned, removes `router_class_leakage` warnings, and simplifies future template authoring.

## Motivation

- `templates/transportation-basic-classes.yaml` still feeds `hub_dispatch_airport`, `hub_dispatch_downtown`, etc. via constant percentages even though `HubDispatchRouter` exists. When the actual class mix in the hub queue drifts, the analyzer emits `router_class_leakage`.
- We want routers to supply per-class flow to downstream nodes automatically; templates shouldn’t need duplicate expressions.
- Fixing this now keeps the CL‑M‑04.x demos clean before we dive into FT-M-05.05 perf work.

## Phase 1 — Router Output Plumbing

1. **Sim/Core support**:
   - Extend `TemplateRouterRoute` handling so each route can emit a concrete series (e.g., `HubDispatchRouter→AirportDispatchQueue`) rather than requiring manual expressions.
   - Allow router targets to reference either a node ID (e.g., `AirportDispatchQueue`) or an intermediate “demand” node; the engine should write per-class CSVs for each routed leg.
2. **Analyzer updates**:
   - Teach `router_class_leakage` to expect downstream nodes to consume those router-generated series. Update tests to ensure conservation holds once expressions are removed.

## Phase 2 — Template Retrofits

1. Update `templates/transportation-basic-classes.yaml`:
   - Remove `hub_dispatch_airport`, `hub_dispatch_industrial`, `hub_dispatch_downtown` expressions.
   - Point dispatch queue arrivals directly at the router outputs.
   - Adjust any downstream expressions relying on the old nodes.
2. Run and capture new canonical runs for transportation (CLI + analyzer harness) to confirm warnings disappear.
3. Audit other class-enabled templates (`supply-chain-multi-tier-classes`, etc.) to ensure they either don’t use routers or already consume router outputs.

## Phase 3 — Tooling & Docs

1. Add regression tests (Sim + analyzer) verifying router targets produce per-class series and queues consume them.
2. Update `docs/templates/template-authoring.md` and `examples/class-enabled.yaml` to demonstrate the new router pattern (no manual `%` expressions).
3. Document the migration steps in `docs/releases/FT-M-05.05.md` so template authors know to remove legacy splits.

## Definition of Done

- `flow-sim generate --id transportation-basic-classes` produces a run with no `router_class_leakage` warning.
- Router outputs materialize per-route series automatically and are consumed by downstream nodes—verified via unit tests/analyzers.
- Docs/samples reflect the new router usage pattern.
