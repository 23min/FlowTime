# Epic: Question-Driven Interface

**Status:** draft (future — depends on post-E-16 fact surfaces and the relevant analytical primitives)
**Depends on:** E-16 truth-gated state/window contracts, resumed p3b/p3c where needed,
UI Workbench epic, Analytical Views epic
**Architecture:** [reference/ui-paradigm.md](../ui-workbench/reference/ui-paradigm.md)

---

## Intent

Add a structured query interface where users ask **specific analytical
questions** and FlowTime computes deterministic, provenanced answers. This
is not a chatbot — it is a computation interface that shows its work.

The interface starts as a panel of pre-built questions with parameter inputs
and computed results. It can later evolve toward a DSL or LLM-assisted
interaction model, but the foundation is structured computation.

## Why Not a Dashboard?

Dashboards show everything and hope the user spots the insight. The
question-driven interface inverts this: the user states what they want to
know, and the system computes exactly that — with supporting evidence,
provenance, and links to the relevant view.

This matches FlowTime's identity as a deterministic engine. Every answer
is a traceable computation, not an opaque metric on a dashboard.

## Goals

### Q1: Bottleneck Questions

| Question | Computation | Output |
|----------|------------|--------|
| "Where is the bottleneck?" | Cross-node utilization comparison + WIP accumulation detection | Highlight node with highest sustained rho; show supporting evidence (queue growth, capacity saturation) |
| "Is the bottleneck shifting?" | Compare bottleneck node across time windows | Timeline of which node was the constraint in each segment |

**Depends on:** stable post-E-16 fact surfaces plus the bottleneck primitive

### Q2: Cycle Time Questions

| Question | Computation | Output |
|----------|------------|--------|
| "Why is cycle time high at node X?" | Cycle time decomposition (AC-1) + flow efficiency (AC-3) | Stacked breakdown: queue time vs. service time. "82% of cycle time is queue time." |
| "What does queueing theory predict for node X?" | Kingman approximation (m-ec-p3c AC-3) | Predicted vs. actual queue wait. "Kingman predicts 310ms, actual is 280ms — model is consistent." Or: "Actual is 2x predicted — arrivals may be burstier than modeled." |

**Depends on:** stable post-E-16 cycle-time facts, enriched by p3c Kingman diagnostics

### Q3: Capacity Questions

| Question | Computation | Output |
|----------|------------|--------|
| "What if I double capacity at node X?" | Re-run model with modified capacity series | Before/after comparison: cycle time, queue depth, utilization. "Cycle time drops 45%, but bottleneck shifts to auth_svc." |
| "What if I add a WIP limit of N at node X?" | Re-run with wipLimit set | Impact on queue depth, overflow volume, upstream effects |

**Depends on:** p3b plus scenario overlay infrastructure

### Q4: Health Questions

| Question | Computation | Output |
|----------|------------|--------|
| "Is this model healthy?" | Conservation checks + steady-state validation | List of warnings, invariant violations. "3 nodes have conservation violations > 5%." |
| "Are my cycle time estimates reliable?" | Steady-state validation (AC-5) | "Arrival rate diverges 38% across window — Little's Law estimates may be unreliable." |
| "Where is starvation or blocking?" | Starvation/blocking detection (W2.6) | List of bins and nodes where starvation or blocking was detected |

**Depends on:** stable post-E-16 health/warning facts plus the relevant resumed analytical primitives

### Q5: Flow Distribution Questions

| Question | Computation | Output |
|----------|------------|--------|
| "How does work split across classes?" | Per-class arrivals/served aggregation | Class breakdown: "Orders: 60%, Refunds: 25%, VIP: 15%." |
| "Which class has the worst cycle time?" | Per-class cycle time comparison (AC-2) | Ranked table with per-class queue time, service time, flow efficiency |

**Depends on:** stable post-E-16 class-truth and cycle-time facts

## Interaction Design

### Structured Query Panel

```
+---------------------------------------------+
| Question: [dropdown or search]              |
| "Why is cycle time high at ___?"            |
|                                             |
| Node: [api_svc v]                           |
| Window: [all bins v]                        |
|                                             |
| [Compute]                                   |
+---------------------------------------------+
| Answer:                                     |
|                                             |
| Cycle time at api_svc: 340ms                |
|   Queue time:   280ms (82%)  ||||||||||||   |
|   Service time:  60ms (18%)  |||            |
|   Flow efficiency: 0.18                     |
|                                             |
| Kingman predicts: 310ms (Ca=1.2, Cs=0.4)   |
| Delta: -30ms (actual < predicted)           |
|                                             |
| [View in Decomposition ->]                  |
| [Pin to Workbench ->]                       |
+---------------------------------------------+
```

Key principles:
- **Provenance:** Every number links to the formula and inputs that produced
  it. Click "280ms" to see "queueDepth=14, served=3, binMs=60000".
- **Cross-linking:** Answers link to the relevant view (decomposition, heatmap)
  and can pin nodes to the workbench.
- **Deterministic:** Same model + same question + same parameters = same
  answer. Always.
- **Graceful degradation:** If a required primitive is unavailable (e.g.,
  Kingman needs Cv, but model has no PMFs), say so: "Kingman approximation
  unavailable — arrivals are deterministic (Cv=0)."

### Future: DSL Integration

When a DSL exists, the question dropdown can accept typed expressions:

```
cycleTime("api_svc", bin=5)
bottleneck(metric="utilization", window=[3,7])
whatIf("db_pool", capacity=200%)
```

The DSL produces the same structured output as the pre-built questions.

### Future: LLM Layer

An LLM can translate natural language to DSL queries, adding conversational
flexibility while preserving determinism:

```
User: "Why is the API slow?"
LLM -> cycleTime("api_svc") + bottleneck()
Engine -> computed results
LLM -> "api_svc has 340ms cycle time, 82% of which is queue time.
        The bottleneck is db_pool at 92% utilization."
```

The LLM never invents numbers — it translates questions to engine queries
and narrates the deterministic results.

## Non-Goals

- Free-form chat about topics outside FlowTime's model
- Probabilistic / Monte Carlo answers (engine is deterministic)
- Real-time monitoring or alerting
- Building a general query language (start with pre-built questions)

## Open Questions

1. **Panel placement:** Dedicated page? Slide-over panel accessible from any
   view? A command palette (Cmd+K) style interaction?

2. **Question catalog:** Who curates the list of available questions? Start
   with a hardcoded set and see which ones users actually use before building
   extensibility.

3. **Server-side vs. client-side computation:** Pre-built questions that only
   rearrange existing `/state` data can run client-side. What-if questions
   that re-run the model require a server round-trip. The UI should handle
   both transparently.

4. **DSL design:** This epic describes the UI. The DSL itself is a separate
   effort (language design, parser, engine integration). This epic should not
   block on DSL availability — pre-built questions work without one.

## Milestones

To be defined during planning. Likely sequence:

1. Question panel scaffold (UI chrome, question selector, parameter inputs)
2. Health questions (conservation, steady-state — uses existing engine output)
3. Cycle time questions (decomposition, Kingman — after the post-E-16 fact surface is stable, enriched by p3c)
4. Bottleneck questions (after the stable post-E-16 bottleneck fact surface is available)
5. Capacity what-if questions (after scenario overlay infrastructure)
6. DSL integration (after DSL epic delivers a parser)
