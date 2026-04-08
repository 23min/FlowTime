# Tracking: m-E19-03 Schema, Template & Example Retirement

**Status:** in-progress (started 2026-04-08)
**Epic:** [E-19 Surface Alignment & Compatibility Cleanup](./spec.md)
**Milestone spec:** [m-E19-03-schema-template-example-retirement.md](./m-E19-03-schema-template-example-retirement.md)
**Branch:** `milestone/m-E19-03-schema-template-example-retirement` (off `epic/E-19`)

## Acceptance Criteria

- [x] AC1. UI demo template generators in `src/FlowTime.UI/Services/TemplateServiceImplementations.cs` emit current `binSize`/`binUnit` schema in lines 431, 475, 1356, 1462, 1536; migration warning comment removed. (Bundle A, commit `dd61ca6`)
- [x] AC2. `src/FlowTime.UI/wwwroot/sample/run-example.json` grid shape rewritten to `{ "bins": 8, "binSize": 1, "binUnit": "hours" }`. (Bundle A, commit `dd61ca6`)
- [x] AC3. `src/FlowTime.Cli/Program.cs:98` verbose label rewritten to `binSize={grid.BinSize}, binUnit={grid.BinUnit...}`. (Bundle A, commit `dd61ca6`)
- [x] AC4. YAML examples in `docs/architecture/whitepaper.md:250` and `docs/architecture/retry-modeling.md` (lines 417, 466, 527) rewritten to current schema; Little's Law formula on `whitepaper.md:77` preserved with `<!-- m-E19-03:allow-binminutes-notation -->` marker appended. (Bundle B, commit pending)
- [ ] AC5. `examples/test-old-schema.yaml`, `test-no-schema.yaml`, `test-new-schema.yaml` moved to `examples/archive/` via `git mv`; empty `examples/time-travel/` directory deleted; `examples/archive/README.md` created/updated; inbound references updated.
- [ ] AC6. `docs/ui/template-integration-spec.md` moved to `docs/archive/ui/template-integration-spec.md` via `git mv`; inbound references updated.
- [x] AC7. Catalog-stale phrasing rewritten in `docs/guides/UI.md:3`, `docs/reference/contracts.md:111`, `docs/reference/engine-capabilities.md:30`. (Bundle B, commit pending). Implementation-time discovery: `engine-capabilities.md:30` claimed "no catalog/export/import/registry endpoints" but the Engine API actually has both `/v1/runs/{runId}/export/*` (6 handler literals) and `/v1/artifacts/*` registry routes. Rewrote to `No streaming endpoints.` — this is both a catalog-consistency edit and a factual correction. Broader engine-capabilities.md accuracy audit not in scope for m-E19-03.
- [x] AC8. `["binMinutes"]` dictionary-key literals in `tests/FlowTime.UI.Tests/ParameterConversionIntegrationTests.cs` (lines 23, 51, 107) renamed to `["binSize"]`; test class still passes without other edits. (Bundle A, commit `dd61ca6`)
- [ ] AC9. `scripts/m-E19-03-grep-guards.sh` created, 11 guards implemented, all passing.
- [ ] AC10. Status surfaces reconciled at wrap (spec, epic spec, ROADMAP.md, epic-roadmap.md, CLAUDE.md, this tracking doc); final test count and grep-guard results recorded.

## Commit Plan (Bundles)

Per milestone spec Technical Notes — five focused commits plus the wrap.

- [x] **Bundle A** (AC1 + AC2 + AC3 + AC8): deprecated `binMinutes` authoring shape in code — four code/fixture rewrites in one conceptual cleanup. Commit `dd61ca6`. 4 files changed, +37 −24. Tests: 1250 passed, 9 skipped, 0 failed.
- [x] **Bundle B** (AC4 + AC7): active docs cleanup — rewrites `binMinutes` YAML examples and catalog-stale phrasing. Commit pending. 5 files changed. Tests: 1250 passed, 9 skipped, 0 failed.
- [ ] **Bundle C** (AC5 + AC6): archive moves — three schema-migration example YAMLs, empty `time-travel/` dir, stale UI spec
- [ ] **AC9**: grep-guard script as its own commit
- [ ] **AC10**: wrap — tracking doc finalization and status-surface reconciliation

