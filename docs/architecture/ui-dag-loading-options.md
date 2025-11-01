# UI DAG Loading Options

**Audience:** FlowTime UI + API maintainers  
**Status:** Exploratory design note (UI‑M‑03.22.2 context)  
**Last updated:** 2025‑04‑07  

---

## 1. Current Data Flow (Selective Window Loading)

Today the UI pulls topology + state in slices:

- `/v1/runs/{id}/graph` returns the nodes and edges the canvas needs. Optional query flags (`mode=full`, dependency fields) allow callers to include compute nodes, but only metadata travels.
- `/v1/runs/{id}/state_window?startBin=X&endBin=Y` streams a bounded window of timeseries data. The response currently contains operational nodes (service/queue) only. The UI adds per-node sparklines by slicing those series client-side.
- Any additional demand (e.g., expression node previews) requires the API to enrich the window payload; the client never recomputes expressions locally.

**Design intent:** keep payloads bounded (≤ 500 bins) and authoritative, shift expensive derivations to the service, and load only what the viewport requires.

---

## 2. “Ship the Entire DAG” Concept

The early idea was to serialize the entire computed DAG (all nodes + every timeseries) and hand it to the UI so it can render anything offline. Concretely this would mean:

- A new artifact: e.g., `runs/<id>/dag.bundle` (json/parquet) containing *every* node, its semantics, and fully resolved timeseries across the run window.
- UI bootstrap flow: download the bundle once, hydrate client-side stores, render topology/inspector/analytics without additional round-trips.
- Client-side evaluation: optional ability to replay expressions/const nodes locally for experimentation.

---

## 3. Assessment

### 3.1 Advantages

| Benefit | Detail |
| --- | --- |
| **UX Flexibility** | UI can experiment with new visualizations (multi-series overlays, time scrubbing, expression “what-if”) without touching the API. |
| **Offline / latency** | Once downloaded, navigation between views is instantaneous; no additional API hops for hover events or inspector panels. |
| **Debugging & tooling** | Developers/data scientists can poke at the full DAG in the browser console, build ad-hoc overlays, or export directly. |
| **POC velocity** | Rapid prototyping possible by mutating client-only views while backend evolves more slowly. |

### 3.2 Drawbacks

| Risk / Cost | Impact |
| --- | --- |
| **Payload size** | Expression-heavy runs with hundreds of nodes × ≥500 bins easily exceed tens of MB; pushes UX into streaming/worker territory. |
| **Duplication of semantics** | Engine already evaluates expressions; mirroring that evaluator (and keeping it bug-for-bug compatible) in JS is high risk. |
| **Security / governance** | Some derived data may be sensitive; shipping “everything” to the browser removes server-side controls. |
| **Incremental updates** | Any new series requires regenerating the giant artifact; no cheap partial refreshes. |
| **Memory / perf** | Persisting the entire DAG in browser memory (plus derived caches for sparklines, inspector, etc.) increases footprint and GC churn. |
| **Operational complexity** | Need artifact versioning, CDN caching, and UI fallbacks for divergent schema versions. |

### 3.3 “Simultaneous” Mode?

We could theoretically support both:

1. **Selective mode (default):** existing `/graph` + `/state_window` behavior.
2. **Bulk DAG mode (experimental):** new endpoint or artifact, gated by feature flag. UI chooses mode at runtime (e.g., query parameter, local storage toggle).

However, running both paths in production implies:

- API must own artifact creation (probably powered by FlowTime Engine) and lifecycle management.
- UI needs abstraction layers so inspectors/overlays can source data either from window slices or from the in-memory DAG bundle without duplicating business logic.
- Test matrix doubles: every new viz must work in both selective and bulk mode.
- Telemetry/analytics must clearly identify which mode a session used, since performance footprints differ.

---

## 4. Recommendation (April 2025)

1. **Short-term:** Fix the selective pipeline first (ensure expression nodes + computed values appear in `/state_window`). That unlocks the current milestone without architectural churn.
2. **Medium-term spike:** If we want to explore DAG bundles, prototype it as a *developer-only* endpoint:
   - API: expose `/v1/runs/{id}/dag` that zips `series/index.json`, the CSVs, and the flow graph metadata. No new evaluator—just a convenient bundle.
   - UI: add a developer switch that, when enabled, downloads the bundle once and hydrates the same stores used by the selective loader.
   - Measure bundle sizes, download times, and memory impact on representative runs before committing.
