# Tracking: m-E18-01 Parameterized Evaluation

**Milestone:** m-E18-01
**Epic:** E-18 Time Machine
**Status:** complete — merged to main 2026-04-10
**Branch:** `milestone/m-E18-01-parameterized-evaluation`
**Started:** 2026-04-10
**Completed:** 2026-04-10

## Progress

| AC | Description | Status |
|----|-------------|--------|
| AC-1 | ParamTable struct in Plan | pending |
| AC-2 | Compiler populates ParamTable | pending |
| AC-3 | evaluate_with_params function | pending |
| AC-4 | Equivalence (no overrides = defaults) | pending |
| AC-5 | Full post-eval pipeline with overrides | pending |
| AC-6 | Parameter override affects downstream | pending |
| AC-7 | Class arrival rate override | pending |
| AC-8 | WIP limit override | pending |
| AC-9 | Parameter schema extraction | pending |
| AC-10 | Compile-once eval-many pattern | pending |

## Implementation Phases

### Phase 1: ParamTable + evaluate_with_params (AC-1, AC-3, AC-4)
- Add ParamTable, ParamEntry, ParamValue, ParamKind to plan.rs
- Add evaluate_with_params to eval.rs
- Verify equivalence: no overrides = same as evaluate

### Phase 2: Compiler populates params (AC-2, AC-9)
- Register const nodes, arrival rates, WIP limits, initial conditions
- extract_params accessor

### Phase 3: Full pipeline + override propagation (AC-5, AC-6, AC-7, AC-8)
- eval_model_with_params entry point
- Override propagation through class decomposition, edges, analysis
- Class rate override + normalization invariant
- WIP limit override + overflow

### Phase 4: Compile-once eval-many (AC-10)
- Multi-eval independence test
- Performance verification (no recompile overhead)
