---
id: M-066
title: Flow-Authority Policy Spike
status: in_progress
parent: E-25
acs:
    - id: AC-1
      title: Three-class flow taxonomy documented
      status: met
    - id: AC-2
      title: Footprint analysis preserved for the three G-032 options
      status: met
    - id: AC-3
      title: Repo-wide doc sweep classifies every routing-relevant document
      status: open
    - id: AC-4
      title: Conflicting docs are revised or marked needs-revision
      status: open
    - id: AC-5
      title: ADR drafted naming the flow-authority policy
      status: open
    - id: AC-6
      title: ADR names the schema/compile/analyse enforcement points
      status: open
    - id: AC-7
      title: Class-2 capacity-aware allocator deferred-follow-up gap filed
      status: open
    - id: AC-8
      title: Other deferred follow-ups filed as gaps where needed
      status: open
    - id: AC-9
      title: ADR ratified with status accepted
      status: open
    - id: AC-10
      title: Epic spec and G-032 reference the ratified ADR
      status: open
    - id: AC-11
      title: aiwf check clean
      status: open
---

## Goal

Produce a ratified architectural decision record (ADR) that names the **flow-authority policy** for the FlowTime engine — the rules that govern how flow flows from producers to consumers in the model. The policy must cleanly accommodate the three classes of physical systems FlowTime exists to model (static-share fan-out, dynamic routing, capacity-aware allocation), make routing authority unambiguous at every fan-out point, and name the enforcement layers (schema, compile, analyse) where the policy gets teeth. The deliverable is the ADR plus a doc-sweep classification (preserved inline in this spec) that classifies every routing-relevant document in the repo against the policy; no engine code, no template edits, no test changes ship from this milestone.

## Context

This milestone began life as "edge-flow authority decision" — pick one of three options listed in G-032 (edge weights win / expr authority wins / both-must-agree) and ratify a `D-NNN`. During M-066's working session the framing widened: the question is not just "which surface is normative for class-3 fan-outs" but **"what is FlowTime's flow-authority policy across the three classes of physical systems we exist to model, and how do we enforce it at every layer of the engine"**.

The three classes of physical systems, named:

1. **Class 1 — producer-side push routing.** Producer (or a routing actor immediately after it) computes per-consumer volumes from its own state and a routing rule. Examples: load balancers in front of stateless workers, conveyor diverters, broadcast trees. The router node (`kind: router`) implements this today.
2. **Class 2 — consumer-side pull / capacity-aware allocation.** Producer offers `served(t)` units; an allocator considers all consumers' available capacity this bin and apportions accordingly. Examples: schedulers dispatching jobs to free workers, warehouse pickers assigning bins to free pickers, admission controllers matching requests to backend health. **The engine does not surface this today**; templates that need it must currently fake it via expr arithmetic, which is a modeling error.
3. **Class 3 — edge-as-channel / static-weight routing.** Each producer→consumer edge has a static weight that means "this is the channel's share of this producer's outflow when no other information is available." Examples: percentage-split routing rules, demand-probability splits, PMF-style fan-outs. Edge weights are the routing rule; consumer demand/capacity is irrelevant to *which* consumer gets *what share* (it only affects whether the routed volume queues or serves at the consumer).

