# m-E18-13 — SessionModelEvaluator

**Epic:** E-18 Time Machine
**Branch:** `milestone/m-E18-13-session-model-evaluator` (from `epic/E-18-time-machine`)
**Status:** complete — merged to epic branch 2026-04-15

## Goal

Replace per-point subprocess compile overhead with a single persistent engine session.
Today, every `IModelEvaluator.EvaluateAsync` call spawns `flowtime-engine eval` as a fresh
subprocess that re-parses YAML and re-compiles the Plan. For a sweep of 200 points this is
200 compiles; for an optimization run it can be 100–1000 compiles. Each spawn is
~100–500 ms of pure compile overhead.

`SessionModelEvaluator` uses the m-E18-02 session protocol (MessagePack over stdin/stdout):
compile once on the first call, then send `eval` with parameter overrides for every
subsequent call. The expected speedup for large batches is ~10–50×.

Also makes model fitting practical — fitting typically runs 100–1000 evaluations with
the optimizer as the inner loop, which is not viable with per-point subprocess compile.

## Scope

**Namespace:** `FlowTime.TimeMachine.Sweep`

- `SessionModelEvaluator : IModelEvaluator, IAsyncDisposable` — persistent session bridge:
  - Lazy-spawns `flowtime-engine session` subprocess on first `EvaluateAsync`
  - First call: sends `compile` request with the (already-patched) YAML; captures the list
    of parameter IDs from the response; returns the series from the compile result
  - Subsequent calls: uses `ConstNodeReader` to read the current value of each captured
    parameter ID from the patched YAML; sends `eval { overrides: { ... } }`; returns series
    from the response
  - Serializes protocol I/O with `SemaphoreSlim` (one request at a time per instance)
  - MessagePack via the `MessagePack` package (already used in integration tests); encodes
    requests with `ContractlessStandardResolver` as `Dictionary<string, object>`
  - Wire framing: 4-byte big-endian length prefix + MessagePack payload (matches
    `engine/cli/src/protocol.rs`)
  - `DisposeAsync`: closes stdin, waits briefly for the subprocess to exit, kills the
    process tree if still alive after the timeout

**DI registration** (`src/FlowTime.API/Program.cs`):
- New config key `RustEngine:UseSession` (default `true`). Selects which `IModelEvaluator`
  implementation is registered:
  - `true` → `SessionModelEvaluator` (persistent session, compile-once)
  - `false` → `RustModelEvaluator` (stateless subprocess per eval — retained as fallback)
- `IModelEvaluator`, `SweepRunner`, `SensitivityRunner`, `GoalSeeker`, `Optimizer` change
  from `AddSingleton` → `AddScoped`. Session lifetime must match the analysis run; Scoped
  gives one evaluator per HTTP request with automatic disposal. Even when `UseSession=false`
  the Scoped lifetime is harmless — runners are stateless wrappers.
- `RustEngineRunner` remains `Singleton` (still used by E-20 bridge/parity tests and by
  `RustModelEvaluator`).

**Why keep `RustModelEvaluator`:**
- Fallback switch if the session protocol surfaces bugs in the wild (30 lines of code; negligible maintenance).
- Diagnostic comparison path — "does the non-session path agree?" is a cheap bug-triage question.
- Process isolation per eval is genuinely different behavior from a stateful session; both have legitimate deployment shapes (see cloud-deployment notes in `ROADMAP.md`).
- Two production impls make `IModelEvaluator` a real seam, not a testing-only interface.

**Package reference:**
- Add `MessagePack` 3.1.4 to `src/FlowTime.TimeMachine/FlowTime.TimeMachine.csproj`
  (same version already used in `FlowTime.Integration.Tests`)

**In scope:**
- `src/FlowTime.TimeMachine/Sweep/SessionModelEvaluator.cs`
- `src/FlowTime.TimeMachine/FlowTime.TimeMachine.csproj` (MessagePack package)
- `src/FlowTime.API/Program.cs` (config switch + DI scope changes)
- Unit tests: `tests/FlowTime.TimeMachine.Tests/Sweep/SessionModelEvaluatorTests.cs`
  (covers override extraction logic and error paths that do not require the subprocess)
