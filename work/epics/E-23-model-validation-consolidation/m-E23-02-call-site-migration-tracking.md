# m-E23-02: Call-Site Migration — Tracking

**Started:** 2026-04-26
**Completed:** pending
**Branch:** `milestone/m-E23-02-call-site-migration` (branched from `epic/E-23-model-validation-consolidation` at `427e2a9`)
**Spec:** `work/epics/E-23-model-validation-consolidation/m-E23-02-call-site-migration.md`
**Commits:** _pending_

## Acceptance Criteria

- [ ] **AC1: Call-site enumeration is fresh.** First action of the milestone — `grep -rn "ModelValidator\.Validate" --include="*.cs"` outside `.claude/worktrees/`. Live list recorded below (refresh-time line numbers, not cached from spec).
- [ ] **AC2: Production call sites migrated.** `src/FlowTime.API/Program.cs`, `src/FlowTime.Cli/Program.cs`, `src/FlowTime.TimeMachine/Validation/TimeMachineValidator.cs` flipped to `ModelSchemaValidator.Validate`. Production diff small — type-name swap plus error-shape adaptation at response sites.
- [ ] **AC3: Test suite migrated.** Three `tests/FlowTime.Tests/Schema/*.cs` files rewritten to assert against `ModelSchemaValidator`. Each assertion classified into one of (a)/(b)/(c)/(d) buckets; bucket recorded per-assertion in this doc. `SimToEngineWorkflowTests.cs` updated.
- [ ] **AC4: Error-phrasing audit recorded.** Before/after table for representative invalid-model corpus (missing `bins`, missing `grid`, wrong `schemaVersion`, legacy top-level `arrivals`, legacy node-level `expression`, legacy `binMinutes`); plus downstream-consumer note for any UI/CLI/API site that surfaces validator strings verbatim.
- [ ] **AC5: API contract preserved.** `POST /v1/run` returns HTTP 400 with `{ "error": "..." }` JSON body; `error` string may be differently phrased but shape (one field, semicolon-joined on multi-error) identical. API contract tests green.
- [ ] **AC6: CLI stderr output preserved.** Invalid-model run still writes `Model validation failed:` followed by one `  - <msg>` line per error; exit code 1. Tests + manual smoke green.
- [ ] **AC7: `ModelValidator` has zero production callers.** `grep -rn "ModelValidator\.Validate\|ModelValidator\b" --include="*.cs"` outside `.claude/worktrees/` returns matches only inside `src/FlowTime.Core/Models/ModelValidator.cs`. No other `.cs` references the type.
- [ ] **AC8: Full .NET suite green.** `dotnet test FlowTime.sln` passes. Both canaries green: E-24's `TemplateWarningSurveyTests` (`val-err == 0`) and m-E23-01's `RuleCoverageRegressionTests`. No Svelte/Blazor regressions (Playwright smoke against live engine).
- [ ] **AC9 (optional): Latency delta recorded.** If feasible: warm-run timing of `POST /v1/run` against a representative template before vs. after the migration. Non-blocking; informational.

## Decisions made during implementation

<!-- Decisions that came up mid-work that were NOT pre-locked in the milestone spec.
     For each: what was decided, why, and a link to a decision record if one was
     opened. If no new decisions arose, say "None — all decisions are pre-locked
     in the milestone spec." -->

- (none yet)

## AC1 — Live call-site enumeration (refreshed at start-milestone, 2026-04-26)

`grep -rn "ModelValidator\.Validate\|ModelValidator\b" --include="*.cs"` (excluding `.claude/worktrees/`, `bin/`, `obj/`, `ModelSchemaValidator`, `TemplateModelValidator`, `TemplateValidator`):

### Production call sites (3 files, 3 calls)

| File | Line | Snippet |
|---|---:|---|
| `src/FlowTime.API/Program.cs` | 657 | `var validationResult = ModelValidator.Validate(cleanYaml);` |
| `src/FlowTime.Cli/Program.cs` | 76 | `var validationResult = ModelValidator.Validate(yaml);` |
| `src/FlowTime.TimeMachine/Validation/TimeMachineValidator.cs` | 50 | `var structResult = ModelValidator.Validate(yaml);` |

### Test call sites (4 files, 28 calls)

