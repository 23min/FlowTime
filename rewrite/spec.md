# FlowTime Seeded Rewrite

**Status:** Decided direction, being specified. Nothing described here is built, and the new repository does not exist yet.
**Date:** 2026-10-17, revised 2026-10-18

FlowTime restarts in a new repository, seeded with the parts of today's Rust engine that work and checked against a new oracle built from the concept spec and from queueing theory. The C# engine, both .NET services, the Blazor UI and the existing planning prose stay behind in this repository, which is archived read-only.

## Decisions

Decisions 1 to 14 were made on 2026-10-17, decisions 15 and 16 on 2026-10-18.

| # | Question | Decision |
| --- | --- | --- |
| 1 | Restart or replace in place | Seeded restart |
| 2 | Where | A new repository, created with devcontainer-kit once the next aiwf release is out and verifier-kit is stable enough |
| 3 | Shape checks | CUE schemas registered as aiwf contracts, from the start |
| 4 | Gate and guard | verifier-kit's `./verify` and guard, from the start |
| 5 | The oracle's files | Data files that one harness reads |
| 6 | How a queue is written | One backlog row in the model; a queue resource and a service with a buffer are distinct component kinds, and the UI draws them differently |
| 7 | How flow splits | A router node with named outputs |
| 8 | Parameters | Each parameter declares its class, value or structural, and a check confirms its uses match |
| 9 | The UI | Decided once the HTTP and session contracts exist (direction H); until then one page draws the files the CLI writes |
| 10 | The oracle | Closed forms per concept as the main oracle, laws every model obeys, and one reference evaluator for combinations; the C# engine is a second opinion, never a gate |
| 11 | Validation | A small discrete-event simulation suite, run on demand, never a gate |
| 12 | The oracle's language | Python, importing nothing from the engine |
| 13 | Randomness below capacity | Variability is part of the model language; per-bin waiting estimates from standard formulas first, built so that tracking each queue's spread can follow |
| 14 | What describes a service | Its servers and its service time per request type; its capacity follows from them, and a request's time at the service is its service time plus its wait |
| 15 | How ambitious the core is | Shaped from the start for components that carry state and mechanisms; built one concept at a time, the service first, and each further mechanism added when an incident needs it |
| 16 | What comes first | A slice that ingests telemetry from openshift-lab and from a simulation of it, replays it with a derived model and shows it on one page; templates and the full UI follow later (Order of work) |

## Why a seeded restart

- **The core is small and works.** The Rust engine's parse, compile, evaluate and analyse code is about 3,200 lines without tests, its 218 tests pass, and its numbers match the C# engine wherever both run a model.
- **The weight is everywhere else.** About 79,000 lines of C#, 62,000 lines of C# tests and 813,000 words of docs and plans, much of it describing a system the code no longer matches. Replacing in place means keeping those paths working, or deleting them, at every step.
- **Nothing forces compatibility.** FlowTime has no users (consolidation proposal, P8).
- **A new repository does not fix the process that grew this one.** Scope ran ahead of code, and the Rust engine's ten milestones (E-0020) merged in two days with a parity test that skipped whatever it could not compare. The new repository therefore has an oracle the builder cannot edit, a gate from the first commit, and plans kept only as aiwf entities.

Starting from zero was rejected because it throws away working, tested code and leaves the new spec as its only check. Replacing in place was rejected because two engines, three queue lowerings, two services and the old docs would remain until the last retirement epic, and the old structure would keep shaping the new design.

## Architecture

One Cargo workspace. A pure core holds the essence; three libraries sit above it; one binary serves every surface from the same calls. Dependencies point only downward. The oracle is Python, lives beside the code, and is never shipped.

![The Svelte UI talks HTTP and WebSocket to one Rust binary, flowtime, which serves the CLI, the HTTP API and the WebSocket session. The binary calls three libraries (templates, analysis and runs), and all of them use the pure core: model types, validate, compile, evaluate per bin, metrics and invariants. A dashed amber box below marks the Python oracle, which is never shipped and checks the engine through its CLI.](spec/1-architecture.svg)