Initial **status-sync commit** (flip m-E19-03 draft→in-progress across status surfaces, create milestone spec and this tracking doc) runs before Bundle A.

## Grep Guards

Each must return zero matches after the milestone completes. Full script: `scripts/m-E19-03-grep-guards.sh`.

- [x] Guard 1: No `binMinutes` in `src/FlowTime.UI/Services/TemplateServiceImplementations.cs` (verified after Bundle A)
- [x] Guard 2: No `binMinutes` in `src/FlowTime.UI/wwwroot/` (verified after Bundle A)
- [x] Guard 3: No `binMinutes` in `src/FlowTime.Cli/` (verified after Bundle A)
- [x] Guard 4: No `binMinutes` in `docs/architecture/whitepaper.md` except lines containing `m-E19-03:allow-binminutes-notation` (verified after Bundle B — single allowlisted occurrence on line 77)
- [x] Guard 5: No `binMinutes` in `docs/architecture/retry-modeling.md` (verified after Bundle B)
- [ ] Guard 6: No `examples/test-old-schema.yaml`, `test-no-schema.yaml`, or `test-new-schema.yaml` reference outside `examples/archive/` and `docs/archive/`
- [ ] Guard 7: No active reference (outside `docs/archive/`) to `docs/ui/template-integration-spec.md`
- [ ] Guard 8: No active reference to pre-v1 routes `/api/templates/{id}/schema` or `/api/templates/generate` outside `docs/archive/` and release notes
- [x] Guard 9: No `template/catalog` literal in `docs/guides/UI.md` or `docs/reference/contracts.md` (verified after Bundle B)
- [x] Guard 10: No `catalog/export/import/registry` literal in `docs/reference/engine-capabilities.md` (verified after Bundle B)
- [x] Guard 11: No `["binMinutes"]` dictionary-key literal in `tests/FlowTime.UI.Tests/ParameterConversionIntegrationTests.cs` (verified after Bundle A)

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

Per-bundle notes, file lists, and grep-guard results are appended as each bundle lands.

### Status Sync (pre-Bundle A) — commit `73e74d4`

Initial spec + tracking doc + status-surface reconciliation. No code changes.

- New: `work/epics/E-19-.../m-E19-03-schema-template-example-retirement.md` (spec, status `in-progress`)
- New: `work/epics/E-19-.../m-E19-03-schema-template-example-retirement-tracking.md` (this file)
- Modified: epic spec (header + milestone table row flipped `next` → `in-progress`)
- Modified: `ROADMAP.md` (E-19 status line)
- Modified: `work/epics/epic-roadmap.md` (E-19 row status + milestones list)
- Modified: `CLAUDE.md` Current Work section (immediate next step, branch topology, m-E19-03 scope summary)

### Bundle A — commit `dd61ca6`

Deprecated `binMinutes` authoring shape retired from every active first-party code surface. 4 files changed, +37 −24.

**Files:**
- `src/FlowTime.UI/Services/TemplateServiceImplementations.cs` — 2× JSON schema property blocks (transportation-basic, manufacturing-line) replaced `binMinutes` with paired `binSize`+`binUnit` entries matching the existing `it-system-microservices` pattern; 3× demo YAML generators (transportation, manufacturing, supply-chain) emit `binSize: 1, binUnit: hours` or `binUnit: days` instead of `binMinutes: 60`/`1440`; top-of-file `⚠️ SCHEMA MIGRATION IN PROGRESS` warning comment removed.
- `src/FlowTime.UI/wwwroot/sample/run-example.json` — grid shape flipped to `{ "bins": 8, "binSize": 1, "binUnit": "hours" }`.
- `src/FlowTime.Cli/Program.cs` — verbose run summary label now reads `bins={grid.Bins}, binSize={grid.BinSize}, binUnit={grid.BinUnit.ToString().ToLowerInvariant()}` (the underlying `TimeGrid.BinMinutes` computed property stays untouched — it is the internal concept, not a schema field).
- `tests/FlowTime.UI.Tests/ParameterConversionIntegrationTests.cs` — 3× dict literals flipped `["binMinutes"] = 60` → `["binSize"] = 60`. `binSize` is an actual template parameter per `templates/transportation-basic.yaml:23`, and the test's assertions all target `demandPattern`/`capacityPattern` serialization, not the grid key — so the rename is semantically equivalent.