| File | Line(s) | Calls |
|---|---|---:|
| `tests/FlowTime.Integration.Tests/SimToEngineWorkflowTests.cs` | 38, 120 | 2 |
| `tests/FlowTime.Tests/Schema/SchemaErrorHandlingTests.cs` | 24, 50, 65, 76, 96, 130, 154, 179, 204, 231 | 10 |
| `tests/FlowTime.Tests/Schema/TargetSchemaValidationTests.cs` | 29, 51, 74, 96, 123, 149, 175, 197, 223, 253, 285 | 11 |
| `tests/FlowTime.Tests/Schema/SchemaVersionTests.cs` | 28, 50, 78, 102, 125 | 5 |

### Type declaration (stays this milestone; deleted in m-E23-03)

| File | Line | Snippet |
|---|---:|---|
| `src/FlowTime.Core/Models/ModelValidator.cs` | 9 | `public static class ModelValidator` |

**Totals:** 3 production call sites + 28 test call sites + 1 type declaration = **31 textual references** (target after migration: 1, the type declaration itself).

**Notes vs. spec:** Matches the spec's file list exactly. The spec's headline count "three production sites and four test files" maps to the live numbers above; per-file call counts (10/11/5/2 across the four test files) were not previously enumerated.

## Work Log

<!-- One entry per AC (preferred). First line: one-line outcome · commit <SHA> · tests <N/M> -->

### AC1 — Live call-site enumeration recorded

Refreshed enumeration captured above. Matches spec on file list; per-file call counts now explicit. · commit _pending_ · tests _no test changes for AC1_

## Reviewer notes (optional)

- The spec puts the rollback contract front-and-centre: `ModelValidator.cs` stays on disk so a one-commit revert returns the runtime path to the imperative validator without losing any m-E23-01 work. Verify the milestone exits with `ModelValidator.cs` byte-identical to its m-E23-01 tip.
- Per ADR-E-23-01, **no forwarding shim**. Direct call-site replacement only. If a test gets a forwarding-style stub instead of a direct swap, flag it.

## Validation

- `dotnet build FlowTime.sln` — green required at every commit.
- `dotnet test FlowTime.sln` — green at milestone close.
- `TemplateWarningSurveyTests.Survey_Templates_For_Warnings` — `val-err == 0` (E-24's hard-asserting canary stays green).
- `RuleCoverageRegressionTests` (m-E23-01) — every audited rule continues to fail validation.
- API contract tests for `POST /v1/run` — HTTP 400 + `{ "error": "..." }` shape preserved.

## Deferrals

<!-- Work observed during this milestone but deliberately not done. Mirror into work/gaps.md. -->

- (filed during work)

## Initial context

This milestone follows m-E23-01 Rule-Coverage Audit (closed 2026-04-26 on the same epic branch). After m-E23-01 the schema and `ModelSchemaValidator`'s 12 named adjuncts together cover every rule the embedment audit identified — proven by the 26-test negative-case canary (`RuleCoverageRegressionTests`). The two validators are now behaviourally compatible on both the success path (E-24 canary) and the failure path (m-E23-01 canary), so call-site migration is safe.

The migration is mostly mechanical (type-name swap), but error phrasing differs:
- `ModelValidator` returns flat strings — e.g. `"Grid must specify bins"`.
- `ModelSchemaValidator` returns JSON-schema-shaped messages — e.g. `/grid/bins: Required properties are missing: [bins]`.

Tests asserting on exact phrasing and any UI/CLI consumer that surfaces error strings verbatim must be audited and updated. Per the spec's constraint, the validator's phrasing is **not** rewritten to match `ModelValidator`'s — consumers update to the JSON-schema shape.

`ModelValidator.cs` is **left on disk untouched** at this milestone's close (m-E23-03 is the deletion milestone). After AC7 lands, the only `.cs` file referencing the type is `ModelValidator.cs` itself.

## Tooling note

`wf-graph promote m-E23-02 --to in-progress` was attempted at start-milestone; the CLI returned the generic usage line on the second invocation and on attempts to mark m-E23-01 `complete`. Both flag-order variations probed; no clear cause identified (lockfile present but stale and unowned). Fell back to manual spec-frontmatter edit (`status: ready` → `status: in-progress`) per the start-milestone skill's escape hatch. Graph entry for m-E23-02 was successfully flipped to `in-progress` on the first call; m-E23-01's graph status is still `in-progress` (should be `complete`) — flagged as graph drift to clean up at next opportunity.
