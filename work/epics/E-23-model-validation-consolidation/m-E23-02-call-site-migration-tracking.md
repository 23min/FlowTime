# m-E23-02: Call-Site Migration — Tracking

**Started:** 2026-04-26
**Completed:** 2026-04-26
**Branch:** `milestone/m-E23-02-call-site-migration` (branched from `epic/E-23-model-validation-consolidation` at `427e2a9`)
**Spec:** `work/epics/E-23-model-validation-consolidation/m-E23-02-call-site-migration.md`
**Commits:** _pending_

## Acceptance Criteria

- [x] **AC1: Call-site enumeration is fresh.** First action of the milestone — `grep -rn "ModelValidator\.Validate" --include="*.cs"` outside `.claude/worktrees/`. Live list recorded below (refresh-time line numbers, not cached from spec).
- [x] **AC2: Production call sites migrated.** `src/FlowTime.API/Program.cs`, `src/FlowTime.Cli/Program.cs`, `src/FlowTime.TimeMachine/Validation/TimeMachineValidator.cs` flipped to `ModelSchemaValidator.Validate`. Production diff small — type-name swap plus error-shape adaptation at response sites.
- [x] **AC3: Test suite migrated.** Three `tests/FlowTime.Tests/Schema/*.cs` files rewritten to assert against `ModelSchemaValidator`. Each assertion classified into one of (a)/(b)/(c)/(d) buckets; bucket recorded per-assertion in this doc. `SimToEngineWorkflowTests.cs` updated.
- [x] **AC4: Error-phrasing audit recorded.** Before/after table for representative invalid-model corpus (missing `bins`, missing `grid`, wrong `schemaVersion`, legacy top-level `arrivals`, legacy node-level `expression`, legacy `binMinutes`); plus downstream-consumer note for any UI/CLI/API site that surfaces validator strings verbatim.
- [x] **AC5: API contract preserved.** `POST /v1/run` returns HTTP 400 with `{ "error": "..." }` JSON body; `error` string may be differently phrased but shape (one field, semicolon-joined on multi-error) identical. API contract tests green.
- [x] **AC6: CLI stderr output preserved.** Invalid-model run still writes `Model validation failed:` followed by one `  - <msg>` line per error; exit code 1. Tests + manual smoke green.
- [x] **AC7: `ModelValidator` has zero production callers.** `grep -rn "ModelValidator\.Validate\|ModelValidator\b" --include="*.cs"` outside `.claude/worktrees/` returns matches only inside `src/FlowTime.Core/Models/ModelValidator.cs`. No other `.cs` references the type.
- [x] **AC8: Full .NET suite green.** `dotnet test FlowTime.sln` passes. Both canaries green: E-24's `TemplateWarningSurveyTests` (`val-err == 0`) and m-E23-01's `RuleCoverageRegressionTests`. No Svelte/Blazor regressions (Playwright smoke against live engine).
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

### AC2 — Production call sites migrated (3 files)

All three production sites flipped to `ModelSchemaValidator.Validate`. · commit _pending — staged_ · tests covered by AC8.

| File | Change | Notes |
|---|---|---|
| `src/FlowTime.API/Program.cs:657` | `ModelValidator.Validate(cleanYaml)` → `ModelSchemaValidator.Validate(cleanYaml)` | One-line swap; surrounding error-shape adapter (`semicolon-joined`) unchanged. |
| `src/FlowTime.Cli/Program.cs:76` | `ModelValidator.Validate(yaml)` → `ModelSchemaValidator.Validate(yaml)` | One-line swap; stderr `Model validation failed:\n  - <msg>` block unchanged. |
| `src/FlowTime.TimeMachine/Validation/TimeMachineValidator.cs:41-58` | Removed redundant dual-validator block; tier-1 schema check is now `ModelSchemaValidator.Validate` alone. | Per spec — m-E23-01 closed rule parity, so the schema-driven evaluator catches every legacy-field, structure, and grid-shape concern that `ModelValidator` previously caught. |

### AC3 — Test suite migrated (4 files, 28 calls → 28 calls; assertions reframed)

