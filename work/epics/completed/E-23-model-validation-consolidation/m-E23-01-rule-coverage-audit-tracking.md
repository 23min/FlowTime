# m-E23-01: Rule-Coverage Audit — Tracking

**Started:** 2026-04-26
**Completed:** 2026-04-26
**Branch:** `milestone/m-E23-01-rule-coverage-audit` (branched from `epic/E-23-model-validation-consolidation` at `9cae437`)
**Spec:** `work/epics/E-23-model-validation-consolidation/m-E23-01-rule-coverage-audit.md`
**Commits:**
- `b3efda0` — feat(validation): close ModelSchemaValidator silent-error blind spot + restructure node-kind clusters (m-E23-01 part 1) — Fix 1 + Fix 2 + schema restructure + AC3
- `d42f649` — feat(validation): add 12 cross-reference/cross-array adjuncts to ModelSchemaValidator (m-E23-01 AC4)
- `<this-commit>` — docs(m-E23-01): close audit doc — disposition flips, AC4 work-log, investigation status

## Acceptance Criteria

- [x] **AC1: Full embedment inventory.** Machine-reviewable table enumerating every rule encoded in (a) `ModelValidator.cs`, (b) `ModelParser.cs`, (c) `SimModelBuilder.cs`, (d) `RunOrchestrationService.cs` + `GraphService.cs`. Each entry: file:line range, rule in plain English, current `model.schema.yaml` expression form (if any).
- [x] **AC2: Per-rule disposition.** Every rule classified as exactly one of: schema-covered, schema-add, adjunct, parser-justified, drop. Each disposition cited (line numbers / rationale / "no live consumer" note).
- [x] **AC3: Schema additions land.** Every `schema-add` rule declared in `docs/schemas/model.schema.yaml` with a `# rule from ModelValidator.cs:N — added m-E23-01` citation comment.
- [x] **AC4: Adjunct additions land.** Every `adjunct` rule implemented as a named method on `ModelSchemaValidator` (sibling to `ValidateClassReferences`), invoked from `Validate`, with at least one negative-case unit test.
- [x] **AC5: Coverage canary stays green.** `TemplateWarningSurveyTests.Survey_Templates_For_Warnings` continues to report `val-err == 0` across all twelve templates at `ValidationTier.Analyse` after schema/adjunct additions.
- [x] **AC6: Negative-case canary.** New `tests/FlowTime.Core.Tests/Schema/RuleCoverageRegressionTests.cs` (or analogous location) feeds a deliberately-invalid model snippet for each non-trivial rule; asserts `ModelSchemaValidator.Validate(...).IsValid == false` plus error-substring containment.
- [x] **AC7: No call-site migration in this milestone.** `grep -rn "ModelValidator\.Validate" --include="*.cs"` returns the same call sites at end-of-milestone as at start. `ModelValidator.cs` unchanged.
- [x] **AC8: No behaviour regression.** `dotnet test FlowTime.sln` green; UI suites untouched.
- [x] **AC9: Audit doc committed.** This tracking doc contains the full embedment table, per-rule disposition, schema-edit / adjunct-edit citations, and the negative-case test catalogue. Reviewable at PR time without running any tool.

## Decisions made during implementation

<!-- Decisions that came up mid-work that were NOT pre-locked in the milestone spec.
     For each: what was decided, why, and a link to a decision record if one was
     opened. If no new decisions arose, say "None — all decisions are pre-locked
     in the milestone spec." -->

- (none yet)

## Work Log

<!-- One entry per AC (preferred). First line: one-line outcome · commit <SHA> · tests <N/M> -->

### Fix 1 — Validator silent-error fallback (`ModelSchemaValidator.CollectErrors`)

Applied by builder agent `a838e5dc28e7e1516`. **+80 lines** on `src/FlowTime.Core/Models/ModelSchemaValidator.cs` + new test file `tests/FlowTime.Core.Tests/Schema/ModelSchemaValidatorSilentErrorRegressionTests.cs` (208 lines, 6 tests, all green).

The synthesizer adds `SynthesizePathOnlyError`, `WalkForDeepestInvalid`, and `IsDeeperThan` helpers. The call-site guard inside `Validate(...)` triggers the synthesizer when `evaluation.IsValid == false` AND `CollectErrors` yielded zero strings. Path-only message format: `<InstanceLocation>: schema rule failed at <EvaluationPath>`. Two `internal` test entry points (`CollectErrorsForTests`, `SynthesizePathOnlyErrorForTests`) expose the helpers via the existing `InternalsVisibleTo("FlowTime.Core.Tests")`.

