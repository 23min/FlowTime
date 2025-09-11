using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Filters;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Order;
using BenchmarkDotNet.Running;
using FlowTime.Core;
using FlowTime.Core.Models;
using System.Text;

namespace FlowTime.Tests.Performance;

/// <summary>
/// BenchmarkDotNet-based performance tests for M1.6 (M1.5 + BenchmarkDotNet infrastructure).
/// Provides reliable, statistically rigorous performance measurements with proper warmup and JIT compilation.
/// This represents the baseline benchmark capabilities established in M1.6 milestone.
/// </summary>
[MemoryDiagnoser]
[RankColumn]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[Config(typeof(Config))]
public class M16BenchmarkDotNetTests
{
    private class Config : ManualConfig
    {
        public Config()
        {
            AddJob(Job.Default
                .WithStrategy(RunStrategy.Throughput)
                .WithWarmupCount(3)     // 3 warmup iterations to ensure JIT compilation
                .WithIterationCount(10) // 10 measurement iterations for statistical reliability
                .WithLaunchCount(1)     // Single process to reduce noise
                .WithUnrollFactor(1)    // Single invocation per iteration for timing accuracy
            );
            
            AddColumn(StatisticColumn.Mean);
            AddColumn(StatisticColumn.Error);
            AddColumn(StatisticColumn.StdDev);
            AddColumn(StatisticColumn.Median);
        }
    }

    private ModelDefinition? _smallScaleModel;
    private ModelDefinition? _mediumScaleModel;
    private ModelDefinition? _largeScaleModel;
    private ModelDefinition? _simpleExpressionModel;
    private ModelDefinition? _complexExpressionModel;
    private ModelDefinition? _shiftExpressionModel;

    [GlobalSetup]
    public void Setup()
    {
        // Pre-generate test data to avoid including generation time in benchmarks
        _smallScaleModel = GenerateModel(10, 100, i => $"base_{i % 5} * 1.5");
        _mediumScaleModel = GenerateModel(100, 1000, i => $"base_{i % 10} * 2.0");
        _largeScaleModel = GenerateModel(1000, 1000, i => $"base_{i % 20} + 1.0");
        
        // Expression type comparison models (same size for fair comparison)
        _simpleExpressionModel = GenerateModel(100, 1000, i => $"base_{i % 10} * 1.5");
        _complexExpressionModel = GenerateModel(100, 1000, i => $"MIN(base_{i % 10} * 2, base_{(i+1) % 10})");
        _shiftExpressionModel = GenerateModel(100, 1000, i => $"base_{i % 10} + SHIFT(base_{(i+1) % 10}, 1)");
    }

    // ===== SCALE BENCHMARKS =====

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Scale")]
    public object SmallScale_Parse()
    {
        return ModelParser.ParseModel(_smallScaleModel!);
    }

    [Benchmark]
    [BenchmarkCategory("Scale")]
    public object MediumScale_Parse()
    {
        return ModelParser.ParseModel(_mediumScaleModel!);
    }

    [Benchmark]
    [BenchmarkCategory("Scale")]
    public object LargeScale_Parse()
    {
        return ModelParser.ParseModel(_largeScaleModel!);
    }

    [Benchmark]
    [BenchmarkCategory("Scale")]
    public object SmallScale_Evaluate()
    {
        var (grid, graph) = ModelParser.ParseModel(_smallScaleModel!);
        var order = graph.TopologicalOrder();
        return graph.Evaluate(grid);
    }

    [Benchmark]
    [BenchmarkCategory("Scale")]
    public object MediumScale_Evaluate()
    {
        var (grid, graph) = ModelParser.ParseModel(_mediumScaleModel!);
        var order = graph.TopologicalOrder();
        return graph.Evaluate(grid);
    }

    [Benchmark]
    [BenchmarkCategory("Scale")]
    public object LargeScale_Evaluate()
    {
        var (grid, graph) = ModelParser.ParseModel(_largeScaleModel!);
        var order = graph.TopologicalOrder();
        return graph.Evaluate(grid);
    }

