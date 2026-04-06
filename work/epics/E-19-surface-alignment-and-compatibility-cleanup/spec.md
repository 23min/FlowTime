# Epic: Surface Alignment & Compatibility Cleanup

**ID:** E-19
**Status:** planning

## Goal

Tighten the remaining non-analytical legacy and compatibility surfaces after E-16 so FlowTime exposes current Engine/Sim contracts consistently across first-party UI, Sim, docs, schemas, and examples without carrying stale fallback layers or stripping supported Blazor capability.

## Context

E-16 purifies analytical truth and contract facts, but broader repo surfaces still carry compatibility debt that sits outside the formula-first boundary:

- first-party Blazor/UI clients still contain endpoint and metrics fallback paths
- parallel Svelte and Blazor surfaces need an explicit shared-contract discipline so one UI does not silently drift from the other
- active UI/template code still carries demo generation and schema-migration residue such as `binMinutes`-based templates
- Sim and UI surfaces still expose transitional endpoint or discovery helpers whose purpose is no longer clearly distinguished from true supported behavior
- docs, schemas, and examples still keep some deprecated material on current surfaces instead of moving it to archive/historical space

These are not analytical truth seams, so they should not be folded back into E-16. But they also should not remain unowned. If left alone, temporary compatibility layers become a de facto product support promise and the parallel UI surfaces will drift.

This epic creates an explicit post-purification cleanup lane: once E-16 finishes, FlowTime should tighten the rest of its first-party surfaces around one current contract and a small set of explicitly supported user paths. That includes keeping Blazor current as a supported debugging/plan-B surface, not retiring it.

## Scope

### In Scope

- Inventory of first-party compatibility and legacy seams outside E-16's analytical boundary
- Explicit supported-surface policy across Engine, Sim, Svelte UI, Blazor UI, docs, schemas, and examples
- Retirement of first-party UI and Sim runtime fallback paths that are no longer required once canonical endpoints and contracts exist
- Removal or archival of deprecated schema, demo, template, and example material from active current surfaces
- Blazor UI alignment work so `FlowTime.UI` stays current with evolving Engine/Sim contracts without carrying stale compatibility wrappers
- Tests, grep audits, and documentation updates that prevent reintroduction of deleted compatibility layers

### Out of Scope

- Analytical truth, emitted-series logic, by-class purity, warning fact ownership, or consumer fact publication already owned by E-16
- New analytical primitives from E-10 Phase 3 (`p3d`, `p3c`, `p3b`)
- Delivering missing Svelte UI product features themselves; E-11 still owns additive Svelte buildout
- Blazor UI retirement or functionality removal as a cleanup goal
- Additive backward-compatibility phases for deprecated surfaces
- Generic low-level library fallbacks with no product-contract meaning (for example layout placement fallbacks) unless they are later promoted into a first-party compatibility promise

## Constraints

- Starts after E-16 establishes purified current contracts; runtime/API cleanup milestones should consume those contracts rather than redefine them
- Does not block E-10 Phase 3 resume by default; this runs as a parallel post-purification cleanup lane unless a specific milestone intentionally touches a shared contract gate
- Svelte and Blazor run in parallel; shared client and contract changes must keep both UIs aligned rather than privileging only one surface
- Forward-only: archive or delete deprecated surfaces instead of carrying new compatibility shims
- Remove stale wrappers and fallback logic without deleting supported Blazor debugging/operator workflows unless a separate decision explicitly approves it
- Each milestone must make the supported-vs-historical boundary narrower, not broader

## Success Criteria

- [ ] One explicit supported compatibility matrix exists for Engine, Sim, Svelte UI, Blazor UI, docs, schemas, and examples
- [ ] First-party clients no longer maintain duplicate endpoint, metrics, or health fallback logic where the canonical contract exists
- [ ] Active UI/template surfaces no longer generate or promote deprecated schema shapes such as `binMinutes`-based demo templates
- [ ] Legacy examples, docs, and schema references are either archived/historical or deleted; current docs present one canonical surface
- [ ] Blazor UI support policy is explicit: it remains a supported debugging/plan-B surface and consumes current Engine/Sim contracts without stale compatibility wrappers
- [ ] Grep and regression audits prove targeted legacy/fallback helpers are removed or isolated to historical/archive surfaces only

## Risks & Open Questions

| Risk / Question | Impact | Mitigation |
|----------------|--------|------------|
| Which operational fallbacks are true compatibility seams vs useful local-dev resilience? | Medium | Milestone 1 creates the supported-surface inventory and keeps only explicitly justified operational resilience |
| Parallel Svelte and Blazor work may drift if sync ownership stays implicit | High | Keep milestone 4 about Blazor support alignment, plus shared contract audits, rather than retirement |
| Some docs/examples are still useful as migration references | Low | Move them to archive/historical locations instead of keeping them on current surfaces |
| Consumers outside first-party UI may still read deprecated endpoints/fields | Medium | Make the supported-surface policy explicit before deletion milestones start |

## Sequencing

This epic starts immediately after E-16 as a post-purification cleanup lane. It should run in parallel with resumed E-10 Phase 3 work and final E-11 parity work, not silently replace them.

- E-16 removes analytical truth and consumer-fact reconstruction seams.
- E-19 removes the remaining non-analytical legacy, fallback, and current-surface debt around those purified contracts.
- E-11 continues as a parallel UI track; E-19 keeps both first-party UIs aligned to the same current Engine/Sim contracts.

## Milestones

| ID | Title | Summary | Depends On | Status |
|----|-------|---------|------------|--------|
| m-E19-01-supported-surface-inventory | Supported Surface Inventory & Exit Criteria | Inventory compatibility seams, define supported vs historical surfaces, and pin deletion/archival gates. | E-16 | draft |
| m-E19-02-runtime-endpoint-cleanup | Runtime Endpoint & Client Cleanup | Remove first-party health, metrics, and endpoint fallbacks once canonical Engine/Sim contracts are authoritative. | m-E19-01 | draft |
| m-E19-03-schema-template-example-retirement | Schema, Template & Example Retirement | Remove or archive deprecated schema, demo template, and example material from active current surfaces. | m-E19-01 | draft |
| m-E19-04-blazor-support-alignment | Blazor Support Alignment | Remove stale `FlowTime.UI` compatibility wrappers, keep Blazor aligned with current Engine/Sim contracts, and define clear supported debugging/operator workflows alongside the parallel Svelte UI. | m-E19-02, m-E19-03 | draft |

## Candidate Deletion / Archival Targets

- `FlowTime.UI` client fallbacks and legacy endpoint probes that duplicate canonical Engine/Sim contracts
- stale metrics/state reconstruction in first-party Blazor consumers that survive E-16
- demo/template generation paths that still emit deprecated schema fields on active surfaces
- active docs/examples that present deprecated schema shapes as current guidance
- Sim service compatibility shims whose only purpose is preserving transitional first-party clients that have already been replaced

## References

- `work/epics/E-16-formula-first-core-purification/spec.md`
- `work/epics/E-11-svelte-ui/spec.md`
- `ROADMAP.md`
- `work/gaps.md`
