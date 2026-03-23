# Theoretical Foundations of Flow Analysis

Reference document covering the mathematical and conceptual foundations that
underpin flow analysis in software delivery, manufacturing, and knowledge work
systems. Each section identifies what a flow analysis engine must capture to
support the concept.

---

## Table of Contents

1. [Little's Law](#1-littles-law)
2. [Theory of Constraints](#2-theory-of-constraints)
3. [Cumulative Flow Diagrams](#3-cumulative-flow-diagrams)
4. [Queueing Theory](#4-queueing-theory)
5. [Flow Metrics (Flow Framework)](#5-flow-metrics-flow-framework)
6. [Bottleneck Analysis](#6-bottleneck-analysis)
7. [WIP Limits](#7-work-in-progress-wip-limits)
8. [Variability and Its Effects](#8-variability-and-its-effects)
9. [Multi-Stage Pipeline Analysis](#9-multi-stage-pipeline-analysis)
10. [Starvation and Blocking](#10-starvation-and-blocking)
11. [Monte Carlo Simulation](#11-monte-carlo-simulation-for-flow-forecasting)
12. [Aging WIP](#12-aging-wip)

---

## 1. Little's Law

**Importance: CRITICAL**

### Explanation

Little's Law (John D.C. Little, 1961) is the foundational equation of flow
analysis. It states a deceptively simple relationship between three quantities
in any stable system:

```
L = lambda * W
```

Where:
- **L** = average number of items in the system (WIP)
- **lambda** = average throughput (arrival rate = departure rate in steady state)
- **W** = average time an item spends in the system (cycle time)

Rearranged for the forms most used in software delivery:

```
Average Cycle Time = WIP / Throughput
Throughput = WIP / Average Cycle Time
WIP = Throughput * Average Cycle Time
```

The law is remarkably general. It holds for any stable system regardless of
the arrival distribution, service distribution, or service order (FIFO, LIFO,
priority, random). It requires only that the system is in a *steady state* --
the long-run average WIP is constant.

### Assumptions and Limitations

Little's Law holds exactly under these conditions:

1. **Steady state (conservation of flow)**: The average rate at which items
   enter the system equals the average rate at which they leave. Over the
   measurement window, WIP does not trend up or down.

2. **Consistent units**: All three quantities must use the same time unit
   (e.g., items/week, weeks, items).

3. **Average WIP age is stable**: Items that have been in the system a long
   time should not dominate the population. If zombie items accumulate (items
   started but never finished), the "L" grows without a corresponding increase
   in "lambda", and the law's insight breaks down.

4. **Long enough measurement window**: Since the law relates *averages*, short
   windows with high variance produce unreliable results. The law says nothing
   about individual items.

**When it breaks down in practice**:
- Work arrives and departs in large batches (violates smooth averaging)
- Measurement windows are too short (days, not weeks/months)
- Abandoned or zombie items inflate WIP without ever completing
- The system is non-stationary (e.g., team size changing, process redesign)

### Why It Matters

Little's Law provides the theoretical justification for virtually every flow
optimization strategy. It explains *why* reducing WIP reduces cycle time. It
explains *why* you cannot increase throughput by simply starting more work. It
connects the three metrics that every flow tool must track.

### Data/Properties a Flow Engine Must Capture

| Property | Purpose |
|---|---|
| Item start timestamp (entered active work) | Calculate cycle time per item |
| Item completion timestamp | Calculate cycle time; compute throughput |
| Snapshot of WIP count at regular intervals | Track average WIP over time |
| Throughput per time period (items completed) | Compute throughput rate |
| Current item state (in-progress vs. done vs. abandoned) | Identify zombie items that break steady-state |
| Time-series of all three metrics | Validate that the system is in steady state before applying the law |

---

## 2. Theory of Constraints (ToC)

**Importance: CRITICAL**

### Explanation

The Theory of Constraints, developed by Eliyahu M. Goldratt (presented in
*The Goal*, 1984), is a management philosophy built on a single premise:
**every system has at least one constraint that limits its overall
throughput**. System-level throughput can only be improved by improving the
constraint.

#### The Five Focusing Steps

1. **IDENTIFY** the constraint -- Find the stage, resource, or policy that
   limits the throughput of the entire system.

2. **EXPLOIT** the constraint -- Maximize the output of the constraint using
   existing resources. Ensure the constraint is never idle, never works on
   low-priority items, and has everything it needs to operate at full capacity.

3. **SUBORDINATE** everything else to the constraint -- All other stages
   should operate at the pace of the constraint, not at their own maximum
   capacity. Over-producing upstream of the constraint only builds inventory
   (WIP) without increasing system throughput.

4. **ELEVATE** the constraint -- If exploiting and subordinating are not
   enough, invest in increasing the constraint's capacity (add resources,
   automate, redesign).

5. **REPEAT** -- Once a constraint is broken, a new constraint emerges
   elsewhere. Return to step 1.

#### Drum-Buffer-Rope (DBR)

DBR is the execution methodology derived from ToC:

- **Drum**: The constraint sets the pace ("beat") for the entire system. All
  scheduling is based on the constraint's capacity.

- **Buffer**: A time buffer is placed *before* the constraint to protect it
  from upstream variability. This ensures the constraint is never starved for
  work, even when upstream stages have occasional slowdowns.

- **Rope**: A signaling mechanism that ties the release of new work into the
  system to the constraint's consumption rate. The "rope" prevents
  overloading upstream stages and controls WIP.

Simplified DBR (S-DBR) uses only the shipping buffer and market demand as the
drum, which is closer to how knowledge work systems operate.

### Why It Matters

ToC provides the *strategic* layer for flow analysis. While Little's Law
tells you the relationship between metrics, ToC tells you *where to focus*.
In a multi-stage pipeline, improving a non-bottleneck stage has zero effect
on system throughput. This is deeply counterintuitive -- teams frequently
optimize the wrong stage.

For a flow analysis tool, ToC means the engine must be able to identify which
stage is the current constraint and track whether interventions actually
improve system-level throughput.

### Data/Properties a Flow Engine Must Capture

| Property | Purpose |
|---|---|
| Per-stage WIP counts over time | Identify stages with persistent WIP accumulation |
| Per-stage throughput rates | Compare stage capacities; find the minimum |
| Per-stage cycle time / processing time | Find slowest stages |
| Per-stage utilization (active time / available time) | Identify saturated stages |
| Queue depth before each stage | Measure buffer sizes; detect starvation |
| Time-series of constraint location | Track whether the constraint moves after interventions |
| Work item flow events (entered stage, exited stage) | Reconstruct item paths through the pipeline |

---

## 3. Cumulative Flow Diagrams (CFDs)

**Importance: CRITICAL**

### Explanation

A Cumulative Flow Diagram is a stacked area chart where:

- The **x-axis** represents time (days, weeks, sprints)
- The **y-axis** represents the *cumulative* count of items
- Each **colored band** represents a workflow stage (e.g., Backlog, In
  Progress, Review, Done)
- The **top line** of each band shows cumulative arrivals into that stage
- The **bottom line** shows cumulative departures from that stage

The CFD is the single most information-dense visualization in flow analysis
because three critical metrics can be read directly from it:

1. **WIP** at any point in time = vertical distance between the arrival curve
   and the departure curve for a given stage (or set of stages).

2. **Approximate average cycle time** = horizontal distance between the
   arrival curve and the departure curve. This assumes FIFO ordering; with
   priority reordering, it becomes an approximation.

3. **Throughput** = slope of the "Done" line. A steeper slope = higher
   throughput. Throughput over an interval = rise of the Done line over that
   interval.

#### How to Read Patterns

| Pattern | Meaning |
|---|---|
| Parallel, evenly-spaced bands | Stable, healthy flow |
| Widening band | WIP accumulating in that stage -- potential bottleneck |
| Narrowing band | Stage is draining faster than being fed -- excess capacity or upstream starvation |
| Flat "Done" line | Zero throughput -- nothing is being completed |
| Flat arrival line for a stage | Nothing entering that stage -- upstream starvation |
| Sudden vertical jump in arrivals | Batch arrival of work (e.g., sprint planning) |
| Bands converging then diverging | Temporary blockage followed by release |
| Staircase pattern in Done | Work completed in batches rather than continuously |

### Why It Matters

CFDs are the primary diagnostic tool for flow health. They make systemic
problems visible at a glance -- bottlenecks (widening bands), starvation
(narrowing bands), batch behavior (staircases), and throughput trends (slope
of Done). They also provide a visual validation of Little's Law: the three
metrics are literally readable from the geometry of the chart.

### Data/Properties a Flow Engine Must Capture

| Property | Purpose |
|---|---|
| State transition timestamps per item | Build cumulative counts per stage per time unit |
| Ordered list of workflow stages | Define the band stacking order |
| Cumulative arrival count per stage per time bucket | Plot the CFD |
| Cumulative departure count per stage per time bucket | Calculate WIP, cycle time, throughput from the diagram |
| Time bucket granularity (configurable) | Support daily, weekly, or custom time buckets |

---

## 4. Queueing Theory

**Importance: HIGH**

### Explanation

Queueing theory is the mathematical study of waiting lines. It provides
exact or approximate formulas for key performance metrics of systems where
items arrive, wait for service, are served, and depart.

#### Core Concepts

- **Arrival rate (lambda)**: Average rate at which items enter the system.
- **Service rate (mu)**: Average rate at which a server can process items.
- **Utilization (rho)**: rho = lambda / mu. The fraction of time a server is
  busy. Must be < 1 for the system to be stable.
- **Queue length (Lq)**: Average number of items waiting for service.
- **Wait time (Wq)**: Average time an item spends waiting before service begins.
- **System time (W)**: Average total time in system = wait time + service time.

#### Kendall's Notation

Queues are classified as A/S/c where:
- A = arrival process (M = Markovian/Poisson, D = Deterministic, G = General)
- S = service time distribution
- c = number of servers

#### M/M/1 Queue

The simplest non-trivial model: Poisson arrivals, exponential service times,
one server.

```
Lq = rho^2 / (1 - rho)
Wq = rho / (mu * (1 - rho))
W  = 1 / (mu * (1 - rho))
L  = rho / (1 - rho)
```

Key insight: As utilization (rho) approaches 1, queue length and wait time
grow toward infinity. At 90% utilization, the average queue length is 9x
what it is at 50%.

#### M/M/c Queue

Multiple servers (c) sharing a single queue. Reduces wait times compared to
M/M/1 for the same total utilization, because pooling servers smooths out
variability.

#### Kingman's Formula (G/G/1 Approximation)

For the general case where arrival and service distributions are not
necessarily exponential, Kingman (1961) provided an approximation:

```
E[Wq] approximately equals (rho / (1 - rho)) * ((ca^2 + cs^2) / 2) * (1 / mu)
```

Where:
- **rho** = utilization (lambda / mu)
- **ca** = coefficient of variation of inter-arrival times (std dev / mean)
- **cs** = coefficient of variation of service times
- **mu** = service rate

The formula is the product of three factors:
- **U factor**: rho / (1 - rho) -- the utilization effect (hyperbolic blow-up)
- **V factor**: (ca^2 + cs^2) / 2 -- the variability effect
- **T factor**: 1 / mu -- the base service time scale

### Why It Matters

Queueing theory explains the *non-linear* relationship between utilization
and wait times. This is the most counterintuitive insight in flow analysis:
a system at 80% utilization does not have 80% of its capacity consumed --
it has exponentially longer queues than a system at 60% utilization.

This is why knowledge work teams that aim for "100% utilization" (keeping
everyone busy) inevitably suffer terrible cycle times. The math proves that
some slack capacity is essential for flow.

### Data/Properties a Flow Engine Must Capture

| Property | Purpose |
|---|---|
| Inter-arrival times (time between consecutive items entering a stage) | Compute arrival rate and arrival variability (ca) |
| Processing times per stage (time actively worked) | Compute service rate and service variability (cs) |
| Wait times per stage (time in queue before work begins) | Validate against theoretical predictions |
| Number of parallel workers/servers per stage | Support M/M/c modeling |
| Utilization per stage over time | Track utilization trends; detect danger zone (>80%) |
| Stage capacity (theoretical max throughput) | Compare actual vs. theoretical performance |

---

## 5. Flow Metrics (Flow Framework)

**Importance: HIGH**

### Explanation

The Flow Framework, introduced by Mik Kersten in *Project to Product* (2018),
defines a set of metrics for measuring the flow of value through software
delivery value streams. It uses four **flow item types** and five **flow
metrics**.

#### Flow Item Types

| Type | Description |
|---|---|
| **Feature** | New business value |
| **Defect** | Quality fix for existing functionality |
| **Risk** | Security, compliance, or regulatory work |
| **Debt** | Technical debt reduction, architecture improvement |

#### The Five Flow Metrics

**Flow Velocity** -- The number of flow items completed per time period.
Tracks whether the rate of value delivery is accelerating or decelerating.
Disaggregated by item type to reveal *what kind* of work is flowing.

**Flow Time** -- The elapsed time from "work started" to "work complete" for
a flow item. Includes both active work time and wait/queue time. This is
equivalent to cycle time. Measures time-to-market.

**Flow Efficiency** -- The ratio of active work time to total flow time:

```
Flow Efficiency = (Active Time / Total Flow Time) * 100%
```

Typical values in software delivery are shockingly low (often 15-25%),
meaning items spend 75-85% of their time waiting. This metric reveals
the waste hidden in handoffs, queues, and blocked states.

**Flow Load** -- The number of flow items currently in progress in the value
stream. This is WIP. Monitors over- and under-utilization. When flow load
is too high relative to capacity, flow time increases and flow velocity may
decrease (per Little's Law).

**Flow Distribution** -- The proportion of each flow item type (features,
defects, debt, risk) in the current work mix. Reveals whether the
organization is investing appropriately across categories. An organization
that ships only features while ignoring debt and risk is building future
problems.

### Why It Matters

The Flow Framework provides the *business-level* vocabulary for flow analysis.
While queueing theory and Little's Law operate at the mathematical level,
the Flow Framework connects flow metrics to business outcomes: revenue,
cost, customer satisfaction, and employee engagement.

Flow Distribution is particularly powerful because it makes visible the
trade-off between feature work and sustainability work (debt, risk). This
is a strategic decision that flow data should inform.

### Data/Properties a Flow Engine Must Capture

| Property | Purpose |
|---|---|
| Item type classification (feature, defect, debt, risk) | Compute flow distribution; disaggregate all metrics by type |
| Work started timestamp | Compute flow time |
| Work completed timestamp | Compute flow time and flow velocity |
| Active time vs. wait time per item | Compute flow efficiency |
| Current in-progress count by type | Compute flow load |
| Completed count per period by type | Compute flow velocity |
| Value stream boundary definition (start/end states) | Define what "in the system" means |

---

## 6. Bottleneck Analysis

**Importance: CRITICAL**

### Explanation

A bottleneck is any stage in a pipeline where demand exceeds capacity,
causing work items to accumulate and wait. The bottleneck determines the
throughput of the entire system (per Theory of Constraints).

#### Detection Methods

**1. WIP Accumulation (CFD-based)**
The most reliable method. On a cumulative flow diagram, a bottleneck
manifests as a *widening band* -- the arrival rate into the stage exceeds
the departure rate, so WIP grows over time. A stage that consistently
reaches or exceeds its WIP limit is the bottleneck.

**2. Utilization-Based Detection**
Measure the utilization (busy time / available time) of each stage. The
stage with the highest sustained utilization is likely the bottleneck.
Warning: utilization alone can be misleading if a stage is busy with
low-value rework or if workers are multitasking across stages.

**3. Queue Length / Wait Time Analysis**
The stage with the longest average queue or the longest average wait time
before work begins is the bottleneck. This directly measures where items
spend the most time waiting.

**4. Throughput Comparison**
Compare the throughput of each stage. The stage with the lowest
sustainable throughput rate is the bottleneck. All other stages must
subordinate to this rate (per ToC).

**5. Cycle Time Decomposition**
Break total cycle time into time spent in each stage. The stage that
contributes the most to total cycle time (especially wait time) is the
bottleneck.

#### Bottleneck Patterns

- **Persistent bottleneck**: One stage is consistently the constraint.
  The CFD band for that stage widens continuously.

- **Shifting bottleneck**: The constraint moves between stages depending
  on work mix, staffing, or other factors. Harder to address because
  improving one stage may just move the bottleneck.

- **Policy bottleneck**: The constraint is not a capacity issue but a
  policy (e.g., a stage requires approval from a single person who is
  often unavailable). These are often the easiest to fix.

- **Floating bottleneck**: Multiple stages are near capacity, and small
  changes in demand shift the bottleneck. Indicates the system is
  operating near maximum utilization everywhere.

### Why It Matters

Bottleneck analysis is the bridge between measurement and action. Without
it, teams optimize stages that have no impact on system throughput. With
it, teams can direct limited improvement resources to the stage where they
will have maximum impact.

### Data/Properties a Flow Engine Must Capture

| Property | Purpose |
|---|---|
| Per-stage WIP time series | Detect WIP accumulation patterns |
| Per-stage arrival and departure rates | Identify rate mismatches |
| Per-stage queue depth (items waiting for service) | Measure wait queues |
| Per-stage cycle time decomposition (active + wait) | Identify where time is spent |
| Per-stage utilization over time | Detect saturated stages |
| Per-stage WIP limit and current count | Detect limit violations |
| Bottleneck location history | Track whether improvements move the constraint |

---

## 7. Work In Progress (WIP) Limits

**Importance: CRITICAL**

### Explanation

WIP limits are explicit constraints on the maximum number of items allowed
in a stage (or across the system) at any given time. They are the primary
mechanism for controlling flow in pull-based systems like Kanban.

#### Theoretical Basis

WIP limits derive their power from Little's Law:

```
Cycle Time = WIP / Throughput
```

For a given throughput rate, reducing WIP directly reduces cycle time. This
relationship is *linear* in the equation, but the practical effects are
often better than linear because lower WIP also reduces:

- Context switching (each person works on fewer items)
- Coordination overhead (fewer items in flight need status tracking)
- Queue formation (less work competing for the same stage)
- Risk of abandoned work (fewer items means more focus on finishing)

#### Setting WIP Limits

There is no single formula for the optimal WIP limit. Approaches include:

1. **Start with current WIP and reduce gradually**: Measure current average
   WIP, set the limit slightly below it, observe effects, lower further.

2. **Use Little's Law**: WIP = Throughput * Desired Cycle Time. If you want
   a 2-week cycle time and complete 5 items/week, set WIP limit to 10.

3. **Team size heuristic**: WIP limit = number of team members + small
   buffer (often n+1 or n+2) to allow for handoffs and blocked items.

4. **Column-level tuning**: Set limits per stage based on that stage's
   capacity and desired buffer sizes.

#### System-Level vs. Stage-Level WIP Limits

- **System-level WIP limit**: Caps total items in flight across all active
  stages. Controls overall cycle time. Prevents the system from being
  overloaded regardless of where items accumulate.

- **Stage-level WIP limit**: Caps items in a specific stage. Creates a
  pull system: when a stage is full, upstream stages cannot push more work
  into it and must wait or help clear the blockage.

When a stage hits its WIP limit, it creates *back-pressure* that propagates
upstream, naturally throttling the rate at which new work enters the system.
This is the pull mechanism.

### Why It Matters

WIP limits transform a push system (start everything immediately) into a
pull system (start new work only when capacity is available). Without WIP
limits, systems accumulate inventory, cycle times degrade, and the feedback
loop between "starting work" and "experiencing the consequences" is too
slow to enable learning.

WIP limits are also the primary lever for exposing systemic problems. When
a WIP limit is hit, it forces a conversation: "Why is work accumulating
here?" This reveals bottlenecks, quality issues, missing skills, and
process problems that are invisible in a push system where everything is
always "in progress."

### Data/Properties a Flow Engine Must Capture

| Property | Purpose |
|---|---|
| Configured WIP limit per stage | Enforce and track limit compliance |
| System-level WIP limit (if any) | Track total system load |
| Current WIP count per stage | Compare against limits |
| WIP limit violations over time (count and duration) | Measure discipline; correlate with cycle time degradation |
| Back-pressure events (when work was blocked from entering a stage) | Track pull system behavior |
| WIP trend over time per stage | Detect drift away from limits |

---

## 8. Variability and Its Effects

**Importance: HIGH**

### Explanation

Variability is the degree to which arrival times and processing times
deviate from their averages. It is the *hidden enemy* of flow. Two systems
with identical average arrival rates and processing rates can have wildly
different performance if their variability differs.

#### Coefficient of Variation (CV)

The standard measure of relative variability:

```
CV = standard_deviation / mean
```

- CV = 0: No variability (deterministic). All items take exactly the same time.
- CV = 1: Variability equal to the mean (e.g., exponential distribution).
  This is typical of unpredictable knowledge work.
- CV > 1: High variability. Some items take many multiples of the average.
  Common in software development with mixed item sizes.

Two coefficients matter for flow:
- **Ca**: Coefficient of variation of *inter-arrival times* (demand variability)
- **Cs**: Coefficient of variation of *service/processing times* (supply variability)

#### Kingman's Approximation and the Variability Effect

From Kingman's formula, the variability factor is:

```
V = (Ca^2 + Cs^2) / 2
```

This enters the wait time formula *multiplicatively*:

```
E[Wq] = (rho / (1 - rho)) * V * (1 / mu)
```

Reducing V from 1.0 to 0.5 cuts expected wait time in half, independent
of utilization. This means:

- **Reducing variability is often more impactful than adding capacity**.
  Doubling capacity changes rho from 0.8 to 0.4. Halving variability
  cuts wait time by 50% at any utilization level.

- **Both arrival and service variability matter equally**. They are averaged
  in the V factor. You can reduce wait times by smoothing either demand
  (leveling arrival rates) or supply (standardizing work items).

#### Sources of Variability in Software Delivery

| Source | Type | Mitigation |
|---|---|---|
| Varied item sizes (stories vs. epics) | Service variability (Cs) | Break large items into smaller, more uniform pieces |
| Interrupts and expedites | Arrival variability (Ca) | Capacity allocation for interrupts; expedite classes |
| Rework and defects | Service variability (Cs) | Quality practices; shift-left testing |
| Batch arrivals (sprint planning) | Arrival variability (Ca) | Continuous replenishment; smaller batches |
| Skill variability across team members | Service variability (Cs) | Cross-training; pairing |
| External dependencies | Both | Decouple; buffer |
| Context switching | Service variability (Cs) | WIP limits |

### Why It Matters

Variability explains why systems perform worse than their averages would
suggest. A team with an average cycle time of 5 days but CV = 1.5 will
regularly deliver items in 2 days and 15 days -- and the long tail items
are the ones that damage predictability and trust.

Understanding variability is essential for:
- Setting realistic expectations (use percentiles, not averages)
- Choosing the right improvement strategy (reduce variability vs. add capacity)
- Understanding why higher utilization is exponentially more dangerous with
  higher variability

### Data/Properties a Flow Engine Must Capture

| Property | Purpose |
|---|---|
| Individual item cycle times (not just averages) | Compute standard deviation and CV |
| Inter-arrival times between items entering each stage | Compute Ca per stage |
| Processing times per item per stage | Compute Cs per stage |
| Percentile distributions (50th, 85th, 95th) | Report realistic ranges, not just averages |
| Time-series of variability metrics | Track whether variability is increasing or decreasing |
| Item size/type classification | Analyze whether item mix drives variability |

---

## 9. Multi-Stage Pipeline Analysis

**Importance: HIGH**

### Explanation

Real workflows are not single queues -- they are multi-stage pipelines where
work flows through a sequence (or network) of stages, each with its own
capacity, processing time, and variability characteristics.

#### Stage-Level Metrics

Each stage in a pipeline should be analyzed independently:

- **Stage cycle time**: Time from entry to exit for that stage
- **Stage queue time**: Time waiting before active work begins in that stage
- **Stage active time**: Time actually being processed in that stage
- **Stage throughput**: Items completed per time period
- **Stage WIP**: Items currently in that stage
- **Stage utilization**: Fraction of available capacity being used

#### The Pipeline Effect

In a serial pipeline, the overall system behavior is determined by:

1. **The slowest stage sets system throughput** (ToC constraint)
2. **Variability compounds across stages** -- even if each stage has moderate
   variability, the end-to-end variability is larger because delays propagate
3. **WIP accumulates before the bottleneck** -- upstream stages produce
   faster than the bottleneck can consume
4. **Stages downstream of the bottleneck are starved** -- they have excess
   capacity but insufficient input

#### Inter-Stage Dependencies

- **Handoff delays**: Time lost during transitions between stages (review
  queues, deployment queues, approval gates). Often the largest component
  of total cycle time.
- **Batch transfer**: Work moves between stages in batches rather than one
  at a time, introducing delay (items in a batch wait for the last item to
  be ready).
- **Feedback loops**: Rework sends items back to earlier stages, increasing
  effective arrival rates and WIP in those stages.
- **Parallel stages**: Some pipeline stages run in parallel (e.g., testing
  and documentation). Items must wait for all parallel stages to complete.

#### Cycle Time Decomposition

Total cycle time decomposes into:

```
Total CT = sum over all stages of (queue_time_i + active_time_i + handoff_time_i)
```

In most software delivery pipelines, queue time and handoff time account for
70-90% of total cycle time, while active work time is only 10-30%. This is
directly visible in flow efficiency calculations.

### Why It Matters

Analyzing the pipeline as a whole reveals dynamics that per-stage analysis
misses: bottleneck propagation, batch effects, feedback loop amplification,
and the compounding of variability. It also reveals that most improvement
opportunities lie in *the spaces between stages* (queues, handoffs) rather
than in making individual stages faster.

### Data/Properties a Flow Engine Must Capture

| Property | Purpose |
|---|---|
| Full state transition log per item (entered_stage, exited_stage, timestamps) | Reconstruct item journey through pipeline |
| Stage ordering / pipeline topology | Define expected flow path |
| Queue time vs. active time per stage per item | Decompose cycle time; compute flow efficiency per stage |
| Handoff/transition time between stages | Measure inter-stage delays |
| Rework events (item returning to a previous stage) | Detect feedback loops; adjust effective arrival rates |
| Parallel stage relationships | Model fork/join patterns |
| Batch size at each transition | Detect batch transfer effects |
| Per-stage capacity (servers/workers) | Model each stage as a queue |

---

## 10. Starvation and Blocking

**Importance: HIGH**

### Explanation

Starvation and blocking are the two failure modes of flow in a multi-stage
pipeline with WIP limits.

#### Starvation

A stage is **starved** when it has available capacity but no incoming work
items to process. The stage is idle not because of its own problems, but
because the upstream stage is not producing output fast enough.

Causes:
- Upstream bottleneck (upstream stage is the constraint)
- Upstream stage has a quality problem and is producing rework instead of output
- Batch behavior upstream (work arrives in lumps with gaps in between)
- Upstream WIP limit too low (throttling output unnecessarily)

Effects:
- Wasted capacity in the starved stage
- Reduced system throughput (if the starved stage is or could be the constraint)
- Downstream stages are also starved (cascading starvation)

#### Blocking

A stage is **blocked** when it has completed work items ready to move
forward, but cannot release them because the downstream stage is full
(at its WIP limit). The blocked stage must stop processing new items even
though it has capacity.

Causes:
- Downstream bottleneck (downstream stage is the constraint)
- Downstream WIP limit too tight
- Downstream stage has a quality issue causing slow processing
- A "gateway" or approval stage that processes work in batches

Effects:
- The blocked stage's effective throughput drops (even though it could process more)
- WIP accumulates in the blocked stage
- Back-pressure propagates further upstream (cascading blocking)
- Items age in the blocked state, increasing cycle time

#### The Relationship Between Starvation and Blocking

In a pipeline with WIP limits, blocking upstream is the *mechanism* that
causes starvation downstream of the constraint. They are two sides of the
same coin. A well-functioning pull system with appropriate WIP limits will
experience *small amounts* of both starvation and blocking as normal
fluctuation. Large or persistent starvation/blocking indicates a systemic
imbalance.

#### Buffer Management (from ToC)

The Theory of Constraints addresses starvation and blocking through buffers:
- Place a time/WIP buffer *before* the constraint to prevent starvation
- Keep the space *after* the constraint empty to prevent blocking
- Monitor buffer penetration (how full/empty buffers are) as an early
  warning system

### Why It Matters

Starvation and blocking directly reduce system throughput. More importantly,
they are *symptoms* that point to root causes: bottlenecks, capacity
imbalances, batch effects, and policy constraints. A flow engine that can
detect and report starvation/blocking patterns gives teams actionable
signals about where to intervene.

### Data/Properties a Flow Engine Must Capture

| Property | Purpose |
|---|---|
| Stage idle time (time with available capacity but no input items) | Detect starvation |
| Stage blocked time (time with completed items unable to move forward) | Detect blocking |
| WIP limit per stage and current count | Determine if blocking is WIP-limit-driven |
| Queue depth before each stage over time | Detect starvation (empty queues) |
| Output rate of upstream stages | Determine if starvation is caused by upstream slow throughput |
| Downstream capacity availability | Determine if blocking is caused by downstream saturation |
| Buffer penetration levels | Early warning for starvation/blocking |
| Starvation and blocking event log (stage, start_time, end_time, duration) | Historical pattern analysis |

---

## 11. Monte Carlo Simulation for Flow Forecasting

**Importance: HIGH**

### Explanation

Monte Carlo simulation is a probabilistic forecasting technique that uses
historical throughput data (and optionally cycle time data) to generate a
distribution of possible future outcomes through repeated random sampling.

#### How It Works

1. **Collect historical throughput data**: Record the number of items
   completed per day (or per week) over a representative historical period.

2. **Define the forecasting question**: Either:
   - "How many items can we complete in N days?" (quantity forecast)
   - "When will we complete N items?" (date forecast)

3. **Run simulations** (typically 10,000+ iterations):
   - For each simulated "day", randomly sample a throughput value from the
     historical dataset (sampling with replacement).
   - Accumulate the simulated throughput until the target is reached.
   - Record the result (total items completed, or number of days elapsed).

4. **Build the probability distribution**: The 10,000+ results form a
   distribution. Report percentiles:
   - 50th percentile: "There's a 50% chance we'll finish by this date"
   - 85th percentile: "There's an 85% chance we'll finish by this date"
   - 95th percentile: "Very high confidence date"

#### Advantages Over Deterministic Forecasting

- **Embraces uncertainty**: Rather than producing a single "estimate" that
  is almost certainly wrong, produces a range of outcomes with probabilities.
- **Uses real data**: Based on what the team actually achieved, not what
  they estimated or planned.
- **No estimation required**: Does not require story points, t-shirt sizes,
  or any per-item estimation. Only throughput history.
- **Naturally incorporates variability**: Days with zero throughput, high
  throughput, and average throughput are all represented proportionally.

#### Enhancements

- **Cycle time Monte Carlo**: Instead of sampling throughput, sample cycle
  times per item to forecast individual item completion dates.
- **Split by item type**: Sample different distributions for features vs.
  bugs to account for different flow characteristics.
- **Rolling window**: Use only recent history (e.g., last 8 weeks) to
  account for team changes or process improvements.
- **Residual item count**: Account for items already in progress by starting
  the simulation with items at their current age.

### Why It Matters

Monte Carlo simulation transforms flow data into actionable forecasts that
respect uncertainty. Traditional estimation (story points, planning poker)
produces false precision. Monte Carlo produces honest probability ranges
that enable better stakeholder communication and risk management.

It is the primary method recommended by the Kanban community (Daniel
Vacanti, Troy Magennis, and others) for probabilistic forecasting.

### Data/Properties a Flow Engine Must Capture

| Property | Purpose |
|---|---|
| Daily (or weekly) throughput counts over a historical window | Input data for throughput-based simulation |
| Individual item cycle times over a historical window | Input data for cycle-time-based simulation |
| Item type classification | Enable type-specific simulations |
| Current in-progress items and their ages | Initialize simulation with residual items |
| Configurable historical window (e.g., last N weeks) | Control which data feeds the simulation |
| Completed item count per day, partitioned by various dimensions | Support filtered/segmented simulations |

---

## 12. Aging WIP

**Importance: HIGH**

### Explanation

Aging WIP analysis tracks how long each currently in-progress item has been
in its current state (or in the system overall). It is a *leading indicator*
-- unlike cycle time (which is measured after completion), WIP age gives
early warning about items that are likely to miss delivery expectations.

#### WIP Age vs. Cycle Time

- **Cycle time** is measured *after* an item is completed. It is a *lagging*
  indicator. You know the cycle time only once the item is done.
- **WIP age** is the *current age* of an in-progress item. It is a *leading*
  indicator. An item whose WIP age has already exceeded the 85th percentile
  of historical cycle times is very likely to have a long total cycle time.

#### Percentile-Based Aging Thresholds

Using historical cycle time data, set thresholds based on percentile
completions:

| Threshold | Meaning | Typical Action |
|---|---|---|
| Below 50th percentile | Item is progressing normally. Half of all historical items completed faster than this. | No action needed |
| 50th-75th percentile | Item is taking longer than average. Monitor. | Check for blockers |
| 75th-85th percentile | Item is in the slow zone. Most historical items completed faster. | Active intervention; discuss in standup |
| Above 85th percentile | Item is aging dangerously. Only 15% of historical items took this long. | Expedite or escalate; consider splitting |
| Above 95th percentile | Item is in critical territory. This is an outlier. | Root cause analysis; likely blocked or stuck |

#### The Aging WIP Chart

A scatter plot or dot plot where:
- X-axis: Workflow stages (columns on the board)
- Y-axis: Age of each item (days in progress)
- Each dot represents a current in-progress item
- Horizontal lines mark percentile thresholds (50th, 85th, 95th)

Items appearing above the 85th percentile line in early stages are
particularly concerning -- they have already spent more time than 85% of
completed items, and they still have stages remaining.

### Why It Matters

Aging WIP is the most actionable day-to-day metric for flow management.
While CFDs and throughput charts show systemic trends, the aging WIP chart
shows *specific items that need attention right now*. It enables proactive
management: intervene before items become late rather than measuring how
late they were after the fact.

It also directly supports Service Level Expectations (SLEs): "85% of items
should complete within 14 days." Any item whose age exceeds 14 days is
at risk of violating the SLE.

### Data/Properties a Flow Engine Must Capture

| Property | Purpose |
|---|---|
| Item start timestamp (when work began) | Calculate current age |
| Current item state/stage | Position on aging chart |
| Historical cycle time distribution | Compute percentile thresholds |
| Per-stage historical cycle times | Compute stage-specific thresholds |
| Percentile thresholds (configurable: 50th, 75th, 85th, 95th) | Define warning levels |
| Item metadata (type, priority, assignee) | Enable filtered aging views |
| Blocked/waiting status per item | Distinguish aging from blocked vs. aging from slow progress |
| SLE definition (percentile + target days) | Evaluate SLE compliance for in-progress items |

---

## Summary: Engine Data Requirements

The following table consolidates the core data structures a flow analysis
engine needs to support all 12 theoretical foundations:

### Item-Level Data

| Field | Type | Required By |
|---|---|---|
| item_id | identifier | All |
| item_type | enum (feature, defect, debt, risk) | Flow Metrics, Monte Carlo, Bottleneck |
| created_at | timestamp | Arrival analysis |
| started_at | timestamp | Little's Law, Aging WIP, Flow Time |
| completed_at | timestamp | Little's Law, Cycle Time, Monte Carlo |
| current_state | enum (workflow stage) | Aging WIP, WIP counts |
| priority / class_of_service | enum | Aging WIP, WIP analysis |

### State Transition Events

| Field | Type | Required By |
|---|---|---|
| item_id | identifier | All |
| from_state | enum | Multi-stage, CFD, Rework detection |
| to_state | enum | Multi-stage, CFD, Rework detection |
| timestamp | timestamp | All |
| active_time_in_state | duration | Flow Efficiency, Variability |
| wait_time_in_state | duration | Flow Efficiency, Queueing Theory |

### Stage Configuration

| Field | Type | Required By |
|---|---|---|
| stage_id | identifier | All |
| stage_name | string | CFD, visualization |
| stage_order | integer | CFD, Pipeline analysis |
| wip_limit | integer (nullable) | WIP Limits, Blocking |
| stage_type | enum (queue, active, done, buffer) | Flow Efficiency, Starvation/Blocking |
| parallel_capacity | integer | Queueing Theory (M/M/c) |

### Time-Series / Aggregate Data

| Metric | Granularity | Required By |
|---|---|---|
| WIP count per stage | daily | CFD, Little's Law, Bottleneck |
| Throughput count | daily | Little's Law, Monte Carlo, Flow Velocity |
| Cumulative arrivals per stage | daily | CFD |
| Cumulative departures per stage | daily | CFD |
| Stage utilization | daily | Queueing Theory, Bottleneck |
| Stage queue depth | daily | Starvation/Blocking |

### Derived Metrics (computed from above)

| Metric | Formula / Method | Required By |
|---|---|---|
| Cycle time per item | completed_at - started_at | Little's Law, Aging WIP, Monte Carlo |
| Average cycle time | mean of cycle times in window | Little's Law |
| Cycle time percentiles | P50, P85, P95 of cycle times | Aging WIP, SLE |
| Throughput rate | items completed / time period | Little's Law, Monte Carlo |
| Flow efficiency | active_time / (active_time + wait_time) | Flow Metrics |
| Coefficient of variation (Ca, Cs) | std_dev / mean for inter-arrival and service times | Variability, Kingman |
| Stage utilization | active_time / available_time | Queueing Theory, Bottleneck |
| WIP age per item | now - started_at | Aging WIP |

---

## Importance Summary

| # | Concept | Importance | Rationale |
|---|---|---|---|
| 1 | Little's Law | **Critical** | Foundational equation; connects WIP, throughput, cycle time |
| 2 | Theory of Constraints | **Critical** | Determines where to focus improvement; bottleneck thinking |
| 3 | Cumulative Flow Diagrams | **Critical** | Primary diagnostic visualization; shows all three metrics |
| 4 | Queueing Theory | **High** | Explains non-linear utilization effects; Kingman's formula |
| 5 | Flow Metrics (Flow Framework) | **High** | Business-level vocabulary; connects flow to outcomes |
| 6 | Bottleneck Analysis | **Critical** | Actionable identification of constraints from data |
| 7 | WIP Limits | **Critical** | Primary control mechanism for flow; enables pull systems |
| 8 | Variability | **High** | Hidden driver of poor performance; explains unpredictability |
| 9 | Multi-Stage Pipeline | **High** | Real-world systems are multi-stage; composition effects matter |
| 10 | Starvation/Blocking | **High** | Failure modes of flow; diagnostic signals |
| 11 | Monte Carlo Simulation | **High** | Probabilistic forecasting; replaces estimation |
| 12 | Aging WIP | **High** | Leading indicator; most actionable day-to-day metric |

---

## Sources and References

### Books
- Goldratt, E. M. (1984). *The Goal*
- Kersten, M. (2018). *Project to Product*
- Little, J. D. C. (1961). "A Proof for the Queuing Formula: L = lambda W"
- Kingman, J. F. C. (1961). "The Single Server Queue in Heavy Traffic"
- Vacanti, D. S. (2015). *Actionable Agile Metrics for Predictability*
- Anderson, D. J. (2010). *Kanban: Successful Evolutionary Change for Your Technology Business*

### Online Resources
- [Little's Law - Project Production Institute](https://projectproduction.org/journal/littles-law-a-practical-approach-to-understanding-production-system-performance/)
- [Theory of Constraints - TOC Institute](https://www.tocinstitute.org/theory-of-constraints.html)
- [Cumulative Flow Diagram - Businessmap](https://businessmap.io/kanban-resources/kanban-analytics/cumulative-flow-diagram)
- [Kingman Formula - AllAboutLean](https://www.allaboutlean.com/kingman-formula/)
- [Flow Framework - flowframework.org](https://flowframework.org/ffc-discover/)
- [Monte Carlo Simulation - Nave](https://getnave.com/blog/monte-carlo-simulation/)
- [Aging WIP - Kanban Zone](https://kanbanzone.com/2019/aging-work-in-progress/)
- [WIP Limits - Businessmap](https://businessmap.io/kanban-resources/getting-started/what-is-wip)
- [Queueing Theory and Kanban - Kanban Tool](https://kanbantool.com/kanban-guide/queuing-theory)
- [Bottleneck Analysis - Businessmap](https://businessmap.io/lean-management/pull/what-is-bottleneck)
