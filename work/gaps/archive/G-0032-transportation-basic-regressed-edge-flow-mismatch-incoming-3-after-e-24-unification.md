---
id: G-0032
title: '`transportation-basic` regressed: `edge_flow_mismatch_incoming` × 3 after E-0024 unification'
status: addressed
addressed_by: [ADR-0001, M-0066]
---

### Why this is a gap

Running the `transportation-basic` template (titled "Transportation Network with Hub Queue") today (2026-04-28) emits **three** analyser warnings of code `edge_flow_mismatch_incoming` (severity `warning`):

```
Arrivals do not match sum of incoming edge flows. Edges: queue_to_airport.   (LineAirport,    Δ ≈ 1.89)
Arrivals do not match sum of incoming edge flows. Edges: queue_to_downtown.  (LineDowntown,   Δ ≈ 9.46)
Arrivals do not match sum of incoming edge flows. Edges: queue_to_industrial.(LineIndustrial, Δ ≈ 7.57)
```

The analyser is asserting the conservation invariant `arrivals[t] == sum(incoming_edges_after_lag[t])` at the three downstream service nodes (`LineAirport`, `LineDowntown`, `LineIndustrial`) fed by the central hub queue. The mismatch values are **not** floating-point noise — they're 1.89, 9.46, 7.57 in absolute units. Real semantic divergence.

Comparing run artifacts on disk for the **same template** under **default parameters**:

| Run | Date | Warning count | `edge_flow_mismatch_incoming` |
|---|---|---|---|
| `run_20260424T124735Z_25e2fe28` | 2026-04-24 | 0 | 0 |
| `run_20260424T124839Z_ef75aea3` | 2026-04-24 | 0 | 0 |
| `run_20260424T142044Z_f6d43b90` | 2026-04-24 | 0 | 0 |
| `run_20260424T150244Z_b2f4c995` | 2026-04-24 | 0 | 0 |
| `run_20260428T165413Z_6ed5974e` | 2026-04-28 | 3 | 3 |

Four prior runs of the same template emitted **zero** warnings. Today's run emits **three**.

### What did NOT change in the regression window

- `git log -- src/FlowTime.Core/Analysis/InvariantAnalyzer.cs` — last commit is `044d2bc` (E-0016-03 era, pre-window). Conservation logic untouched. The mismatch tolerance is unchanged.
- `git log -- templates/transportation-basic.yaml` — last commit `a06ed88` (M-0006.01 era). Template YAML and default parameters unchanged.

### What DID change in the regression window

Between 2026-04-24 (last clean runs) and 2026-04-28 (offending run), **E-0024 Schema Alignment** merged to main on 2026-04-25 (D-0051). Relevant commits in `src/FlowTime.Core/`:

- `131dd35` — M-0050 step 1: `ProvenanceDto`, `OutputDto` cleanup, YamlDotNet 17.0.1
- `5a2a31d` — post-m-E24-02 cleanup: delete `ProvenanceEmbedder`, rename `GridDefinition.StartTimeUtc` → `Start`, drop `Template Legacy*` aliases
- `a7c984f` — M-0052: `ParseScalar` honors ScalarStyle; quote type-ambiguous strings on emit
- `b3efda0` — M-0046 part 1: close `ModelSchemaValidator` silent-error blind spot + restructure node-kind clusters
- `d42f649` — M-0046 AC4: 12 cross-reference/cross-array adjuncts on `ModelSchemaValidator`
- `4fe8d45` — M-0048: delete `ModelValidator`; relocate `ValidationResult`

The shape of the model passing Sim → Engine changed. The pre-E-0024 runs' `spec.yaml` carries `generator: flowtime-sim`, `topology:` block, `classes: []` (the legacy `SimModelArtifact` shape). The post-E-0024 run's `spec.yaml` is the unified `ModelDto` form — no `topology:` block, single-level `values:`. Same source template, same parameters, materially different compiled-graph shape.

### Likely cause (hypothesis — needs Engine-team confirmation)

