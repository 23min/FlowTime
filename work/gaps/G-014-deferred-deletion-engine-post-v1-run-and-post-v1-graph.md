---
id: G-014
title: 'Deferred deletion: Engine `POST /v1/run` and `POST /v1/graph`'
status: open
---

### Why this is a gap

M-024 A5/matrix scheduled `POST /v1/run` and `POST /v1/graph` for deletion in M-025 on the premise that they are not used by first-party UIs. During M-025 implementation (2026-04-08), discovery showed these routes are the primary run-creation mechanism for the Engine test suite:

- `ProvenanceHashTests.cs` — 13 call sites
- `ProvenanceHeaderTests.cs` — 8 call sites
- `ProvenanceEmbeddedTests.cs` — 8 call sites
- `ProvenancePrecedenceTests.cs` — 7 call sites
- `ProvenanceStorageTests.cs` — 6 call sites
- `ProvenanceQueryTests.cs` — 2 call sites
- `Legacy/ApiIntegrationTests.cs` — 3 `/v1/run` + 2 `/v1/graph` call sites
- `ParityTests.cs` — 2 call sites
- `CliApiParityTests.cs` — 1 call site

Total: 50 `/v1/run` call sites and 2 `/v1/graph` call sites across 9 test files covering Engine-side runtime provenance, CLI ↔ API parity, and legacy integration. These are coverage-load-bearing for runtime provenance behaviour adjacent to the E-16 purity work.

### Status

Deletion deferred out of M-025. Recorded in `work/decisions.md` as D-042. Supported-surface matrix updated: row for these routes now reads `transitional` with owning milestone `deferred (see work/gaps.md)` instead of `delete m-E19-02`.

### Resolution path

Before deletion can proceed, the 50+ test call sites must migrate to an alternative run-creation path. Two plausible options:

1. **Test-only in-process adapter** over `Graph.Evaluate` or `RunOrchestrationService` that bypasses HTTP entirely. Cleanest option because it does not pull Sim infrastructure into Engine tests.
2. **Sim orchestration endpoint with template fixtures** (`POST /api/v1/orchestration/runs`). Requires each test to publish a template YAML and spin up Sim infrastructure; increases cross-surface coupling in Engine tests.

Once the migration lands, `POST /v1/run` and `POST /v1/graph` can be deleted outright along with the `Legacy/ApiIntegrationTests.cs` Legacy suite. A dedicated follow-up milestone (candidate: `m-E19-02a-engine-runtime-route-retirement` or similar) should own the migration. Not scheduled yet.

### Immediate implications

- Do not add new callers to `/v1/run` or `/v1/graph` on any surface.
- Do not add new Provenance tests that use the HTTP `/v1/run` path; favour a direct in-process path instead.
- Track the eventual deletion as an explicit unit of work rather than letting it float as a tolerated coexistence state.

---
