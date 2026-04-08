# Epic: Surface Alignment & Compatibility Cleanup

**ID:** E-19
**Status:** active — m-E19-01, m-E19-02, and m-E19-03 completed, m-E19-04 next

## Goal

Tighten the remaining non-analytical legacy and compatibility surfaces after E-16 so FlowTime exposes current Engine/Sim contracts consistently across first-party UI, Sim, docs, schemas, and examples without carrying stale fallback layers or stripping supported Blazor capability.

## Context

E-16 purifies analytical truth and contract facts, but broader repo surfaces still carry compatibility debt that sits outside the formula-first boundary:

- first-party Blazor/UI clients still contain endpoint and metrics fallback paths
- parallel Svelte and Blazor surfaces need an explicit shared-contract discipline so one UI does not silently drift from the other
- active UI/template code still carries demo generation and schema-migration residue such as `binMinutes`-based templates
- Sim and UI surfaces still expose transitional endpoint or discovery helpers whose purpose is no longer clearly distinguished from true supported behavior
- Sim still publicly owns template-driven run creation while Engine `/runs` now imports canonical bundles and reads canonical run artifacts
- storage-backed drafts and archived run bundles are active first-party surfaces, but their supported status versus transitional status is not explicit
- catalog-era runtime seeding, endpoints, and UI clients still exist even where active callers say catalogs are no longer used
- template vs draft vs model vs run vs bundle terminology is ambiguous enough that the current Sim orchestration path can be mistaken for the future Time Machine contract
- docs, schemas, and examples still keep some deprecated material on current surfaces instead of moving it to archive/historical space

These are not analytical truth seams, so they should not be folded back into E-16. But they also should not remain unowned. If left alone, temporary compatibility layers become a de facto product support promise and the parallel UI surfaces will drift.

This epic creates an explicit post-purification cleanup lane: once E-16 finishes, FlowTime should tighten the rest of its first-party surfaces around one current contract and a small set of explicitly supported user paths. That includes keeping Blazor current as a supported debugging/plan-B surface, not retiring it.

## Scope

### In Scope

- Inventory of first-party compatibility and legacy seams outside E-16's analytical boundary
- Explicit supported-surface policy across Engine, Sim, Svelte UI, Blazor UI, docs, schemas, and examples
- Explicit boundary between current Sim authoring/orchestration surfaces and the future E-18 Time Machine foundation
- Retirement of first-party UI and Sim runtime fallback paths that are no longer required once canonical endpoints and contracts exist
- Retention/deletion decisions for storage-backed drafts, archived run bundles, bundle import paths, runtime catalogs, and catalog-era caller residue
- Removal or archival of deprecated schema, demo, template, and example material from active current surfaces
- Blazor UI alignment work so `FlowTime.UI` stays current with evolving Engine/Sim contracts without carrying stale compatibility wrappers
- Tests, grep audits, and documentation updates that prevent reintroduction of deleted compatibility layers

### Out of Scope

- Analytical truth, emitted-series logic, by-class purity, warning fact ownership, or consumer fact publication already owned by E-16
- New analytical primitives from E-10 Phase 3 (`p3d`, `p3c`, `p3b`)
- Delivering missing Svelte UI product features themselves; E-11 still owns additive Svelte buildout
- Blazor UI retirement or functionality removal as a cleanup goal
- The new Time Machine runtime parameter foundation, reevaluation API, evaluation SDK, CLI/sidecar, or optimization/fitting modes owned by E-18
- Chunked/stateful execution semantics and streaming/stateful seams owned by later E-18 layers or dedicated streaming work
- Additive backward-compatibility phases for deprecated surfaces
- Generic low-level library fallbacks with no product-contract meaning (for example layout placement fallbacks) unless they are later promoted into a first-party compatibility promise

## Constraints

- Starts after E-16 establishes purified current contracts; runtime/API cleanup milestones should consume those contracts rather than redefine them
- Does not block E-10 Phase 3 resume by default; this runs as a parallel post-purification cleanup lane unless a specific milestone intentionally touches a shared contract gate
- Svelte and Blazor run in parallel; shared client and contract changes must keep both UIs aligned rather than privileging only one surface
- Forward-only: archive or delete deprecated surfaces instead of carrying new compatibility shims
- Remove stale wrappers and fallback logic without deleting supported Blazor debugging/operator workflows unless a separate decision explicitly approves it
- Each milestone must make the supported-vs-historical boundary narrower, not broader

## Boundary To E-18

E-19 and E-18 are adjacent but they do different work:

- **E-19** narrows or deletes the current first-party residue around Sim authoring/orchestration, storage-backed drafts, archived bundle refs, runtime catalogs, and ambiguous caller paths. It decides what remains supported on today's surfaces.
- **E-18** builds the replacement Time Machine foundation: runtime parameter identity, deterministic overrides, reevaluation APIs, evaluation SDK, and CLI/sidecar over compiled graphs.

The current Sim orchestration path is therefore not the default path forward for Time Machine evaluation. If an E-19 deletion depends on new capability, that dependency should be made explicit against E-18 rather than preserving the current residue indefinitely.

## Current Findings To Use

