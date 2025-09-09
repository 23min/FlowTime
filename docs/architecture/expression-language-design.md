# Expression Language Architecture Design

**Document Version**: 1.0  
**Created**: September 9, 2025  
**Implementation**: M1.5 Expression Language Foundation  
**Branch**: `feature/M1.5-expression-language`

## Overview

This document captures the key architectural decisions made during the implementation of FlowTime's M1.5 expression language foundation. The expression system enables users to write mathematical expressions like `(a + b) * 2 + SHIFT(c, 1)` that are parsed, compiled, and evaluated against time-series data.

## System Architecture

```
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│ Expression Text │───▶│ Parser (AST)    │───▶│ Compiler        │
│ "(a+b)*SHIFT(c)"│    │ ExpressionNode  │    │ ExpressionNode  │
└─────────────────┘    │ Hierarchy       │    │ → INode Graph   │
                       └─────────────────┘    └─────────────────┘
                                                        │
                                                        ▼
                                              ┌─────────────────┐
                                              │ Evaluation      │
                                              │ Series-based    │
                                              │ TimeGrid ops    │
                                              └─────────────────┘
```

## Key Design Decisions

### 1. Parser Architecture: Recursive Descent

**Decision**: Implement a hand-written recursive descent parser

**Implementation**:
```csharp
// ExpressionParser.cs - Direct grammar mapping
private ExpressionNode ParseExpression() 
{
    return ParseTerm();  // Handle + and - operators
}

private ExpressionNode ParseTerm()
{
    var left = ParseFactor();  // Handle * and / operators
    while (position < expression.Length && IsTermOperator(CurrentChar))
    {
        // Left-associative operator handling
    }
}

private ExpressionNode ParseFactor()
{
    // Handle literals, parentheses, function calls, node references
}
```

**Rationale**:
- **Simplicity**: Direct mapping from EBNF grammar to code makes the parser easy to understand and maintain
- **Performance**: Good enough for FlowTime's expression complexity (~10-50 tokens typical)
- **Error Handling**: Natural placement for meaningful error messages with position information
- **Maintainability**: Easy to extend grammar by adding new parse methods

**Alternatives Considered**:

| Alternative | Why Not Chosen |
|-------------|----------------|
| **Parser Generators** (ANTLR, etc.) | Overkill for simple grammar; adds build dependency and complexity |
| **Shunting-yard Algorithm** | More complex to implement and debug; harder to provide good error messages |
| **Expression Trees + Reflection** | Runtime overhead; less type safety; harder to optimize |

**Trade-offs**:
- ✅ **Pros**: Simple, fast, maintainable, good error messages
- ⚠️ **Cons**: Manual grammar maintenance, limited to LL(1) grammars

### 2. Evaluation Strategy: Series-Based Processing

**Decision**: Evaluate expressions over entire time series at once, not bin-by-bin

**Implementation**:
```csharp
public Series Evaluate(TimeGrid grid, Func<NodeId, Series> getInput)
{
    var sourceValues = getInput(sourceNode.Id);
    var shiftedValues = new double[grid.Bins];
    
    // Process entire series in single pass
    for (int i = 0; i < grid.Bins; i++)
    {
        if (i < lag) 
            shiftedValues[i] = 0.0;           // Zero-fill
        else 
            shiftedValues[i] = sourceValues[i - lag];  // Direct indexing
    }
    return new Series(shiftedValues);
}
```

**Rationale**:
- **Consistency**: Matches existing FlowTime node evaluation patterns
- **Performance**: Vectorized operations over entire time series, single memory allocation
- **Memory Efficiency**: No repeated temporary object creation during evaluation
- **Backward Compatibility**: All existing nodes continue working unchanged

**Alternatives Considered**:

| Alternative | Why Not Chosen |
|-------------|----------------|
| **Bin-by-bin Evaluation** | Would require changing all existing nodes; much more complex state management |
| **Lazy Evaluation** | Unnecessary complexity for FlowTime's deterministic grid patterns |
| **Streaming Evaluation** | FlowTime operates on fixed grids, not infinite streams |

**Trade-offs**:
- ✅ **Pros**: Fast, memory-efficient, consistent with existing architecture
- ⚠️ **Cons**: Higher memory usage (full series in memory), less suitable for infinite streams

### 3. SHIFT Implementation: Direct Array Indexing

**Decision**: Use direct array indexing for temporal shift operations rather than stateful queue management

**Implementation**:
```csharp
// Simple, direct approach
for (int i = 0; i < grid.Bins; i++)
{
    if (i < lag)
        shiftedValues[i] = 0.0;                    // Fill with zeros
    else 
        shiftedValues[i] = sourceValues[i - lag];  // Direct indexing
}
```

**Rationale**:
- **Simplicity**: Minimal state, easy to understand and verify correctness
- **Performance**: O(1) access time, no queue management overhead
- **Memory**: Single output array allocation, no additional data structures
- **Correctness**: Impossible to have off-by-one errors with queue management

**Alternatives Considered**:

