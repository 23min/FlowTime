---
id: E-25
title: Engine Truth Gate — Edge-Flow Authority + Golden-Output Canary
status: active
---

# E-25 — Engine Truth Gate

## Goal

Resolve the engine-correctness investigation surfaced during E-21 dogfooding (G-032 + G-033) and lock down testing rigor before further engine evolution. Concretely: make a defensible design call on edge-flow authority (expr nodes vs. topology edge weights), align engine + shipped templates so the conservation invariant is clean, and promote the lightweight `Survey_Templates_For_Warnings` baseline canary into a strict per-template **golden-output** canary that compares numeric series + warning sets at a sanctioned baseline.

## Context

E-21 M-045 dogfooding (2026-04-28) caught three `edge_flow_mismatch_incoming` warnings on `transportation-basic` that were not present in same-template runs from four days earlier. Investigation (`patch/edge-flow-mismatch`, 2026-05-01) diagnosed the warnings as **improved analyser coverage surfacing a latent template inconsistency**, not an engine bug: pre-E-24 the engine wasn't writing per-edge `flowVolume` series, so the conservation check in `InvariantAnalyzer.cs:323-335` silently skipped; post-E-24 the series exists, the check runs, and it correctly detects that expr-layer arrivals (`hub_dispatch * splitAirport`, parameter-driven splits) diverge from the engine's edge-weight-uniform apportionment (every queue→line edge declared with `weight: 1`). Nine shipped templates are affected; `transportation-basic-classes` is worst at 8 run-warnings.

The patch shipped a **Phase 2 baseline canary** (D-053) — `tests/FlowTime.Integration.Tests/TemplateWarningSurveyTests.cs` now hard-asserts per-template `run-warn` counts against `ExpectedRunWarnings`. Drift in either direction fails the build. This stops the bleeding (no silent regressions slip through) but does not resolve the underlying design call and does not detect numeric drift inside a stable warning count. Both gaps remain open.

The strategic context: this work is the prerequisite for E-22 (Time Machine — Model Fit) and ideally also for E-15 (Telemetry Ingestion). E-22 fits engine output against telemetry; silently-drifting engine output would corrupt fit results without warning. Per D-045 Option A reasoning, locking down engine-output stability is sequenced before fit work.

The aiwf v3 planning tree currently has zero in-flight epics (E-21 closed 2026-05-01, E-11 closed 2026-05-02). E-25 is the next-epic decision.

### Supersedes / closes

- Resolves [G-032](../../gaps/G-032-transportation-basic-regressed-edge-flow-mismatch-incoming-3-after-e-24-unification.md) (`addressed`) — the design question and the per-template baseline cleanup.
- Resolves [G-033](../../gaps/G-033-tests-are-too-weak-surveyed-output-only-canaries-cannot-detect-drift-need-deterministic-golden-output-assertions.md) (`addressed`) — the golden-output canary infrastructure and initial pinning.
- Builds on [D-053](../../decisions/D-053-testing-rigor-approach-phase-2-baseline-canary-first-full-golden-output-canon-deferred.md) (Phase 2 baseline canary) — promotes the deferred full golden-output canon now that empirical signal (this epic) exists.

### Related

- **E-22 Time Machine — Model Fit & Chunked Evaluation** (`work/epics/E-22-time-machine-model-fit-chunked-evaluation/epic.md`) — downstream consumer; E-25 is gating per the strategic call. Fit accuracy depends on detectable engine-output stability at numeric precision, which baseline-counts don't catch. E-22's success criteria reference fit residuals "within documented tolerance"; that tolerance is meaningful only against an engine whose output stability is itself canonical.
- **E-15 Telemetry Ingestion** (`work/epics/E-15-telemetry-ingestion-topology-inference-and-canonical-bundles/epic.md`) — also downstream; E-15's canonical-bundle-replay parity argument benefits from the golden canary as the "synthetic side" reference point.
- **E-24 Schema Alignment** (completed 2026-04-25) — the merge that surfaced G-032 by enabling per-edge `flowVolume` series writing.

