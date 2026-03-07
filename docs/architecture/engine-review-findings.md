# Engine & Schema Review Findings

> **Date:** 2026-03-07
> **Scope:** Engine code (FlowTime.Core, FlowTime.Sim.Core), JSON schemas (docs/schemas/), documentation claims
> **Excluded:** ai/ folder, UI code

## Summary

The core engine is solid. The execution model, RNG, expression system, and artifact pipeline all do what the documentation claims. The main risks are **documentation drift** (features evolved beyond what docs describe) and a few claims that overstate current implementation maturity.

---

## 1. Documentation Drift (High Priority)

### 1.1 Expression Language: 6 Undocumented Functions

`docs/architecture/expression-language-design.md` documents 4 functions for M-01.05: SHIFT, MIN, MAX, CLAMP. The code in `src/FlowTime.Core/Expressions/ExprNode.cs` actually implements **11 functions**:

| Function | Args | Status |
|----------|------|--------|
| SHIFT | 2 | Documented |
| CONV | 2 | Implemented, not in design doc |
| MIN | 2 | Documented |
| MAX | 2 | Documented |
| CLAMP | 3 | Documented |
| MOD | 2 | Implemented, listed as "candidate" in roadmap |
| FLOOR | 1 | Implemented, listed as "candidate" in roadmap |
| CEIL | 1 | Implemented, listed as "candidate" in roadmap |
| ROUND | 1 | Implemented, listed as "candidate" in roadmap |
| STEP | 2 | Implemented, listed as "candidate" in roadmap |
| PULSE | 1-3 | Implemented, listed as "candidate" in roadmap |

**Action:** Update `expression-language-design.md` to document all 11 functions. Update `expression-extensions-roadmap.md` to move MOD/FLOOR/CEIL/ROUND/STEP/PULSE from "candidate" to "shipped."

### 1.2 Dependency Constraints: Overstated as "Shipped"

The charter claims "Options A & B shipped" (M-01.07). In reality:

- `ConstraintAllocator.cs` is a 67-line proportional allocation utility
- It has **no callers in the evaluation pipeline**
- Constraints are loaded from topology but never enforced during graph evaluation

**Action:** Downgrade charter language from "shipped" to "foundations laid — enforcement pending (M-10.03)."

### 1.3 Time-Travel: Scope Clarification Needed

Documentation describes time-travel as "feature-complete" (TT-M-03.32.1). The `/state` and `/state_window` APIs work well, but they are **read-only queries over completed run artifacts**, not temporal debugging or mid-run replay.

**Action:** Clarify in charter that "time-travel" means run-artifact state queries, not temporal breakpoints or replay.

### 1.4 Catalog Architecture: Design vs Implementation Gap

`docs/architecture/catalog-architecture.md` describes a sophisticated system-catalog-as-SSOT pattern. The code has `Catalog` and `CatalogIO` classes, but these are simple YAML-backed data structures — not the authoritative engine-level concept described in the docs.

**Action:** Either label the catalog doc as aspirational/draft, or note which parts are implemented vs planned.

---

## 2. Code Quality (Medium Priority)

### 2.1 Graph.cs: Duplicate Topological Sort

`src/FlowTime.Core/Execution/Graph.cs` — `ValidateAcyclic()` runs Kahn's algorithm in the constructor (lines 17-49), then `TopologicalOrder()` runs the **exact same algorithm again** on every `Evaluate()` call (lines 81-106). Both use O(V^2) inner loops instead of an adjacency list.

**Fix:** Cache the topological order from constructor validation. Build a proper adjacency index.

### 2.2 Silent Division by Zero

`ExprNode.cs:70` — `right[i] != 0.0 ? left[i] / right[i] : 0.0` silently returns 0 on division by zero. In a deterministic modeling engine, silently masking errors is risky.

**Fix:** Emit a warning (consistent with InvariantAnalyzer pattern) or document the policy.

### 2.3 No Consistent NaN/Infinity Policy

- `ExprNode.cs` — division by zero returns 0.0
- `ServiceWithBufferNode.cs` — non-finite values silently converted to 0
- `ClassMetricsAggregator` — NaN returns null
- `CONV` in ExprNode — skips non-finite samples with `continue`

There is no documented or enforced policy for handling non-finite values.

**Fix:** Define a policy (warn + clamp, propagate NaN, or reject) and apply it consistently.

### 2.4 PCG32.NextInt Modulo Bias

`Pcg32.cs:99` — `NextUInt32() % range` introduces modulo bias when range doesn't evenly divide 2^32. This doesn't affect the primary use case (`NextDouble()` for PMF sampling), but `NextInt` is a public API.

