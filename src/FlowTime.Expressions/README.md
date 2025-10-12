# FlowTime.Expressions

FlowTime.Expressions provides the shared parser, AST types, and semantic validation logic that both FlowTime Engine and FlowTime.Sim consume. The library guarantees that expression behaviours (syntax, SHIFT handling, validation errors) remain consistent across surfaces.

## What’s Included
- Recursive-descent parser (`ExpressionParser`) that produces strongly-typed AST nodes.
- AST model (`ExpressionNode` hierarchy) with visitor support for evaluators/compilers.
- Semantic helpers (e.g., `ExpressionSemanticValidator.HasSelfReferencingShift`) for enforcing SHIFT initial-condition rules.

## Usage
```csharp
using FlowTime.Core.Expressions; // Engine compiler consumes FlowTime.Expressions ASTs

var ast = new ExpressionParser("MAX(arrivals, SHIFT(arrivals, 1))").Parse();

if (ExpressionSemanticValidator.HasSelfReferencingShift(ast, "queue_depth"))
{
    // prompt caller to supply topology initialCondition metadata
}

var node = ExpressionCompiler.Compile(ast, "queue_depth");
```
- Engine code references the project directly (`FlowTime.Core/FlowTime.Core.csproj`).
- FlowTime.Sim should add a project reference (or NuGet dependency once published) and rely on the same validator before executing expressions.

## Testing
- `tests/FlowTime.Expressions.Tests` covers parser behaviour, AST structure, semantic validation, and integration with the Engine evaluator.
- Run `dotnet test FlowTime.Expressions.Tests/FlowTime.Expressions.Tests.csproj` locally; the suite executes as part of `dotnet test FlowTime.sln`.

## Manual Verification
- Use the HTTP scratch file at `src/FlowTime.API/FlowTime.API.http` to submit a canonical time-travel model, then hit `/state` and `/state_window` to confirm shared validation errors surface as expected.
- The file binds to fixture CSVs under `fixtures/time-travel/http-demo` and exercises topology semantics plus derived metrics.
- A dedicated failure sample (section 7 in the `.http` file) posts a model with a self-referencing SHIFT that lacks an initial condition. The Engine should respond with `409` and the shared error message.

## Adoption Notes
- Keep semantic checks inside FlowTime.Expressions; Engine consumers should not re-implement visitor logic (see `ModelParser.ValidateInitialConditions`).
- FlowTime.Sim test projects already reference the library and include a placeholder smoke test (`tests/FlowTime.Sim.Tests/Expressions/ExpressionLibrarySmokeTests.cs`) that will graduate to full validation during SIM-M-03.
- When extending the expression language, add parser + validator coverage here first, then update Engine/Sim integration tests to reflect the shared behaviour.