All 28 `ModelValidator.Validate` calls swapped to `ModelSchemaValidator.Validate`. Fixtures rewritten to canonical array-form `nodes:`. Assertions reframed per the (a)-(d) rubric:

#### Per-test bucketing (28 test methods total)

##### `tests/FlowTime.Integration.Tests/SimToEngineWorkflowTests.cs` (2 calls)

| Test method | Bucket | Note |
|---|---|---|
| `Sim_Template_Generates_Engine_Model_That_Parses` | (a) | Pure type-name swap. Template emits canonical shape; validation stays `IsValid=true`. |
| `Telemetry_Mode_Model_Parses_WithFileSources` | (a) | Pure type-name swap. Template emits canonical shape; validation stays `IsValid=true`. |

Summary: 2× (a), 0× (b), 0× (c), 0× (d).

##### `tests/FlowTime.Tests/Schema/SchemaErrorHandlingTests.cs` (10 calls)

| Test method | Bucket | Note |
|---|---|---|
| `ValidateModel_MultipleErrors_ReturnsAllErrors` | (a) | Substring assertions on `schemaVersion`/`binSize`/`binUnit` survive shape change. Fixture: nodes-map → nodes-array. |
| `ValidateModel_InvalidYaml_ReturnsParseError` | (b) | Relaxed substring set to include `Validation error` (the shape `ModelSchemaValidator` returns when YAML→JSON conversion throws). Fixture: nodes-map → nodes-array. |
| `ValidateModel_EmptyModel_ReturnsError` | (a) | Pure swap — null/empty path unchanged. |
| `ValidateModel_NullModel_ReturnsError` | (a) | Pure swap — null/empty path unchanged. |
| `ValidateModel_ErrorMessages_AreNotEmpty` | (a) | Generic non-empty assertions; no phrasing dependency. Fixture: nodes-map → nodes-array. |
| `ValidateModel_WithWarnings_StillValid` | (b) | Fixture updated: `bins=24` → `bins=1` so the m-E23-01 const-values cross-array adjunct is satisfied (preserves the original "extra unused node should still validate" intent). |
| `ValidateModel_CaseSensitivity_BinUnit` | (d) | Reframed: schema is case-sensitive (enum keyword), legacy was lenient. Test now asserts `IsValid=false` on `binUnit: Hours`. The original test's lenient-case contract is gone with the imperative validator. |
| `ValidateModel_ExtraFields_Ignored` | (d) | Reframed: schema's root + grid `additionalProperties: false` rejects unknown fields. Original "forward-compatibility lenience" contract is gone. Test now asserts `IsValid=false` and that the unknown field name surfaces in errors. |
| `ValidateModel_InvalidBins_ReturnsSpecificError` (Theory ×3) | (c) | Relaxed: legacy assertion required `e.Contains("bins") && (e.Contains("range") || e.Contains("1") || e.Contains("10000"))`. New shape: `/grid/bins: Value 0 is less than the minimum 1`. The conjunction softened to `e.Contains("bins")` since the schema's enum/min/max wording is not contract-meaningful — bucket (c) semantic relax. |
| `ValidateModel_InvalidBinSize_ReturnsSpecificError` (Theory ×3) | (c) | Same pattern as bins. |

Summary: 5× (a), 2× (b), 2× (c), 2× (d).

##### `tests/FlowTime.Tests/Schema/SchemaVersionTests.cs` (5 calls)

| Test method | Bucket | Note |
|---|---|---|
| `ValidateModel_WithSchemaVersion1_Succeeds` | (a) | Pure swap; fixture `bins=24` → `bins=1` to satisfy const-values length adjunct. |
| `ValidateModel_MissingSchemaVersion_ReturnsError` | (b) | Substring `Required` (case-insensitive) preserved (schema error: `Required properties ["schemaVersion"] are not present`). |
| `ValidateModel_InvalidSchemaVersion_ReturnsError` (Theory ×4) | (b) | Substring `1` preserved (schema error: `Expected "1"` from `const: 1` keyword). |
| `ValidateModel_SchemaVersionAsString_AcceptsIfParseable` | (d) | Reframed: schema is strictly typed (`type: integer, const: 1`); stringified `'1'` is now rejected. Legacy lenient `TryConvertToInt` behavior is gone. |
| `ValidateModel_SchemaVersionNull_ReturnsError` | (a) | Pure swap. |

