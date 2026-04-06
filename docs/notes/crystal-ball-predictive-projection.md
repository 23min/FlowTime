# Crystal Ball: Predictive Projection from Observed Traffic

A design note capturing the "crystal ball" capability that emerges when FlowTime's deterministic evaluation is combined with real-time traffic observation.

---

## The insight

FlowTime evaluates the entire time grid in milliseconds. Real work takes hours or days to propagate through a system. When you observe traffic entering the system right now, FlowTime can compute the state at every downstream node across all future bins faster than the real system will get there.

This is not forecasting. It is not probabilistic prediction. It is deterministic projection: given observed arrivals and a calibrated model, FlowTime computes the exact consequences at every node and every future bin according to the model's algebra. The projection is as precise as the model is accurate.

The engine already has the mechanics for this. The evaluation pipeline -- compile, topologically sort, single-pass evaluate -- does not care whether input series come from templates, captured telemetry, or a live observation feed. The missing pieces are not engine changes; they are real-time ingestion and model calibration.

---

## How it works: an example

Consider a payment processing pipeline with three stages: intake, validation, and settlement. Work takes approximately 1 hour to propagate from intake to settlement. The model has retry kernels at each stage.

At 10:00 AM, you observe 150 transactions/bin entering the intake node, a 50% increase over the expected 100/bin. FlowTime evaluates the full grid forward:

- **Intake (now):** Queue depth at intake is already known -- it is the current observation. No prediction needed.
- **Validation (+20 minutes):** FlowTime shows validation queue depth will reach 120 items by 10:20, given intake's current throughput rate and validation's capacity of 80/bin. Utilization at validation will be 100%.
- **Settlement (+60 minutes):** FlowTime shows settlement queue depth will reach 85 items by 11:00 AM, with a latency estimate of 45 minutes. If settlement has a T+0 SLA, it will breach at 10:50 AM.

The retry kernel extends the prediction horizon further. If validation has a 10% failure rate and retry kernel `[0.0, 0.5, 0.3, 0.2]`, the retry effects three bins out are already determined by the current error rate. FlowTime shows the retry-amplified load arriving at settlement 3 bins from now, before the real system has produced those retries.

All of these projections are available right now, in under a millisecond, because FlowTime evaluates the entire grid in a single pass. The real system will not reach these states for another hour.

---

## Three modes

The crystal ball is the third mode of operation, alongside the two FlowTime already supports:

| Mode | System | Demand | Question |
|------|--------|--------|----------|
| What-was (replay) | As it was | As it was | "What happened?" |
| What-if | Changed | Same or changed | "What if we change the system?" |
| Crystal ball | As it is now | Observed, projected forward | "What will happen?" |

The what-was and what-if modes are already implemented. They differ only in the source of input data (captured telemetry versus modified parameters). The crystal ball mode differs in a more fundamental way: its input data is partially observed (entry nodes have live data) and partially projected (downstream nodes are computed from the model).

But from the engine's perspective, this distinction does not matter. The engine evaluates whatever series it receives. If the arrival series at entry nodes contains observed data for early bins and projected patterns for later bins, the engine computes downstream consequences the same way it always does.

---

## Three requirements

For predictive projection to produce useful results, three conditions must hold:

### 1. Calibrated model

The model's topology, capacity parameters, failure rates, and retry kernels must match the real system closely enough that the algebra produces realistic consequences. A model that assumes 10% failure rate when the real system has 25% will produce inaccurate queue depth projections.

Calibration is the hardest requirement and the one with the longest path to delivery. E-15 (Telemetry Ingestion, Topology Inference, and Canonical Bundles) provides the infrastructure: the Graph Builder infers topology from real traces, and the Gold Builder produces binned facts that can be compared against model predictions. E-18 (Headless Pipeline) enables model fitting -- adjusting model parameters to minimize the gap between model output and observed telemetry.

A calibrated model does not need to be perfect. The crystal ball is useful even when the model is approximate, because the projections still capture structural effects (retry amplification, queue inertia, batch timing) that would be invisible without the model. The accuracy of specific queue depth predictions improves with calibration; the qualitative shape of the projection is valuable from the start.

### 2. Fresh arrivals data

The crystal ball requires near-real-time observation of traffic at entry nodes. Yesterday's telemetry answers "what happened yesterday?" but not "what will happen this afternoon?" The observation feed must be fresh enough that the prediction horizon is useful.

