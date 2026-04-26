---
id: E-23-model-validation-consolidation
status: ready-to-resume
depends_on: E-24-schema-alignment
completed:
---

# E-23: Model Validation Consolidation

## Goal

Make `docs/schemas/model.schema.yaml` the **only declarative source of structural truth** about the post-substitution model, and `ModelSchemaValidator` the **only runtime evaluator**. Eliminate every "embedded schema" — every place outside the canonical schema where model rules are re-encoded. After E-23 closes:

- One schema. Declared in `model.schema.yaml`.
- One validator. `ModelSchemaValidator.Validate`, with named adjuncts (alongside `ValidateClassReferences`) for any rule JSON Schema draft-07 cannot express.
- Zero parallel imperative validators. `ModelValidator.cs` is deleted.
- Every rule has exactly one canonical home. No silent rules in parsers, emitters, or post-parse orchestration paths.

## Spirit and history

The discovery driving E-23 was that FlowTime did not have a central model schema every component agreed on. Different files enforced different — sometimes contradictory — versions of "what is a valid model." E-24 Schema Alignment fixed two of those embedments: it unified the **type** (`SimModelArtifact` + `ModelDefinition` collapsed to `ModelDto`/`ProvenanceDto` in `FlowTime.Contracts`) and consolidated the **schema document** (`model.schema.yaml` rewritten top-to-bottom against `ModelDto`, hard-asserted at `val-err == 0` across the twelve templates per `TemplateWarningSurveyTests`).

E-23 closes the loop. After E-24 there is still a parallel imperative validator on the live `/v1/run` and CLI paths (`src/FlowTime.Core/Models/ModelValidator.cs`, 214 lines, ~25 rules) plus latent rules in the parser, emitter, and orchestration layers that have never been audited against the schema. E-23 audits those embedments, lifts every rule into either the schema or a named `ModelSchemaValidator` adjunct, migrates every call site to `ModelSchemaValidator`, and deletes `ModelValidator` outright.

This is the same Truth Discipline spirit that drove E-24's type unification: the canonical contract owns the rule; nothing else does. The 2026-04-23 guard added to `.ai-repo/rules/project.md` (*"'API stability' does not mean 'keep old functions around.'"*) is the explicit prohibition on retaining `ModelValidator` as a dead alternative entry point once `ModelSchemaValidator` covers it. The companion guards (*"Do not restate a canonical contract in many places from memory"*, *"Do not let adapter/UI projection become the only place where semantics exist"*) extend the same discipline to the parser, emitter, and orchestration layers.

## Context

Two validators ship today in `FlowTime.Core`:

- **`ModelValidator.Validate`** (`src/FlowTime.Core/Models/ModelValidator.cs`) — hand-rolled, imperative, uses YamlDotNet `IgnoreUnmatchedProperties()`. Does not consult the JSON schema. Used by `POST /v1/run` (`src/FlowTime.API/Program.cs:657`), the Engine CLI (`src/FlowTime.Cli/Program.cs:76`), and `TimeMachineValidator` tier-1 (`src/FlowTime.TimeMachine/Validation/TimeMachineValidator.cs:50`).
- **`ModelSchemaValidator.Validate`** (`src/FlowTime.Core/Models/ModelSchemaValidator.cs`) — reads `docs/schemas/model.schema.yaml` and performs full JSON-schema evaluation plus the `ValidateClassReferences` adjunct. Used inside `TimeMachineValidator.ValidateSchema` (line 46), reachable from `POST /v1/validate` and the `flowtime validate` CLI command.

Post-E-24 they validate the same C# type (`ModelDto`) against semantically the same input. That is the foundation E-23 builds on. What E-23 must still do:

1. **Audit every rule embedment.** `ModelValidator`'s 25 rules are the obvious target, but parser tolerations, silent emission defaults, and post-parse orchestration checks are equally embedments. Each one is a place where model truth lives outside the canonical schema and can therefore drift.
2. **Lift each rule to its canonical home.** Schema-expressible rules go to `model.schema.yaml`. Cross-reference / runtime-data rules go to `ModelSchemaValidator` adjuncts. A rule that legitimately cannot be a model rule (e.g., scalar-style coercion is YAML representation concern, not model rule — already addressed by E-24 m-E24-04) gets a written justification.
3. **Migrate every production call site.** `POST /v1/run`, Engine CLI, `TimeMachineValidator` tier-1, plus four test surfaces flip from `ModelValidator.Validate` to `ModelSchemaValidator.Validate`.
4. **Delete `ModelValidator.cs` outright.** No delegation shim, no dead alternative entry point.

