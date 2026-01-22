# Dependency Modeling Ideas (Aspirational)

> **Status note:** This document is **aspirational** and was drafted without full awareness of current engine constraints. It explores desirable futures and may exceed what FlowTime can implement today. For the feasibility‑grounded roadmap and current constraints, see `docs/architecture/dependencies-future-work.md`.

You’re basically trying to model a **shared, failure-prone, latency-amplifying resource** that (a) constrains throughput, (b) injects delays and retries (load amplification), and (c) can *discard* work after retry exhaustion (DLQ / abandon). And you need it to fit FlowTime’s constraints: **deterministic, grid-aligned, spreadsheet-y expressions, DAG evaluation, and telemetry-fed**. 

Below are several modeling/implementation plans (including your two), with tradeoffs, plus the approach I think best preserves “purity” *and* delivers explainability and telemetry compatibility.

---

## First: what “dependency modeling” must capture (or you’ll miss incidents)

A dependency outage/slowness causes at least four distinct system phenomena:

1. **Capacity bottleneck / contention**
   Multiple callers contend for the same limited resource; the dependency becomes the bottleneck.

2. **Delay inflation (queueing) → SLA breaches**
   Latency rises as a function of backlog and service rate (Little’s Law-ish). FlowTime already leans on backlog/latency primitives for exactly this. 

3. **Errors & retries → load amplification**
   Failures create future arrivals via a retry kernel/convolution. FlowTime explicitly calls out retries as `CONV(errors, retry_kernel)` (deterministic). 

4. **Retry exhaustion / timeouts → losses (DLQ / abandon)**
   Some portion of work is no longer “in backlog”; it’s *gone* (DLQ, poison queue, dropped). FlowTime’s roadmap already anticipates DLQ/spill semantics at the queue level. 

Any dependency representation that doesn’t make these four things visible will struggle to explain “why the SLA blew up” or “where the transactions went”.

---

## Constraints you must preserve (FlowTime purity)

These are the big design constraints implied by FlowTime’s core docs:

* **Deterministic evaluation on a canonical grid** (spreadsheet metaphor). 
* **DAG model with node ports producing time-series**. 
* Core primitives: **Backlog**, **Delay/SHIFT**, **Retry kernel**, **Autoscale**, and explainable lineage. 
* Must run directly on telemetry (“Gold schema”) without PMFs (PMFs optional). 

So: keep any new dependency concept either:

* (A) reducible to existing primitives at compile time (template expansion), or
* (B) a small new primitive that still behaves like backlog/delay/retry (scan + vector ops), deterministic.

---

## Alternative plans for modeling dependencies

### Plan 1 — Dependency as an explicit **in-series node** (what you’re already doing)

**What it is:**
Model a dependency call as an actual stage in the flow graph (service → dependency → continuation). The dependency node itself has capacity/backlog/latency/errors ports like a “service-ish” node.

**How it would look conceptually:**

* `Svc.Work` → `DB.Call` → `Svc.Continue`
* `DB.Call` uses Backlog semantics; outputs `served`, `queue`, `latency`, `errors`.
* Retries can be modeled as additional future arrivals (retry kernel) feeding back into the `DB.Call` input *via time shift/delay* (more on that later). 

**Pros**

* **Max explainability**: the dependency is literally a bottleneck node you can color by latency/utilization. 
* **Shared dependency is natural**: multiple callers can route into the same dependency node (contention is visible).
* **Telemetry alignment is straightforward**: dependency becomes a “node” in Gold telemetry (`node=DB.Payments`, `arrivals/served/errors`, etc.). 

**Cons**

* **Graph bloat**: every synchronous call turns into multiple stages (pre-call, call, post-call).
* **Synchronous semantics can be misleading** if you don’t also model caller thread/connection pool blocking.
  If you just move “work” into the dependency node, you may under-model caller-side backpressure mechanisms (thread pool saturation, connection pool waits).
* **Retries can tempt you into cycles** if you literally wire outputs back to inputs. You must keep it causal/delay-based or encapsulate it.

**Best use**

* When the dependency itself is the star of the show and you want incident forensics (“the DB queue exploded, everything upstream backed up”).

---

### Plan 2 — Dependency as a **constraint on a node** (what you’re already doing)

**What it is:**
The service node keeps its place in the flow; the dependency is attached as a constraint that modifies the service’s effective capacity/latency/errors.