Regression test uses an in-line synthetic schema with a `not` keyword (durable; doesn't depend on production schema evolution). Confirms: `IsValid == false` + `CollectErrors` yields zero + synthesizer produces a non-empty path-only diagnostic. Six tests cover sanity, silent-error trigger, message format, idempotency, end-to-end validator invariant, and walker tiebreaker determinism.

**Caveat documented in the test:** `not`-keyword failures evaluate the inner schema as `IsValid == true` (the inner schema matched; that's exactly what makes the parent `not` keyword fail). So `EvaluationPath` lands at the parent `items` schema, not at the `not` keyword itself. For top-level `not` failures the message degrades to `: schema rule failed at ` (both pointers are document root) — known semantic limit of path-only diagnostics, not a bug.

### Schema restructure — kind clusters → node-level `oneOf`

Applied by builder agent `a61fdd7f3fa669022`. **+119 / −68 lines** on `docs/schemas/model.schema.yaml` (lines 676-763 replaced).

Five-arm `oneOf` at `nodes[].items` level enumerating all kinds (`const`, `expr`, `pmf`, `router`, `serviceWithBuffer`). Each arm uses `additionalProperties: false` with explicit per-arm allow-list to forbid sibling-kind fields — cleaner than `not`-based forbidding, and crucially, **structurally avoids the JsonEverything `not`-keyword silent-error class**.

Per-arm shape:

| Arm | `kind` | Required | Forbidden via `additionalProperties: false` | Inline arm constraints |
|---|---|---|---|---|
| `const` | `const` | `values` | `expr`, `pmf`, `inputs`, `routes`, `inflow`, `outflow`, `loss` | — |
| `expr` | `expr` | `expr` | `values`, `pmf`, `inputs`, `routes`, `inflow`, `outflow`, `loss` | — |
| `pmf` | `pmf` | `pmf` | `values`, `expr`, `inputs`, `routes`, `inflow`, `outflow`, `loss` | — |
| `router` | `router` | `inputs`, `routes` | `values`, `expr`, `pmf`, `inflow`, `outflow`, `loss` | `inputs.required: [queue]`; `routes.items.anyOf: [{required:[classes]},{required:[weight]}]` |
| `serviceWithBuffer` | `serviceWithBuffer` | `inflow`, `outflow` | `values`, `expr`, `pmf`, `inputs`, `routes` | `loss` allowed, optional |

All five arms allow `id`, `kind`, `metadata`, `dispatchSchedule` (so the existing per-property AC3 schema-add edits — `minItems`, `minLength`, defaults — continue to apply at the outer `properties` level).

Negative-case smoke verified: feeding `kind: const` + top-level `pmf:` (the now-impossible-by-emission shape Fix 2 removed) produces `IsValid = False`, 16 errors — headline `/nodes/0: Expected 1 matching subschema but found 0`, then `/nodes/0/pmf: All values fail against the false schema`. **Surfaced cleanly without needing Fix 1's fallback** — `oneOf`-no-arm-match and `additionalProperties: false` are both well-reported keywords in JsonEverything.

### Fix 2 — Emitter: `BuildProfiledConstNode` drops top-level `pmf:` block

Applied by builder agent `af32580e3cf20f45b`. **−7 lines** on `src/FlowTime.Sim.Core/Templates/SimModelBuilder.cs` + 2 test assertions flipped in `tests/FlowTime.Sim.Tests/NodeBased/ModelGenerationTests.cs:211-213` and `:273-275` (`Assert.NotNull(profiledNode.Pmf)` → `Assert.Null(profiledNode.Pmf)`).

The forensic record (`metadata.origin.kind`, `metadata.profile.kind`, `metadata.profile.name`, `metadata.pmf.expected`) is preserved as before. The duplicated source `pmf.values` / `pmf.probabilities` were leftover after a prior metadata-pattern refactor and contributed nothing the metadata didn't.

Wire-shape verified on `templates/transportation-basic.yaml` post-fix: zero top-level `pmf:` blocks under any `kind: const` node; metadata block carries the four canonical forensic keys exactly as expected.

### Stale-fixture follow-up gap (post-Fix-2 / post-restructure)

`tests/FlowTime.Api.Tests/GraphEndpointTests.cs:131-154` (`GetGraph_FullMode_EmitsProfiledPmfNodes`) hand-authors a model.yaml fixture with `kind: const` + a top-level `pmf:` block. After Fix 2 no Sim run produces this shape; after the schema restructure the shape would fail validation. The Engine's permissive YAML reader (`ModelService.ConvertToModelDefinition`) still accepts it via `IgnoreUnmatchedProperties()`, so the test continues to pass. **Pre-existing test fixture exercising a no-longer-realistic input.** File a `work/gaps.md` entry; absorb in m-E23-02 or a small standalone patch.

### Remaining flake (pre-existing, unrelated to m-E23-01)

`FlowTime.Integration.Tests.SessionModelEvaluatorIntegrationTests.Dispose_TerminatesSubprocess` — Rust subprocess teardown timing. **Passes in isolation** (verified — 1/1 pass, 630 ms). Intermittent in full-suite runs. Same family as `RustEngineBridgeTests.RustEngine_CleansUpTempDirectory_OnFailure`, both well-documented as flaky in the project's history. Untouched by m-E23-01.

### AC3 — Schema-add edits landed (16 rows, +34 lines on `model.schema.yaml`)

Applied by builder agent `ad6c54b5022b04385`. Per-row citations land as `# rule from <file>:<line> — added m-E23-01` comments on the schema lines immediately above each addition. Verification: build green (1 pre-existing xUnit2031 warning), canary green (`Survey_Templates_For_Warnings` reports `val-err == 0` across all 12 templates), full suite **1,814 passed / 0 failed / 9 skipped** (pre-existing infrastructure-gated skips). No commits made; staged for review.

Per-row landing summary (file:line citations point at the schema, not at the original embedment source — see embedment table for original file:line):

- **#30** `topology.nodes[].required: + semantics` (~schema:128)
- **#31/#32** topology-node `semantics.required: [arrivals, served]` (~schema:190)
- **#33** topology-edge `items.required: [from, to]` (~schema:354)
- **#36/#37** constraint `semantics.required: [arrivals, served]` (~schema:432)
- **#46** const-node `values.minItems: 1` (~schema:518)
- **#47** expr-node `expr.minLength: 1` (~schema:527)
- **#54** router `inputs.queue.minLength: 1` (~schema:608)
- **#56** router `routes[].target.minLength: 1` (~schema:628)
- **#57** router `routes[].weight.exclusiveMinimum: 0` (~schema:642)
- **#61** `dispatchSchedule.phaseOffset.default: 0` (~schema:1074)
- **#86** `rng.kind.default: pcg32` (~schema:923)
- **#88** `nodes[].kind.default: const` (~schema:498)
- **#89** `topology.nodes[].kind.default: service` (~schema:140)
- **#94** `topology.edges[].weight.default: 1.0` (~schema:375)

Five originally-tabulated rows dropped per Decisions 1 (#19 binUnit case) and 2 (#3, #4, #10, #20 legacy-field migration messages).

### AC4 — Adjunct method implementations landed (12 adjuncts, +652 lines on `ModelSchemaValidator.cs`)

Applied by builder agent `a6abaa02cb87b8b80` on commit `d42f649`. **+652 lines** on `src/FlowTime.Core/Models/ModelSchemaValidator.cs` + new test file `tests/FlowTime.Core.Tests/Schema/RuleCoverageRegressionTests.cs` (797 lines, 26 tests, all green).

Twelve named static methods sibling to `ValidateClassReferences`, each invoked from `Validate(yaml)` after JSON-schema evaluation, each adding any errors to the running list. Plus shared helpers `CollectNodeIds`, `CollectTopologyInitialIds`, `IsPmfNode`, `TryGetGridBins`, `TryParseIsoDateTime`, and a private `NodeReferenceCollector` AST visitor.

| Adjunct | Tests | Lifted from |
|---|---:|---|
| `ValidateNodeIdUniqueness` | 2 | **gap** — schema notes documented as adjunct, no prior enforcer |
| `ValidateOutputSeriesReferences` | 2 | **gap** — schema notes documented as adjunct |
| `ValidateExpressionNodeReferences` | 2 | **gap** — uses `ExpressionParser` AST walk via `NodeReferenceCollector` |
| `ValidateConstNodeValueCount` | 2 | **gap** — schema notes documented as adjunct |
| `ValidatePmfArrayLengths` | 1 | `ModelParser.cs:420-421` |
| `ValidatePmfValueUniqueness` | 1 | `ModelParser.cs:431-432` |
| `ValidatePmfProbabilitySum` | 2 | `Pmf.Pmf` ctor (`Pmf.cs`) — surfaces from validator path; tolerance ±1e-4 per audit table |
| `ValidateSelfShiftRequiresInitialCondition` | 2 | `ModelParser.cs:307-316` (reuses `ExpressionSemanticValidator`) |
| `ValidateTopologySeriesReferences` | 4 | **gap** — `SemanticReferenceResolver` only owns syntax. Handles `self`, `file:`, `series:`, `@classId` prefixes. |
| `ValidateWipOverflowTarget` | 2 | `ModelCompiler.cs:380-384` |
| `ValidateWipOverflowAcyclic` | 2 | `ModelCompiler.cs:412-420` — path-traversal-from-each-source matching imperative source's shape (single-outbound-edge graph by construction) |
| `ValidateDateTimeFormats` | 4 | `ModelParser.cs:251-252` — covers `grid.start` and `provenance.generatedAt`; `DateTime.TryParse` with `InvariantCulture` + `AdjustToUniversal | AssumeUniversal` |

**Mode-aware integration choice — option (b).** `ValidateSimulationModel` (audit rows #77 / #80) stays at `RunOrchestrationService.ValidateSimulationModel` rather than lifting into `ModelSchemaValidator`. Rationale: `RunOrchestrationService` already operates on `ModelDto` directly and throws `TemplateValidationException` on failure — a different error shape than `ValidationResult.Errors`. Lifting it would require either adding a mode parameter (every existing caller of `Validate(yaml)` must change, conflicting with AC7's "no call-site migration") or duplicating the rules across both layers (the embedment problem E-23 is supposed to eliminate). Both worse than today. Audit-table dispositions for rows #77 / #80 flipped from "adjunct (mode-aware)" → "**parser-justified (mode-specific)** — orchestration-layer enforcement is the canonical home; not portable to the schema-validator path."

**Imperative checks left in place** per the AC4 constraint — `ModelParser.cs`, `ModelCompiler.cs`, `Pmf.Pmf` ctor unchanged. The adjuncts are additive defence-in-depth; m-E23-02 / m-E23-03 will reconcile duplicates if appropriate.

**Branch-coverage audit:** every reachable conditional branch in the 12 adjunct methods has at least one test exercising it. Defensive branches (entry-null after schema gate, `GetValue<double>` throw, `pmf` null when `IsPmfNode` returned true) are documented as defensive-only and not bespoke-tested — same precedent as `ValidateClassReferences`. Full per-method audit captured in the AC4 builder-agent report (preserved in conversation transcript).

**Verification:** `dotnet build FlowTime.sln` green. `dotnet test FlowTime.sln --filter "FullyQualifiedName~RuleCoverageRegressionTests"` 26/26 green. `dotnet test FlowTime.sln --filter "FullyQualifiedName~Survey_Templates_For_Warnings"` green (no shipped template trips a new adjunct). Full suite **1,846 / 0 / 9** — net **+27** vs. the pre-AC4 baseline of 1,819, with one pre-existing Rust-subprocess flake (`SessionModelEvaluatorIntegrationTests.Dispose_TerminatesSubprocess`) that surfaces in some full-suite runs and passes cleanly in isolation.

### D3 — Profiled-PMF emission investigation (verdict + two predecessor fixes)

D3 background agent `a1ab8ae81e8a1d509` returned with **verdict D** — none of the three pre-formulated possibilities. The const+pmf shape *does* reach `ModelSchemaValidator`, the schema's `if kind: const then not pmf` rule *does* fire (`evaluation.IsValid == false`), but `ModelSchemaValidator.CollectErrors` (`src/FlowTime.Core/Models/ModelSchemaValidator.cs:100-122`) silently drops `not`-keyword failures because JsonEverything propagates invalidity *without* populating string `Errors` on any leaf node, and `CollectErrors` only yields strings where `Errors` is non-empty. Net: `errors.Count == 0` despite `IsValid == false`. **The canary is currently green because of this validator bug, not despite it.**

The bug is `not`-keyword-specific: missing-required, type, enum, and pattern violations DO surface correctly. Three live `not` clauses are silently bypassed today (schema:677-713 — `kind: const → not [expr, pmf]`, `kind: expr → not [values, pmf]`, `kind: pmf → not [values, expr]`).

Two predecessor fixes belong in m-E23-01 — they are real coverage-audit findings (schema rules currently not enforced):

- **Fix 1 — validator** (`ModelSchemaValidator.CollectErrors`): when `evaluation.IsValid == false` and no leaf yielded a string, synthesize an error from `(InstanceLocation, EvaluationPath)`. Closes the silent blind spot for `not`, `oneOf`, and any future terse-keyword failure.
- **Fix 2 — emitter** (`SimModelBuilder.BuildProfiledConstNode`, `src/FlowTime.Sim.Core/Templates/SimModelBuilder.cs:225-256`): stop emitting the `pmf` block when `kind: const`. One existing test (`tests/FlowTime.Sim.Tests/NodeBased/ModelGenerationTests.cs:206-213`) currently pins the buggy shape and needs updating.

**Required ordering** (canary preservation): AC3 ✓ → Fix 2 (emitter; wire shape now conforms) → Fix 1 (validator; blind spot closed; test that pre-Fix-2 const+pmf shape would now be caught) → AC4 (11 adjuncts on the fixed validator) → AC5+AC6 close.

m-E23-02 / m-E23-03 remain safe regardless — `ModelValidator` does not redundantly cover the const+pmf rule, so the existing call-site swap and final delete neither regress nor introduce new red.

### AC1 — Embedment inventory (94 rules cataloged)

Read-only audit completed by background `general-purpose` agent. 94 rule-rows across 6 source files. Inventory + per-rule dispositions captured in the [Embedment table](#embedment-table) section below. Cross-reference findings (textbook adjunct candidates JSON Schema draft-07 cannot express) and 8 narrative surprises captured in [Cross-reference findings](#cross-reference-findings) and [Surprises](#surprises).

Files audited:

- `src/FlowTime.Core/Models/ModelValidator.cs` — 24 rules (the canonical embedded validator we are migrating away from).
- `src/FlowTime.Core/Models/ModelParser.cs` — 27 rules (post-parse runtime checks).
- `src/FlowTime.Sim.Core/Templates/SimModelBuilder.cs` — 14 rules (post-substitution emitter).
- `src/FlowTime.TimeMachine/Orchestration/RunOrchestrationService.cs` — 4 rules (sim-mode pre-execute).
- `src/FlowTime.Core/Compiler/ModelCompiler.cs` — 5 rules (post-parse model walk).
- `src/FlowTime.Contracts/Dtos/ModelDtos.cs` + `Services/ModelService.cs` + `Engine/Time/TimeGrid.cs` — 14 DTO defaults / coercion shims / runtime re-checks.

Three production call sites in this repo (`API/Program.cs:657`, `Cli/Program.cs:76`, `TimeMachine/Validation/TimeMachineValidator.cs:50`); 29 test references across 5 files. No external repos reference `ModelValidator` (legacy sibling `flowtime-sim-vnext` was absorbed into this repo and is being retired — to be removed; do not reference further).

## Embedment table

The agent produced row-by-row inventory with file:line, plain-English rule, current schema status, current `ModelSchemaValidator` adjunct status, and likely classification. Main-thread dispositions appended in the **disposition** column. **`@needs-decision` rows are open questions surfaced below in [Open decisions before AC3/AC4](#open-decisions-before-ac3ac4).**

### `ModelValidator.cs` (the imperative validator we are migrating away from)

| # | source file:line | rule | schema? | adjunct? | disposition |
|---|---|---|---|---|---|
| 1 | :11-14 | YAML deserializer tolerates unknown root-level fields (anti-rule for the dynamic-dict pre-pass) | n/a | n/a | **drop** — anti-rule artefact of the pre-pass; schema's root `additionalProperties: false` is the strict counter. Disappears with `ModelValidator.cs`. |
| 2 | :25-28 | Reject null/empty YAML | no | yes (ModelSchemaValidator:25-28 mirrors) | **schema-covered** |
| 3 | :37-40 | Reject top-level `arrivals` field with explicit "use 'nodes' array" hint | partial (root `additionalProperties: false` catches generically) | no | **schema-add @needs-decision-2** — preserve migration message via `not: { required: [arrivals] }` + `description`, or accept generic message |
| 4 | :41-44 | Reject top-level `route` field with explicit hint | partial (same as #3) | no | **schema-add @needs-decision-2** |
| 5 | :47-50 | `schemaVersion` is required | yes (schema:19-22) | no | **schema-covered** |
| 6 | :53-57 | `schemaVersion` must convert to integer | yes (schema:25-26) | no | **schema-covered** |
| 7 | :58-61 | `schemaVersion` must equal `1` | yes (schema:27 const:1) | no | **schema-covered** |
| 8 | :64-67 | Top-level `grid` is required | yes (schema:21) | no | **schema-covered** |
| 9 | :69, 82-83 | `grid` must be an object | yes (schema:35) | no | **schema-covered** |
| 10 | :106-110 | Reject legacy `binMinutes` with migration hint "use binSize and binUnit instead" | partial (additionalProperties catches generically) | no | **schema-add @needs-decision-2** |
| 11 | :113-115 | `grid.bins` required | yes (schema:42) | no | **schema-covered** |
| 12 | :117-120 | `grid.bins` must be integer | yes (schema:47) | no | **schema-covered** |
| 13 | :121-124 | `grid.bins` ∈ [1,10000] | yes (schema:48-49) | no | **schema-covered** (also runtime in `TimeGrid.cs:66-67`) |
| 14 | :127-129 | `grid.binSize` required | yes (schema:42) | no | **schema-covered** |
| 15 | :131-134 | `grid.binSize` integer | yes (schema:60) | no | **schema-covered** |
| 16 | :135-138 | `grid.binSize` ∈ [1,1000] | yes (schema:61-62) | no | **schema-covered** |
| 17 | :141-143 | `grid.binUnit` required | yes (schema:42) | no | **schema-covered** |
| 18 | :145-148 | `grid.binUnit` non-empty string | partial (enum forbids empty) | no | **schema-covered** (the enum rule below makes empty fail anyway) |
| 19 | :152-156 | `grid.binUnit` ∈ {minutes,hours,days,weeks} (case-insensitive in parser; case-sensitive in schema) | yes (schema:74-78 enum) | no | **schema-add @needs-decision-1** — pattern with `(?i)` or accept strict casing |
| 20 | :170-176 | Reject node-level `expression` field with migration hint "use 'expr' instead" | partial (node items `additionalProperties: false`) | no | **schema-add @needs-decision-2** |
| 21 | :91-93 | YAML syntax errors → `"Invalid YAML syntax: ..."` | n/a | yes (mirrors :53-55) | **schema-covered** (mirrored) |
| 22 | :95-97 | Generic catch-all → `"Validation error: ..."` | n/a | yes (mirrors :57-59) | **schema-covered** (mirrored) |
| 23 | :181-198 | `TryConvertToInt` helper (int / long-in-int-range / parseable-string) | n/a | n/a | **drop** — pre-pass-only helper; goes away with `ModelValidator.cs` |
| 24 | :69 (else), :167-168 | Tolerate non-dict keys / values in node map (silent skip) | n/a | n/a | **drop** — anti-rule artefact; schema validates structurally |

### `ModelParser.cs` (post-parse `ModelDefinition` → `Graph`)

| # | source file:line | rule | schema? | adjunct? | disposition |
|---|---|---|---|---|---|
| 25 | :31-32 | Grid not null (post-DTO null guard) | yes (schema:21) | no | **parser-justified** — defence-in-depth post-parse re-check; harmless |
| 26 | :34-36 | `binSize > 0` AND `binUnit` non-empty (combined) | partial (schema:61, enum) | no | **parser-justified** — defence-in-depth |
| 27 | :38 | `binUnit` parses to TimeUnit (case-insensitive) | partial — schema is case-sensitive | no | **@needs-decision-1** (mirror of #19) |
| 28 | :65-66 | Silent skip of empty-ID nodes when building lookup map | n/a | n/a | **drop** — tolerance, not rule; schema enforces non-empty ID |
| 29 | :90-91 | Topology node `id` non-whitespace | yes (schema:127, :131 minLength:1) | no | **parser-justified** — defence-in-depth |
| 30 | :93-94 | Topology node `semantics` block required | partial (schema:175 declares but not required) | no | **schema-add** — add `semantics` to topology-node `required` list |
| 31 | :98-99 | Topology semantics `arrivals` required | partial (schema:187 declares but no required-list on topology semantics) | no | **schema-add** — add `arrivals` to topology semantics `required` |
| 32 | :99 | Topology semantics `served` required | partial (schema:193 declares but not required) | no | **schema-add** — same |
| 33 | :145-146 | Topology edges `from` AND `to` required | partial (schema:344-361 declares but not required) | no | **schema-add** — add `from, to` to topology edge `required` (note parser uses Source/Target naming; schema/DTO uses from/to — confirm wire shape) |
| 34 | :163-165 | Constraint `id` required | yes (schema:401-407) | no | **schema-covered** |
| 35 | :168-170 | Constraint `semantics` required | yes (schema:401-407) | no | **schema-covered** |
| 36 | :178-179 | Constraint semantics `arrivals` required | partial (schema:419 declares but not required) | no | **schema-add** |
| 37 | :179 | Constraint semantics `served` required | partial (schema:425 declares but not required) | no | **schema-add** |
| 38 | :251-252 | `grid.start`, when present, is parseable ISO date-time | partial — `format: date-time` is annotation-only in draft-07 | no | **adjunct** — `ValidateDateTimeFormats` adjunct on `grid.start` (and any other `format: date-time` field). Cleaner than schema `pattern:`. |
| 39 | :262 | C# null-guard on `model` arg | n/a | n/a | **drop** — language-level concern, not a rule |
| 40 | :307-316 | Self-shift expression requires matching `topology.initialCondition.queueDepth` | no — cross-reference between AST and topology | no | **adjunct** — textbook cross-reference; impossible in draft-07 |
| 41 | :336 | `topoNode.InitialCondition.QueueDepth ?? 0` | no | no | **parser-justified** — implicit default mirrored in DTO; consider `default: 0` in schema |
| 42 | :349-350 | `serviceWithBuffer` requires `inflow, outflow` | yes (schema:706-714 if/then) | no | **schema-covered** |
| 43 | :367-368 | Node `id` non-whitespace | yes (schema:457, pattern :460-462) | no | **parser-justified** — defence-in-depth |
| 44 | :370-373 | Node `kind` non-whitespace | yes (schema:457 required, :475-481 enum) | no | **parser-justified** |
| 45 | :378-386 | Node `kind` ∈ {const,expr,pmf,serviceWithBuffer,router} (case-insensitive in parser) | yes (case-sensitive in schema) | no | **@needs-decision-1** |
| 46 | :391-392 | `const` node requires `values` (non-empty array) | partial (`if kind: const then required: [values]`; no `minItems`) | no | **schema-add** — add `minItems: 1` |
| 47 | :399-400 | `expr` node requires `expr` (non-empty string) | partial (no `minLength: 1`) | no | **schema-add** — add `minLength: 1` |
| 48 | :404-411 | `expr` parses via `ExpressionParser` (syntactic) | no | no | **parser-justified** — expression-grammar concern; not a structural rule. Token-existence check is adjunct (#59). |
| 49 | :417-418 | `pmf.values` non-empty | yes (schema:528 required, :535 minItems) | no | **schema-covered** |
| 50 | :420-421 | `pmf.probabilities.length == values.length` | no — cross-array length | no | **adjunct** — textbook |
| 51 | :431-432 | `pmf.values` no duplicates | no — cross-array dedup | no | **adjunct** |
| 52 | :437 | `pmf.probabilities` sum to 1.0 (±1e-4) — enforced inside `Pmf.Pmf` ctor | no | no | **adjunct** — schema notes (line 1223) already document this as adjunct rule; surface the error from validator path, not deep ctor |
| 53 | :454-455 | `serviceWithBuffer` requires `inflow` AND `outflow` (combined message) | yes (mirror of #42) | no | **schema-covered** |
| 54 | :467-470 | `router` requires `inputs.queue` non-whitespace | partial (`if kind: router` block; whitespace not enforced) | no | **schema-add** — add `minLength: 1` (or `pattern: ^\\S`) |
| 55 | :472-475 | `router` requires ≥1 route | yes (schema:594 minItems:1, if/then required:[routes]) | no | **schema-covered** |
| 56 | :479-481 | `router.routes[].target` non-whitespace | partial (`required: [target]`; whitespace not enforced) | no | **schema-add** — `minLength: 1` |
| 57 | :484-486 | Each route has non-empty `classes` array OR positive `weight` | partial (`anyOf: [required:[classes], required:[weight]]`; positive-weight not declared) | no | **schema-add** — add `exclusiveMinimum: 0` to `weight` |
| 58 | :501-505 | `dispatchSchedule.kind` ∈ {time-based} (case-insensitive in parser) | yes (case-sensitive enum) | no | **@needs-decision-1** |
| 59 | :507-510 | `dispatchSchedule.periodBins > 0` | yes (schema:1033-1035 minimum:1) | no | **schema-covered** |
| 60 | :513-516 | `dispatchSchedule.capacitySeries` wrap-in-NodeId (no validation) | n/a | n/a | **drop** — type-wrap, not a rule |
| 61 | :518 | `dispatchSchedule.phaseOffset ?? 0` | partial (no `default:` declared) | no | **schema-add** — declare `default: 0` for documentation; **also parser-justified** (default in DTO) |

### `SimModelBuilder.cs` (post-substitution emitter)

| # | source file:line | rule | schema? | adjunct? | disposition |
|---|---|---|---|---|---|
| 62 | :46-53 | `grid.start` derives from template's `Grid.Start` ?? `Window.Start`; OmitNull when both absent | n/a | n/a | **parser-justified** — emitter contract; `start` optional in schema |
| 63 | :130 | `topology.nodes[].semantics.errors` emitted only when source non-whitespace | n/a | n/a | **parser-justified** — emitter contract |
| 64 | :145-148 | `topology.nodes[].semantics.queueDepth` emitted only when non-whitespace | n/a | n/a | **parser-justified** |
| 65 | :175 | If `classes` declared, every `traffic.arrivals[].classId` must be present | yes (schema:1062-1077 if/then) | yes (`ValidateClassReferences` :124-167) | **schema-covered** (and adjunct — both layers enforce) |
| 66 | :177-180 | Every `arrivals[].classId` referenced exists in `model.classes` | no | yes (`ValidateClassReferences` :163-166) | **adjunct** (already exists; the prior-art) |
| 67 | :188 | If template arrival pattern null → emit `Kind = ""` | partial (schema enum forbids empty) | no | **emitter-fix @surprise** — emitting `""` would fail schema; either unreachable in practice or emitter is broken. Investigate post-AC1. |
| 68 | :206-218, :225-256 | `BuildProfiledConstNode` — emits `kind: "const"` AND retains `pmf` block | n/a | n/a | **emitter-fix @needs-decision-3** — schema's `if kind: const then not: [pmf]` rule rejects this shape; how does this not blow up the canary today? |
| 69 | :227-230 | Profile weight count must equal `grid.bins` (template-time check) | n/a | n/a | **parser-justified** — pre-emission template check, not a post-substitution model rule |
| 70 | :323-325 | `NodeDto.Kind` defaults to `""` if template `Kind` null | partial (enum forbids empty) | no | **emitter-fix** — likely unreachable but verify |
| 71 | :326-329 | `pmf` and `expr` nodes have `Values` forced null on emit | n/a | n/a | **parser-justified** — mirrors schema's not-clauses (`if kind: pmf/expr then not: [values]`) |
| 72 | :338-340 | `inflow/outflow/loss` emitted only for `serviceWithBuffer` | n/a | n/a | **parser-justified** |
| 73 | :342-343 | `inputs/routes` emitted only for `router` | n/a | n/a | **parser-justified** |
| 74 | :344 | `dispatchSchedule` emitted only for `serviceWithBuffer` | n/a | n/a | **parser-justified** |
| 75 | :438-439 | `provenance.generator` defaults to `"{template.Generator}/{assemblyVersion}"` | n/a | n/a | **parser-justified** — emitter contract; provenance shape is opaque to schema |
| 76 | :451-457 | `provenance.modelId` defaults to SHA-256 hex of substituted YAML | n/a | n/a | **parser-justified** — forensic field |

### `RunOrchestrationService.cs`

| # | source file:line | rule | schema? | adjunct? | disposition |
|---|---|---|---|---|---|
| 77 | :838-841 | Simulation: `Grid.Start` non-empty | no — schema `start` is optional | no | **parser-justified (mode-specific)** — orchestration-layer enforcement is the canonical home; schema can't condition on run mode and the validator path correctly does not enforce because telemetry-mode runs legitimately don't need `grid.start`. Disposition flipped from "adjunct (mode-aware)" 2026-04-26 per AC4 mode-aware integration choice (option b). |
| 78 | :843-846 | Simulation: `Grid.Bins > 0` | yes (schema:48 minimum:1) | no | **schema-covered** (also re-checked here as defence-in-depth) |
| 79 | :848-851 | Simulation: `Grid.BinSize > 0` | yes (schema:61 minimum:1) | no | **schema-covered** |
| 80 | :853-856 | Simulation: `Topology.Nodes` non-empty | no — `topology.nodes` has no `minItems` | no | **parser-justified (mode-specific)** — same shape as #77; orchestration-layer enforcement is the canonical home, since telemetry-mode runs legitimately can have an empty topology. Disposition flipped 2026-04-26 per AC4 mode-aware integration choice (option b). |

### `ModelCompiler.cs` (post-parse model walk)

| # | source file:line | rule | schema? | adjunct? | disposition |
|---|---|---|---|---|---|
| 81 | :148-156 | Queue-like topology node (kind ∈ {servicewithbuffer, queue, dlq}) requires `semantics.arrivals` | partial (schema covers `serviceWithBuffer` only) | no | **adjunct** — cross-field rule keyed on topology-node-kind set; not in draft-07's reach |
| 82 | :159-172 | Queue-like topology node requires `semantics.served` OR `semantics.capacity` | no | no | **adjunct** |
| 83 | :380-384 | `wipOverflow` target (when not `"loss"` / default) must reference a topology node with queue-depth series | no — cross-reference | no | **adjunct** |
| 84 | :412-420 | WIP overflow routing graph acyclic | no — graph invariant | no | **adjunct** |
| 85 | :10-15 | Queue-like kinds set `{servicewithbuffer, queue, dlq}` | partial (schema node-kind enum has only `serviceWithBuffer`) | no | **observation** — schema's node-kind enum vs. compiler's topology-node-kind set are different concepts. Confirm the schema doesn't constrain `topology.nodes[].kind` (it doesn't — `type: string` only at schema:138). Likely fine; flag for the surprises section. |

### DTOs / `ModelService` / `TimeGrid`

| # | source file:line | rule | schema? | adjunct? | disposition |
|---|---|---|---|---|---|
| 86 | `ModelDtos.cs:13` | `RngDto.Kind` defaults `"pcg32"` | partial (enum at :898; no `default`) | no | **schema-add (declare default)** + **parser-justified** (DTO initializer) — both helpful |
| 87 | `ModelDtos.cs:53` | `GridDto.BinUnit` defaults `"minutes"` | yes (enum at :74-78) | no | **parser-justified** — only reached via test/manual construction (schema marks `binUnit` required) |
| 88 | `ModelDtos.cs:63` | `NodeDto.Kind` defaults `"const"` | partial (enum :475-481; no `default`) | no | **schema-add (declare default)** |
| 89 | `ModelDtos.cs:133` | `TopologyNodeDto.Kind` defaults `"service"` | partial (`type: string` at :138-144; no enum, no default) | no | **schema-add (declare default; consider enum)** |
| 90 | `ModelDtos.cs:223` | `DispatchScheduleDto.Kind` defaults `"time-based"` | yes (enum :1024-1028 with `default: time-based`) | no | **schema-covered** |
| 91 | `ModelService.cs:22` | YAML deserializer permissive (`IgnoreUnmatchedProperties`) | n/a | n/a | **parser-justified** — engine intake is permissive; schema is the strict gate (documented) |
| 92 | `ModelService.cs:62` | `arrival.ClassId` null/whitespace → coerced to `"*"` on convert | partial (schema:842-847 declares nullable) | no | **parser-justified** — bridges DTO-nullable to legacy non-nullable; magic-value coercion. Note `ValidateClassReferences` uses **declared** `classId` not the coerced one — order-dependent; document. |
| 93 | `ModelService.cs:119` | `OutputDto.As` null → coerced to `""` on convert | partial (`as` optional with pattern :755-768) | no | **parser-justified** — bridges nullable to legacy non-nullable; coerced `""` would fail re-validation but path is post-validation only. Document as bridge artefact. |
| 94 | `ModelService.cs:175-176` | `TopologyEdgeDefinition.Weight` defaults `1.0` when null | partial (no `default`) | no | **schema-add (declare default)** |

## Cross-reference findings

JSON Schema draft-07 cannot express these. Most are **gaps** — currently NOT enforced anywhere. Proposed adjuncts:

| Rule | Current home | Proposed adjunct |
|---|---|---|
| Node IDs unique within `nodes[]` | **Gap** — schema:1221 notes it; no method exists | `ValidateNodeIdUniqueness` |
| `outputs[].series` references resolve to a node ID (or `*`) | **Gap** — schema:1226 notes it | `ValidateOutputSeriesReferences` |
| `expr` references resolve to declared node IDs | **Gap** — schema:1224 notes it; `GraphAnalyzer.GetNodeInputs` extracts but only for graph-build | `ValidateExpressionNodeReferences` |
| `const` node `values.length == grid.bins` | **Gap** — schema:1222 notes it | `ValidateConstNodeValueCount` |
| PMF `probabilities` sum to 1.0 (±1e-4) | enforced in `Pmf.Pmf` ctor | `ValidatePmfProbabilitySum` (lift from ctor; surface error from validator) |
| PMF `values` unique | enforced in `ModelParser.cs:431-432` | `ValidatePmfValueUniqueness` |
| PMF `values.length == probabilities.length` | enforced in `ModelParser.cs:420-421` | `ValidatePmfArrayLengths` |
| Self-shift expression requires `topology.initialCondition.queueDepth` | enforced in `ModelParser.cs:307-316` | `ValidateSelfShiftRequiresInitialCondition` |
| Topology `arrivals` series id resolves to a node | **Gap** — `SemanticReferenceResolver.ParseSeriesReference` handles syntax only | `ValidateTopologySeriesReferences` |
| WIP overflow target exists | enforced in `ModelCompiler.cs:380-384` | `ValidateWipOverflowTarget` |
| WIP overflow graph acyclic | enforced in `ModelCompiler.cs:412-420` | `ValidateWipOverflowAcyclic` |
| Class IDs in `traffic.arrivals[].classId` exist in `classes[]` | **already adjunct** (`ValidateClassReferences:124-167`) | n/a — prior art |
| Class IDs in router `route.classes` exist in `classes[]` | **Gap** | `ValidateClassReferences` extension |
| Sim-mode: `grid.start` non-empty + `topology.nodes` non-empty | enforced in `RunOrchestrationService:838-856` | `ValidateSimulationModel` (mode-aware adjunct) |
| Date-time format on `grid.start` (and any other `format: date-time` field) | enforced in `ModelParser.cs:251-252` | `ValidateDateTimeFormats` |

That's **11 proposed adjunct methods** beyond the existing `ValidateClassReferences`.

## Surprises

(verbatim from agent output, lightly reformatted; main thread should investigate the starred items before AC3/AC4)

1. ⚠️ **`SimModelBuilder.BuildProfiledConstNode` emits `kind: "const"` + retained `pmf` block** (#68 / row #205-256). Schema's `if kind: const then not: [pmf]` rule rejects this shape. Either the path doesn't reach `ModelSchemaValidator` today, or the canary `Survey_Templates_For_Warnings` doesn't exercise a profiled-PMF template. **`@needs-decision-3` — investigate before m-E23-02.**
2. ⚠️ **Case-sensitivity inconsistency.** Parser uses `ToLowerInvariant()`; schema enums are case-sensitive. Affects `binUnit` (#19), node `kind` (#45), `dispatchSchedule.kind` (#58). After m-E23-02, `Minutes` (capital M) will be rejected. **`@needs-decision-1`.**
3. ⚠️ **`ModelValidator` legacy field-rejection migration messages will degrade to generic "additional property not allowed"** (#3, #4, #10, #20). Either preserve via `not: { required: [legacyField] }` + `description:` or accept the degradation. **`@needs-decision-2`.**
4. **Both `ModelValidator` and `ModelSchemaValidator` are wired into production paths today.** From grep: `Program.cs:657` (Engine API), `Cli/Program.cs:76` (CLI), `TimeMachineValidator.cs:50` all call `ModelValidator.Validate`. `ModelSchemaValidator.Validate` is also live (powers the canary). Confirm before m-E23-02 swaps call sites.
5. **`RunOrchestrationService.ValidateSimulationModel` adds two cross-mode rules (`grid.start` required + `topology.nodes` non-empty for sim).** Final disposition (after AC4): **parser-justified (mode-specific)** — orchestration-layer enforcement is the canonical home (rows #77 / #80). Schema can't condition on run mode; lifting into `ModelSchemaValidator` would require a `ValidateForSimulation(yaml)` overload changing every existing caller, conflicting with m-E23-01's "no call-site migration" constraint. Telemetry-mode runs legitimately don't need `grid.start` or a populated topology, so a mode-blind adjunct would over-enforce.
6. **Topology-node-kind enum.** Compiler accepts `{servicewithbuffer, queue, dlq}` for topology-node `kind`; schema doesn't constrain `topology.nodes[].kind` (just `type: string`). Likely fine — different concept from the `nodes[].kind` enum. Flag-only.
7. **`ModelService.ConvertToModelDefinition` is the unified-DTO → legacy-internal-type bridge.** Per Truth Discipline ("don't keep both bridge + replacement once replacement is active"), this conversion still exists post-E-24 because `ModelDefinition` (legacy internal shape) hasn't been retired. **Out of scope for m-E23-01.** Flag for m-E23-03 (does deleting `ModelValidator` also let us propagate `ModelDto` all the way through `ModelParser`?).
8. **`ModelValidator` test coverage** is dense (29 references across 5 test files). Some are negative-case rule tests that match exactly what AC6's `RuleCoverageRegressionTests` wants. Liftable directly if keyed on rule names rather than `ModelValidator` class. **m-E23-02 concern.**

## Decisions resolved (2026-04-26)

- **Decision 1 — case-sensitivity:** **(a) strict.** Schema enums are case-sensitive; capital forms (`Minutes`, `MINUTES`) become validation errors after m-E23-02 swap. Per Truth Discipline: one canonical form, no permissiveness drift. All shipped templates use lowercase already; canary unaffected. Behaviour-change to be documented in `work/gaps.md` for any external author reaching out.
- **Decision 2 — legacy-field migration messages:** **(b) accept generic.** No users to protect; no shims, no migration aids — we stay pure. Schema rejects `binMinutes` / top-level `arrivals` / top-level `route` / node-level `expression` via `additionalProperties: false`; the error message will be generic. **Drops 5 schema-add rows** (#3, #4, #10, #19, #20).
- **Decision 3 — profiled-PMF emission shape:** **investigate thoroughly.** Background agent `a1ab8ae81e8a1d509` dispatched to determine which of A (illusory; `pmf` is null when emitting `const`), B (real but bad shape never reaches validator), or C (bad shape reaches validator but no template exercises it) holds. AC3/AC4 edits proceed in parallel; D3's verdict factors back in before close.

## Schema-add scope after decisions

Reduced from 21 candidate rows to **16 actual schema edits** (D1 + D2 dropped 5). Remaining work is small targeted additions: `required:` lists, `minLength: 1`, `minItems: 1`, `exclusiveMinimum: 0`, default declarations. See AC3 work-log entry below for the per-edit citation trail.

## Open decisions before AC3/AC4

The audit surfaced **three judgement calls** that affect user-facing behaviour and need explicit decisions before schema-add / adjunct edits land. Each is referenced by `@needs-decision-N` markers in the embedment table above.

### Decision 1 — case-sensitivity on schema enums

**Affected rules:** `binUnit` (#19, #27), node `kind` (#45), `dispatchSchedule.kind` (#58).

**Today:** `ModelParser` accepts `Minutes`, `MINUTES`, `minutes` (case-insensitive via `ToLowerInvariant()`). Schema enums require exact casing (`minutes` only).

**After m-E23-02 swap:** `ModelSchemaValidator` rejects `Minutes` even though `ModelParser` accepted it. Behaviour change.

**Options:**

- **(a) Strict** — accept the schema's case-sensitive behaviour. `Minutes` becomes a validation error. All shipped templates already use lowercase, so the canary is unaffected. External authors / programmatic generators that emit capital forms regress. Truth Discipline-aligned (one canonical form, no permissiveness drift).
- **(b) Permissive** — declare each enum as a `pattern:` with case-insensitive regex (e.g. `^(?i:minutes|hours|days|weeks)$`). Preserves parser behaviour. Harder to read in the schema; loses the enumerated value list as a discoverable contract.

**My lean:** (a) strict. Permissiveness rot is exactly what E-23's spirit is fighting; case-folding is a stylistic concession that becomes a wedge for other "be lenient" arguments. Document the tightening in the milestone tracking and `work/gaps.md` so external authors can update. Verify the canary stays green (it should — all templates use lowercase).

### Decision 2 — legacy-field migration messages

**Affected rules:** `binMinutes` (#10), top-level `arrivals` (#3), top-level `route` (#4), node-level `expression` (#20).

**Today:** `ModelValidator` rejects each with a specific migration hint, e.g. *"binMinutes is no longer supported, use binSize and binUnit instead"*.

**After m-E23-02 swap:** the rejection still happens (via `additionalProperties: false`), but the message degrades to `"additional property 'binMinutes' is not allowed"`. The migration hint disappears.

**Options:**

- **(a) Preserve hints** — add explicit `not: { required: [binMinutes] }` clauses on `grid` (and equivalents for the other three legacy fields) with `description:` strings the schema validator surfaces in errors. Preserves UX. Adds ~12 lines to the schema. Custom `description:` surfacing requires verifying the validator emits descriptions on `not:` failures — may need a small `ModelSchemaValidator` change.
- **(b) Accept generic** — let the message degrade. Document the change in `work/gaps.md` ("legacy-field error messages no longer carry migration hints; if a user hits one, refer them to schema docs"). Smaller surface, less compatibility shimming.

**My lean:** (a) preserve. These are real migration aids that real users hit; the message is the only signal they get. Cost is ~12 lines of schema + verifying `description:` surfacing. Pays off the first time someone hits a legacy field.

### Decision 3 — profiled-PMF emission shape

**Surprise:** `SimModelBuilder.BuildProfiledConstNode` (lines 225-256) emits a node with `kind: "const"` AND a populated `pmf` block. The schema's `if kind: const then not: [pmf]` rule (lines 649-660) rejects this shape.

**Question:** how is the canary green today?

**Three possibilities to investigate:**

- The canary doesn't exercise a profiled-PMF template. Verify: read the 12 templates, check for `kind: pmf` + a profile reference. If none, the path is untested by the canary; **real gap before m-E23-02 swap**.
- The schema validation runs *before* profile resolution (at template-validation time, not post-substitution). Verify the call order in `SimModelBuilder` and `ModelSchemaValidator` invocations.
- The emitter actually drops the `pmf` block when emitting `kind: const`. Re-read lines 225-256 carefully to confirm the surprise is real.

**Action:** investigation step before AC3/AC4. Outcome decides whether the schema needs an `else` branch on the `if kind: const` clause, or the emitter needs to drop the `pmf` block, or this is a no-op.

**My lean:** start the investigation immediately (small, scoped), then return with findings before locking AC3/AC4.

---

## Investigation status

- [x] AC1 inventory complete · agent `acea9a0c242948b4d` · 94 rules · cross-repo grep confirms no sibling-repo dependency
- [x] AC2 dispositions assigned (final) · all `@needs-decision-N` rows resolved · rows #77 / #80 flipped from "adjunct (mode-aware)" → "parser-justified (mode-specific)" per AC4 mode-aware integration choice
- [x] Decision 1 (case-sensitivity) — resolved as **(a) strict** (2026-04-26)
- [x] Decision 2 (legacy-field migration messages) — resolved as **(b) accept generic** (2026-04-26)
- [x] Decision 3 (profiled-PMF emission shape) — resolved as **verdict D** (2026-04-26) — `not`-keyword silent-error blind spot; closed via Fix 1 (validator) + Fix 2 (emitter)
- [x] AC3 schema-add edits — 16 rows landed on `model.schema.yaml` via builder agent `ad6c54b5022b04385`
- [x] D3 Fix 2 — emitter cleanup; canary green; full suite green
- [x] Schema restructure — 5-arm `oneOf` at `nodes[].items`; structurally airtight; smoke-tested green
- [x] D3 Fix 1 — silent-error fallback in `ModelSchemaValidator.CollectErrors`; +6 regression tests green
- [x] AC4 adjunct method implementations — 12 adjuncts via builder agent `a6abaa02cb87b8b80` on commit `d42f649`; 26 negative-case tests green
- [x] AC5 canary stays green — verified at every step; `Survey_Templates_For_Warnings` `val-err == 0` across all 12 templates post-AC4
- [x] AC6 `RuleCoverageRegressionTests` catalogue — 26 tests covering the 12 adjuncts (gap-class + lifted-class) plus the 6 silent-error regression tests from Fix 1; schema-covered rules continue to be tested via existing per-feature suites
- [x] AC7 no call-site migration — `ModelValidator.cs` unchanged; `grep` invariant maintained (3 production call sites + 29 test refs unchanged)
- [x] AC8 no behaviour regression — final state: **1,846 passed / 0 failed / 9 skipped** (with one pre-existing Rust-subprocess flake that surfaces intermittently in full-suite runs and passes cleanly in isolation; family already documented in this tracking doc)
- [x] AC9 audit doc committed — this commit

## Reviewer notes (optional)

- The audit is the deliverable. Schema and adjunct edits are by-products of doing the audit honestly. Reviewers should verify that every `schema-add` row in the embedment table corresponds to a real schema diff, every `adjunct` row corresponds to a real method on `ModelSchemaValidator`, and every `parser-justified` row carries a written rationale (not just an assertion).
- No cross-repo coordination concern. The legacy sibling that historically held Sim code has been absorbed into this repo and will be deleted; do not re-introduce it as a reference surface.

## Validation

- `dotnet build FlowTime.sln` — green required at every commit.
- `dotnet test FlowTime.sln` — green at milestone close.
- `TemplateWarningSurveyTests.Survey_Templates_For_Warnings` — `val-err == 0` (E-24's hard-asserting canary stays green).
- `RuleCoverageRegressionTests` (new) — every audited rule has at least one negative-case test that fires.
- Build cost: default `dotnet build` unaffected; analyzer-enabled build (`/p:RoslynatorAnalyze=true`) ~50 s.

## Deferrals

<!-- Work observed during this milestone but deliberately not done. Mirror into work/gaps.md. -->

- (filed during work)

## Initial context

This milestone replaces the original `m-E23-01-schema-alignment` milestone. The original was largely absorbed by E-24 m-E24-03 (schema rewrite) and m-E24-05 (canary commit + hard-assertion promotion). The unowned piece — the rule-by-rule audit ensuring `ModelSchemaValidator` + the schema together cover every rule `ModelValidator` enforces — becomes the new m-E23-01 focus. Slug change resolves collision with E-24's "schema-alignment" slug.

The pre-rescope branch `milestone/m-E23-01-schema-alignment` and `stash@{0}` are obsolete (their content was absorbed by E-24 milestones); flagged for retirement. This milestone branches fresh from post-E-24 `epic/E-23-model-validation-consolidation` (which == `main` at `9cae437`).

**Spirit (per epic spec rewrite 2026-04-26):** `model.schema.yaml` is the only declarative source of structural truth; `ModelSchemaValidator` is the only runtime evaluator. Eliminate every "embedded schema" — every place outside the canonical schema where model rules are re-encoded. E-24 closed the type and schema-document embedments; E-23 closes the rule-evaluator embedment. This milestone is the prep work that makes m-E23-02's call-site migration safe (every rule has a known canonical home before any call site flips).
