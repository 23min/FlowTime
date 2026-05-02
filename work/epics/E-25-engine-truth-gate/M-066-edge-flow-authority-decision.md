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

For each of the three options enumerated in G-032 (edge weights win / expr authority wins / both-must-agree), the decision record captures:

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

- **No engine code change.** This milestone delivers a decision, not an implementation. Any code touched in `src/FlowTime.Core/` is out of scope.
- **No template edits.** No file under `templates/` is modified by this milestone. Template surveys may *read* templates to compute footprint estimates, but no edits land.
- **No `ExpectedRunWarnings` changes.** The Phase 2 baseline canary stays exactly as committed by the patch on 2026-05-01 throughout this milestone. Resetting baselines is m-E25-02's job, conditional on the engine + template change landing.
- **`D-NNN` preferred; ADR allowed if explicitly flagged.** The default is `D-NNN` for project-scoped decisions per repo convention. If during the milestone's review the team decides the authority call deserves the more permanent ADR home (`docs/adr/ADR-NNNN-…`), promote then; do not pre-empt the choice. Either way, the AC-3 deliverable is one ratified record — not both.
- **Footprint analysis is honest, not advocacy.** AC-1 documents all three options with comparable rigor. The footprint table should let a reader who hasn't been part of the conversation see why the chosen option won. Stacking the deck (e.g., "rejected option B has unbounded scope" without supporting evidence) defeats the purpose.
- **Project rules.** .NET 9 / C# 13 (no code in this milestone, but the conventions apply to any code-cited line ranges); invariant culture; no time or effort estimates in the decision body.

## Design Notes

- **Footprint estimation methodology.** For each option, walk one representative template under that option's resolution (e.g., for option 1 on `transportation-basic`, draft what the edited template would look like and count the diff). Extrapolate across the 9 affected templates. Do not full-spec all 9 edits in this milestone — that's m-E25-02's work. Goal here is order-of-magnitude estimation.
- **Engine-LOC magnitude bands.** Use coarse buckets: `small` (< 50 LOC, single file), `medium` (50–300 LOC, 1–3 files), `large` (300+ LOC or multi-subsystem). Anything `large` triggers the epic risk-table mitigation per AC-5.
- **Survey artifact format.** A simple markdown table in the decision body with columns `option | engine-LOC | templates-edited | baseline-zero achievable? | user-facing impact`. Three rows.

## Footprint analysis (M-066's working artefact)

### Methodology

For each option, the analysis answers four questions:

1. **Engine-LOC magnitude** — which `src/FlowTime.Core/` files change, with magnitude band: `small` (< 50 LOC, single file), `medium` (50–300 LOC, 1–3 files), `large` (300+ LOC or multi-subsystem).
2. **Affected-templates count** — exact templates needing edits, sourced from G-032's table and confirmed against `ExpectedRunWarnings`.
3. **Baseline-zero achievability** — does the option drop every entry in `ExpectedRunWarnings` (sourced from G-032) to `0`, or are there templates where residual warnings persist?
4. **User-facing impact** — does the option change the template authoring contract for templates outside the shipped set?

Per AC-2 the survey explicitly walks all 9 affected shipped templates from G-032's table. The footprint estimate is order-of-magnitude only — not a full implementation spec; that is M-067's job.

### Affected templates (G-032 baseline, captured 2026-05-01)

Sourced from `ExpectedRunWarnings` and G-032's "Affected templates" table:

| Template | Run-warn baseline | Mismatch source typology |
|---|---|---|
| `transportation-basic` | 1 | Parameter-driven 3-way split: `hub_dispatch_{airport,industrial,downtown} = hub_dispatch * splitX`. Three queue→line edges, all `weight: 1` (default, implicit). |
| `transportation-basic-classes` | 8 | Worst offender. Has explicit `kind: router` on `HubDispatchRouter` plus three downstream `*DispatchQueue` nodes; uses class-aware route splits **and** post-router weight-1 edges to lines. Eight-fan-out warning count comes from per-edge mismatch on multiple node hops. |
| `supply-chain-multi-tier` | 2 | Multi-tier supplier→DC→retailer flow with expr-driven tier-share splits, edges left at `weight: 1`. Two mismatches at the two split-receiver nodes. |
| `supply-chain-multi-tier-classes` | 2 | Same shape as multi-tier, with class segmentation. |
| `it-system-microservices` | 1 | One service node where expr-derived arrivals diverge from edge-weight-uniform apportionment. |
| `manufacturing-line` | 1 | WIP queue → quality control → packaging chain; expr-driven scrap fractions on output mean the inflow to packaging is computed in expr, not derivable from edge weights. |
| `network-reliability` | 1 | Single-edge `RequestQueue → CorePlatform`, but `CorePlatform.arrivals` is wired to `request_queue_served` via an expr (`MIN(server_capacity, request_queue_demand)`), so the edge-weight-derived materializer flow (`request_queue_served * 1`) and the analyser-observed `arrivals` series can drift inside the lag/CONV semantics. |
| `supply-chain-incident-retry` | 1 | Incident-spike retry path where retry attempts feed back into a service whose `arrivals` is computed in expr. |
| `warehouse-picker-waves` | 1 | Wave-driven dispatch where picker capacity gates served, but downstream wave-completion `arrivals` is expr-derived. |