**Typical deterministic forms**

* **Throughput cap:** `cap_eff[t] = MIN(cap_cpu[t], cap_dep[t] / calls_per_item)`
* **Concurrency/latency cap:** `cap_eff[t] ≈ concurrency_limit / dep_latency[t]` (bin-scaled)
* **Failure injection:** `errors_from_dep[t] = served[t] * dep_error_rate[t]`
* **Retry amplification:** `retry_load = CONV(errors, retry_kernel)` (added to arrivals). 

**Pros**

* **Model stays small & accessible** (huge win for adoption).
* Very “spreadsheet-y”: dependency is just another referenced series in an expression. 
* Easy to calibrate from telemetry when you don’t have dependency queue depth—just use measured latency/error rates.

**Cons**

* You can **lose bottleneck locality**: “why did Billing.Settle slow down?” becomes “because its capacity expression got smaller,” not “because DB X saturated.”
* **Shared dependency contention is hard** unless you introduce a shared resource allocator (otherwise each caller “sees” full dependency capacity and you double-count).
* Modeling DLQ/abandon as a side effect becomes opaque unless you add explicit “loss” outputs.

**Best use**

* When you want a **fast, minimal model** that still predicts/ explains capacity collapse under dependency slowness, but you’re OK with less fidelity about where queueing happens.

---

### Plan 3 — Dependency as a **shared Resource Pool node** (new, and very powerful)

This is the main “missing third way” between your two approaches.

**What it is:**
Treat the dependency as a first-class node, but **not “in series” with business flows**. Instead it is a **resource pool** that grants capacity/tokens to callers.

Think: “DB has 5k req/5m capacity (or 500 concurrent). Callers compete. Allocation is deterministic and explainable.”

**Core mechanism**

* Callers produce **call demand series** (derived from their work):
  `db_demand_from_A[t] = started_A[t] * calls_per_item_A`
* Dependency pool allocates capacity across all demands using a policy you *already have conceptually* (priority / weighted-fair). FlowTime backlog already references capacity sharing policies. 
* Pool outputs per-caller `granted_calls[t]` and pool-level `queue/latency/errors`.

**Pros**

* **Correct shared-bottleneck modeling** without forcing the dependency into the business-flow DAG as a stage.
* **Great explainability**: “DB cap was 2000 calls/bin, Billing demanded 1600, Orders demanded 1200; Orders got squeezed → upstream SLA breach.”
* **Telemetry friendly**: the pool node maps neatly to a dependency telemetry stream in Gold (`arrivals/served/errors/capacity_proxy`). 

**Cons**

* Needs a clear story for **dimensions**:

  * Flows are business classes.
  * Callers are services.
  * Now you need “caller share” inside the dependency.
    You can solve this by making caller a port dimension (multi-port output) or by compiling to internal “subflows”.
* It’s a new concept for modelers: “dependency as a shared pool” is intuitive to engineers, but you’ll want UI affordances (collapse/expand, top callers).

**Best use**

* When you care about **cross-service contention**, root causing cascades, and explaining multi-team incidents.

---

### Plan 4 — Dependency semantics on **edges**, compiled into hidden nodes (new; best for “accessibility + purity”)

**What it is:**
Modelers draw a clean graph: Service → Service, and attach “dependency call policies” to edges (or to a `dependsOn:` list). The engine **compiles** these into a subgraph of standard primitives (Backlog/Delay/Retry/Spill), but the UI can collapse it.

This fits FlowTime’s “expressions as core + lineage” philosophy: you can still show the expansion via lineage metadata. 

**Pros**

* **Most accessible UX**: dependencies don’t explode the visible DAG.
* **Purity**: engine stays simple if compilation uses primitives; DAG eval remains intact. 
* You can support both of your approaches as two compilation modes:

  * “Inline stage expansion” (Plan 1 behavior)
  * “Capacity constraint collapse” (Plan 2 behavior)

**Cons**

* Debuggability risk if the expansion is too magical. You must rely on strong lineage/inspection so users can see: “this edge expands to these nodes and these series.” 

**Best use**

* When you want a small modeling surface area but still want fidelity when needed.

---

### Plan 5 — Allow explicit **time-delayed feedback** for retries (advanced; I’d keep it internal)

This is less about “dependency node type” and more about “retry loops”.