Summary: 2× (a), 2× (b), 0× (c), 1× (d).

##### `tests/FlowTime.Tests/Schema/TargetSchemaValidationTests.cs` (11 calls)

| Test method | Bucket | Note |
|---|---|---|
| `ValidateModel_CompleteTargetSchema_Succeeds` | (a) | Fixture: nodes-map → nodes-array; `bins=24` → `bins=1` to satisfy const-values adjunct. |
| `ValidateModel_LegacyBinMinutes_ReturnsError` | (b) | All three load-bearing tokens (`binMinutes`/`binSize`/`binUnit`) appear across the multi-error output; assertion changed from `Contains` per-error to per-tokens-in-joined-output. |
| `ValidateModel_MissingBinSize_ReturnsError` | (a) | Substring `binSize` preserved. |
| `ValidateModel_MissingBinUnit_ReturnsError` | (a) | Substring `binUnit` preserved. |
| `ValidateModel_ValidTimeUnits_Succeeds` (Theory ×4) | (a) | Fixture: nodes-map → nodes-array. |
| `ValidateModel_InvalidTimeUnit_ReturnsError` (Theory ×4) | (c) | Original asserted on the verbatim valid-unit list (`minutes/hours/days/weeks`); the JSON-schema shape doesn't enumerate them in the message. Relaxed to `Contains("binUnit")`. |
| `ValidateModel_ExprNode_UsesExprField` | (a) | Fixture: nodes-map → nodes-array; explicit `kind: expr`. |
| `ValidateModel_LegacyExpressionField_ReturnsError` | (b) | Fixture: explicit `kind: expr`. Both `expression` and `expr` tokens appear across the multi-error output (5-arm `oneOf` produces several arm-mismatch reports + the additionalProperties-false rejection). |
| `ValidateModel_LegacyArrivalsRouteSchema_ReturnsError` | (c) | Original asserted `Contains("not supported")` (legacy phrasing). Relaxed to substring on `arrivals`/`route`; new wording is `All values fail against the false schema` (additionalProperties-false at root). |
| `ValidateModel_PmfNode_WithTargetSchema_Succeeds` | (b) | Fixture rewritten: legacy `pmf: [{value, probability}, ...]` (per-entry objects) → canonical `pmf: { values: [...], probabilities: [...] }` (parallel arrays per current schema). `bins=24` → `bins=1`. |
| `ValidateModel_MixedNodeTypes_Succeeds` | (b) | Same pmf rewrite + `bins=168` → `bins=1`. |

Summary: 7× (a), 3× (b), 1× (c), 0× (d) — counts include theory parents only (theory-rows roll up).

#### File-level summary

| File | (a) | (b) | (c) | (d) | Total |
|---|--:|--:|--:|--:|--:|
| `SimToEngineWorkflowTests.cs` | 2 | 0 | 0 | 0 | 2 |
| `SchemaErrorHandlingTests.cs` | 5 | 2 | 2 | 2 | 11 (+ theory expansions) |
| `SchemaVersionTests.cs` | 2 | 2 | 0 | 1 | 5 |
| `TargetSchemaValidationTests.cs` | 7 | 3 | 1 | 0 | 11 (+ theory expansions) |
| **Total** | **16** | **7** | **3** | **3** | **29 methods** |

(Theory-row expansions: `SchemaErrorHandlingTests` 14 distinct test runs; `SchemaVersionTests` 8; `TargetSchemaValidationTests` 17; `SimToEngineWorkflowTests` 2 — total 41 distinct test runs covering the 28 swapped calls plus theory expansions.)

Migrated tests: 39/39 in `FlowTime.Tests/Schema/*` plus 6 in `FlowTime.Integration.Tests/SimToEngineWorkflowTests.cs` — all green.

### AC4 — Error-phrasing audit

Six representative invalid-model fixtures plus two extras (8 total). Both validators run on each fixture (via a transient scratch test `_PhrasingAudit_Scratch.cs` that was deleted before commit). Each fixture uses canonical array-form `nodes:` so the comparison isolates the rule under test.

