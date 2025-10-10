# Template Testing Guide

## Overview

This guide covers testing strategies for FlowTime-Sim templates, from unit tests for individual components to integration tests with the FlowTime Engine.

## Test Categories

### 1. Schema Validation Tests

Ensure templates conform to the DAG schema structure.

```csharp
[TestFixture]
public class TemplateSchemaValidationTests
{
    [Test]
    public void ValidTemplate_PassesSchemaValidation()
    {
        // Arrange
        var templateYaml = @"
metadata:
  id: test-template
  title: Test Template
parameters:
  - name: capacity
    type: number
    default: 100
grid:
  bins: 12
  binSize: 60
  binUnit: minutes
nodes:
  - id: requests
    kind: const
    values: [100, 200]
outputs:
  - series: requests
    filename: requests.csv";

        // Act
        var template = TemplateLoader.LoadFromString(templateYaml);
        var result = TemplateValidator.Validate(template);

        // Assert
        Assert.That(result.IsValid, Is.True);
        Assert.That(result.Errors, Is.Empty);
    }

    [Test]
    public void Template_WithMissingRequiredField_FailsValidation()
    {
        // Arrange - missing metadata.id
        var invalidYaml = @"
metadata:
  title: Test Template
nodes:
  - id: test
    kind: const
    values: [1]
outputs:
  - series: test
    filename: test.csv";

        // Act & Assert
        Assert.Throws<TemplateValidationException>(
            () => TemplateLoader.LoadFromString(invalidYaml));
    }

    [TestCase("")]
    [TestCase("123invalid")]
    [TestCase("invalid.id")]
    public void Template_WithInvalidId_FailsValidation(string invalidId)
    {
        // Test ID format validation (kebab-case pattern)
        var template = CreateTemplateWithId(invalidId);
        var result = TemplateValidator.Validate(template);
        
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors.Any(e => e.Field == "metadata.id"), Is.True);
    }
}
```

### 2. Parameter Processing Tests

Validate parameter substitution and type handling.

```csharp
[TestFixture] 
public class ParameterProcessingTests
{
    [Test]
    public void ParameterProcessor_WithSimpleSubstitution_ReplacesCorrectly()
    {
        // Arrange
        var template = new TemplateSpec
        {
            Parameters = new List<ParameterSpec>
            {
                new() { Name = "capacity", Type = "number", Default = 100 }
            },
            Nodes = new List<NodeSpec>
            {
                new() { Id = "server", Kind = "const", Values = "${capacity}" }
            }
        };
        
        var parameters = new Dictionary<string, object> { ["capacity"] = 250 };

        // Act
        var result = ParameterProcessor.Process(template, parameters);

        // Assert
        var serverNode = result.Nodes.First(n => n.Id == "server");
        Assert.That(serverNode.Values, Is.EqualTo(250));
    }

    [Test]
    public void ParameterProcessor_WithArrayCycling_ExpandsCorrectly()
    {
        // Arrange
        var template = CreateTemplateWithArrayParameter();
        var parameters = new Dictionary<string, object> 
        { 
            ["pattern"] = new[] { 1, 2, 3 },
            ["bins"] = 7
        };

        // Act
        var result = ParameterProcessor.Process(template, parameters);

        // Assert
        var patternNode = result.Nodes.First(n => n.Id == "pattern_node");
        Assert.That(patternNode.Values, Is.EqualTo(new[] { 1, 2, 3, 1, 2, 3, 1 }));
    }

    [Test]
    public void ParameterProcessor_WithTypeValidation_RejectsInvalidTypes()
    {
        // Arrange
        var template = CreateTemplateWithNumberParameter();
        var invalidParameters = new Dictionary<string, object> 
        { 
            ["numberParam"] = "not_a_number" 
        };

        // Act & Assert
        Assert.Throws<ParameterValidationException>(
            () => ParameterProcessor.Process(template, invalidParameters));
    }

    [TestCase(50)]
    [TestCase(100)] 
    [TestCase(200)]
    public void ParameterProcessor_WithRangeValidation_AcceptsValidValues(int value)
    {
        // Test min/max constraint validation
        var template = CreateTemplateWithRangeConstraints(min: 1, max: 1000);
        var parameters = new Dictionary<string, object> { ["capacity"] = value };

        // Should not throw
        Assert.DoesNotThrow(() => ParameterProcessor.Process(template, parameters));
    }

    [TestCase(-1)]
    [TestCase(1001)]
    public void ParameterProcessor_WithRangeValidation_RejectsInvalidValues(int value)
    {
        // Test constraint violation
        var template = CreateTemplateWithRangeConstraints(min: 1, max: 1000);
        var parameters = new Dictionary<string, object> { ["capacity"] = value };

        Assert.Throws<ParameterValidationException>(
            () => ParameterProcessor.Process(template, parameters));
    }
}
```

