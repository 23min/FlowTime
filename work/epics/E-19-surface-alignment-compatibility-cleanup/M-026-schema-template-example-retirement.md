---
id: M-026
title: Schema, Template & Example Retirement
status: done
parent: E-19
---

## Goal

Remove deprecated schema shapes, demo-template residue, schema-migration compatibility examples, and stale authoring docs from active first-party surfaces. When this milestone closes, no active `src/`, `templates/`, `examples/`, or `docs/` surface emits or promotes the deprecated `binMinutes` YAML authoring shape, and no schema-migration fixture or pre-v1 authoring spec survives on the current `examples/` or `docs/ui/` surfaces.

## Context

[M-024](./M-024.md) inventoried active schema, template, example, and docs surfaces in the supported-surfaces matrix at [docs/architecture/supported-surfaces.md](../../../docs/architecture/supported-surfaces.md) and assigned owning milestones. Every row whose `Owning milestone` column is `m-E19-03` is executed here.

[M-025](./M-025.md) already deleted the runtime seams (stored drafts, Sim ZIP archive layer, Engine bundle-import, runtime catalogs, `/api/v1/drafts/validate`, Engine `/v1/debug/scan-directory`) and narrowed `/api/v1/drafts/run` to inline-only. This milestone is the schema/authoring cleanup pass over the same supported-surface baseline.

Scope boundaries inherited from M-024:

