using System.Diagnostics;
using FlowTime.Core;
using FlowTime.Core.Models;
using Xunit;
using Xunit.Abstractions;

namespace FlowTime.Tests.Performance;

/// <summary>
/// Performance tests for M1.5 Expression Language implementation.
/// These tests help ensure the implementation scales well and identify potential bottlenecks.
/// </summary>
public class M15PerformanceTests
{
    private readonly ITestOutputHelper output;

    public M15PerformanceTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Fact]
    public void Test_SmallScale_Performance()
    {
        // Baseline: 10 nodes, 100 bins
        var (parseTime, evalTime, memory) = RunPerformanceTest(10, 100);
        
        output.WriteLine($"SMALL SCALE (10 nodes, 100 bins):");
        output.WriteLine($"  Parse Time: {parseTime:F2}ms");
        output.WriteLine($"  Eval Time:  {evalTime:F2}ms"); 
        output.WriteLine($"  Memory:     {memory:F2}MB");
        output.WriteLine($"  Total:      {parseTime + evalTime:F2}ms");
        
        // Sanity checks - these should be very fast
        Assert.True(parseTime < 50, $"Parse time {parseTime}ms too slow for 10 nodes");
        Assert.True(evalTime < 50, $"Eval time {evalTime}ms too slow for 10 nodes");
        Assert.True(memory < 10, $"Memory usage {memory}MB too high for 10 nodes");
    }

    [Fact]
    public void Test_MediumScale_Performance()
    {
        // Medium scale: 100 nodes, 1000 bins
        var (parseTime, evalTime, memory) = RunPerformanceTest(100, 1000);
        
        output.WriteLine($"MEDIUM SCALE (100 nodes, 1000 bins):");
        output.WriteLine($"  Parse Time: {parseTime:F2}ms");
        output.WriteLine($"  Eval Time:  {evalTime:F2}ms");
        output.WriteLine($"  Memory:     {memory:F2}MB");
        output.WriteLine($"  Total:      {parseTime + evalTime:F2}ms");
        
        // Should still be reasonable
        Assert.True(parseTime < 500, $"Parse time {parseTime}ms too slow for 100 nodes");
        Assert.True(evalTime < 1000, $"Eval time {evalTime}ms too slow for 100 nodes");
        Assert.True(memory < 100, $"Memory usage {memory}MB too high for 100 nodes");
    }

    [Fact]
    public void Test_LargeScale_Performance()
    {
        // Large scale: 1000 nodes, 1000 bins
        var (parseTime, evalTime, memory) = RunPerformanceTest(1000, 1000);
        
        output.WriteLine($"LARGE SCALE (1000 nodes, 1000 bins):");
        output.WriteLine($"  Parse Time: {parseTime:F2}ms");
        output.WriteLine($"  Eval Time:  {evalTime:F2}ms");
        output.WriteLine($"  Memory:     {memory:F2}MB");
        output.WriteLine($"  Total:      {parseTime + evalTime:F2}ms");
        
        // This might be slower but should still be reasonable
        Assert.True(parseTime < 5000, $"Parse time {parseTime}ms too slow for 1000 nodes");
        Assert.True(evalTime < 10000, $"Eval time {evalTime}ms too slow for 1000 nodes");
        Assert.True(memory < 500, $"Memory usage {memory}MB too high for 1000 nodes");
    }

    [Fact]
    public void Test_ExtremeScale_Performance()
    {
        // Extreme scale: 10000 nodes, 100 bins (fewer bins to keep test reasonable)
        var (parseTime, evalTime, memory) = RunPerformanceTest(10000, 100);
        
        output.WriteLine($"EXTREME SCALE (10000 nodes, 100 bins):");
        output.WriteLine($"  Parse Time: {parseTime:F2}ms");
        output.WriteLine($"  Eval Time:  {evalTime:F2}ms");
        output.WriteLine($"  Memory:     {memory:F2}MB");
        output.WriteLine($"  Total:      {parseTime + evalTime:F2}ms");
        
        // This will be slower but should still complete
        Assert.True(parseTime < 30000, $"Parse time {parseTime}ms too slow for 10000 nodes");
        Assert.True(evalTime < 30000, $"Eval time {evalTime}ms too slow for 10000 nodes");
        Assert.True(memory < 1000, $"Memory usage {memory}MB too high for 10000 nodes");
    }

    [Fact]
    public void Test_ExpressionType_Performance()
    {
        // Compare performance of different expression types
        var nodeCount = 100;
        var bins = 1000;

        // Simple expressions (node * scalar)
        var simple = RunExpressionTypeTest(nodeCount, bins, i => $"base_{i % 10} * 1.5");
        
        // Complex expressions (with functions)
        var complex = RunExpressionTypeTest(nodeCount, bins, i => $"MIN(base_{i % 10} * 2, base_{(i+1) % 10})");
        
        // SHIFT expressions (temporal)
        var shift = RunExpressionTypeTest(nodeCount, bins, i => $"base_{i % 10} + SHIFT(base_{(i+1) % 10}, 1)");

        output.WriteLine($"EXPRESSION TYPE COMPARISON ({nodeCount} nodes, {bins} bins):");
        output.WriteLine($"  Simple:  Parse={simple.parseTime:F2}ms, Eval={simple.evalTime:F2}ms");
        output.WriteLine($"  Complex: Parse={complex.parseTime:F2}ms, Eval={complex.evalTime:F2}ms");
        output.WriteLine($"  SHIFT:   Parse={shift.parseTime:F2}ms, Eval={shift.evalTime:F2}ms");
        
        // SHIFT should be reasonably close to others (not orders of magnitude slower)
        Assert.True(shift.evalTime < simple.evalTime * 5, "SHIFT expressions too slow compared to simple");
        Assert.True(complex.evalTime < simple.evalTime * 3, "Complex expressions too slow compared to simple");
    }

    private (double parseTime, double evalTime, double memoryMB) RunPerformanceTest(int nodeCount, int bins)
    {
        return RunExpressionTypeTest(nodeCount, bins, i => $"base_{i % 10} + {i * 0.1:F1}");
    }

    private (double parseTime, double evalTime, double memoryMB) RunExpressionTypeTest(int nodeCount, int bins, Func<int, string> exprGenerator)
    {
        // Create a model with many nodes
        var model = new ModelDefinition
        {
            Grid = new GridDefinition { Bins = bins, BinMinutes = 60 },
            Nodes = new List<NodeDefinition>(),
            Outputs = new List<OutputDefinition>()
        };

        // Create base const nodes (10 of them)
        for (int i = 0; i < 10; i++)
        {
            var values = new double[bins];
            for (int b = 0; b < bins; b++)
            {
                values[b] = 10 + i + (b * 0.1);
            }
            
            model.Nodes.Add(new NodeDefinition
            {
                Id = $"base_{i}",
                Kind = "const",
                Values = values
            });
        }

        // Create many expression nodes that reference the base nodes
        for (int i = 0; i < nodeCount; i++)
        {
            model.Nodes.Add(new NodeDefinition
            {
                Id = $"expr_{i}",
                Kind = "expr",
                Expr = exprGenerator(i)
            });
        }

        // Add a few outputs
        for (int i = 0; i < Math.Min(5, nodeCount); i++)
        {
            model.Outputs.Add(new OutputDefinition
            {
                Series = $"expr_{i}",
                As = $"output_{i}"
            });
        }

        // Measure memory before
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var memoryBefore = GC.GetTotalMemory(false);

        // Measure parse time
        var parseStopwatch = Stopwatch.StartNew();
        var (grid, graph) = ModelParser.ParseModel(model);
        parseStopwatch.Stop();

        // Measure evaluation time
        var evalStopwatch = Stopwatch.StartNew();
        var order = graph.TopologicalOrder();
        var results = graph.Evaluate(grid);
        evalStopwatch.Stop();

        // Measure memory after
        var memoryAfter = GC.GetTotalMemory(false);
        var memoryUsedMB = (memoryAfter - memoryBefore) / (1024.0 * 1024.0);

        return (
            parseStopwatch.Elapsed.TotalMilliseconds,
            evalStopwatch.Elapsed.TotalMilliseconds,
            memoryUsedMB
        );
    }

    [Fact]
    public void Test_Grid_Size_Scaling()
    {
        // Test how performance scales with grid size (keeping nodes constant)
        var nodeCount = 100;
        
        var sizes = new[] { 100, 500, 1000, 5000 };
        
        output.WriteLine($"GRID SIZE SCALING ({nodeCount} nodes):");
        
        foreach (var bins in sizes)
        {
            var (parseTime, evalTime, memory) = RunPerformanceTest(nodeCount, bins);
            var timePerBin = evalTime / bins;
            
            output.WriteLine($"  {bins,4} bins: Parse={parseTime:F1}ms, Eval={evalTime:F1}ms ({timePerBin:F3}ms/bin), Memory={memory:F1}MB");
            
            // Evaluation time should scale roughly linearly with bins
            Assert.True(timePerBin < 1.0, $"Evaluation time per bin {timePerBin:F3}ms too high for {bins} bins");
        }
    }

    [Fact]
    public void Test_Node_Count_Scaling()
    {
        // Test how performance scales with node count (keeping grid size constant)
        var bins = 1000;
        
        var counts = new[] { 10, 50, 100, 500, 1000 };
        
        output.WriteLine($"NODE COUNT SCALING ({bins} bins):");
        
        foreach (var nodeCount in counts)
        {
            var (parseTime, evalTime, memory) = RunPerformanceTest(nodeCount, bins);
            var timePerNode = (parseTime + evalTime) / nodeCount;
            
            output.WriteLine($"  {nodeCount,4} nodes: Parse={parseTime:F1}ms, Eval={evalTime:F1}ms ({timePerNode:F3}ms/node), Memory={memory:F1}MB");
            
            // Should scale reasonably with node count
            Assert.True(timePerNode < 10.0, $"Time per node {timePerNode:F3}ms too high for {nodeCount} nodes");
        }
    }
}
