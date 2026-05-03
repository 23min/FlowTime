---
id: M-069
title: Schema + Compile + Analyse Enforcement
status: draft
parent: E-25
depends_on: [M-066]
acs:
    - id: AC-1
      title: Schema rejects consumer-side peer-relative split arithmetic
      status: open
    - id: AC-2
      title: Compile-time fan-out routing-authority detector implemented
      status: open
    - id: AC-3
      title: Analyser warning routing_authority_ambiguous implemented
      status: open
    - id: AC-4
      title: Analyser warning consumer_side_peer_split_detected implemented
      status: open
    - id: AC-5
      title: Existing edge_flow_mismatch_incoming/_outgoing warnings preserved
      status: open
    - id: AC-6
      title: Test suite covers gates with deliberately-broken model fixtures
      status: open
    - id: AC-7
      title: Branch-coverage audit complete for all new gate code
      status: open
    - id: AC-8
      title: Existing template suite passes all new gates without regression
      status: open
    - id: AC-9
      title: Documentation updated to reference the gates
      status: open
    - id: AC-10
      title: aiwf check clean
      status: open
---

## Goal

Land the schema/compile/analyse enforcement points named by the M-066 ADR so the flow-authority policy is enforced at every gate, not just documented. The deliverable is engine + validator + analyser code that rejects models violating the policy, plus tests proving each gate fires on deliberately-broken model fixtures. **No template edits, no `ExpectedRunWarnings` baseline reset**: this milestone makes the gates real; the next milestone (M-067 Engine + Template Alignment) edits templates so they pass under the new gates.

## Context

M-066 ratified an ADR naming the flow-authority policy: routing authority lives at producer-fan-out points, edge weights are normative for static-share fan-outs (class 3), router nodes for dynamic routing (class 1), class-2 capacity-aware allocation is not surfaced today and is filed as a deferred gap, consumer-side expr arithmetic must not encode peer-relative splits. The ADR explicitly named three enforcement layers:

- **Schema** (`docs/schemas/model.schema.yaml` + `ModelSchemaValidator`) — rejects models that encode peer-relative splits in consumer expr arithmetic.
- **Compile** (`ModelCompiler` / `TimeMachineValidator`) — fan-out detection: any producer with >1 outgoing edge must have exactly one routing authority declared.
- **Analyse** (`InvariantAnalyzer.cs`) — new warnings `routing_authority_ambiguous` and `consumer_side_peer_split_detected` alongside the existing `edge_flow_mismatch_incoming` / `_outgoing` family.

This milestone implements those three layers. It does NOT edit the existing 9 affected shipped templates — that's M-067's job. The existing shipped template suite is expected to **fail** the new gates after this milestone lands; that failure is the proof that the gates work, and M-067's job is to fix the templates so they pass.

The `ExpectedRunWarnings` baseline canary stays exactly as it is. The new gates produce *errors* and *warnings* that are independent of the existing run-warn baseline. M-067 will reset the baselines once the templates are aligned.

## Acceptance criteria

### AC-1 — Schema rejects consumer-side peer-relative split arithmetic

`ModelSchemaValidator` (and the underlying schema in `docs/schemas/model.schema.yaml` if the rule can be expressed there) rejects models where a consumer node's expr formula references both an upstream producer's `served`-class series **and** a `split*` parameter, where the rule is the canonical class-3-faked-by-arithmetic pattern named in the ADR. Detection mechanic is a milestone-internal design choice (AST inspection of the expr formula vs. structural pattern matching vs. a heuristic with documented false-positive bound). Rejection is a tier-1 or tier-2 validation error returned by `POST /v1/run`; the user sees a `400 { error }`.

The exact pattern set the rule recognizes is documented inline in the validator code AND in the testing doc this milestone touches (AC-9). False positives are acceptable if the false-positive bound is documented and the false-positive case is itself a modeling error that should be re-expressed; false negatives (real peer-split arithmetic that slips through) fail this AC.

