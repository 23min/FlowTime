---
id: M-059
title: Phase 3c — Variability Preservation (Cv + Kingman)
status: done
parent: E-10
---

## Goal

Preserve the coefficient of variation (Cv) when compiling PMFs, and use it to compute Kingman's approximation for predicted queue waiting time. This answers: "How variable is this stage?" and "What does queueing theory predict about queue behavior here?"

## Context

FlowTime uses PMFs (probability mass functions) to model stochastic arrivals and service times. The `PmfCompiler` reduces each PMF to a series of expected values `E[X]` — the distribution shape is discarded. This loses the **coefficient of variation** (`Cv = σ/μ`), which is arguably the most important parameter in queueing theory.

**Why Cv matters:** Kingman's formula shows that queue waiting time depends on BOTH utilization AND variability:
```
E[Wq] ≈ (ρ/(1-ρ)) × ((Ca² + Cs²)/2) × E[S]
```
Two nodes at identical utilization can have wildly different queue behavior if their variability differs. A node at 85% utilization with bursty arrivals (Cv=1.5) behaves worse than one at 95% with steady arrivals (Cv=0.3).

FlowTime is domain-agnostic and must support stochastic models across any domain. Variability preservation is essential for meaningful flow analysis.

## Acceptance Criteria

1. **AC-1: Cv computed from two sources.**
   - **PMF compilation:** When `PmfCompiler` compiles a PMF to a series, compute alongside `E[X]`:
     - `σ` (standard deviation) from the PMF distribution
     - `Cv = σ / μ` (coefficient of variation, where μ = E[X])
     - Store Cv per bin in a companion structure. When μ = 0, Cv = 0 (no variation around zero).
   - **Observed series statistics:** For non-PMF series (telemetry replay, constants, expressions), compute sample Cv from the series values over a configurable sliding window (default: full series). `σ_sample = std(values[window])`, `μ_sample = mean(values[window])`, `Cv = σ_sample / μ_sample`. Constant series produce Cv = 0 by construction. This enables Kingman's approximation for telemetry-driven models, not only synthetic/PMF models.

2. **AC-2: Cv accessible in evaluation context.** The Cv data is stored alongside the compiled series values so downstream analysis (Kingman, future DSL) can access it. A `CvMetadata` record or similar wraps `{ CoefficientOfVariation: double[], Source: Pmf | Observed | Constant }`. The source tag distinguishes PMF-derived Cv (exact, per-bin from distribution shape) from observed Cv (sample statistic, approximate) and constant Cv (zero by definition).

3. **AC-3: Kingman's approximation per node.** For ServiceWithBuffer nodes where Cv data is available for both arrivals and service, compute per-bin:
   ```
   E[Wq] ≈ (ρ/(1-ρ)) × ((Ca² + Cs²)/2) × E[S]
   ```
   Where:
   - ρ = utilization (served/capacity)
   - Ca = Cv of arrivals series
   - Cs = Cv of service series
   - E[S] = mean service time (serviceTimeMs)
   
   Exposed as `kingmanPredictedWaitMs` in state responses. Null when any input is unavailable or ρ >= 1.0 (formula diverges).

4. **AC-4: Tests and gate.** Tests cover: Cv computation from known PMFs (Cv=0 for deterministic, Cv=1 for exponential), Cv computation from observed series (known sample statistics), Cv source tagging (Pmf vs Observed vs Constant), Kingman approximation with known inputs (both PMF and observed Cv), graceful null when inputs missing, ρ >= 1.0 returns null. Full test suite green.

## Technical Notes

- **Cv from PMF:** For a discrete PMF `{(v₁, p₁), (v₂, p₂), ...}`:
  - `μ = Σ(vᵢ × pᵢ)`
  - `σ² = Σ(pᵢ × (vᵢ - μ)²)`
  - `Cv = σ / μ`
- **Per-bin Cv:** Each bin may have a different PMF (or the same PMF sampled differently). Cv is computed per-bin from the PMF used for that bin.
- **Constant/expression nodes:** Cv = 0 by construction (deterministic).
- **Telemetry-driven nodes:** Cv computed from observed series as sample statistics. This is essential for crystal ball / predictive projection use cases where telemetry replay is the primary input. See `docs/notes/predictive-systems-and-uncertainty.md` for the design rationale — without observed Cv, Kingman's formula produces zero-variance predictions for all real-data models.
- **Kingman divergence:** When ρ ≥ 1.0, the formula produces infinity (queue grows unbounded). Return null with a warning rather than infinity.
- **Kingman as diagnostic, not prediction:** The formula assumes steady-state M/G/1 queueing. Real systems are transient. Kingman's value is as a **diagnostic** — "queueing theory predicts this node should have X ms wait time; actual is Y ms; the delta suggests the model may be missing something."

## Out of Scope

- Variability-based bottleneck ranking (DSL epic).
- Multi-server Kingman (M/G/c) — start with single-server approximation.
- Preserving the full PMF distribution shape (only Cv is preserved).

## Dependencies

- Phase 1 complete ✅
- Phase 3a helpful (cycle time provides serviceTimeMs for Kingman) but not strictly required
