---
id: M-004
title: Generator Extraction → TimeMachine
status: done
parent: E-18
acs:
  - id: AC-1
    title: '`src/FlowTime.TimeMachine/` exists; `src/FlowTime.Generator/` is gone'
    status: met
  - id: AC-2
    title: '`tests/FlowTime.TimeMachine.Tests/` exists; `tests/FlowTime.Generator.Tests/` is gone'
    status: met
  - id: AC-3
    title: '`dotnet build FlowTime.sln` succeeds with zero errors'
    status: met
  - id: AC-4
    title: '`dotnet test FlowTime.sln` passes with the same test count'
    status: met
  - id: AC-5
    title: '`rg "FlowTime\.Generator" src/ tests/ --include="*.cs" --include="*.csproj"` returns zero matches'
    status: met
  - id: AC-6
    title: Solution file contains TimeMachine entry; Generator entry is absent
    status: met
---

## Goal

Rename `FlowTime.Generator` → `FlowTime.TimeMachine`. Move all classes, update all
references in consumers (src + tests), remove `FlowTime.Generator` from the solution.
Pure structural refactor — no behavior change, all tests green, no coexistence window
(per D-032 Path B).

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
- Tiered validation (M-003)
- Any behavior changes whatsoever

## Acceptance criteria

### AC-1 — `src/FlowTime.TimeMachine/` exists; `src/FlowTime.Generator/` is gone

### AC-2 — `tests/FlowTime.TimeMachine.Tests/` exists; `tests/FlowTime.Generator.Tests/` is gone

### AC-3 — `dotnet build FlowTime.sln` succeeds with zero errors

### AC-4 — `dotnet test FlowTime.sln` passes with the same test count

### AC-5 — `rg "FlowTime\.Generator" src/ tests/ --include="*.cs" --include="*.csproj"` returns zero matches

### AC-6 — Solution file contains TimeMachine entry; Generator entry is absent
## Namespace Mapping

| Old | New |
|-----|-----|
| `FlowTime.Generator` | `FlowTime.TimeMachine` |
| `FlowTime.Generator.Artifacts` | `FlowTime.TimeMachine.Artifacts` |
| `FlowTime.Generator.Capture` | `FlowTime.TimeMachine.Capture` |
| `FlowTime.Generator.Models` | `FlowTime.TimeMachine.Models` |
| `FlowTime.Generator.Orchestration` | `FlowTime.TimeMachine.Orchestration` |
| `FlowTime.Generator.Processing` | `FlowTime.TimeMachine.Processing` |
