---
id: D-046
title: E-21 admits read-only run-adjacent model YAML endpoint under run-read surface
status: accepted
---

**Status:** active
**Context:** E-21 — Svelte Workbench & Analysis Surfaces declared `Backend API changes — Svelte UI is a pure consumer` in its epic spec (Scope → Out of Scope and Constraints). During M-040 (Sweep & Sensitivity Surfaces) implementation, the `/analysis` route needs to discover sweepable parameters (const nodes) from a selected run's model YAML. There is no existing endpoint that returns a run's model YAML. Alternatives considered:
1. Second Sim/artifact round-trip to reconstruct the model path — brittle, tightly couples the UI to artifact directory layout and requires multi-call orchestration for a trivial read.
2. Upload the YAML from the client — defeats the "pick a run, discover its params" UX and requires the user to keep the YAML synchronized with the stored run.
3. Server-side `/v1/runs/{runId}/params` endpoint — pushes YAML-parse logic into the server for no material gain over static-file serve.

The chosen fix is a tiny read-only endpoint that serves the already-persisted `model/model.yaml` for a given run — no compute, no new authoring surface, no new write path. It is directly analogous to the existing run-read surface (run manifest, series CSVs, provenance) and should be classified the same way.
**Decision:** Admit `GET /v1/runs/{runId}/model` into the supported first-party surface. Treat it as a **read-only run-adjacent endpoint**, not as a new functional feature. Revise the E-21 Scope and Constraints to permit read-only run-adjacent endpoints strictly necessary to consume existing run artifacts; authoring, write, and compute endpoints remain Out of Scope for E-21. This narrow widening applies only to E-21 and only to endpoints that serve artifacts already produced by the engine for a given run.
**Consequences:**
- E-21 epic spec Scope → Out of Scope and Constraints are updated to reflect the carve-out; future E-21 milestones may add similar read-only run-adjacent endpoints without needing a new decision, provided they strictly serve existing artifacts.
- Authoring, orchestration, sweep/optimize/goal-seek/validate compute endpoints, telemetry sinks, and any write-path additions remain out of scope for E-21 and would each need their own decision record.
- Client-side YAML parsing in the UI (`js-yaml` dependency added by M-040) is accepted as the default: adequate for typical model sizes; a promote-to-server `/v1/runs/{runId}/params` endpoint is possible future work if browser parsing proves fragile.
- The path-traversal finding in `work/gaps.md` ("Ultrareview findings on `epic/E-21-svelte-workbench-and-analysis` 2026-04-20", Finding 1) applies to this endpoint and to the four sibling endpoints on the same pattern. The shared `GetRunDirectorySafe` helper should cover all five call sites in one pass; this decision does not defer that fix.
