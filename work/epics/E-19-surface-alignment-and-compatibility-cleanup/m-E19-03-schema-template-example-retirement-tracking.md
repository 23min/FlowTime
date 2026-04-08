# Tracking: m-E19-03 Schema, Template & Example Retirement

**Status:** completed (2026-04-08)
**Epic:** [E-19 Surface Alignment & Compatibility Cleanup](./spec.md)
**Milestone spec:** [m-E19-03-schema-template-example-retirement.md](./m-E19-03-schema-template-example-retirement.md)
**Branch:** `milestone/m-E19-03-schema-template-example-retirement` (off `epic/E-19`)
**Final test count:** 1250 passed, 9 skipped, 0 failed
**Grep guards:** 11/11 passing via `scripts/m-E19-03-grep-guards.sh`

## Acceptance Criteria

- [x] AC1. UI demo template generators in `src/FlowTime.UI/Services/TemplateServiceImplementations.cs` emit current `binSize`/`binUnit` schema in lines 431, 475, 1356, 1462, 1536; migration warning comment removed. (Bundle A, commit `dd61ca6`)
- [x] AC2. `src/FlowTime.UI/wwwroot/sample/run-example.json` grid shape rewritten to `{ "bins": 8, "binSize": 1, "binUnit": "hours" }`. (Bundle A, commit `dd61ca6`)
- [x] AC3. `src/FlowTime.Cli/Program.cs:98` verbose label rewritten to `binSize={grid.BinSize}, binUnit={grid.BinUnit...}`. (Bundle A, commit `dd61ca6`)
- [x] AC4. YAML examples in `docs/architecture/whitepaper.md:250` and `docs/architecture/retry-modeling.md` (lines 417, 466, 527) rewritten to current schema; Little's Law formula on `whitepaper.md:77` preserved with `<!-- m-E19-03:allow-binminutes-notation -->` marker appended. (Bundle B, commit pending)
- [x] AC5. `examples/test-old-schema.yaml`, `test-no-schema.yaml`, `test-new-schema.yaml` moved to `examples/archive/` via `git mv`; empty `examples/time-travel/` directory deleted; `examples/archive/README.md` created/updated; inbound references updated. (Bundle C, commit pending)
- [x] AC6. `docs/ui/template-integration-spec.md` moved to `docs/archive/ui/template-integration-spec.md` via `git mv`; inbound references updated. (Bundle C, commit pending)
- [x] AC7. Catalog-stale phrasing rewritten in `docs/guides/UI.md:3`, `docs/reference/contracts.md:111`, `docs/reference/engine-capabilities.md:30`. (Bundle B, commit pending). Implementation-time discovery: `engine-capabilities.md:30` claimed "no catalog/export/import/registry endpoints" but the Engine API actually has both `/v1/runs/{runId}/export/*` (6 handler literals) and `/v1/artifacts/*` registry routes. Rewrote to `No streaming endpoints.` — this is both a catalog-consistency edit and a factual correction. Broader engine-capabilities.md accuracy audit not in scope for m-E19-03.
- [x] AC8. `["binMinutes"]` dictionary-key literals in `tests/FlowTime.UI.Tests/ParameterConversionIntegrationTests.cs` (lines 23, 51, 107) renamed to `["binSize"]`; test class still passes without other edits. (Bundle A, commit `dd61ca6`)
- [x] AC9. `scripts/m-E19-03-grep-guards.sh` created, 11 guards implemented, all passing. (commit pending)
- [x] AC10. Status surfaces reconciled at wrap (spec, epic spec, ROADMAP.md, epic-roadmap.md, CLAUDE.md, this tracking doc); final test count and grep-guard results recorded. (wrap commit pending)

## Commit Plan (Bundles)

Per milestone spec Technical Notes — five focused commits plus the wrap.

- [x] **Bundle A** (AC1 + AC2 + AC3 + AC8): deprecated `binMinutes` authoring shape in code — four code/fixture rewrites in one conceptual cleanup. Commit `dd61ca6`. 4 files changed, +37 −24. Tests: 1250 passed, 9 skipped, 0 failed.
- [x] **Bundle B** (AC4 + AC7): active docs cleanup — rewrites `binMinutes` YAML examples and catalog-stale phrasing. Commit pending. 5 files changed. Tests: 1250 passed, 9 skipped, 0 failed.
- [x] **Bundle C** (AC5 + AC6): archive moves — three schema-migration example YAMLs, empty `time-travel/` dir, stale UI spec. Commit pending. 8 files changed. Tests: 1250 passed, 9 skipped, 0 failed.
- [x] **AC9**: grep-guard script as its own commit. Commit pending. 11/11 guards passing.
- [x] **AC10**: wrap — tracking doc finalization and status-surface reconciliation. Commit pending.

Initial **status-sync commit** (flip m-E19-03 draft→in-progress across status surfaces, create milestone spec and this tracking doc) runs before Bundle A.

