# Testing strategy

This repository separates tests by concern:

- Core unit and contract tests: `tests/FlowTime.Tests`
  - Scope: algorithms, graph, series, time grid, validation, determinism.
  - No web dependencies. Fast and deterministic.
- API slice tests: `tests/FlowTime.Api.Tests`
  - Scope: Minimal API endpoints, request/response shapes, happy and error paths.
  - Uses `WebApplicationFactory<Program>` from `Microsoft.AspNetCore.Mvc.Testing` to host the app in-memory.
- End-to-end (optional, future): `tests/FlowTime.E2E.Tests`
  - Scope: full system flows using the CLI or hosted API (potentially with Testcontainers).

## How to run

- All tests: use the workspace task "test" or run `dotnet test` from the repo root.
- Only core tests: run in `tests/FlowTime.Tests`.
- Only API tests: run in `tests/FlowTime.Api.Tests`.

## Guidelines

- Favor black-box tests at public boundaries. Keep internals flexible.
- Add a happy path and at least one boundary/negative case per feature.
- Keep API tests resilient by asserting on contract, not incidental formatting.
- When public behavior changes intentionally, update tests and document why in the PR.

### Upcoming (M1 / M1.5) Additions

Artifact & contract tests (Contracts Parity milestone M1):

* JSON Schema validation for `run.json`, `manifest.json`, `series/index.json`.
* Canonical model hashing: verify `spec.yaml` canonicalization (strip comments, normalize LF, trim trailing whitespace, collapse blank lines, key-order insensitive) produces stable `scenarioHash` / `modelHash`.
* Hash stability: reordering YAML keys or list of node definitions (where order is semantically irrelevant) must not change hashes.
* Series integrity: recompute each per-series hash from raw CSV bytes (LF newlines, invariant formatting) and match `manifest.json` values.
* Determinism: identical model + RNG seed ⇒ identical hashes & CSV byte sequences (excluding runId if non-deterministic timestamp segment is allowed; when `--deterministic-run-id` used runId must also match).
* RNG object: validate `rng.kind` and `rng.seed` presence and that varying seed changes scenarioHash only when model content identical.
* Negative: corrupt a CSV then run an integrity check expecting mismatch detection (future CLI command or test helper).

Expression tests (M1.5):
- Parser correctness (precedence, associativity, references).
- Built-ins (e.g., SHIFT) with boundary cases (shift beyond range → zeros or clamp as specified).

Backlog & latency tests (future M7):
- Conservation: cumulative(inflow) - cumulative(served) == backlog[last].
- Latency zero-division handling (served[t] == 0). Warning emission captured in manifest.

Synthetic adapter tests (SYN-M0):
- Round-trip: read artifacts → reconstruct series → byte-equal to originals.
- Hash agreement when adapter regenerates manifest.

## Notes

- The API project exposes `partial class Program` to enable `WebApplicationFactory` discovery.
- The `.http` file under `apis/FlowTime.API` contains example requests for manual checks.

### Artifact Contract References

Authoritative spec: [contracts.md](contracts.md)  
Schemas: [run](schemas/run.schema.json), [manifest](schemas/manifest.schema.json), [series index](schemas/series-index.schema.json)

Test essentials:
1. Validate JSON against schemas.
2. Recompute per-series hash from CSV bytes (LF) and match `manifest.json` `seriesHashes`.
3. Canonicalize `spec.yaml` and verify `scenarioHash`.