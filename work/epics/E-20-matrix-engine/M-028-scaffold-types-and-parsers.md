---
id: M-028
title: Scaffold, Types, and Parsers
status: done
parent: E-20
acs:
  - id: AC-1
    title: 'AC-1: Rust workspace and crate structure'
    status: met
  - id: AC-2
    title: 'AC-2: Devcontainer Rust toolchain'
    status: met
  - id: AC-3
    title: 'AC-3: Model types with serde deserialization'
    status: met
  - id: AC-4
    title: 'AC-4: All existing model fixtures deserialize'
    status: met
  - id: AC-5
    title: 'AC-5: Expression parser'
    status: met
  - id: AC-6
    title: 'AC-6: Expression parser parity'
    status: met
  - id: AC-7
    title: 'AC-7: Reference model fixtures extracted'
    status: met
---

## Goal

Stand up the Rust project, port all model types with YAML deserialization, port the expression parser, and extract reference model fixtures. After this milestone, the Rust crate can parse any FlowTime model YAML and any FlowTime expression — the complete data layer with no computation.

## Context

No Rust code exists in the repo. The devcontainer does not have Rust installed. The C# engine defines ~22 model types in `ModelParser.cs` and a recursive-descent expression parser in `FlowTime.Expressions/`. Existing YAML model files in `examples/` and `fixtures/` serve as integration test fixtures.

## Acceptance criteria

### AC-1 — AC-1: Rust workspace and crate structure

**AC-1: Rust workspace and crate structure.** A Rust workspace at `engine/` (repo root) with two crates:
- `engine/core/` — library crate (`flowtime-core`) containing model types, expression parser, and (future) compiler/evaluator.
- `engine/cli/` — binary crate (`flowtime-engine`) that depends on `flowtime-core`. For this milestone, it only parses a model YAML and prints a summary (node count, grid dimensions).
- `Cargo.toml` workspace at `engine/` level.
### AC-2 — AC-2: Devcontainer Rust toolchain

**AC-2: Devcontainer Rust toolchain.** The devcontainer gains Rust support:
- `rustup` and `cargo` available on `$PATH`.
- `cargo build` and `cargo test` work from `engine/`.
- Installation via devcontainer feature or post-create script — not manual.
### AC-3 — AC-3: Model types with serde deserialization

**AC-3: Model types with serde deserialization.** Rust structs mirroring every C# model type that participates in YAML deserialization:
- `ModelDefinition`, `GridDefinition`, `NodeDefinition`, `TopologyDefinition`, `TopologyNodeDefinition`, `TopologyNodeSemanticsDefinition`, `TopologyEdgeDefinition`, `ConstraintDefinition`, `ConstraintSemanticsDefinition`, `ClassDefinition`, `TrafficDefinition`, `ArrivalDefinition`, `ArrivalPatternDefinition`, `OutputDefinition`, `RouterDefinition`, `RouterInputsDefinition`, `RouterRouteDefinition`, `DispatchScheduleDefinition`, `PmfDefinition`, `InitialConditionDefinition`, `UiHintsDefinition`.
- All fields use camelCase JSON/YAML naming (matching existing schema).
- Optional fields are `Option<T>`.
- `serde_yaml` for deserialization.
### AC-4 — AC-4: All existing model fixtures deserialize

**AC-4: All existing model fixtures deserialize.** Every `.yaml` model file in `examples/` and `fixtures/` (excluding `examples/archive/`) parses into the Rust `ModelDefinition` without error. Test: `cargo test` includes a parameterized test that loads each fixture.
### AC-5 — AC-5: Expression parser

**AC-5: Expression parser.** Port of the C# `ExpressionParser` (recursive descent) producing equivalent AST types:
- AST: `Expr` enum with variants `Literal(f64)`, `ArrayLiteral(Vec<f64>)`, `NodeRef(String)`, `BinaryOp { op, left, right }`, `FunctionCall { name, args }`.
- `BinaryOp`: Add, Subtract, Multiply, Divide.
- Grammar: `Expression = Term (('+' | '-') Term)*`, `Term = Factor (('*' | '/') Factor)*`, `Factor = Number | Array | NodeRef | FunctionCall | '(' Expression ')'`.
- Error reporting with position.
### AC-6 — AC-6: Expression parser parity

**AC-6: Expression parser parity.** Every expression that appears in existing model fixtures and C# test fixtures parses correctly. Additionally, the following expressions must parse and produce correct AST structure (extracted from C# tests):
- `"capacity"` → NodeRef
- `"100.0"` → Literal
- `"a + b"` → BinaryOp(Add)
- `"a * b + c"` → precedence: Add(Mul(a, b), c)
- `"(a + b) * c"` → Mul(Add(a, b), c)
- `"SHIFT(demand, 1)"` → FunctionCall("SHIFT", [NodeRef("demand"), Literal(1)])
- `"CONV(errors, [0.0, 0.6, 0.3, 0.1])"` → FunctionCall with ArrayLiteral
- `"CLAMP(queue_depth / 50, 0, 1)"` → nested function + binary op
- `"raw_arrivals * (1 - SHIFT(pressure, 1))"` → nested binary ops with function call
- `"MIN(capacity, arrivals)"` → FunctionCall
- `"MAX(0, SHIFT(queue_depth, 1) + arrivals)"` → nested
### AC-7 — AC-7: Reference model fixtures extracted

**AC-7: Reference model fixtures extracted.** A directory `engine/fixtures/` containing YAML model files at graduated complexity levels, copied or symlinked from existing `examples/` and `fixtures/`. At minimum:
- Simple: const-only model (e.g., `m0.const.yaml`)
- Expression: model with expr nodes
- Queue: model with serviceWithBuffer topology
- PMF: model with PMF nodes
- Router: model with router nodes
- Constraint: model with constraints
- Multi-class: model with class definitions
- WIP limit: model with wipLimit/wipOverflow
- These serve as the progressive parity test fixtures for M2-M6.
## Technical Notes

- **Crate naming:** `flowtime-core` (library) and `flowtime-engine` (binary) follow Rust conventions (kebab-case crate names).
- **Workspace location:** `engine/` at repo root keeps Rust separate from the .NET solution. `Cargo.lock` lives in `engine/`.
- **serde field naming:** Use `#[serde(rename_all = "camelCase")]` on structs to match the existing YAML camelCase convention. Use `#[serde(default)]` for optional fields that have C# defaults.
- **Expression parser:** The C# parser is 371 lines. The Rust port should be similar size. Use `&str` + byte position for error reporting.
- **No computation:** This milestone deliberately excludes compilation, evaluation, and artifact writing. The types and parsers are the foundation; computation starts in M-029.
- **Sim YAML models:** Some fixtures use Sim-specific fields (`metadata.generator`, `parameters`, etc.) that are not in `ModelDefinition`. Use `#[serde(flatten)]` or ignore unknown fields with `#[serde(deny_unknown_fields)]` disabled — existing models must parse without error.

## Out of Scope

- Model compilation (topo sort, column map, plan generation) — M-029
- Evaluation (matrix ops) — M-029+
- Model validation (schema checks, initial condition validation) — M-029+
- Artifact writing — M-033
- WebAssembly compilation — future
- Expression evaluation (only parsing) — M-029

## Dependencies

- None (first milestone in E-20)
