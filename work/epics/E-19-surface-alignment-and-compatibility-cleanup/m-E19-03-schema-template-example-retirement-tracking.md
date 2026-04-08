# Tracking: m-E19-03 Schema, Template & Example Retirement

**Status:** in-progress (started 2026-04-08)
**Epic:** [E-19 Surface Alignment & Compatibility Cleanup](./spec.md)
**Milestone spec:** [m-E19-03-schema-template-example-retirement.md](./m-E19-03-schema-template-example-retirement.md)
**Branch:** `milestone/m-E19-03-schema-template-example-retirement` (off `epic/E-19`)

## Acceptance Criteria

- [ ] AC1. UI demo template generators in `src/FlowTime.UI/Services/TemplateServiceImplementations.cs` emit current `binSize`/`binUnit` schema in lines 431, 475, 1356, 1462, 1536; migration warning comment removed.
- [ ] AC2. `src/FlowTime.UI/wwwroot/sample/run-example.json` grid shape rewritten to `{ "bins": 8, "binSize": 1, "binUnit": "hours" }`.
- [ ] AC3. `src/FlowTime.Cli/Program.cs:98` verbose label rewritten to `binSize={grid.BinSize}, binUnit={grid.BinUnit...}`.
- [ ] AC4. YAML examples in `docs/architecture/whitepaper.md:250` and `docs/architecture/retry-modeling.md` (lines 417, 466, 527) rewritten to current schema; Little's Law formula on `whitepaper.md:77` preserved with `<!-- m-E19-03:allow-binminutes-notation -->` marker appended.
- [ ] AC5. `examples/test-old-schema.yaml`, `test-no-schema.yaml`, `test-new-schema.yaml` moved to `examples/archive/` via `git mv`; empty `examples/time-travel/` directory deleted; `examples/archive/README.md` created/updated; inbound references updated.
- [ ] AC6. `docs/ui/template-integration-spec.md` moved to `docs/archive/ui/template-integration-spec.md` via `git mv`; inbound references updated.
- [ ] AC7. Catalog-stale phrasing rewritten in `docs/guides/UI.md:3`, `docs/reference/contracts.md:111`, `docs/reference/engine-capabilities.md:30`.
- [ ] AC8. `["binMinutes"]` dictionary-key literals in `tests/FlowTime.UI.Tests/ParameterConversionIntegrationTests.cs` (lines 23, 51, 107) renamed to `["binSize"]`; test class still passes without other edits.
- [ ] AC9. `scripts/m-E19-03-grep-guards.sh` created, 11 guards implemented, all passing.
- [ ] AC10. Status surfaces reconciled at wrap (spec, epic spec, ROADMAP.md, epic-roadmap.md, CLAUDE.md, this tracking doc); final test count and grep-guard results recorded.

## Commit Plan (Bundles)

Per milestone spec Technical Notes — five focused commits plus the wrap.

- [ ] **Bundle A** (AC1 + AC2 + AC3 + AC8): deprecated `binMinutes` authoring shape in code — four code/fixture rewrites in one conceptual cleanup
- [ ] **Bundle B** (AC4 + AC7): active docs cleanup — rewrites `binMinutes` YAML examples and catalog-stale phrasing
- [ ] **Bundle C** (AC5 + AC6): archive moves — three schema-migration example YAMLs, empty `time-travel/` dir, stale UI spec
- [ ] **AC9**: grep-guard script as its own commit
- [ ] **AC10**: wrap — tracking doc finalization and status-surface reconciliation

Initial **status-sync commit** (flip m-E19-03 draft→in-progress across status surfaces, create milestone spec and this tracking doc) runs before Bundle A.

## Grep Guards

Each must return zero matches after the milestone completes. Full script: `scripts/m-E19-03-grep-guards.sh`.