## Grep Guards

Each must return zero matches after the milestone completes. Full script: `scripts/m-E19-03-grep-guards.sh`.

- [x] Guard 1: No `binMinutes` in `src/FlowTime.UI/Services/TemplateServiceImplementations.cs` (verified after Bundle A)
- [x] Guard 2: No `binMinutes` in `src/FlowTime.UI/wwwroot/` (verified after Bundle A)
- [x] Guard 3: No `binMinutes` in `src/FlowTime.Cli/` (verified after Bundle A)
- [x] Guard 4: No `binMinutes` in `docs/architecture/whitepaper.md` except lines containing `m-E19-03:allow-binminutes-notation` (verified after Bundle B — single allowlisted occurrence on line 77)
- [x] Guard 5: No `binMinutes` in `docs/architecture/retry-modeling.md` (verified after Bundle B)
- [x] Guard 6: No `examples/test-old-schema.yaml`, `test-no-schema.yaml`, or `test-new-schema.yaml` reference outside `examples/archive/` and `docs/archive/` (verified after Bundle C — surviving matches in `work/epics/completed/` and m-E19-03 tracking/spec docs are explicitly allowlisted by scope)
- [x] Guard 7: No active reference (outside `docs/archive/`) to `docs/ui/template-integration-spec.md` (verified after Bundle C — `supported-surfaces.md` refs updated to `docs/archive/ui/`)
- [x] Guard 8: No active reference to pre-v1 routes `/api/templates/{id}/schema` or `/api/templates/generate` outside `docs/archive/` and release notes (verified — pre-v1 route literals now only exist inside the archived `docs/archive/ui/template-integration-spec.md` content itself)
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

### Bundle C — commit pending

Archive moves for three schema-migration example YAMLs and the stale pre-v1 UI spec. No source code, no tests, no docs content changes beyond link updates. 8 files changed.

**Moves (via `git mv` so rename history is preserved):**
- `examples/test-old-schema.yaml` → `examples/archive/test-old-schema.yaml`
- `examples/test-no-schema.yaml` → `examples/archive/test-no-schema.yaml`
- `examples/test-new-schema.yaml` → `examples/archive/test-new-schema.yaml`
- `docs/ui/template-integration-spec.md` → `docs/archive/ui/template-integration-spec.md`

**Deletions:**
- `examples/time-travel/` — empty leftover directory, `rmdir`'d.

**Creations:**
- `examples/archive/README.md` — archive index explaining what the three test-*-schema YAMLs are, their purpose (schema-transition coverage fixtures), their status under `ModelValidator`'s rejection gate, and links to the m-E19-01 decision record and current schema documentation.

**Inbound-reference updates (`docs/architecture/supported-surfaces.md`):**
- Row 78 (Examples matrix row): three YAML link paths flipped to `examples/archive/`; decision column updated from `archive` to `archived (m-E19-03)`.
- Row 83 (Current docs matrix row for template-integration-spec): link path flipped to `docs/archive/ui/`; decision column updated from `archive/update` to `archived (m-E19-03)`.
- Raw sweep appendix rows 206-208: three YAML link paths flipped to `examples/archive/`.
- Raw sweep appendix row 229: template-integration-spec link path flipped to `docs/archive/ui/`.

**Explicitly left untouched (per spec out-of-scope):**
- `work/epics/completed/ui-schema-migration/UI-M-02.09.md` and `UI-M-02.09-log.md` — historical completed-epic logs under `work/epics/completed/`, out of scope.
- `CLAUDE.md:232` — plain-text mention of `template-integration-spec.md` in the m-E19-03 scope summary, not a path link.
- m-E19-03 spec and tracking doc's own source-path references — these describe the archive *decision and action*, so source paths are appropriate context.

**Audit (no test/code references found):**
- `src/`, `tests/`, `scripts/` grep for `test-old-schema|test-no-schema|test-new-schema`: 0 files.
- `docs/` grep for `docs/ui/template-integration-spec`: 0 files outside the now-archived path itself.

**Verification:**
- Build: 0 errors, 1 pre-existing `xUnit2031` warning
- Tests: 1250 passed / 9 skipped / 0 failed
- Grep guards 6, 7, 8: all passing

### AC9 — commit pending

Grep-guard script `scripts/m-E19-03-grep-guards.sh` codified. Modeled on `scripts/m-E19-02-grep-guards.sh`, but uses a per-guard scope pattern because m-E19-03 guards target specific files/directories across `src/`, `tests/`, `docs/`, and `examples/` rather than a uniform `src/ + tests/` sweep.