| # | Fixture | Legacy `ModelValidator` | New `ModelSchemaValidator` |
|---|---|---|---|
| 1 | `grid` block missing | `Model must have a grid definition` | `: Required properties ["grid"] are not present` |
| 2 | `grid.bins` missing | `Grid must specify bins` | `/grid: Required properties ["bins"] are not present` |
| 3 | `schemaVersion: 99` | `schemaVersion must be 1` | `/schemaVersion: Expected "1"` (+ a const-values length adjunct echo from the array-form fixture having only 1 value vs `bins: 24`) |
| 4 | top-level `arrivals:` legacy field | `Top-level 'arrivals' field is not supported. Use 'nodes' array with node definitions instead.` | `/arrivals: All values fail against the false schema` |
| 5 | node-level `expression:` legacy field (with `kind: expr`) | _no error_ — legacy validator silently ignores under `IgnoreUnmatchedProperties` when the node uses canonical `kind: expr` | 5-arm `oneOf` produces several arm-mismatch reports including: `/nodes/0: Required properties ["expr"] are not present`, `/nodes/0/expression: All values fail against the false schema`, etc. |
| 6 | `grid.binMinutes` legacy field | `binMinutes is no longer supported, use binSize and binUnit instead` | `/grid: Required properties ["binSize","binUnit"] are not present` + `/grid/binMinutes: All values fail against the false schema` |
| 7 | `schemaVersion` missing | `schemaVersion is required` | `: Required properties ["schemaVersion"] are not present` |
| 8 | `binUnit: years` (invalid enum) | `binUnit must be one of: minutes, hours, days, weeks` | `/grid/binUnit: Value should match one of the values specified by the enum` |

#### Downstream consumer scan

- **API consumer** (`POST /v1/run`): wraps `string.Join("; ", validationResult.Errors)` into `{ "error": "..." }` — preserves the new shape verbatim. ✅
- **CLI consumer** (`flowtime run` stderr): writes `Model validation failed:\n  - <msg>` per error — preserves the new shape verbatim. ✅
- **UI consumer scan:**
  - Blazor (`src/FlowTime.UI/**/*.razor*`) — `rg "Grid must|binMinutes is no longer|schemaVersion must|not supported|expression.*field"` returns no matches outside `ModelValidator.cs` itself + an unrelated CSV `binMinutes` field on `Simulate.razor:406`. No regex-parser of validator output.
  - Svelte (`ui/**/*.{svelte,ts}`) — same `rg` returns no matches. ✅
