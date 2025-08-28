# Testing Strategy

Focus: determinism, contract stability, and internal parity invariants.

## Layout
Tests live under `tests/FlowTime.Sim.Tests` (xUnit).

## Categories
- Spec validation: ensures schemaVersion, arrivals/service rules enforced.
- RNG determinism: `PcgRngSnapshotTests` locks first N samples (portable sequence guarantee).
- Manifest: hash stability + structure (`DeterminismTests.SimMode_MetadataManifest_HashesStable`).
- Internal parity (formerly external adapter parity Phase 5): `AdapterParityTests` run end-to-end simulation twice and assert:
	- Gold arrivals stable & reproducible.
	- Served == arrivals (current milestone identity) for all bins.
	- Aggregated events (NDJSON) per timestamp align with Gold counts (including zero bins).
	- Manifest basic properties (paths, hashes length, rng, seed).
	- Negative guard: deliberate mutation detected (ensures assertions meaningful).

Note: The prior cross-repo engine graph reconstruction was removed to decouple this repo from the FlowTime engine. Full adapter ↔ engine roundtrip tests will live in the future SYN-M0 adapter repository.

## Determinism Guarantees
Given identical spec (including `seed` and `rng`):
1. `events.ndjson` content hash stable (line-ending normalized).
2. `gold.csv` content hash stable.
3. Manifest hashes derived solely from (1)+(2) and metadata fields.

## Running
```bash
dotnet test
```

CI runs full suite on PRs; parity & manifest tests kept <2s total.

## Adding Tests
- When introducing new output fields, update manifest tests if hashes depend on them.
- For new stochastic processes, add snapshot + distribution sanity (mean/variance) tests while preserving determinism.

## Future (SIM-M2+)
- Service/backlog metrics parity.
- Performance micro-benchmarks with threshold assertions.