E-0024's model unification changed how the queue-and-route flow compiles into the runtime DAG. The conservation invariant now fails at the three downstream service nodes by a small but non-trivial margin. Specific suspect: how the central queue's outgoing edges, lag, and the downstream `arrivals` series wire together post-unification — the unified shape may emit a slightly different routing semantic for `serviceWithBuffer` → service hop than the pre-unification pipeline did.

Alternative benign reading: the analyser is correctly detecting a real model property that was always present but didn't fire pre-unification because the lag handling or edge volume computation produced an exactly-canceling difference. In that case the "regression" is actually the analyser working *better* post-unification, not worse. The 1.89 / 9.46 / 7.57 magnitudes lean against this — they're at non-trivial scale.

### Status

**Diagnosed 2026-05-01 in `patch/edge-flow-mismatch`.** Hypothesis (2) is correct — **a real model issue in `transportation-basic` (and at least 8 sibling templates) that was masked pre-E-0024 and is now correctly surfaced.** The fix is template- or engine-side and requires a design decision on which authority wins (expr layer vs topology edge weights). Not blocking — gated by the Phase 2 baseline canary added in the same patch.

**Mechanism, confirmed by reading run artifacts side-by-side:**

- `arrivals_airport[t]` (LineAirport's incoming series) is byte-identical between the clean (pre-E-0024, 2026-04-24) and dirty (post-E-0024, 2026-04-28) runs at value `2.270` per bin (= `hub_dispatch * splitAirport (0.3)` per the template's expr nodes).
- `hub_dispatch[t]` (HubQueue's served, the source of all three line-arrivals) is also byte-identical at value `7.567` per bin.
- The `queue_to_airport` edge declaration is byte-identical (`from: HubQueue:out, to: LineAirport:in, weight: 1`).
- **The pre-E-0024 run has no per-edge `flowVolume` series at all** (48 series files); the post-E-0024 run has 11 such files (59 total) including `edge_queue_to_airport_flowVolume` at value `2.522` per bin (= `hub_dispatch / 3`, the engine's weight-uniform apportionment).
- The analyser's incoming-conservation check (`src/FlowTime.Core/Analysis/InvariantAnalyzer.cs:323-335`) reads `edgeFlowSeries[edgeId]`. Pre-E-0024, the series wasn't there → `TrySumEdgeFlows` returned `false` → the check **silently skipped**. Post-E-0024, the series is there → the check runs → finds `2.270 != 2.522` → emits `edge_flow_mismatch_incoming`.

**The divergence is real**, not a tolerance artifact. The template uses parameter-driven splits in the expr layer (`splitAirport=0.3`, `splitIndustrial=0.2`, downtown implicit `0.5`) but every queue→line edge has `weight: 1`, so the engine's weight-based edge-flow computation produces uniform 1/3 splits. The conservation invariant `arrivals == sum(incoming_edges_after_lag)` correctly detects the mismatch.

**The "regression" is improved analyser coverage**, not a new bug. Edge `flowVolume` series began being emitted somewhere in the E-0023 / E-0024 commit chain (the writers `EdgeFlowMaterializer.cs` + `RunArtifactWriter.cs` weren't directly touched in the window — likely a downstream effect of the model-unification changes that gave the materializer the structural information it needed).

### Outstanding design question — resolved 2026-05-05

> **Resolution:** Option 1 (edge weights win) was selected and ratified as [ADR-0001 — Flow-Authority Policy](../../docs/adr/ADR-0001-flow-authority-policy.md) under M-0066 AC-9. Class-3 fan-outs (the `transportation-basic` family captured below) are governed by edge-weight authority; consumer-side peer-relative expr arithmetic for splits is rejected. Engine and template alignment work to apply the policy to the 9 affected templates lives in [M-0067 — Engine + template alignment](../epics/E-0025-engine-truth-gate/M-0067-engine-template-alignment.md). Class-2 capacity-aware allocation is deferred to [G-0038](G-0038-class-2-capacity-aware-allocator-deferred-from-m-066-flow-authority-policy.md). The historical design discussion below is preserved unchanged for audit.

Which authority wins for edge flow volumes — **expr nodes** (`arrivals_airport = hub_dispatch * splitAirport`) or **topology edge weights** (`queue_to_airport: weight: 1`)?

Three options:

1. **Edge weights win.** Templates must encode splits in edge weights, not in expr arithmetic. Current templates need editing (8+ templates affected). Pro: edges are the canonical structural language; expr nodes inflating splits manually is redundant. Con: 8+ templates to fix; possibly other modeling patterns to reconsider.
2. **Expr authority wins.** Engine should not auto-derive edge flow from weights when an expr node already produces the receiver's `arrivals` series. The conservation check would need to skip edges whose target's `arrivals` is computed (not derived). Pro: templates as-is are correct; minimal change. Con: changes the engine's edge-volume semantics for an entire class of models.
3. **Both surfaces are normative; the template should make them agree.** Templates use parameters in BOTH places (expr splits AND edge weights). The author commits to the redundancy and tooling helps keep them in sync. Pro: single source of truth conceptually. Con: more verbose templates; tooling needed.

This is an Engine architecture call, not something the M-0045 patch resolves. The Phase 2 canary baseline locks in current state so future drift is detected; the design call lives in a future engine milestone (likely the same "engine truth gate" or testing-rigor follow-up that will eventually look at golden-output canaries).

**Affected templates (run-warn count under the Phase 2 canary, captured 2026-05-01):**

| Template | run-warn |
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

The 1.89 / 9.46 / 7.57 worst-case mismatch values originally cited from `data/runs/run_20260428T165413Z_6ed5974e` for `transportation-basic` are still informative as the magnitude signal.

### Quick-win canary added (2026-05-01, `patch/edge-flow-mismatch`)

Phase 2 of `Survey_Templates_For_Warnings` now hard-asserts a per-template `run-warn` baseline against `ExpectedRunWarnings`. Any drift (upward or downward) fails the build with a clear before/after message. The 9 known-non-zero baselines above are encoded with rationale pointing back to this gap entry. Fix the underlying template/engine question and the baselines drop to zero in the same commit; ship a different change that bumps any baseline and the test fails until the dictionary is updated deliberately. **The canary now actively prevents the class of silent drift that produced this finding.**

### Why the build-time canary did not catch this

`tests/FlowTime.Integration.Tests/TemplateWarningSurveyTests.cs::Survey_Templates_For_Warnings` (E-0024 M-0053) hard-asserts **`val-err == 0`** across all twelve shipped templates. `edge_flow_mismatch_incoming` has severity `warning`, not error — so it never fails the canary. The canary collects val-warn counts as diagnostic output but does not assert on them. **A val-warn delta of +3 on `transportation-basic` slipped through silently.**

This is a known weakness of the canary: it gates on validator errors only, not on analyser warnings. See sibling gap entry below for the broader testing-rigor argument.

### Reference

- Offending run (today): `data/runs/run_20260428T165413Z_6ed5974e/run.json` — three `edge_flow_mismatch_incoming` items in `warnings[]`.
- Clean baseline (pre-E-0024): `data/runs/run_20260424T150244Z_b2f4c995/run.json` — empty `warnings[]`.
- Analyser source: `src/FlowTime.Core/Analysis/InvariantAnalyzer.cs:323-335` (incoming-edge conservation check) and `:330` (warning message).
- Template: `templates/transportation-basic.yaml`.
- Canary: `tests/FlowTime.Integration.Tests/TemplateWarningSurveyTests.cs`.

### Immediate implications

- Plan an Engine-side investigation milestone — likely a small "E-0024 follow-up" or new E-0025 micro-milestone — before further analyser/engine work that depends on the conservation invariant being clean across all shipped templates.
- Treat any new appearances of `edge_flow_mismatch_incoming` (or `_outgoing`) on previously-clean templates as a regression signal until this is resolved.
- Do NOT delete the offending run artifact (`run_20260428T165413Z_6ed5974e`) — it is the reproduction case.
- m-E21 / m-E22 work can proceed in parallel; this regression is engine-side and does not block consumer-surface work.

---
