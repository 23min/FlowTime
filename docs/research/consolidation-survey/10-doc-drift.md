---
title: Documentation Drift — Consolidated Findings
status: as-of-2026-05-06
owner: synthesis
---

# Documentation drift

A consolidated catalog of every place the four parallel investigations found `docs/` (or `CLAUDE.md`, or schemas, or project naming) disagreeing with what the code actually does. Items are grouped by category and rated `MAJOR` (likely to mislead a reader into wrong architectural conclusions), `MODERATE` (technically wrong, easy to trip on), or `MINOR` (cosmetic, only confusing on close reading).

The list is the union of findings from the four phase-one agents (process+code-graph, engine+validation, pipeline+lifecycle, telemetry+rust). Each item cites the source(s) of truth and where the drift lives.

## Executive summary

The largest drift category by far is **schema-vs-writer drift on the canonical run artifacts** (`run.json`, `manifest.json`, `series-index.json`). The schemas under `docs/schemas/` declare `additionalProperties: false` but the writers emit fields not in the schema, and *no test validates writer output against schema*. The canonical artifact contract is effectively code-defined, with the schemas serving as historical record rather than active contract.

The second-largest category is **misleading project names** — `FlowTime.Adapters.Synthetic` is the run-artifact reader, not a synthetic-data generator; `FlowTime.Contracts` depends on `FlowTime.Core` and pulls SQLite; `FlowTime.TimeMachine` is the canonical home for run orchestration regardless of "Sim vs Engine" boundary.

The third category is **deployment / operations docs that reference files and contracts that don't exist** — `docs/guides/deployment.md` references Dockerfiles that aren't in the repo; the devcontainer port-forwards don't match running ports; environment variables like `FLOWTIME_API_BASEURL` are set with no consumer.

Below: 41 drift items, organized by category.

---

## Category 1 — Canonical artifact schemas vs. writer output `[MAJOR]`

This category is the most consequential because the artifacts are the persisted, externally-readable contract.

### 1.1 — `run.json` schema is incomplete vs. the writer
- **Source of truth (code):** `RunArtifactWriter.cs:317-340`
- **Drift location:** `docs/schemas/run.schema.json`
- **Detail:** Schema has `additionalProperties: false`. Writer emits `inputHash`, `modelHash`, `classCoverage`, `classes` — all unmodelled by the schema. A consumer validating run.json against the schema would falsely reject every real run artifact.
- **Severity:** MAJOR

### 1.2 — `manifest.json` schema misses `classes`
- **Source of truth (code):** `RunArtifactWriter.cs` (manifest emission)
- **Drift location:** `docs/schemas/manifest.schema.json`
- **Detail:** Schema has `additionalProperties: false` and does not define `classes`. Writer always emits `classes`.
- **Severity:** MODERATE

### 1.3 — `series-index.json` schema misses fields and has wrong `kind` enum
- **Source of truth (code):** writer in `RunArtifactWriter.cs`
- **Drift location:** `docs/schemas/series-index.schema.json`
- **Detail:** Schema requires `id, kind, path, unit, points, hash` per series. Writer also emits `componentId, class, classKind`. Schema `kind` enum lacks `"edge"`, which the writer uses for edge-flow series. Top-level `classes` and `classCoverage` are not in the schema.
- **Severity:** MODERATE

### 1.4 — `aggregatesTable.path` declares an aspirational filename
- **Source of truth (code):** `RunArtifactWriter.cs` plus the export endpoint
- **Drift location:** `series-index.json` always declares `aggregates/node_time_bin.parquet`
- **Detail:** When `POST /v1/runs/{id}/export` is called, the actual file is named `aggregates/export.parquet`. The path in the index is wrong unless the export uses the canonical filename, which it doesn't.
- **Severity:** MODERATE

### 1.5 — `eventCount` is hard-coded zero
- **Source of truth (code):** writer
- **Drift location:** `manifest.json` always reports `eventCount: 0`
- **Detail:** Vestigial from earlier streaming designs. Field still emitted, always zero, never updated.
- **Severity:** MINOR (cosmetic but signals dead schema field)