### AC-2 — Compile-time fan-out routing-authority detector implemented

`ModelCompiler` (or `TimeMachineValidator`'s tier-2 compile pass; milestone picks the right home) walks the compiled topology and, for every producer node with more than one outgoing edge, asserts that exactly one routing authority is declared at the fan-out:

- **Edge weights** — a non-default weight pattern across the outgoing edges (the ADR defines what counts as "non-default"; default-uniform is the absence-of-authority case).
- **Router node** — a `kind: router` node downstream is the routing authority; the producer's outgoing edges feed into it.
- **Capacity-aware allocator** — the surface that does not exist today; the detector is forward-compatible with the ADR's class-2 deferred work.

If zero or more than one authority is detected at any fan-out, compile fails with a clear error naming the offending producer node and the conflict. This is a tier-2 error.

### AC-3 — Analyser warning routing_authority_ambiguous implemented

`InvariantAnalyzer.cs` (or the analyse-tier path that hosts the existing `edge_flow_mismatch_incoming` family) emits a warning with code `routing_authority_ambiguous` for any post-compile model where the routing authority detection (from AC-2) succeeded but the runtime edge-flow values diverge from the declared authority's expected values. This is the lighter-weight runtime check that catches drift the compile-time check missed (e.g., a router node that the compile detector accepted but whose runtime output disagrees with declared edge weights for some reason).

The warning carries: severity `warning`, code `routing_authority_ambiguous`, the node id of the offending producer, the edge ids involved, and a human-readable message naming the conflict. Severity is `warning` — analyser warnings do not reject the run.

### AC-4 — Analyser warning consumer_side_peer_split_detected implemented

`InvariantAnalyzer.cs` emits a warning with code `consumer_side_peer_split_detected` when a consumer node's runtime arrivals appear to follow a peer-relative split pattern (e.g., the arrivals are a constant fraction of the producer's served, where the fraction matches `1 / (number of consumers)` or a named `split*` parameter value). This is the runtime backstop for AC-1: even if the schema rule misses a case, the analyser catches it at run-time.

The warning carries: severity `warning`, code `consumer_side_peer_split_detected`, the consumer node id, the producer node id, the apparent split fraction, and a human-readable message. The detection heuristic and false-positive bounds are documented inline in the analyser code.

### AC-5 — Existing edge_flow_mismatch_incoming/_outgoing warnings preserved

The existing `edge_flow_mismatch_incoming` and `edge_flow_mismatch_outgoing` warnings continue to fire exactly as before this milestone. The new warnings from AC-3 and AC-4 are *additive*. The Phase 2 baseline canary's `ExpectedRunWarnings` dictionary is unchanged by this milestone — its values reflect the existing run-warn counts, and adding new warning codes that fire on the same templates does not by itself reset the baselines (M-067 owns that reset).

This AC pins the no-regression guarantee: any pre-milestone test that passed must still pass after this milestone.

### AC-6 — Test suite covers gates with deliberately-broken model fixtures

For each of the four new gate behaviors (AC-1, AC-2, AC-3, AC-4), at least one fixture model is added to the test suite that:

- **Triggers the gate** (the model contains the pattern the gate is supposed to catch). Test asserts the gate fires with the expected error/warning code, severity, and node id.
- **Does NOT trigger the gate** (the model contains a related-but-legitimate pattern). Test asserts the gate does NOT fire (no false positive).