**Fix:** Use rejection sampling (standard PCG approach) or mark `NextInt` as internal if bias is acceptable.

### 2.5 Formatting Issues in Graph.cs

Lines 21-27 and 84-87 have inconsistent indentation (some lines flush-left inside method bodies). Unused `using System.Collections.Concurrent` import on line 1.

**Fix:** Run formatter, remove unused import.

---

## 3. Schema Issues (Medium Priority)

### 3.1 Missing model.schema.yaml

`ModelSchemaValidator.cs` references a `model.schema.yaml` file, but this was not found in `docs/schemas/`. Validation may silently skip if the file is missing.

**Action:** Locate or create the missing schema file.

### 3.2 Meta-Schema Version Mismatch

`template.schema.json` uses JSON Schema 2020-12 while all other schemas use draft-07. Both work with JsonSchema.Net, but tooling inconsistencies are possible.

**Action:** Standardize on one meta-schema version across all schema files.

### 3.3 Duplicated Definitions Across Schemas

Grid, hash pattern (`sha256:[a-f0-9]{64}`), and seriesId pattern are independently defined in each schema file. No cross-schema `$ref` usage.

**Action:** Consider extracting shared `$defs` into a common definitions file. Low priority but reduces maintenance.

### 3.4 Mixed Warning Types in run.schema.json

The `warnings` field accepts both plain strings and structured objects (with code, message, severity, nodeId). This complicates consumer code.

**Action:** Consider deprecating plain-string warnings in a future schema version.

---

## 4. Testing Gaps (Medium Priority)

### 4.1 No End-to-End Determinism Test

The charter's headline claim is "same inputs → same outputs." Individual components are deterministic, but there's no test that runs the same model twice and asserts bitwise-identical artifacts.

**Action:** Add an integration test: same template + same seed → identical run.json + series CSVs.

### 4.2 No Tests for Newer Expression Functions

MOD, FLOOR, CEIL, ROUND, STEP, and PULSE lack dedicated test coverage. The parser and evaluator tests cover the original 4-5 functions well, but the newer additions appear untested or under-tested.

**Action:** Add unit tests for each new function, including edge cases (MOD by zero, PULSE with phase > period, STEP at exact threshold).

### 4.3 Router Convergence Unguarded

`RouterAwareGraphEvaluator` does iterative re-evaluation with overrides but has no visible convergence check or iteration limit. Pathological models could loop.

**Action:** Add a max-iteration guard with a descriptive error.

---

## 5. Verified Claims (No Action Needed)

These core claims are well-supported by the code:

- **Deterministic DAG evaluation** — Kahn's algorithm, topological sort, memoized evaluation
- **PCG-XSH-RR implementation** — Correct algorithm, state save/restore, warmup step
- **Expression language core** — Recursive descent parser, AST, series-based evaluation
- **ServiceWithBuffer queue semantics** — `Q[t] = max(0, Q[t-1] + inflow - outflow - loss)`
- **Schema validation at runtime** — JsonSchema.Net with 3 validators (manifest, template, model)
- **Provenance tracking** — Deterministic inputHash, embedded in artifacts, queryable via API
- **Post-evaluation invariant analysis** — Conservation checks, queue depth validation, edge flow analysis (~700 lines)
- **Run artifact structure** — model/, series/, run.json, manifest.json as documented

---

## Prioritized Action Items

| # | Priority | Category | Action |
|---|----------|----------|--------|
| 1 | High | Docs | Update expression design doc with all 11 implemented functions |
| 2 | High | Docs | Downgrade constraint claims from "shipped" to "foundations laid" |
| 3 | High | Docs | Clarify time-travel scope (artifact query, not temporal debugging) |
| 4 | Medium | Code | Cache topological order in Graph constructor |
| 5 | Medium | Code | Define and enforce NaN/Infinity/div-by-zero policy |
| 6 | Medium | Code | Fix Pcg32.NextInt modulo bias |
| 7 | Medium | Schema | Locate or create missing model.schema.yaml |
| 8 | Medium | Tests | Add end-to-end determinism integration test |
| 9 | Medium | Tests | Add tests for MOD/FLOOR/CEIL/ROUND/STEP/PULSE |
| 10 | Medium | Tests | Add router convergence guard |
| 11 | Low | Docs | Label catalog architecture doc as aspirational/draft |
| 12 | Low | Schema | Standardize meta-schema version (draft-07 vs 2020-12) |
| 13 | Low | Schema | Extract shared $defs into common definitions file |
| 14 | Low | Code | Fix Graph.cs formatting and unused import |
