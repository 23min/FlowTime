# Tracking: m-E17-04 Warnings Surface

**Milestone:** m-E17-04
**Epic:** E-17 Interactive What-If Mode
**Status:** complete
**Branch:** `milestone/m-E17-04-warnings-surface`
**Started:** 2026-04-10
**Completed:** 2026-04-11

## Pre-flight verification

Verified before implementation: the proposed `capacity-constrained` model produces a `served_exceeds_capacity` warning at default values, and the warning clears when capacity is raised to 20:

```
validate (arrivals=15, capacity=10) → 1 warning: Service · served_exceeds_capacity
validate (arrivals=15, capacity=20) → no warnings
```

The demo arc works end-to-end at the engine level.

## Progress

| AC | Description | Status |
|----|-------------|--------|
| AC-1 | Warnings in compile response | done |
| AC-2 | Warnings in eval response | done |
| AC-3 | Capacity-constrained example model | done |
| AC-4 | Warnings banner | done |
| AC-5 | Warning details panel | done |
| AC-6 | Node badge on topology graph | done |
| AC-7 | Reactive updates on eval | done |
| AC-8 | Pure unit tests (warnings.ts) | done |
| AC-9 | Playwright E2E | done |
| AC-10 | Compile error surface unchanged | done |

## Test Results

- **Rust**: 17/17 session integration tests pass (3 new: `session_compile_returns_warnings`, `session_eval_warnings_clear_and_return_on_parameter_tweak`, `session_simple_model_has_empty_warnings_array`)
- **Vitest**: 173/173 pass (19 new warnings.test.ts tests + example-models updated for 4-model list)
- **Playwright**: 19/19 pass (5 new warnings E2E tests)

## Implementation Phases

### Phase 1: Protocol + session handler (AC-1, AC-2, AC-10)
- engine/cli/src/protocol.rs: WarningMsg type, add `warnings` to CompileResult + EvalResultMsg
- engine/cli/src/session.rs: convert EvalResult.warnings → WarningMsg in handle_compile, handle_eval
- Rust integration test verifying warnings flow through the session subprocess

### Phase 2: Svelte types + pure helpers (AC-8)
- ui/src/lib/api/engine-session.ts: WarningInfo type, update CompileResult / EvalResult
- ui/src/lib/api/warnings.ts: groupWarningsByNode, nodeHasWarning, severityClass
- ui/src/lib/api/warnings.test.ts: 8+ pure-function tests
- Update existing engine-session.test.ts mocks to include warnings: []

### Phase 3: Example model (AC-3)
- ui/src/lib/api/example-models.ts: add CAPACITY_CONSTRAINED
- Update EXAMPLE_MODELS array
- Update example-models.test.ts

### Phase 4: What-If page UI (AC-4, AC-5, AC-6, AC-7)
- Warnings banner component (inline in page for now)
- Warnings panel (list grouped by node)
- Node badges: pragmatic SVG overlay approach
- Wire `warnings` state, ensure reactivity

### Phase 5: Playwright E2E (AC-9)
- New spec: load capacity-constrained → warning visible
- Tweak capacity up → warning clears
- Tweak back down → warning returns
- Simple-pipeline → no warning banner (regression check)