The freshness requirement depends on the system's propagation time. For a pipeline where work takes 1 hour from entry to the node of interest, a 5-minute-old observation still provides a 55-minute prediction window. For a system with 5-minute propagation, the observation must be within seconds to be useful.

This requires either a streaming ingestion pipeline or a frequently-refreshed batch ingestion path. Neither exists today. The streaming capability is a future epic beyond E-15's batch-first scope.

### 3. Stable system parameters

The prediction assumes that system parameters (capacity, failure rates, retry kernels) do not change during the prediction window. If the operations team scales up capacity in response to the same observation that triggered the prediction, the prediction becomes stale.

This is a feature, not a bug. You can model the capacity change too: run the crystal ball twice, once with current parameters and once with the planned intervention. Compare the two futures. This is exactly the what-if workflow, but with observed traffic instead of synthetic demand.

---

## Prediction horizon

The natural prediction horizon equals the propagation delay through the topology. For any node of interest, the prediction window is the longest path (in time) from entry to that node.

- **Entry node**: Zero horizon. The current state is observed, not predicted.
- **First hop**: Prediction window equals one bin's processing time plus any temporal delay in the edge.
- **Deep node**: Prediction window is the cumulative path length. For a 5-stage pipeline with 10-minute bins and `SHIFT(upstream, 1)` at each stage, the deepest node has a 50-minute prediction horizon.

Retry kernels extend the effective horizon. A kernel `[0.0, 0.5, 0.3, 0.2]` means that errors at bin `t` generate retries at bins `t+1`, `t+2`, and `t+3`. The retry contribution to downstream load at `t+3` is already determined by the error count at `t`. If the error count at `t` is observed, the retry effects three bins into the future are known.

The total prediction horizon for a node is therefore: `max(path_length, path_length + kernel_span)`, where `kernel_span` is the number of nonzero elements in the longest retry kernel along the path.

---

## Variability bands

The current engine produces sharp deterministic predictions: "the queue depth at settlement will be 47 at bin 14." This is precise for the model but does not capture the uncertainty inherent in projecting a stochastic real system.