## Scope

### In scope

- **Flow-authority policy and ratified ADR.** Name FlowTime's flow-authority policy across the three classes of physical systems FlowTime exists to model (class 1 dynamic routing, class 2 capacity-aware allocation, class 3 static-weight). Ratify the policy as an ADR (durable architectural truth) with the schema/compile/analyse enforcement layers explicitly named.
- **Three-class flow taxonomy doc.** A `docs/architecture/flow-authority-policy.md` (or M-066-chosen name) document that names the three classes, the routing actor's responsibilities per class, and the authority surface assigned to each class. Prose backbone the ADR references.
- **Repo-wide doc sweep.** A tracking artefact that classifies every routing-relevant document in the repo (`aligned` / `silent` / `conflicting` / `ambiguous`) against the policy. Conflicting docs are revised in this epic or marked `[needs revision per ADR-NNNN]`.
- **Policy enforcement at every gate.** Schema rule rejecting consumer-side peer-relative split arithmetic; compile-time fan-out routing-authority detector (every producer with >1 outgoing edge must declare exactly one routing authority); analyser warnings `routing_authority_ambiguous` and `consumer_side_peer_split_detected`. The ADR names what each layer enforces; the enforcement milestone (M-069) lands the implementation.
- **Engine implementation of the chosen authority.** Whatever the policy commits to (the M-066 footprint analysis suggests class-3 fan-outs use edge weights; the ADR makes the call), the engine + analyser + emitted edge-flow series reflects it consistently; the conservation invariant produces clean output on the shipped template set under default parameters.
- **Template alignment.** Edit each affected shipped template (the templates listed in G-032's "Affected templates" table) so that `ExpectedRunWarnings` baselines drop to zero in the same commit that lands the engine change AND so the templates pass the M-069 enforcement gates. Where the policy forces template-form changes, those edits land here.
- **Golden-output canary infrastructure.** A test harness under `tests/FlowTime.Integration.Tests/` that, for each shipped template, runs the engine at a pinned parameter set and compares the full output bundle against a pinned fixture: per-series per-bin numeric values (tolerance comparison), full warning set (codes + messages + node/edge ids + severity), run manifest summary fields. Tolerance and serialization format are design choices for the milestone.
- **Initial pinning across the shipped template set.** For each shipped template (12 today; the canonical list lives in `templates/` and is enumerated by `Survey_Templates_For_Warnings`), capture the expected output bundle once at a sanctioned commit post-engine-fix, store under `tests/fixtures/golden-templates/<template-id>/` with a README documenting parameter set, capture date, and capture commit hash. The list of templates pinned must equal the list `Survey_Templates_For_Warnings` enumerates — drift between the two is itself a build failure.
- **Sanctioned-regeneration workflow.** A `--regenerate` (or equivalent) mode that, given an explicit opt-in, re-captures the bundles after a sanctioned engine change. The diff is the PR-review artifact. Pattern: dotnet snapshot-update style.
- **`val-warn` delta gate as bridge canary.** Until the full golden canary is green across the template set, extend the existing `Survey_Templates_For_Warnings` to also fail on `val-warn` count drift (D-053 currently asserts on `run-warn` only). This is a small change inside the test that already exists.
- **Documentation.** The flow-authority policy doc in `docs/architecture/`; a `docs/testing/golden-output-canary.md` document covering the canary, the regeneration workflow, and the meaning of a green build; an optional `docs/testing/flow-authority-gates.md` covering the deliberately-broken fixtures and the gate-debugging workflow.

### Out of scope

- **Floating-point exact equality across platforms.** Tolerance-based numeric comparison is fine and is what every other comparable engine canary uses (per G-033 and D-053).
- **Pinning intermediate compilation IR.** The pinned artifact is the **observable engine output** (series + warnings + manifest), not the compiled DAG. The IR can change freely as long as the output stays equivalent.
- **Golden-canary coverage of non-default parameter combinatorics.** One sanctioned parameter set per template at this milestone. Sweeps, randomized inputs, fuzz-style coverage are future scope.
- **Engine evolution beyond what the design call requires.** Refactors of `InvariantAnalyzer`, conservation tolerance reshaping, new analyser warning families, or stateful-node-boundary work belong to other epics. E-25 ships the minimum engine change that the chosen authority demands and stops.
- **E-22 Time Machine work.** Model Fit, Chunked Evaluation, the Pipeline SDK — all live in E-22, which is gated on this epic shipping.
- **E-15 Telemetry Ingestion.** Gold Builder, Graph Builder, canonical-bundle adapters — all live in E-15, sequenced after this epic per the D-045 Option A reasoning.
- **UI surfacing of the canary.** Failures appear in CI; no Svelte/Blazor visualization is required by this epic.
- **Engine performance regressions** introduced by the chosen authority. If a template's chosen authority requires expensive engine work, that's followed up under a separate engine-performance epic; E-25 ships correctness first.
- **Historical run-artifact preservation policy.** G-033 notes "do not delete `data/runs/*` until canary in place"; once E-25 ships, the policy is moot. This epic does not codify a permanent retention rule.

## Constraints

- **Policy-first sequencing.** The flow-authority ADR is ratified before the enforcement milestone (M-069) begins; the enforcement gates are landed before the engine + template alignment milestone (M-067) begins; the canary milestone (M-068) is last. Implementation that pre-empts upstream milestones is forbidden. (Rationale: the policy across three classes commits the engine to a flow-theoretic model that drives every downstream layer; getting the policy wrong cascades into rework at every layer.)
- **No coexistence window for the chosen authority.** Once the policy is ratified and the engine implements it, the rejected framings are not retained as tolerated alternate paths. Per project truth-discipline: do not keep "temporary" compatibility shims without explicit deletion criteria.
- **Forward-only template regeneration.** If the chosen authority forces template-form changes, all affected shipped templates land their edits in the same change. No "some templates new form, some old form" coexistence.
- **Phase 2 baseline canary stays green throughout.** D-053's `Survey_Templates_For_Warnings` `run-warn` baseline gate is not loosened during the work; baseline values change only as templates legitimately regress to zero, never to higher numbers.
- **Golden-canary serialization is reviewable.** Pinned fixtures are stored in a format that produces a meaningful PR diff (not an opaque binary). JSON-with-stable-key-order or similar; design choice for the canary milestone.
- **Tolerance values are documented and conservative.** The numeric tolerance the canary uses must be explicit (in code or the canary doc) and chosen so legitimate engine fixes don't constantly tax the regeneration workflow, but real semantic drift fails fast. Default candidate: relative tolerance 1e-9 against double-precision bins; revisited at the canary milestone.
- **`val-err == 0` and `run-warn` baselines remain hard gates.** No regressions to the existing gate posture.
- **Project rules.** .NET 9 / C# 13; invariant culture; camelCase JSON payloads; private fields without leading underscore.

## Success criteria

<!-- Reference-phrased; counts that drift over time are pulled from referenced lists, not reproduced inline. -->

- [ ] G-032 status promoted to `addressed` and references this epic; G-033 status promoted to `addressed` and references this epic.
- [ ] A ratified ADR (under `docs/adr/`) names the flow-authority policy across the three classes of physical systems, names the rejected framings with rationale, and names the schema/compile/analyse enforcement points.
- [ ] A `docs/architecture/flow-authority-policy.md` (or M-066-chosen name) document defines the three-class taxonomy and is referenced by the ADR.
- [ ] A doc-sweep tracking artefact (`work/epics/E-25-engine-truth-gate/m-E25-01-flow-authority-doc-sweep.md`) classifies every routing-relevant document in the repo; conflicting docs are revised or marked `[needs revision per ADR-NNNN]`.
- [ ] A class-2 capacity-aware allocator deferred-follow-up gap is filed naming the future engine work; the ADR references it.
- [ ] The engine implementation reflects the policy; on every shipped template enumerated by `Survey_Templates_For_Warnings`, running at default parameters produces zero `edge_flow_mismatch_incoming` and zero `edge_flow_mismatch_outgoing` warnings.
- [ ] M-069's enforcement gates (schema rule, compile-time fan-out detector, two new analyser warnings) are live and tested with deliberately-broken model fixtures; existing shipped templates pass all gates after M-067 lands.
- [ ] Every entry in `ExpectedRunWarnings` (in `tests/FlowTime.Integration.Tests/TemplateWarningSurveyTests.cs`) sourced from the G-032 affected-templates table is reset to zero in the same commit that lands the engine + template change. Other entries (if any) are individually justified.
- [ ] A golden-output canary asserts, for every shipped template enumerated by `Survey_Templates_For_Warnings`, that the engine's full output bundle (per-series per-bin within tolerance, full warning set exactly, manifest summary fields) matches a pinned fixture committed under `tests/fixtures/golden-templates/<template-id>/`.
- [ ] The set of template ids covered by the golden canary equals the set enumerated by `Survey_Templates_For_Warnings`; coverage drift fails the build.
- [ ] Each pinned fixture directory contains a README naming the parameter set, capture date, and capture commit hash.
- [ ] A sanctioned-regeneration workflow exists: an engineer can run a documented command (e.g. `dotnet test --filter ... --update-fixtures` or equivalent) and re-capture the pinned bundles; the resulting diff is the PR-review artifact.
- [ ] `Survey_Templates_For_Warnings` is extended to fail on `val-warn` delta as well as `run-warn` delta; both gates active simultaneously.
- [ ] Documentation: an architecture note captures the edge-flow authority decision; a testing note captures the golden-output canary, the regeneration workflow, and the meaning of a green build.
- [ ] Full repo test suite is green at epic close; no skipped tests added by this work that aren't explicitly gated on infrastructure (e.g. `FLOWTIME_E2E_TEST_RUNS=1` style).
- [ ] On epic completion, the epic frontmatter is promoted to `status: done` via `aiwf promote E-25 done`; `ROADMAP.md` is regenerated via `aiwf render roadmap --write`; a wrap artefact at `work/epics/E-25-engine-truth-gate/wrap.md` captures what shipped, the pinned-fixture catalog state, and any deferred follow-ups. (Epic dirs stay in place under `work/epics/E-NN-<slug>/` regardless of status — aiwf v3's truth surface is the frontmatter, not the path.)

## Open questions

| Question | Blocking? | Resolution path |
|---|---|---|
| What is the flow-authority policy across the three classes of physical systems FlowTime models (class 1 dynamic routing, class 2 capacity-aware allocation, class 3 static-weight)? | **Yes** | M-066 (the policy spike) produces a ratified ADR naming the policy and the enforcement points. M-069/M-067/M-068 gate on it. |
| ADR or D-NNN for the flow-authority decision record? | No | Settled — ADR. The flow-authority policy is durable architectural truth, not a project-bound decision; it outlives this project's planning context. The repo's `docs/adr/` is empty today; this ADR seeds it. |
| Numeric tolerance for the golden canary — absolute, relative, hybrid; what magnitude? | No | Decided inside the canary milestone with empirical evidence (run several known-clean templates, measure observed bin-to-bin variance under repeat runs, set tolerance with margin). Default starting point: relative 1e-9. |
| Pinned-fixture serialization format — JSON, CSV+JSON-warnings, MessagePack? | No | Decided inside the canary milestone. Constraint: must produce reviewable PR diffs. JSON-with-stable-key-order is the strawman. |
| Should the golden canary live in `tests/FlowTime.Integration.Tests/` alongside `Survey_Templates_For_Warnings`, or in its own project? | No | Decided inside the canary milestone. Default: same project, sibling test class. |
| When the policy forces template-form changes, do user-authored templates outside `templates/` need a migration tool? | No | Out of scope at epic close (we ship only the shipped-template set); but the question is logged as a deferred-follow-up gap if the policy creates user-facing template breakage. |
| Does the `val-warn` delta gate also need a baseline dictionary like `ExpectedRunWarnings`? | No | Decided inside M-067's bridge-canary work. If the engine + template fix lands `val-warn == 0` for all templates, the gate is a hard zero. If not, a baseline dictionary mirrors the existing pattern. |
| Class-2 capacity-aware allocator — when does it ship? | No | Out of scope for E-25; filed as deferred follow-up gap during M-066. The current shipped template set does not require class-2 today; the policy's stance is that class-2 is not surfaced today and templates that need it must currently model differently. The deferred gap captures the future engine work. |

## Risks

| Risk | Impact | Mitigation |
|---|---|---|
| The flow-authority policy commits to a framing that the doc sweep reveals as unworkable for some shipped template surface. | Med | The doc sweep (M-066 AC-3) runs before the ADR is ratified (M-066 AC-9). Ratification gates on the sweep being clean. If the sweep surfaces a structural problem, the policy is revisited before the ADR is locked. |
| The class-2 deferred gap turns out to be needed sooner than expected (a downstream epic — E-15, E-22 — discovers a template that requires capacity-aware allocation). | Med | The deferred-gap framing names class-2 as future engine work, not "we won't ship it"; if a downstream epic needs it, that epic owns scoping a class-2 surfacing milestone. The policy itself is forward-compatible (M-069 AC-2's compile detector is coded with class-2 as a future authority surface, not requiring a refactor to add). |
| The original G-032 authority decision (in its narrow framing) goes a way that requires significant engine-semantics rework — out-of-scope creep into general edge-volume computation. | Med | The footprint analysis preserved in M-066's spec body documents engine-LOC bands per option; if the chosen option lands as `large` or larger, split into multi-milestone implementation phase rather than extending E-25 indefinitely. The ADR names what the engine work commits to; anything beyond becomes a follow-up gap. |
| Option 1 (edge weights win) requires editing 8+ shipped templates; mistakes during the edit produce silent semantic shifts in the templates that ship to users. | Med | The golden-output canary is built first against the **post-fix** state, so every template edit is reviewed against pinned numeric expectations. The Phase 2 baseline canary stays green throughout. |
| The golden-output canary's tolerance is set too tight and the canary becomes a regeneration-tax on benign engine work. | Med | Empirical tolerance-setting inside the canary milestone, documented clearly. Tolerance can be widened per-template if the engine has known acceptable variance there. |
| The golden-output canary's tolerance is set too loose and real semantic drift slips through. | Med | The canary's failure-mode test (deliberate engine perturbation → canary fires) is part of the canary milestone's AC. |
| The pinned fixtures are forgotten/skipped during a sanctioned engine change and stale fixtures cause confusing CI failures elsewhere. | Low | The regeneration workflow is documented, and its invocation produces a PR-reviewable diff that an engineer must consciously commit. |
| Numeric output is non-reproducible across platforms (Linux dev container vs. CI vs. local macOS) at the chosen tolerance. | Med | Capture the pinned fixtures from a designated CI environment; document which environment is canonical; widen tolerance only if cross-platform variance is shown empirically. |
| The Phase 2 baseline canary (D-053) and the new golden canary develop conflicting expectations during the transition (e.g. baseline allows N warnings, golden expects exact zero). | Low | The epic's success criteria explicitly require both gates green simultaneously; the implementation milestone lands the engine + template fix + baseline reset + golden pinning in coordinated commits. |

## Milestones

<!-- Sequencing rationale: policy first (the ADR commits the engine to a flow-theoretic model that drives every downstream layer). Enforcement gates second (make the policy real at schema/compile/analyse layers; existing templates fail; failure is the proof that the gates work). Engine + template + baseline reset third (fix the templates so they pass under the new gates; engine code reflects the policy). Golden canary fourth (built on top of an engine whose output is now both policy-conformant and trustworthy). Note: ids do not encode execution order in aiwf v3; depends_on does. M-069 was added after M-066/M-067/M-068 were already allocated, so the dependency chain is M-066 → M-069 → M-067 → M-068. -->

- [M-066 — **Flow-Authority Policy Spike**](M-066-edge-flow-authority-decision.md) — three-class taxonomy doc, repo-wide doc sweep, ratified ADR naming the policy and enforcement points, deferred class-2 gap. Closes the G-032 design question. · depends on: —
- [M-069 — **Schema + Compile + Analyse Enforcement**](M-069-schema-compile-analyse-enforcement.md) — schema rule rejecting consumer-side peer-split, compile-time fan-out routing-authority detector, analyser warnings `routing_authority_ambiguous` + `consumer_side_peer_split_detected`, deliberately-broken fixture coverage. · depends on: M-066
- [M-067 — **Engine + template alignment**](M-067-engine-template-alignment.md) — implement the chosen authority in the engine, edit affected shipped templates so they pass M-069's gates, reset `ExpectedRunWarnings` entries to zero, add `val-warn` delta gate to the existing canary. · depends on: M-069
- [M-068 — **Golden-output canary**](M-068-golden-output-canary.md) — fixture infrastructure, regeneration workflow, initial pinning across the shipped template set, documentation. Closes G-033. · depends on: M-067

Detailed per-milestone acceptance criteria are filled in the milestone specs above; original three-milestone sequence planned 2026-05-02; widened to four milestones on 2026-05-02 after the M-066 framing widened to a flow-authority policy spike.


## ADRs produced

- **ADR-NNNN — Flow-Authority Policy.** Names the routing-authority policy across the three classes of physical systems (class 1 dynamic routing, class 2 capacity-aware allocation, class 3 static-weight); commits to edge-weights-normative for class-3, router-nodes for class-1, class-2 not surfaced today (deferred); rejects consumer-side expr arithmetic encoding peer-relative splits; names the schema/compile/analyse enforcement points. Allocated and ratified by M-066. Seeds the previously-empty `docs/adr/` directory.

## References

- Gap [G-032](../../gaps/G-032-transportation-basic-regressed-edge-flow-mismatch-incoming-3-after-e-24-unification.md) — the regression that surfaced the latent inconsistency; full investigation history and the three authority options.
- Gap [G-033](../../gaps/G-033-tests-are-too-weak-surveyed-output-only-canaries-cannot-detect-drift-need-deterministic-golden-output-assertions.md) — the structural argument for a golden-output canary; proposed shape.
- Decision [D-053](../../decisions/D-053-testing-rigor-approach-phase-2-baseline-canary-first-full-golden-output-canon-deferred.md) — Phase 2 baseline canary; deferred full golden canon (this epic picks up that deferred scope).
- Decision [D-045](../../decisions/D-045-svelte-ui-fork-platform-for-all-new-telemetry-fit-discovery-surfaces-blazor-enters-maintenance-mode.md) — Option A delivery sequence (E-21 → E-15 → Telemetry Loop & Parity → E-22); E-25 sits between E-21 close and E-15 / E-22 start as the engine truth gate.
- Analyser source: `src/FlowTime.Core/Analysis/InvariantAnalyzer.cs:323-335` (incoming-edge conservation) and `:309-321` (outgoing-edge conservation; both warning families).
- Existing baseline canary: `tests/FlowTime.Integration.Tests/TemplateWarningSurveyTests.cs` — `ExpectedRunWarnings` dictionary at `:79`, assertion at `:340-358`.
- Reproduction artifacts (preserve until E-25 ships): `data/runs/run_20260424T150244Z_b2f4c995/run.json` (clean baseline, pre-E-24) and `data/runs/run_20260428T165413Z_6ed5974e/run.json` (post-E-24 regression).
- Affected shipped templates (current baselines): G-032 "Affected templates" table.
- Downstream consumers: E-22 (`work/epics/E-22-time-machine-model-fit-chunked-evaluation/epic.md`) and E-15 (`work/epics/E-15-telemetry-ingestion-topology-inference-and-canonical-bundles/epic.md`).