### 3. PMF Validation Tests

Ensure probability mass functions are structurally valid. Note: PMF semantic validation (normalization, grid alignment) is handled by the Engine during compilation, but FlowTime-Sim can perform basic structural checks.

```csharp
[TestFixture]
public class PmfValidationTests
{
    [Test]
    public void PmfValidator_WithValidProbabilities_PassesValidation()
    {
        // Arrange
        var pmfNode = new NodeSpec
        {
            Id = "reliability",
            Kind = "pmf",
            Pmf = new PmfSpec
            {
                Values = new[] { 1.0, 0.8, 0.5, 0.0 },
                Probabilities = new[] { 0.7, 0.2, 0.08, 0.02 } // Sum = 1.0
            }
        };

        // Act
        var result = PmfValidator.Validate(pmfNode);

        // Assert
        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void PmfValidator_WithInvalidSum_FailsValidation()
    {
        // Arrange - probabilities sum to 0.99 (outside tolerance)
        var pmfNode = new NodeSpec
        {
            Id = "reliability", 
            Kind = "pmf",
            Pmf = new PmfSpec
            {
                Values = new[] { 1.0, 0.5 },
                Probabilities = new[] { 0.5, 0.49 } // Sum = 0.99
            }
        };

        // Act
        var result = PmfValidator.Validate(pmfNode);

        // Assert  
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors.Any(e => e.Message.Contains("sum to 1.0")), Is.True);
    }

    [TestCase(1.0)] // Exactly 1.0
    [TestCase(1.001)] // Within positive tolerance  
    [TestCase(0.999)] // Within negative tolerance
    public void PmfValidator_WithToleranceRange_PassesValidation(double totalProbability)
    {
        // Test ±0.001 tolerance for floating-point precision
        var pmfNode = CreatePmfWithTotalProbability(totalProbability);
        var result = PmfValidator.Validate(pmfNode);
        
        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void PmfValidator_WithMismatchedArrayLengths_FailsValidation()
    {
        // Arrange
        var pmfNode = new NodeSpec
        {
            Id = "test",
            Kind = "pmf", 
            Pmf = new PmfSpec
            {
                Values = new[] { 1.0, 0.5, 0.0 }, // Length 3
                Probabilities = new[] { 0.7, 0.3 } // Length 2
            }
        };

        // Act
        var result = PmfValidator.Validate(pmfNode);

        // Assert
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors.Any(e => e.Message.Contains("same length")), Is.True);
    }
}
```

### 4. RNG Validation Tests

Validate random number generator configuration for Engine PMF compilation.