The remaining shipped templates (`dependency-constraints-attached`, `dependency-constraints-minimal`, `it-document-processing-continuous`) are at baseline 0 today and stay at 0 under all three options (no expr/edge-weight authority conflict in their topology).

### Option footprint summary

| Option | Engine-LOC | Templates edited | Baseline-zero achievable? | User-facing impact |
|---|---|---|---|---|
| **1 — Edge weights win** | `small` (< 50 LOC; doc-only adjustments to message text + an analyser comment block) | 9 | **Yes** — for 8/9 cleanly. `network-reliability` may need verification (residual analyser tolerance margin under CONV-driven lag); see footnote. | **Yes** — user templates that encode splits via expr arithmetic on a fan-out node will fire the warning. Migration is mechanical: move the split fraction from the expr math into the edge `weight:` field. |
| **2 — Expr authority wins** | `medium` (50–300 LOC; 2-3 files: `EdgeFlowMaterializer.cs` plus `InvariantAnalyzer.cs` skip-when-expr-authoritative branch, possibly `RouterSpecificationBuilder.cs`) | 0 | **Yes** for all 9 — the analyser stops flagging the case it can no longer detect; the materializer emits expr-derived series. | **No** breakage to existing templates. But: changes engine semantics for an entire class of models — edge weights become advisory when the target's `arrivals` series is expr-computed. |
| **3 — Both must agree** | `medium` (50–150 LOC; new analyser warning code + new template-validator rule that detects redundancy/disagreement; possibly a tooling helper) | 9 (additions only, ~45-60 LOC total) | Conditional — depends on tolerance and parameter-expression reproducibility | New authoring discipline (declare both surfaces); tooling required; more verbose templates |