**Structural differences from m-E19-02:**
- Per-guard scope: each guard inlines its own `rg` target path(s) rather than a single top-level scope list. Needed because guards 1-5, 9, 10, 11 each target one file or one directory, and guards 6, 7, 8 sweep active doc surfaces with archive exclusions.
- Allowlist: guard 4 pipes through `grep -v 'm-E19-03:allow-binminutes-notation'` to filter the Little's Law formula line. The marker is an inline HTML comment; Markdown renderers strip it, but ripgrep sees it and the filter excludes the line deterministically without depending on line numbers (line numbers drift).
- Archive exclusions: guards 6, 7, 8 pipe through `grep -v '^docs/archive/'` (and, for guard 8, `grep -v '^docs/releases/'`) to allow references inside archived docs to survive untouched. Historical content is explicitly allowed to keep its own historical context.
- Shell: `check()` helper function replaces the m-E19-02 inline loop. Same pattern otherwise — collect `rg` output, fail if non-empty, track pass/fail counts, exit 1 with a summary line.

**First-run result:**
```
PASS  Guard 1 — no binMinutes in TemplateServiceImplementations.cs
PASS  Guard 2 — no binMinutes in src/FlowTime.UI/wwwroot/
PASS  Guard 3 — no binMinutes in src/FlowTime.Cli/
PASS  Guard 4 — no binMinutes in whitepaper.md except allowlisted Little's Law notation
PASS  Guard 5 — no binMinutes in retry-modeling.md
PASS  Guard 6 — no stale test-*-schema.yaml references outside archive
PASS  Guard 7 — no template-integration-spec.md references outside archive
PASS  Guard 8 — no pre-v1 /api/templates/ route literals outside archive
PASS  Guard 9 — no template/catalog literal in UI.md or contracts.md
PASS  Guard 10 — no catalog/export/import/registry literal in engine-capabilities.md
PASS  Guard 11 — no ["binMinutes"] dict key in ParameterConversionIntegrationTests.cs

m-E19-03 grep guards: 11/11 passed
RESULT: PASS — m-E19-03 cleanup invariants hold.
```

All eleven guards pass on first run, confirming that Bundles A, B, and C leave the active surfaces clean of every pattern m-E19-01 flagged for retirement in the matrix rows owned by m-E19-03.

### Wrap — commit pending

Final verification pass against the milestone tip (commit `a772bf3`):

- `scripts/m-E19-03-grep-guards.sh` → 11/11 passing
- `dotnet build FlowTime.sln` → 0 errors, 1 pre-existing `xUnit2031` warning in `ClassMetricsAggregatorTests.cs` (unrelated to m-E19-03)
- `dotnet test FlowTime.sln --no-build` → 1250 passed / 9 skipped / 0 failed, identical to the m-E19-02 merge baseline

**Status surfaces reconciled (all in wrap commit):**
- This tracking doc: header status `in-progress` → `completed (2026-04-08)`, final test count and grep guards summary added, wrap section appended.
- Milestone spec `m-E19-03-schema-template-example-retirement.md`: header `Status` field `in-progress` → `completed`.
- Epic spec `work/epics/E-19-surface-alignment-and-compatibility-cleanup/spec.md`: header `Status` line updated from `m-E19-01 and m-E19-02 completed, m-E19-03 in-progress` → `m-E19-01, m-E19-02, and m-E19-03 completed, m-E19-04 next`; milestone table row for m-E19-03 flipped `in-progress` → `completed`; milestone table row for m-E19-04 flipped `draft` → `next`.
- `ROADMAP.md` E-19 section status line.
- `work/epics/epic-roadmap.md` E-19 block: status line + key milestones summary line.
- `CLAUDE.md` Current Work section: immediate next step retargeted to m-E19-03 merge and m-E19-04 drafting; E-19 block branch topology updated (m-E19-03 wrap complete, awaiting merge); added `Completed (m-E19-03)` bullet summarizing the milestone; `Next` pointer updated to m-E19-04.

**No merge in the wrap commit.** Merging `milestone/m-E19-03-schema-template-example-retirement` into `epic/E-19` is a separate explicit action requiring user approval, matching the m-E19-02 pattern where the merge happened as a distinct step after wrap.

**Summary:**
- 10/10 ACs complete
- 11/11 grep guards codified and passing
- 6 commits total: `73e74d4` (status sync) → `dd61ca6` (Bundle A code) → `3aaf159` (Bundle B docs) → `adc05a0` (Bundle C archive) → `a772bf3` (AC9 guards) → wrap
- 0 deferrals, 0 gaps opened. Clean milestone.

**Deviations from spec — none.** All implementation-time discoveries (stale `engine-capabilities.md:30` fragment being factually wrong beyond catalog, no `src/`/`tests/` inbound references to archived files) were absorbed within the existing AC scope without requiring spec changes.

**Learnings appended to `work/agent-history/builder.md`** — see the m-E19-03 section for patterns worth repeating (comment-marker allowlist, per-guard scope script pattern, forward-only archive with source-path rewrite) and pitfalls to avoid (letting multi-line clarifications introduce new grep hits, assuming doc statements are factually correct just because the spec claimed they were).