### 1.6 — Schema validation is not exercised anywhere
- **Source of truth (code):** *no test* under `tests/` validates writer output against the schemas in `docs/schemas/`
- **Implication:** The whole `docs/schemas/` directory is effectively *historical reference*. The "code is truth" rule applies absolutely. This is itself the most consequential drift fact in the repo.
- **Severity:** MAJOR

---

## Category 2 — Validation tier docs vs. validator behavior `[MAJOR]`

### 2.1 — Tier 1 is described as "no compile, no evaluation"
- **Source of truth (code):** `ModelSchemaValidator.cs` adjunct rules + AST walk
- **Drift location:** `src/FlowTime.TimeMachine/Validation/ValidationTier.cs:9-13`
- **Detail:** Tier 1 docstring says "cheap — no compile, no evaluation." In reality tier 1 walks expression ASTs (`ValidateExpressionNodeReferences`) and runs cycle detection on the `wipOverflow` graph (`ValidateWipOverflowAcyclic`). It is structurally heavier than the docstring claims.
- **Severity:** MODERATE

### 2.2 — Tier 2 is described as "catches expression parse failures"
- **Source of truth (code):** `TimeMachineValidator.ValidateCompile` does not invoke `ModelParser.ParseModel`
- **Drift location:** `src/FlowTime.TimeMachine/Validation/ValidationTier.cs:17`
- **Detail:** Tier 2 only runs `ModelCompiler.Compile`, which transforms but does not evaluate expressions. Expression parse failures only surface at tier 3.
- **Severity:** MAJOR — affects what callers can rely on

### 2.3 — `ValidationWarning` strips warning richness at the boundary
- **Source of truth (code):** `InvariantWarning` carries `(NodeId, Code, Message, Bins, Value, Severity, EdgeIds)` — `ValidationWarning` only carries `(NodeId, Code, Message)`
- **Drift location:** undocumented at the `TimeMachineValidator` boundary
- **Detail:** Tier-3 callers cannot distinguish `severity: "info"` from `severity: "warning"`, cannot see offending bin indices, cannot see worst-case numeric values, cannot see edge ids. The validation API is silently lossy.
- **Severity:** MAJOR — directly affects M-0069's "richer diagnostics" goal

### 2.4 — Tier 3 re-parses and re-compiles
- **Source of truth (code):** `TemplateInvariantAnalyzer.Analyze` re-parses YAML and re-runs compile even though tier 2 already did
- **Detail:** Free correctness loss masquerading as a perf nit — if tier 2 mutates the model and tier 3 re-derives it from the original YAML, they're not analyzing the same model.
- **Severity:** MODERATE

### 2.5 — Edge-flow conservation warnings are unreachable from validators
- **Source of truth (code):** `TemplateInvariantAnalyzer.Analyze` passes `edgeSeries=null` to `InvariantAnalyzer.Analyze` because it does not call `EdgeFlowMaterializer`
- **Drift location:** the validator chain (`POST /v1/validate`)
- **Detail:** `edge_flow_mismatch_outgoing` / `edge_flow_mismatch_incoming` / `edge_class_mismatch` / `edge_class_partial_coverage` warnings only fire in the artifact-write path (`RunArtifactWriter.cs`). They cannot be elicited from `POST /v1/validate?tier=analyse`. **This directly contradicts what a reader of the validation-tier docs would assume**, and matters for M-0069 because those are exactly the warnings M-0069 is supposed to extend.
- **Severity:** MAJOR

### 2.6 — Two `ValidationResult` types coexist
- **Source of truth (code):** `FlowTime.Core` defines one; `FlowTime.TimeMachine.Validation` defines another with same name in different namespace
- **Detail:** Easy to import the wrong one; static analysis won't flag it.
- **Severity:** MINOR

### 2.7 — Topology node `kind` accepts unrecognised strings
- **Source of truth (code):** `ModelCompiler.IsQueueLikeKind` recognises only `{serviceWithBuffer, queue, dlq}` and silently ignores anything else
- **Drift location:** `docs/schemas/model.schema.yaml:139-142` documents default as `"service"` with no enum constraint
- **Detail:** `kind: queueueue` parses, compiles, evaluates, and the engine just ignores the typo as if the topology node were a no-op `service`. No warning surfaces.
- **Severity:** MODERATE