The historical context — the pause for E-24, the original m-E23-01 "schema-alignment" milestone, the stashed input material on branch `milestone/m-E23-01-schema-alignment` — is captured in `D-2026-04-24-036` (E-23 paused, E-24 created), `D-2026-04-24-037` (Option E unify ratified), and `D-2026-04-25-038` (E-24 closed, E-23 ready to resume). Those decisions stand; this spec rewrite is the post-E-24 reformulation, not a contradiction of them.

## Scope

### In Scope

- **Embedment audit.** Every rule encoded in `ModelValidator.cs`, `ModelParser.cs`, `SimModelBuilder.cs` (post-E-24), `RunOrchestrationService.cs`, and `GraphService.cs` is enumerated and dispositioned. Per-rule outcome: schema-covered, schema-add, adjunct, parser-justified, or drop.
- **Schema additions.** Every rule classified `schema-add` lands in `docs/schemas/model.schema.yaml` with a citation comment so the rule's provenance is traceable.
- **Adjunct additions.** Every rule classified `adjunct` lands as a named method on `ModelSchemaValidator` alongside `ValidateClassReferences`, invoked from `Validate`, with at least one negative-case unit test.
- **Call-site migration.** Production: `POST /v1/run`, Engine CLI, `TimeMachineValidator` tier-1. Tests: `tests/FlowTime.Integration.Tests/SimToEngineWorkflowTests.cs`, `tests/FlowTime.Tests/Schema/{TargetSchemaValidationTests,SchemaVersionTests,SchemaErrorHandlingTests}.cs`.
- **Error-message phrasing audit.** `ModelValidator` returns flat strings (`"Grid must specify bins"`); `ModelSchemaValidator` returns JSON-schema-shaped messages (`/grid/bins: Required properties are missing: [bins]`). Tests asserting on phrasing get updated; UI / CLI consumers that surface raw strings get audited and adjusted (or none, if no consumer regex-parses validator output).
- **`ModelValidator` deletion.** `src/FlowTime.Core/Models/ModelValidator.cs` is removed; `ValidationResult` (currently bottom of that file) is moved to its own file.
- **Negative-case canary.** A new test catalogue feeds a deliberately-invalid model snippet for each non-trivial audited rule and asserts `ModelSchemaValidator.Validate(...).IsValid == false`. Locks in the audit's coverage claim.
- **Survey canary remains green.** `TemplateWarningSurveyTests.Survey_Templates_For_Warnings` continues to report `val-err == 0` across all twelve templates at `ValidationTier.Analyse` — E-24's hard assertion stays in place; nothing in E-23 may break it.

### Out of Scope

- Any change to `SimModelBuilder`'s emission of `grid.start` from `window.start`. Sim is correct; E-24 already aligned the schema (`grid.start` declared optional). E-23 does not modify Sim emission.
- Any change to Blazor or Svelte UI. Both remain untouched. m-E21-07 Validation Surface (E-21) consumes the consolidated validator after E-23 closes; that is the next epic's concern.
- New validator features: line/column mapping, LSP integration, incremental validation, per-field suggestions, compile-time rule extraction, schema-draft migration. The goal is consolidation, not expansion.
- `Template`-layer validation. `TemplateSchemaValidator` validates pre-substitution authoring templates against `template.schema.json` — that is genuinely a different contract and stays distinct. E-23 only touches post-substitution model validation.
- Reintroducing any deprecated schema fields (`binMinutes`, snake_case provenance, two-type YAML, etc.). Per project rules.
- External-consumer compatibility. No camelCase aliases, no dual-shape acceptance, no migration mode. Forward-only per `ADR-E-24-02`.

## Constraints