3. **Decision gate:** Only promote dual-mode UX if the spike shows compelling wins (e.g., huge latency savings or required offline workflows). Otherwise, keep the API-driven selective mode as the primary path and reserve DAG dumps for tooling/export scenarios.

---

## 5. Follow-up Questions

- Do we need per-user access control for derived expression series? If yes, bulk DAG mode would demand additional filtering/encryption.
- Should the engine emit partially aggregated DAG snapshots (e.g., block compressed) instead of CSV collections to mitigate size issues?
- How would telemetry/observability work for client-side expression evaluation (if ever introduced)? We’d need reproducibility guarantees.

Until those are addressed, treat DAG-in-the-UI as an experimental lever while we continue investing in the selective window pipeline. This keeps the production UI lean, performant, and easier to reason about, while still leaving room for power-user tooling if we discover strong use cases.

---

## 6. WebAssembly “Shared Engine” Concept

### 6.1 Idea

Compile the existing FlowTime expression/computation engine (C#) to WebAssembly using `dotnet wasm` or native-AOT, and ship that module to the browser. Both server and client would execute the exact same bytecode for expression evaluation, eliminating drift. The UI could:

- Download a DAG bundle containing raw inputs (base series) only.
- Use the WASM engine to recompute expression/const/pmf nodes on demand.
- Reuse identical validation rules and error handling in both environments.

### 6.2 Feasibility Snapshot

| Dimension | Notes |
| --- | --- |
| **Technical** | The core engine is .NET; WASM compilation is feasible. We would need to isolate dependencies (e.g., file I/O, logging) that are not browser-friendly and ensure deterministic floating-point code paths. |
| **Packaging** | Bundle size would include the WASM runtime (~1–2 MB for trimmed builds) + engine assemblies. With gzip/brotli we can likely stay under ~3 MB, but that is still a significant first-load hit. |
| **Execution** | WASM can crunch expression nodes quickly but will still be bound by JavaScript→WASM marshaling. For 500-bin × 100-node runs this is acceptable; for 10k bins we must benchmark. |
| **Parity** | Running the same IL reduces divergence risk, but we still need version pinning (UI must download engine vX whenever backend runs vX) and fallback when the module fails to load. |
| **Security** | Shipping the evaluator to the client means all inputs needed to compute expressions must also be sent. Anything sensitive should be blocked or anonymized first. |

### 6.3 When to Use It

- **Good fit** for exploratory tooling, “what-if” workflows, or offline dashboards where users tweak expression definitions locally.
- **Less compelling** for the standard UI path: the selective API already performs computations server-side. WASM would shift CPU cost to the browser without reducing network costs if we still ship every underlying series.

---

## 7. Streaming & Mutation Considerations

### 7.1 Streaming Bundles

Whether we pick selective or bulk mode, large runs benefit from streaming:

- **API-side:** stream gzip’d JSON/Parquet chunks via HTTP chunked encoding, or expose line-delimited JSON. The UI can progressively hydrate stores while data arrives.
- **UI-side:** use `ReadableStream` (Fetch) and incremental parsers (e.g., `ndjson`, binary chunk decoders) to render the canvas progressively, showing skeleton states while the remainder loads.
- **Backpressure:** WebAssembly evaluators must tolerate chunked arrival—compute only when the required base series are available.

### 7.2 DAG Mutation (Client-driven)

If we allow users to “edit” the DAG in-browser (e.g., modify an expression, insert a node):

- With the WASM engine, we can recompute the affected subgraph locally by re-evaluating dependencies starting from the edited node. We need incremental invalidation: recompute descendants only.
- For the selective API path, we would need a mutation endpoint (`POST /dag/hypothesize`) that returns updated series for the affected nodes. Otherwise the UI has no way to persist the changes.
- **State separation:** we should mark mutated graphs as ephemeral; avoid confusing users by blending hypothetical results with canonical run data.
- **Sync story:** if the user decides to “commit” a change, that must flow back to FlowTime Engine / model definitions. Otherwise the mutation remains a sandbox artifact.

### 7.3 Operational Guardrails

- Capture telemetry for mutate/recompute operations (either local or server) so we can measure cost.
- Add feature gates—bulk/WASM/mutation should stay behind opt-in flags until performance and UX are proven.

---

**Summary:** A WebAssembly-in-browser evaluator is technically achievable and would give us true code sharing between backend and UI, but comes with non-trivial payload and dependency management. It makes the most sense coupled with bulk DAG bundles and heavy experimentation features (mutation, what-if). For the primary production experience, we should continue leaning on the server pipeline—possibly with streaming—while we explore the WASM route in parallel as a targeted PoC.
