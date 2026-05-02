---
id: E-25
title: Engine Truth Gate — Edge-Flow Authority + Golden-Output Canary
status: proposed
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

- **Edge-flow authority design call.** Resolve the G-032 question (expr nodes vs. topology edge weights vs. both-must-agree) as a project-scoped decision (`D-NNN`). This is an Engine architecture call documented in the gap as having three options with materially different blast radii.
- **Engine implementation of the chosen authority.** Whatever authority wins, the engine + analyser + emitted edge-flow series must reflect it consistently; the conservation invariant must produce clean output on the shipped template set under default parameters.
- **Template alignment.** Edit each affected shipped template (the templates listed in G-032's "Affected templates" table) so that `ExpectedRunWarnings` baselines drop to zero in the same commit that lands the engine change. Where an authority forces template-form changes, those edits land here.
- **Golden-output canary infrastructure.** A test harness under `tests/FlowTime.Integration.Tests/` that, for each shipped template, runs the engine at a pinned parameter set and compares the full output bundle against a pinned fixture: per-series per-bin numeric values (tolerance comparison), full warning set (codes + messages + node/edge ids + severity), run manifest summary fields. Tolerance and serialization format are design choices for the milestone.
- **Initial pinning across the shipped template set.** For each shipped template (12 today; the canonical list lives in `templates/` and is enumerated by `Survey_Templates_For_Warnings`), capture the expected output bundle once at a sanctioned commit post-engine-fix, store under `tests/fixtures/golden-templates/<template-id>/` with a README documenting parameter set, capture date, and capture commit hash. The list of templates pinned must equal the list `Survey_Templates_For_Warnings` enumerates — drift between the two is itself a build failure.
- **Sanctioned-regeneration workflow.** A `--regenerate` (or equivalent) mode that, given an explicit opt-in, re-captures the bundles after a sanctioned engine change. The diff is the PR-review artifact. Pattern: dotnet snapshot-update style.
- **`val-warn` delta gate as bridge canary.** Until the full golden canary is green across the template set, extend the existing `Survey_Templates_For_Warnings` to also fail on `val-warn` count drift (D-053 currently asserts on `run-warn` only). This is a small change inside the test that already exists.
- **Documentation.** A short authority document in `docs/architecture/` (or extending `docs/architecture/headless-engine-architecture.md`) capturing the edge-flow authority decision and the conservation invariant's contract; a `docs/testing/golden-output-canary.md` document covering the canary, the regeneration workflow, and the meaning of a green build.

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

- **Decision-first sequencing.** The edge-flow authority `D-NNN` is ratified before the engine + template implementation milestone begins. Implementation that pre-empts the design call is forbidden. (Risk-management rationale: the three options have materially different blast radii — option 1 forces 8+ template edits, option 2 changes engine semantics for an entire class of models, option 3 makes templates more verbose. Picking wrong here costs an epic-scale rework.)
- **No coexistence window for the chosen authority.** Once the design call is made and the engine implements it, the rejected option is not retained as a tolerated alternate path. Per project truth-discipline: do not keep "temporary" compatibility shims without explicit deletion criteria.
- **Forward-only template regeneration.** If the chosen authority forces template-form changes, all affected shipped templates land their edits in the same change. No "some templates new form, some old form" coexistence.
- **Phase 2 baseline canary stays green throughout.** D-053's `Survey_Templates_For_Warnings` `run-warn` baseline gate is not loosened during the work; baseline values change only as templates legitimately regress to zero, never to higher numbers.
- **Golden-canary serialization is reviewable.** Pinned fixtures are stored in a format that produces a meaningful PR diff (not an opaque binary). JSON-with-stable-key-order or similar; design choice for the canary milestone.
- **Tolerance values are documented and conservative.** The numeric tolerance the canary uses must be explicit (in code or the canary doc) and chosen so legitimate engine fixes don't constantly tax the regeneration workflow, but real semantic drift fails fast. Default candidate: relative tolerance 1e-9 against double-precision bins; revisited at the canary milestone.
- **`val-err == 0` and `run-warn` baselines remain hard gates.** No regressions to the existing gate posture.
- **Project rules.** .NET 9 / C# 13; invariant culture; camelCase JSON payloads; private fields without leading underscore.

## Success criteria

<!-- Reference-phrased; counts that drift over time are pulled from referenced lists, not reproduced inline. -->

- [ ] G-032 status promoted to `addressed` and references this epic; G-033 status promoted to `addressed` and references this epic.
- [ ] A ratified `D-NNN` (or ADR if the engine team decides architectural-record-class is warranted) names the edge-flow authority and documents the rejected options + rationale.
- [ ] The engine implementation reflects the chosen authority; on every shipped template enumerated by `Survey_Templates_For_Warnings`, running at default parameters produces zero `edge_flow_mismatch_incoming` and zero `edge_flow_mismatch_outgoing` warnings.
- [ ] Every entry in `ExpectedRunWarnings` (in `tests/FlowTime.Integration.Tests/TemplateWarningSurveyTests.cs`) sourced from the G-032 affected-templates table is reset to zero in the same commit that lands the engine + template change. Other entries (if any) are individually justified.
- [ ] A golden-output canary asserts, for every shipped template enumerated by `Survey_Templates_For_Warnings`, that the engine's full output bundle (per-series per-bin within tolerance, full warning set exactly, manifest summary fields) matches a pinned fixture committed under `tests/fixtures/golden-templates/<template-id>/`.
- [ ] The set of template ids covered by the golden canary equals the set enumerated by `Survey_Templates_For_Warnings`; coverage drift fails the build.
- [ ] Each pinned fixture directory contains a README naming the parameter set, capture date, and capture commit hash.
- [ ] A sanctioned-regeneration workflow exists: an engineer can run a documented command (e.g. `dotnet test --filter ... --update-fixtures` or equivalent) and re-capture the pinned bundles; the resulting diff is the PR-review artifact.
- [ ] `Survey_Templates_For_Warnings` is extended to fail on `val-warn` delta as well as `run-warn` delta; both gates active simultaneously.
- [ ] Documentation: an architecture note captures the edge-flow authority decision; a testing note captures the golden-output canary, the regeneration workflow, and the meaning of a green build.
- [ ] Full repo test suite is green at epic close; no skipped tests added by this work that aren't explicitly gated on infrastructure (e.g. `FLOWTIME_E2E_TEST_RUNS=1` style).
- [ ] On epic completion, `ROADMAP.md` is regenerated; epic dir is moved to `work/epics/completed/E-25-…/` per project convention; a wrap artefact under that dir captures what shipped, the pinned-fixture catalog state, and any deferred follow-ups.

## Open questions

| Question | Blocking? | Resolution path |
|---|---|---|
| Edge-flow authority — expr nodes win, edge weights win, or both-must-agree (G-032 options 1/2/3)? | **Yes** | Design milestone (m-E25-01) produces a `D-NNN` (or ADR) ratifying one option; epic implementation milestones gate on it. |
| ADR or D-NNN for the authority decision? | No | Default to D-NNN consistent with this repo's pattern (the `docs/adr/` dir is empty; D-053 is the most recent comparable architectural call). Promote to ADR only if the engine team flags it as needing a more permanent home. |
| Numeric tolerance for the golden canary — absolute, relative, hybrid; what magnitude? | No | Decided inside the canary milestone with empirical evidence (run several known-clean templates, measure observed bin-to-bin variance under repeat runs, set tolerance with margin). Default starting point: relative 1e-9. |
| Pinned-fixture serialization format — JSON, CSV+JSON-warnings, MessagePack? | No | Decided inside the canary milestone. Constraint: must produce reviewable PR diffs. JSON-with-stable-key-order is the strawman. |
| Should the golden canary live in `tests/FlowTime.Integration.Tests/` alongside `Survey_Templates_For_Warnings`, or in its own project? | No | Decided inside the canary milestone. Default: same project, sibling test class. |
| When the chosen authority forces template-form changes, do user-authored templates outside `templates/` need a migration tool? | No | Out of scope at epic close (we ship only the shipped-template set); but the question is logged as a deferred-follow-up gap if the authority decision creates user-facing template breakage. |
| Does the `val-warn` delta gate also need a baseline dictionary like `ExpectedRunWarnings`? | No | Decided inside the bridge-canary work. If the engine + template fix lands `val-warn == 0` for all templates, the gate is a hard zero. If not, a baseline dictionary mirrors the existing pattern. |

## Risks

| Risk | Impact | Mitigation |
|---|---|---|
| The G-032 authority decision goes a way that requires significant engine-semantics rework (option 2) — out-of-scope creep into general edge-volume computation. | High | The design milestone's deliverable is the decision **plus** the implementation footprint analysis; if option 2 lands and the engine work exceeds an implementation-milestone scope, split into a multi-milestone implementation phase rather than extending E-25 indefinitely. The decision record names what the engine work commits to; anything beyond becomes a follow-up gap. |
| Option 1 (edge weights win) requires editing 8+ shipped templates; mistakes during the edit produce silent semantic shifts in the templates that ship to users. | Med | The golden-output canary is built first against the **post-fix** state, so every template edit is reviewed against pinned numeric expectations. The Phase 2 baseline canary stays green throughout. |
| The golden-output canary's tolerance is set too tight and the canary becomes a regeneration-tax on benign engine work. | Med | Empirical tolerance-setting inside the canary milestone, documented clearly. Tolerance can be widened per-template if the engine has known acceptable variance there. |
| The golden-output canary's tolerance is set too loose and real semantic drift slips through. | Med | The canary's failure-mode test (deliberate engine perturbation → canary fires) is part of the canary milestone's AC. |
| The pinned fixtures are forgotten/skipped during a sanctioned engine change and stale fixtures cause confusing CI failures elsewhere. | Low | The regeneration workflow is documented, and its invocation produces a PR-reviewable diff that an engineer must consciously commit. |
| Numeric output is non-reproducible across platforms (Linux dev container vs. CI vs. local macOS) at the chosen tolerance. | Med | Capture the pinned fixtures from a designated CI environment; document which environment is canonical; widen tolerance only if cross-platform variance is shown empirically. |
| The Phase 2 baseline canary (D-053) and the new golden canary develop conflicting expectations during the transition (e.g. baseline allows N warnings, golden expects exact zero). | Low | The epic's success criteria explicitly require both gates green simultaneously; the implementation milestone lands the engine + template fix + baseline reset + golden pinning in coordinated commits. |

## Milestones

<!-- Sequencing rationale: design call first (decision before implementation, per the constraint). Engine + template + baseline reset second (the change-set whose validity the design call defines). Golden canary third — built on top of an engine whose output is now trustworthy, so the pinned fixtures capture truth-as-decided rather than truth-with-known-asterisks. The design milestone can be small (a focused investigation + decision record); the implementation milestone is the largest. The canary milestone has its own depth in fixture authoring and regeneration tooling. -->

- m-E25-01 — **Edge-flow authority decision** — gather signal, write the `D-NNN`, ratify. Closes the G-032 design question. · depends on: —
- m-E25-02 — **Engine + template alignment** — implement the chosen authority in the engine, edit affected shipped templates, reset `ExpectedRunWarnings` entries to zero, add `val-warn` delta gate to the existing canary. · depends on: m-E25-01
- m-E25-03 — **Golden-output canary** — fixture infrastructure, regeneration workflow, initial pinning across the shipped template set, documentation. Closes G-033. · depends on: m-E25-02

Milestones will be sequenced in detail (with per-milestone ACs) via `aiwfx-plan-milestones` after this epic spec is reviewed.

## ADRs produced

- (none yet — the edge-flow authority decision will be captured as `D-NNN` per repo convention; promoted to ADR only if the engine team flags the need for a more permanent record.)

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