| Surface / Residue | Current Role | Risk If Left Unowned | E-19 Intent |
|------------------|--------------|----------------------|-------------|
| Sim `/api/v1/orchestration/runs` and `/api/v1/drafts/run` | Current first-party template-driven run creation | Transitional Sim execution APIs harden into a support obligation before the Time Machine replacement ships | Inventory, narrow, retain only if explicitly supported as an authoring surface |
| `data/storage/drafts` and draft CRUD endpoints | Saved editable YAML working copies | Hidden product surface with unclear support and lifecycle | Decide whether draft authoring is a supported Sim feature or transitional residue |
| `data/storage/runs` and `bundleRef`-based archive flow | Portable archived run bundles | Duplicates canonical runs and makes the archive path look like the primary run contract | Decide whether bundle refs stay as an interchange/import surface or are retired |
| Engine `/runs` import path via bundle path/archive/ref | Canonical bundle import, not template orchestration | Confused with Time Machine run creation | Keep only if import is explicitly supported; document it as import, not evaluation ownership |
| Runtime catalogs, catalog endpoints, and UI catalog clients | Residue from earlier Sim/catalog direction | Zombie compatibility surface that invites new callers | Retire from active first-party paths unless an explicit support case survives inventory |
| Template/draft/model/run/bundle terminology drift | Ambiguous product language | Wrong architecture hardens by naming accident | Publish one boundary ADR and use it as the inventory baseline |

## Success Criteria

- [x] One explicit supported compatibility matrix exists for Engine, Sim, Svelte UI, Blazor UI, docs, schemas, and examples
- [x] One explicit terminology and ownership matrix exists for template, draft, model, run, bundle, and catalog surfaces
- [ ] First-party clients no longer maintain duplicate endpoint, metrics, or health fallback logic where the canonical contract exists
- [x] Current Sim orchestration/storage/catalog residue is either explicitly supported with scope boundaries or removed from active first-party paths
- [ ] Active UI/template surfaces no longer generate or promote deprecated schema shapes such as `binMinutes`-based demo templates
- [ ] Legacy examples, docs, and schema references are either archived/historical or deleted; current docs present one canonical surface
- [x] Blazor UI support policy is explicit: it remains a supported debugging/plan-B surface and consumes current Engine/Sim contracts without stale compatibility wrappers
- [x] E-18 planning remains clean: no current Sim draft/catalog/bundle choreography is treated as the default programmable/Time Machine contract
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
| m-E19-01-supported-surface-inventory | Supported Surface Inventory, Boundary ADR & Exit Criteria | Inventory compatibility seams, define supported vs historical surfaces, publish the terminology/ownership ADR, and pin retention/deletion gates for drafts, bundles, catalogs, and import paths. | E-16 | completed |
| m-E19-02-sim-authoring-and-runtime-boundary-cleanup | Sim Authoring & Runtime Boundary Cleanup | Narrow Sim to explicitly supported authoring/orchestration surfaces, remove transitional catalog/runtime callers, and keep Engine import/query ownership explicit. | m-E19-01 | completed |
| m-E19-03-schema-template-example-retirement | Schema, Template & Example Retirement | Remove or archive deprecated schema, demo template, and example material from active current surfaces. | m-E19-01 | completed |
| m-E19-04-blazor-support-alignment | Blazor Support Alignment | Remove stale `FlowTime.UI` compatibility wrappers, keep Blazor aligned with current Engine/Sim contracts, and define clear supported debugging/operator workflows alongside the parallel Svelte UI. | m-E19-02, m-E19-03 | next |

## Candidate Retention / Decision Matrix

### Retain Only If Explicitly Supported

- Repo-backed templates and template metadata served by Sim as an authoring surface
- Engine `/v1/runs/*` query/read endpoints over canonical run artifacts
- Blazor and Svelte as parallel first-party UIs consuming current contracts
- Storage-backed drafts only if saved draft authoring remains a supported user workflow

### Decide In `m-E19-01`

- Sim `/api/v1/orchestration/runs` as a supported authoring convenience vs transitional surface
- Sim `/api/v1/drafts/*` lifecycle and whether storage-backed drafts remain a product promise
- `data/storage/runs` and `bundleRef` as an explicit interchange/import surface vs transitional archive residue
- Engine bundle import endpoints as supported import workflows vs temporary migration surfaces
- Runtime catalog seeding, catalog endpoints, and catalog-aware UI clients

### Delete / Archive Candidates

- `FlowTime.UI` client fallbacks and legacy endpoint probes that duplicate canonical Engine/Sim contracts
- stale metrics/state reconstruction in first-party Blazor consumers that survive E-16
- demo/template generation paths that still emit deprecated schema fields on active surfaces
- active catalog selection, mock catalog services, and placeholder `CatalogId = "default"` callers that are no longer part of the supported path
- active docs/examples that present deprecated schema shapes as current guidance
- Sim service compatibility shims whose only purpose is preserving transitional first-party clients that have already been replaced
- docs or examples that imply the current Sim orchestration/storage choreography is the future programmable/Time Machine contract

## References

- `work/epics/E-16-formula-first-core-purification/spec.md`
- `work/epics/E-11-svelte-ui/spec.md`
- `work/epics/E-18-headless-pipeline-and-optimization/spec.md`
- `docs/architecture/template-draft-model-run-bundle-boundary.md`
- `ROADMAP.md`
- `work/gaps.md`