| Crate | Owns | Never does | Seeded from |
| --- | --- | --- | --- |
| `core` | Model types; validation (schema, references, one routing authority per split); compile to a plan with a parameter table; bin-major evaluation; derived metrics; invariant warnings; diagnostics with source positions | File or network access; template syntax; anything the UI shows but the engine did not compute | `engine/core`: `eval.rs`, `plan.rs`, `expr.rs` copied as the formula layer, with the evaluator's loop extended for component state and steps shorter than a bin; `model.rs` and `compiler.rs` rewritten to the model spec |
| `templates` | Template parsing, typed parameters, the authoring sugar and its lowering | Text substitution on YAML; evaluation | New, in direction F; the 12 templates are its acceptance corpus |
| `analysis` | Flow accounting, deriving a model from a run bundle, replay residuals; later sweep, sensitivity, goal-seek and Nelder–Mead optimize, each a loop over evaluate-with-overrides | Recompiling per point; reading files | New for telemetry; `FlowTime.TimeMachine/Sweep` (about 1,600 lines of C#) for the sweeps |
| `runs` | The run folder: model, one CSV per series, `run.json`, manifest; reading it back; reading run bundles | Computing values | `engine/core/src/sink.rs` |
| `flowtime` (binary) | CLI commands, HTTP endpoints, WebSocket session, all calling the same library functions | Business logic in handlers | `engine/cli` (session protocol) |

## One run, end to end

The template is lowered once; the model is compiled once; analysis and the UI session re-evaluate the same plan with different constants.

![A template and its parameter values are lowered to a model in which every series is declared once, then validated, compiled to a plan with a parameter table, and evaluated bin by bin. The result goes to a stored run folder, to the UI's live session, or to an analysis loop that re-evaluates the same plan without recompiling. A dashed amber arrow carries diagnostics from validation back to the template line and parameter.](spec/2-one-run.svg)

## The model language

### Rules

1. **Every series is declared once, by id: as a row, or as a router's named output.** No name is invented by convention. Today's Rust engine creates names such as `processing_queue` and `processing_utilization` from a topology node's id, so renaming the node silently renames them.
2. **Anything that computes is a node:** `const`, `expr`, `pmf`, the backlog row, and `router`. A split is one router node that lists all its branches; consumers read a branch by name, such as `HubRouter.airport`. In templates, edge weights and class routes become such a router. Its shares sit in one declaration, so "positive and complete" is a local check, and the capacity-aware allocator deferred in G-0038 later takes the same shape.
3. **The topology computes nothing.** It names the components, says what kind each one is, labels rows with roles (arrivals, served, servers, service time, capacity, buffer) and carries layout. Derived metrics are outputs addressed by the component, such as `Processing.utilization`.
4. **Sugar exists only in templates,** and is lowered in one place: `queueDepth: self`, edge weights, class shorthands.
5. **Parameters are typed, and each one's class is declared.** A value parameter becomes a constant row that formulas name directly and that analysis and the UI override without recompiling. A structural parameter (a bin count, a dispatch period, a telemetry file) is resolved when the template is lowered, on the parsed template, never by replacing text. A check rejects a value parameter wherever a literal is required, such as a grid field or a `PULSE` period. Proposed syntax: `structural: true`, with value as the default.
6. **Variability is an input.** Each arrival stream and each service carries how uneven it is (its coefficient of variation): taken from a PMF, measured from telemetry, or assumed. The core reports a per-bin estimate of the random queue and wait beside the fluid queue.
7. **A service is described by its servers and its service time,** per request type. Its capacity follows: servers × bin length ÷ service time. A request's time at the service is its service time plus its wait, so two services with the same capacity can differ widely in latency. At the same capacity of 100 requests a second and a load of 90, one server taking 10 ms gives 100 ms per request, and 100 servers taking 1 s each give about 1,022 ms; a model that knows only capacity reports the same for both.

### Variability

Today both engines replace a PMF with its average before evaluating: C# fills every bin with the expected value (`PmfNode`), Rust compiles a PMF to a constant, and generated template models store a PMF as an average shaped by a daily profile. A C# path that samples a PMF per bin (`PmfCompiler`) has no caller in the application. The only use of a PMF's spread is Rust's Kingman metric, which treats every other series as having none. FlowTime computes no request-time percentiles: the Sim service's `/api/v1/series/summarize` reports percentiles of a series' per-bin values, and telemetry series can be tagged as percentiles so the UI shows them as such.

Without variability, the flow model reads zero queue and zero wait at every load below capacity (see Validation against simulation). The first estimate per bin uses standard formulas for the typical line and wait at a given load and variability, including services with several parallel workers. Carrying each queue's spread alongside its average, which also handles recovery after a rush and yields bands and approximate percentiles, can follow. The simulation suite judges both against random runs.

### Two kinds of queue

The arithmetic is one recurrence, Q[t] = max(0, Q[t−1] + inflow[t] − outflow[t] − loss[t]). What differs is who owns the backlog, what drains it, and which metrics apply.

| | Queue resource | Service with a buffer |
| --- | --- | --- |
| Examples | Azure Service Bus queue, Amazon SQS, RabbitMQ | SQL Server, an HTTP server's request queue, a thread pool |
| In the topology | Its own component, `kind: queue` | A `service` that owns a buffer row |
| What drains it | Its consumers, pulling: a consumer's served reads the queue's depth in the previous bin | The service's own capacity, which follows from its servers and service time: served = MIN(capacity, arrivals + previous depth) |
| Its own losses | Expiry (TTL), dead-lettering after too many deliveries, producers refused when full | Requests rejected when the buffer is full, client timeouts |
| Metrics | Depth and wait time; no utilization of its own | Depth, wait time and utilization |
| Template shorthand (proposed) | A queue component with `buffer: self`; its consumers name it | A service with `buffer: self`, servers and a service time, lowered to the capacity row, the buffer row and the standard served formula |

The UI draws the two differently, so the difference reaches it as a fact: the engine's graph output, in the session and in the run folder, carries each component's kind and the buffer row it owns, and that graph message has its own CUE contract. The UI never infers a kind from names. A queue with several competing consumers needs a capacity-aware share between them (ADR-0001's class 2, deferred in G-0038); until that exists it is modeled with one consumer or a fixed split.

### Template and model, side by side

Template, as an author writes it:

```yaml
parameters:
  - name: servers         # value (the default): a const row
    type: integer
    default: 4
  - name: serviceTime     # seconds per request
    type: number
    default: 12
  - name: bins
    type: integer
    structural: true      # resolved when lowering
    default: 12
grid: { bins: ${bins}, binSize: 10, binUnit: minutes }
nodes:
  - id: demand
    kind: const
    values: [100, 100, 100, 300, 300, 300, 100, 100, 100, 100, 100, 100]
  - id: capacity
    kind: expr
    expr: "servers * 600 / serviceTime"   # 600 s in a 10-minute bin
  - id: served
    kind: expr
    expr: "MIN(capacity, demand + SHIFT(processing_queue, 1))"
topology:
  nodes:
    - id: Processing
      kind: service
      semantics:
        arrivals: demand
        served: served
        servers: servers
        serviceTime: serviceTime
        capacity: capacity
        queueDepth: self    # sugar, lowered once
```

Model, as the engine accepts it after lowering:

```yaml
schemaVersion: 2
grid: { bins: 12, binSize: 10, binUnit: minutes }
nodes:
  - id: servers             # from the parameter
    kind: const
    values: [4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4]
  - id: serviceTime         # from the parameter
    kind: const
    values: [12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12]
  - id: capacity            # 200 per bin
    kind: expr
    expr: "servers * 600 / serviceTime"
  - id: demand
    kind: const
    values: [100, 100, 100, 300, 300, 300, 100, 100, 100, 100, 100, 100]
  - id: served
    kind: expr
    expr: "MIN(capacity, demand + SHIFT(processing_queue, 1))"
  - id: processing_queue    # declared, not implied; kind name still open
    kind: queue
    inflow: demand
    outflow: served
topology:                   # labels only
  nodes:
    - id: Processing
      kind: service
      semantics:
        arrivals: demand
        served: served
        servers: servers
        serviceTime: serviceTime
        capacity: capacity
        queueDepth: processing_queue
```

The syntax stays close to today's YAML; only the rules are new. Whether templates may refer to the name `self` lowers to, or must name the queue explicitly, is open.

## Specs and their checks

A spec without a check is the kind of document this repository already has too many of, so each spec is short and paired with a test that fails when code and spec disagree.

| Spec | Defines | Checked by | Starting material in this repository |
| --- | --- | --- | --- |
| Concepts | Per-bin equations, one section per concept, edge cases decided: queue, service (servers and service time), WIP and overflow, dispatch, routing and splits, constraints, retry echo, PMF, `SHIFT` and feedback, classes, derived metrics, invariants | Closed-form properties over generated models; laws; worked examples as readable instances | ADR-0001; `docs/architecture/nan-policy.md`, `retry-modeling.md`, `flow-authority-policy.md` |
| Model language | Node kinds and fields, reference rules, one routing authority per split | A CUE schema registered as an aiwf contract, with valid and invalid fixtures; the Rust parser reads the same fixtures | The 13 cross-reference rules listed in `docs/schemas/model.schema.yaml`; E-0023's 94-rule audit, deleted in the May migration and recoverable from git (`d0127eee`) |
| Template language | Typed parameters, structural or value; the sugar and exactly what it lowers to | A CUE contract for the template's shape; snapshot tests for the lowering, template in, model out | The 12 templates; `QueueNodeSynthesizer` and `ModelCompiler` as reference behavior |
| Run folder | Layout, series id format, `run.json`, manifest | A CUE contract per file; a write-then-read round trip; the writer's output checked against the contract | `engine/core/src/sink.rs` |
| Run bundle | What one load-test run's telemetry holds: the run record, the configuration in force, spans, metric series for the run's window | A CUE contract with valid and invalid fixtures; openshift-lab's export and the simulation's output both checked against it | openshift-lab's k6 run record (`loadtest/post.js`), Tempo's trace API, Prometheus range queries |
| Session and HTTP | Messages and endpoints, including the graph output with component kinds | A CUE contract for the messages; a verifier-kit spine for the session's states; Playwright tests in the UI | `engine/cli/src/protocol.rs`, `session.rs` |

## Verification

One gate, three kinds of check. The builder's Stop hook, CI and a person all run `./verify`, which runs `cargo test`, clippy, format, `aiwf contract verify` and the Python oracle's property tests. aiwf's own pre-push hook also runs the contract checks.

![The builder agent's Stop hook, CI on every pull request, and a person before review all call one ./verify script. It runs three kinds of check. Shape: CUE schemas registered as aiwf contracts, with valid and invalid fixtures that the Rust parser also reads; it misses wrong numbers. Meaning: closed forms, laws and a reference evaluator; it misses what no form, law or generated model reaches. Behavior: verifier-kit spines for the session and the run lifecycle; it misses the values inside a step. Below, the guard lets the builder read oracle paths but never change them, and ./verify --full plants bugs that must be caught.](spec/3-verification.svg)

CUE is the source of truth for shape, and a Rust test feeds the same `valid/` and `invalid/` fixtures to the Rust parser; if they disagree, the build fails. Without that test the repository would again have two validators drifting apart, which E-0023 spent three milestones removing.

verifier-kit's spine covers lifecycle and permissions only, so it suits the session and run lifecycle, not the arithmetic. For the arithmetic its design suggests a property-testing library and lists decision tables as designed but not built; FlowTime's numeric oracle could be that view's first real user.

### Where the expected numbers come from

Closed forms per concept are the main oracle; one reference evaluator covers combinations and is trusted only after it passes every closed-form property. Nobody computes grids by hand. The whole oracle side is Python, imports nothing from the engine, and reaches it only through its CLI.

| Source | Covers | Independent because |
| --- | --- | --- |
| Closed forms from queueing theory | Each concept in simple shapes: a queue's growth, peak and clear time; queues in series; splits; WIP limits and overflow; constraints; dispatch; retries; Little's law over a period that starts and ends empty; the Kingman metric against exact M/M/1 | Derived by algebra over the whole run, not bin by bin |
| Laws | Any model: conservation, non-negativity, more capacity never lengthens a queue, renaming changes nothing | They hold for any correct engine |
| Reference evaluator | Combinations with no closed form, such as retries feeding a router in a feedback loop | Written from the spec alone, naive and grid-shaped, small enough to read in one sitting, trusted only after passing every closed-form property |
| C# second opinion | Whatever C# can run | Written by other hands, earlier; see below for when to distrust it |

Models come from a property-based generator: seeded so any failure replays, shrinking a failing model to the smallest one that still fails, and owned by the oracle side. Every model it produces must pass the CUE model contract. Worked examples are readable instances of the closed forms: the spike example's peak of 300 is 3 bins × 100 excess.

### The oracle's files

```
oracle/
  examples/                # one folder per concept
    queue/
      spike-drains/
        model.yaml         # in the new model language
        expected.csv       # a row per bin, a column per series
        warnings.yaml      # expected warnings
        derivation.md      # the closed form behind the numbers
        example.yaml       # what it shows, who ratified it
  properties/              # closed forms and laws, with generators
  reference/               # the reference evaluator, once ratified
```

`example.yaml`, whose shape is itself a CUE contract:

```yaml
id: queue/spike-drains
concept: queue
shows: >
  Demand of 300 against capacity 200 builds the queue by 100 per bin;
  it drains by 100 per bin once demand falls back to 100.
derivedFrom: >
  peak = spike bins x (spike demand - capacity);
  clears after peak / (capacity - base demand) bins
checkedBy: reference evaluator, every bin
secondOpinion: C# engine agrees in every bin
ratifiedBy: human/peter
tolerance: { relative: 1.0e-9, absolute: 1.0e-9 }
```

The harness follows five rules:

1. Every series in `expected.csv` must exist in the output. A missing series fails; nothing is skipped silently.
2. Values are compared bin by bin within the tolerance. A failure names the example, the series, the first differing bin and both values.
3. Warnings are compared exactly, as a set of node and code.
4. Extra output series are ignored: an example pins only what someone derived.
5. An example is committed only when its closed form, the reference evaluator and your ratification agree. A disagreement, including one with the C# second opinion, is settled in the concept spec first.

The first rule is aimed at a known failure: today's parity test (`RustEngineParityTests.cs`) skips every Rust series that has no C# series of the same name, so it never compared a queue Rust created.

### Validation against simulation

The simulation suite checks how good the per-bin flow approximation is, not the code. Its standard systems are one queue, two queues in series, a router, and retries with timeouts. It runs on demand, never as a gate, and is built once the queue, routing and retry concepts are specified. Its findings become stated accuracy limits in the concept spec, and its output doubles as synthetic telemetry with known true parameters for the telemetry work.

#### The theory: the fluid limit

FlowTime's per-bin recurrence is what queueing theory calls a fluid model. Work is treated as a continuous flow, so a queue grows exactly as fast as arrivals exceed capacity and drains exactly as fast as capacity exceeds arrivals. A real system is random: arrivals come in bursts and service times vary, so its queue follows a random path.

The fluid limit connects the two. Multiply arrivals and capacity by a factor n and divide the random queue by n; as n grows, the result converges to the fluid path, the way an average converges in the law of large numbers. The random wobble around that path grows only like √n, by the central limit theorem, so relative to the flow it shrinks like 1/√n. In one line: queue ≈ n × fluid path + √n × noise.

![Three panels show the spike model at 1, 10 and 100 times the volume. FlowTime's queue rises linearly to its peak at 60 minutes and falls back to zero at 90. The simulation's average follows it, and the band holding the middle 90% of runs narrows from 79–122% of the peak at 1× to 98–102% at 100×. At 90 minutes, when FlowTime's queue is zero, the simulation still holds 7.0%, 2.1% and 0.63% of the peak.](spec/4-fluid-limit.svg)

The spike model shows it. The middle 90% of simulated runs spans 79–122% of FlowTime's peak at 1× volume, 94–107% at 10× and 98–102% at 100×, narrowing by about √10 at each step.

**Where the gap concentrates.** A queue can't go below zero, so wherever the fluid path sits at zero, randomness only adds: the average of max(0, X) is larger than max(0, the average of X). At the moment a backlog clears, the queue's accumulated randomness is close to normal, with variance σ² = Σ (arrival rate + service rate) × duration over the busy period, and the average of its positive part is σ/√(2π), about 0.4σ. For the spike, σ² = (30 + 20) × 30 + (10 + 20) × 30 = 2,400 at 1×, so theory predicts 19.5 items still waiting where FlowTime says zero; the simulation shows 21.0. At 10× the prediction is 61.8 and the simulation shows 61.6; at 100×, 195 and 188, within one standard error over 1,000 runs. This closed form can be one of the simulation suite's own checks.

**At steady load.** Below capacity the fluid queue is zero at any load, but a random queue holds ρ/(1 − ρ) items on average, where ρ is arrivals divided by capacity (one server, random arrivals and service times: the M/M/1 queue). The simulation lands on that curve.

![Average number of items in a one-server system against load. FlowTime's queue is zero at every load below capacity. Queueing theory's curve, rho over one minus rho, rises slowly and then steeply near capacity, and the simulation's points at loads 0.5, 0.7, 0.8, 0.9 and 0.95 sit on it; a table beside the chart lists the simulated and theoretical values.](spec/5-steady-load.svg)

Counted in items, that queue does not grow with volume when one server simply gets faster, so at high volume it is small next to the flow. Counted in time it does not shrink: an item spends 1/(1 − ρ) service times in the system at any volume, 10 at a load of 0.9 and 20 at 0.95, and that is what latency targets feel. It grows without bound as load approaches capacity: 9 items at 0.9, 19 at 0.95, 99 at 0.99. That is where the flow model is least reliable. FlowTime's Kingman metric is its tool for that wait, and it needs the variability of arrivals and service as input: today it takes it from PMF nodes and treats constant series as having none. When volume comes from more parallel servers rather than a faster one, a different regime applies (the many-server, or Halfin–Whitt, regime); that is a question for the simulation suite and is not measured here. Tails such as p99 stay outside what a flow model predicts at any volume.

**Two services in series, near capacity.** A more realistic case shows the size of the gap. Service A feeds service B, which is 10% faster. Demand rises from 75% of A's capacity to 90%, then 95%, exceeds it at 105% for 30 minutes, and recedes. FlowTime (the Rust engine on the same model) reports no queue at A except during the overload, and never a queue at B. The simulation holds 9 items at A at 90% load and 16 at 95%; during the overload it holds 54 to FlowTime's 30; when FlowTime says A has cleared, it still holds 35; and B, which FlowTime never shows queueing, holds 2 to 9 items throughout. This is not a defect in the engine, which computes the fluid model exactly. It is the fluid model's blind spot, and it sits where most production systems run, between about 70% and 95% of capacity. FlowTime therefore carries variability as an input and estimates waiting per bin (decision 13, and Variability under The model language); the simulation suite's main job is to judge those estimates.

### The C# engine as a second opinion

The C# engine stays runnable in the archived repository and is consulted while examples are derived. A disagreement with it is a question for the concept spec, settled there and never in C#'s favor by default. Nothing is captured from it, and nothing in the new repository depends on it. Its arithmetic holds up where it can be checked independently, but it records current behavior, not correct behavior, and these problems are known:

| Problem | Where | Evidence | For a second opinion |
| --- | --- | --- | --- |
| A convolution estimate of backlog is added to a queue's inflow, so backlog is counted twice; each queue peaks 2.8 times higher than the same recurrence without it | `it-system-microservices`, `manufacturing-line`, `network-reliability` | measured | Don't use these templates' results as a reference |
| C#'s own conservation check fails (`edge_flow_mismatch_incoming`, 6 warnings): splits are written as arithmetic in the consumer while edges keep weight 1 | the three above and `transportation-basic` | measured | Treat their split values as suspect |
| Capacity, processing-time or served-count series are missing, so utilization or service time can't be computed for some nodes | `supply-chain-incident-retry`, `transportation-basic-classes` | measured | No opinion on those metrics |
| Constraint allocation and WIP-overflow routing are never applied on any run path; no template exercises either | engine | from code; measured for the templates | No opinion at all on constraints or WIP limits |
| Crash in a feedback loop when the queue has a constant loss series | engine | measured | Leave the loss out, or skip C# |
| `flowtime run` crashes on `queueDepth: self` | engine | measured | Write the queue as an explicit node |
| The orchestration path fails on `supply-chain-incident-retry` (duplicate key in `EdgeFlowMaterializer.BuildEdgeFlowSeries`, `EdgeFlowMaterializer.cs:27`) | engine | measured | Use `flowtime run` for that template |
| The test suite's warning baseline doesn't match today's runs (8 expected and 3 raised on `transportation-basic-classes`) | `TemplateWarningSurveyTests.cs` | measured against the code's table | Unexplained; don't rely on it |

So the closed forms and laws must cover everything the templates don't exercise and C# can't vouch for: a constraint that binds, WIP limits and overflow, dispatch schedules, retries and feedback loops.

## What crosses over

![Left, today's repository with sizes in production lines; right, the new repository. The Rust evaluator, plan and expression parser (885 lines) are copied, then reworked. The Rust model and compiler (2,071 lines) are rewritten against the model spec. The 12 templates are rewritten in the new template language as acceptance tests. The C# engine (15,014 lines) is consulted as a second opinion while deriving examples, never as a gate. Selected pages of the Svelte UI and dag-map are ported once the API settles. ADR-0001, the concept docs and the schema's rules become specs with checks. The .NET services, CLIs and TimeMachine (30,989 lines), the Blazor UI (32,812), the C# tests (62,056) and about 813,000 words of docs and plans stay behind in the archived repository.](spec/6-what-crosses-over.svg)

Only about 3,000 lines cross over as code. The rest crosses as tests (the templates) or as specs. What survives as code is the formula layer: expressions, operations over series, parameter overrides and the bin-by-bin loop, with the tests that pin their edge cases. How a service is modeled, latency and the compiler are new.

## Order of work

Usefulness is checked early. The first slice ingests telemetry, replays it with a model derived from it, and shows the result on one page, before templates or a full UI exist. Two sources of telemetry go through the same code: openshift-lab (`github.com/23min/openshift-lab`, private), a small real system that runs load tests and emits metrics and OpenTelemetry traces, and a simulation of it whose true parameters are known. Each step ends on a check a machine can run, and the oracle for each concept exists before its Rust does.

### Stage A: data and the first oracle

Needs no new repository.

1. **A1 Run bundle.** In openshift-lab, an export script writes one folder per load-test run: k6's run record, the configuration in force (replicas, pools, timeouts, retries), every span of the run from Tempo, and the Prometheus series for the run's time window. FlowTime owns the bundle's format as a CUE contract.
   *Done when* a constant-rate baseline and one ramp are exported, both pass the contract, and each bundle's span count matches k6's iteration count.
2. **A2 Simulation of the lab.** A discrete-event simulation, in Python on the oracle side, with the lab's services, worker pools and call order, written from the lab's code and its baseline spans rather than from FlowTime's spec. It writes the same bundle and records its true parameters.
   *Done when* the simulation at the baseline's rate matches the lab's per-hop latencies within a stated tolerance, and its bundles pass the contract.
3. **A3 The service concept and its oracle.** The concept spec for a service (servers, service time and variability; decisions 13 and 14) with its closed forms, laws and 3 to 5 worked examples; the reference evaluator for it; and the simulation suite's first systems, one service and two in series. The C# engine is consulted as a second opinion, and each disagreement is settled in the spec.
   *Done when* the reference evaluator passes every closed-form property of the concept.

### Stage B: the first useful slice

Needs the new repository.

1. **B0 New repository, gate first.** Starts once the next aiwf release is out and verifier-kit is stable enough. Generate the devcontainer with devcontainer-kit (answers below), create the Cargo workspace, run `aiwf init`, install the CUE recipe, and bring in verifier-kit's `./verify` and guard, with the oracle under a guarded path. Move `rewrite/` in, and copy the formula layer: evaluator, plan and expression parser. Tag this repository and archive it read-only, keeping it runnable for second opinions.
   *Done when* a failing test, a fixture the schema wrongly accepts, and a builder change to an oracle path each fail the pull request.
2. **B1 Ingest and flow accounting.** Read a bundle into binned series per hop, take the call graph and call order from spans, and balance each hop's flow with no model (telemetry-analysis F1's adapters, F7, F2's first half).
   *Done when* the simulated bundles balance exactly and their call graph equals the simulation's, and every imbalance in the lab's bundles is listed and explained.
3. **B2 Page, telemetry only.** One HTML page that draws the files B1 writes: the topology from the data, a time scrubber and per-hop charts, in SVG, with no framework and no server.
   *Done when* a browser test opens a bundle's output and scrubbing moves every view.
4. **B3 The service in the engine.** Implement A3's concept, with each service's state shaped for counts by stage and age so that mechanisms can be added later.
   *Done when* every closed-form property, law and worked example passes, the engine agrees with the reference evaluator, and the simulation suite states the concept's accuracy between 70% and 95% load.
5. **B4 Derive and replay.** Build a model from a bundle: servers from the configuration, service times from spans at low load, demand from the load test. Replay the run and report residuals per hop and bin (F1, F2).
   *Done when* the parameters derived from simulated bundles match the true ones and their residuals stay within tolerance, and the lab's bundles get residuals reported, largest first.
6. **B5 Page, model and telemetry.** Telemetry as dots, the replay as lines, residuals marked.
   *Done when* a browser test shows a replay beside its telemetry and the largest residual is marked.
7. **B6 First prediction.** From the baseline alone, predict the lab's knee and the hop that saturates first, committed before the lab's ramp runs (openshift-lab E-0004, M-0014).
   *Done when* the ramp's result is compared with the committed prediction and the comparison is recorded; a miss names what the model lacks.
8. **B7 An AI describes the system.** A skill reads openshift-lab's code, manifests and configuration and writes the model, citing the file and line behind each value, then validates, evaluates and revises it against the residuals. A cited value counts as configured, an uncited one as assumed.
   *Done when* its model is compared with B4's derived model and each disagreement is explained: a misreading, configuration that isn't what runs, or a mechanism the configuration doesn't show.

### Stage C: incidents, one mechanism each

Each incident is designed in openshift-lab with its settings known, predicted before it runs, then run, replayed and explained. A mechanism's closed forms and simulation checks come before its Rust, and it is accepted when it shrinks the residual on the lab's incident and recovers the true parameters from the simulated one. The telemetry-analysis spec's coverage map records each one.

1. **C1 Held workers.** A slow dependency holds the receiver's workers for the length of each call (F3, blocking calls).
2. **C2 Timeouts and retries.** A client timeout with retries on the call to encryption, under load (F3).
3. **C3 Wasted work.** The same, where the called service keeps working on calls whose caller gave up: the metastable case (F3). Whether the lab's services keep working after the caller gives up is to be checked.
4. **C4 Counterfactuals.** Replay an incident with one setting changed, predict the outcome, then run that setting in the lab.

*Done when* each mechanism passes its closed forms, recovers the simulation's truth and shrinks the lab's residual, and each counterfactual's prediction is compared with the lab's run.

Not in Stages A to C: templates and the template language, the HTTP API and live session, the Svelte UI, sweeps and optimization, request classes, autoscalers and modes, variability levels above 1, and what-ifs computed in the page.

### Next: D, more mechanisms

Parallel calls and joins, asynchronous hand-offs and message queues, circuit breakers and other modes, autoscalers, rate limits, caches and batching. Each arrives the way Stage C's do: an incident designed in the lab, its prediction, its replay.

### Future directions

A note, not a plan. These follow as needs arise, in no fixed order.

- **E More systems:** several request types (classes), a system of a different shape, and production telemetry, where spans are sampled and formats differ.
- **F Authoring:** the template language, for systems that don't exist yet, lowered to the model language, with the 12 templates as its acceptance tests once the mechanisms they use exist; and the AI skill grown up.
- **G Analysis:** uncertainty bands, the coverage map, competing mechanisms, and sweep, sensitivity, goal-seek and optimize.
- **H Interaction:** the engine in the browser for live what-ifs, the animated view, then the HTTP API, the session and a full UI; what the Svelte UI keeps is decided here (decision 9).
- **I Live:** telemetry streaming in, the model kept aligned with the present, and projection forward.

Also waiting for a need: chunked evaluation, an MCP server, the capacity-aware allocator (G-0038) and deployment. The cost of a day at steps of a second or less for dozens of components is not measured.

## Devcontainer

Generated by devcontainer-kit.

| Answer | Value | Why |
| --- | --- | --- |
| `languages` | `rust`, `python`, `go`; `node` when the UI returns | Rust for the system; Python for the oracle side; Go to build CUE and verifier-kit's Go `spine`; Node for the Svelte UI when it returns (direction H) |
| `aiwf` | `true` | Planning, contracts and the pre-push contract check |
| `siblings` | `flowtime-vnext`, read-only | The archived repository beside the new one, for edge cases. Running C# as a second opinion needs .NET, which this devcontainer leaves out, so that happens in the old repository's devcontainer |
| `ports` | The new binary's HTTP port; `5173` when the UI returns | One service replaces 8081 and 8090 |
| Open | How `cue` gets installed | devcontainer-kit has no CUE option: a Go install step, an apt package, or a new kit option |

## Risks

| Risk | How it shows up | Guard |
| --- | --- | --- |
| Second-system effect | The new design tries to fix everything at once | Stages A to C only, D next; the future directions wait for a need |
| Lost edge cases | Behavior that 62,000 lines of C# tests pinned silently changes | The schema's rule list; closed forms and laws for every concept; the archived repository and its C# engine stay available |
| A spec that is complete and wrong | Code and spec agree, and both are wrong | Closed forms derived independently of any evaluator; laws; validation against simulation, and later real telemetry |
| The builder grades its own work | Tests written to pass, as with the parity test that skipped what it couldn't compare | Oracle files the builder never edits; each new test seen failing first; planted bugs in `./verify --full` |
| Prose regrows | Plans and docs outnumber code and drift from it | Docs are the specs plus one architecture page; plans live only as aiwf entities |
| A long stretch with nothing to show | Usefulness is untested until late | A page on real telemetry at B2, a replay at B4 and a checked prediction at B6 |
| The simulation shares a mistake with the spec | The lab simulation and FlowTime agree, and both are wrong | The simulation is written from the lab's code and checked against the lab's baseline before use; the lab itself is the final check |
| A noisy lab | Runs differ because CRC shares one iMac with k6 and everything else | Repeated runs; the run record holds the node's CPU; residuals read against the spread between runs |
| A work-in-progress toolkit at the center | verifier-kit changes under FlowTime, or FlowTime waits on a view it hasn't built | Pin the version; FlowTime's needs go upstream as verifier-kit issues; the numeric oracle runs as plain Python tests until a view exists |
| CUE and Rust drift apart | The schema accepts a model the Rust parser rejects, or the reverse | One fixture set read by both; a disagreement fails the build |

## Open decisions

- **How verifier-kit enters the repository:** a pinned copy of its tools, a submodule that follows its main branch, or the Go `spine` binary once its port is done. Waits until verifier-kit is stable enough.
- **The backlog row's kind name:** `buffer` would keep it apart from the `queue` component.
- **Whether templates may refer to the name `self` lowers to,** or must name the queue explicitly.
- **What the UI keeps:** decided in direction H.
- **How the page's browser test runs:** Playwright for Python, which the oracle side already has, or Node.
## Measurements

Taken 2026-10-17 on commit 412467e0, in this repository's devcontainer (Linux; .NET SDK 10.0.401 targeting net9.0; cargo 1.98.1; Node 24.21.0). The scripts, and the model files they read, are in `evidence/measurements/`, indexed in `README.md`; entries without a script were single commands, and each entry says what was run. Figures 1 to 3 in `spec/` show the proposed system; Figures 4 and 5 chart the simulation entry below; the sizes in Figure 6 come from the last entry.

- **Rust test suite.** `cargo test --release` in `engine/`: 218 passed, 0 failed.
- **Every model in the repository through both engines.** 75 files through `flowtime run` (C#) and `flowtime-engine eval` (Rust):

  | Source | Files | C# runs | Rust runs |
  | --- | --- | --- | --- |
  | Generated from the 12 templates (`flow-sim generate`) | 12 | 12 | 2 (7 fail on the queue node, 3 on the router form; each file's byte-order mark also has to be removed first) |
  | `engine/fixtures/` | 21 | 2 | 14 |
  | Svelte samples | 8 | 5 | 8 |
  | Blazor models | 7 | 6 | 7 (the seventh is invalid on purpose; Rust accepts it) |
  | `examples/` and `fixtures/` | 17 | 4 | 7 (`examples/m0.const.yaml`, the README quickstart, fails in both) |

- **Four ways to write the same queue,** each through both engines: an explicit node in `nodes[]` runs in C# only; a queue implied by the topology and never referenced runs in both, identical in every bin; a queue implied and referenced by name runs in Rust only; `queueDepth: self` crashes `flowtime run` and makes Rust name the queue column `self`.
- **Where each engine reads its own form, they agree:** a demand spike and a `SHIFT`-based backpressure loop gave identical series in both.
- **Both C# paths over the 12 templates** at default parameters and declared seeds (`flowtime run`, and the Sim orchestration endpoint run locally): on the 11 where both succeed, every shared series is identical in every bin; orchestration adds 6 to 47 edge series per template, needs an explicit `rng.seed`, and fails on `supply-chain-incident-retry`.
- **Double counting.** With the templates' `outputs:` filter removed, every queue whose inflow includes a `_carry` series was re-run as the same recurrence without it: `it-system-microservices` peaks at 11,552 against 4,122, `manufacturing-line` 10,878 against 3,905, `network-reliability` 8,521 against 3,087.
- **Constraints and WIP.** The only template declaring constraints (`dependency-constraints-attached`) sets each constraint's capacity equal to its demand, so none binds; no template uses WIP limits.
- **Closed-form property.** A generator built 300 random single-queue spike models (6 to 40 bins; base demand below capacity below spike demand; random spike start and length) and ran each through `flowtime-engine eval`. Every bin of every model matched the closed form: growth of (spike demand − capacity) per bin during the spike, drain of (capacity − base demand) per bin after it, never below zero. With the backlog removed from the served formula, all 300 failed, each naming its first wrong bin.
- **Simulation, spike model.** The spike model as an M/M/1 queue (capacity 20 per minute, demand 10 rising to 30 per minute from minute 30 to 60), queue length sampled every minute, demand and capacity scaled together:

  | Volume | Runs | Simulated mean at the peak, against FlowTime | Middle 90% of runs at the peak | Still waiting at 90 min, simulated | Predicted, σ/√(2π) |
  | --- | --- | --- | --- | --- | --- |
  | 1× | 4,000 | +0.76% | 79–122% | 21.0 items, 7.0% of the peak | 19.5 |
  | 10× | 2,000 | +0.09% | 94–107% | 61.6 items, 2.1% of the peak | 61.8 |
  | 100× | 1,000 | −0.01% | 98–102% | 188 items, 0.63% of the peak | 195 |

- **Simulation, two services in series.** Service A (capacity 20 per minute) feeds B (22 per minute). Demand per 10-minute bin is 150, 180, 190, 210, 190, 180 and 150 items, each phase lasting 30 minutes and the last 60. FlowTime from `flowtime-engine eval`; simulation a tandem M/M/1, 2,000 runs. Queue at A at the end of each phase, FlowTime against simulation: 0 and 2.9 (75% load), 0 and 9.1 (90%), 0 and 15.8 (95%), 30 and 53.6 (105%), 0 and 35.5 (back to 95%), 0 and 14.9 (90%), 0 and 2.9 (75%). Queue at B: FlowTime 0 throughout, simulation 2.1 to 9.0.
- **Simulation, steady load.** M/M/1 with capacity 1, run for 4 million time units at each load, the first 5% discarded: average items in the system 1.00, 2.34, 4.00, 8.79 and 20.01 at loads 0.5, 0.7, 0.8, 0.9 and 0.95, against theory's 1.00, 2.33, 4.00, 9.00 and 19.00.

- **Sizes.** `wc -l` on production code, tests excluded: Rust engine logic 3,204; Rust artifacts, CLI and session 1,723; C# Core 15,014; Blazor UI 32,812; Svelte UI 15,916 (pages: analysis 1,826, time-travel 1,379, what-if 709, run 240, health 124; shared components 6,465 in 44 files); C# tests 62,056. Markdown in `docs/` and `work/`: 694 files, about 813,000 words.