- Integration tests: `tests/FlowTime.Integration.Tests/SessionModelEvaluatorIntegrationTests.cs`
  (requires the Rust binary; skipped if not present, following the existing
  `EngineSessionWebSocketTests` pattern)
- Milestone tracking doc
- Update `docs/architecture/time-machine-analysis-modes.md` to note the new evaluator and config switch

**Out of scope:**
- Session pooling / reuse across HTTP requests — each request gets its own session
- Auto-reconnect on session crash — if the subprocess dies, the next call surfaces the error
- Chunked evaluation (Mode 6) — separate later milestone
- Model-change detection (session always compiles the YAML it sees first; further calls
  assume the same base model, which holds for all current analysis runners)

## Design

### Lifecycle

```
T=0   SessionModelEvaluator ctor (no I/O yet)
T=1   SweepRunner calls EvaluateAsync(patchedYaml1)
      → lazy spawn subprocess
      → send compile { yaml: patchedYaml1 }
      → receive compile result { params: [...], series: {...} }
      → store paramIds from the response
      → return series
T=2   SweepRunner calls EvaluateAsync(patchedYaml2)
      → use ConstNodeReader to read each paramId value from patchedYaml2
      → send eval { overrides: { ... } }
      → receive eval result { series: {...} }
      → return series
...
T=N   HTTP request ends → DI scope disposes the evaluator
      → DisposeAsync: close stdin, wait 1s, kill if still alive
```

### Why the `overrides` approach works

The compile result captures the initial parameter defaults from the YAML (including whatever
values the first patch applied). On every subsequent call the evaluator sends an explicit
override for every tracked parameter, so the session always evaluates with the current
patched values. The compile-time defaults only matter for the very first call's return.

### Request/response shapes

Requests are plain `Dictionary<string, object>` (contractless MessagePack). Responses
are `Dictionary<object, object>` navigated by key. Matching the Rust protocol:

| Method | Request params | Response `result` |
|--------|----------------|-------------------|
| `compile` | `{ yaml: string }` | `{ params: [...], series: { id: [double,...] }, bins, grid, graph, warnings }` |
| `eval` | `{ overrides: { paramId: double } }` | `{ series: { id: [double,...] }, elapsed_us, warnings }` |

Errors arrive as `{ error: { code, message } }` with no `result` key. The evaluator
raises `InvalidOperationException` with the error code + message.

## Acceptance Criteria

- [x] `SessionModelEvaluator` exists, implements `IModelEvaluator` and `IAsyncDisposable`
- [x] Constructor validates engine path (non-null, non-whitespace)
- [x] First `EvaluateAsync` call spawns the subprocess exactly once; subsequent calls reuse it
- [x] First call sends `compile`; subsequent calls send `eval` with overrides extracted via `ConstNodeReader`
- [x] Returned series dictionary uses case-insensitive keys (matches `RustModelEvaluator`)
- [x] Error responses (`error` key present) raise `InvalidOperationException` with code + message
- [x] `DisposeAsync` closes stdin, waits for exit, kills the process tree on timeout
- [x] `DisposeAsync` is idempotent (safe to call multiple times)
- [x] Calling `EvaluateAsync` after `DisposeAsync` throws `ObjectDisposedException`
- [x] `CancellationToken` is observed during I/O
- [x] Concurrent `EvaluateAsync` calls on one instance are serialized (no interleaved frames)
- [x] DI: `IModelEvaluator`, `SweepRunner`, `SensitivityRunner`, `GoalSeeker`, `Optimizer` all registered as `Scoped`
- [x] DI: `RustEngine:UseSession` config (default `true`) selects `SessionModelEvaluator`; `false` selects `RustModelEvaluator`
- [x] `RustModelEvaluator.cs` retained as fallback; covered by an API test that flips the config switch
- [x] Unit tests pass: 32 tests total
  - 6 constructor + disposal (SessionModelEvaluatorTests)
  - 3 BuildOverrides (empty / all-found / some-missing)
  - 5 ExtractResult (success / error-with-code-msg / error-missing-subfields / neither / malformed-result)
  - 4 ExtractParamIds (missing-key / not-array / valid / malformed-items)
  - 6 ExtractSeries (missing-key / not-dict / valid / case-insensitive / non-string-key / non-array-value)
  - 1 WriteFrameAsync (length-prefixed MessagePack)
  - 5 ReadFrameAsync (valid / zero / negative / excessive / truncated)
  - 2 ReadExactAsync (full-read / EOF-mid-read)