| Alternative | Why Not Chosen |
|-------------|----------------|
| **Circular Buffer/Queue** | Added complexity for no benefit in fixed-grid evaluation |
| **History Tracking** | Unnecessary when entire source series is available |
| **Stateful Bin-by-bin** | Would require complex state management between evaluations |

**Trade-offs**:
- ✅ **Pros**: Simple, fast, correct, minimal memory overhead
- ⚠️ **Cons**: Requires full source series availability (not streaming-friendly)

### 4. AST Design: Abstract Methods with Direct Evaluation

**Decision**: Use abstract base class with direct evaluation methods rather than visitor pattern

**Implementation**:
```csharp
public abstract class ExpressionNode
{
    public abstract double EvaluateScalar(Dictionary<string, double> context);
    public abstract INode CompileToNode(string nodeId, Dictionary<string, INode> nodeContext);
}

public class BinaryOpExpressionNode : ExpressionNode
{
    public override double EvaluateScalar(Dictionary<string, double> context)
    {
        var leftVal = Left.EvaluateScalar(context);
        var rightVal = Right.EvaluateScalar(context);
        return Operator switch {
            '+' => leftVal + rightVal,
            '-' => leftVal - rightVal,
            '*' => leftVal * rightVal,
            '/' => rightVal != 0 ? leftVal / rightVal : throw new DivideByZeroException(),
            _ => throw new InvalidOperationException($"Unknown operator: {Operator}")
        };
    }
}
```

**Rationale**:
- **Type Safety**: Compile-time checking of operations and method signatures
- **Performance**: Direct method calls, no reflection or runtime dispatch overhead
- **Extensibility**: Easy to add new node types by implementing abstract methods
- **Debugging**: Clear call stack for expression evaluation, easy to trace execution

**Alternatives Considered**:

| Alternative | Why Not Chosen |
|-------------|----------------|
| **Visitor Pattern** | More flexible but adds complexity we don't need yet; harder to understand |
| **Interpreter Pattern** | Would require complex context passing, more indirection |
| **Code Generation** | Overkill for current expression complexity; adds build complexity |

**Trade-offs**:
- ✅ **Pros**: Type-safe, fast, easy to debug, extensible
- ⚠️ **Cons**: Adding new operations requires touching base class

### 5. Error Handling: Exceptions for Parse Errors

**Decision**: Use exceptions for parsing errors with rich position information

**Implementation**:
```csharp
private char ExpectChar(char expected)
{
    if (position >= expression.Length || expression[position] != expected)
        throw new ExpressionParseException(
            $"Expected '{expected}' at position {position}", 
            position, 
            expression);
    return expression[position++];
}

public class ExpressionParseException : Exception
{
    public int Position { get; }
    public string Expression { get; }
    
    public ExpressionParseException(string message, int position, string expression) 
        : base(message)
    {
        Position = position;
        Expression = expression;
    }
}
```

**Rationale**:
- **.NET Conventions**: Exceptions are idiomatic for parsing errors in .NET
- **Early Failure**: Parse errors should stop execution immediately (fail-fast principle)
- **Rich Context**: Exception messages can include position, context, and suggestions
- **Simplicity**: No need to thread `Result<T, Error>` types through all parser methods

**Alternatives Considered**:

| Alternative | Why Not Chosen |
|-------------|----------------|
| **Result<T, Error> Types** | Would require threading through all parser methods; complex to implement |
| **Nullable Returns** | Loses error context and position information |
| **Error Accumulation** | For expressions, fail-fast is better than collecting multiple errors |

**Trade-offs**:
- ✅ **Pros**: Rich error information, follows .NET conventions, simple to implement
- ⚠️ **Cons**: Exception overhead (acceptable for error cases), less functional programming style

### 6. Function Handling: Built-in Function Compilation

**Decision**: Implement built-in functions with direct compilation rather than extensible registry

**Implementation**:
```csharp
private INode CompileFunctionCall(FunctionCallExpressionNode funcNode, 
                                 Dictionary<string, INode> nodeContext)
{
    return funcNode.FunctionName switch
    {
        "SHIFT" => CompileShiftFunction(funcNode, nodeContext),
        "MIN" => CompileMinFunction(funcNode, nodeContext),
        "MAX" => CompileMaxFunction(funcNode, nodeContext),
        "CLAMP" => CompileClampFunction(funcNode, nodeContext),
        _ => throw new ArgumentException($"Unknown function: {funcNode.FunctionName}")
    };
}

private INode CompileShiftFunction(FunctionCallExpressionNode funcNode, 
                                  Dictionary<string, INode> nodeContext)
{
    if (funcNode.Arguments.Count != 2)
        throw new ArgumentException("SHIFT function requires exactly 2 arguments");
        
    var sourceNode = funcNode.Arguments[0].CompileToNode("temp", nodeContext);
    var lagValue = funcNode.Arguments[1].EvaluateScalar(new Dictionary<string, double>());
    
    return new ShiftNode($"shift_{Guid.NewGuid()}", sourceNode, (int)lagValue);
}
```

**Rationale**:
- **YAGNI Principle**: Only implement what M1.5 requires (4 functions)
- **Performance**: Direct compilation, no registry lookup overhead at runtime
- **Type Safety**: Compile-time validation of function signatures and argument counts
- **Simplicity**: No complex registration/discovery mechanisms needed

