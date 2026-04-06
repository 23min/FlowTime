# Predictive Systems, Uncertainty, and FlowTime's Position

A design note examining how FlowTime relates to other predictive systems, what makes its approach distinct, how it should handle uncertainty, and what it should not become.

---

## Are we inventing anything new?

No — and yes.

**No**, in the sense that all models predict. That is literally what a model is: a simplified representation that lets you compute states you have not observed. Newton's laws predict where a cannonball lands. A weather model predicts tomorrow's temperature. An M/M/1 queue model predicts steady-state wait time. FlowTime predicting future queue depth from observed arrivals is the same idea that has been around since the dawn of mathematical modeling.

**Yes**, in the sense that FlowTime occupies a specific and underserved point in the design space. The contribution is not "prediction" in the abstract — it is a particular *combination* of properties that do not usually coexist in a single system.

---

## The landscape of predictive systems for flow and operations

### Analytical queueing models (M/M/1, Erlang-C, Kingman)

Closed-form formulas. Given arrival rate, service rate, and variability, compute steady-state queue length, wait time, utilization. Extremely fast, mathematically precise, but they assume *stationarity* — the system is in steady state. They cannot model time-varying demand, transient behavior, or multi-stage topologies with feedback. They answer "what is the long-run average?" not "what happens at 14:00 when the lunch rush hits?"

### Discrete-event simulation (Arena, AnyLogic, SimPy)

Track individual entities through a system. Stochastic by nature — each run is different because arrivals and service times are sampled from distributions. Run thousands of replications and get probability distributions of outcomes. Very flexible (can model anything), but slow, opaque (hard to trace why a specific outcome occurred), and non-deterministic (need many runs for statistical confidence).

### System dynamics (Stella, Vensim, Powersim)

Stocks-and-flows on continuous differential equations. Model accumulation and feedback at aggregate levels, but usually continuous-time and focused on long-term policy dynamics rather than operational bin-level prediction. Deterministic, but the models tend to be high-level.

### ML / time-series forecasting (Prophet, ARIMA, LSTMs)

Data-driven. Learn patterns from historical data, extrapolate forward. Probabilistic by nature (confidence intervals). Good at capturing patterns they have seen before, bad at structural reasoning ("what happens if we add a retry loop?"). They predict *what the numbers will look like* without modeling *why*.

### Process mining tools (Celonis, Disco)

Reconstruct process graphs from event logs. Strong at discovery and conformance checking. But they are fundamentally backward-looking — they tell you what happened, not what will happen. Their "predictions" are usually statistical extrapolation of historical patterns, not structural simulation.

---

## Where FlowTime sits

FlowTime is none of these. It combines properties that do not usually coexist:

| Property | Queueing theory | DES | System dynamics | ML forecast | Process mining | FlowTime |
|----------|----------------|-----|-----------------|-------------|---------------|----------|
| Time-varying demand | No | Yes | Somewhat | Yes | No | Yes |
| Multi-stage topology | Limited | Yes | Yes | No | Yes (observed) | Yes |
| Feedback / retries | Limited | Yes | Yes | Learned | Observed | Yes (explicit) |
| Deterministic | Yes | No | Yes | No | N/A | Yes |
| Traceable / explainable | Yes | No | Somewhat | No | N/A | Yes |
| Bin-level operational detail | No | Yes | No | Yes | No | Yes |
| Fast (milliseconds) | Yes | No | Yes | Yes | N/A | Yes |
| Structural what-if | Limited | Yes | Yes | No | No | Yes |

The unique combination is: **deterministic + time-varying + multi-stage + explicit feedback + traceable + fast + structural what-if**. No single other system does all of these simultaneously.

DES can model everything FlowTime can, but it is stochastic, slow, and opaque. Queueing theory is deterministic and fast, but cannot handle transients or topologies. ML is fast and handles time-variation, but cannot do structural what-if. FlowTime threads the needle.

---

## How probabilistic systems differ from FlowTime

The core difference is philosophical, and it matters for how predictions are used.

### Probabilistic systems say: "Here is a distribution of possible futures."

A DES simulation of a payment pipeline might report: "Across 10,000 replications, the queue depth at settlement at bin 8 has mean 47, median 42, 95th percentile 78." This is honest about uncertainty. But:

- You cannot trace *why* the 95th percentile is 78. Which specific sequence of events caused it? The answer is "many different sequences, stochastically."
- You cannot do structural what-if cleanly. "What if I add 20% capacity?" requires re-running 10,000 simulations.
- The answer depends on how many replications you ran. 100 runs gives different confidence intervals than 10,000.

### FlowTime says: "Here is the exact future implied by these inputs and this algebra."

FlowTime's prediction of queue depth 47 at bin 8 is the *algebraic consequence* of the input series and the model's formulas. You can trace every step. You can change one parameter and get the new answer in milliseconds. But:

- The prediction has zero uncertainty bars. It does not tell you "47 plus or minus 15."
- If the real system has stochastic behavior (variable service times, random failures), the model's deterministic projection is the *expected value* — the center of the distribution that the real system samples from. The tails are invisible.

### The practical difference

