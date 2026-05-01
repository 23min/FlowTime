# Tracking: m-E18-02 Engine Session + Streaming Protocol

**Milestone:** m-E18-02
**Epic:** E-18 Time Machine
**Status:** complete — merged to main 2026-04-10
**Branch:** `milestone/m-E18-02-engine-session-protocol`
**Started:** 2026-04-10
**Completed:** 2026-04-10

## Progress

| AC | Description | Status |
|----|-------------|--------|
| AC-1 | `session` CLI command | pending |
| AC-2 | Length-prefixed MessagePack framing | pending |
| AC-3 | `compile` command | pending |
| AC-4 | `eval` command | pending |
| AC-5 | `get_params` command | pending |
| AC-6 | `get_series` command | pending |
| AC-7 | Error handling | pending |
| AC-8 | Session state | pending |
| AC-9 | Performance (<1ms eval) | pending |
| AC-10 | Integration test (subprocess) | pending |

## Implementation Phases

### Phase 1: Protocol types + framing (AC-2)
- Add rmp-serde dependency
- protocol.rs: Request/Response types, read_message/write_message with length-prefix framing
- Unit tests for round-trip serialize/deserialize

### Phase 2: Session struct + commands (AC-1, AC-3, AC-4, AC-5, AC-6, AC-7, AC-8)
- session.rs: Session state (model, plan, state matrix, overrides)
- Command dispatch: compile, eval, get_params, get_series
- Error handling: not_compiled, compile_error, unknown_method
- cmd_session() in main.rs

### Phase 3: Integration test + performance (AC-9, AC-10)
- Spawn subprocess, send compile + eval sequence via MessagePack
- Performance benchmark: 1,000 evals < 1s