- **`ModelValidator` is deleted, not delegated.** No forwarding shim. No "temporary alias." Single consolidation endpoint after E-23: `ModelSchemaValidator.Validate`.
- **Schema-first preference for rule placement.** When a rule is expressible in JSON Schema draft-07, it belongs in the schema, not as an adjunct. Adjuncts are the exception (cross-reference checks, runtime-data conditionals), not the default.
- **Parser-justified is a written justification, not a default.** Any rule left in parser/emitter code must answer "why is it not a model rule?" in the m-E23-01 audit doc. "Hard to move" is not a justification.
- **Byte-for-byte parity on the `/v1/run` success path.** Currently-valid models receive byte-identical responses post-migration. Error responses preserve HTTP status (400) and JSON shape (`{ "error": "..." }`); error phrasing may change (covered by phrasing audit in m-E23-02).
- **Survey canary stays green at every milestone close.** `TemplateWarningSurveyTests` continues to report `val-err == 0` at `ValidationTier.Analyse`. A non-zero count fails the build.
- **No reintroduction of `FlowTime.Generator` or any variant of the deleted provenance pipeline.** Per `D-2026-04-07-019` Path B.

## Success Criteria

- [ ] Embedment audit recorded in `m-E23-01-rule-coverage-audit-tracking.md`. Every rule in `ModelValidator.cs`, `ModelParser.cs`, `SimModelBuilder.cs`, `RunOrchestrationService.cs`, and `GraphService.cs` has a row with: file:line citation, plain-English rule, current schema status (yes/no/partial with line cite), and final disposition (schema-covered, schema-add, adjunct, parser-justified, drop).
- [ ] Every rule classified `schema-add` is declared in `docs/schemas/model.schema.yaml` with a citation comment. Every rule classified `adjunct` is implemented as a named method on `ModelSchemaValidator` and invoked from `Validate`, with at least one negative-case unit test.
- [ ] Negative-case canary (`tests/FlowTime.Core.Tests/Schema/RuleCoverageRegressionTests.cs` or analogous) covers every non-trivial audited rule. Each test feeds a deliberately-invalid model snippet and asserts `ModelSchemaValidator.Validate(...).IsValid == false` plus the expected error contains the rule's identifying substring.
- [ ] `src/FlowTime.Core/Models/ModelValidator.cs` is deleted. `grep -rn "ModelValidator\b" --include="*.cs"` outside `.claude/worktrees/` returns zero hits.
- [ ] `ModelSchemaValidator.Validate` is the single model-YAML validator called from `POST /v1/run`, `POST /v1/validate`, the Engine CLI, the `flowtime validate` Time Machine CLI, `TimeMachineValidator`, and all test paths.
- [ ] `POST /v1/run` produces byte-identical success responses for all currently-valid models post-migration. For currently-invalid models, error responses preserve HTTP 400 and `{ "error": "..." }` shape; error phrasing may differ and is covered by an explicit phrasing audit in m-E23-02.
- [ ] `TemplateWarningSurveyTests.Survey_Templates_For_Warnings` reports `val-err == 0` across all twelve templates at `ValidationTier.Analyse`. (E-24's hard assertion stays green.)
- [ ] Full `.NET` test suite passes: `dotnet test FlowTime.sln` is green. UI vitest and Playwright remain green (Svelte and Blazor surfaces untouched).
- [ ] `ValidationResult` is moved out of the deleted `ModelValidator.cs` into its own file `src/FlowTime.Core/Models/ValidationResult.cs` (pure relocation, no API change), since it remains the shared result type used by `ModelSchemaValidator` and `TimeMachineValidator`.

## Open Questions

| Question | Blocking? | Resolution path |
|----------|-----------|-----------------|
| Does every rule in `ModelValidator` have an equivalent JSON Schema draft-07 expression, or do some land as adjuncts? | Yes — gates m-E23-01 outcome | m-E23-01 rule audit answers per-rule. Audit output is the first deliverable. Default leaning: most rules express cleanly (enums, ranges, const, `additionalProperties: false`). Cross-reference rules (e.g., node-id uniqueness within `nodes[]`) land as adjuncts because draft-07's `uniqueItems` only matches identical scalars, not "objects with same `id`". |
| Are there parser tolerations in `ModelParser.cs` that constitute unwritten rules? | Yes — gates m-E23-01 outcome | m-E23-01 reads `ModelParser.cs` (733 lines) for `IgnoreUnmatchedProperties`, `??` defaults, conditional branches that swallow missing data. Each becomes an audit row. |
| Are there orchestration-layer checks in `RunOrchestrationService` / `GraphService` that should be model rules? | Yes — gates m-E23-01 outcome | m-E23-01 surveys the post-parse path. Likely findings: class-reference checks (already in `ValidateClassReferences`), output-id uniqueness, etc. Each becomes an audit row. |
| Should m-E23-02 and m-E23-03 collapse into one milestone, or stay split for rollback safety? | No — sequencing detail | Default: stay split. m-E23-02 leaves `ModelValidator.cs` on disk as a single-revert safety net during the call-site flip; m-E23-03 deletes it once green. If the m-E23-01 audit shows the migration is byte-trivial on every site, the user can collapse before m-E23-02 starts. |
| Do non-test callers of `ModelValidator` exist in sibling repositories (MCP, external tools)? | Unknown — gates the delete | m-E23-01 audit includes a cross-repo grep against sibling checkouts visible to this workspace (treated read-only per project rule). If callers exist, E-23 either absorbs them or coordinates deletion with the sibling. |

## Risks (optional)

| Risk | Impact | Mitigation |
|------|--------|------------|
| A rule embedded in `ModelValidator` (or parser, or emitter) is silently dropped when `ModelValidator.cs` is deleted because it had no schema or adjunct equivalent | High | m-E23-01 audit is the primary defense — every rule must classify into schema-covered, schema-add, adjunct, parser-justified, or drop. Negative-case canary in m-E23-01 AC6 is the secondary defense — proves `ModelSchemaValidator` actually catches every rule the audit claims. Tertiary defense: keep `ModelValidator.cs` on disk through m-E23-02 so revert is one commit. |
| Error-phrasing change breaks a downstream consumer (Blazor surfacing validation messages verbatim, CLI scripts grep'ing stdout) | Medium | m-E23-02 includes an error-phrasing audit + UI/CLI consumer scan. Tests asserting exact phrasing get relaxed to semantic assertions (`errors.Should().Contain(e => e.Contains("bins"))`). |
| `ModelSchemaValidator` per-request cost is materially higher than `ModelValidator`'s hand-rolled checks at hot paths like `POST /v1/run` | Low | m-E23-02 records a before/after timing of `POST /v1/run` against a representative template. If latency grows by more than ~2 ms the audit doc records it; if materially more, escalate. No mitigation work planned until we have a number. |
| External consumers (VSCode YAML plugin, docs tooling) read `docs/schemas/model.schema.yaml` directly and break on schema additions | Low | Additions are permissive (new optional rules tightening invalid cases that were already accidentally invalid). External consumers accept more, not less. Document the schema bump in the milestone tracking doc. |
| Post-E-24 line numbers in `Program.cs` / `TimeMachineValidator.cs` shift from what the spec recorded; m-E23-02 references stale lines | Low | m-E23-02 spec instructs starting with a fresh `grep -rn "ModelValidator\.Validate" --include="*.cs"` to enumerate live call sites at start-milestone time, not relying on cached line numbers. |

## Milestones

Sequencing: rule-coverage audit first (doc-only with schema and adjunct additions where the audit shows they are needed), then call-site migration (mechanical with phrasing audit), then `ModelValidator` deletion (cleanup + assertion that nothing calls it anymore). Three milestones.

- [m-E23-01-rule-coverage-audit](./m-E23-01-rule-coverage-audit.md) — Audit every embedment of model rules across `ModelValidator.cs`, `ModelParser.cs`, `SimModelBuilder.cs`, and post-parse orchestration. Land schema additions and `ModelSchemaValidator` adjuncts so every rule has a single canonical home. Negative-case canary catalogue locks coverage in. · **ready (E-24 closed 2026-04-25 — `D-2026-04-25-038`)** · depends on: —
- [m-E23-02-call-site-migration](./m-E23-02-call-site-migration.md) — Switch every production call site and test from `ModelValidator.Validate` to `ModelSchemaValidator.Validate`. Audit error-message phrasing and update test assertions / UI consumers as needed. `ModelValidator.cs` left on disk as a single-revert safety net. · **ready (waits on m-E23-01)** · depends on: m-E23-01
- [m-E23-03-delete-model-validator](./m-E23-03-delete-model-validator.md) — Delete `ModelValidator.cs` and any dedicated `ModelValidator`-only test files that survived m-E23-02. Move `ValidationResult` to its own file. Assert `grep` returns zero callers. Archive E-23. · **ready (waits on m-E23-02)** · depends on: m-E23-02

## ADRs

- **ADR-E-23-01 — Delete, do not delegate.** Target state is a single validator (`ModelSchemaValidator`). `ModelValidator` is deleted rather than rewired to forward to `ModelSchemaValidator`, because: (1) a forwarding shim is a new compatibility layer the 2026-04-23 Truth Discipline guard explicitly forbids, (2) there are no external-surface users of `ModelValidator` that justify a shim, (3) a delete is cheaper to maintain than a forward for all future readers. Recorded in `work/decisions.md` before m-E23-01 begins.
- **ADR-E-23-02 — Schema as the single structural contract; adjuncts are the named exception.** `ModelSchemaValidator` reads `model.schema.yaml` and performs JSON-schema evaluation plus named adjunct methods (`ValidateClassReferences` is the prior art). Any structural rule not expressible in JSON Schema draft-07 lands as an adjunct method, not as a second parallel validator and not as silent parser/emitter logic. Adjuncts are named, invoked from `Validate`, and individually unit-tested. Ratified at m-E23-01 audit close, with the enumerated adjunct list as the ratification artefact.
- **ADR-E-23-03 (candidate) — Parser/emitter rules need written justification.** If the m-E23-01 audit classifies any rule as `parser-justified` (left in parser or emitter code rather than lifted to schema or adjunct), this ADR records the rationale framework: the rule is a YAML representation concern, not a model concern, and would not survive a non-YAML serialization of `ModelDto`. Deferred pending m-E23-01 audit findings — recorded only if the audit produces a non-empty `parser-justified` set.

## References

### Source-code pointers

- `src/FlowTime.Core/Models/ModelValidator.cs` — slated for deletion (214 lines, ~25 imperative rules)
- `src/FlowTime.Core/Models/ModelSchemaValidator.cs` — consolidation target (263 lines)
- `src/FlowTime.Core/Models/ModelParser.cs` — parser audited in m-E23-01 (733 lines)
- `src/FlowTime.Sim.Core/Templates/SimModelBuilder.cs` — emitter audited in m-E23-01 (458 lines)
- `src/FlowTime.TimeMachine/Validation/TimeMachineValidator.cs` — tiered validator, drops the `ModelValidator` delegation in m-E23-02
- `src/FlowTime.API/Program.cs` — `POST /v1/run` call site (re-enumerate at m-E23-02 start)
- `src/FlowTime.Cli/Program.cs` — Engine CLI call site (re-enumerate at m-E23-02 start)
- `docs/schemas/model.schema.yaml` — schema, target for audit-driven additions
- `tests/FlowTime.Integration.Tests/TemplateWarningSurveyTests.cs` — survey canary (committed, hard-asserting per E-24 m-E24-05)

### Decisions and ADRs

- `D-2026-04-24-035` — E-23 ratification (delete-not-delegate discipline)
- `D-2026-04-24-036` — E-23 paused, E-24 created
- `D-2026-04-24-037` — Option E (unify) ratified within E-24
- `D-2026-04-25-038` — E-24 closed, E-23 ready to resume
- E-24 ADRs: ADR-E-24-01 Unify · ADR-E-24-02 Forward-only · ADR-E-24-03 Schema declares only consumed fields · ADR-E-24-04 `ScalarStyle.Plain` gates `ParseScalar` · ADR-E-24-05 `QuotedAmbiguousStringEmitter`

### Related epics

- **E-24 Schema Alignment** (closed 2026-04-25): `work/epics/completed/E-24-schema-alignment/spec.md` — unified the type and the schema; E-23 builds on that foundation.
- **E-21 Svelte Workbench** (paused at m-E21-07): `work/epics/E-21-svelte-workbench-and-analysis/spec.md` — m-E21-07 Validation Surface consumes the consolidated `ModelSchemaValidator` once E-23 closes.

### Truth Discipline

- `.ai-repo/rules/project.md` → Truth Discipline Guards — the 2026-04-23 *"'API stability' does not mean 'keep old functions around.'"* guard plus *"Do not restate a canonical contract in many places from memory"* and *"Do not let adapter/UI projection become the only place where semantics exist"*.