A probabilistic system tells you "there is a 15% chance the queue exceeds 70." That is actionable for risk management.

FlowTime tells you "if arrivals follow this pattern, the queue reaches 47." That is actionable for structural understanding and intervention design.

They answer different questions. The probabilistic system answers "how bad could it get?" FlowTime answers "here is exactly why it gets bad, and here is what changes it."

---

## How to improve FlowTime as a predictive system

Three directions, in order of architectural fit.

### 1. Confidence bands from analytical formulas (fits perfectly)

This is the m-ec-p3c (Kingman's approximation) path. If the model carries coefficient of variation (Cv) for arrival and service processes, the engine can analytically compute confidence bands around the deterministic prediction:

```
Expected queue time:  W = Q / served_rate              (current FlowTime)
Queue time variance:  from Kingman's formula using Cv   (planned, m-ec-p3c)
80th percentile:      W + z_80 * sigma_W                (derived)
```

This gives "expected queue 47, 80th percentile 62, 95th percentile 78" — without running any simulations. It is still a single-pass evaluation, still deterministic (same Cv produces same bands), still traceable. The output is simply richer.

This is the highest-value improvement because it stays within FlowTime's architecture. No Monte Carlo, no stochastic sampling in the engine, no loss of traceability. The bands are analytically derived from the same algebra.

### 2. Scenario ensembles via the Sim layer (fits well)

Instead of running one deterministic model, run N models with slightly different parameters sampled from distributions. The Sim layer already supports stochastic sampling:

- Sample 50 arrival patterns from a distribution around the observed mean.
- Run all 50 through the engine (still milliseconds each).
- Report the distribution of outcomes across runs.

Each individual run is deterministic and traceable. The ensemble gives a distribution. This is cheaper than DES (50 aggregate models, not 10,000 entity-level simulations) and every run is inspectable.

The engine does not need to change. The Sim layer orchestrates the ensemble, the engine evaluates each variant, and a comparison surface reports the distribution.

### 3. Monte Carlo within the engine (possible but dangerous)

Add stochastic evaluation to the engine itself: instead of evaluating `served := MIN(capacity, arrivals)` deterministically, sample service times from a distribution per bin. This is what DES does at a coarser granularity.

This is the most powerful option and also the most dangerous. It breaks the core invariant that same inputs produce same outputs (unless the RNG seed is fixed, which recovers determinism but adds complexity). It makes traceability harder. And it converges on DES territory where FlowTime has no competitive advantage.

### Recommendation

Option 1 (Kingman bands) first — already planned, fits perfectly. Option 2 (Sim ensembles) second — architecturally clean, infrastructure mostly exists. Option 3 (stochastic engine) probably never — it sacrifices FlowTime's identity for marginal gain over existing DES tools.

---

## What FlowTime should NOT become

### Do not make the engine stochastic

The moment the core evaluation loop samples from distributions, you lose:

- **Determinism**: the foundational property.
- **Traceability**: which sample caused this outcome?
- **Speed**: need multiple runs for statistical confidence.
- **Simplicity**: RNG state management, seed propagation, convergence testing.

The Sim layer exists precisely to be the stochastic boundary. Keep the engine pure.

### Do not add per-entity tracking

The temptation is to track individual work items to get exact cycle time distributions, aging WIP, or priority queuing within a queue. This turns FlowTime into a DES. The whitepaper already flags this as a non-goal (section 13.1: "Stay Flow-First, No DES"). The moment you track entities, you need event queues, scheduling policies, and per-entity state machines. You are competing with Arena and AnyLogic on their home turf, without their decades of optimization.

### Do not chase ML-style forecasting

FlowTime should not try to *learn* arrival patterns from data and extrapolate them. That is what Prophet, ARIMA, and LSTMs do, and they are better at it. FlowTime's job is: given arrivals (from whatever source — observed, conjectured, ML-predicted), compute consequences through the topology. Let an external forecasting tool predict arrivals; let FlowTime propagate them. The boundary is: ML predicts *what arrives*, FlowTime predicts *what happens to it*.

### Do not blur the Sim/Engine boundary

The clean separation — Sim produces deterministic series, Engine evaluates them — is load-bearing. If the engine starts accepting distributions as inputs, you need a sampling policy, convergence criteria, and replication management *inside the evaluator*. Keep that complexity in Sim, where it belongs.

---

## Non-deterministic nodes: should FlowTime model uncertainty?

This is the deepest question and deserves careful treatment.

### The problem

Currently, every FlowTime node is a pure function: given input series, it produces exactly one output series. `served := MIN(capacity, arrivals)` is the same every time.

But real systems have nodes that behave non-deterministically even given the same inputs:

- A service with variable processing times (sometimes fast, sometimes slow).
- A router where the decision depends on runtime state (load balancer with random selection).
- A retry mechanism where backoff duration is randomized.
- A human decision point (triage nurse choosing priority based on judgment).
- An external dependency with unpredictable latency (third-party API).

FlowTime has no concept for "this node's behavior is uncertain." Every node has exactly one behavior given its inputs.

### The answer: uncertainty as metadata, not as stochastic execution

The FlowTime model should be able to *describe* non-deterministic behavior. But the engine should not *execute* non-deterministically. The distinction is between knowing that a system has variance and simulating that variance sample by sample.

Three levels of increasing richness, all preserving deterministic evaluation:

#### Level 1: Node-level Cv as metadata

The model carries `Cv_service: 0.3` as a parameter on a node. The engine evaluates the deterministic expected value as today, but also computes analytical bounds using Kingman's formula. The node is not stochastic — it carries a *description of its uncertainty* that the engine uses to compute bounds.

This is what m-ec-p3c is heading toward. It is the right first step.

```yaml
- id: payment_service
  kind: service
  capacity: 80
  variability:
    cv_service: 0.3
    cv_arrivals: 0.5
```

Engine output:

```
Queue depth:  47 (expected)  |  62 (p80)  |  78 (p95)
Latency:      28 min         |  38 min    |  52 min
```

#### Level 2: Per-series distributional metadata

Instead of a single `capacity: 80` series, the model carries `capacity_mean: 80, capacity_cv: 0.3` — or even a full distribution descriptor. The engine still evaluates the expected value, but derived metrics like queue time and latency use the distribution parameters to compute tighter bounds.

This is a step beyond m-ec-p3c. Instead of node-level Cv, it is per-series distributional metadata. The engine remains deterministic; it simply knows more about the uncertainty in its inputs.

#### Level 3: Analytical uncertainty propagation through the DAG

A deeper version where the engine propagates not just expected values through the DAG but also variances. For linear operations, this is straightforward:

```
Var(A + B) = Var(A) + Var(B)                      (independent)
Var(k * A) = k^2 * Var(A)
```

For nonlinear operations (`MIN`, `MAX`, `CONV` with uncertain inputs), exact propagation is not possible, but approximations exist (e.g., delta method, moment matching). The engine would compute approximate output distributions from input distributions, one pass, no sampling.

This is the most ambitious option and would require careful approximation choices. But it would make FlowTime a **variance-aware deterministic evaluator** — a category that essentially does not exist in current tooling.

### What this means for the crystal ball

The crystal ball with uncertainty metadata would produce output like:

```
Node: settlement (prediction horizon: +60 min)
  Queue depth:  47 (expected)  |  62 (p80)  |  78 (p95)
  Latency:      28 min         |  38 min    |  52 min
  Utilization:  0.85           |  0.92      |  0.98
  SLA risk:     Low            |  Moderate  |  High
```

All from a single evaluation pass. No Monte Carlo. The bounds come from analytical formulas applied to the variance metadata carried through the model.

This makes FlowTime a *better* crystal ball than a probabilistic system for operational use, because distributional predictions are produced at evaluation speed (milliseconds), not simulation speed (minutes). A DES needs 10,000 runs to build that distribution. FlowTime computes it analytically in one pass.

### What should NOT be modeled as non-deterministic nodes

**Human decision-making.** Triage nurses, manual routing decisions, judgment calls — these are not meaningfully modelable as distributions. They are better handled as scenario parameters: "what if the triage nurse routes 40% to fast-track instead of 30%?" This is a what-if question, not a variance question.

**Rare catastrophic events.** "What if the database goes down?" is not a variance around a mean — it is a scenario. Model it as a what-if with explicit capacity-to-zero, not as a high-variance service time distribution.

**Correlated failures.** If two services fail together (shared dependency), modeling them as independent distributions with individual Cv values will underestimate the joint failure probability. Correlated failure is better modeled through explicit dependency nodes and shared constraints than through stochastic independence assumptions.

---

## The deeper insight

FlowTime's power as a predictive system comes from being *algebraically precise about the expected case while analytically honest about uncertainty*. It does not simulate randomness — it *reasons about* randomness. That is a fundamentally different and arguably more useful capability for the operational questions FlowTime is designed to answer.

The crystal ball shows a sharp image at the center with a computed haze around the edges. The DES shows a blurry image that gets sharper the longer you stare (more replications). FlowTime's version is faster, cheaper, and more traceable — at the cost of the haze being approximate rather than exact.

For operational decision-making — "should we add capacity?", "when will the backlog clear?", "will Express orders breach SLA this afternoon?" — that trade-off is almost always worth it.

---

## References

- Crystal ball design note: `docs/notes/crystal-ball-predictive-projection.md`
- FlowTime overview: `docs/flowtime.md`
- Whitepaper (non-goals, stay flow-first): `docs/architecture/whitepaper.md` (section 13)
- Flow theory foundations (queueing theory, Kingman's): `docs/reference/flow-theory-foundations.md`
- Flow theory coverage (m-ec-p3c variability milestone): `docs/reference/flow-theory-coverage.md`
- E-10 spec (Phase 3c — variability preservation): `work/epics/E-10-engine-correctness-and-analytics/spec.md`
- E-15 spec (telemetry ingestion, topology inference): `work/epics/E-15-telemetry-ingestion/spec.md`
- FlowTime vs Ptolemy (design boundaries): `docs/notes/flowtime-vs-ptolemy-and-related-systems.md`