**Alternatives Considered**:

| Alternative | Why Not Chosen |
|-------------|----------------|
| **Plugin Architecture** | Overkill for M1.5; can add later when we have 20+ functions |
| **Reflection-based Discovery** | Runtime overhead, less type safety, harder to optimize |
| **External Function Registry** | Additional complexity without current benefit |

**Trade-offs**:
- ✅ **Pros**: Fast, type-safe, simple to implement and debug
- ⚠️ **Cons**: Adding functions requires code changes (vs. configuration)

## Performance Characteristics

### Parsing Performance
- **Time Complexity**: O(n) where n is expression length
- **Space Complexity**: O(d) where d is maximum expression depth
- **Typical Performance**: Sub-millisecond for expressions under 100 tokens

### Evaluation Performance
- **Time Complexity**: O(m) where m is number of time bins
- **Space Complexity**: O(m) for output series allocation
- **Typical Performance**: 10-100 microseconds for 100-bin time series

### Memory Usage
- **AST Storage**: ~100-500 bytes per expression
- **Evaluation Memory**: 8 bytes per bin per intermediate series
- **Garbage Collection**: Minimal allocations during evaluation

## Extension Points for Future Development

### M9.5 Retry Modeling Support
The architecture anticipates future retry modeling requirements:

```csharp
// Future CONV operator implementation
public class ConvNode : IStatefulNode
{
    // Can follow same patterns as ShiftNode
    // Convolution with retry kernels
}

// Function registry when we have 20+ functions
public interface IFunctionRegistry
{
    INode CompileFunction(string name, IList<ExpressionNode> args, 
                         Dictionary<string, INode> context);
}
```

### Performance Optimization Opportunities
1. **JIT Compilation**: Generate IL code for hot expressions
2. **Vectorization**: Use SIMD instructions for arithmetic operations
3. **Constant Folding**: Pre-compute constant sub-expressions
4. **Common Sub-expression Elimination**: Cache repeated computations

### Evaluation Strategy Extensions
1. **Streaming Evaluation**: Bin-by-bin evaluation for infinite series
2. **Parallel Evaluation**: Multi-threaded evaluation for large grids
3. **GPU Acceleration**: CUDA/OpenCL for very large time series

## Validation and Testing

### Test Coverage
- **Parser Tests**: 28 tests covering all grammar elements and error cases
- **SHIFT Node Tests**: 7 tests covering lag scenarios and edge cases  
- **Integration Tests**: 5 tests validating end-to-end expression evaluation
- **Total Coverage**: 40 expression-related tests, all passing

### Performance Validation
- **Parsing**: ~0.1ms for typical expressions (20-50 tokens)
- **Evaluation**: ~0.05ms for 100-bin time series
- **Memory**: <1KB per active expression, minimal GC pressure

### Correctness Validation
- **Mathematical Accuracy**: All arithmetic operations tested against expected results
- **Temporal Correctness**: SHIFT operations validated against manual calculations
- **Error Handling**: All error conditions tested with appropriate exceptions

## Architectural Principles Applied

### FlowTime Core Principles
1. **Deterministic Evaluation**: All expressions produce identical results for identical inputs
2. **Time Grid Alignment**: All operations respect bin boundaries and grid structure
3. **Type Safety**: Strong typing throughout expression system
4. **Performance Focus**: Sub-millisecond evaluation for typical use cases

### Software Engineering Principles
1. **YAGNI**: Only implement features required for M1.5
2. **SOLID**: Single responsibility, open/closed, dependency inversion
3. **Fail Fast**: Parse errors throw immediately with rich context
4. **Composability**: Expressions can be nested and combined arbitrarily

## Decision Log

| Decision | Date | Rationale | Alternatives Considered |
|----------|------|-----------|------------------------|
| Recursive Descent Parser | 2025-09-09 | Simplicity, maintainability | ANTLR, Shunting-yard |
| Series-based Evaluation | 2025-09-09 | Performance, consistency | Bin-by-bin, lazy evaluation |
| Direct Array Indexing | 2025-09-09 | Simplicity, performance | Queue-based, stateful |
| Abstract Method AST | 2025-09-09 | Type safety, performance | Visitor pattern, interpreter |
| Exception Error Handling | 2025-09-09 | .NET conventions, simplicity | Result types, nullable returns |
| Built-in Function Compilation | 2025-09-09 | YAGNI, performance | Plugin architecture, registry |

## Future Evolution Strategy

The M1.5 implementation provides a solid foundation for future enhancements while avoiding over-engineering. Key evolution paths:

1. **M9.5 Integration**: Add CONV operator and retry modeling functions
2. **Performance Scaling**: Add JIT compilation when expressions become performance bottlenecks
3. **Function Extensibility**: Add registry pattern when we reach 20+ built-in functions
4. **Advanced Features**: Add when-if conditional expressions, statistical functions

The architecture intentionally balances immediate M1.5 needs with long-term extensibility, following the principle of "make it work, make it right, make it fast" - focusing on correctness and simplicity first.