FlowTime’s core requirement says **DAG**. 
So I would *not* expose general cycles.

But you can still model retries without violating DAG by ensuring retries are represented as **future arrivals via DELAY/SHIFT** and compiled into an acyclic subgraph. This aligns with the existing retry primitive definition. 

So: keep user models DAG; implement retry logic either:

* inside a `RetryPolicy` expansion template, or
* in a dedicated `RetryNode` that is acyclic (attempt0 → attempt1 → attempt2…).

---

## The “cleanest” design for FlowTime

### Recommendation: **Dependency = Resource Pool node + CallPolicy template expansion**

This combines the strengths of Plans 1/2/3/4 while keeping the surface area clean.

#### 1) A first-class `Dependency` node (resource pool)

A dependency node should be a *resource provider*, not necessarily a stage in the business flow:

**Ports (suggested minimal set)**

* `in` (call demand, maybe aggregated)
* `served_ok`
* `served_err`
* `queue` (call backlog)
* `latency_min` (call wait estimate)
* `utilization`

These match FlowTime’s core outputs (`arrivals`, `served`, `queue`, `latency`, `capacity`, `utilization`). 

**Internals**

* Use Backlog primitive for call backlog + latency estimate. 
* Error modeling:

  * simplest: error_rate[t] as an input series (from telemetry)
  * richer: error_rate as function of utilization (scenario-able), but keep deterministic.

**Multi-caller allocation**

* Deterministic priority / weighted-fair allocation across callers—same family of policies as multi-class. 

> Key point: this is where you get true “shared dependency contention” fidelity.

#### 2) A `CallPolicy` attached to a caller (schema-level), compiled into nodes

Instead of forcing the modeler to hand-wire retry/dlq/etc, you provide a small schema like:

```yaml
nodes:
  - id: Billing.Settle
    kind: service
    # ...
    calls:
      - dep: DB.Payments
        calls_per_item: 2.0
        mode: sync   # or async
        bulkhead: 200           # max inflight calls
        timeout_min: 1.5
        retry:
          max_attempts: 3
          backoff_kernel: [0.0, 0.6, 0.3, 0.1]
        on_exhaust: dlq
```

Then the compiler expands into a small subgraph using **Backlog**, **DELAY/SHIFT**, and **Retry = CONV(errors, kernel)**. 

This keeps modeling accessible while still being pure/deterministic.

#### 3) Support two compilation modes (your two approaches) behind one user concept

For each dependency call, allow a switch:

* **Mode A: “Constraint view”** (fast, compact)

  * Collapse everything into an effective capacity / latency / error augmentation on the caller.
  * Best for high-level capacity planning.

* **Mode B: “Expanded view”** (forensics, explainability)

  * Materialize explicit nodes: `CallDemand → DepBacklog → Success/Failure → RetryDelay → … → DLQ`.
  * UI collapses by default but can expand when you debug an incident.

This is a really good compromise: the *model surface* stays clean, but you can always expand the internals.

---

## Modeling retries + DLQ without violating DAG (and keeping determinism)

A clean deterministic expansion is to **unroll attempts** (attempt0 → attempt1 → attempt2…) rather than feeding back.

For `max_attempts = N`, you compile to N “attempt streams”:

* Attempt 0 demand: `a0[t]` (derived from caller starts)
* Served at dependency: `s0[t]` (via pool allocation/backlog)
* Errors: `e0[t] = s0[t] * err_rate[t]`
* Retry arrivals: `a1 = DELAY(e0, backoff_kernel)`
* …
* DLQ: `dlq = eN` (or `eN - retried` if you allow partial retries)

This is **acyclic** because every attempt depends on a delayed prior attempt (future bins). It also matches FlowTime’s stated retry-kernel approach. 

For “lost transactions,” you now have explicit series:

* `dlq[t]` (exhausted)
* optionally `abandon[t]` (timeouts / client cancel)
* `loss[t] = dlq[t] + abandon[t]`

And it dovetails with planned “DLQ/spill/finite buffer” semantics at the queue layer. 

---

## Modeling backpressure realistically: sync vs async matters

This is where many “dependency as a node” models go wrong.

### Async calls (true async, event loop, non-blocking)

* Service capacity is mostly CPU-bound; dependency affects *completion latency* and *error/retry*, not necessarily the “workers available”.
* Best modeled as:

  * business item starts → dependency inflight/backlog/latency → item completes when dep returns