```csharp
[TestFixture]
public class RngValidationTests
{
    [Test]
    public void RngValidator_WithValidPcg32Config_PassesValidation()
    {
        // Arrange
        var template = new TemplateSpec
        {
            Metadata = new TemplateMetadata { Id = "test", Title = "Test" },
            Grid = new GridSpec { Bins = 12, BinSize = 1, BinUnit = "hours" },
            Nodes = new[] { new NodeSpec { Id = "test", Kind = "const", Values = new[] { 1.0 } } },
            Outputs = new[] { new OutputSpec { Series = "test", Filename = "test.csv" } },
            Rng = new RngSpec 
            { 
                Kind = "pcg32", 
                Seed = 12345 
            }
        };

        // Act
        var result = TemplateValidator.Validate(template);

        // Assert
        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void RngValidator_WithInvalidSeed_FailsValidation()
    {
        // Arrange
        var template = new TemplateSpec
        {
            Metadata = new TemplateMetadata { Id = "test", Title = "Test" },
            Grid = new GridSpec { Bins = 12, BinSize = 1, BinUnit = "hours" },
            Nodes = new[] { new NodeSpec { Id = "test", Kind = "const", Values = new[] { 1.0 } } },
            Outputs = new[] { new OutputSpec { Series = "test", Filename = "test.csv" } },
            Rng = new RngSpec 
            { 
                Kind = "pcg32", 
                Seed = -1  // Invalid: negative seed
            }
        };

        // Act
        var result = TemplateValidator.Validate(template);

        // Assert
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors.Any(e => e.Message.Contains("seed")), Is.True);
    }

    [Test]
    public void RngValidator_WithUnsupportedKind_FailsValidation()
    {
        // Arrange
        var template = new TemplateSpec
        {
            Metadata = new TemplateMetadata { Id = "test", Title = "Test" },
            Grid = new GridSpec { Bins = 12, BinSize = 1, BinUnit = "hours" },
            Nodes = new[] { new NodeSpec { Id = "test", Kind = "const", Values = new[] { 1.0 } } },
            Outputs = new[] { new OutputSpec { Series = "test", Filename = "test.csv" } },
            Rng = new RngSpec 
            { 
                Kind = "legacy",  // Invalid: only pcg32 supported
                Seed = 12345 
            }
        };

        // Act
        var result = TemplateValidator.Validate(template);

        // Assert
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors.Any(e => e.Message.Contains("pcg32")), Is.True);
    }

    [Test]
    public void RngValidator_WithoutRngSection_PassesValidation()
    {
        // Arrange - RNG section is optional
        var template = new TemplateSpec
        {
            Metadata = new TemplateMetadata { Id = "test", Title = "Test" },
            Grid = new GridSpec { Bins = 12, BinSize = 1, BinUnit = "hours" },
            Nodes = new[] { new NodeSpec { Id = "test", Kind = "const", Values = new[] { 1.0 } } },
            Outputs = new[] { new OutputSpec { Series = "test", Filename = "test.csv" } },
            Rng = null  // Optional section - Engine will use default if PMF nodes present
        };

        // Act
        var result = TemplateValidator.Validate(template);

        // Assert
        Assert.That(result.IsValid, Is.True);
    }
}
```

### 5. Expression Validation Tests

Validate expression syntax and node references.

```csharp
[TestFixture]
public class ExpressionValidationTests
{
    [Test]
    public void ExpressionValidator_WithValidNodeReferences_PassesValidation()
    {
        // Arrange
        var template = new TemplateSpec
        {
            Nodes = new List<NodeSpec>
            {
                new() { Id = "requests", Kind = "const", Values = new[] { 100 } },
                new() { Id = "capacity", Kind = "const", Values = new[] { 200 } },
                new() { Id = "processed", Kind = "expr", Expr = "MIN(requests, capacity)" }
            }
        };

        // Act
        var result = ExpressionValidator.ValidateReferences(template);

        // Assert
        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void ExpressionValidator_WithUnknownNodeReference_FailsValidation()
    {
        // Arrange
        var template = new TemplateSpec
        {
            Nodes = new List<NodeSpec>
            {
                new() { Id = "requests", Kind = "const", Values = new[] { 100 } },
                new() { Id = "processed", Kind = "expr", Expr = "MIN(requests, unknown_node)" }
            }
        };

        // Act
        var result = ExpressionValidator.ValidateReferences(template);

        // Assert
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors.Any(e => e.Message.Contains("unknown_node")), Is.True);
    }

    [TestCase("MIN(a, b)")]
    [TestCase("MAX(a, b, c)")]
    [TestCase("SUM(a)")]
    [TestCase("AVG(a, b)")]
    [TestCase("a + b * c")]
    [TestCase("(a + b) / MAX(c, 1)")]
    public void ExpressionValidator_WithValidSyntax_PassesValidation(string expression)
    {
        // Test various valid expression patterns
        var result = ExpressionValidator.ValidateSyntax(expression);
        Assert.That(result.IsValid, Is.True);
    }

    [TestCase("MIN()")] // Missing arguments
    [TestCase("INVALID_FUNC(a)")] // Unknown function
    [TestCase("a +")] // Incomplete expression
    [TestCase("(a + b")] // Unbalanced parentheses
    public void ExpressionValidator_WithInvalidSyntax_FailsValidation(string expression)
    {
        // Test syntax error detection
        var result = ExpressionValidator.ValidateSyntax(expression);
        Assert.That(result.IsValid, Is.False);
    }
}
```