---

## Category 3 — Service architecture vs. claimed boundaries `[MAJOR]`

### 3.1 — "Sim service and Engine API never communicate over HTTP"
- **Source of truth (code):** Both services register `RunOrchestrationService` directly (`src/FlowTime.API/Program.cs:112`, `src/FlowTime.Sim.Service/Program.cs:60`); both link `FlowTime.Sim.Core`, `FlowTime.Core`, `FlowTime.TimeMachine`.
- **Drift location:** `CLAUDE.md` and various architecture diagrams imply a Sim → Engine HTTP boundary
- **Detail:** Neither service calls the other over HTTP. They share the `data/runs/` filesystem as the only inter-service contract. The "two services" framing is a *deployment* split, not an *architecture* split.
- **Severity:** MAJOR — dominates redesign reasoning

### 3.2 — `FlowTime.API` references `FlowTime.Sim.Core`
- **Source of truth (code):** `src/FlowTime.API/FlowTime.API.csproj` ProjectReference
- **Drift location:** Naming + the implicit "Engine ≠ Sim" mental model
- **Detail:** Engine API embeds `TemplateService` in-process. The Engine API can serve template-related endpoints structurally (it just doesn't register the POST verb).
- **Severity:** MAJOR

### 3.3 — `FlowTime.TimeMachine` references `FlowTime.Sim.Core`
- **Source of truth (code):** `src/FlowTime.TimeMachine/FlowTime.TimeMachine.csproj:21`
- **Drift location:** Naming
- **Detail:** TimeMachine is conceptually engine-side analytics, but its csproj pulls in Sim.Core. Reflects the truth that orchestration crosses the boundary.
- **Severity:** MODERATE

### 3.4 — `FlowTime.Sim.Service` references `FlowTime.Sim.Cli`
- **Source of truth (code):** `src/FlowTime.Sim.Service/FlowTime.Sim.Service.csproj`
- **Detail:** A long-lived service depending on a CLI executable project is unusual layering. Likely vestigial — the CLI may host shared logic that should live in `FlowTime.Sim.Core`.
- **Severity:** MODERATE

### 3.5 — `FlowTime.Contracts` depends on `FlowTime.Core` and pulls SQLite
- **Source of truth (code):** `src/FlowTime.Contracts/FlowTime.Contracts.csproj`
- **Drift location:** Naming — "Contracts" implies a leaf project
- **Detail:** Contracts is supposed to be the shared-DTO leaf, but it's not a leaf and not minimal. Reusing it as a contract surface for downstream tools would pull in a heavy dependency tree.
- **Severity:** MODERATE

### 3.6 — Two `MapRunOrchestrationEndpoints` extension classes
- **Source of truth (code):** Sim's at `src/FlowTime.Sim.Service/Extensions/RunOrchestrationEndpointExtensions.cs:13`; Engine API's at `src/FlowTime.API/Endpoints/RunOrchestrationEndpoints.cs:14`
- **Detail:** Same name, different namespaces, different shapes (Sim registers POST creation; Engine registers GET-only). Easy to mistake which is in scope when reading code.
- **Severity:** MINOR

---

## Category 4 — Misleading project names `[MAJOR]`

### 4.1 — `FlowTime.Adapters.Synthetic` is the run-artifact reader, not a synthetic generator
- **Source of truth (code):** `src/FlowTime.Adapters.Synthetic/RunArtifactAdapter.cs`
- **Drift location:** Project name
- **Detail:** The name suggests "generates synthetic data." In reality this is the read-only artifact adapter — `RunManifest`, `SeriesIndex`, individual series — used by `FlowTime.API` (Engine) and tests to consume run directories. It is the canonical reader of what `RunArtifactWriter` produces.
- **Severity:** MAJOR — name actively misleads

### 4.2 — `FlowTime.Contracts` is not a leaf
- See 3.5

### 4.3 — Namespace collision: `FlowTime.Expressions` (project) vs. `FlowTime.Core.Expressions` (sub-namespace)
- **Source of truth (code):** `src/FlowTime.Expressions/` has `namespace FlowTime.Expressions`; `src/FlowTime.Core/Expressions/` has `namespace FlowTime.Core.Expressions`
- **Detail:** Different namespaces, easy to confuse which is the parser and which is the runtime AST.
- **Severity:** MODERATE

### 4.4 — `FlowTime.TimeMachine` is mostly orchestration, not "time machine"
- **Source of truth (code):** What's actually in the project: `RunOrchestrationService`, `TelemetryBundleBuilder`, `TimeMachineValidator`, sweep/sensitivity/goal-seek/optimize runners
- **Drift location:** Name suggests temporal scrubbing or replay; reality is run orchestration + analysis modes
- **Detail:** The "time machine" label suggests a UX concept the code doesn't directly implement. The project is named after the originating epic (E-0022) but the contents are general-purpose orchestration.
- **Severity:** MODERATE

---

## Category 5 — Code-vs-code drift (duplicate / parallel implementations) `[MODERATE]`

### 5.1 — `ParameterSubstitution.cs` is dead production code
- **Source of truth (code):** `src/FlowTime.Sim.Core/Templates/ParameterSubstitution.cs`
- **Detail:** Object-level substituter, parallel implementation to `TemplateService.SubstituteParameters` (the YAML-text-level production path). Only referenced by `tests/FlowTime.Sim.Tests/NodeBased/`. Production uses YAML-text-level exclusively.
- **Severity:** MODERATE — if production diverges from tests, tests pass while production breaks

### 5.2 — Two `modelId` values land on disk for the same run
- **Source of truth (code):** `SimModelBuilder.ComputeModelId` (sha256 of substituted YAML, full hex) writes one; `ProvenanceService.ComputeDeterministicHash` (`templateId + sorted params`, first 8 hex prefixed by `model_<ts>_`) writes another
- **Drift location:** `model/model.yaml`'s embedded `provenance.modelId` vs. `model/provenance.json`'s `modelId`
- **Detail:** Two different functions, two different semantics, both labeled `modelId`. Per the design (`m-E24-02`) they're meant to represent different things, but neither is documented at the consuming surfaces. Downstream consumers may confuse them.
- **Severity:** MODERATE

### 5.3 — `generatedAt` injected inside YAML produces non-determinism
- **Source of truth (code):** `SimModelBuilder.BuildProvenance` injects `DateTimeOffset.UtcNow` into `provenance.generatedAt` *inside the resolved YAML*, which then gets hashed into `provenance.modelId`
- **Detail:** Same template + same parameters produces different YAML-embedded `modelId` across re-generations. Run-id and `inputHash` are stable; the in-YAML `modelId` drifts. Either intentional with implicit semantics, or a bug.
- **Severity:** MODERATE — hard to debug if a downstream consumer hashes the YAML

### 5.4 — `TelemetryBundleBuilder.RewriteTelemetrySemanticsToSources` is implemented but never called
- **Source of truth (code):** `src/FlowTime.TimeMachine/Telemetry/TelemetryBundleBuilder.cs`
- **Detail:** Full implementation, no production caller. `NormalizeTelemetrySources` is the live path.
- **Severity:** MINOR (dead code, but signals design uncertainty)

### 5.5 — `ITelemetrySource` / `CanonicalBundleSource` / `FileCsvSource` exist with zero production callers
- **Source of truth (code):** types exist in `FlowTime.Core` (or adjacent); no production reference
- **Detail:** Scaffolded for E-0022 Fit. Their role today is zero. Reading the code, you'd expect them to be hooked up.
- **Severity:** MINOR

### 5.6 — `model.yaml` is written twice per run
- **Source of truth (code):** `RunArtifactWriter`
- **Detail:** Once at the run root as `spec.yaml`, once under `model/`. No inline comment explaining why. Likely legacy compatibility.
- **Severity:** MINOR

### 5.7 — Two CSV header conventions coexist
- **Source of truth (code):** capture: `bin_index,classId,value`; canonical run series: `t,value`
- **Drift location:** No unifying doc explains when each applies
- **Severity:** MODERATE — affects telemetry/runs interop

### 5.8 — `wipLimit` (scalar) and `wipLimitSeries` (reference) silently coexist
- **Source of truth (code):** `ServiceWithBufferNode` runtime prefers the series silently
- **Detail:** No validator flags the redundancy. An author providing both gets a value they didn't author dominate.
- **Severity:** MODERATE

---

## Category 6 — Aspirational / historical documentation `[MAJOR]`

### 6.1 — `docs/guides/deployment.md` references Dockerfiles that don't exist
- **Drift location:** `docs/guides/deployment.md` cites `src/FlowTime.API/Dockerfile` and `src/FlowTime.Sim.Service/Dockerfile`
- **Reality:** Neither file exists. No Dockerfiles, no Compose files, no Kubernetes manifests anywhere in the repo.
- **Severity:** MAJOR — operations doc is fictional

### 6.2 — Devcontainer port-forward 8080 doesn't match running ports
- **Drift location:** `.devcontainer/devcontainer.json:68-73` forwards port 8080 labeled "FlowTime API"
- **Reality:** The Engine API runs on 8081 (per launchSettings, tasks, code).
- **Severity:** MODERATE — dev experience confusion

### 6.3 — `FLOWTIME_API_BASEURL` is set but unused
- **Drift location:** `.devcontainer/devcontainer.json:65` sets `FLOWTIME_API_BASEURL=http://flowtime-api:8081`
- **Reality:** No DNS for `flowtime-api`; no consumer of this env var was found.
- **Severity:** MINOR

### 6.4 — `start-api` task uses PowerShell `$env:` syntax in a bash environment
- **Drift location:** `.vscode/tasks.json:75`
- **Reality:** The devcontainer runs bash. PowerShell-style env-var syntax fails silently or sets nothing.
- **Severity:** MINOR (likely already broken in practice)

### 6.5 — E-0015 (Telemetry Ingestion) status is `proposed`; no infrastructure
- **Drift location:** `docs/flowtime.md:333` claims PMFs are "interchangeable with telemetry"
- **Reality:** True only because telemetry-mode runs *pre-bake* CSVs into const-node values. There is no live telemetry feed mechanism. No Gold Builder, no `TelemetryLoader`, no Graph Builder, no dataset has been ingested.
- **Severity:** MAJOR — invites incorrect assumptions about what's possible today

### 6.6 — E-0022 (Time Machine) preconditioned on unscheduled epic
- **Drift location:** `work/epics/E-0022-*/epic.md` references "Telemetry Loop & Parity" epic with no E-number
- **Severity:** MINOR (planning issue, not architecture)

### 6.7 — Sim health endpoint advertises a stale `availableEndpoints` list
- **Drift location:** `src/FlowTime.Sim.Service/Program.cs:189-200`
- **Reality:** Omits `/api/v1/orchestration/runs`, `/api/v1/drafts/*`, `/api/v1/series/*`, `/api/v1/profiles/*`, `/api/v1/templates/{id}/source`.
- **Severity:** MINOR

### 6.8 — `CLAUDE.md` lists `src/FlowTime.UI.Tests` as a project location
- **Drift location:** `CLAUDE.md`
- **Reality:** Actual location is `tests/FlowTime.UI.Tests`.
- **Severity:** MINOR

---

## Category 7 — Test layout drift `[MODERATE]`

### 7.1 — Test mirror is broken in three ways
- **Source of truth (code):** Project layout under `tests/`
- **Drift location:** `CLAUDE.md` says "tests mirrors source"
- **Detail:** No `FlowTime.Contracts.Tests`. Sim core/cli/service collapsed into one `FlowTime.Sim.Tests`. Two non-mirroring test projects (`FlowTime.Tests`, `FlowTime.Integration.Tests`) exist with no source-side counterparts.
- **Severity:** MODERATE

### 7.2 — Test exclusions persist files
- **Drift location:** `tests/FlowTime.Tests/FlowTime.Tests.csproj:31-33` excludes `Legacy/**` and `ApiIntegrationTests.cs`; `src/FlowTime.Sim.Service/FlowTime.Sim.Service.csproj:38-44` `<Compile Remove>`s four legacy template-repository files
- **Detail:** Files persist on disk despite being build-excluded. Indicates incomplete cleanup, not architectural drift, but invites future readers to re-include them.
- **Severity:** MINOR

---

## Category 8 — Future-vs-implemented ambiguity `[MODERATE]`

These items aren't quite "drift" but blur the line between what's shipped and what's planned, in ways that affect a reader's mental model.

### 8.1 — `EvalResult.class_map` and `edge_map` exist in Rust; `G-0016` says class/edge gaps remain
- **Detail:** Internally computed; whether they're exposed in the artifact at parity is the actual gap. The G-0016 status text understates progress.
- **Severity:** MINOR

### 8.2 — Two writers in Rust core (`writer.rs`, `sink.rs`); which runs depends on context
- **Detail:** Minimal `writer.rs` and full `sink.rs` both exist. Selection depends on `--output` flag and whether the API uses `RustEngineRunner` or `SessionModelEvaluator`. Not documented.
- **Severity:** MINOR

### 8.3 — `chunk_step` referenced in design comments but not in actual session method list
- **Detail:** `docs/architecture/time-machine-analysis-modes.md` and `engine/cli/src/session.rs` design comments mention `chunk_step`, but the implemented session methods don't include it.
- **Severity:** MINOR

### 8.4 — Telemetry capture metric enum is too narrow
- **Detail:** Six values in the telemetry-manifest enum cannot round-trip retry/failures/processing-time-sum semantics that the topology model supports. Implies the capture format will need to change before telemetry round-trips fully.
- **Severity:** MODERATE

### 8.5 — Boolean parameters allowed by template schema but no template uses them
- **Detail:** Schema admits `type: boolean`. No shipped template authors a boolean param. Codepaths through `TemplateParameterFormatter` would render `true`/`false` but aren't exercised.
- **Severity:** MINOR

### 8.6 — Rust lowercases topology node ids; C# preserves casing
- **Detail:** Parity tests use case-insensitive matching as a workaround. Latent compatibility issue if the surfaces ever produce ids that differ only in case.
- **Severity:** MODERATE

---

## Category 9 — UI / CLI surface drift `[MINOR]`

### 9.1 — `FlowTime.UI` (Blazor) has zero project references
- **Detail:** Strictly HTTP-coupled. Architectural fact, but a reader expecting embedded UI logic would be surprised.
- **Severity:** MINOR

### 9.2 — Two concurrent UIs (Blazor + Svelte) coexist
- **Drift location:** `CLAUDE.md` says Svelte is the rewrite target
- **Reality:** Both are buildable, both have VS Code tasks, both speak HTTP to the services.
- **Severity:** MINOR (in-flight migration, not architectural drift per se)

### 9.3 — Svelte UI's production hosting model is undeclared
- **Detail:** `svelte.config.js` uses `@sveltejs/adapter-auto` with no production target pinned.
- **Severity:** MINOR (deployment-time concern)

---

## Drift summary

41 items total. **Severity distribution:**

| Severity | Count | Categories |
|---|---|---|
| MAJOR | 8 | 1.1, 1.6, 2.2, 2.3, 2.5, 3.1, 3.2, 4.1, 6.1, 6.5 |
| MODERATE | 20 | various |
| MINOR | 13 | various |

**Highest-priority drift to address (in any future cleanup):**

1. **Fix or retire `docs/schemas/*.schema.json`** (Category 1). Either bring schemas current and validate writer output against them, or move them to `docs/archive/` and label them historical. The current state — schemas exist, writer ignores them, no test enforces — is worse than either fix.
2. **Document the Sim/Engine boundary truthfully** (3.1). Update `CLAUDE.md` and any architecture diagrams to say: "Sim.Service is the *creation* surface (template+params → resolved model), Engine API is the *evaluation read* surface (resolved model → run artifacts), they share libraries and a filesystem, they do not call each other over HTTP."
3. **Fix or remove `docs/guides/deployment.md`** (6.1). It describes a deployment that doesn't exist.
4. **Decide what `ValidationWarning` should carry across the tier boundary** (2.3) — the current shape silently drops half the diagnostic. M-0069 will need this fixed before its `val-warn` gate is meaningful.
5. **Rename `FlowTime.Adapters.Synthetic`** (4.1). The wrong name is everywhere.
6. **Make tier-3 reach the edge-flow conservation warnings** (2.5). The validator chain that's supposed to enforce ADR-0001 doesn't actually emit the relevant warnings today.