> **Footnote on `network-reliability` under option 1:** This template's mismatch is not a fan-out split; it is a single-edge `RequestQueue → CorePlatform` whose mismatch is driven by expr-layer CONV/lag semantics on `request_queue_served`, not by missing weight encoding. Setting `weight: 1` (already the default) on a single edge does not change the materializer output. Resolving this template's warning under option 1 may require either a small per-template expr restructure (route via the materializer's arithmetic, not via expr re-derivation) or accepting it as a residual warning with an explicit `ExpectedRunWarnings` entry. Verifying whether option 1 cleans this template to zero requires actually running the edited template — out of scope for this decision milestone but flagged for M-067 as a likely per-template addendum.

### Per-option deep dive

#### Option 1 — Edge weights win

**Authority statement.** Edge weights in the `topology.edges[].weight` field are canonical for incoming-edge flow volumes. When an expr node's arithmetic produces a value that a downstream node consumes as `arrivals`, the expr is documentation; the edge weight is what the engine apportions against. Templates must encode their splits in edge weights.

**Engine change.**

- **No semantic change.** The `EdgeFlowMaterializer` already does weight-based apportionment (`Routing/EdgeFlowMaterializer.cs:172-188, 235-251`). The materializer produces `flowVolume` per edge by `weight / totalWeight * source_served`. That is the chosen authority's output today — nothing to change.
- **Analyser comment update.** `InvariantAnalyzer.cs:323-335` already does the right thing — it asserts `arrivals == sum(incoming_edges_after_lag)`. Update the surrounding doc comment to name the chosen authority (edge weights) so future readers don't reverse-engineer it.
- **Optional message text refinement.** The warning message at `:330` (`"Arrivals do not match sum of incoming edge flows"`) can be made more diagnostic.

Estimate: < 30 LOC, single file, doc-only. Magnitude **`small`**.

**Critical sub-question for option 1.** Does the engine expose `edge_<id>_flowVolume` as a node reference that an expr can consume? If yes, the rewrite below works directly. If not, the engine needs a small addition to make per-edge `flowVolume` series referenceable from `expr` nodes. The materializer at `Routing/EdgeFlowMaterializer.cs:54` already builds a `flowVolumes` dictionary keyed by edgeId; surfacing this as a referenceable namespace is a small engine change (~30-60 LOC, one file). M-067 should answer this for sure; for footprint purposes it nudges option 1's engine LOC from `small` to **upper-bound `medium`** if the wiring isn't already there. Worth flagging as the option's primary engine risk.

**Template change worked example (`transportation-basic`).** Today (excerpt):

```yaml
- id: hub_dispatch_airport
  kind: expr
  expr: "hub_dispatch * ${splitAirport}"   # 0.3
- id: hub_dispatch_industrial
  kind: expr
  expr: "hub_dispatch * ${splitIndustrial}"  # 0.2
- id: hub_dispatch_downtown
  kind: expr
  expr: "hub_dispatch - hub_dispatch_airport - hub_dispatch_industrial"

- id: arrivals_airport
  kind: expr
  expr: "hub_dispatch_airport"
# ...
```

Edges:

```yaml
- id: queue_to_airport
  from: HubQueue:out
  to: LineAirport:in
- id: queue_to_downtown
  from: HubQueue:out
  to: LineDowntown:in
- id: queue_to_industrial
  from: HubQueue:out
  to: LineIndustrial:in
```

Under option 1:

```yaml
# topology edges
- id: queue_to_airport
  from: HubQueue:out
  to: LineAirport:in
  weight: ${splitAirport}    # 0.3
- id: queue_to_downtown
  from: HubQueue:out
  to: LineDowntown:in
  weight: 0.5
- id: queue_to_industrial
  from: HubQueue:out
  to: LineIndustrial:in
  weight: ${splitIndustrial}  # 0.2

# nodes — arrivals_X reference materializer-emitted edge flow
- id: arrivals_airport
  kind: expr
  expr: "edge_queue_to_airport_flowVolume"
- id: arrivals_industrial
  kind: expr
  expr: "edge_queue_to_industrial_flowVolume"
- id: arrivals_downtown
  kind: expr
  expr: "edge_queue_to_downtown_flowVolume"
```

Diff size: ~10-15 lines per template. The `hub_dispatch_{airport,industrial,downtown}` intermediate nodes either become aliases or are deleted. Outputs that referenced them need their arithmetic updated.

**Templates edited.**

| Template | Estimated diff size | Notes |
|---|---|---|
| `transportation-basic` | ~15 LOC | Fan-out 3, parameter-driven splits. Mechanical. |
| `transportation-basic-classes` | ~30-40 LOC | Worst case: `kind: router` plus three downstream queues plus class-aware splits. Need to verify router-arm weights interact correctly with class assignments. |
| `supply-chain-multi-tier` | ~20 LOC | Tier-share split fan-out. Mechanical. |
| `supply-chain-multi-tier-classes` | ~25 LOC | Same as multi-tier with class layer. |
| `it-system-microservices` | ~10 LOC | Single split node. |
| `manufacturing-line` | ~10 LOC | Quality scrap fraction → edge weight. |
| `network-reliability` | **TBD** | Possibly residual under option 1; see footnote. May need expr restructure rather than weight edit. |
| `supply-chain-incident-retry` | ~10-15 LOC | Incident-retry feedback loop. |
| `warehouse-picker-waves` | ~10 LOC | Wave-completion fan-out. |

Total: ~150-180 LOC of template changes across 9 files. Each template's `ExpectedRunWarnings` entry resets to 0 in the same commit per epic constraint.

**Baseline-zero achievability:** Yes for 8 templates with high confidence; `network-reliability` flagged as possibly needing addendum.

**User-facing impact:** Real but bounded. User-authored templates that encode splits in expr arithmetic will start emitting `edge_flow_mismatch_incoming` warnings. Migration is mechanical: identify the expr split node feeding `arrivals_X`; move the split fraction onto the corresponding edge as `weight:`; replace the `arrivals_X` expr to reference the materialized edge flow series (or simplify to consume the upstream's `served`). This warrants a deferred-follow-up gap per AC-6 — a migration helper or template-linter rule.

#### Option 2 — Expr authority wins

**Authority statement.** When a target node's `arrivals` is computed by an expr that consumes the source node's `served` (directly or via a chain), the expr is canonical. The engine should not auto-derive edge `flowVolume` from edge weights in that case; instead, it should emit the expr-derived value as the edge series. Edge weights become advisory metadata for visualization but do not own the numeric flow.

**Engine change.**

1. **`Routing/EdgeFlowMaterializer.cs` (764 lines today).** Currently the non-router weight-driven block at `:155-265` does `series = ScaleSeries(baseSeries, fraction)` where `fraction = weight / totalWeight`. Option 2 replaces this with a lookup: for each edge, determine if the target node's `arrivals` semantic series is expr-defined and references a known source; if so, emit that expr-derived series directly. If not, fall back to weight-fraction. The "is expr authoritative" detection requires walking the target node's `arrivals` series and tracing its expr dependency tree. Estimated 80-150 new LOC plus dependency-tree-walk helper.
2. **`Analysis/InvariantAnalyzer.cs` (1864 lines today).** The conservation check at `:323-335` would need to know that the target's `arrivals` is expr-authoritative against this set of edges, and either skip the check or verify a different invariant. Estimated 30-60 LOC.
3. **Possibly `Compiler/RouterSpecificationBuilder.cs`.** If option 2 makes router weights also advisory, the router builder may need adjustment. Likely 0-50 LOC depending on how far the change reaches.

Total estimate: **`medium`**, 150-300 LOC, 2-3 files. Not `large` (the change doesn't ripple into the runtime evaluator or storage layer), but the materializer change is non-trivial — it changes how the engine apportions volume on a class of edges.

**Template changes.** Zero. Templates as authored today are correct under option 2. The 9 templates' `ExpectedRunWarnings` entries reset to 0 because the divergence stops existing once the materializer outputs expr-derived edge flows.

**Baseline-zero achievability:** Yes, all 9, by definition.

**User-facing impact:**

- No template breakage. User-authored templates continue to work without edits.
- Engine-semantics shift, with downstream consequences. Edge weights become advisory for any edge whose target has an expr-driven `arrivals`. This is a real semantic change:
  - Telemetry-replay (E-15) needs to know which authority applies when comparing observed flow to model flow.
  - Model fit (E-22) needs to know whether to fit edge weights or expr parameters when residuals appear.
  - The `weight: 1` defaults that pervade existing templates become essentially documentation; the engine ignores them when expr authority kicks in.
- No migration tooling needed.

#### Option 3 — Both must agree

**Authority statement.** Edge weights and expr-derived arrivals are both normative. Templates must author both surfaces and ensure they agree (modulo tolerance). The engine validates the agreement at compile time (template-validator) or at run time (analyser); the existing `edge_flow_mismatch_incoming` warning is repurposed as the agreement-check signal.

**Engine change.**

- **`Analysis/InvariantAnalyzer.cs` adjustments.** The conservation check at `:323-335` is already the agreement-check; option 3 keeps it but tightens its meaning. Possibly add a complementary `edge_flow_redundancy_missing` warning when only one authority is present. Estimated 30-80 LOC.
- **`ModelSchemaValidator.cs`** (or a new analyser pass) needs a redundancy-presence check: every fan-out edge whose target has an expr-driven `arrivals` must also have an explicit `weight:`. Estimated 30-60 LOC.
- No materializer change. Materializer continues weight-based flow; analyser ensures the expr matches.

Total estimate: **`medium`**, 50-150 LOC, 1-2 files.

**Template changes.** Add explicit `weight:` declarations on every fan-out edge that the expr layer encodes a split for. The expr arithmetic stays. Diff size: ~6 LOC added per simple template, ~12-15 LOC for `transportation-basic-classes`. Across 9 templates: ~45-60 LOC of additions.

**Baseline-zero achievability:** Conditional. If the analyser's tolerance is appropriate and the template author authors the agreement correctly, yes. The risk: parameter expressions on both sides may not evaluate identically under all parameter combinations, especially with floating-point. Tolerance widening may be needed. `network-reliability` may not benefit — its mismatch is not a fan-out shape.

**User-facing impact:**

- New authoring discipline. User-authored templates must declare both surfaces. A user who only writes expr arithmetic and leaves `weight: 1` (today's pattern) gets warnings.
- More verbose templates. The split fraction is expressed twice. Tooling can help.
- Tooling required. A template-linter rule that detects the missing-`weight:` pattern is part of the option 3 deliverable.
- Cognitive cost. "Which side is the source of truth?" becomes the perennial question; the honest answer is "both, and the engine checks they agree".

### Engineering observations

1. **Today's behavior is already option 1 in the materializer, option 2 in the analyser-skip pre-E-24, option-undecided in the templates.** The materializer apportions by weight (option 1's authority output). The analyser, pre-E-24, silently skipped because `flowVolume` series weren't emitted (option 2's runtime behavior, accidentally). The templates encode splits in expr (option-2-or-3 authoring style). E-24's correct emission of `flowVolume` series exposed the latent inconsistency.
2. **Option 1 is the smallest engine change but the largest template change.** Validates the materializer's existing behavior as canonical; ~150-180 LOC of template changes.
3. **Option 2 is the biggest engine change and zero template change.** Makes edge weights second-class for a class of edges — a real semantic shift with downstream consequences for E-15 / E-22.
4. **Option 3 is medium engine and medium template (in additions).** Preserves both authorities but mandates redundancy. Most verbose authoring contract; needs tooling.
5. **None of the options dispute the conservation invariant itself.** All three keep `arrivals == sum(incoming_edges_after_lag)` as the invariant; they differ on which side is canonical when divergence is found.
6. **`network-reliability` is unique.** Its mismatch is not a fan-out split; it is a single-edge `RequestQueue → CorePlatform` where the `arrivals` is `request_queue_served`, itself an expr `MIN(server_capacity, request_queue_demand)` consuming `request_queue_carry` (which uses `CONV` for lag). Worth a focused diagnostic in M-067 regardless of which option wins; flagged as a per-template addendum.

### Reads of the evidence

> Per the milestone constraint ("Footprint analysis is honest, not advocacy"), this section names what the evidence supports without prescribing a choice. The user makes the authority call.

**Option 1 (edge weights win) is favored when:**

- Edge weights are considered the canonical structural language of the topology layer and the team wants one normative authority.
- Telemetry replay (E-15) and model fit (E-22) can rely on edge weights as the single dial to fit against.
- The team is willing to absorb a one-time template-rewrite cost (~150-180 LOC across 9 templates) in exchange for engine simplicity.
- Migration tooling for user-authored templates is acceptable as a follow-up gap.

**Option 2 (expr authority wins) is favored when:**

- Authoring ergonomics (existing templates work without edits) outweighs engine simplicity.
- The team is comfortable with edge weights becoming advisory for a class of edges.
- The downstream cost of "which authority?" entering E-15 and E-22 is acceptable.

**Option 3 (both must agree) is favored when:**

- The team values defense-in-depth and wants the engine to validate authoring intent.
- Tooling can carry the burden of redundancy maintenance.
- A more verbose authoring contract is acceptable.

**The evidence leans toward option 1.** Reasons:

- The engine already implements option 1 in the materializer; it is the path of least engine change.
- The conservation invariant becomes a hard, single-direction truth: `arrivals == sum(weight-derived flows)`. Easier to reason about for telemetry replay and model fit.
- Option 2's "advisory weights" semantic is harder to explain than "weights are canonical, and the expr is wrong if they disagree".
- Option 3's redundancy maintenance is a long-term cost paid by every future template author for a problem that option 1 makes structurally impossible.

The strongest argument against option 1 is the user-facing breakage: every user-authored template that encodes splits via expr will start emitting warnings until the user migrates. A deferred-follow-up gap for migration tooling is the mitigation.

## Work log

- **2026-05-02 — milestone start.** Branch `milestone/M-066-edge-flow-authority-decision` created from `main`. `aiwf promote M-066 in_progress` — atomic commit `5554ed5`. Read milestone spec, epic spec, gap G-032, decision precedent D-053. Read analyser source (`InvariantAnalyzer.cs:307-336, 878-918, 1012-1056`). Read materializer source (`Routing/EdgeFlowMaterializer.cs:155-265`) — confirmed weight-fraction logic locations. Inspected `transportation-basic.yaml`, `transportation-basic-classes.yaml`, `network-reliability.yaml`, `manufacturing-line.yaml` (sampling across the typology spectrum). Confirmed the `ExpectedRunWarnings` baseline at `tests/FlowTime.Integration.Tests/TemplateWarningSurveyTests.cs:79`.
- **2026-05-02 — footprint analysis filled into this spec.** Three options walked, per-template diff estimates produced, `network-reliability` flagged as a per-template addendum candidate, the engine sub-question on `flowVolume`-series referenceability flagged as the option-1 primary engine risk. Awaiting user's authority call.

## Surfaces touched

- `work/decisions/D-NNN-<slug>.md` (new — the ratified decision)
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
