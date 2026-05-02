---
id: M-066
title: Edge-Flow Authority Decision
status: in_progress
parent: E-25
acs:
    - id: AC-1
      title: Footprint analysis documented for all three options
      status: open
    - id: AC-2
      title: Option survey covers all affected shipped templates
      status: open
    - id: AC-3
      title: D-NNN drafted naming chosen authority
      status: open
    - id: AC-4
      title: Rejected options recorded with rationale
      status: open
    - id: AC-5
      title: Implementation footprint estimate captured in the decision
      status: open
    - id: AC-6
      title: Deferred follow-ups filed as gaps where needed
      status: open
    - id: AC-7
      title: D-NNN ratified with status accepted
      status: open
    - id: AC-8
      title: Epic spec and gap G-032 reference the ratified D-NNN
      status: open
    - id: AC-9
      title: aiwf check clean
      status: open
---

## Goal

Produce a ratified project-scoped decision (`D-NNN`) that names the **edge-flow authority** for the FlowTime engine — i.e., which surface (expr nodes, topology edge weights, or a mandated agreement of both) is canonical for incoming-edge flow volumes. The deliverable is the decision record and the footprint analysis that supports it; no engine code, no template edits, no test changes ship from this milestone.

## Context

E-21 M-045 dogfooding (2026-04-28) caught three `edge_flow_mismatch_incoming` warnings on `transportation-basic`. The patch investigation `patch/edge-flow-mismatch` (2026-05-01) diagnosed the warnings as **improved analyser coverage**: pre-E-24 the engine wasn't emitting per-edge `flowVolume` series, so the conservation check at `src/FlowTime.Core/Analysis/InvariantAnalyzer.cs:323-335` silently skipped; post-E-24 the series exists, the check runs, and it correctly detects expr-layer arrivals (`hub_dispatch * splitAirport`, parameter-driven splits) diverging from the engine's edge-weight-uniform apportionment (every queue→line edge declared with `weight: 1`).

Per G-032 the divergence affects **9 of the 12 shipped templates** and has three resolution options with materially different blast radii:

1. **Edge weights win.** Templates must encode splits in edge weights, not in expr arithmetic. ~9 templates need editing; engine semantics unchanged.
2. **Expr authority wins.** The engine should not auto-derive edge flow from weights when an expr node already produces the receiver's `arrivals` series. Engine semantics change for an entire class of models; templates as-is are correct.
3. **Both surfaces are normative; the template makes them agree.** Templates use parameters in BOTH places (expr splits AND edge weights); tooling helps keep them in sync. More verbose templates; tooling needed.

Picking wrong here costs an epic-scale rework. The epic constraint requires this decision to be **ratified before m-E25-02 starts**. This milestone owns the call and the footprint analysis that justifies it; it does not own implementation.

The decision form is a `D-NNN` per repo convention (D-053 is the most recent comparable architectural call; `docs/adr/` is empty). Promotion to ADR is allowed if the team flags it as needing a more permanent record, but the default is `D-NNN`. The milestone deliberately scopes that promotion-or-not as a closeout question, not a blocker.

## Acceptance criteria

### AC-1 — Footprint analysis documented for all three options

For each of the three options enumerated in G-032 (edge weights win / expr authority wins / both-must-agree), the decision record (or its supporting tracking doc) captures:

- **Engine-LOC magnitude** — which files in `src/FlowTime.Core/` change, with rough line-count estimate (small / medium / large; concrete file list).
- **Affected-templates count** — exact number of shipped templates that need editing under each option, sourced from G-032's "Affected templates" table (the 9 listed) and confirmed against `ExpectedRunWarnings` in `tests/FlowTime.Integration.Tests/TemplateWarningSurveyTests.cs:79`.
- **Expected baseline-zero achievability** — does the option produce `ExpectedRunWarnings == 0` for every shipped template, or are there templates where the option does not eliminate the conservation warning?
- **User-facing template-form impact** — does the option change the template authoring contract for templates outside the shipped set (i.e., user-authored templates in the wild)?