**Preserved (verified untouched per spec § Preserved Surfaces):**
- `TimeGrid.BinMinutes` computed property
- `MetricsGrid.BinMinutes` wire-format field
- `ModelValidator` `binMinutes` rejection gate + covering `TargetSchemaValidationTests`
- `RuntimeAnalyticalEvaluator` internal `binMinutes` method parameters
- `FlowTimeApiModels`/`FlowTimeSimApiClient` `[JsonIgnore]`-annotated computed `BinMinutes` display helpers
- `TimeTravelMetricsClient`/`TimeTravelApiModels` consumers of retained `MetricsGrid.BinMinutes`
- `SimResultsService` internal `var binMinutes = 60` mock-data variable
- `Simulate.razor`/`TimeTravel/Topology.razor` UI consumers of computed display helpers

**Verification:**
- Build: 0 errors, 1 pre-existing `xUnit2031` warning in `ClassMetricsAggregatorTests.cs`
- Tests: 1250 passed / 9 skipped / 0 failed (identical to m-E19-02 baseline)
- Grep: zero `binMinutes` in all four guard targets (guards 1, 2, 3, 11)

**Environmental note:** encountered disk-space failure on first test run (`No space left on device` writing `/tmp/flowtime_template_validation_*/run_deterministic_*/series/*.csv`). Root cause was Docker Desktop VM overlay at 100% (not FlowTime-specific — cumulative across all containers on the host). Freed with `dotnet clean` + stale `/tmp/flowtime_*` cleanup (~120 MB free). Tests re-ran clean. User freed Docker host disk space between Bundle A and Bundle B (196 GB free by Bundle B test run).

### Bundle B — commit pending

Active docs cleanup. Docs-only changes, no code. 5 files changed.

**Files:**
- `docs/architecture/whitepaper.md` — (a) line 77 Little's Law formula preserved and allowlisted with inline `<!-- m-E19-03:allow-binminutes-notation -->` marker plus a two-word clarification ("Derived concept: bin-duration-in-minutes. Not the deprecated YAML schema field."); (b) line 250 YAML example flipped `grid: { bins: 6, binMinutes: 5 }` → `grid: { bins: 6, binSize: 5, binUnit: minutes }`.
- `docs/architecture/retry-modeling.md` — three identical YAML example lines (417, 466, 527) flipped `grid: { bins: 24, binMinutes: 60 }` → `grid: { bins: 24, binSize: 1, binUnit: hours }` via `replace_all`.
- `docs/guides/UI.md` — line 3: `template/catalog calls` → `template calls`.
- `docs/reference/contracts.md` — line 111: `template/catalog endpoints for model generation` → `template endpoints for model generation`.
- `docs/reference/engine-capabilities.md` — line 30: `No streaming endpoints; no catalog/export/import/registry endpoints.` → `No streaming endpoints.` (discovered at implementation time: the fragment was factually wrong beyond the catalog issue — Engine has 6 export + artifact registry handler literals in `Program.cs`, so a "drop `catalog/` only" edit would have left the line still half-incorrect. Dropped the whole fragment as the closest natural phrasing allowed by the spec.)

**Verification:**
- Build: 0 errors, only the pre-existing `xUnit2031` warning
- Tests: 1250 passed / 9 skipped / 0 failed
- Grep guards 4, 5, 9, 10: all passing. Only `binMinutes` match in `whitepaper.md` is line 77 with the allowlist marker.