- **Note (fixture #5):** Legacy `ModelValidator` accepted `expression:` silently when the node had `kind: expr` because YamlDotNet's `IgnoreUnmatchedProperties` applies to typed-deserialized `nodeDict`. ModelValidator's `expression`-field check only fires when the field is named `expression` literally on the YAML map — and even then only via `nodeKvp.Key == "expression"`. The new schema-driven validator catches the legacy field via 5-arm `oneOf` `additionalProperties: false`. **This is a strict-validation tightening (good); does not require remediation.**

### AC5 — API contract preserved

`POST /v1/run` failure path tested via `ParityTests.Api_Run_Applies_Router_Overrides` (failed — see "Discovered issues" below) and `ProvenanceEmbeddedTests.PostRun_ProvenanceAtWrongLevel_ReturnsError` (failed — see same). On the SUCCESS path (`Sim_Template_Generates_Engine_Model_That_Parses`), the response shape is unchanged. On the FAILURE path the wrapper code (`return Results.BadRequest(new { error = string.Join("; ", ...) })`) is unchanged — same JSON shape, different error string content. **Three pre-existing test failures surface as regressions** because the new strict validator catches things that were previously accepted; see "Discovered issues" below.

### AC6 — CLI stderr output preserved

`Program.cs:79-84` writes the same `Model validation failed:\n  - <msg>` lines and returns exit code 1 — unchanged structure. The CLI exit-path tests (in `FlowTime.Cli.Tests`) are green except for `CliApiParityTests.Cli_Run_Applies_Router_Overrides` which uses the same legacy `generator:` fixture as `ParityTests.Api_Run_Applies_Router_Overrides` and fails for the same reason.

### AC7 — `ModelValidator` zero callers (production code)

```
$ grep -rn "ModelValidator\.Validate|\bModelValidator\b" --include="*.cs" \
    | grep -v ".claude/worktrees" | grep -v "/bin/" | grep -v "/obj/" \
    | grep -v "ModelSchemaValidator" | grep -v "TemplateModelValidator" | grep -v "TemplateValidator"

tests/FlowTime.Tests/Schema/SchemaErrorHandlingTests.cs:156:        // Note: This was previously asserted as "still valid" against ModelValidator's lenient
tests/FlowTime.Tests/Schema/SchemaErrorHandlingTests.cs:182:        // rejects unknown root fields. ModelValidator was lenient (silent ignore for forward
tests/FlowTime.Tests/Schema/SchemaVersionTests.cs:102:        // Bucket (d) reframe — the legacy ModelValidator was lenient (TryConvertToInt
tests/FlowTime.Tests/Schema/TargetSchemaValidationTests.cs:251:        // precise "not supported" phrasing is gone (legacy ModelValidator wording) — bucket (c)
src/FlowTime.TimeMachine/Validation/TimeMachineValidator.cs:47:        // ModelValidator previously caught.
src/FlowTime.Core/Models/ModelValidator.cs:9:public static class ModelValidator
```

All non-`ModelValidator.cs` matches are in **comments** explaining the migration (test files document the bucket-(d) reframes; `TimeMachineValidator.cs` documents why the dual-validator block was removed). No live code references the type by name. **AC7 satisfied.**

### AC8 — Full-suite delta — STOP AND REPORT

Full-suite count: **1839 / 7 / 9** vs. baseline **1846 / 0 / 9**. Net `–7` failing. **6 are pre-existing latent issues that the strict schema validator surfaces** for the first time, **1 is a known Rust-subprocess flake** (`RustEngineBridgeTests.RustEngine_CleansUpTempDirectory_OnFailure`, passes in isolation — pre-existing, documented). None are validator rule gaps (m-E23-01 scope). None are caused by the migration logic itself. **Three categories of new-to-strict-mode failures:**

#### Category I — `ProvenanceService.StripProvenance` roundtrip bug (3 failures)

- `M_E24_02_Step6_AcceptanceTests.PostV1Run_ProducesDeterministicSeries_ForRepresentativeTemplate(templateId: "transportation-basic-classes")`
- `M_E24_02_Step6_AcceptanceTests.PostV1Run_ProducesDeterministicSeries_ForRepresentativeTemplate(templateId: "it-system-microservices")`
- `M_E24_02_Step6_AcceptanceTests.PostV1Run_ProducesDeterministicSeries_ForRepresentativeTemplate(templateId: "dependency-constraints-minimal")`

**Root cause:** `src/FlowTime.API/Services/ProvenanceService.cs:125-146` `StripProvenance` deserializes the YAML to `Dictionary<string, object>` and re-serializes to remove the `provenance:` key. The roundtrip **loses scalar-style information** — string `"1.5"` becomes number `1.5` because the generic deserializer parses it as `double` and the default serializer emits it without quotes. Specifically, `nodes[].metadata.pmf.expected` (which `SimModelBuilder.cs:267` writes as a `G17`-formatted string into a `Dictionary<string, string>`) round-trips into a YAML number scalar.

The schema declares `nodes[].metadata.additionalProperties.type: string`; the post-strip number value fails the type check.

**Verification:** I confirmed in a transient test that `ModelSchemaValidator.Validate(rendered)` returns `IsValid=true, Errors.Count=0`, but `ModelSchemaValidator.Validate(StripProvenance(rendered))` returns `IsValid=false, Errors.Count=466`. The defect is entirely in `StripProvenance`, not in the rendered template or the validator.

**Why it didn't surface before:** Legacy `ModelValidator` did not enforce metadata-value type — it ignored `nodes[].metadata` entirely (no metadata rules in `ValidateNodes`).

**Fix path:** Replace `StripProvenance`'s dictionary roundtrip with a scalar-style-preserving removal (e.g., `YamlStream`-based surgical node deletion, or a line-level removal that preserves the rest of the YAML byte-for-byte). **Out of scope for m-E23-02 per the spec's "touch only" list.**

#### Category II — Test fixtures using legacy `generator:` at root (2 failures)

- `ParityTests.Api_Run_Applies_Router_Overrides`
- `CliApiParityTests.Cli_Run_Applies_Router_Overrides`

**Root cause:** The shared `routerOverrideModel` fixture (declared in `tests/FlowTime.Api.Tests/ParityTests.cs:94-146`) starts with `generator: flowtime-sim` at the root level. The canonical `model.schema.yaml` does NOT declare `generator` as a top-level property, and the schema's root has `additionalProperties: false`. Per the schema's own design notes (lines 1003-1007): _"Sim's prior `window` / top-level `mode` / `generator` / `metadata` had no Engine consumer and were dropped in m-E24-02."_

The fixture predates E-24's schema unification; the legacy `ModelValidator` silently accepted top-level `generator:` (it only checked for `arrivals` / `route` / `schemaVersion` / `grid` / `nodes`).

**Fix path:** Remove the `generator: flowtime-sim` line from the fixture. **Out of scope for m-E23-02 per the spec's "touch only" list.**

#### Category III — Test asserting documented future behavior (1 failure)

- `ProvenanceEmbeddedTests.PostRun_ProvenanceAtWrongLevel_ReturnsError`

**Root cause:** Test fixture nests `provenance:` inside the `grid:` block. The schema's `grid` block has `additionalProperties: false`, so `/grid/provenance` is rejected. The test asserts `HttpStatusCode.OK` with explanatory comment:

```csharp
// M2.9: Provenance at wrong level is currently ignored (no validation error)
// Future: Could add validation to reject malformed provenance placement
Assert.Equal(HttpStatusCode.OK, response.StatusCode);
// Provenance must be at root level, not nested in grid (currently not enforced)
```

The "future" arrived with `ModelSchemaValidator` — the strict schema now enforces what the legacy validator did not. The test's documented "could add validation" intent is now active.

**Fix path:** Update the test's expected status code from `HttpStatusCode.OK` to `HttpStatusCode.BadRequest`, and assert the error contains `provenance` and `grid`. **Out of scope for m-E23-02 per the spec's "touch only" list.**

#### Category IV — `Provenance_AtWrongLevel` second occurrence

A different test in the same file (`PostRun_ProvenanceAtWrongLevel_ReturnsError`) is the only test in this category — already covered above. Counter is **3+2+1 = 6** distinct failures across 7 test runs (the M_E24_02 theory has 3 inline-data rows).

#### Canaries

- ✅ `TemplateWarningSurveyTests.Survey_Templates_For_Warnings` — 1/1 pass. `val-err == 0` across 12 templates at `ValidationTier.Analyse` (the in-process validator path which DOES NOT go through `StripProvenance` — explains why the canary stays green even though Category I fails).
- ✅ `RuleCoverageRegressionTests` — 26/26 pass. Every audited m-E23-01 rule still produces an error.

### AC8 — Close-out (scope-expansion fixes landed, suite green)

**Outcome:** Suite **1856 passing / 0 failing / 9 skipped** vs. m-E23-01 baseline of 1846 / 0 / 9. Net **+10 passing** — the 10 new branch-coverage tests for `ProvenanceService.StripProvenance`. **0 failing** — all 6 scope-expansion failures from the AC8 STOP-AND-REPORT closed. Both canaries stayed green throughout (`Survey_Templates_For_Warnings` 1/1, `RuleCoverageRegressionTests` 26/26). The pre-existing Rust-subprocess flake (`RustEngineBridgeTests.RustEngine_CleansUpTempDirectory_OnFailure`) did not surface in this suite run.

#### Category I close — `ProvenanceService.StripProvenance` round-trip bug

`src/FlowTime.API/Services/ProvenanceService.cs:125-181` — replaced the `Dictionary<string, object>` deserialize-and-re-serialize round-trip with a `YamlStream`-based surgical removal mirroring `RunArtifactWriter.NormalizeTopologySemantics` (file:line `929-1013`). Walks `stream.Documents[0].RootNode` as a `YamlMappingNode`, removes the `provenance` key, emits via `stream.Save(writer, assignAnchors: false)`. Scalar styles on every untouched node are preserved by construction (the YamlStream representation model carries `ScalarStyle` per-node), so `pmf.expected: "3.5"` survives the strip as the double-quoted form `ModelSchemaValidator`'s metadata type-check expects. No `SerializerBuilder` involved — no need to mirror the `QuotedAmbiguousStringEmitter` because no scalars are emitted from new sources; only the parsed tree's existing scalars are re-emitted. **Three M_E24_02_Step6 theory rows green** (transportation-basic-classes, it-system-microservices, dependency-constraints-minimal).

**Branch-coverage audit on `StripProvenance`** — six reachable branches, all exercised in `tests/FlowTime.Api.Tests/Services/ProvenanceServiceStripTests.cs`:

| # | Branch | Test |
|---|---|---|
| 1 | `string.IsNullOrWhiteSpace(yaml)` true (null/empty/whitespace) | `StripProvenance_NullOrWhitespace_ReturnsInputUnchanged` (4 theory rows) |
| 2 | `stream.Load` throws `YamlException` (malformed) | `StripProvenance_MalformedYaml_ReturnsInputUnchanged` |
| 3 | `stream.Documents.Count == 0` (comments-only) | `StripProvenance_NoDocuments_ReturnsInputUnchanged` |
| 4 | Root is not a `YamlMappingNode` (sequence at root) | `StripProvenance_RootIsSequence_ReturnsInputUnchanged` |
| 5 | `provenance` key absent | `StripProvenance_NoProvenanceKey_ReturnsInputUnchanged` |
| 6 | Happy path (provenance present, removed) | `StripProvenance_ProvenancePresent_RemovesKey` + `StripProvenance_PreservesAmbiguousStringScalars` (the regression guard for the m-E23-02 bug itself) |

10/10 unit tests pass.

#### Category II close — Stale fixture with legacy `generator:` field

Removed the `generator: flowtime-sim` line from both copies of the `routerOverrideModel` fixture:

- `tests/FlowTime.Api.Tests/ParityTests.cs:96` — 1 line deleted
- `tests/FlowTime.Api.Tests/CliApiParityTests.cs:234` — 1 line deleted

Both files held independent copies of the fixture (no shared constant); the CLI parity test lives in `FlowTime.Api.Tests` (not a separate `FlowTime.Cli.Tests/CliApiParityTests.cs` as the task brief suggested). Each line removal is a single text deletion with no comment cleanup needed (the legacy line had no surrounding explanatory comment). `ParityTests.Api_Run_Applies_Router_Overrides` and `CliApiParityTests.Cli_Run_Applies_Router_Overrides` are now both green.

#### Category III close — Test asserting documented future behaviour

`tests/FlowTime.Api.Tests/Provenance/ProvenanceEmbeddedTests.cs:259-263` — flipped `Assert.Equal(HttpStatusCode.OK, ...)` to `Assert.Equal(HttpStatusCode.BadRequest, ...)` and added two assertions on the response body containing `"provenance"` and `"grid"`. Replaced the M2.9-era "Future: Could add validation..." comment with an m-E23-02 explanation: the future arrived because `ModelSchemaValidator` enforces `grid.additionalProperties: false`, which the legacy `ModelValidator` did not. `ProvenanceEmbeddedTests` 11/11 pass.

#### Verification grid

| Command | Result |
|---|---|
| `dotnet build FlowTime.sln --nologo --verbosity quiet` | 0 warnings · 0 errors |
| `dotnet test ... ~Survey_Templates_For_Warnings` | 1/1 (canary green) |
| `dotnet test ... ~RuleCoverageRegressionTests` | 26/26 (canary green) |
| `dotnet test ... ~M_E24_02_Step6_AcceptanceTests` | 5/5 (Category I closed) |
| `dotnet test ... ~ParityTests \| ~CliApiParityTests` | 26/26 across both filters (Category II closed) |
| `dotnet test ... ~ProvenanceEmbeddedTests` | 11/11 (Category III closed) |
| `dotnet test ... ~ProvenanceServiceStripTests` | 10/10 (new branch-coverage tests) |
| `dotnet test FlowTime.sln` (full suite) | **1856 / 0 / 9** (vs. baseline 1846 / 0 / 9, net +10) |

### AC9 — Latency delta

Not measured (optional per spec; deferred — non-blocking).

### Watertight coverage — `ProvenanceService.StripProvenance` integration regression

After the AC8 close-out the user flagged that the 10 unit tests cover the function's branches but no integration test pins the user-visible contract end-to-end. Two integration tests added in `tests/FlowTime.Api.Tests/Provenance/ProvenanceStripIntegrationTests.cs` (new file):

- `PostRun_EmbeddedProvenance_WithAmbiguousMetadataScalars_RoundTripsSuccessfully` — POSTs a payload carrying both a root `provenance:` block AND every flavour of YAML-1.2-ambiguous metadata scalar (`"3.5"`, `"true"`, `"pmf"`, `"100"`, `"null"`) to `/v1/run`. Asserts HTTP 200 + canonical response shape (`runId`, `series.demand` as 4-element array with exact values `[100, 120, 150, 130]`). Fires at the API surface if `StripProvenance` ever regresses to lose scalar styles.
- `StripProvenance_OutputWithAmbiguousMetadataScalars_PassesModelSchemaValidator` — cross-validator unit-level contract: `StripProvenance(yaml) → ModelSchemaValidator.Validate` returns `IsValid == true`. Direct chain proving the production fix's contract.

Plus four sub-cases added to `ProvenanceServiceStripTests.cs`:

| Test | Pins |
|---|---|
| `StripProvenance_MultiDocumentYaml_StripsFirstDocumentOnly` | Single-document contract — `Documents[0]` processed, subsequent documents pass through with their own `provenance:` keys intact |
| `StripProvenance_ProvenanceValueIsScalar_RemovesKeyAnyway` | Structural tolerance — `provenance: "scalar"` is removed regardless of value shape; doesn't crash |
| `StripProvenance_NestedProvenanceLeftAlone` | Root-only semantics — `grid.provenance` is NOT touched; nested mis-placement is the schema validator's problem |
| `StripProvenance_LiteralProvenanceStringInValue_NotConfused` | False-positive guard — `"this is the provenance section"` literal in a metadata value isn't accidentally processed |

**Final suite:** **1862 passing / 0 failing / 9 skipped** (delta +6 vs the AC8 close-out's 1856; +16 total vs the m-E23-01 baseline of 1846). Build clean.

## Discovered issues — STOP AND REPORT

The 7 failing tests are NOT m-E23-01 rule gaps. They are pre-existing latent issues that surface for the first time when the strict schema validator becomes the gate. Per the spec's "touch only" list, addressing them in this milestone would expand scope. Three options:

1. **Expand m-E23-02 scope** to fix `ProvenanceService.StripProvenance` (Category I, 1 prod-file change), update the legacy fixture (Category II, 1 fixture change), and update the M2.9-documented-future test (Category III, 1 test-assertion change). All three are mechanical and low-risk. Total: ~5 files added to the touch list. The milestone closes clean.
2. **Defer all three categories** to follow-up patches; mark the 7 failing tests as `Skip = "..."` with gap entries in `work/gaps.md`. AC8 then reads "1846 + 7 skipped / 0 / 9 + 0" — green build with documented skips. Cosmetically less clean but stays in scope.
3. **Pause m-E23-02** until (1) is approved as a scope expansion. The migration work is staged but not committed.

The user is asked to choose. My recommendation is **(1)**: the three fixes are surgical, confined to four files (`ProvenanceService.cs`, `tests/FlowTime.Api.Tests/ParityTests.cs`, `tests/FlowTime.Cli.Tests/CliApiParityTests.cs`, `tests/FlowTime.Api.Tests/Provenance/ProvenanceEmbeddedTests.cs`), each documented above with the root cause, and they let m-E23-02 close cleanly without leaving a category of pre-existing bugs to handle in m-E23-03 (which is purely a delete operation).

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