- [x] Integration tests pass with the Rust binary present: 8 tests (SessionModelEvaluatorIntegrationTests)
  - [x] Compile-once / eval-many returns correct series after parameter override
  - [x] Parity on numeric values against per-eval path (keys differ by design — documented in `work/gaps.md`)
  - [x] `SweepRunner` drives `SessionModelEvaluator` end-to-end over a 5-point sweep
  - [x] Session subprocess does not leak after disposal
  - [x] Invalid model raises `InvalidOperationException` with engine error code
  - [x] Concurrent calls on one instance are serialized
- [x] API DI tests pass: 4 tests (ModelEvaluatorRegistrationTests — default/true/false/scope lifetime)
- [x] `dotnet build FlowTime.sln` green
- [x] `dotnet test FlowTime.sln` all green (1,620 passed / 9 skipped)
- [x] `docs/architecture/time-machine-analysis-modes.md` updated — now documents both evaluator paths, config switch, and scoped lifetime

## Coverage notes

**Covered:** every reachable branch in the production implementation — 44 dedicated tests (32 unit + 8 integration + 4 DI). The unit tests deliberately exercise every parsing helper with hand-crafted protocol payloads that the real Rust engine would not produce (missing fields, malformed types, non-string keys, out-of-range frame lengths), because those are defense-in-depth paths against protocol corruption and must not fail silently.

**Explicitly not covered (defensive paths, acceptable gaps):**

| Path | Why untested |
|------|--------------|
| `DisposeAsync` graceful-timeout → `Process.Kill` (line ~380) | Requires simulating a stuck subprocess; no deterministic way in unit tests. Behavior is symmetrically correct with the kill-succeeds case which IS covered by `Dispose_TerminatesSubprocess`. |
| `DisposeAsync` generic exception while waiting for exit (line ~385) | Defense-in-depth catch — unreachable in practice. `WaitForExitAsync` only throws `OperationCanceledException` (covered) or completes normally. |
| `SpawnProcess` `Process.Start` returns null | Only happens on platform-level process creation failure with an executable path that exists. Not reproducible in test. |
| `ExchangeAsync` `stdin`/`stdout` null guard | Defensive — caller always invokes after `SpawnProcess` has assigned both streams. Unreachable in practice. |
| `EvaluateAsync` inner-after-mutex disposed check | Race between `DisposeAsync` and an in-flight `EvaluateAsync`. Hard to trigger deterministically. The outer check + mutex make this extremely narrow. |
| `EvalAsync` error response | The Rust session only errors on `eval` when no model has been compiled (covered by compile-error path instead) or on a programmer bug that isn't otherwise reachable. |

These six branches remain in the code as defense-in-depth and would be removed only with explicit evidence that they cannot occur under any future refactor.

## Dependencies

- m-E18-02 (engine session protocol) — delivered
- m-E18-08 (ITelemetrySource) — independent
- m-E18-09 (`IModelEvaluator` seam, `ConstNodePatcher`) — delivered
- m-E18-10 (`ConstNodeReader`) — delivered
- `MessagePack` 3.1.4 — already in integration tests, add to TimeMachine

## Risks / notes

- **Scope lifetime change.** Moving the four runners from `Singleton` → `Scoped` is a DI
  semantics change. Runners are stateless wrappers over `IModelEvaluator`, so the risk is
  low, but verify the minimal API endpoints still resolve them correctly.
- **Test flakiness from subprocess I/O.** Integration tests must guard against slow spawn
  on first call; use a 5 s initial-compile timeout and skip cleanly if the binary is absent.
- **Process leak on abnormal termination.** `DisposeAsync` kills the process tree; CI must
  not accumulate stray `flowtime-engine` processes between tests.
- **MessagePack dependency surface.** Adding `MessagePack` to `FlowTime.TimeMachine` pulls
  it into the runtime surface. Acceptable — it is already transitively available through
  `FlowTime.Integration.Tests` and matches the wire format owned by the Rust engine.