    // ===== EXPRESSION TYPE BENCHMARKS =====

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("ExpressionType")]
    public object SimpleExpression_Parse()
    {
        return ModelParser.ParseModel(_simpleExpressionModel!);
    }

    [Benchmark]
    [BenchmarkCategory("ExpressionType")]
    public object ComplexExpression_Parse()
    {
        return ModelParser.ParseModel(_complexExpressionModel!);
    }

    [Benchmark]
    [BenchmarkCategory("ExpressionType")]
    public object ShiftExpression_Parse()
    {
        return ModelParser.ParseModel(_shiftExpressionModel!);
    }

    [Benchmark]
    [BenchmarkCategory("ExpressionType")]
    public object SimpleExpression_Evaluate()
    {
        var (grid, graph) = ModelParser.ParseModel(_simpleExpressionModel!);
        var order = graph.TopologicalOrder();
        return graph.Evaluate(grid);
    }

    [Benchmark]
    [BenchmarkCategory("ExpressionType")]
    public object ComplexExpression_Evaluate()
    {
        var (grid, graph) = ModelParser.ParseModel(_complexExpressionModel!);
        var order = graph.TopologicalOrder();
        return graph.Evaluate(grid);
    }

    [Benchmark]
    [BenchmarkCategory("ExpressionType")]
    public object ShiftExpression_Evaluate()
    {
        var (grid, graph) = ModelParser.ParseModel(_shiftExpressionModel!);
        var order = graph.TopologicalOrder();
        return graph.Evaluate(grid);
    }

    // ===== END-TO-END BENCHMARKS =====

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("EndToEnd")]
    public object SmallScale_Complete()
    {
        var (grid, graph) = ModelParser.ParseModel(_smallScaleModel!);
        var order = graph.TopologicalOrder();
        return graph.Evaluate(grid);
    }

    [Benchmark]
    [BenchmarkCategory("EndToEnd")]
    public object MediumScale_Complete()
    {
        var (grid, graph) = ModelParser.ParseModel(_mediumScaleModel!);
        var order = graph.TopologicalOrder();
        return graph.Evaluate(grid);
    }

    [Benchmark]
    [BenchmarkCategory("EndToEnd")]
    public object LargeScale_Complete()
    {
        var (grid, graph) = ModelParser.ParseModel(_largeScaleModel!);
        var order = graph.TopologicalOrder();
        return graph.Evaluate(grid);
    }

    // ===== HELPER METHODS =====

    private static ModelDefinition GenerateModel(int nodeCount, int bins, Func<int, string> expressionGenerator)
    {
        var model = new ModelDefinition
        {
            Grid = new GridDefinition { Bins = bins, BinMinutes = 60 },
            Nodes = new List<NodeDefinition>(),
            Outputs = new List<OutputDefinition>()
        };

        // Create base distributions (referenced by expressions)
        // Ensure we create enough base nodes for all possible references
        int baseNodeCount = Math.Max(20, nodeCount / 2); // Generous allocation for base nodes
        for (int i = 0; i < baseNodeCount; i++)
        {
            var values = new double[bins];
            for (int b = 0; b < bins; b++)
            {
                values[b] = i + 1.0 + (b * 0.01);
            }
            
            model.Nodes.Add(new NodeDefinition
            {
                Id = $"base_{i}",
                Kind = "const",
                Values = values
            });
        }

        // Create expression nodes
        for (int i = 0; i < nodeCount; i++)
        {
            model.Nodes.Add(new NodeDefinition
            {
                Id = $"node_{i}",
                Kind = "expr",
                Expr = expressionGenerator(i)
            });
        }

        // Add some outputs for completeness
        for (int i = 0; i < Math.Min(5, nodeCount); i++)
        {
            model.Outputs.Add(new OutputDefinition
            {
                Series = $"node_{i}",
                As = $"output_{i}"
            });
        }

        return model;
    }
}

/// <summary>
/// Console runner for M1.5 benchmarks. 
/// Use this class to run specific benchmark categories.
/// </summary>
public static class M15BenchmarkRunner
{
    public static void RunAllBenchmarks()
    {
        BenchmarkRunner.Run<M16BenchmarkDotNetTests>();
    }

    public static void RunScaleBenchmarks()
    {
        BenchmarkRunner.Run<M16BenchmarkDotNetTests>(
            DefaultConfig.Instance.AddFilter(new BenchmarkCategoryFilter("Scale")));
    }

    public static void RunExpressionTypeBenchmarks()
    {
        BenchmarkRunner.Run<M16BenchmarkDotNetTests>(
            DefaultConfig.Instance.AddFilter(new BenchmarkCategoryFilter("ExpressionType")));
    }

    public static void RunEndToEndBenchmarks()
    {
        BenchmarkRunner.Run<M16BenchmarkDotNetTests>(
            DefaultConfig.Instance.AddFilter(new BenchmarkCategoryFilter("EndToEnd")));
    }
}

/// <summary>
/// Simple category filter for running specific benchmark groups.
/// </summary>
public class BenchmarkCategoryFilter : BenchmarkDotNet.Filters.IFilter
{
    private readonly string _category;

    public BenchmarkCategoryFilter(string category)
    {
        _category = category;
    }

    public bool Predicate(BenchmarkCase benchmarkCase)
    {
        return benchmarkCase.Descriptor.HasCategory(_category);
    }
}