With the planned E-10 Phase 3c variability work (`m-ec-p3c` -- Cv tracking and Kingman's approximation), the engine could produce confidence bands: "expected queue depth 47, 80th percentile 58, 95th percentile 72." The Kingman formula relates queue waiting time to the coefficient of variation of arrival and service processes:

```
W ~ (Cv_a^2 + Cv_s^2) / 2 * (rho / (1 - rho)) * E[S]
```

If the model carries `Cv` values from calibrated input distributions, the crystal ball can project not just the mean trajectory but the variability band around it. This turns a point prediction into a risk assessment.

Until the variability milestone ships, crystal ball projections are point estimates. Users should understand that the prediction is exact for the model and approximate for reality.

---

## How it connects to existing epics

The crystal ball is not a single epic. It is the capability that emerges when several existing and planned pieces come together:

| Epic | Contribution to crystal ball |
|------|------------------------------|
| **E-15** (Telemetry Ingestion) | Calibrated model: Graph Builder infers topology from traces, Gold Builder produces binned facts for calibration. This is the hardest prerequisite. |
| **E-10 Phase 3c** (Variability) | Richer predictions: Cv tracking enables confidence bands instead of point estimates. |
| **E-10 Phase 3a** (Cycle Time) | Prediction horizon calculation: cycle time decomposition provides per-node propagation delay. |
| **Streaming epic** (future) | Real-time arrival data: near-real-time ingestion at entry nodes. Without this, the crystal ball uses recent-batch data with reduced freshness. |
| **Anomaly Detection** (future) | Divergence detection: flags when predictions diverge from subsequently observed reality, indicating model drift. |
| **E-17** (Interactive What-If) | Live parameter adjustment: change system parameters in the crystal ball projection and see the future change in real time. "What if we add capacity now -- when does the queue clear?" |
| **E-18** (Headless Pipeline) | Model calibration: parameter fitting and sensitivity analysis against real telemetry. Produces the calibrated models the crystal ball requires. |

The engine already has the evaluation mechanics. The missing pieces are (1) real-time or near-real-time ingestion, (2) model calibration from observed data, and (3) optionally, variability bands for richer predictions.

---

## The crystal ball is emergent

No single epic delivers the crystal ball. Instead, it emerges naturally when:

1. E-15 delivers topology inference and telemetry ingestion (calibrated model).
2. A streaming or rapid-batch ingestion path delivers fresh arrivals data.
3. The existing engine evaluates the model with observed entry data.

Each of these pieces has independent value: E-15 enables what-was replay on real data, streaming enables operational dashboards, and the engine already powers what-if analysis. The crystal ball is the intersection of all three, requiring no additional engine capability beyond what is already shipped.

This emergent property is a consequence of FlowTime's core design: because the engine evaluates the entire grid deterministically from input series, any source of input series -- synthetic, captured, or observed -- produces valid output. The crystal ball is not a feature to be built; it is a usage pattern to be enabled by infrastructure.

---

## Relationship to E-15's process mining framing

E-15 frames telemetry ingestion as "prove FlowTime works on real data." The Graph Builder takes event logs or traces and infers a topology. The Gold Builder bins raw events into per-node series. The validation goal is parity: does the model reproduce observed behavior?

The crystal ball reframes the same infrastructure as "predict the future from real data." Same Graph Builder output (the calibrated topology). Same Gold Builder pipeline (but with fresh data instead of historical batches). Same engine evaluation. Different narrative.

E-15's Graph Builder + Gold Builder produce the calibrated model. Add fresh arrivals at entry nodes and you have prediction. The pipeline is identical; only the temporal relationship between input data and evaluation changes.

This reframing does not change E-15's scope or deliverables. It adds a motivation beyond validation: the same calibration infrastructure that proves the model works also enables the model to project forward.

---

## Not a probabilistic forecast

The crystal ball shows one precise future per scenario. "The queue depth at settlement will be 47 at bin 14," not "73% chance it exceeds 40." This is a deliberate design choice.

**Sharpness is a feature.** A point prediction is traceable: you can follow the algebra from the observed arrivals through every intermediate node to the predicted queue depth. You can explain why the number is 47 and not 50. A probabilistic forecast obscures this traceability behind distributions.

**Sharpness is also a limitation.** The prediction is exact for the model but approximate for reality. The model does not capture every source of variability in the real system. Users must understand that when FlowTime says "queue depth 47," it means "47 according to the model's algebra given observed inputs," not "47 with certainty."

The planned variability work (`m-ec-p3c`) offers a middle path: a deterministic point estimate plus analytically-derived confidence bands from Kingman's formula. This preserves traceability (the center estimate is still fully traceable) while acknowledging uncertainty (the bands communicate "the model predicts 47 but the real system could reasonably produce 35-72 given typical variability").

---

## Use cases

### Capacity planning

"If Black Friday traffic is 3x normal, where does the system break?"

Feed 3x current observed traffic into the model. FlowTime shows which nodes exceed capacity first, how the retry cascade propagates, and when the system reaches steady-state overload. No need to wait for Black Friday to find out.

### Incident prevention

"Current error rate at the payment service predicts queue overflow at settlement in 2 hours."

Observe the current error rate at the payment service. The retry kernel spreads those errors across future bins. FlowTime projects the downstream queue depth and flags when it crosses the settlement service's capacity threshold. The operations team has a 2-hour warning window.

### SLA forecasting

"Given current intake velocity, Express orders will breach the T+0 SLA by 14:00."

A multi-class model with Express, Standard, and Economy classes. Observe current intake rates per class. FlowTime's per-class projections show when the Express class's queue depth exceeds the threshold that makes same-day settlement impossible. The SLA breach is projected before it happens.

### Recovery planning

"After this incident, the backlog will take 4 hours to clear."

An incident has pushed queue depth to 500 items. The incident is resolved and capacity returns to normal. Feed the current queue depth as an initial condition. FlowTime projects the drain rate given current capacity and arrival patterns. The answer is precise: at capacity 80/bin and arrivals 60/bin, the net drain is 20/bin, and 500 items clear in 25 bins (25 hours if bins are hourly, 25 * 5 = 125 minutes if bins are 5-minute).

---

## References

- E-15 spec: `work/epics/E-15-telemetry-ingestion/spec.md` -- Telemetry Ingestion, Topology Inference, and Canonical Bundles
- E-17 spec: `work/epics/E-17-interactive-what-if-mode/spec.md` -- Interactive What-If Mode
- E-18 spec: `work/epics/E-18-headless-pipeline-and-optimization/spec.md` -- Headless Pipeline and Optimization
- E-10 spec: `work/epics/E-10-engine-correctness-and-analytics/spec.md` -- Engine Correctness and Analytical Primitives (Phase 3c: Variability)
- FlowTime overview (technical reference): `docs/flowtime.md`
- FlowTime overview (narrative version): `docs/flowtime-v2.md`
- Roadmap: `ROADMAP.md`
