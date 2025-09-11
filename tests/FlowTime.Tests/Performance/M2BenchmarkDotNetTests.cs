using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Filters;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Order;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Reports;
using FlowTime.Core;
using FlowTime.Core.Models;
using System.Text;

namespace FlowTime.Tests.Performance;

/// <summary>
/// BenchmarkDotNet-based performance tests for M2 PMF implementation.
/// Provides reliable, statistically rigorous performance measurements comparing PMF vs constant node performance.
/// </summary>
[MemoryDiagnoser]
[RankColumn]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[Config(typeof(Config))]
public class M2BenchmarkDotNetTests
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

    private ModelDefinition? _smallConstModel;
    private ModelDefinition? _smallPmfModel;
    private ModelDefinition? _mediumConstModel;
    private ModelDefinition? _mediumPmfModel;
    private ModelDefinition? _smallPmfSimpleModel;
    private ModelDefinition? _smallPmfComplexModel;

    [GlobalSetup]
    public void Setup()
    {
        // Pre-generate test data to avoid including generation time in benchmarks
        
        // Scale comparison models
        _smallConstModel = GenerateConstModel(50, 100);
        _smallPmfModel = GeneratePmfModel(50, 100, CreateSmallPmf);
        _mediumConstModel = GenerateConstModel(200, 500);
        _mediumPmfModel = GeneratePmfModel(200, 500, CreateSmallPmf);
        
        // PMF complexity comparison models (same size for fair comparison)
        _smallPmfSimpleModel = GeneratePmfModel(50, 100, CreateSmallPmf);
        _smallPmfComplexModel = GeneratePmfModel(50, 100, CreateComplexPmf);
    }

    // ===== PMF VS CONST BASELINE BENCHMARKS =====

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("PmfVsConst")]
    public object SmallScale_Const_Parse()
    {
        return ModelParser.ParseModel(_smallConstModel!);
    }

    [Benchmark]
    [BenchmarkCategory("PmfVsConst")]
    public object SmallScale_Pmf_Parse()
    {
        return ModelParser.ParseModel(_smallPmfModel!);
    }

    [Benchmark]
    [BenchmarkCategory("PmfVsConst")]
    public object MediumScale_Const_Parse()
    {
        return ModelParser.ParseModel(_mediumConstModel!);
    }

    [Benchmark]
    [BenchmarkCategory("PmfVsConst")]
    public object MediumScale_Pmf_Parse()
    {
        return ModelParser.ParseModel(_mediumPmfModel!);
    }

    [Benchmark]
    [BenchmarkCategory("PmfVsConst")]
    public object SmallScale_Const_Evaluate()
    {
        var (grid, graph) = ModelParser.ParseModel(_smallConstModel!);
        return graph.Evaluate(grid);
    }

    [Benchmark]
    [BenchmarkCategory("PmfVsConst")]
    public object SmallScale_Pmf_Evaluate()
    {
        var (grid, graph) = ModelParser.ParseModel(_smallPmfModel!);
        return graph.Evaluate(grid);
    }

    [Benchmark]
    [BenchmarkCategory("PmfVsConst")]
    public object MediumScale_Const_Evaluate()
    {
        var (grid, graph) = ModelParser.ParseModel(_mediumConstModel!);
        return graph.Evaluate(grid);
    }

    [Benchmark]
    [BenchmarkCategory("PmfVsConst")]
    public object MediumScale_Pmf_Evaluate()
    {
        var (grid, graph) = ModelParser.ParseModel(_mediumPmfModel!);
        return graph.Evaluate(grid);
    }

    // ===== PMF COMPLEXITY BENCHMARKS =====

    [Benchmark]
    [BenchmarkCategory("PmfComplexity")]
    public object SimplePmf_Parse()
    {
        return ModelParser.ParseModel(_smallPmfSimpleModel!);
    }

    [Benchmark]
    [BenchmarkCategory("PmfComplexity")]
    public object ComplexPmf_Parse()
    {
        return ModelParser.ParseModel(_smallPmfComplexModel!);
    }

    [Benchmark]
    [BenchmarkCategory("PmfComplexity")]
    public object SimplePmf_Evaluate()
    {
        var (grid, graph) = ModelParser.ParseModel(_smallPmfSimpleModel!);
        return graph.Evaluate(grid);
    }

    [Benchmark]
    [BenchmarkCategory("PmfComplexity")]
    public object ComplexPmf_Evaluate()
    {
        var (grid, graph) = ModelParser.ParseModel(_smallPmfComplexModel!);
        return graph.Evaluate(grid);
    }

    // ===== END-TO-END BENCHMARKS =====

    [Benchmark]
    [BenchmarkCategory("EndToEnd")]
    public object ConstWorkflow_EndToEnd()
    {
        var (grid, graph) = ModelParser.ParseModel(_smallConstModel!);
        return graph.Evaluate(grid);
    }

    [Benchmark]
    [BenchmarkCategory("EndToEnd")]
    public object PmfWorkflow_EndToEnd()
    {
        var (grid, graph) = ModelParser.ParseModel(_smallPmfModel!);
        return graph.Evaluate(grid);
    }

    // ===== HELPER METHODS =====

    private static ModelDefinition GenerateConstModel(int nodeCount, int bins)
    {
        var nodes = new List<NodeDefinition>();
        var baseNodeCount = Math.Max(20, nodeCount / 2);

        // Generate base nodes with constant values
        for (int i = 0; i < baseNodeCount; i++)
        {
            nodes.Add(new NodeDefinition
            {
                Id = $"base_{i}",
                Kind = "const",
                Values = new[] { 10.0 + i }
            });
        }

        // Generate derived nodes with expressions referencing base nodes
        for (int i = baseNodeCount; i < nodeCount; i++)
        {
            var baseRef = i % baseNodeCount;
            nodes.Add(new NodeDefinition
            {
                Id = $"derived_{i}",
                Kind = "expr",
                Expr = $"base_{baseRef} * 1.5"
            });
        }

        return new ModelDefinition
        {
            Grid = new GridDefinition { Bins = bins, BinMinutes = 60 },
            Nodes = nodes
        };
    }

    private static ModelDefinition GeneratePmfModel(int nodeCount, int bins, Func<string> pmfGenerator)
    {
        var nodes = new List<NodeDefinition>();
        var baseNodeCount = Math.Max(20, nodeCount / 2);

        // Generate base nodes with PMF values - using PMF from the generator
        for (int i = 0; i < baseNodeCount; i++)
        {
            var pmfString = pmfGenerator();
            // Parse the PMF string to extract values and probabilities
            var pmfDict = ParsePmfString(pmfString);
            
            nodes.Add(new NodeDefinition
            {
                Id = $"base_{i}",
                Kind = "pmf", 
                Pmf = pmfDict
            });
        }

        // Generate derived nodes with expressions referencing base nodes
        for (int i = baseNodeCount; i < nodeCount; i++)
        {
            var baseRef = i % baseNodeCount;
            nodes.Add(new NodeDefinition
            {
                Id = $"derived_{i}",
                Kind = "expr",
                Expr = $"base_{baseRef} * 1.5"
            });
        }

        return new ModelDefinition
        {
            Grid = new GridDefinition { Bins = bins, BinMinutes = 60 },
            Nodes = nodes
        };
    }

    private static string CreateSmallPmf()
    {
        // Small PMF with 3 values - should be fast
        return "PMF(10: 0.4, 15: 0.4, 20: 0.2)";
    }

    private static string CreateComplexPmf()
    {
        // Complex PMF with many values - should be slower
        var sb = new StringBuilder("PMF(");
        for (int i = 0; i < 20; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append($"{10 + i * 0.5:F1}: 0.05");
        }
        sb.Append(")");
        return sb.ToString();
    }

    private static Dictionary<string, double> ParsePmfString(string pmfString)
    {
        // Simple parser for PMF(value1: prob1, value2: prob2, ...)
        var result = new Dictionary<string, double>();
        
        // Remove "PMF(" prefix and ")" suffix
        var content = pmfString.Substring(4, pmfString.Length - 5);
        
        // Split by commas and parse each value:probability pair
        var pairs = content.Split(',');
        foreach (var pair in pairs)
        {
            var parts = pair.Split(':');
            if (parts.Length == 2)
            {
                var value = parts[0].Trim();
                var prob = double.Parse(parts[1].Trim());
                result[value] = prob;
            }
        }
        
        return result;
    }
}
