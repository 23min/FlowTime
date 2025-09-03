# Testing Strategy

Focus: determinism, artifact contract stability, and integrity (hash) invariants.

## Layout
Tests live under `tests/FlowTime.Sim.Tests` (xUnit).

## Categories
- Spec validation: ensures schemaVersion and arrivals/service rules enforced.
- RNG determinism: `PcgRngSnapshotTests` (if retained for historical reference) or equivalent ensures stable portable RNG sequence.
- Artifact writer & integrity: `ManifestWriterTests`, `SchemaValidationTests` (renamed responsibilities) verify:
  - Per-series CSV deterministic bytes & SHA-256 hashes for identical spec+seed.
  - Tampering with a series file triggers hash mismatch (integrity guard).
  - Dual-write `run.json` & `manifest.json` exist and share `schemaVersion: 1` (currently identical content by design).
  - `runId` matches documented pattern `sim_YYYY-MM-DDTHH-mm-ssZ_<8slug>`.
  - `series/index.json` enumerates all series; each entry has path, unit, hash, points count.
- Optional events file (if emitted) basic shape guard (line-delimited JSON); its absence is not a failure.
- Service integration (SIM-SVC-M2): minimal slice tests via WebApplicationFactory covering:
  - POST /sim/run -> run id & artifact pack existence
  - GET index & series endpoints (200 / 404 behavior)
  - POST /sim/overlay creates distinct run with shallow patch
  - CLI vs Service parity (per-series CSV hash equality for same spec+seed)

## Determinism Guarantees
Given identical spec (including seed & rng):
1. Every per-series CSV file’s bytes are identical (LF newlines, invariant culture formatting) ⇒ identical SHA-256 hashes (`sha256:<64 hex>`).
2. `series/index.json` and `manifest.json` content identical except for permissible ordering differences (currently stable ordering enforced).
3. `run.json` & `manifest.json` identical payloads in SIM-M2 (reserved for divergence later). Differences limited to timestamp & runId across separate executions.

## Removed / Deprecated Tests
Legacy tests referencing `metadata.json`, single `gold.csv`, or external FlowTime engine parity (`AdapterParityTests`, `LiveApiTests`, legacy determinism harnesses) were removed or stubbed to decouple this repository and reflect the new artifact contracts.

## Running
```bash
 dotnet test
```
Suites kept fast (<2s typical on devcontainer) to encourage frequent execution.

## Adding Tests
- New series kinds: add enumeration + unit assertions in index tests.
- New artifact JSON fields: add positive/negative schema assertions.
- Performance-sensitive generators: consider snapshotting first N outputs for stability.
- When introducing divergence between `run.json` and `manifest.json`, add an explicit test documenting the semantic difference.

## Future (Post SIM-M2)
// Service tests reintroduced in SIM-SVC-M2
- Streaming parity (stream reconstruction equals file snapshot).
- Backlog/queue depth & latency estimate semantics (schemaVersion 2) tests.
- Performance micro-benchmarks with threshold assertions.
