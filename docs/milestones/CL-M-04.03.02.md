# CL-M-04.03.02 — Scheduled Dispatch & Flow Control Primitives

**Status:** 📋 Proposed  
**Depends on:** CL‑M‑04.03.01 (router nodes & class routing), CL‑M‑04.03 (UI class visualization)  
**Target:** Allow templates to model bursty “bus stop” behavior and other cadence-driven flows by extending the expression/runtime toolbox (MOD/FLOOR/PULSE) and by introducing scheduled dispatch semantics for queues/routers.

---

## Overview

Recent router work revealed that many transportation/logistics scenarios require more than continuous PMF flows. Dispatchers often accumulate inventory between departures (e.g., buses or picker waves), toggle routing based on SLAs, or apply hysteresis before scaling. CL‑M‑04.03.02 formalizes these behaviors by:

1. Adding math/gating primitives to the expression engine (MOD/FLOOR/STEP/PULSE).
2. Extending backlog nodes with a `dispatchSchedule` so queues release inventory on defined cadences.
3. Updating analyzers, templates, and docs so router/queue nodes can explicitly represent scheduled dispatching.

This milestone is intentionally scoped to “scheduled dispatch,” but it lays groundwork for future behavior-oriented flows captured in `docs/architecture/expression-extensions-roadmap.md`.

---

## Requirements

### FR1 — Expression Primitives
- Implement `MOD`, `FLOOR`, `CEIL`, `ROUND`, `STEP`, and `PULSE(period, phase, amplitude)` in `FlowTime.Expressions` and surface them in schema docs.
- Update `ExpressionSemanticValidator` so these functions participate in dependency extraction and analyzer warnings.
- Add unit coverage in `FlowTime.Expressions.Tests` verifying AST parsing + evaluation.

### FR2 — Scheduled Dispatch Backlogs
- Schema (`docs/schemas/model.schema.yaml`) gains optional `dispatchSchedule` under backlog nodes:
  ```yaml
  dispatchSchedule:
    kind: time-based
    periodBins: 6        # required
    phaseOffset: 0       # optional
    capacitySeries: bus_capacity # optional override
  ```
- Engine (`ClassContributionBuilder` / backlog execution) releases backlog only on bins matching the schedule: served = min(backlog, capacity) when `(binIndex - phase) % period == 0`; served = 0 otherwise.
- Allow schedules to reference a `PULSE` helper instead when explicit config is absent.

### FR3 — Template & Docs
- Update `templates/transportation-basic-classes.yaml` so Airport/Downtown/Industrial dispatch queues use scheduled dispatch (bus-stop bursts). Document the change in README/template-authoring guides.
- Provide a second example (e.g., warehouse picker waves) demonstrating cadence-based release.
- Revise `docs/templates/template-authoring.md` with a “Scheduled Dispatch” section including best practices and analyzer expectations.

### FR4 — Analyzer & CLI Support
- Template analyzer flags invalid schedules (period <= 0, missing capacity series, etc.).
- Analyzer emits info-level notices when dispatch queues never release inventory (misaligned schedule).
- `flow-sim generate` should print schedule metadata for nodes (period, capacity, next departure) when verbose.
- Analyzer additionally validates that per-bin `served` never exceeds `arrivals` (per node and per class) so impossible SLAs are flagged.

### FR5 — API/UI Integration
- `/graph` and `/state_window` responses include `dispatchSchedule` metadata so the UI can annotate scheduled nodes (tooltip showing “Dispatch every 6 bins”).
- UI chips on topology highlight scheduled routers/queues (icon or label) without blocking other work.

### FR6 — Template Cache Refresh
- Add a “Clear Template Cache” capability so Sim/API services reload YAML without restarting:
  - Expose a CLI command (or API endpoint) that invalidates the template cache on disk and in-memory lookup tables.
  - Surface the action in the Run page (button or menu item) so operators can trigger a refresh before regenerating runs.
  - Update docs to explain when to use cache refresh vs. deterministic runs (`--overwrite`).

### Test Plan
- **Expressions:** new tests in `FlowTime.Expressions.Tests` for MOD/FLOOR/CEIL/ROUND/STEP/PULSE.
- **Engine:** add `ScheduledDispatchTests` in `FlowTime.Core.Tests` covering backlog release across multiple periods, zero-load handling, and capacity override.
- **Templates:** update golden runs (transportation, new warehouse example) verifying bursty arrivals and `classCoverage`.
- **Analyzer:** tests that misconfigured schedules raise warnings; valid schedules pass.
- **End-to-end:** rerun `dotnet test --nologo` plus sample `flow-sim generate` runs to capture analyzer output in the milestone tracker.

---

## Phases & Deliverables

1. **Phase 1 — Expression plumbing (RED→GREEN):**
   - Add failing expression tests, implement new functions, update schema docs.
2. **Phase 2 — Scheduled backlogs & analyzers:**
   - Extend backlog node execution, analyzer warnings, schema validation.
3. **Phase 3 — Template/UI integration:**
   - Refactor transportation template, add new example, update docs/UI metadata, regenerate runs/analyzers.

Each phase follows FlowTime guardrails (TDD order, tracker updates, full `dotnet build` + `dotnet test` before hand-off). Adoption guidance and analyzer output must be recorded in `docs/milestones/tracking/CL-M-04.03.02-tracking.md` when the milestone kicks off.