### 6. Circular Dependency Tests

Ensure DAG structure has no cycles.

```csharp
[TestFixture]
public class CircularDependencyTests
{
    [Test]
    public void DependencyAnalyzer_WithValidDAG_PassesValidation()
    {
        // Arrange - Linear dependency chain
        var template = new TemplateSpec
        {
            Nodes = new List<NodeSpec>
            {
                new() { Id = "a", Kind = "const", Values = new[] { 1 } },
                new() { Id = "b", Kind = "expr", Expr = "a * 2" },
                new() { Id = "c", Kind = "expr", Expr = "b + 10" }
            }
        };

        // Act
        var result = DependencyAnalyzer.ValidateNoCycles(template);

        // Assert
        Assert.That(result.HasCycles, Is.False);
    }

    [Test]
    public void DependencyAnalyzer_WithDirectCycle_DetectsCycle()
    {
        // Arrange - Direct circular dependency
        var template = new TemplateSpec
        {
            Nodes = new List<NodeSpec>
            {
                new() { Id = "a", Kind = "expr", Expr = "b + 1" },
                new() { Id = "b", Kind = "expr", Expr = "a * 2" }
            }
        };

        // Act
        var result = DependencyAnalyzer.ValidateNoCycles(template);

        // Assert
        Assert.That(result.HasCycles, Is.True);
        Assert.That(result.CyclicNodes, Contains.Item("a"));
        Assert.That(result.CyclicNodes, Contains.Item("b"));
    }

    [Test]
    public void DependencyAnalyzer_WithIndirectCycle_DetectsCycle()
    {
        // Arrange - Indirect cycle: a → b → c → a
        var template = new TemplateSpec
        {
            Nodes = new List<NodeSpec>
            {
                new() { Id = "a", Kind = "expr", Expr = "c + 1" },
                new() { Id = "b", Kind = "expr", Expr = "a * 2" },
                new() { Id = "c", Kind = "expr", Expr = "b / 3" }
            }
        };

        // Act
        var result = DependencyAnalyzer.ValidateNoCycles(template);

        // Assert
        Assert.That(result.HasCycles, Is.True);
    }
}
```

### 7. Integration Tests

Test complete template processing pipeline.

```csharp
[TestFixture]
public class TemplateIntegrationTests
{
    [Test]
    public async Task TemplateService_WithCompleteTemplate_GeneratesValidModel()
    {
        // Arrange
        var template = LoadTestTemplate("it-system-microservices");
        var parameters = new Dictionary<string, object>
        {
            ["bins"] = 12,
            ["requestPattern"] = new[] { 100, 150, 200 },
            ["capacity"] = 250
        };

        // Act
        var result = await _templateService.GenerateModelAsync(template.Id, parameters);

        // Assert
        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.GeneratedModel, Is.Not.Empty);
        
        // Validate FlowTime Engine compatibility
        var engineModel = result.GeneratedModel;
        Assert.That(engineModel, Contains.Substring("schemaVersion: 1"));
        Assert.That(engineModel, Contains.Substring("grid:"));
        Assert.That(engineModel, Contains.Substring("arrivals:"));
    }

    [Test]
    public async Task TemplateService_WithParameterValidationError_ReturnsError()
    {
        // Arrange
        var template = LoadTestTemplate("simple-template");
        var invalidParameters = new Dictionary<string, object>
        {
            ["capacity"] = -1 // Violates min constraint
        };

        // Act
        var result = await _templateService.GenerateModelAsync(template.Id, invalidParameters);

        // Assert
        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Errors.Any(e => e.Message.Contains("capacity")), Is.True);
    }

    [Test]
    public async Task TemplateService_WithAllNodeTypes_ProcessesCorrectly()
    {
        // Arrange - Template with const, pmf, and expr nodes
        var template = CreateComplexTestTemplate();
        var parameters = GetValidParametersFor(template);

        // Act
        var result = await _templateService.GenerateModelAsync(template.Id, parameters);

        // Assert
        Assert.That(result.IsSuccess, Is.True);
        
        // Verify all node types were processed
        var processedTemplate = result.ProcessedTemplate;
        Assert.That(processedTemplate.Nodes.Any(n => n.Kind == "const"), Is.True);
        Assert.That(processedTemplate.Nodes.Any(n => n.Kind == "pmf"), Is.True);
        Assert.That(processedTemplate.Nodes.Any(n => n.Kind == "expr"), Is.True);
    }
}
```

