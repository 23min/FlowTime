---
id: D-044
title: Rust engine architecture — evaluation vs. artifact sink vs. consumer adapters
status: accepted
---

**Status:** active
**Context:** The Rust matrix engine (E-20) currently produces a minimal artifact set (series CSVs, index.json, run.json, manifest.json) via its `writer.rs`. The C# `RunArtifactWriter` (1,287 lines) produces a much richer layout (model/, series/ with per-class CSVs, spec.yaml, provenance.json, metadata.json, aggregates/) but conflates evaluation, class decomposition, invariant analysis, topology normalization, and file writing into one call. For E-17/E-18 (interactive what-if, headless pipelines, parameter sweeps, streaming), the engine must work as a pure function called many times without mandatory file I/O on each call. But artifacts are not optional — they provide durability (crash recovery), audit (what ran, when, with what), and reproducibility (replay from stored input).
**Decision:** Three-layer architecture with a structured boundary between each:

1. **Engine core (Rust, pure function).** `eval(model, params) → EvalResult`. Owns: compilation, evaluation, invariant analysis, class decomposition, edge series materialization. No side effects. Deterministic and reproducible. The `EvalResult` struct is the complete output: series values, per-class series, edge series, warnings, grid info, column map, plan metadata.

2. **Artifact sink (Rust library, mandatory but pluggable).** `sink.write(model_input, eval_result) → ()`. Owns: directory layout, file hashing, manifest stamping, provenance attachment, atomic writes. Guarantees: crash-safe (each result persisted before next eval starts in pipeline mode), auditable, replayable. Backend is pluggable (filesystem now, S3/database future) but the sink itself is not optional in production — it is the durability guarantee. The sink receives the model input and the EvalResult; it never calls the engine.

3. **Consumer adapters (Rust or C#, per surface).** Read from artifacts or from in-flight EvalResult. Own: JSON API responses, websocket push, CSV export, pipeline array formatting, UI state projection. Adapters are optional and surface-specific.

The existing `writer.rs` in the Rust CLI is the first artifact sink implementation (filesystem backend, CLI-scoped). The C# `RunArtifactWriter` is the current production sink (richer schema, provenance, classes) but will be replaced once the Rust engine core returns complete EvalResults including per-class and edge series.

**Consequences:**
- Rust engine core gaps (per-class decomposition, edge series, `outputs:` filtering) are evaluation-layer work and must land before E-17/E-18.
- Artifact layout gaps (model/ dir, spec.yaml, metadata.json, provenance.json, per-class CSV naming, full index.json schema) are sink-layer work. The sink can be built incrementally as the engine core returns richer EvalResults.
- The C# `RunArtifactWriter` remains the production sink until the Rust sink reaches parity. No premature deletion.
- For E-18 pipeline mode: `compile(model) → Plan; for params in sweep: eval(plan, params) → EvalResult; sink.write(...); yield to consumer`. The sink writes each result as it arrives — crash at iteration N means N-1 results are persisted. The sink and consumer run in parallel on the same EvalResult.
- `StateQueryService` reads artifacts produced by whichever sink wrote them. The directory layout contract is shared between sinks and consumers, not owned by the engine core.
