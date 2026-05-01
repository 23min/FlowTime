---
id: M-037
title: Artifact Sink Parity
status: done
parent: E-20
depends_on:
    - M-036
---

## Goal

The Rust artifact sink produces the full directory layout that `StateQueryService` can read. After this milestone, the C# `RunArtifactWriter` is no longer needed for Rust-evaluated runs. E-17 and E-18 are unblocked.

## Context

The Rust engine (after M-036) returns complete evaluation results: total series, per-class series, edge series, warnings, grid info, and metadata. The current `writer.rs` produces a minimal artifact set (bare series CSVs, simple index.json, run.json, manifest.json). The C# `RunArtifactWriter` produces a much richer layout that `StateQueryService` expects: `model/` directory, normalized `spec.yaml`, per-class CSV naming, full JSON schemas with class metadata, and provenance files.

Per D-044, the artifact sink is a separate layer from the engine core. It receives the model input and EvalResult, and persists them durably. This milestone builds the full sink as a Rust library used by both the CLI and the .NET bridge.

## Acceptance Criteria

1. **AC-1: `model/` directory.** The sink writes:
   - `model/model.yaml` — copy of the input model YAML
   - `model/metadata.json` — template metadata extracted from YAML provenance section and/or passed metadata: `{ schemaVersion, templateId, templateTitle, templateVersion, mode, modelHash, source, hasTelemetrySources, telemetrySources, nodeSources, parameters }`
   - `model/provenance.json` — written when provenance metadata is provided (pass-through). Omitted when absent (backward compatible).

2. **AC-2: `spec.yaml` at run root.** Normalized model YAML with topology semantics rewritten to `file://` URIs pointing to series CSV paths. This is what `StateQueryService` reads to resolve topology node bindings.

3. **AC-3: Series ID naming convention.** Series files use the format `{nodeId}@{COMPONENT_ID}@{CLASS_ID}.csv`:
   - Default (no class): `{nodeId}@{COMPONENT_ID}@DEFAULT.csv`
   - Per-class: `{nodeId}@{COMPONENT_ID}@{classId}.csv`
   - Edge: `edge_{edgeId}_{metric}@{COMPONENT_ID}@{classId}.csv`
   - Component IDs follow C# conventions: `ARRIVALS`, `SERVED`, `QUEUE`, `ERRORS`, etc.

4. **AC-4: Full `series/index.json` schema.** Each series entry includes:
   - `id`, `kind` (flow/stock/ratio/time), `path`, `unit`, `componentId`, `class`, `classKind` (fallback/specific), `points`, `hash`
   - `formats` section with aggregates table reference
   - `classes` array with declared class definitions
   - `classCoverage` field (full/partial/missing)

5. **AC-5: Full `run.json` schema.** Includes:
   - `schemaVersion`, `runId`, `engineVersion`, `source`, `inputHash`
   - `grid` (bins, binSize, binUnit, timezone, align)
   - `scenarioHash`, `modelHash`
   - `classesCoverage`
   - `warnings` array (nodeId, code, message, severity, bins)
   - `series` array (id, path, unit)
   - `classes` array (id, displayName, description)

6. **AC-6: Full `manifest.json` schema.** Extends existing to include:
   - `rng` section (kind, seed)
   - `provenance` section (hasProvenance, modelId, templateId, inputHash)
   - `classes` array
   - `seriesHashes` (per-series SHA256)
   - `createdUtc` timestamp

7. **AC-7: `aggregates/` directory.** Created as a placeholder (empty directory). Matches C# behavior.

8. **AC-8: Deterministic run ID.** When deterministic mode is requested, run ID is derived from `sha256(normalized_spec + seed + bias)` truncated to 16 hex chars. Matches C# `DeterministicRunNaming`.

9. **AC-9: StateQueryService integration test.** A C# integration test that:
   - Evaluates a class-enabled model through the Rust engine + sink
   - Loads the produced run directory via `StateQueryService.LoadContextAsync`
   - Verifies: topology resolved, per-class series loadable, provenance hash valid, warnings present
   - This is the definitive proof that the Rust sink is compatible.

10. **AC-10: Parity with C# artifact layout.** For a reference model, produce artifacts from both C# `RunArtifactWriter` and Rust sink. Compare directory structures and file contents. Document any intentional differences.

## Out of Scope

- Replacing `RunArtifactWriter` callers (that's wiring work for when the switch happens)
- Parquet aggregates (placeholder only — future work)
- Telemetry bundle building (stays in C# `TelemetryBundleBuilder`)
- Template orchestration (stays in C# `RunOrchestrationService`)
- Storage backend abstraction (filesystem only — S3/database is future)

## Key References

- `src/FlowTime.Core/Artifacts/RunArtifactWriter.cs` — C# reference (1,287 lines)
- `src/FlowTime.API/Services/StateQueryService.cs` — reads artifacts back (5,195 lines)
- `src/FlowTime.Core/Artifacts/DeterministicRunNaming.cs` — run ID generation
- `engine/core/src/writer.rs` — current minimal Rust writer
- D-044 — three-layer architecture (engine core / artifact sink / consumer adapters)
- D-043 — provenance strategy
