# m-E18-07 — Generator Extraction → TimeMachine

**Epic:** E-18 Time Machine  
**Branch:** `milestone/m-E18-07-generator-extraction`  
**Status:** complete

## Goal

Rename `FlowTime.Generator` → `FlowTime.TimeMachine`. Move all classes, update all
references in consumers (src + tests), remove `FlowTime.Generator` from the solution.
Pure structural refactor — no behavior change, all tests green, no coexistence window
(per D-2026-04-07-019 Path B).

## Scope

**In scope:**
- Create `src/FlowTime.TimeMachine/FlowTime.TimeMachine.csproj` with identical dependencies
- Move all Generator source files; update `FlowTime.Generator.*` namespaces → `FlowTime.TimeMachine.*`
- Rename `tests/FlowTime.Generator.Tests/` → `tests/FlowTime.TimeMachine.Tests/`; update its csproj
- Update project references in: FlowTime.Cli, FlowTime.Sim.Service, FlowTime.API, FlowTime.Api.Tests, FlowTime.Cli.Tests, FlowTime.Integration.Tests
- Update `using FlowTime.Generator.*` → `using FlowTime.TimeMachine.*` across all source files
- Register TimeMachine in FlowTime.sln; remove Generator entry
- Delete `src/FlowTime.Generator/` entirely

**Out of scope:**
- Tiered validation (m-E18-06)
- Any behavior changes whatsoever

## Acceptance Criteria

- [x] `src/FlowTime.TimeMachine/` exists; `src/FlowTime.Generator/` is gone
- [x] `tests/FlowTime.TimeMachine.Tests/` exists; `tests/FlowTime.Generator.Tests/` is gone
- [x] `dotnet build FlowTime.sln` succeeds with zero errors
- [x] `dotnet test FlowTime.sln` passes with the same test count
- [x] `rg "FlowTime\.Generator" src/ tests/ --include="*.cs" --include="*.csproj"` returns zero matches
- [x] Solution file contains TimeMachine entry; Generator entry is absent

## Namespace Mapping

| Old | New |
|-----|-----|
| `FlowTime.Generator` | `FlowTime.TimeMachine` |
| `FlowTime.Generator.Artifacts` | `FlowTime.TimeMachine.Artifacts` |
| `FlowTime.Generator.Capture` | `FlowTime.TimeMachine.Capture` |
| `FlowTime.Generator.Models` | `FlowTime.TimeMachine.Models` |
| `FlowTime.Generator.Orchestration` | `FlowTime.TimeMachine.Orchestration` |
| `FlowTime.Generator.Processing` | `FlowTime.TimeMachine.Processing` |