This footprint analysis is the technical evidence the chosen option stands on. It is independent of which option ultimately wins.

### AC-2 — Option survey covers all affected shipped templates

The survey explicitly walks the 9 affected templates from G-032's table — `transportation-basic`, `transportation-basic-classes`, `supply-chain-multi-tier`, `supply-chain-multi-tier-classes`, `it-system-microservices`, `manufacturing-line`, `network-reliability`, `supply-chain-incident-retry`, `warehouse-picker-waves` — and confirms whether each option resolves the conservation warning for each template, or notes any template where the option leaves residual warnings (which would need a per-template addendum).

### AC-3 — D-NNN drafted naming chosen authority

A `D-NNN` is added under `work/decisions/` via `aiwf add decision` (allocates the next `D-NNN` id). The decision body names the chosen authority unambiguously. The title is in the form `D-NNN — Edge-flow authority: <chosen option>`. The decision references G-032 and this epic.

### AC-4 — Rejected options recorded with rationale

The two rejected options are named explicitly in the decision body, each with a one-paragraph rationale citing the footprint analysis from AC-1. "Rejected because too expensive" without footprint citation is not sufficient. Future readers should be able to read the decision and reconstruct why option A beat options B and C without needing to hunt through patch notes.

### AC-5 — Implementation footprint estimate captured in the decision

The chosen option's footprint estimate from AC-1 is restated inline in the decision (engine-LOC magnitude, affected-templates count, expected baseline-zero achievability). m-E25-02's planning starts from this number. If the implementation overruns the estimate by more than a milestone's worth of work, that triggers a re-scope conversation per the epic's risk table mitigation for "engine work exceeds an implementation-milestone scope".

### AC-6 — Deferred follow-ups filed as gaps where needed

If the chosen authority creates user-facing template breakage outside the shipped set (e.g., the chosen option changes the template authoring contract in a way that obsoletes existing user-authored templates), a deferred-follow-up gap is filed via `aiwf add gap` capturing the migration concern. The decision references the gap by id. If no user-facing breakage exists, the decision states that explicitly. The point is to make the user-impact analysis explicit either way.

### AC-7 — D-NNN ratified with status accepted

The decision is promoted from `proposed` to `accepted` via `aiwf promote D-NNN accepted` after user review. The ratified status is the gate that unlocks m-E25-02; m-E25-02's planning AC explicitly cites the ratified `D-NNN`.

### AC-8 — Epic spec and gap G-032 reference the ratified D-NNN

The epic spec at `work/epics/E-25-engine-truth-gate/epic.md` is updated so its "Open questions" table marks the edge-flow authority question as resolved with a link to the `D-NNN`. G-032's status moves to `addressed` with a reference to the `D-NNN` (the gap is fully closed only when m-E25-02 lands the implementation; this milestone closes the design portion).

### AC-9 — aiwf check clean

After the decision is added and statuses are updated, `aiwf check` reports `ok — no findings`. No frontmatter drift, no orphaned references.

## Constraints

- **No engine code change.** This milestone delivers a decision, not an implementation. Any code touched in `src/FlowTime.Core/` outside the decision-supporting tracking artifact is out of scope.
- **No template edits.** No file under `templates/` is modified by this milestone. Template surveys may *read* templates to compute footprint estimates, but no edits land.
- **No `ExpectedRunWarnings` changes.** The Phase 2 baseline canary stays exactly as committed by the patch on 2026-05-01 throughout this milestone. Resetting baselines is m-E25-02's job, conditional on the engine + template change landing.
- **`D-NNN` preferred; ADR allowed if explicitly flagged.** The default is `D-NNN` for project-scoped decisions per repo convention. If during the milestone's review the team decides the authority call deserves the more permanent ADR home (`docs/adr/ADR-NNNN-…`), promote then; do not pre-empt the choice. Either way, the AC-3 deliverable is one ratified record — not both.
- **Footprint analysis is honest, not advocacy.** AC-1 documents all three options with comparable rigor. The footprint table should let a reader who hasn't been part of the conversation see why the chosen option won. Stacking the deck (e.g., "rejected option B has unbounded scope" without supporting evidence) defeats the purpose.
- **Project rules.** .NET 9 / C# 13 (no code in this milestone, but the conventions apply to any code-cited line ranges); invariant culture; no time or effort estimates in the decision body.

