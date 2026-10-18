# FlowTime — Features for Telemetry-Based Analysis

**Status:** Proposed — exploration only; nothing described here is implemented.
**Date:** 2026-10-17, revised 2026-10-18

FlowTime's flow model is the right abstraction for analysing a distributed system from its telemetry; seven features would turn it from a modeling tool into an analysis tool, and the first two matter most: building models from telemetry, and making the gaps between model and telemetry the main object of analysis. Its first use is explaining what happened in an incident; predictions follow, and only where the evidence reaches.

These features build on the seeded rewrite (`rewrite/spec.md`). Its model language, its decision to make variability an input (decision 13), and its oracle and simulation suite (decisions 10 and 11) apply throughout.

## The analysis loop

![Fig. 1. Telemetry, simulated first and then real, is read into ingestion and flow accounting (F1 adapters, F7 topology, F2 flow balance before any model). A calibrated model is fitted to it (F1, variability included, with provenance and identifiability). The model is compared with the telemetry to give residuals per node and bin (F2), highlighted as the main object of analysis. The largest residuals go to explanation (F3 feedback, F4 tails, F5 bin-size sensitivity), which refines the model, and the refined model feeds what-if analysis with bands (F6, sweeps, goal-seek, optimize).](spec/1-analysis-loop.svg)