### 8. Performance Tests

Ensure template processing remains fast for UI use.

```csharp
[TestFixture]
public class TemplatePerformanceTests
{
    [Test]
    public async Task TemplateProcessing_WithLargeParameters_CompletesWithinTimeLimit()
    {
        // Arrange
        var template = CreateLargeTemplate(nodeCount: 50, outputCount: 20);
        var parameters = CreateLargeParameterSet(paramCount: 100);

        // Act & Assert
        var stopwatch = Stopwatch.StartNew();
        var result = await _templateService.GenerateModelAsync(template.Id, parameters);
        stopwatch.Stop();

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(1000)); // Under 1 second
    }

    [Test]
    public void ParameterSubstitution_WithLargeArrays_HandlesEfficiently()
    {
        // Test array cycling performance with large datasets
        var template = CreateTemplateWithLargeArray();
        var parameters = new Dictionary<string, object>
        {
            ["largePattern"] = Enumerable.Range(1, 10000).ToArray(),
            ["bins"] = 50000
        };

        var stopwatch = Stopwatch.StartNew();
        var result = ParameterProcessor.Process(template, parameters);
        stopwatch.Stop();

        Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(500));
    }
}
```

## Test Utilities

### Template Builders

```csharp
public static class TemplateTestBuilder
{
    public static TemplateSpec CreateMinimalTemplate(string id = "test-template")
    {
        return new TemplateSpec
        {
            Metadata = new MetadataSpec { Id = id, Title = "Test Template" },
            Grid = new GridSpec { Bins = 12, BinSize = 60, BinUnit = "minutes" },
            Nodes = new List<NodeSpec>
            {
                new() { Id = "test_node", Kind = "const", Values = new[] { 100 } }
            },
            Outputs = new List<OutputSpec>
            {
                new() { Series = "test_node", Filename = "test.csv" }
            }
        };
    }

    public static TemplateSpec WithParameter(this TemplateSpec template, string name, string type, object defaultValue)
    {
        template.Parameters.Add(new ParameterSpec
        {
            Name = name,
            Type = type,
            Default = defaultValue
        });
        return template;
    }

    public static TemplateSpec WithNode(this TemplateSpec template, string id, string kind, object definition)
    {
        var node = new NodeSpec { Id = id, Kind = kind };
        
        switch (kind)
        {
            case "const":
                node.Values = definition;
                break;
            case "pmf":
                node.Pmf = (PmfSpec)definition;
                break;
            case "expr":
                node.Expr = (string)definition;
                break;
        }
        
        template.Nodes.Add(node);
        return template;
    }
}
```

### Test Data Helpers

```csharp
public static class TestDataHelper
{
    public static Dictionary<string, object> CreateValidParameters()
    {
        return new Dictionary<string, object>
        {
            ["bins"] = 12,
            ["capacity"] = 100,
            ["pattern"] = new[] { 10, 20, 30 }
        };
    }

    public static PmfSpec CreateValidPmf()
    {
        return new PmfSpec
        {
            Values = new[] { 1.0, 0.8, 0.5, 0.0 },
            Probabilities = new[] { 0.7, 0.2, 0.08, 0.02 }
        };
    }
}
```

## Testing Best Practices

1. **Test Template Validity First**: Always validate schema before testing logic
2. **Use Realistic Data**: Test with domain-appropriate values and constraints
3. **Test Edge Cases**: Minimum/maximum values, empty arrays, zero values
4. **Validate PMF Mathematics**: Always check probability sums in tests
5. **Test Performance**: Ensure real-time UI responsiveness
6. **Integration Testing**: Test complete template-to-model pipeline
7. **Error Path Testing**: Verify graceful handling of invalid inputs

This comprehensive testing approach ensures template reliability and maintains the quality standards expected for FlowTime-Sim template assets.