- [ ] Guard 1: No `binMinutes` in `src/FlowTime.UI/Services/TemplateServiceImplementations.cs`
- [ ] Guard 2: No `binMinutes` in `src/FlowTime.UI/wwwroot/`
- [ ] Guard 3: No `binMinutes` in `src/FlowTime.Cli/`
- [ ] Guard 4: No `binMinutes` in `docs/architecture/whitepaper.md` except lines containing `m-E19-03:allow-binminutes-notation`
- [ ] Guard 5: No `binMinutes` in `docs/architecture/retry-modeling.md`
- [ ] Guard 6: No `examples/test-old-schema.yaml`, `test-no-schema.yaml`, or `test-new-schema.yaml` reference outside `examples/archive/` and `docs/archive/`
- [ ] Guard 7: No active reference (outside `docs/archive/`) to `docs/ui/template-integration-spec.md`
- [ ] Guard 8: No active reference to pre-v1 routes `/api/templates/{id}/schema` or `/api/templates/generate` outside `docs/archive/` and release notes
- [ ] Guard 9: No `template/catalog` literal in `docs/guides/UI.md` or `docs/reference/contracts.md`
- [ ] Guard 10: No `catalog/export/import/registry` literal in `docs/reference/engine-capabilities.md`
- [ ] Guard 11: No `["binMinutes"]` dictionary-key literal in `tests/FlowTime.UI.Tests/ParameterConversionIntegrationTests.cs`

## Preserved Surfaces (Must Not Regress)

See milestone spec § Preserved Surfaces for the full list. Key retained `binMinutes` owners that must stay untouched:

- `src/FlowTime.Core/Models/TimeGrid.cs` — `BinMinutes` computed property
- `src/FlowTime.Core/Models/ModelValidator.cs` — `binMinutes` rejection gate
- `src/FlowTime.Contracts/TimeTravel/MetricsContracts.cs` — `MetricsGrid.BinMinutes` wire-format field
- `src/FlowTime.Core/Metrics/RuntimeAnalyticalEvaluator.cs` — internal `binMinutes` method parameters
- `src/FlowTime.API/Services/{MetricsService,StateQueryService,AggregatesCsvExporter,NdjsonExporter,ParquetExporter}.cs` — internal `BinMinutes` options
- `src/FlowTime.UI/Services/{FlowTimeApiModels,FlowTimeSimApiClient,TimeTravelMetricsClient,TimeTravelApiModels,SimResultsService}.cs` — computed display helpers and internal variables
- `src/FlowTime.UI/Pages/{Simulate,TimeTravel/Topology}.razor` — consumers of the computed display helpers
- `docs/schemas/model.schema.md`, `docs/schemas/model.schema.yaml` — authoritative migration docs
- `docs/architecture/whitepaper.md:77` — Little's Law math notation (allowlisted via comment marker)
- `docs/architecture/reviews/*` — historical review snapshots
- `tests/FlowTime.Tests/Schema/TargetSchemaValidationTests.cs`, `tests/FlowTime.UI.Tests/GridInfoSchemaTests.cs`, `SimGridInfoSchemaTests.cs`, `GraphRunResultSchemaTests.cs`, `TemplateServiceMetadataTests.cs`, `tests/FlowTime.Core.Tests/Safety/NaNPolicyTests.cs`, `tests/FlowTime.Tests/ApiIntegrationTests.cs`, `tests/FlowTime.Api.Tests/Legacy/ApiIntegrationTests.cs`, `tests/FlowTime.Api.Tests/StateEndpointTests.cs`, `tests/FlowTime.Api.Tests/StateResponseSchemaTests.cs`, `tests/FlowTime.Api.Tests/Golden/metrics-run_metrics_fixture.json` — all exercising retained contracts

## Implementation Log

Per-bundle notes, file lists, and grep-guard results will be appended as each bundle lands.

### Status Sync (pre-Bundle A)

Pending commit approval. Changes:

- `work/epics/E-19-surface-alignment-and-compatibility-cleanup/m-E19-03-schema-template-example-retirement.md` — new spec file, status `in-progress`
- `work/epics/E-19-surface-alignment-and-compatibility-cleanup/m-E19-03-schema-template-example-retirement-tracking.md` — new tracking doc (this file)
- `work/epics/E-19-surface-alignment-and-compatibility-cleanup/spec.md` — header status + milestone table row flipped `next` → `in-progress`
- `ROADMAP.md` — E-19 status line flipped `m-E19-03 next` → `m-E19-03 in-progress`
- `work/epics/epic-roadmap.md` — E-19 row status + milestones list flipped `next` → `in-progress`
- `CLAUDE.md` — Current Work section: immediate next step, E-19 block (branch topology, completed, scope, next), all synced to m-E19-03 active on milestone branch