**Why "consumer-side expr arithmetic" (option 2 of the original G-032 framing) is wrong on flow-purity grounds, regardless of footprint cost:** a consumer that hard-codes `arrivals = served * 0.25` embeds peer knowledge in an actor that, by the actor model above, must not have peer knowledge. Such a model is not telemetry-replayable (replay substitutes telemetry-observed arrivals; there is no `*0.25` to substitute), is not invariant under peer addition (add a fifth consumer and every existing consumer's arithmetic is wrong), and is not robust to capacity-aware allocation (cannot express "1/3 each across the three live peers when one is saturated" without reaching into peer state, at which point the consumer is a router masquerading as one). This is structural, not preferential.

The reframe collapses the original three options:

- **Original option 2 — expr authority wins on the consumer side — is rejected on flow-purity grounds, not footprint cost.** Pre-empts class 1, breaks class 2's future surfacing, and embeds peer knowledge where it must not live.
- **Original option 1 — edge weights win — is the right answer for class-3 fan-outs**, which is the case all 9 affected templates from G-032 actually instantiate. Edge weights ARE the routing rule for class 3.
- **Original option 3 — both must agree — is structurally redundant for class 3.** Forces the author to encode the same fact twice and tooling to verify they agree. Adds tax with no expressive gain.

The chosen policy, expressed crisply: **routing authority is declared at the producer's outgoing edges; for static-share fan-outs (class 3) edge weights are normative; router nodes (class 1) are required when routing is dynamic; class-2 capacity-aware allocation is not surfaced in the engine today and must be filed as a deferred gap; consumer-side expr arithmetic must not encode peer-relative splits — consumer arrivals come from edge flow and any other arithmetic on the consumer side is a modeling error and must be rejected by the engine's gates**.

This milestone documents the policy, sweeps the repo for docs that contradict it, ratifies the policy as an ADR, and names the enforcement layers (schema/compile/analyse) where the policy must be enforced. The actual enforcement implementation is the new M-NNN-enforcement milestone (the next milestone in the epic). The actual engine + template alignment is the milestone after that. The actual golden canary is the milestone after that.

## Acceptance criteria

### AC-1 — Three-class flow taxonomy documented

A taxonomy document under `docs/architecture/` (e.g. `docs/architecture/flow-authority-policy.md` — milestone may revisit name) names the three classes of physical systems the engine models, with one paragraph per class describing: the routing actor's responsibilities, what role each of {producer, consumer, routing} plays, the canonical examples, and the authority surface the policy assigns to that class. The taxonomy is the prose backbone the ADR references; it is also the document a future template author reads to know which class their model belongs in.

### AC-2 — Footprint analysis preserved for the three G-032 options

The footprint analysis already produced during this milestone (preserved inline in this spec under "Footprint analysis (M-066's working artefact)") documents engine-LOC magnitude, per-template diff estimates, and baseline-zero achievability for each of the original three G-032 options. The preserved analysis lets a future reader see why the chosen authority won on flow-purity grounds *and* on engineering-cost grounds.

### AC-3 — Repo-wide doc sweep classifies every routing-relevant document

A doc-sweep classification preserved as a section of this spec ("Doc-sweep classification (M-066's AC-3 artefact)") classifies every routing-relevant document in the repo against the policy. Search axes:

- **Axis 1 — explicit routing-semantics discussions.** `docs/architecture/headless-engine-architecture.md`, `docs/architecture/time-machine-analysis-modes.md`, `docs/templates/template-authoring.md`, `docs/modeling.md`, `docs/flowtime-engine-charter.md`, plus any architecture doc that names "arrivals", "served", "fan-out", "split", "routing", "edge", "weight", "downstream", "upstream", "consumer", "producer".
- **Axis 2 — implicit routing assumptions.** Documents that talk about how flow flows without naming routing authority. The danger: documents correct under one class but presented as universal.
- **Axis 3 — ADRs and decision records.** `docs/adr/` is empty today. Decision records: D-046 (`GET /v1/runs/{runId}/model`), D-047 (`trace` field on goal-seek/optimize), D-051 (E-24 schema-alignment closure), D-053 (testing rigor). Read each for routing-semantic implications.
- **Axis 4 — gaps that may need revision.** G-032 (already addressed by this milestone), G-016 (Rust Engine Parity), G-018 (`IModelEvaluator` Series-Key Shape Divergence), G-019 (Sim-generated model shape vs. Rust engine compiler expectations), G-003 (Dependency Constraint Enforcement — touches capacity → class 2), G-011, G-013 (fit/calibration → routing model assumptions).

For each hit, classify into one of: **aligned**, **silent**, **conflicting**, **ambiguous**. Output is one section per hit in the inlined doc-sweep classification (this spec's "Doc-sweep classification (M-066's AC-3 artefact)" section), with a one-sentence note on what (if anything) needs to change.

### AC-4 — Conflicting docs are revised or marked needs-revision

For every doc classified as **conflicting** in AC-3, either:

- **Revise it in this milestone** if the change is small (a paragraph or section); or
- **Mark it `[needs revision per ADR-NNNN]`** at the top of the document with a one-line note, and file a deferred follow-up gap if the revision is substantial enough to warrant its own work.

The point: no doc classified as conflicting may survive M-066 close in its current state without an explicit pointer to the ADR.

### AC-5 — ADR drafted naming the flow-authority policy

An ADR is added under `docs/adr/` via `aiwf add adr`, which allocates the next `ADR-NNNN` id and scaffolds frontmatter (ADR is a first-class aiwf entity kind per CLAUDE.md). The ADR:

- Names the policy explicitly: routing authority lives at producer-fan-out; edge weights are normative for class-3; router nodes for class-1; class-2 not currently surfaced (filed as deferred gap); consumer-side expr arithmetic must not encode peer-relative splits.
- References the three-class taxonomy doc (AC-1) and the doc sweep (AC-3).
- Records the rejected framings (original consumer-side-expr-wins, original both-must-agree) with rationale citing both the flow-purity argument and the footprint analysis (AC-2).
- States the constraint that exactly one routing authority lives at any producer-fan-out point; nothing else may inject routing information.

### AC-6 — ADR names the schema/compile/analyse enforcement points

The ADR explicitly names the layers where the policy gets teeth, and what each layer is responsible for enforcing. At minimum:

- **Schema** (`docs/schemas/model.schema.yaml` + `ModelSchemaValidator`) — rejects models that encode peer-relative splits in consumer expr arithmetic (e.g., expr nodes whose formula references the producer's `served` series and a `split*` parameter directly).
- **Compile** (`ModelCompiler` / `TimeMachineValidator`) — fan-out detection: any producer with >1 outgoing edge must have exactly one routing authority declared (weights, router, or — once surfaced — capacity-aware allocator). Compile-time error if zero or more than one authority is detected.
- **Analyse** (`InvariantAnalyzer.cs`) — new warnings: `routing_authority_ambiguous` (producer with conflicting authority surfaces), `consumer_side_peer_split_detected` (consumer expr that looks peer-relative). The existing `edge_flow_mismatch_incoming` / `_outgoing` warnings stay as they are.

The ADR names *what* each layer enforces; the next milestone (Schema + Compile + Analyse Enforcement) lands the implementation.

### AC-7 — Class-2 capacity-aware allocator deferred-follow-up gap filed

A gap is filed via `aiwf add gap` capturing class-2 (consumer-side pull / capacity-aware allocation) as a future engine concern. The gap names: the modeling cases that require it (warehouse pickers, schedulers, admission control), why expr arithmetic cannot fake it correctly, the proposed surface (a capacity-aware router actor distinct from the static-weight router), and explicit "deferred — future capability, not current correctness" framing. The ADR references the gap.

### AC-8 — Other deferred follow-ups filed as gaps where needed

If the doc sweep (AC-3) or the policy ratification (AC-5) surfaces follow-up work that doesn't fit in M-066 and isn't already absorbed into the next epic milestones, file each as a separate gap. Examples that may surface: template-authoring guide rewrite, Rust engine alignment work that mirrors the .NET engine policy, telemetry-replay protocol changes that depend on the policy. Each gap names trigger and action.

### AC-9 — ADR ratified with status accepted

The ADR is promoted from `proposed` to `accepted` via `aiwf promote ADR-NNNN accepted` (ADR is a first-class aiwf entity; the FSM transition + commit trailer audit trail apply). Ratification is HITL: the human approves the promotion before the commit lands. The ratified status is the gate that unlocks the next epic milestone.

### AC-10 — Epic spec and G-032 reference the ratified ADR

The epic spec at `work/epics/E-25-engine-truth-gate/epic.md` is updated so its "Open questions" table marks the edge-flow authority question as resolved with a link to the ADR. G-032's status moves to `addressed` with a reference to the ADR (the gap is fully closed only when the engine + template alignment milestone lands; this milestone closes the design portion).

### AC-11 — aiwf check clean

After all updates, `aiwf check` reports `ok — no findings`. No frontmatter drift, no orphaned references.

## Footprint analysis (M-066's working artefact)

<!-- Preserved per AC-2. Original work product from before the framing widened to a flow-purity policy spike. The footprint cost evidence is independently valid; the policy framing makes the chosen direction also right on first principles. -->

### Methodology

For each of the three G-032 options (edge weights win / expr authority wins / both-must-agree), one representative template is walked under that option's resolution; the diff is counted; the per-template cost is extrapolated across the 9 affected shipped templates listed in G-032. Engine-LOC magnitude is bucketed: `small` (< 50 LOC, single file), `medium` (50–300 LOC, 1–3 files), `large` (300+ LOC or multi-subsystem). Anything `large` triggers the epic risk-table mitigation (split into a multi-milestone implementation phase rather than extending E-25 indefinitely).

### Affected templates

Sourced from G-032's "Affected templates" table and confirmed against `ExpectedRunWarnings` in `tests/FlowTime.Integration.Tests/TemplateWarningSurveyTests.cs:79`. Nine templates are affected:

| Template | Run-warn baseline |
|---|---|
| `transportation-basic-classes` | 8 |
| `supply-chain-multi-tier` | 2 |
| `supply-chain-multi-tier-classes` | 2 |
| `it-system-microservices` | 1 |
| `manufacturing-line` | 1 |
| `network-reliability` | 1 |
| `supply-chain-incident-retry` | 1 |
| `transportation-basic` | 1 |
| `warehouse-picker-waves` | 1 |

### Option footprint summary

| Option | Engine-LOC | Templates edited | Baseline-zero achievable? | User-facing impact |
|---|---|---|---|---|
| 1 — Edge weights win | `small` (< 50 LOC; doc + possibly `flowVolume` series referenceability bumps to upper-bound `medium` ~30-60 LOC if expr nodes need to consume the `flowVolume` namespace) | 9 | Yes for 8/9; `network-reliability` flagged as possible residual (mismatch is single-edge CONV/lag, not fan-out) | Yes — user templates encoding splits in expr will fire warnings; mechanical migration; deferred-follow-up gap for tooling per AC-7/AC-8 |
| 2 — Expr authority wins | `medium` (150-300 LOC; `EdgeFlowMaterializer.cs` + `InvariantAnalyzer.cs` + possibly `RouterSpecificationBuilder.cs`) | 0 | Yes for all 9 by definition | No template breakage, but edge weights become advisory for a class of edges — semantic shift with E-15 / E-22 downstream consequences |
| 3 — Both must agree | `medium` (50-150 LOC; analyser + schema-validator) | 9 (additions only, ~45-60 LOC total) | Conditional — depends on tolerance and parameter-expression reproducibility | New authoring discipline (declare both surfaces); tooling required; more verbose templates |

### Engineering observations

1. **`network-reliability` is unique among the 9.** Its mismatch is not a fan-out split — it's a single-edge `RequestQueue → CorePlatform` whose `arrivals` is an expr (`MIN(server_capacity, request_queue_demand)`) consuming `request_queue_carry` (CONV-driven lag). Setting `weight: 1` on the edge does not change materializer output. Under option 1 it may need a small per-template expr restructure or accept residual under an `ExpectedRunWarnings` entry. Under option 2 it cleans automatically. Under option 3 the parameter-expression equality may be tolerance-sensitive. Worth flagging as a per-template addendum for the engine + template alignment milestone regardless of which option wins.

2. **Option 1 has a hidden engine sub-question.** Can `expr` nodes reference per-edge `flowVolume` series (e.g., `arrivals_airport = edge_queue_to_airport_flowVolume`)? The materializer at `Routing/EdgeFlowMaterializer.cs:54` builds a `flowVolumes` dictionary, but whether expr nodes consume it as a referenceable namespace is unverified by this analysis. If they cannot, option 1's engine LOC moves from `small` (~30 LOC, doc-only) toward upper-bound `medium` (~30-60 LOC for series-referenceability plumbing). The engine + template alignment milestone must answer this; for footprint purposes it is the option's primary engine risk.

3. **`transportation-basic-classes` (8 warnings, worst offender) has a `kind: router` node plus three downstream `*DispatchQueue` serviceWithBuffer nodes.** Under option 1, the rewrite needs to reconcile router-arm class assignments with edge weights — possibly the single hardest per-template edit at ~30-40 LOC. Worth noting in the engine + template alignment milestone plan as the longest-pole template.

### Read of the evidence (cost dimension)

The cost evidence leans toward option 1 (edge weights win): smallest engine LOC, conservation invariant becomes single-direction truth (easier reasoning for E-15 telemetry replay and E-22 model fit), avoids option 2's "weights are advisory under condition X" semantic asterisk that propagates to telemetry replay's authority story, avoids option 3's perpetual redundancy-maintenance tax. The cost-strongest argument *against* option 1 is user-facing template breakage outside the shipped set; mitigation is a deferred-follow-up gap for migration tooling / template-linter rule.

The flow-purity reframe (see Context above) reaches the same conclusion through a stronger argument: option 2 (consumer-side expr authority) is structurally wrong because it embeds peer knowledge in an actor that must not have peer knowledge. Option 3 (both must agree) is structurally redundant for class 3. Option 1 (edge weights win) is the right answer for class 3 on first principles.

The cost evidence and the purity argument agree. The ADR can cite both.

## Doc-sweep classification (M-066's AC-3 artefact)

<!-- Preserved per AC-3. Originally drafted as a standalone artefact under `work/epics/E-25-engine-truth-gate/M-066-flow-authority-doc-sweep.md` and inlined here per aiwf v3 convention: the milestone spec is the single home for working analysis. -->

**Date:** 2026-05-05
**Reference policy:** [`docs/architecture/flow-authority-policy.md`](../../../docs/architecture/flow-authority-policy.md) (the three-class taxonomy authored under AC-1).

### Purpose

This section classifies every routing-relevant document in the repository against the flow-authority policy. The classification feeds AC-4 (which decides whether each conflicting doc is revised in M-066 or marked needs-revision and deferred). Search axes follow the M-066 spec under AC-3:

- **Axis 1** — explicit routing-semantics discussions in architecture / template / charter docs.
- **Axis 2** — implicit routing assumptions (correct under one class but presented as universal).
- **Axis 3** — ADRs and decision records.
- **Axis 4** — gaps that may need revision.

For each hit, one of four labels:

- **aligned** — already consistent with the three-class taxonomy; no change needed.
- **silent** — does not assert routing authority; no contradiction; no change needed.
- **conflicting** — contains an assertion that contradicts the policy; AC-4 either revises or marks.
- **ambiguous** — wording leaves the authority surface unclear; AC-4 either tightens or marks.

Out-of-scope classes (not in the table below):

- `docs/archive/`, `docs/releases/` — historical (what WAS); per `CLAUDE.md` truth-precedence, never current authority.
- `docs/notes/` and `docs/research/` — exploration only; never implementation authority.
- `docs/guides/{CLI,UI,MCP}.md` — CLI/UI/MCP usage docs; do not touch routing semantics.
- `docs/architecture/dag-map-*.md` — DAG-map visualization library docs; "router" / "split" usage is rendering-mechanical, unrelated to flow routing.
- `docs/development/*.md` — covered by [G-037](../../gaps/G-037-pre-aiwf-v1-framework-docs-survived-migration-and-contradict-the-v3-model.md); v1-residue docs already moved to `docs/archive/`. The remaining `versioning.md` and `ui-debug-mode.md` do not touch routing.
- Pre-aiwf decisions that pre-date the policy and do not assert it (D-001 … D-052 except D-053): silent by audit; not enumerated below.

### Summary counts

| Label | Count |
|---|---|
| aligned | 10 |
| silent | 30 |
| conflicting | 2 |
| ambiguous | 0 |
| **Total in scope** | **42** |

### Conflicting hits (action required)

Listed up front per the milestone Design Notes ("Sort by classification severity (conflicting first)"). Both are AC-4 follow-ups; both fit within the small-revision threshold and do not require deferred-follow-up gaps.

1. **`docs/flowtime-v2.md` line 246** — universal "fan-out → router with weights" mapping in the real-world-to-model table; elides class-3 edge-as-channel. Revise: split the row into class-3 (edges with `weight`) and class-1 (`router` node).
2. **`docs/reference/flow-theory-coverage.md` lines 111–118** — section 8 verdict "Dynamic routing — Not planned"; the policy classifies this as class-2 capacity-aware allocation (deferred future capability). Revise: change verdict to "Deferred" with forward references to ADR-NNNN and the class-2 gap.

The full per-axis tables follow.

### Hits — Axis 1 (explicit routing-semantics discussions)

| File | Classification | Excerpt / location | Note (AC-4 follow-up) |
|---|---|---|---|
| `docs/architecture/flow-authority-policy.md` | aligned | The policy doc itself (M-066 AC-1 deliverable). | No change. Reference target for ADR-0001. |
| `docs/architecture/headless-engine-architecture.md` | silent | Mentions `arrivals`/`served` as parameter examples (lines 156, 165–166); does not assert routing authority. | No change. |
| `docs/architecture/time-machine-analysis-modes.md` | silent | Uses `arrivals`/`served` as parameter / series identifiers in API examples. No fan-out semantics asserted. | No change. |
| `docs/architecture/whitepaper.md` | silent | Lists `routers` as a node category (line 43); does not pin per-fan-out authority. | No change. The policy taxonomy supersedes this listing-level mention. |
| `docs/architecture/matrix-engine.md` | silent | Maps C# `RouterFlowMaterializer` to Rust `compile_router` (line 175); implementation-level concept map, no policy claim. | No change. |
| `docs/architecture/class-dimension-decision.md` | aligned | Line 32: "routers and analyzers use a single class key" — describes class-key cardinality, not routing authority. Consistent with class 1 router-actor framing. | No change. |
| `docs/architecture/expression-language-design.md` | silent | Routing-vocabulary hits ("consumer", "producer") refer to compiler-stage producers/consumers of expressions, not flow producers/consumers. | No change. |
| `docs/architecture/backpressure-pattern.md` | silent | Routing-vocabulary hits refer to backpressure feedback, not fan-out apportionment. | No change. |
| `docs/architecture/retry-modeling.md` | silent | Treats retry-feedback edges as flow-conservation concerns; does not assert per-fan-out authority. | No change. |
| `docs/architecture/supported-surfaces.md` | silent | Surface inventory; routing-vocabulary hits are listing-level. | No change. |
| `docs/templates/template-authoring.md` | aligned | Lines 249–303 actively steer authors away from "percentage math across expressions" toward `kind: router` (class 1) and `weight` on router routes. Does not yet describe class 3 (edge-as-channel) as a first-class option, but does not contradict the policy. | No change in M-066. Possible class-3 enhancement deferred (filed as gap under AC-8 if warranted). |
| `docs/templates/template-testing.md` | aligned | Line 53 explicitly tells authors "no stale expression splits" once a router is introduced — directly aligned with the policy's no-peer-relative-splits rule. | No change. |
| `docs/templates/metric-alias-authoring.md` | silent | Names router nodes as one alias surface; no authority claim. | No change. |
| `docs/templates/profiles.md` | silent | Profile semantics; routing-vocabulary hits do not touch authority. | No change. |
| `docs/modeling.md` | silent | Lists "routers" as a node-kind category (line 26); doc-map only, no semantic claim. | No change. |
| `docs/flowtime-engine-charter.md` | silent | Line 32 lists `router` among node kinds; no authority claim. | No change. |
| `docs/flowtime-charter.md` | silent | Line 28 mentions "routing changes" as a scenario type; no per-fan-out semantics asserted. | No change. |
| `docs/flowtime.md` | silent | Top-level overview; no routing-authority claims. | No change. |
| `docs/flowtime-v2.md` | conflicting | Line 246 (the "real-world to model mapping" table): `Fan-out (one input, multiple outputs) \| router node with weights or class-based routing`. Asserts router-with-weights as the mapping for *all* fan-out, eliding class-3 (edge-as-channel / static-weight on edges) entirely. | **AC-4 revise (small).** Replace the row with two rows: one for static-share class-3 fan-out (edges with `weight`), one for class-1 dynamic / class-based fan-out (`router` node). One-paragraph diff at most. |
| `docs/reference/contracts.md` | silent | API-contract reference; no routing semantics. | No change. |
| `docs/reference/data-formats.md` | silent | Edge-type listing (throughput / effort); does not pin authority. | No change. |
| `docs/reference/engine-capabilities.md` | aligned | Line 13: edges support typed `flowVolume` series; line 50: `flowVolume` is the per-edge truth. Consistent with the policy's class-3 authority surface. Line 68 names `router_missing_class_route` / `router_class_leakage` warnings — class-1 enforcement, aligned. | No change. |
| `docs/reference/flow-theory-foundations.md` | silent | Theory primer; routing-vocabulary hits don't pin authority. | No change. |
| `docs/reference/flow-theory-coverage.md` | conflicting | Section 8 "Routing & Flow Splitting" (lines 111–118): row `Dynamic routing (route by downstream queue length) \| **Not planned**`. The flow-authority policy explicitly classifies this as class-2 (capacity-aware allocation) and frames it as a *deferred future capability*, not "not planned" (the deferred-follow-up gap is filed under AC-7). | **AC-4 revise (small).** Change the "Not planned" row to "**Deferred** — class-2 capacity-aware allocation, see ADR-NNNN and the class-2 gap (AC-7)." Single-cell edit. |
| `docs/schemas/model.schema.md` | silent | Schema reference; routing-vocabulary hits at line 323 ("typically a service/router") name router as a sink-of-flow, not a routing-authority claim. | No change. |
| `docs/schemas/template-schema.md` | silent | Schema reference. | No change. |
| `docs/schemas/README.md` | silent | Index. | No change. |
| `docs/concepts/nodes-and-expressions.md` | silent | No routing-vocabulary hits beyond expression-evaluator producers/consumers. | No change. |
| `docs/concepts/pmf-modeling.md` | silent | Line 332 references `kind: router` as a future routing-integration example; consistent with class 1. | No change. |

### Hits — Axis 2 (implicit routing assumptions)

The grep for routing-vocabulary across the engine charter, modeling overview, and whitepaper surfaced only listing-level mentions of `router` as a node kind (the silent rows in Axis 1). No document was found that asserts a routing-authority rule implicitly while presenting it as universal — the prevailing pattern is silence on per-fan-out authority. The only exception is `flowtime-v2.md` line 246 (already classified conflicting in Axis 1 above), which presents the router-with-weights mapping as universal.

No additional Axis-2 entries.

### Hits — Axis 3 (ADRs and decision records)

`docs/adr/` does not exist today; ADR-0001 (the M-066 deliverable under AC-5) seeds it. No prior ADRs to classify.

For decision records under `work/decisions/`, the M-066 spec names D-046, D-047, D-051, D-053:

| File | Classification | Excerpt / location | Note (AC-4 follow-up) |
|---|---|---|---|
| `work/decisions/D-046-e-21-admits-read-only-run-adjacent-model-yaml-endpoint-under-run-read-surface.md` | silent | API-surface decision; no routing semantics. | No change. |
| `work/decisions/D-047-additive-trace-field-on-v1-goal-seek-and-v1-optimize.md` | silent | Goal-seek/optimize trace field; no routing semantics. | No change. |
| `work/decisions/D-051-e-24-schema-alignment-closed-e-23-model-validation-consolidation-ready-to-resume.md` | silent | Schema-alignment closure; no routing-authority claim. | No change. |
| `work/decisions/D-053-testing-rigor-approach-phase-2-baseline-canary-first-full-golden-output-canon-deferred.md` | aligned | Names the unresolved authority question (line 8: "the underlying template-vs-engine authority question … which side is canonical for edge flow volumes (expr nodes encode splits, topology edge weights would need to mirror them)") and defers its resolution to a future engine milestone. The policy spike (M-066) closes this question; the ADR is the resolution D-053 anticipates. | No revision needed in D-053; the gap reference G-032 (which D-053 implicitly tracks) gets the ADR cross-reference under AC-10. |

A scan of the rest of `work/decisions/D-001` … `D-052` for `routing|router|edge.*weight|fan-?out|peer-relative|consumer-side|producer-side` returned only D-053 — none of the others assert routing authority, and they are listed here as silent-by-audit rather than enumerated.

### Hits — Axis 4 (gaps that may need revision)

| File | Classification | Excerpt / location | Note (AC-4 follow-up) |
|---|---|---|---|
| `work/gaps/G-032-transportation-basic-regressed-edge-flow-mismatch-incoming-3-after-e-24-unification.md` | aligned | The gap that motivated this milestone. The three options it lists are exactly the framings the policy resolves (option 2 rejected on flow-purity grounds; option 1 selected for class 3). | AC-10 promotes G-032 to `addressed` with a reference to ADR-0001. No body revision needed. |
| `work/gaps/G-008-router-convergence-guard-deferred-from-phase-1.md` | aligned | Line 11: "single-pass design is correct for static router weights … convergence guard is only needed if dynamic/expression-based router weights are introduced." Consistent with class 1 (static router) vs. class 2 (dynamic / capacity-aware) framing. | No change. The class-2 gap to be filed under AC-7 should cross-reference G-008 if the convergence concern resurfaces with the future allocator surface. |
| `work/gaps/G-037-pre-aiwf-v1-framework-docs-survived-migration-and-contradict-the-v3-model.md` | aligned | Already addressed (status: `addressed`). v1-framework docs were moved to `docs/archive/` and do not contain routing-authority claims that survived. | No change. |
| `work/gaps/G-019-sim-generated-model-shape-vs-rust-engine-compiler-expectations.md` | aligned | Lines 22–23 reference "routing is expressed via edge weights rather than a node field" — the class-3 case the policy formalizes. The gap describes a Sim/Rust shape divergence, not a contradiction with the policy. | No change. |
| `work/gaps/G-016-rust-engine-parity-evaluation-core-gaps.md` | silent | Mentions `class routing internally`; no per-fan-out authority claim. | No change. |
| `work/gaps/G-003-dependency-constraint-enforcement-deferred-m-10-03.md` | silent | "Routing" mention is MCP intent-routing, not flow-routing. | No change. |
| `work/gaps/G-011-continuous-prediction-crystal-ball-usage-pattern.md` | silent | No routing-authority content. | No change. |
| `work/gaps/G-013-e-18-model-calibration-needs-crystal-ball-design-input.md` | silent | No routing-authority content. | No change. |
| `work/gaps/G-018-imodelevaluator-series-key-shape-divergence.md` | silent | No routing-authority content. | No change. |

### AC-4 follow-up summary

Two conflicting docs require small in-place revisions in M-066 AC-4:

1. **`docs/flowtime-v2.md` line 246** — split the universal-fan-out row into class-3 (edges) and class-1 (router) rows. Diff size: one table row → two rows + a one-line note.
2. **`docs/reference/flow-theory-coverage.md` lines 111–118** — change the "Not planned" verdict for dynamic routing to "Deferred — class-2 capacity-aware allocation" with a forward reference to ADR-NNNN and the class-2 gap. Diff size: one cell.

Both are within the AC-4 "small revision" threshold (paragraph or section that can be replaced without changing the doc's overall structure). No `[needs revision per ADR-NNNN]` markers are required — both can be revised in M-066.

No deferred follow-up gaps (AC-8) surfaced from this sweep beyond the class-2 capacity-aware allocator gap to be filed under AC-7. The possible class-3 enhancement to `template-authoring.md` (treat edge-as-channel as a first-class authoring pattern alongside the router) is a candidate AC-8 entry, but it is enhancement work — not a contradiction with the policy — and may be folded into the engine + template alignment milestone (M-067) instead. AC-4 review will decide.

### Cross-references (doc sweep)

- Policy doc: [`docs/architecture/flow-authority-policy.md`](../../../docs/architecture/flow-authority-policy.md)
- Footprint analysis (M-066's working artefact): see "Footprint analysis (M-066's working artefact)" section above
- Originating gap: [G-032](../../gaps/G-032-transportation-basic-regressed-edge-flow-mismatch-incoming-3-after-e-24-unification.md)
- Sibling gap: [G-037](../../gaps/G-037-pre-aiwf-v1-framework-docs-survived-migration-and-contradict-the-v3-model.md)
- Related gap: [G-008](../../gaps/G-008-router-convergence-guard-deferred-from-phase-1.md)
- Related decision: [D-053](../../decisions/D-053-testing-rigor-approach-phase-2-baseline-canary-first-full-golden-output-canon-deferred.md)
- Epic: [E-25 Engine Truth Gate](./epic.md)

## Constraints

- **No engine code change.** This milestone delivers a policy + ADR + sweep, not an implementation. Any code touched in `src/FlowTime.Core/` outside the milestone artefacts is out of scope.
- **No template edits.** No file under `templates/` is modified by this milestone. Template surveys may *read* templates to compute footprint estimates and to check that the policy classification holds, but no edits land.
- **No `ExpectedRunWarnings` changes.** The Phase 2 baseline canary stays exactly as committed by the patch on 2026-05-01 throughout this milestone. Resetting baselines is the engine + template alignment milestone's job, conditional on the engine + template change landing.
- **No test code changes.** `Survey_Templates_For_Warnings` is not modified. New analyser warnings are *named* in the ADR (AC-6) but not implemented here; that's the next milestone.
- **ADR-class, not D-NNN.** This is durable architectural truth, not a project-bound decision. The repo's `docs/adr/` directory is empty; this ADR seeds it. Recording the decision as `D-NNN` is rejected because the policy outlives this project's planning context.
- **Footprint analysis is preserved verbatim.** The footprint analysis section in this spec is M-066's working artefact and is preserved per AC-2. Editing it is out of scope unless a factual error is discovered; the framing-widening (flow-purity reframe) does not invalidate the cost evidence.
- **Exactly one routing authority per producer-fan-out.** The policy itself is the binding rule the milestone ratifies. The schema/compile/analyse enforcement layers (named by AC-6) are not implemented here but their *existence and responsibilities* are pinned by this milestone.
- **Project rules.** .NET 9 / C# 13 (no code in this milestone, but the conventions apply to any code-cited line ranges); invariant culture; no time or effort estimates in the ADR body.

## Design Notes

- **ADR allocation mechanics.** The repo's `docs/adr/` is empty. ADR is a first-class aiwf entity kind (per CLAUDE.md and `aiwf add` usage). Use `aiwf add adr <slug>` to allocate the next `ADR-NNNN` id, scaffold frontmatter, and seed the directory. Promote `proposed → accepted` via `aiwf promote` so the FSM check + commit trailer apply.
- **Doc-sweep classification convention.** Use four labels: `aligned` / `silent` / `conflicting` / `ambiguous`. For each hit, the inlined doc-sweep section records: filepath, the routing-relevant excerpt, the classification, a one-sentence note. Sort by classification severity (conflicting first). The summary at the top of the section gives counts per label.
- **What "revise in this milestone" means for AC-4.** A "small" revision is a paragraph or section that can be replaced or rewritten without changing the doc's overall structure. Anything larger gets the `[needs revision per ADR-NNNN]` mark and a deferred follow-up gap. Goal: cap M-066's doc-edit scope so the milestone closes promptly; the heavy doc-rewrite work belongs to a future milestone or wf-patch.
- **The doc-sweep methodology favors greppable patterns.** Use `rg` to find candidate documents, then read each candidate in full before classifying. Don't classify based on the grep snippet alone — context matters and a doc that mentions "edge weight" once may be aligned, silent, or conflicting depending on what surrounds the mention.
- **Class-2 deferred-gap framing.** The gap (AC-7) must explicitly say *deferred — future capability, not current correctness*. The point is to make the deferral honest: nothing in the current shipped template set needs class-2; the policy says class-2 isn't currently surfaced; the gap captures the future-engine work.
- **G-037 already names some of the doc-sweep hits.** During M-066's working session a sibling gap G-037 was filed: "Pre-aiwf v1 framework docs survived migration and contradict the v3 model." It enumerates several `docs/development/*.md` files with v1 conventions that contradict the v3 model. The doc sweep (AC-3) should pick up where G-037 leaves off — those v1 docs may also contain routing-semantic claims that need the same classification pass.

## Surfaces touched

- `docs/adr/ADR-NNNN-<slug>.md` (new — the ratified ADR)
- `docs/architecture/flow-authority-policy.md` (new — the three-class taxonomy doc; name may revisit during the milestone)
- `work/epics/E-25-engine-truth-gate/M-066-edge-flow-authority-decision.md` (this file; frontmatter status updates as ACs land; footprint section preserved verbatim; doc-sweep classification inlined per AC-3)
- `work/epics/E-25-engine-truth-gate/epic.md` (small edit — open-questions table update; success-criteria refresh per the new milestone shape)
- `work/gaps/G-032-…md` (status promotion to `addressed`; reference the ADR)
- `work/gaps/G-NNN-class-2-capacity-aware-allocator.md` (new — the deferred-follow-up gap from AC-7)
- `work/gaps/G-NNN-…md` (any other deferred-follow-up gaps from AC-8)
- Any docs revised in AC-4 (in-place edits with conventional commit; or `[needs revision per ADR-NNNN]` marker added at the top)
- `ROADMAP.md` (regenerated via `aiwf render roadmap --write`)

## Out of scope

- Engine code change (next milestone — Schema + Compile + Analyse Enforcement).
- Template edits (the milestone after that — Engine + Template Alignment).
- `ExpectedRunWarnings` baseline reset (Engine + Template Alignment).
- Golden-output canary infrastructure or fixtures (the final milestone — Golden-Output Canary).
- Class-2 capacity-aware allocator implementation (filed as deferred gap per AC-7; future engine work, not E-25 scope).
- Any change to the `Survey_Templates_For_Warnings` test (Engine + Template Alignment owns the val-warn delta gate addition).
- Heavyweight rewrites of pre-aiwf v1 documentation (G-037's territory; M-066 marks conflicting docs but does not rewrite them past the small-revision threshold defined in AC-4).
- Engine-team architecture review pass beyond what is needed to ratify the chosen policy. If the review surfaces broader engine-architecture questions (e.g., conservation tolerance reshaping, new analyser warning families beyond the two named in AC-6), those are filed as deferred gaps and not absorbed into this milestone.

## Dependencies

- E-25 epic spec ratified (in place — see `work/epics/E-25-engine-truth-gate/epic.md`).
- Patch `patch/edge-flow-mismatch` merged (in place — Phase 2 baseline canary committed 2026-05-01).
- G-032 in `addressed` status pending this milestone's resolution.
- G-037 (sibling gap; documents the v1-residue contradicting v3) — informs the doc sweep but does not block.

## References

- Epic spec: `work/epics/E-25-engine-truth-gate/epic.md`
- Gap: `work/gaps/G-032-transportation-basic-regressed-edge-flow-mismatch-incoming-3-after-e-24-unification.md` — the three options and per-template impact table
- Gap: `work/gaps/G-037-pre-aiwf-v1-framework-docs-survived-migration-and-contradict-the-v3-model.md` — sibling gap; doc-sweep input
- Phase 2 baseline canary: `tests/FlowTime.Integration.Tests/TemplateWarningSurveyTests.cs:79` (the `ExpectedRunWarnings` dictionary)
- Decision precedent for project-scoped calls: `work/decisions/D-053-testing-rigor-approach-phase-2-baseline-canary-first-full-golden-output-canon-deferred.md`
- Analyser source: `src/FlowTime.Core/Analysis/InvariantAnalyzer.cs:323-335` (incoming-edge conservation; the rule the policy binds)
- Materializer source: `src/FlowTime.Core/Routing/EdgeFlowMaterializer.cs:54` (the `flowVolumes` dictionary; option 1's engine sub-question)
- Conversation framing: this milestone widened from "decision-only D-NNN" to "policy spike + ADR" during M-066's working session on 2026-05-02. The flow-purity argument that collapsed the original three options is the framing-widening rationale.

## Work log

- **2026-05-02** — milestone-start ritual ran on branch `milestone/M-066-edge-flow-authority-decision`. Status promoted draft→in_progress (commit `5554ed5`). Footprint analysis filled in (commit `8bf15ed`). Sibling gap G-037 filed (commit `95e4b18`).
- **2026-05-02** — milestone scope widened: from "edge-flow authority decision" (option 1/2/3 pick) to "flow-authority policy spike + ADR + repo-wide doc sweep". Reframe rationale: G-032's option 2 (consumer-side expr authority) is structurally wrong on flow-purity grounds independent of footprint cost; the policy needs to accommodate three classes of physical systems (class-1 dynamic routing, class-2 capacity-aware allocation, class-3 static-weight); enforcement must be named at schema/compile/analyse layers. New milestone added to E-25 to land the enforcement work; M-067/M-068 scopes adjusted.