Telemetry is checked for flow balance before any model exists. Once a model is calibrated, its residuals drive the explanation, and each explanation that shrinks a residual refines the model before any what-if result is trusted. A what-if is trusted only at the loads where the mechanisms it rests on have been checked (F1's coverage map).

## First use: what happened

FlowTime's first use is post-incident analysis on real telemetry: replay an incident with the demand that actually arrived, compare every series, and explain the residuals. Predictions come from a model derived this way, never from one written by hand.

- **An incident holds the stress.** Timeouts, retries, held workers and backpressure act only under load. Calm telemetry can't show them; one incident can.
- **Explaining needs no prediction.** A replay is fed the observed demand, so every claim it makes is checked against the same window.
- **Its what-ifs are the safest kind.** A counterfactual on the incident window, such as a breaker opening a minute earlier or a shorter timeout, asks about conditions the model has just been checked against.
- **Each incident adds checked ground.** A mechanism that explained an incident is checked at that load, and later predictions lean on it there and nowhere else.

More telemetry helps when it covers more conditions. A deploy, a scale-up, a load test or an incident adds a condition the model hasn't been checked against; another month of calm days adds little.

A derived model still has a built part: people write the mechanisms in the library. Telemetry sets their parameters and chooses between them, and a residual that no mechanism explains shows where the library falls short (F2).

Built models remain for systems that don't exist yet, such as a new service or a launch. Every parameter in them is *assumed* (F1), and their results say so.

## Features at a glance

Three features come first because without them FlowTime analyses its author's assumptions, not the system.

| Feature | Question it answers for the analyst | Today (repo, 2026-10-17) | Priority |
| --- | --- | --- | --- |
| F1 Telemetry to model | What does this system actually look like, and which parameters are evidence rather than guesses? | ingestion (E-0015) and model fit (E-0022) are proposed, not built | first |
| F2 Residuals and flow accounting | Where does reality differ from the model, and where is flow nobody measures? | conservation checks run on models, not on real telemetry | first |
| F7 Topology and fan-out from traces | Who calls whom, and how many downstream calls does one request trigger? | topology is written by hand; inference is proposed in E-0015 | first, as input to F1 |
| F3 State-dependent feedback | Why did overload cascade, and why didn't the system recover? | retries, WIP limits and SHIFT backpressure exist; no wasted work from timeouts, blocking calls or modes | second |
| F4 Observed tails and reliability flags | What did p99 do, and when are mean-based conclusions unsafe? | means via Little's law; percentiles deferred in the coverage matrix | second |
| F5 Bin-size sensitivity and peaks | Would a finer grid change the conclusion? | one fixed grid; the docs advise a smaller bin size | third |
| F6 Uncertainty bands | How sure is this what-if result? | sweeps exist; results are single deterministic lines | third |

## F1 — Telemetry to model

Without calibration, every FlowTime result is a consequence of the author's assumptions; with it, results are claims about the real system.

**Why it is needed.** Typical telemetry gives request rate, error rate and duration per service, plus CPU and saturation figures. It rarely gives queue depth or capacity: the real queues sit in thread pools, connection pools, kernel buffers and clients. Capacity is only visible while a service is saturated; the rest of the time the data shows only a lower bound. And real systems wait well below capacity, because arrivals bunch up and service times vary. A model of averages alone has one way to explain that waiting, a lower capacity, so a fit without variability reports a plausible capacity that is wrong.

**What it is.**

- **Adapters** that read aggregated series from metrics stores and log aggregations into FlowTime's binned format, with units and bin alignment checked on the way in.
- **Calibration** that fits capacities, service times, the variability of arrivals and service, retry kernels and routing weights to observed arrivals, served counts, errors and durations.
- **Variability from latency.** How latency rises with load identifies both a service's capacity and how uneven its traffic is; the fitted variability feeds the model language's variability input.
- **Parameter provenance** on every parameter: *configured* (read from deployment and client settings: replica counts, worker and connection pool sizes, timeouts, retry policies), *observed* (read from telemetry), *inferred* (fitted, with a range), or *assumed* (written by the author). Settings are facts, neither measured nor guessed, so they get their own class rather than counting as observed. Every result shows which assumed parameters it depends on.
- **Identifiability flags.** A service that never saturated gets "capacity at least X", not a point estimate. A parameter the data can't pin down is reported as such instead of fitted to noise.
- **A coverage map per mechanism.** Identifiability extends from parameters to mechanisms. For each mechanism and load band (below 70% of capacity, 70 to 95%, 95 to 100%, overload), the map records whether a replay at that load has checked it, telemetry has only seen it act, or it has never been seen. A what-if that leans on a mechanism outside its checked ground carries that flag and names the cheapest way to settle it, such as a setting to read or a load test.

**Today.** Telemetry runs pre-bake captured CSV files into constant nodes. E-0015 (ingestion, topology inference) and E-0022 (model fit, chunked evaluation) are proposed.

**Done when** a run written out as telemetry and read back through the adapters gives the same values in every bin, a fit on simulated telemetry recovers the simulation's true capacities and variability within a stated tolerance, and then a model built from a week of real telemetry reproduces the observed served counts and latencies within a stated tolerance, with every parameter carrying its provenance and every mechanism its coverage.

## F2 — Residuals and flow accounting

Deep analysis is mostly finding where the model and reality disagree and explaining why; FlowTime should make that disagreement its main output.

**Why it is needed.** A model that fits on average can still be wrong in the bins that matter: during an incident, a deploy, a traffic shift. And telemetry itself is often inconsistent in ways nobody notices until the numbers are put through a balance. Residuals are only worth reading once the model's known blind spots are closed: without variability, every service running between about 70% and 95% of capacity would show a residual that comes from the model averaging away randomness, not from the system.

**What it is.**

- **Flow accounting on raw telemetry, with no model.** Per node and bin: arrivals − served − errors − drops − change in queue. A persistent non-zero balance points at unaccounted flow: hidden retries, a queue nobody measures, silent drops, missing or double-counted instrumentation.
- **Model residuals.** Model minus telemetry, per node, series and bin, ranked by size and persistence, and shown on the topology over time.
- **Explanations as hypotheses.** For a large residual, the candidate causes the model can test: a capacity change, an unmodelled retry path, a routing shift. Rerunning with one parameter changed shows whether the residual disappears. A residual that no mechanism in the library explains stays open until a mechanism is added, and the next incident checks it.
- **Competing mechanisms.** When two mechanisms both fit, both run side by side, the series and bins where they disagree are shown, and the telemetry that would decide between them is named. An api that holds a worker for each synchronous call and one that doesn't can match the same calm latency; under load they put the queue in different places and call for different fixes, and the api's worker-pool gauge tells them apart even before the load arrives.

**Today.** The invariant analyzer checks conservation per node, class and edge, and the coverage matrix calls this one of FlowTime's strongest areas. It runs on models. The telemetry-loop exploration compares a model with telemetry the model generated itself, not with real telemetry.

**Done when** a real telemetry bundle can be checked for flow balance without writing a model, every calibrated run reports its residuals per node and bin, and two mechanisms that both fit are reported side by side with the deciding telemetry named.

## F3 — State-dependent feedback

The failures distributed-systems analysts care most about are feedback loops, and FlowTime can model them with the same deterministic recurrences it already uses.

**Why it is needed.** Metastable failures are the clearest case: the system stays degraded after the trigger is gone, because a sustaining effect keeps load above what it can do usefully. Huang et al. studied 22 such failures from 11 organizations and found at least 4 of 15 major AWS outages in the past decade were metastable. In Brooker's summary of Bronson et al., clients time out but keep sending, the server completes work nobody is waiting for, and "throughput stays high while goodput drops to zero". Today "metastable" appears nowhere in the repo, and "goodput" twice.

**What it is.**

- **Timeouts and wasted work.** A served request whose latency exceeds the caller's timeout counts as throughput but not goodput; the caller's retry adds load on top.
- **Blocking calls.** An edge marked synchronous holds the caller's concurrency while it waits, so effective capacity ≈ concurrency limit / dependency latency (the formula already in `docs/architecture/dependency-ideas.md`). A slow dependency then consumes upstream capacity, which is how failures cascade.
- **Capacity that depends on load.** Under overload, useful capacity can fall: thrashing, lock contention, cache misses. A capacity curve over utilization or queue depth expresses it.
- **Modes.** Circuit breakers (closed, open, half-open), autoscalers with a lag, degraded and recovery states, each with its own parameters: the modal models the Ptolemy exploration recommends.

**Today.** Retries with kernels, WIP limits with overflow and SHIFT-based backpressure exist. Autoscale nodes are listed as not implemented; blocking calls and timeouts are discussed in `dependency-ideas.md` but not modeled.

**Done when** a template reproduces a retry-and-timeout metastable failure: arrivals return to normal after a spike, and the model stays degraded until the retry or timeout parameters change. The simulation suite's retries-with-timeouts system is the reference before a real incident is.

## F4 — Observed tails and reliability flags

FlowTime should show measured tails beside its modeled estimates and say when the estimates can't be trusted. A model of averages can't predict p99; the per-bin waiting estimates from the variability input give typical waits, and approximate percentiles only once each queue's spread is tracked.

**Why it is needed.** Service-level objectives and user pain live in p95 and p99, while Little's law and flow recurrences give averages. Dean and Barroso show that at large scale, short high-latency episodes that don't matter in smaller systems come to dominate overall performance. Estimates that reach those tails have to be judged against measurement before they are trusted, or they undermine the explainability FlowTime is built on.

**What it is.**

- **Histograms as observed series.** Read the latency histograms metrics systems already export and carry p50, p95 and p99 per node and bin next to the modeled mean.
- **Reliability flags** on bins where mean-based conclusions are unsafe: high utilization, high variability (the per-bin waiting estimate shows where waiting time explodes), and transients where Little's law's steady-state assumption fails.
- **Tail amplification on fan-out edges**, as a warning: a request that waits for the slowest of N calls has a longer tail than any one of them.

**Today.** Latency is derived from queue depth and served counts. The coverage matrix lists percentile distributions as deferred: "Can only say 'average is X'". PMFs are collapsed to their averages in both engines, Kingman's approximation uses a spread only for PMF nodes, and the Sim service's percentile summary reports percentiles of per-bin values, not of request times.

**Done when** every node with histogram telemetry shows measured percentiles beside the modeled estimate, and bins outside the model's reliable range are marked.

## F5 — Bin-size sensitivity and within-bin peaks

The bin size is an analysis choice that can change the conclusion, so FlowTime should test it rather than leave it implicit.

**Why it is needed.** A one-minute burst inside a five-minute bin disappears into the average, and a queue that builds and drains within one bin shows as zero. The docs advise a smaller bin size, but telemetry often can't support one: hourly aggregates can't be split into five-minute bins without inventing data, as `docs/flowtime-v2.md` itself warns.

**What it is.**

- **A sensitivity check.** Where the data allows two resolutions, run both and flag any conclusion that changes, such as which node is the bottleneck or whether a backlog clears.
- **Peaks within a bin.** Read maximum-within-interval series where telemetry has them (gauge maxima, peak queue depth) and flag bins where the peak exceeds capacity even though the mean doesn't.

**Today.** One fixed grid per run, chosen by the author; sub-bin dynamics are a deliberate trade-off documented in `docs/flowtime.md`.

**Done when** an analysis can report "this conclusion holds at both 1-minute and 5-minute bins", or flags that it doesn't.

## F6 — Uncertainty bands

A what-if answer should come as a range, because its inputs are ranges; FlowTime's speed makes that cheap without giving up determinism.

**Why it is needed.** A single deterministic line reads as precise even when capacities are inferred within a range (F1) and tomorrow's arrivals are unknown. Decisions about capacity or staffing turn on whether the bad case is tolerable, not on the average case.

**What it is.**

- **Ensembles over parameter ranges.** Run the model across the ranges F1 attaches to inferred parameters, with a fixed seed so the ensemble itself is reproducible.
- **Ensembles over history.** Replay the arrival patterns of several real days, not one synthetic pattern.
- **Bands from each queue's spread.** Once the model tracks each queue's spread alongside its average, every queue and wait carries a band without an ensemble.
- **Bands in every result:** median and an interval per series, with the inputs that widen the band most named.
- **Bounded by coverage.** A band covers uncertainty in parameters, not in mechanisms. A what-if that moves a mechanism outside its checked ground (F1's coverage map) shows that flag beside its band.

**Today.** Sweeps, sensitivity and goal-seeking exist and re-evaluate the model many times; results are presented as single runs.

**Done when** a what-if result shows a band, and the parameter that contributes most to its width is named.

## F7 — Topology and fan-out from traces

Metrics say how busy each service is but not who calls whom; distributed traces say both, and should be the primary source of topology.

**Why it is needed.** A hand-written topology encodes what the author believes the architecture is. The real call graph drifts with every deploy, and fan-out (one request triggering N downstream calls) multiplies load in ways no per-service metric shows.

**What it is.**

- **Edges from aggregated spans:** caller, callee and call count per bin, read from span counts instead of individual traces.
- **Fan-out multipliers** per edge: downstream calls per upstream request, as an observed series.
- **Synchronous or asynchronous** per edge, where the span data shows whether the caller waited, feeding the blocking-call model in F3.
- **Topology drift:** edges that appear or vanish between periods, flagged against the model.

**Today.** Topology is written in templates; E-0015 proposes inferring topology from event logs, traces and sensor feeds; it is not built.

**Done when** a model's topology and fan-out multipliers can be generated from a span aggregation, and differences from the hand-written topology are reported.

## What FlowTime should not do

These features keep FlowTime's existing guardrails; none of them needs individual requests, entity tracking or a general modeling lab.

- **No per-request simulation or item tracking.** The guardrail in the whitepaper ("Stay Flow-First (No DES)") stands; F3's timeouts, blocking calls and modes are all recurrences over bins. A discrete-event simulation exists only as test tooling outside the product: the seeded rewrite's validation suite.
- **No observability platform.** Storage, dashboards, alerting and raw log ingestion belong to existing tools; FlowTime reads their aggregated series through adapters.
- **No tails from averages alone.** Measured percentiles are shown as observed (F4). Modeled percentiles come only from the variability estimates, once each queue's spread is tracked, and only after the simulation suite has judged them.
- **No generic anomaly detection.** Observability tools already have it. FlowTime's own kind is "the flow doesn't add up here" (F2).
- **No process mining of individual cases.** Event logs are a source of aggregated flows only; tracking cases would bring per-item simulation in through the back door.
- **No general modeling lab.** Modes (F3) borrow one Ptolemy idea; the rest stays outside, as the Ptolemy design note already concludes.

## Order of work

These features follow the seeded rewrite's core, through its analysis and session phase (`rewrite/spec.md`). Every step is checked first on simulated telemetry from the seeded rewrite's simulation suite, where the true parameters are known, and only then on real telemetry. Within them, flow accounting on raw telemetry comes first, because it needs only ingestion and gives an analyst something useful before any model exists. Steps 1 to 4 applied to an incident window make the first use, a replay with residuals, and counterfactuals on that window are the first what-ifs.

![Fig. 2. Three sources of telemetry, checked in order. First, a FlowTime run's own series, with no randomness, are written out and read back through the F1 adapters, with no model involved; the check is whether the same numbers come back, which catches a value the adapters lose or change but says nothing about whether the model is realistic. Second, a request-by-request random simulation with known capacity and variability, test tooling that is never shipped, is binned and put through the whole analysis loop, the same code real telemetry goes through; the check is whether the loop recovers the truth, which catches a fit that reports the wrong capacity but misses what the simulation leaves out. Third, real telemetry goes through the same loop, and then on to what-if with bands, with no answer key; the check is the residuals, model minus telemetry, which catch where the system differs from the model but miss a wrong model that happens to fit.](spec/2-three-checks.svg)

1. **Ingestion adapters and raw flow accounting** (F1's adapters, F2's first half): read telemetry and check it for flow balance.
2. **Topology from traces** (F7): call graph, fan-out multipliers, synchronous or asynchronous edges.
3. **Calibration with provenance and identifiability** (F1): fitted parameters, variability included, with ranges, and flags for what the data can't determine.
4. **Model residuals** (F2's second half): needs step 3.
5. **Feedback mechanics** (F3): timeouts and goodput first, then blocking edges, then modes. Each is accepted when it shrinks residuals on a real incident.
6. **Observed tails and reliability flags** (F4): independent of steps 3 to 5; can run alongside them.
7. **Uncertainty bands** (F6): needs the ranges from step 3.
8. **Bin-size sensitivity** (F5): small; fits wherever there is room.

Sweeps, sensitivity, goal-seeking and optimization stay as they are. Their value rises once calibration exists, because they then optimize the system rather than the author's assumptions.

## Open questions

- [ ] Which telemetry source first: a metrics store, log aggregations or trace spans?
- [ ] Which variability signals can that source supply: rates within a bin, latency at low load, latency histograms?
- [ ] What calibration method: least squares on served counts, or a fit that weights incident windows more?
- [ ] How are inferred ranges represented in run artifacts, so F6 can use them?
- [ ] Is a real incident with good telemetry available as the acceptance case for F3, and as the first replay?
- [ ] What marks a mechanism as checked at a load band: which residual tolerance, on which series?
- [ ] Do autoscale nodes belong in F3's modes, or stay out as a separate feature?

## Sources

- [Metastable Failures in the Wild](https://www.usenix.org/conference/osdi22/presentation/huang-lexiang) — Huang et al., OSDI 2022
- [Metastable failures in distributed systems](https://brooker.co.za/blog/2021/05/24/metastable/) — Marc Brooker's summary of Bronson et al., HotOS 2021
- [The Tail at Scale](https://research.google/pubs/pub40801/) — Dean and Barroso, Communications of the ACM, 2013
- FlowTime repository (read 2026-10-17): `docs/reference/engine-capabilities.md`, `docs/reference/flow-theory-coverage.md`, `docs/architecture/dependency-ideas.md`, `docs/architecture/whitepaper.md`, `docs/explorations/ptolemy/spec.md`, `docs/explorations/telemetry-loop-parity/spec.md`, `ROADMAP.md`, `rewrite/spec.md` (decisions 10, 11 and 13, and the simulation measurements behind them)