Fixtures live under `tests/fixtures/flow-authority-gates/` (new directory) with a README naming what each fixture exercises. The test class lives under `tests/FlowTime.Core.Tests/` or `tests/FlowTime.Api.Tests/` (milestone picks the right home based on which gate's code-under-test is involved).

This is the TDD red phase for each gate: write the broken-fixture test first (red), implement the gate (green), refactor.

### AC-7 — Branch-coverage audit complete for all new gate code

Per CLAUDE.md's branch-coverage hard rule: every reachable conditional in the new gate code (the schema rule, the compile detector, both analyser warnings) has an explicit test exercising it. The audit walks the diff line-by-line and confirms coverage. The audit's output is recorded in this milestone's spec body or in a sibling tracking section before the milestone closes.

### AC-8 — Existing template suite passes all new gates without regression

After the gates land, the existing 12 shipped templates are run through `Survey_Templates_For_Warnings`. The expected outcome:

- Templates that the ADR names as policy-conformant pass all gates clean.
- Templates that the ADR names as policy-violating (the 9 affected from G-032) **fail one or more gates** (likely AC-1 schema-level or AC-4 analyser warning). This failure is the proof that the gates work; M-067 fixes the templates.

The exact failure mode for each of the 9 templates is documented in this milestone's spec body before close. M-067 plans against this documented failure mode.

The Phase 2 `ExpectedRunWarnings` baseline canary is unchanged; the existing run-warn baselines stay exactly as they are. The new gate failures are *separate* signals that M-067 will collapse to clean-by-construction.

### AC-9 — Documentation updated to reference the gates

The taxonomy doc from M-066 (`docs/architecture/flow-authority-policy.md` or its M-066-chosen name) is updated with a "Enforcement" section that names the four gates landed by this milestone, what each rejects/warns on, and how an author can debug a violation. The testing doc that lands in M-068 (`docs/testing/golden-output-canary.md`) is not touched here — that's M-068's deliverable.

If a new testing-doc home is appropriate (e.g., `docs/testing/flow-authority-gates.md` for the deliberately-broken fixtures), it lands in this milestone.

### AC-10 — aiwf check clean

After all updates and gate landings, `aiwf check` reports `ok — no findings`. No frontmatter drift, no orphaned references.

## Constraints

- **No template edits.** The 9 affected shipped templates from G-032 are not touched by this milestone. Their edits land in M-067. This milestone's job is to make the gates real and prove they fire on the broken patterns.
- **No `ExpectedRunWarnings` changes.** The baseline canary stays exactly as it is. Resetting baselines is M-067's job once the templates are aligned. This milestone's new gates are independent of the existing run-warn baseline.
- **TDD by default.** Per CLAUDE.md hard rule. AC-6 captures this: each gate's fixture-driven test is written first (red), then the gate is implemented (green), then refactored.
- **Branch-coverage hard rule.** Per CLAUDE.md hard rule and AC-7.
- **No coexistence window with the rejected policy.** The four gates are landed atomically as far as the engine is concerned: a model that violated the policy before this milestone may now produce *different* errors/warnings, but no model is silently accepted that wasn't accepted before. The Phase 2 baseline canary's run-warn counts are unchanged for templates that don't trip the new gates.
- **The existing shipped templates are expected to fail.** This is by design (AC-8); failure is the proof that the gates work. The existing CI is expected to *not* break because the new gates fire as warnings/errors on `POST /v1/run` for deliberately-test fixtures, not on the existing template-survey canary's run-warn count check.
- **Project rules.** .NET 9 / C# 13; private fields camelCase without leading underscore; invariant culture; camelCase JSON; no time or effort estimates in spec or commit bodies.

## Design Notes

- **Where the schema rule lives.** Three options: (a) JSON Schema additions to `docs/schemas/model.schema.yaml` (limited expressive power); (b) a custom validator rule in `ModelSchemaValidator` that runs after JSON-Schema parsing (full C# expressive power); (c) both (schema-level for the simple case, validator-level for the AST-pattern case). Strawman: option (b) for the AST-pattern detection, with a schema-level pattern hint if a useful subset can be expressed declaratively. The ADR's enforcement section in M-066 may prefer one over the others; pick whichever matches the ADR.
- **Where the compile detector lives.** Two options: (a) a new pass in `ModelCompiler`; (b) a new validation in `TimeMachineValidator`'s tier-2 compile path. Strawman: option (b), because `TimeMachineValidator` is already the canonical home for tier-N validation per E-23.
- **Detection heuristic for AC-4.** The `consumer_side_peer_split_detected` warning is harder to make precise than the schema rule because it's a runtime-output check. Strawman: detect when a consumer node's arrivals are within a tight tolerance (e.g., 1e-6 relative) of `producer.served * f` for some `f` that matches `1/k` where `k = number of consumers fed by producer`, OR `f` matches a named template parameter that contains "split" in its name. Both are heuristics; document the false-positive bound.
- **Class-2 forward-compatibility.** AC-2's compile detector should be coded so that adding a "capacity-aware allocator" authority surface in a future engine milestone is a small change (e.g., a new enum case in the routing-authority taxonomy, not a refactor of the detector's structure).
- **Test layout.** Strawman: deliberately-broken fixtures under `tests/fixtures/flow-authority-gates/<gate-name>/`, with one fixture per AC × triggering/non-triggering pair. Named test class per gate so failure messages stay clean.
- **Conventional commit prefixes.** Engine code → `fix(core):` or `feat(core):`. Tests → `test(core):`. Docs → `docs(arch):` or `docs(testing):`. Conventional commits, no emoji, per CLAUDE.md.

## Surfaces touched

- `src/FlowTime.Core/...` (engine code; specific files determined during implementation — at minimum `Analysis/InvariantAnalyzer.cs`, possibly `ModelCompiler.cs` or a new pass under `Compilation/`, possibly `Validation/ModelSchemaValidator.cs`)
- `src/FlowTime.TimeMachine/Validation/...` if the compile-time detector lives there
- `docs/schemas/model.schema.yaml` if any of the rule is expressible at JSON-Schema level
- `tests/FlowTime.Core.Tests/...` and/or `tests/FlowTime.Api.Tests/...` (new test classes per gate)
- `tests/fixtures/flow-authority-gates/<gate-name>/...` (new — deliberately-broken model fixtures)
- `docs/architecture/flow-authority-policy.md` (M-066's deliverable; this milestone adds an "Enforcement" section)
- `docs/testing/flow-authority-gates.md` (new, optional — deliberately-broken fixture catalog and gate-debugging guide)
- `work/epics/E-25-engine-truth-gate/M-069-schema-compile-analyse-enforcement.md` (this file; status updates as ACs land)
- `work/epics/E-25-engine-truth-gate/epic.md` (small edit — milestone-status reflection if applicable)

## Out of scope

- Template edits (M-067).
- `ExpectedRunWarnings` baseline reset (M-067).
- The `val-warn` delta gate addition to `Survey_Templates_For_Warnings` (M-067 — the gate addition is part of M-067's bridge canary work).
- Class-2 capacity-aware allocator implementation (deferred per M-066's gap; future engine work).
- Rust-engine alignment with the policy (G-016 territory; future work).
- Golden-output canary infrastructure (M-068).
- Heavyweight refactors of `InvariantAnalyzer` beyond adding the two new warnings (out of scope unless adding the warnings forces a refactor; in that case the refactor is part of the AC, not an extension of the milestone).

## Dependencies

- M-066 — ratified ADR naming the policy and the enforcement points. M-069 cannot start until the ADR is `accepted`.

## References

- Epic spec: `work/epics/E-25-engine-truth-gate/epic.md`
- M-066 spec (ADR source-of-truth): `work/epics/E-25-engine-truth-gate/M-066-edge-flow-authority-decision.md`
- Gap: `work/gaps/G-032-…md` — the original conservation-warning regression that surfaced the policy gap
- Phase 2 baseline canary: `tests/FlowTime.Integration.Tests/TemplateWarningSurveyTests.cs:79`
- Analyser source (existing warnings): `src/FlowTime.Core/Analysis/InvariantAnalyzer.cs:323-335` (incoming) and `:309-321` (outgoing)
- Validator source: `src/FlowTime.API/Services/...` (M-046/M-047/M-048 era; the consolidated `ModelSchemaValidator.Validate` is the entry point)
- Compiler source: `src/FlowTime.TimeMachine/...` (per E-24's unified shape)
