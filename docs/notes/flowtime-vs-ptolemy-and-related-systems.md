# FlowTime vs Ptolemy (and Related Systems)

This note captures a design review of FlowTime using Ptolemy and a few other modeling/simulation systems as reference points.

## 1. Composition: FlowTime vs Ptolemy

### Ptolemy

- **Core idea:** Systems are graphs of *actors* coordinated by a *director* (actor-oriented design).
- **Composition:**
  - Models are built visually: drag actors onto a canvas and wire their ports.
  - A *director* (SDF, DE, CT, etc.) defines the global semantics: time model, scheduling, event handling.
- **Execution:**
  - The director controls actor firing and event queues.
  - The same actor network can, in some cases, be run under different directors.

This gives a very rich semantics framework (multiple time/co-ordination models) but also a wide surface area and a fairly “heavy” modeling environment.

### FlowTime

- **Core idea:** Time-binned **flow DAG** over series/metrics. The engine is a post-processing semantics layer between raw telemetry and UX/analysis.
- **Composition:**
  - **YAML templates** describe:
    - Nodes (sources, expression nodes, services, queues, soon `serviceWithBuffer`, routers, etc.).
    - Edges/topology.
    - Parameters (patterns, capacities, classes, expressions).
  - Templates are compiled into a DAG model that the engine evaluates on a fixed time grid.
- **Execution:**
  - The engine executes the DAG over discrete time bins; queueing, routing, scheduled dispatch, etc. are encoded in node types and expressions.
  - The UI is a **visualization** and exploration surface over evaluated results (`/graph`, `/state_window`, etc.), not a computation surface.

In short: Ptolemy is “canvas-first, director-pluggable, multi-semantic simulation of *events/signals*”; FlowTime is “template-first, fixed-semantics, time-binned simulation of *flows*.”

This distinction is intentional and healthy for FlowTime’s goals.

---

## 2. Using Ptolemy as a Reference: Critiques & Confirmations

### 2.1 Where FlowTime is strong for its goals

1. **Semantics discipline (one clear director)**
   - FlowTime effectively chooses one "director": **discrete-time, fixed-step DAG evaluation** with explicit queue/flow semantics.
   - This is simpler to reason about than Ptolemy’s multiple directors and better aligned with:
     - Determinism.
     - High performance.
     - Post-processing over telemetry.
   - We should *not* chase Ptolemy’s generality here.

2. **Engine vs UI separation**
   - FlowTime keeps **all semantics and computation in the engine/API**; the UI only visualizes and annotates.
   - Ptolemy blurs modeling and simulation into a single environment.
   - The FlowTime constraint “no computation in the UI” is important:
     - Semantics stay centralized.
     - There’s no risk of divergence between “what the engine thinks” and “what the UI shows.”
     - The engine remains usable as a standalone pipeline/post-processing component.

3. **Text-first, template-driven modeling**
   - YAML templates are:
     - Diffable and versionable.
     - CI/CD friendly.
     - Easy to parameterize and generate programmatically.
   - Ptolemy’s GUI-based models are harder to treat as code.
   - For FlowTime’s “pipeline component” ambition, templates are preferable.

### 2.2 Ptolemy-inspired gaps / watchpoints for FlowTime

These are not "we must copy Ptolemy", but areas where Ptolemy highlights problems that FlowTime should think about.

#### a) Model composition and hierarchy

Ptolemy:

- Encourages **hierarchical composition**:
  - Composite actors that encapsulate subgraphs.
  - Reusable subsystems with defined interfaces.

FlowTime:

- Currently has single, parameterized templates.
- There are ideas/epics around **subsystems**, but not yet a full hierarchical composition story.

Implication:

- Large, flat DAGs will eventually get unwieldy.
- FlowTime should develop a clear notion of **reusable subgraphs/subsystems** with:
  - Well-defined input/output contracts (flows/series at the boundary).
  - Parameterization.
- The `subsystems/` and `streaming/` epics are natural homes for this; they should not be under-scoped.

#### b) Explicit “directors” vs implicit semantics

Ptolemy:

- Makes the *director* explicit and front-and-center; you always know which time/coordination semantics apply.

FlowTime:

- Semantics are defined in docs/whitepaper, but the model format currently treats them as **implicit**.

Suggestion:

- Keep a **single semantics model** (no multiple time models), but consider making it explicit in the model metadata, e.g.:
  - `semantics:` or `engineProfile:` with fields like:
    - `timeModel: fixedBins`
    - `queueModel: flowConservation`
    - `arrivalModel: aggregated`
- This makes it clear to humans and tools which rules apply and would guard against accidental semantic drift.

#### c) Interactive modeling vs text-first modeling

Ptolemy:

- Very strong in **interactive graph editing** (drag/drop, connect actors, configure visually).

FlowTime:

- YAML templates are the source of truth; the UI today is a **viewer**, not a primary modeling tool.

Implication:

- Users accustomed to Ptolemy/Simulink may miss the interactive experience.
- If FlowTime ever introduces an interactive **template editor** in the UI:
  - It must read/write the canonical YAML schema.
  - It must remain a thin front-end to the same semantics, not a second modeling language.

Guardrail:

- Even if the UI becomes capable of “drag to add a queue, change capacity,” *all* such changes should be persisted as template edits, and the engine remains the only semantics owner.

---

## 3. Other Relevant Systems and Lessons

### a) Simulink / Stateflow

- **What it is:** Block-diagram modeling for dynamic systems and control; strong visual editor, code generation, integration with MATLAB.
- **Relevance:**
  - Shows the power and complexity of graphical modeling with hierarchy.
  - Demonstrates the need for clear modularization and subsystem interfaces in large models.
- **Lessons for FlowTime:**
  - Hierarchy and modularity are vital as models grow.
  - Separation between “plant” and “controller/observer” is a useful pattern; in FlowTime terms, that might inform how we separate telemetry-based flows from control policies.
- **Caution:**
  - Simulink is continuous-time and extremely general; FlowTime should *not* attempt that breadth.

### b) System Dynamics tools (Stella, Vensim, Powersim, AnyLogic SD mode)

- **What they are:** Tools for modeling stocks and flows, often as continuous-time systems of differential equations.
- **Relevance:**
  - Conceptually similar: stocks (levels) and flows over time.
  - FlowTime’s queues/backlogs are stock-like; served/arrivals are flow-like.
- **Lessons for FlowTime:**
  - Good visual metaphors for stocks and flows help users reason about accumulation and delay.
  - FlowTime’s topology UI can lean into this mental model without changing its discrete-time semantics.
- **Difference:**
  - System dynamics is usually continuous; FlowTime is discrete, time-binned, and oriented around telemetry.

### c) Queueing network / performance modeling tools (e.g., QNAP2, PDQ, JMT)

- **What they are:** Tools for analyzing networks of queues with various service disciplines and routing.
- **Relevance:**
  - Similar focus on **throughputs, queues, service rates, and class routing**.
- **Lessons for FlowTime:**
  - There is long-standing value in recognizable patterns (M/M/1, batch server, fork-join, etc.).
  - FlowTime can provide **template patterns** that play a similar role but are matched to telemetry-driven, time-binned semantics.

### d) Static/Dynamic Dataflow and streaming frameworks

- **What they are:** SDF dataflow tools, streaming systems like Flink/Kafka Streams; they model data moving through processing graphs.
- **Relevance:**
  - They share the idea of graphs of computation with flows of data/tokens.
- **Lessons for FlowTime:**
  - It’s important to distinguish **token-level streaming** (per-event) from **aggregated flows** (per-bin aggregates).
- **Guardrail:**
  - FlowTime has chosen aggregated, time-binned semantics and should not drift into token-level DES.

---

## 4. Design Review Conclusions for FlowTime

Key conclusions and guardrails, framed as guidance going forward:

1. **Stay flow-first; no DES.**
   - FlowTime is about flows and aggregated metrics, not individual entities.
   - Any silver-telemetry drill-down (per-entity paths) should be:
     - Optional and derived.
     - Strictly *outside* the core engine semantics.

2. **Engine-only semantics, UI-only visualization.**
   - All computation and semantics live in the engine and APIs.
   - The UI can enhance visuals, but if it needs a new metric, that metric must come from the engine.

3. **Embrace ServiceWithBuffer as the canonical “station” abstraction.**
   - ServiceWithBuffer = service + explicit queue + schedule.
   - All queue behavior and schedules belong to the service-with-buffer, not to UI-only "queue nodes".

4. **Plan hierarchical/compositional modeling (subsystems).**
   - Introduce clear mechanisms for reusable subgraphs/subsystems with:
     - Defined input/output interfaces.
     - Parameters.
   - Keep semantics the same; this is structural composition, not a new time model.

5. **Make engine semantics explicit (but not pluggable).**
   - Borrow the *idea* of Ptolemy’s director (explicit semantics), but:
     - Maintain a single semantics family (fixed, discrete time, flow DAG).
     - Expose it as metadata to make assumptions clear and enforce discipline.

6. **Keep templates as the single source of truth.**
   - Any visual/graphical modeling should be a UI front-end to the YAML template schema.
   - Avoid introducing a second, GUI-only representation of models.

7. **Be comfortable saying "no" to generality.**
   - It is better for FlowTime to be sharp, high-performance, and explainable for its chosen semantics than to be a general-purpose simulation IDE.
   - Features from Ptolemy, Simulink, or DES tools should be evaluated through the lens of FlowTime’s mission:
     - Deterministic, time-binned flow analysis.
     - Telemetry-informed post-processing.
     - First-class but *thin* UI for analysis.

These points can inform the Ptolemy epic in `docs/architecture/ptolemy/` and serve as a reference whenever we’re deciding whether a new idea is aligned with FlowTime’s core identity or risks turning it into “yet another general-purpose simulator."