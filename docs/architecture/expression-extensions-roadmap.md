# FlowTime Expression Extensions Roadmap (Exploratory)

> **Purpose:** Capture the motivation, use cases, and risks for advanced expression operators (ABS/SQRT/POW, EMA/DELAY, conditional expressions, routers/autoscale helpers) that appear in the architecture vision but are not yet scheduled for implementation. This is an exploratory document—future milestones may refine or reprioritize these features.

## 1. Background
- FlowTime’s current engine supports a minimal expression set (arithmetic, SHIFT, CONV, MIN/MAX/CLAMP).
- Architecture docs (whitepaper §3, time-travel Chapter 2, retry-modeling §2) list additional functions needed for richer models (non-linear transforms, smoothed feedback loops, gating logic).
- Some functions exist in docs only (ABS/SQRT/POW enumerated in schemas) while others (EMA) appear in retry modeling guides but not in code.
- The 2025-10-07 engine audit flagged their absence as a gap; TT‑M milestones have focused on DLQs/time-travel without addressing these primitives.

## 2. Candidate Extensions
### 2.1 Numeric Transforms (ABS, SQRT, POW)
- **Use cases:**
  - Modeling square-root staffing rules, quadratic degradation, or power-law demand (e.g., throughput ∝ sqrt(capacity)).
  - Absolute value for symmetric deviations (error bounds, oscillation detection).
- **Current workaround:** Precompute external series or embed MIN/MAX hacks—makes templates less transparent.
- **Risks/complexity:**
  - Need to enforce numerical stability (e.g., SQRT of negative numbers, POW with fractional exponents).
  - Potential for algebraic loops if combined with capacity adjustments (must maintain causal evaluation).
- **Roadmap consideration:** Low-to-medium effort; can extend expression compiler once we finalize semantics.

### 2.2 EMA / DELAY Kernels
- **Source references:** `docs/architecture/retry-modeling.md` uses `EMA` in examples; whitepaper lists EMA in the built-ins.
- **Use cases:**
  - Smoothed failure rates for autoscaling decisions (avoid flapping on noisy data).
  - Modeling cooling-off or backlog smoothing without hand-authoring CONV kernels.
  - `DELAY` with explicit kernel semantics (beyond fixed lag SHIFT) to represent service-time distributions or deterministic reroute delays.
- **Risks:**
  - EMA introduces recursive state; needs clear definition for initialization and stability (α bounds).
  - DELAY kernels must stay causal; negative/lead semantics could break single-pass evaluation.
- **Roadmap:** Requires stateful node support similar to CONV; best tackled alongside future autoscale work.

### 2.3 Conditional Expressions (IF/THEN/ELSE)
- **Use cases:**
  - Reroute when backlog exceeds threshold (overflow to secondary queue).
  - Piecewise failure rates (e.g., error_rate = IF(utilization > 0.9, spike_rate, base_rate)).
- **Current state:** Templates rely on `MIN/MAX` combinations to emulate gating, which obscures intent and is fragile.
- **Risks:**
  - Conditions can introduce hidden dependencies; need guardrails to keep evaluation side-effect free.
  - Must ensure branching doesn’t enable algebraic loops (conditions referencing future bins).
- **Roadmap:** Add conditional AST node with strict semantics (`IF(condition, x, y)` where condition is per-bin boolean). Document best practices for maintainers.

### 2.4 Router/Autoscale Helpers
- **Whitepaper tie-in:** Chapter 2 mentions routers and autoscale policies as first-class abstractions.
- **Use cases:**
  - Splitting flow by weighted shares; dynamic routing based on metrics (failover, partial class routing).
  - Autoscale control loops (scale factor adjustments, cooldown timers) beyond manual expr nodes.
- **Risks:**
  - Routers may require per-edge series or normalized weights (ties into potential EdgeTimeBin work).
  - Autoscale loops need careful design to avoid non-causal dependencies or unstable oscillations.
- **Roadmap:** Defer until we have clearer requirements (Heatmap/Edge overlay work, autoscale epic). Documented here as aspirational features.

### 2.5 Behavior-Oriented Flow Controls
- **Motivation:** Several scenarios surfaced in CL‑M‑04.03.x require functions beyond “probabilistic demand.” These lean on runtime behavior (time gating, SLA breaches, conditional triggers) instead of PMFs.
- **Proposed primitives:**
  - `MOD`, `FLOOR`, `CEIL`, `ROUND`, `STEP`, `PULSE`: provide cadence/pulse control so queues can dispatch on schedules (bus-stop flows) without hand-rolled arrays.
  - `IF(condition, whenTrue, whenFalse)` or ternary equivalent to express SLA penalties, overflow routing, or priority cutovers plainly.
  - `HOLD(series, bins)` / `SMOOTH(series, alpha)` helpers for hysteresis or gradual ramp-up/down.
  - Lightweight stochastic toggles (`BERNOULLI(probability)` or `CHOICE(weights)`) for random failover events without defining entire PMFs.
  - `SEGMENT(binIndex, ranges[], values[])` or `DAYPART()` to encode behavior-by-time blocks for retail/shift models without massive literal arrays.
- **Use cases:**
  - Scheduled bus departures (transportation templates), manufacturing picker “waves,” or nightly store replenishments.
  - SLA breach counters that trigger escalations once latency exceeds thresholds (needs IF/STEP).
  - Autoscale cooldowns that prevent scale-in for N bins after scale-out (HOLD/SMOOTH).
  - Random jitter injection for failover drills or canary percentages.
  - Retail dayparting: lunchtime spikes vs. overnight idle time without external CSVs.
- **Roadmap:** Capture requirements here; implementation ties to future milestones (e.g., CL‑M‑04.03.02 for scheduled dispatch, later epics for SLA logic). Analyzer/UI work must accompany each new behavior to keep parity across docs, schema, and visualization.

## 3. Dependencies & Cross-Cutting Risks
- **Edge telemetry:** Some extensions (routers) benefit from explicit edge metrics; current architecture is node-centric.
- **Analyzer coverage:** New operators/schema fields require analyzer updates (conservation checks with EMA/conditional gating).
- **UI tooling:** Additional semantics must be surfaced in `/graph`/`/state` outputs; UI needs tooltips/legend updates.

## 4. Decision Guidance / Next Steps
- Keep this roadmap synchronized with milestone planning. When a future epic needs a specific operator (e.g., autoscale), flesh out the design in the relevant milestone doc and link back here.
- Avoid premature implementation: these primitives add engine complexity; we only pursue them when a concrete template or UI scenario justifies it.
- Track architecture references (whitepaper, retry modeling, audit) to ensure we close promised gaps deliberately rather than ad hoc.