- `FlowTime.Core`, `FlowTime.Generator`, `FlowTime.API`, and `FlowTime.Sim.*` are **not renamed** and their high-level responsibilities do not change in E-19.
- Analytical surfaces purified by E-16 (notably `MetricsContracts.MetricsGrid.BinMinutes` as a retained wire-format field, the `TimeGrid.BinMinutes` computed property, `ModelValidator`'s `binMinutes` rejection gate, and `TargetSchemaValidationTests` that assert the gate) are explicitly out of scope and must remain untouched.
- Engine and Sim runtime route deletions are not re-opened here — M-025 owns them.
- Blazor stale-wrapper cleanup and demo-mode policy belong to M-027.
- `POST /v1/run` / `POST /v1/graph` remain deferred per [D-042](../../decisions.md#d-2026-04-08-029-defer-post-v1run-and-post-v1graph-deletion-out-of-m-e19-02-ac6-scope-narrowing). Tests that deserialize `Grid { int binMinutes }` against those routes stay as-is.

The distinction this milestone enforces:

- **`binMinutes` as a YAML authoring schema field** — deprecated. Engine's `ModelValidator` rejects it at parse time. Current authoring schema is `binSize` + `binUnit`. Any active surface emitting `binMinutes` in an authored YAML shape is in scope for this milestone.
- **`binMinutes` as the derived internal concept** (bin duration in minutes) — still live. `TimeGrid.BinMinutes`, `MetricsContracts.MetricsGrid.BinMinutes`, internal analytical math, and mathematical notation in architecture docs are out of scope.

## Acceptance Criteria

### AC1 — UI demo template generators emit current schema (schema-migration residue)

`src/FlowTime.UI/Services/TemplateServiceImplementations.cs` is the Blazor mock template service used by demo mode. It currently declares two `JsonSchemaProperty` entries keyed `"binMinutes"` and emits three demo YAML strings with `grid: binMinutes: 60` / `binMinutes: 1440`. These are active surfaces promoting the deprecated YAML authoring shape.

**Rewrite:**

- [src/FlowTime.UI/Services/TemplateServiceImplementations.cs:431](../../../src/FlowTime.UI/Services/TemplateServiceImplementations.cs) — remove the `["binMinutes"]` `JsonSchemaProperty` entry. Replace with a `binSize` / `binUnit` pair matching the current authoring schema, or drop the property if no demo template exposes a bin-duration parameter.
- [src/FlowTime.UI/Services/TemplateServiceImplementations.cs:475](../../../src/FlowTime.UI/Services/TemplateServiceImplementations.cs) — same treatment as line 431.
- [src/FlowTime.UI/Services/TemplateServiceImplementations.cs:1356](../../../src/FlowTime.UI/Services/TemplateServiceImplementations.cs) — demo YAML generator currently writes `grid:\n  binMinutes: 60`. Rewrite to `binSize: 1\n  binUnit: hours`.
- [src/FlowTime.UI/Services/TemplateServiceImplementations.cs:1462](../../../src/FlowTime.UI/Services/TemplateServiceImplementations.cs) — same; rewrite `binMinutes: 60` to `binSize: 1, binUnit: hours`.
- [src/FlowTime.UI/Services/TemplateServiceImplementations.cs:1536](../../../src/FlowTime.UI/Services/TemplateServiceImplementations.cs) — rewrite `binMinutes: 1440` to `binSize: 1, binUnit: days` (daily bins).
- Remove the top-of-file `⚠️ SCHEMA MIGRATION IN PROGRESS` warning comment once the file is clean.

**Preserve:**

- Any `JsonIgnore`-annotated computed `BinMinutes` property used purely for UI display (e.g. in `GridInfo`, `TimeTravelMetricsGridDto`) — these are internal convenience fields, not authoring shapes.
- Demo mode itself — M-026 does not retire demo mode. Blazor demo-mode policy is M-027.

**Grep guard:** No `binMinutes` literal remains anywhere under `src/FlowTime.UI/Services/TemplateServiceImplementations.cs`. Broader `src/FlowTime.UI/` check is deferred to AC7 (grep guard script).

### AC2 — UI sample fixture uses current schema

[src/FlowTime.UI/wwwroot/sample/run-example.json](../../../src/FlowTime.UI/wwwroot/sample/run-example.json) currently reads:

```json
{ "grid": { "bins": 8, "binMinutes": 60 }, ... }
```

This is a static authoring fixture shipped with the Blazor UI and is not a wire-format response. Rewrite the grid shape to current schema:

```json
{ "grid": { "bins": 8, "binSize": 1, "binUnit": "hours" }, ... }
```

**Grep guard:** No `binMinutes` literal remains under `src/FlowTime.UI/wwwroot/`.

### AC3 — CLI verbose output label uses current schema

[src/FlowTime.Cli/Program.cs:98](../../../src/FlowTime.Cli/Program.cs) currently prints:

```csharp
Console.WriteLine($"  Grid: bins={grid.Bins}, binMinutes={grid.BinMinutes}");
```

The underlying `TimeGrid` record already exposes `BinSize` and `BinUnit` ([src/FlowTime.Core/Models/TimeGrid.cs:58-59](../../../src/FlowTime.Core/Models/TimeGrid.cs)). Rewrite the label to expose the current schema shape:

```csharp
Console.WriteLine($"  Grid: bins={grid.Bins}, binSize={grid.BinSize}, binUnit={grid.BinUnit.ToString().ToLowerInvariant()}");
```

The computed `TimeGrid.BinMinutes` property itself stays — it is the live internal concept, not a deprecated schema field. Only the user-facing label string changes.

**Grep guard:** No `binMinutes` literal remains under `src/FlowTime.Cli/`.

### AC4 — Active architecture docs use current schema in YAML examples

Two active architecture docs contain YAML authoring examples still using the deprecated grid shape. Rewrite every YAML example; leave mathematical notation that uses `binMinutes` as the live derived concept (AC4 is about authoring shapes, not math).

**Rewrite YAML examples:**

- [docs/architecture/whitepaper.md:250](../../../docs/architecture/whitepaper.md) — `grid: { bins: 6, binMinutes: 5 }` → `grid: { bins: 6, binSize: 5, binUnit: minutes }`.
- [docs/architecture/retry-modeling.md:417](../../../docs/architecture/retry-modeling.md) — `grid: { bins: 24, binMinutes: 60 }` → `grid: { bins: 24, binSize: 1, binUnit: hours }`.
- [docs/architecture/retry-modeling.md:466](../../../docs/architecture/retry-modeling.md) — same rewrite.
- [docs/architecture/retry-modeling.md:527](../../../docs/architecture/retry-modeling.md) — same rewrite.

**Explicitly leave alone:**

- [docs/architecture/whitepaper.md:77](../../../docs/architecture/whitepaper.md) — Little's Law formula `W[t] ≈ Q[t] / served_rate[t] * binMinutes`. This is mathematical notation for the live derived concept (bin duration in minutes), not a schema reference. `TimeGrid.BinMinutes`, `MetricsGrid.BinMinutes`, and internal evaluator math all still use this concept. **Append the inline marker `<!-- m-E19-03:allow-binminutes-notation -->` to the end of this line** so the grep guard script can deterministically allowlist it.
- [docs/architecture/reviews/*](../../../docs/architecture/reviews/) — historical point-in-time review snapshots. Out of scope.
- [docs/schemas/model.schema.md](../../../docs/schemas/model.schema.md) and [docs/schemas/model.schema.yaml](../../../docs/schemas/model.schema.yaml) — authoritative migration docs that explain the historical transition. Their `binMinutes` references are documented history, not current guidance.

**Grep guard:** No `binMinutes` literal remains in `docs/architecture/whitepaper.md` or `docs/architecture/retry-modeling.md` **except** lines containing the marker `m-E19-03:allow-binminutes-notation`. The marker is an HTML comment that Markdown renderers strip from display; it lets the grep-guard script allowlist legitimate derived-concept notation without depending on drift-prone line numbers.

### AC5 — Schema-migration example fixtures archived

The three schema-migration example YAMLs under `examples/` exist solely as back-compat coverage fixtures, not as current user-facing examples. Per M-024's supported-surfaces matrix (row for schema-migration compatibility examples), their decision is `archive`.

**Move (preserve git history via `git mv`):**

- [examples/test-old-schema.yaml](../../../examples/test-old-schema.yaml) → `examples/archive/test-old-schema.yaml`
- [examples/test-no-schema.yaml](../../../examples/test-no-schema.yaml) → `examples/archive/test-no-schema.yaml`
- [examples/test-new-schema.yaml](../../../examples/test-new-schema.yaml) → `examples/archive/test-new-schema.yaml`

**Delete:**

- [examples/time-travel/](../../../examples/time-travel/) — empty leftover directory. `rmdir` it.

**Audit:**

- Search for callers of the moved files in `src/`, `tests/`, `docs/`, and scripts. Update any references to point at the new `examples/archive/` path or remove the reference if it is dead.
- Add a short `examples/archive/README.md` (or update any existing README in that folder) noting that the files are schema-migration fixtures preserved for historical reference, not current examples.

**Grep guard:** No path `examples/test-old-schema.yaml`, `examples/test-no-schema.yaml`, or `examples/test-new-schema.yaml` remains referenced anywhere in `src/`, `tests/`, or active `docs/` content. Matches under `examples/archive/` and `docs/archive/` are allowed.

### AC6 — Stale template-integration spec archived

[docs/ui/template-integration-spec.md](../../../docs/ui/template-integration-spec.md) is a pre-v1 UI spec that references `/api/templates/{templateId}/schema` and `/api/templates/generate` routes (pre-v1 template surface), contains `binMinutes` references, and carries its own `⚠️ SCHEMA MIGRATION IN PROGRESS` warning. Per M-024's matrix (`archive/update`), move it to the archive tree:

**Move:**

- [docs/ui/template-integration-spec.md](../../../docs/ui/template-integration-spec.md) → `docs/archive/ui/template-integration-spec.md`

**Audit:**

- Search for inbound links in `docs/`, `README.md`, `CLAUDE.md`, and any other active doc. Remove dead links or update to the archive path.

**Grep guard:** No active docs (outside `docs/archive/`) reference `docs/ui/template-integration-spec.md` or the pre-v1 routes `/api/templates/{id}/schema` or `/api/templates/generate`.

### AC7 — Catalog-stale phrasing in active docs updated

M-025 deleted all catalog routes, services, UI components, and DTOs per A5. Two active docs still carry leftover phrasing describing Sim as owning "template/catalog" endpoints. Rewrite the phrasing:

**Rewrite:**

- [docs/guides/UI.md:3](../../../docs/guides/UI.md) — drop `template/catalog calls` to `template calls` (the Sim API hosts template authoring, not catalogs).
- [docs/reference/contracts.md:111](../../../docs/reference/contracts.md) — drop `template/catalog endpoints for model generation` to `template endpoints for model generation`.
- [docs/reference/engine-capabilities.md:30](../../../docs/reference/engine-capabilities.md) — rewrite `no catalog/export/import/registry endpoints` to drop `catalog/` for consistency with M-025's catalog retirement. The statement becomes `no streaming endpoints; no export/import/registry endpoints` (or the closest natural phrasing). The line is factually true either way — this is a consistency edit, not a correction.

**Explicitly leave alone (not in scope):**

- [docs/templates/profiles.md:58](../../../docs/templates/profiles.md) — incidental English phrase `catalog authors stay consistent`, not a FlowTime catalog reference.
- [docs/architecture/template-draft-model-run-bundle-boundary.md](../../../docs/architecture/template-draft-model-run-bundle-boundary.md) — already documents catalogs in their historical/retired context correctly.
- [docs/reference/contracts.md:124](../../../docs/reference/contracts.md) — already correctly notes `no catalog endpoints are shipped`.

**Grep guard:** No `template/catalog` literal remains in `docs/guides/UI.md` or `docs/reference/contracts.md`. No `catalog/export/import/registry` phrasing remains in `docs/reference/engine-capabilities.md`.

### AC8 — Test fixtures with stale parameter keys cleaned

[tests/FlowTime.UI.Tests/ParameterConversionIntegrationTests.cs](../../../tests/FlowTime.UI.Tests/ParameterConversionIntegrationTests.cs) uses `["binMinutes"] = 60` as a template parameter key in three test dictionaries (lines 23, 51, 107). Active templates expose `binSize` (not `binMinutes`) — see [templates/transportation-basic.yaml:23](../../../templates/transportation-basic.yaml) — so the test key references a template parameter that does not exist. The test itself is about parameter type conversion (string arrays vs number arrays being serialized to `demandPattern` / `capacityPattern`) and does not assert anything about the grid parameter key, so a rename preserves semantic meaning.

**Rewrite:**

- Lines 23, 51, 107 — rename the key `["binMinutes"]` to `["binSize"]` in each dictionary literal. Value stays `60` (which is now "60 minutes" interpreted per `binUnit: minutes`, matching the transportation-basic template's default parameter). The assertions on `demandPattern` and `capacityPattern` serialization remain unchanged and continue to exercise the type-conversion behavior the test is named after.
- Confirm at commit time that the test class still passes after the rename with no other edits.

**Explicitly leave alone:**

- Any test that uses `binMinutes` as an internal local variable name (e.g. [tests/FlowTime.UI.Tests/TemplateServiceMetadataTests.cs](../../../tests/FlowTime.UI.Tests/TemplateServiceMetadataTests.cs)) — local naming, not schema.
- Any test that asserts `binMinutes` is rejected by validators (e.g. [tests/FlowTime.Tests/Schema/TargetSchemaValidationTests.cs](../../../tests/FlowTime.Tests/Schema/TargetSchemaValidationTests.cs)) — legitimate invariant test.
- Any test that asserts `binMinutes` does **not** appear in serialized JSON (e.g. [tests/FlowTime.UI.Tests/GridInfoSchemaTests.cs](../../../tests/FlowTime.UI.Tests/GridInfoSchemaTests.cs), `SimGridInfoSchemaTests.cs`) — legitimate invariant test.
- [tests/FlowTime.Tests/ApiIntegrationTests.cs:93](../../../tests/FlowTime.Tests/ApiIntegrationTests.cs), [tests/FlowTime.Api.Tests/Legacy/ApiIntegrationTests.cs:188](../../../tests/FlowTime.Api.Tests/Legacy/ApiIntegrationTests.cs) — `Grid { int binMinutes }` DTOs that deserialize the retained `MetricsGrid.BinMinutes` wire-format field from `POST /v1/run` and `POST /v1/graph`. Deferred per D-042.
- [tests/FlowTime.Api.Tests/StateEndpointTests.cs](../../../tests/FlowTime.Api.Tests/StateEndpointTests.cs), [tests/FlowTime.Api.Tests/StateResponseSchemaTests.cs](../../../tests/FlowTime.Api.Tests/StateResponseSchemaTests.cs) — state query tests passing the current request-shape including the retained `binMinutes` field. Active contract.
- [tests/FlowTime.Api.Tests/Golden/metrics-run_metrics_fixture.json](../../../tests/FlowTime.Api.Tests/Golden/metrics-run_metrics_fixture.json) — golden fixture for the retained `MetricsGrid` response shape.
- [tests/FlowTime.Core.Tests/Safety/NaNPolicyTests.cs](../../../tests/FlowTime.Core.Tests/Safety/NaNPolicyTests.cs) — internal `ComputeLatencyMinutes(binMinutes: …)` helper parameter name, not a schema reference.

**Grep guard:** No `["binMinutes"]` dictionary-key literal remains in `tests/FlowTime.UI.Tests/ParameterConversionIntegrationTests.cs`.

### AC9 — Grep-guard script codified

Create `scripts/m-E19-03-grep-guards.sh` mirroring the structure of `scripts/m-E19-02-grep-guards.sh`. Every guard listed in AC1–AC8 becomes a line in the script. The script must exit 0 when all guards pass.

**Guards, as implemented in the script (each a named test):**

1. No `binMinutes` in `src/FlowTime.UI/Services/TemplateServiceImplementations.cs`
2. No `binMinutes` in `src/FlowTime.UI/wwwroot/`
3. No `binMinutes` in `src/FlowTime.Cli/`
4. No `binMinutes` in `docs/architecture/whitepaper.md` **except** lines containing the comment marker `m-E19-03:allow-binminutes-notation`. The script filters with `grep -v 'm-E19-03:allow-binminutes-notation'` before counting matches.
5. No `binMinutes` in `docs/architecture/retry-modeling.md`
6. No `examples/test-old-schema.yaml`, `examples/test-no-schema.yaml`, or `examples/test-new-schema.yaml` path literal outside `examples/archive/` and `docs/archive/`
7. No active reference (outside `docs/archive/`) to `docs/ui/template-integration-spec.md`
8. No active reference to pre-v1 routes `/api/templates/{id}/schema` or `/api/templates/generate` outside `docs/archive/` and release notes
9. No `template/catalog` literal in `docs/guides/UI.md` or `docs/reference/contracts.md`
10. No `catalog/export/import/registry` literal in `docs/reference/engine-capabilities.md`
11. No `["binMinutes"]` dictionary-key literal in `tests/FlowTime.UI.Tests/ParameterConversionIntegrationTests.cs`

Scoped searches are limited to `src/`, `tests/`, `docs/`, `examples/`, and `templates/` by default, with per-guard exclusions for `docs/archive/`, `docs/releases/`, `docs/architecture/reviews/`, `work/epics/completed/`, and anywhere the guard explicitly allowlists (including the comment-marker allowlist on guard 4).

The script runs locally and in the wrap pass. It is not wired into CI in this milestone — `scripts/m-E19-02-grep-guards.sh` remains the pattern, and CI wiring is deferred.

### AC10 — Tracking doc and status surfaces reconciled

- Create `work/epics/E-19-surface-alignment-and-compatibility-cleanup/m-E19-03-schema-template-example-retirement-tracking.md` at milestone start and update it after each AC lands. Tracking doc records: per-AC file changes, grep-guard results, test counts, and deviations from the spec (if any).
- Flip milestone status in a single reconciliation pass at wrap time:
  - This spec: `draft` → `in-progress` at start → `completed` at wrap.
  - [work/epics/E-19-surface-alignment-and-compatibility-cleanup/spec.md](./spec.md) milestone table: `m-E19-03` status `next` → `in-progress` → `completed`; header `Status:` line updated; `## Milestones` sequence note updated to point at `m-E19-04`.
  - [ROADMAP.md](../../../ROADMAP.md) E-19 section: sync M-026 completion and name `m-E19-04` as next.
  - [work/epics/epic-roadmap.md](../epic-roadmap.md) E-19 row: same sync.
  - [CLAUDE.md](../../../CLAUDE.md) Current Work section: sync E-19 topology and next-step pointer.
- All status-surface updates happen in a single wrap commit after the grep guards pass.

## Technical Notes

### Commit plan (bundled)

ACs are grouped into five focused commits plus the wrap. Each bundle is a single atomic concept so bisect points to one conceptual slice of the milestone.

1. **Bundle A — deprecated `binMinutes` authoring shape in code (AC1 + AC2 + AC3 + AC8).** Four code/fixture edits that all remove `binMinutes` as an authoring/parameter/display label: `TemplateServiceImplementations.cs` demo generators, `run-example.json` fixture, `Cli/Program.cs` verbose label, `ParameterConversionIntegrationTests.cs` parameter key. One conceptual cleanup, one commit.
2. **Bundle B — active docs cleanup (AC4 + AC7).** Rewrites deprecated YAML examples in `whitepaper.md`/`retry-modeling.md` and the catalog-stale phrasing in `UI.md`/`contracts.md`/`engine-capabilities.md`. One docs-cleanup pass, one commit. Includes appending the `m-E19-03:allow-binminutes-notation` marker to `whitepaper.md:77`.
3. **Bundle C — archive moves (AC5 + AC6).** Move 3 test-schema YAMLs to `examples/archive/`, move `template-integration-spec.md` to `docs/archive/ui/`, delete empty `examples/time-travel/`, update any inbound references. All archive operations in one commit so the tree is never half-migrated.
4. **Grep guard script (AC9).** Its own commit. The script must pass against the tree from commits 1–3, proving the cleanup is complete before the wrap.
5. **Wrap (AC10).** Tracking doc finalization and status-surface reconciliation in a single commit after the grep guards pass.

If any bundle surfaces a complication at implementation time (e.g. inbound reference to an archived file requires a cross-bundle edit), stop and present options before widening or splitting the bundle, the way M-025 handled the AC6 scope narrowing.

### Implementation notes

- All archive moves use `git mv` so rename history stays intact.
- Do not rewrite or delete historical review docs, completed-milestone specs, or archived Sim docs under `docs/archive/docs-sim/`. Anything under `docs/archive/`, `docs/releases/`, `work/epics/completed/`, or `docs/architecture/reviews/` is out of scope regardless of whether it contains `binMinutes` or `catalog` references.
- Do not introduce new demo templates or new sample fixtures. If a demo yaml generator no longer has a natural `binMinutes`-equivalent parameter after the rewrite, drop the parameter rather than inventing a new one.
- Do not add advisory comments like `// deprecated, see m-E19-04` to files being rewritten. Forward-only — once the schema shape is current, no migration commentary is needed.
- When moving files to `docs/archive/ui/` and `examples/archive/`, ensure the target directory exists (create it with `mkdir -p` if needed) before the `git mv`.
- The grep-guard script allowlists `whitepaper.md:77` via an HTML comment marker on the line itself (`<!-- m-E19-03:allow-binminutes-notation -->`). Markdown renderers strip the comment from display; the script filters matching lines with `grep -v`. This avoids the drift problem that line-number allowlists hit.

## Preserved Surfaces

Explicit list of surfaces that must remain untouched by this milestone. Any accidental change to these surfaces is a milestone regression.

- `src/FlowTime.Core/Models/TimeGrid.cs` — `BinMinutes` computed property is the live internal concept.
- `src/FlowTime.Core/Models/ModelValidator.cs` — `binMinutes` rejection gate at `ValidateGrid`.
- `src/FlowTime.Core/Metrics/RuntimeAnalyticalEvaluator.cs` — internal `binMinutes` parameter on helper methods.
- `src/FlowTime.Contracts/TimeTravel/MetricsContracts.cs` — `MetricsGrid.BinMinutes` retained wire-format field.
- `src/FlowTime.API/Services/MetricsService.cs`, `StateQueryService.cs`, `AggregatesCsvExporter.cs`, `NdjsonExporter.cs`, `ParquetExporter.cs` — internal `BinMinutes` options and computed fields driven by the retained `MetricsGrid` contract.
- `src/FlowTime.UI/Services/FlowTimeApiModels.cs`, `FlowTimeSimApiClient.cs` — `[JsonIgnore]`-annotated computed `BinMinutes` display helpers and their `NOT serialized to/from JSON` comments.
- `src/FlowTime.UI/Services/TimeTravelMetricsClient.cs`, `TimeTravelApiModels.cs` — `binMinutes` consumption of the retained `MetricsGrid` field for display.
- `src/FlowTime.UI/Services/SimResultsService.cs` — `var binMinutes = 60` internal variable for mock-data generation.
- `src/FlowTime.UI/Pages/Simulate.razor`, `Pages/TimeTravel/Topology.razor` — UI consumers of the computed `GridInfo.BinMinutes` display helper.
- `docs/schemas/model.schema.md`, `docs/schemas/model.schema.yaml` — authoritative migration docs.
- `docs/architecture/reviews/*` — historical review snapshots.
- `docs/architecture/whitepaper.md:77` — Little's Law math notation.
- `examples/m0.const.yaml`, `m0.const.sim.yaml`, `m0.poisson.sim.yaml`, `m15.complex-pmf.yaml`, `m2.pmf.yaml`, `class-enabled.yaml`, `hello/model.yaml`, `http-demo/*.csv` — active examples, already on current schema.
- All 12 YAML files under `templates/` — already on current schema per the template sweep.
- `tests/FlowTime.Tests/Schema/TargetSchemaValidationTests.cs`, `tests/FlowTime.UI.Tests/GridInfoSchemaTests.cs`, `tests/FlowTime.UI.Tests/SimGridInfoSchemaTests.cs`, `tests/FlowTime.UI.Tests/GraphRunResultSchemaTests.cs`, `tests/FlowTime.Core.Tests/Safety/NaNPolicyTests.cs`, `tests/FlowTime.UI.Tests/TemplateServiceMetadataTests.cs`, `tests/FlowTime.Tests/ApiIntegrationTests.cs`, `tests/FlowTime.Api.Tests/Legacy/ApiIntegrationTests.cs`, `tests/FlowTime.Api.Tests/StateEndpointTests.cs`, `tests/FlowTime.Api.Tests/StateResponseSchemaTests.cs`, `tests/FlowTime.Api.Tests/Golden/metrics-run_metrics_fixture.json` — all exercising retained contracts or internal naming.

## Out of Scope

- Touching or rewriting `MetricsGrid.BinMinutes` or any other E-16-purified analytical contract field.
- Rewriting the Little's Law formula in `whitepaper.md:77`. Math notation for the live derived concept stays.
- Rewriting historical review docs under `docs/architecture/reviews/`.
- Rewriting authoritative migration docs under `docs/schemas/model.schema.md` and `docs/schemas/model.schema.yaml`.
- Retiring Blazor demo mode itself. That is M-027 territory.
- Deleting demo-mode `TemplateServiceImplementations.cs` wholesale. M-026 narrows, M-027 decides demo-mode policy.
- `POST /v1/run` and `POST /v1/graph` and their test fixtures — deferred per D-042.
- Removing the `binMinutes` rejection gate in `ModelValidator` or its covering test. The gate is load-bearing.
- Introducing or documenting `FlowTime.TimeMachine`. That is E-18 m-E18-01a.
- New template files, new examples, or new demo YAML generators.
- Performance, observability, or error-handling improvements unrelated to the schema/template/example retirement.
- CI wiring for `scripts/m-E19-03-grep-guards.sh`. The script exists and runs locally; CI integration is deferred.
- Updating release notes, completed-epic specs, or other historical material under `docs/releases/`, `docs/archive/`, or `work/epics/completed/`.

## Guards / DO NOT

- **DO NOT** touch `MetricsContracts.MetricsGrid.BinMinutes`, `TimeGrid.BinMinutes`, the `ModelValidator` rejection gate, or `docs/schemas/model.schema.md`/`.yaml`. These are retained surfaces and their grep matches are legitimate.
- **DO NOT** rewrite the Little's Law formula on `whitepaper.md:77`. The mathematical notation is not a schema reference.
- **DO NOT** delete `examples/test-old-schema.yaml`, `test-no-schema.yaml`, or `test-new-schema.yaml`. They are archived, not deleted — schema-transition coverage is still useful history.
- **DO NOT** archive or delete `docs/schemas/model.schema.md` or `docs/schemas/model.schema.yaml`. These are authoritative migration docs.
- **DO NOT** retire Blazor demo mode or `CatalogService`-equivalent residue not already covered by M-025. Demo-mode policy is M-027.
- **DO NOT** introduce compatibility shims, `binMinutes`-to-`binSize`/`binUnit` converters, or new helper utilities. Rewrite YAML examples in place; they are static content.
- **DO NOT** add advisory comments pointing at M-027 or at deleted surfaces. Forward-only.
- **DO NOT** leave partially archived directories behind. If a moved file has inbound references, update the references in the same commit (or file a grep-guard failure for the wrap pass to catch).
- **DO NOT** widen the milestone scope to include runtime endpoint changes, Contracts-level refactors, or cross-project deletions. Those are other milestones.
- **DO NOT** commit before explicit human approval per the repo's Hard Rules.

## Dependencies

- [M-024 Supported Surface Inventory, Boundary ADR & Exit Criteria](./M-024.md) — supplies the retention/archive decisions and grep-guard taxonomy this milestone executes.
- [M-025 Sim Authoring & Runtime Boundary Cleanup](./M-025.md) — already removed the runtime seams (catalogs, drafts CRUD, bundle import) whose residue AC7 finishes cleaning up in the docs layer.
- [docs/architecture/supported-surfaces.md](../../../docs/architecture/supported-surfaces.md) — authoritative row-by-row ownership.

## References

- [E-19 epic spec](./spec.md)
- [M-024 spec](./M-024.md)
- [M-025 spec](./M-025.md)
- [work/decisions.md](../../decisions.md) — D-035 (shared framing), D-040 (catalogs retired), D-042 (deferred `/v1/run` `/v1/graph`)
- [scripts/M-025.sh](../../../scripts/M-025.sh) — template for the M-026 grep-guard script
