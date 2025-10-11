# FlowTime.Sim Time-Travel Architecture Package

This folder captures the authoritative time-travel architecture for the FlowTime.Sim surface. It complements the existing Engine-focused chapters under `docs/architecture/time-travel/` and translates their KISS principles into concrete expectations for template authoring, services, validation, and testing on the simulation side.

## Document Map

- `sim-architecture-overview.md` — high-level goals, guiding principles, and system context for FlowTime.Sim within the time-travel platform.
- `sim-schema-and-validation.md` — required schema extensions (window, topology, semantics, provenance), expression validation strategy, and how FlowTime.Sim must align with Engine contracts.
- `sim-implementation-plan.md` — actionable workstreams, milestone alignment, and dependencies (including the shared expression library follow-up).
- `sim-decision-log.md` — chronological record of architecture decisions, including items carried over from the readiness audit.

Use these together with the Engine chapters to reason about cross-surface impacts. The roadmap in `time-travel-planning-roadmap.md` remains the single source of truth for milestone sequencing; this package provides the FlowTime.Sim design depth needed to execute those milestones.
