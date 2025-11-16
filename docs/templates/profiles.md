# Template Time-of-Day Profiles

PMF nodes now accept an optional `profile` block that shapes their expected value across the day. Profiles are deterministic weight vectors applied to the PMF’s expected value, letting templates describe realistic diurnal rhythms (rush hours, night shifts, etc.) without relying on telemetry captures.

During `flowtime-sim generate`, each profiled PMF node is expanded into a concrete const series:

1. FlowTime.Sim computes the PMF’s mean `μ`.
2. The profile supplies per-bin weights `w[t]` (normalized so `AVG(w) = 1`).
3. The emitted const series becomes `value[t] = μ * w[t]`, and metadata marks that the node originated from a profiled PMF.

This keeps the runtime deterministic—FlowTime Engine only sees const nodes—and still preserves the original PMF in provenance metadata for inspectors.

## Schema

```yaml
nodes:
  - id: arrivals_north
    kind: pmf
    pmf:
      values: [8, 12, 16, 20, 26]
      probabilities: [0.1, 0.2, 0.3, 0.25, 0.15]
    profile:
      kind: builtin        # builtin | inline (defaults to builtin)
      name: hub-rush-hour  # required for builtin profiles
```

For inline profiles, provide a `weights` array with exactly `grid.bins` entries:

```yaml
profile:
  kind: inline
  weights: [0.4, 0.9, 1.2, 1.3, 1.1, 0.8, 0.5, 0.3]
```

Weights are normalized automatically so they average to `1.0`. Negative entries are rejected to keep the resulting const series non-negative when the PMF mean is positive.

## Built-in Library

FlowTime bundles a small set of reusable 24h×5 min profiles so authors do not have to define 288-element arrays by hand. Each profile is resampled to the template’s `grid.bins`.

| Name | Description |
|------|-------------|
| `weekday-office` | Dual-peak weekday schedule with morning ingress and evening egress surges plus quiet nights. |
| `three-shift` | Continuous manufacturing cadence with slightly stronger day shift and lighter overnight output. |
| `hub-rush-hour` | Transit or network hub with pronounced commuter peaks feeding central queues. |

When in doubt, start with the built-in that matches your domain and refine with inline weights only when you need a bespoke curve.

## Determinism & Metadata

- Profiles are deterministic—there is no per-bin RNG sampling. Runs remain reproducible for a given template + parameter set.
- Expanded const nodes include `metadata.origin.kind = pmf` and the profile identifier so inspectors can trace the provenance of synthetic arrivals/capacities.
- Telemetry overrides (`source: file://…`) still work. If telemetry is supplied at runtime, it takes precedence over the profiled const series just like other const nodes.

## Authoring Tips

1. **Keep PMFs focused on magnitude.** Profiles handle rhythm; let the PMF describe the expected distribution (min/max, tail behavior, etc.).
2. **Inline profiles require full-length arrays.** Use scripts or spreadsheets to generate 288 entries if you need a fully custom shape.
3. **Share patterns.** If you discover a curve that several templates could reuse, add a new built-in profile so catalog authors stay consistent.