### Sync calls (thread-per-request, blocking IO, limited connection pool)

* Dependency latency consumes **caller concurrency**, reducing throughput; backpressure can originate at caller *before* the dependency.
* Best modeled with an internal “inflight” state (or compiled subgraph) that enforces:

  * `inflight[t] = inflight[t-1] + starts[t] - completes[t]`
  * `starts[t] <= bulkhead - inflight[t-1]`

You can implement this deterministically as another Backlog-like scan (it’s basically a “concurrency bucket” stock). This is consistent with FlowTime’s stateful-scan primitives. 

**Practical recommendation:** expose `mode: sync|async` on the call policy, and compile different expansions.

---

## Telemetry compatibility: how to feed dependencies cleanly

FlowTime’s Gold schema already supports the primitives you need: `arrivals/served/errors` plus optional `queue_depth`, `capacity_proxy`, etc. 

To make dependency modeling work well with telemetry:

1. **Create a Gold stream for dependency calls**

* `node = "DB.Payments"` (or whatever)
* `arrivals = call_count`
* `served = success_count` (or completions)
* `errors = failure_count`
  If you can tag dependency telemetry with `flow` (class), even better.

2)Fitting/learning helpers already align with this:

* capacity estimation via saturation/envelope rules (works for dependencies too). 
* learn retry kernels by correlating failures with future arrivals (exactly your retry/backoff problem). 
* learn abandon rate tied to latency (useful for timeouts / give-ups). 

3. PMFs remain optional

* You can run directly on telemetry time series; PMFs only as convenience or for kernels. 

---

## When to choose which approach (rules of thumb)

* Use **Constraint-on-node** when:

  * you need quick answers with minimal graph complexity,
  * the dependency is effectively dedicated to the service (not shared),
  * you mainly care about throughput collapse rather than detailed contention.

* Use **Explicit dependency node (series)** when:

  * you need incident explainability (“the DB queue was the bottleneck”),
  * you want to see queue/latency/utilization on the dependency itself,
  * retries/DLQ behavior must be visible as first-class outputs.

* Use **Resource Pool dependency** when:

  * the dependency is shared across multiple callers and contention is the story,
  * you want to attribute “who consumed the dependency” during an incident,
  * you want to avoid double-counting dependency capacity.

* Use **Edge semantics compiled to nodes** when:

  * you want a simple modeling UX but still want expandability for debugging,
  * you want to support both “compact” and “expanded” views from one schema.

---

## A concrete implementation path (low risk, high leverage)

If I were implementing this in FlowTime with minimal risk:

1. **Start with schema + compiler expansion (Plan 4)**

* Add `calls:` / `dependsOn:` blocks.
* Compile to existing primitives (Backlog/Delay/Retry) so the engine stays small and deterministic. 

2. **Introduce Dependency as a Resource Pool node (Plan 3)**

* Implement deterministic per-bin allocation (priority/weighted-fair) among caller demands (same conceptual machinery as multi-class). 
* Output pool-level queue/latency/util, plus per-caller grants.

3. **Add DLQ/spill outputs in the expansion**

* Even before full “Backlog v2” exists, you can emit a `dlq` series from retry exhaustion.
* Later, align with M13 “DLQ, priority spill, finite buffer” as the canonical queue semantics. 

4. **Telemetry adapters**

* Map dependency calls into Gold schema rows; now your dependency nodes can run in “replay mode” (telemetry-driven) or “what-if mode” (capacity/error/latency parameterized). 

---

## The one design decision that matters most

If you only take one thing from this:

**Make “dependency” a first-class *resource* with explicit capacity sharing across callers, and make “calling it” a policy/template that can expand to either (a) an in-series stage view or (b) a collapsed constraint view.**

That gives you:

* **purity** (still deterministic, grid-aligned, DAG-friendly), 
* **accessibility** (modelers declare `calls:` instead of wiring 12 nodes),
* **telemetry compatibility** (Gold schema already supports the needed signals), 
* and **incident-grade explainability** (you can expand and show retries, DLQ, and the actual bottleneck).

If you want, I can sketch a minimal “compiler expansion” (node list + expressions) for:

* sync call with bulkhead + timeout + retry + DLQ, and
* shared dependency pool with weighted-fair caller allocation,

using only primitives already in the docs (Backlog/Delay/Retry/Capacity caps). 
