# TT‑M‑03.30.3 — PMF Time‑of‑Day Profiles

Status: ✅ Landed  
Owners: Platform (Sim + Templates), Architecture  
Depends on: TT‑M‑03.30.2 (queues/retries baseline)

---

## Why

Templates now rely on PMF nodes for demand/capacity, but PMFs currently emit a flat expected value in every bin. This yields “flat-line” synthetic telemetry even when the real domains have strong diurnal patterns (rush hours, maintenance windows, batch waves). We want richer yet deterministic curves without introducing telemetry captures or per-bin RNG sampling.

## Goals

1. **Profiles for every PMF node** – Allow template authors to attach a time-of-day profile (288×5 min horizon) that shapes the PMF’s expected value across the day. The profile captures the domain rhythm; the PMF keeps describing the magnitude distribution.
2. **Deterministic generation** – FlowTime.Sim expands the PMF+profile into a concrete const series during model generation, so runs remain reproducible and engine/runtime code stays unchanged.
3. **Profile library** – Ship a small catalog of reusable profiles (e.g., `weekday-office`, `three-shift-latam`, `hub-rush-hour`) that templates can reference by name instead of hand-authoring 288 numbers.
4. **Template updates** – Refresh every catalog template to reference the appropriate profile so arrival/throughput curves look realistic out of the box.

## Out of Scope

- Real-time stochastic sampling per bin (still future work).
- Telemetry ingestion changes.
- UI/engine alterations (all shaping happens at template generation time).

## High-Level Design

1. **Schema additions**
   - Extend `TemplateNode` with optional `profile` references:
     ```yaml
     nodes:
       - id: arrivals_north
         kind: pmf
         pmf: { values: [...], probabilities: [...] }
         profile:
           kind: builtin
           name: weekday-office
         # or inline:
         # profile:
         #   kind: inline
         #   weights: [ ... 288 doubles ... ]
     ```
   - `profile.kind` may be `builtin` (lookup table) or `inline`. Built-ins describe the 288-bin vector plus metadata.
2. **FlowTime.Sim processing**
   - During `SimModelBuilder`:
     - Compile the PMF to its expected value `μ`.
     - Resolve the profile weights `w[t]` (defaults to 1.0 for all bins if not provided).
     - Emit a concrete const node with `values[t] = μ * w[t]`. Attach provenance metadata so inspectors know it came from `pmf+profile`.
     - Optionally normalize weights so the average weight = 1.0 (keeping the PMF’s expected total consistent).
3. **Validation**
   - Ensure inline profiles supply exactly `grid.bins` weights.
   - Warn when weights produce negative values or blow up totals beyond template constraints.
4. **Template refresh**
   - Transportation: morning/evening rush profile.
   - Supply chain: warehouse three-shift profile.
   - IT/Manufacturing: “weekday office load” vs. “factory floor continuous”.
   - Network reliability: day/night variation.

## Deliverables

1. Schema + CLI/service/runtime changes enabling `profile` blocks.
2. Profile catalog documented in `docs/templates/profiles.md`.
3. Updated templates with realistic curves (reviewed per domain).
4. Tests for profile expansion (unit + snapshot of generated const series).
5. Milestone entry + release note summarizing the new capability.

## Follow-up

Once this lands, templates get realistic curves immediately, and we still have the option to introduce true stochastic sampling later without breaking determinism (profiles simply become the mean curve we sample around).

### Addendum — End-to-End Latency (in flight)

- We will derive flow latency server-side (no UI math) as: queue wait (Little’s Law latency per queue) + service time (processingTimeMsSum / servedCount) accumulated along the path. The API will expose `flowLatencyMs` in `/state` and `/state_window` so both simulation and real telemetry benefit. Missing inputs yield null plus info-level warnings. UI will render the series in topology/inspector and can surface a latency KPI on the dashboard.

### QA / Open Items (2025-11-17)

- 🔴 API goldens need refresh for new `flowLatencyMs`/bin metadata and orchestration responses (`create-run`, `create-simulation-run`, list). State schema must allow `flowLatencyMs`.
- 🔴 UI Topology inspector test still expects old metric stack (needs flow-latency aware ordering).
- 🟠 SLA dashboard: sparkline now rendered as SVG line; verify visibility across 288-bin windows once bundle rebuilt.
- 🟠 Flow latency focus: add a “Flow latency” focus chip to paint node coloring from `flowLatencyMs` when available; ensure legend reflects color basis.
- 🟠 Perf suite: `FlowTime.Tests.Performance.M2PerformanceTests.Test_PMF_Complexity_Scaling` remains flaky/skip.
- ✅ Topology legend: restore top-right legend for node color basis (passing/warning/breach swatches).
