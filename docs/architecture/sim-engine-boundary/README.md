# Epic: SIM/Engine Boundary Purification

## Motivation

FlowTime currently hosts template orchestration (parameter substitution, analyzer/bundle execution, telemetry capture prep) inside the main Engine API process. The “Run” endpoint in `FlowTime.API` instantiates `RunOrchestrationService`, which in turn uses `TemplateService`/`TemplateParser` to expand templates and generate canonical models before execution. While this enables simple UI flows (“template ID in, run out”), it tightly couples template-compilation code with the execution host. Any change to SIM-layer logic (TemplateParser, analyzers, ServiceWithBuffer synthesis, telemetry bundling) requires redeploying the Engine API, even though the engine’s responsibility should be “consume canonical model and execute.”

The ideal mental model—*SIM produces, engine executes*—is currently diluted. This epic documents work needed to re-establish that separation, reduce deployment blast radius, and make template authoring/runtime surfaces purer.

## Goals

1. **Isolate template compilation/orchestration.** Engine-facing services should only accept canonical models (plus manifests), while SIM/Orchestration services handle template selection, parameter binding, analyzer/bundle generation, and artifact creation.
2. **Introduce an explicit orchestration boundary.** Provide a clear interface (e.g., `POST /orchestration/runs` in the SIM tier) that accepts template IDs/parameters and produces canonical run packages that can be submitted to the engine.
3. **Reduce engine dependencies.** Remove TemplateParser/TemplateService/TelemetryBundleBuilder references from the Engine API, limiting it to model loading (ModelParser), execution, and state query endpoints.
4. **Preserve UX workflows.** The UI and CLI should still offer “pick template and run” flows, but those flows should call the orchestration endpoint rather than coupling UI directly to Engine internals.

## Non-Goals

- Eliminating template support entirely from the backend. We still want server-side parameterization/analyzer enforcement; we just want it outside the Engine runtime.
- Changing the canonical model format or the engine execution pipeline (ModelParser → DAG execution).

## Workstreams

### 1. Orchestration Service Extraction
- Define a dedicated SIM/Orchestration service (could be `FlowTime.Sim.Service` or a new host) responsible for:
  - Template catalog APIs (already present).
  - Parameter validation/dry-run planning.
  - Template expansion to canonical model(s).
  - Telemetry bundle generation, analyzer execution, RNG metadata capture.
  - Run artifact packaging (model.yaml, run.json, manifests) ready for engine ingestion.
- Introduce a stable artifact format (e.g., zipped run package or direct file system layout) that the engine can consume without re-running template logic.
- Ensure the UI/CLI call this orchestration endpoint when the user selects “Plan/Run” with a template ID.

### 2. Engine API Simplification
- Remove references to `TemplateService`, `TemplateParser`, `TelemetryBundleBuilder`, etc., from `FlowTime.API`.
- Update `/v1/runs` (engine) to accept only canonical models or pre-packaged run bundles, rejecting template IDs/parameter sets.
- Provide health endpoints/metadata to confirm engine nodes now load models directly without template compilation.

### 3. Deployment & Configuration Updates
- Update start scripts/tasks so `start-api` (engine) and `start-sim-api` (or equivalent orchestration host) are clearly distinguished; document the required sequence when running locally.
- Ensure “Refresh templates” buttons in UI target the orchestration service, and “Run model” persists run metadata via orchestration before handing off to engine.
- Revisit CLI commands (e.g., `flow-sim generate`) to optionally call the orchestration endpoint rather than duplicating functionality—this keeps CLI, UI, and automation consistent.

### 4. UX/Workflow Validation
- Confirm the UI still supports template discovery, dry runs, and full runs after redirecting to the orchestration service.
- Validate telemetry workflows (capture bundles, analyzer warnings) continue to surface in Run summaries even though engine no longer owns that logic.

## Benefits

- **Clean separation of concerns:** Engine deployments become smaller and only need redeployments when execution logic changes, not template compilers.
- **Reduced blast radius:** Template/schema changes only require pushing the SIM/orchestration tier; running engines stay untouched.
- **Simplified mental model:** Aligns with “SIM produces, engine executes,” making documentation and onboarding clearer.
- **Future flexibility:** Allows independent scaling/hosting of orchestration services (e.g., template-heavy environments) without taxing the engine runtime.

## Risks & Considerations

- **Migration complexity:** Existing `/v1/runs` clients expect to POST template IDs. We must provide migration paths or compatibility shims.
- **Latency:** Splitting orchestration/engine may introduce additional hops (UI → orchestration → engine). Need to ensure orchestration either deploys side-by-side or exposes artifact locations accessible to the engine without large network transfers.
- **Data consistency:** Orchestration must atomically create run artifacts before the engine ingests them. Failure handling (partial packages, temp directories) has to be robust.
- **Testing coverage:** Both layers need dedicated test suites. Ensure integration tests still cover end-to-end flows (template → run → state queries).
- **Operational awareness:** Monitoring/metrics should reflect the new topology so operators know whether failures stem from orchestration (template expansion) or engine execution.

## Next Steps

1. Draft a more detailed design (API contracts, artifact layout, deployment architecture) and review with stakeholders.
2. Create milestones to implement orchestration extraction, engine API simplification, and UI/CLI adjustments.
3. Plan migration strategy (feature flags, staged rollouts) so users can transition smoothly from current combined behavior to the purified separation.