## Design Notes

- **Tracking doc is optional but recommended.** A sibling `m-E25-01-edge-flow-authority-tracking.md` in the epic folder is a sensible home for the footprint analysis tables (per the `aiwfx-track` skill convention). The `D-NNN` then references the tracking doc for the long-form analysis and keeps its own body to the decision narrative. Alternatively the analysis lives inline in the `D-NNN`. Either pattern is acceptable; the choice is stylistic.
- **Footprint estimation methodology.** For each option, walk one representative template under that option's resolution (e.g., for option 1 on `transportation-basic`, draft what the edited template would look like and count the diff). Extrapolate across the 9 affected templates. Do not full-spec all 9 edits in this milestone — that's m-E25-02's work. Goal here is order-of-magnitude estimation.
- **Engine-LOC magnitude bands.** Use coarse buckets: `small` (< 50 LOC, single file), `medium` (50–300 LOC, 1–3 files), `large` (300+ LOC or multi-subsystem). Anything `large` triggers the epic risk-table mitigation per AC-5.
- **Survey artifact format.** A simple markdown table in the decision body (or tracking doc) with columns `option | engine-LOC | templates-edited | baseline-zero achievable? | user-facing impact`. Three rows.

## Surfaces touched

- `work/decisions/D-NNN-<slug>.md` (new — the ratified decision)
- `work/epics/E-25-engine-truth-gate/m-E25-01-edge-flow-authority-tracking.md` (new, optional — long-form footprint analysis if the decision body is too tight to host it)
- `work/epics/E-25-engine-truth-gate/epic.md` (small edit — open-questions table update)
- `work/gaps/G-032-…md` (status promotion to `addressed`; reference the D-NNN)
- `ROADMAP.md` (regenerated via `aiwf render roadmap --write`)

## Out of scope

- Engine code change (m-E25-02).
- Template edits (m-E25-02).
- `ExpectedRunWarnings` baseline reset (m-E25-02).
- Golden-output canary infrastructure or fixtures (m-E25-03).
- Migration tooling for user-authored templates affected by the chosen authority — flagged as a deferred follow-up per AC-6 if applicable; the tool itself is not built here.
- Any change to the `Survey_Templates_For_Warnings` test (m-E25-02 owns the val-warn delta gate addition).
- Engine-team architecture review pass beyond what is needed to ratify the chosen option. If the review surfaces broader engine-architecture questions (e.g., conservation tolerance reshaping, new analyser warning families), those are filed as deferred gaps and not absorbed into this milestone.

## Dependencies

- E-25 epic spec ratified (in place — see `work/epics/E-25-engine-truth-gate/epic.md`).
- Patch `patch/edge-flow-mismatch` merged (in place — Phase 2 baseline canary committed 2026-05-01).
- G-032 in `addressed` status pending this decision's resolution.

## References

- Epic spec: `work/epics/E-25-engine-truth-gate/epic.md`
- Gap: `work/gaps/G-032-transportation-basic-regressed-edge-flow-mismatch-incoming-3-after-e-24-unification.md` — the three options and their per-template impact table
- Phase 2 baseline canary: `tests/FlowTime.Integration.Tests/TemplateWarningSurveyTests.cs:79` (the `ExpectedRunWarnings` dictionary)
- Decision precedent: `work/decisions/D-053-testing-rigor-approach-phase-2-baseline-canary-first-full-golden-output-canon-deferred.md`
- Analyser source: `src/FlowTime.Core/Analysis/InvariantAnalyzer.cs:323-335` (incoming-edge conservation; the rule the authority decision binds)
