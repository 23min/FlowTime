---
id: M-010
title: SessionModelEvaluator
status: done
parent: E-18
acs:
  - id: AC-1
    title: SessionModelEvaluator exists, implements IModelEvaluator and
    status: met
  - id: AC-2
    title: Constructor validates engine path (non-null, non-whitespace)
    status: met
  - id: AC-3
    title: First EvaluateAsync call spawns the subprocess exactly once
    status: met
  - id: AC-4
    title: First call sends compile; subsequent calls send eval with overrides
    status: met
  - id: AC-5
    title: Returned series dictionary uses case-insensitive keys (matches
    status: met
  - id: AC-6
    title: Error responses (error key present) raise InvalidOperationException
    status: met
  - id: AC-7
    title: DisposeAsync closes stdin, waits for exit, kills the process tree on
    status: met
  - id: AC-8
    title: DisposeAsync is idempotent (safe to call multiple times)
    status: met
  - id: AC-9
    title: Calling EvaluateAsync after DisposeAsync throws
    status: met
  - id: AC-10
    title: CancellationToken is observed during I/O
    status: met
  - id: AC-11
    title: Concurrent EvaluateAsync calls on one instance are serialized (no
    status: met
  - id: AC-12
    title: DI
    status: met
  - id: AC-13
    title: DI
    status: met
  - id: AC-14
    title: RustModelEvaluator.cs retained as fallback; covered by an API test
    status: met
  - id: AC-15
    title: Unit tests pass
    status: met
  - id: AC-16
    title: Integration tests pass with the Rust binary present
    status: met
  - id: AC-17
    title: API DI tests pass
    status: met
  - id: AC-18
    title: dotnet build FlowTime.sln green
    status: met
  - id: AC-19
    title: dotnet test FlowTime.sln all green (1,620 passed / 9 skipped)
    status: met
  - id: AC-20
    title: docs/architecture/time-machine-analysis-modes.md updated
    status: met
---

## Goal

Replace per-point subprocess compile overhead with a single persistent engine session.
Today, every `IModelEvaluator.EvaluateAsync` call spawns `flowtime-engine eval` as a fresh
subprocess that re-parses YAML and re-compiles the Plan. For a sweep of 200 points this is
200 compiles; for an optimization run it can be 100–1000 compiles. Each spawn is
~100–500 ms of pure compile overhead.

`SessionModelEvaluator` uses the M-002 session protocol (MessagePack over stdin/stdout):
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

## Acceptance criteria

### AC-1 — SessionModelEvaluator exists, implements IModelEvaluator and

`SessionModelEvaluator` exists, implements `IModelEvaluator` and `IAsyncDisposable`
### AC-2 — Constructor validates engine path (non-null, non-whitespace)

### AC-3 — First EvaluateAsync call spawns the subprocess exactly once

First `EvaluateAsync` call spawns the subprocess exactly once; subsequent calls reuse it
### AC-4 — First call sends compile; subsequent calls send eval with overrides

First call sends `compile`; subsequent calls send `eval` with overrides extracted via `ConstNodeReader`
### AC-5 — Returned series dictionary uses case-insensitive keys (matches

Returned series dictionary uses case-insensitive keys (matches `RustModelEvaluator`)
### AC-6 — Error responses (error key present) raise InvalidOperationException

Error responses (`error` key present) raise `InvalidOperationException` with code + message
### AC-7 — DisposeAsync closes stdin, waits for exit, kills the process tree on

`DisposeAsync` closes stdin, waits for exit, kills the process tree on timeout
### AC-8 — DisposeAsync is idempotent (safe to call multiple times)

`DisposeAsync` is idempotent (safe to call multiple times)
### AC-9 — Calling EvaluateAsync after DisposeAsync throws

Calling `EvaluateAsync` after `DisposeAsync` throws `ObjectDisposedException`
### AC-10 — CancellationToken is observed during I/O

`CancellationToken` is observed during I/O
### AC-11 — Concurrent EvaluateAsync calls on one instance are serialized (no

Concurrent `EvaluateAsync` calls on one instance are serialized (no interleaved frames)
### AC-12 — DI

DI: `IModelEvaluator`, `SweepRunner`, `SensitivityRunner`, `GoalSeeker`, `Optimizer` all registered as `Scoped`
### AC-13 — DI

DI: `RustEngine:UseSession` config (default `true`) selects `SessionModelEvaluator`; `false` selects `RustModelEvaluator`
### AC-14 — RustModelEvaluator.cs retained as fallback; covered by an API test

`RustModelEvaluator.cs` retained as fallback; covered by an API test that flips the config switch
### AC-15 — Unit tests pass

Unit tests pass: 32 tests total
- 6 constructor + disposal (SessionModelEvaluatorTests)
- 3 BuildOverrides (empty / all-found / some-missing)
- 5 ExtractResult (success / error-with-code-msg / error-missing-subfields / neither / malformed-result)
- 4 ExtractParamIds (missing-key / not-array / valid / malformed-items)
- 6 ExtractSeries (missing-key / not-dict / valid / case-insensitive / non-string-key / non-array-value)
- 1 WriteFrameAsync (length-prefixed MessagePack)
- 5 ReadFrameAsync (valid / zero / negative / excessive / truncated)
- 2 ReadExactAsync (full-read / EOF-mid-read)
### AC-16 — Integration tests pass with the Rust binary present

Integration tests pass with the Rust binary present: 8 tests (SessionModelEvaluatorIntegrationTests)
- [x] Compile-once / eval-many returns correct series after parameter override
- [x] Parity on numeric values against per-eval path (keys differ by design — documented in `work/gaps.md`)
- [x] `SweepRunner` drives `SessionModelEvaluator` end-to-end over a 5-point sweep
- [x] Session subprocess does not leak after disposal
- [x] Invalid model raises `InvalidOperationException` with engine error code
- [x] Concurrent calls on one instance are serialized
### AC-17 — API DI tests pass

API DI tests pass: 4 tests (ModelEvaluatorRegistrationTests — default/true/false/scope lifetime)
### AC-18 — dotnet build FlowTime.sln green

`dotnet build FlowTime.sln` green
### AC-19 — dotnet test FlowTime.sln all green (1,620 passed / 9 skipped)

`dotnet test FlowTime.sln` all green (1,620 passed / 9 skipped)
### AC-20 — docs/architecture/time-machine-analysis-modes.md updated

`docs/architecture/time-machine-analysis-modes.md` updated — now documents both evaluator paths, config switch, and scoped lifetime
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

- M-002 (engine session protocol) — delivered
- M-005 (ITelemetrySource) — independent
- M-006 (`IModelEvaluator` seam, `ConstNodePatcher`) — delivered
- M-007 (`ConstNodeReader`) — delivered
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
