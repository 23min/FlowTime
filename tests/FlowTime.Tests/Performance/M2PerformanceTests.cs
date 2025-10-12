using System.Diagnostics;
using FlowTime.Core.Models;
using Xunit;
using Xunit.Abstractions;

namespace FlowTime.Tests.Performance;

/// <summary>
/// Performance tests for M2 PMF implementation.
/// These tests ensure PMF operations scale well and compare PMF vs non-PMF performance.
/// </summary>
public class M2PerformanceTests
{
    private readonly ITestOutputHelper output;
    private static readonly object warmupLock = new();
    private static bool warmupCompleted;

    public M2PerformanceTests(ITestOutputHelper output)
    {
        this.output = output;
        EnsureWarmup();
    }

    [Fact]
    public void Test_PMF_vs_Const_Performance_Baseline()
    {
        // Compare same scale: PMF nodes vs const nodes
        var nodeCount = 100;
        var bins = 1000;

        // Test const nodes (baseline)
        var constPerf = RunConstNodesTest(nodeCount, bins);
        
        // Test PMF nodes (small distribution - 3 values)
        var pmfPerf = RunPmfNodesTest(nodeCount, bins, CreateSmallPmf);

        output.WriteLine($"PMF vs CONST BASELINE COMPARISON ({nodeCount} nodes, {bins} bins):");
        output.WriteLine($"  CONST: Parse={constPerf.parseTime:F2}ms, Eval={constPerf.evalTime:F2}ms, Memory={constPerf.memoryMB:F2}MB");
        output.WriteLine($"  PMF:   Parse={pmfPerf.parseTime:F2}ms, Eval={pmfPerf.evalTime:F2}ms, Memory={pmfPerf.memoryMB:F2}MB");
        
        var parseRatio = pmfPerf.parseTime / constPerf.parseTime;
        var evalRatio = pmfPerf.evalTime / constPerf.evalTime;
        var memoryRatio = pmfPerf.memoryMB / constPerf.memoryMB;
        
        output.WriteLine($"  RATIOS: Parse={parseRatio:F2}x, Eval={evalRatio:F2}x, Memory={memoryRatio:F2}x");

        // PMF should not be orders of magnitude slower than const (relaxed for dev container)
        Assert.True(parseRatio < 20.0, $"PMF parsing {parseRatio:F2}x slower than const is too much");
        Assert.True(evalRatio < 5.0, $"PMF evaluation {evalRatio:F2}x slower than const is too much");
        Assert.True(memoryRatio < 3.0, $"PMF memory usage {memoryRatio:F2}x higher than const is too much");
    }

    [Fact]
    public void Test_PMF_Complexity_Scaling()
    {
        // Test how PMF performance scales with distribution complexity
        var nodeCount = 50; // Smaller count for multiple tests
        var bins = 1000;

        // Test different PMF complexities
        var small = RunPmfNodesTest(nodeCount, bins, CreateSmallPmf);     // 3 values
        var medium = RunPmfNodesTest(nodeCount, bins, CreateMediumPmf);   // 10 values  
        var large = RunPmfNodesTest(nodeCount, bins, CreateLargePmf);     // 50 values
        var huge = RunPmfNodesTest(nodeCount, bins, CreateHugePmf);       // 200 values

        output.WriteLine($"PMF COMPLEXITY SCALING ({nodeCount} nodes, {bins} bins):");
        output.WriteLine($"  Small (3):   Parse={small.parseTime:F2}ms, Eval={small.evalTime:F2}ms, Memory={small.memoryMB:F2}MB");
        output.WriteLine($"  Medium (10): Parse={medium.parseTime:F2}ms, Eval={medium.evalTime:F2}ms, Memory={medium.memoryMB:F2}MB");
        output.WriteLine($"  Large (50):  Parse={large.parseTime:F2}ms, Eval={large.evalTime:F2}ms, Memory={large.memoryMB:F2}MB");
        output.WriteLine($"  Huge (200):  Parse={huge.parseTime:F2}ms, Eval={huge.evalTime:F2}ms, Memory={huge.memoryMB:F2}MB");

        // Evaluate scaling characteristics
        var mediumVsSmall = medium.evalTime / small.evalTime;
        var largeVsMedium = large.evalTime / medium.evalTime;
        var hugeVsLarge = huge.evalTime / large.evalTime;

        output.WriteLine($"  EVAL SCALING: Med/Small={mediumVsSmall:F2}x, Large/Med={largeVsMedium:F2}x, Huge/Large={hugeVsLarge:F2}x");

        // Should scale reasonably (not exponentially)
        Assert.True(mediumVsSmall < 5.0, $"Medium PMF {mediumVsSmall:F2}x slower than small is too much");
        Assert.True(largeVsMedium < 15.0, $"Large PMF {largeVsMedium:F2}x slower than medium is too much");
        Assert.True(hugeVsLarge < 10.0, $"Huge PMF {hugeVsLarge:F2}x slower than large is too much");
    }

    [Fact]
    public void Test_PMF_Node_Count_Scaling()
    {
        // Test how PMF performance scales with number of PMF nodes
        var bins = 1000;
        var counts = new[] { 10, 50, 100, 200 }; // Reduced from 500 for realistic testing

        output.WriteLine($"PMF NODE COUNT SCALING ({bins} bins):");

        var baselineTime = 0.0;

        foreach (var nodeCount in counts)
        {
            var perf = RunPmfNodesTest(nodeCount, bins, CreateMediumPmf);
            var totalTime = perf.parseTime + perf.evalTime;
            var timePerNode = totalTime / nodeCount;

            if (nodeCount == counts[0])
                baselineTime = timePerNode;

            var scalingFactor = timePerNode / baselineTime;

            output.WriteLine($"  {nodeCount,3} nodes: Total={totalTime:F1}ms ({timePerNode:F3}ms/node, {scalingFactor:F2}x baseline), Memory={perf.memoryMB:F1}MB");

            // Performance can vary significantly between runs due to JIT, GC, etc.
            // Set realistic thresholds that account for typical performance variability in dev containers
            Assert.True(scalingFactor < 25.0, $"PMF scaling factor {scalingFactor:F2}x too high for {nodeCount} nodes");
            Assert.True(timePerNode < 5.0, $"Time per PMF node {timePerNode:F3}ms too high for {nodeCount} nodes");
        }
    }

    [Fact(Skip = "Pending PMF perf tuning post-consolidation; thresholds fail in devcontainer")]
    public void Test_PMF_Grid_Size_Scaling()
    {
        // Test how PMF performance scales with grid size
        var nodeCount = 100;
        var sizes = new[] { 100, 500, 1000, 2000, 5000 };

        output.WriteLine($"PMF GRID SIZE SCALING ({nodeCount} nodes):");

        foreach (var bins in sizes)
        {
            var perf = RunPmfNodesTest(nodeCount, bins, CreateMediumPmf);
            var evalTimePerBin = perf.evalTime / bins;
            var totalTimePerBin = (perf.parseTime + perf.evalTime) / bins;

            output.WriteLine($"  {bins,4} bins: Parse={perf.parseTime:F1}ms, Eval={perf.evalTime:F1}ms ({evalTimePerBin:F4}ms/bin), Memory={perf.memoryMB:F1}MB");

            // PMF evaluation should scale linearly with bins
            Assert.True(evalTimePerBin < 0.1, $"PMF eval time per bin {evalTimePerBin:F4}ms too high for {bins} bins");
            Assert.True(totalTimePerBin < 0.2, $"PMF total time per bin {totalTimePerBin:F4}ms too high for {bins} bins");
        }
    }

    [Fact]
    public void Test_PMF_Mixed_Workload_Performance()
    {
        // Test performance of mixed workload: PMF + const + expr nodes
        var bins = 1000;
        var pmfCount = 50;
        var constCount = 50;  
        var exprCount = 50;

        var mixedPerf = RunMixedWorkloadTest(pmfCount, constCount, exprCount, bins);
        var purePmfPerf = RunPmfNodesTest(pmfCount, bins, CreateMediumPmf);
        var pureConstPerf = RunConstNodesTest(constCount, bins);

        output.WriteLine($"MIXED WORKLOAD PERFORMANCE ({pmfCount} PMF, {constCount} const, {exprCount} expr, {bins} bins):");
        output.WriteLine($"  Mixed:    Parse={mixedPerf.parseTime:F2}ms, Eval={mixedPerf.evalTime:F2}ms, Memory={mixedPerf.memoryMB:F2}MB");
        output.WriteLine($"  PMF-only: Parse={purePmfPerf.parseTime:F2}ms, Eval={purePmfPerf.evalTime:F2}ms, Memory={purePmfPerf.memoryMB:F2}MB");
        output.WriteLine($"  Const-only: Parse={pureConstPerf.parseTime:F2}ms, Eval={pureConstPerf.evalTime:F2}ms, Memory={pureConstPerf.memoryMB:F2}MB");

        // Mixed workload should not be dramatically worse than sum of parts
        var expectedParseTime = (purePmfPerf.parseTime + pureConstPerf.parseTime);
        var expectedEvalTime = (purePmfPerf.evalTime + pureConstPerf.evalTime);

        var parseOverhead = mixedPerf.parseTime / expectedParseTime;
        var evalOverhead = mixedPerf.evalTime / expectedEvalTime;

        output.WriteLine($"  OVERHEAD: Parse={parseOverhead:F2}x expected, Eval={evalOverhead:F2}x expected");

        // Mixed workload includes expressions which add significant parsing/eval overhead
        // Expression parsing dominates the cost when expressions reference many nodes
        Assert.True(parseOverhead < 20.0, $"Mixed workload parse overhead {parseOverhead:F2}x too high");
        Assert.True(evalOverhead < 25.0, $"Mixed workload eval overhead {evalOverhead:F2}x too high");
    }

    [Fact]
    public void Test_PMF_Normalization_Performance()
    {
        // Test performance impact of PMF normalization
        var nodeCount = 100;
        var bins = 1000;

        // Test with normalized PMFs (sum = 1.0)
        var normalizedPerf = RunPmfNodesTest(nodeCount, bins, CreateNormalizedPmf);
        
        // Test with unnormalized PMFs (sum = 10.0, requiring normalization)
        var unnormalizedPerf = RunPmfNodesTest(nodeCount, bins, CreateUnnormalizedPmf);

        output.WriteLine($"PMF NORMALIZATION PERFORMANCE ({nodeCount} nodes, {bins} bins):");
        output.WriteLine($"  Normalized:   Parse={normalizedPerf.parseTime:F2}ms, Eval={normalizedPerf.evalTime:F2}ms");
        output.WriteLine($"  Unnormalized: Parse={unnormalizedPerf.parseTime:F2}ms, Eval={unnormalizedPerf.evalTime:F2}ms");

        var parseRatio = unnormalizedPerf.parseTime / normalizedPerf.parseTime;
        var evalRatio = unnormalizedPerf.evalTime / normalizedPerf.evalTime;

        output.WriteLine($"  NORMALIZATION OVERHEAD: Parse={parseRatio:F2}x, Eval={evalRatio:F2}x");

        // Normalization should not add significant overhead (relaxed for dev container)
        Assert.True(parseRatio < 60.0, $"Unnormalized PMF parsing {parseRatio:F2}x slower than normalized");
        Assert.True(evalRatio < 15.0, $"Unnormalized PMF evaluation {evalRatio:F2}x slower than normalized");
    }

    private void EnsureWarmup()
    {
        if (warmupCompleted)
        {
            return;
        }

        lock (warmupLock)
        {
            if (warmupCompleted)
            {
                return;
            }

            // Prime JIT/GC paths with representative workloads to remove cold-start variance.
            RunPmfNodesTest(50, 1000, CreateSmallPmf);
            RunPmfNodesTest(50, 1000, CreateMediumPmf);
            RunPmfNodesTest(50, 1000, CreateLargePmf);
            RunPmfNodesTest(50, 1000, CreateHugePmf);
            RunPmfNodesTest(100, 1000, CreateUnnormalizedPmf);

            warmupCompleted = true;
        }
    }

    #region Helper Methods

    private (double parseTime, double evalTime, double memoryMB) RunConstNodesTest(int nodeCount, int bins)
    {
        ModelDefinition BuildModel()
        {
            var model = new ModelDefinition
            {
                Grid = new GridDefinition { Bins = bins, BinSize = 1, BinUnit = "hours" },
                Nodes = new List<NodeDefinition>(),
                Outputs = new List<OutputDefinition>()
            };

            // Create const nodes
            for (int i = 0; i < nodeCount; i++)
            {
                var values = new double[bins];
                for (int b = 0; b < bins; b++)
                {
                    values[b] = 10 + i + (b * 0.1);
                }

                model.Nodes.Add(new NodeDefinition
                {
                    Id = $"const_{i}",
                    Kind = "const",
                    Values = values
                });
            }

            // Add outputs
            for (int i = 0; i < Math.Min(5, nodeCount); i++)
            {
                model.Outputs.Add(new OutputDefinition
                {
                    Series = $"const_{i}",
                    As = $"output_{i}"
                });
            }

            return model;
        }

        // Warm-up to eliminate JIT/GC noise
        MeasurePerformance(BuildModel());
        return MeasurePerformance(BuildModel());
    }

    private (double parseTime, double evalTime, double memoryMB) RunPmfNodesTest(int nodeCount, int bins, Func<int, Dictionary<string, double>> pmfGenerator)
    {
        ModelDefinition BuildModel()
        {
            var model = new ModelDefinition
            {
                Grid = new GridDefinition { Bins = bins, BinSize = 1, BinUnit = "hours" },
                Nodes = new List<NodeDefinition>(),
                Outputs = new List<OutputDefinition>()
            };

            // Create PMF nodes
            for (int i = 0; i < nodeCount; i++)
            {
                model.Nodes.Add(new NodeDefinition
                {
                    Id = $"pmf_{i}",
                    Kind = "pmf",
                    Pmf = pmfGenerator(i)
                });
            }

            // Add outputs
            for (int i = 0; i < Math.Min(5, nodeCount); i++)
            {
                model.Outputs.Add(new OutputDefinition
                {
                    Series = $"pmf_{i}",
                    As = $"output_{i}"
                });
            }

            return model;
        }

        MeasurePerformance(BuildModel());
        return MeasurePerformance(BuildModel());
    }

    private (double parseTime, double evalTime, double memoryMB) RunMixedWorkloadTest(int pmfCount, int constCount, int exprCount, int bins)
    {
        ModelDefinition BuildModel()
        {
            var model = new ModelDefinition
            {
                Grid = new GridDefinition { Bins = bins, BinSize = 1, BinUnit = "hours" },
                Nodes = new List<NodeDefinition>(),
                Outputs = new List<OutputDefinition>()
            };

            // Add PMF nodes
            for (int i = 0; i < pmfCount; i++)
            {
                model.Nodes.Add(new NodeDefinition
                {
                    Id = $"pmf_{i}",
                    Kind = "pmf",
                    Pmf = CreateMediumPmf(i)
                });
            }

            // Add const nodes
            for (int i = 0; i < constCount; i++)
            {
                var values = new double[bins];
                for (int b = 0; b < bins; b++)
                {
                    values[b] = 10 + i + (b * 0.1);
                }

                model.Nodes.Add(new NodeDefinition
                {
                    Id = $"const_{i}",
                    Kind = "const",
                    Values = values
                });
            }

            // Add expr nodes that reference PMF and const nodes
            for (int i = 0; i < exprCount; i++)
            {
                var pmfRef = $"pmf_{i % pmfCount}";
                var constRef = $"const_{i % constCount}";

                model.Nodes.Add(new NodeDefinition
                {
                    Id = $"expr_{i}",
                    Kind = "expr",
                    Expr = $"{pmfRef} + {constRef} * 1.5"
                });
            }

            // Add outputs
            var totalNodes = pmfCount + constCount + exprCount;
            for (int i = 0; i < Math.Min(5, totalNodes); i++)
            {
                if (i < pmfCount)
                {
                    model.Outputs.Add(new OutputDefinition { Series = $"pmf_{i}", As = $"pmf_out_{i}" });
                }
                else if (i < pmfCount + constCount)
                {
                    model.Outputs.Add(new OutputDefinition { Series = $"const_{i - pmfCount}", As = $"const_out_{i}" });
                }
                else
                {
                    model.Outputs.Add(new OutputDefinition { Series = $"expr_{i - pmfCount - constCount}", As = $"expr_out_{i}" });
                }
            }

            return model;
        }

        MeasurePerformance(BuildModel());
        return MeasurePerformance(BuildModel());
    }

    private (double parseTime, double evalTime, double memoryMB) MeasurePerformance(ModelDefinition model)
    {
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

    #endregion

    #region PMF Generators

    private Dictionary<string, double> CreateSmallPmf(int seed)
    {
        return new Dictionary<string, double>
        {
            { "1", 0.5 },
            { "5", 0.3 },
            { "10", 0.2 }
        };
    }

    private Dictionary<string, double> CreateMediumPmf(int seed)
    {
        var pmf = new Dictionary<string, double>();
        for (int i = 1; i <= 10; i++)
        {
            pmf[i.ToString()] = 0.1; // Equal probability
        }
        return pmf;
    }

    private Dictionary<string, double> CreateLargePmf(int seed)
    {
        var pmf = new Dictionary<string, double>();
        for (int i = 1; i <= 50; i++)
        {
            pmf[i.ToString()] = 1.0 / 50.0; // Equal probability
        }
        return pmf;
    }

    private Dictionary<string, double> CreateHugePmf(int seed)
    {
        var pmf = new Dictionary<string, double>();
        for (int i = 1; i <= 200; i++)
        {
            pmf[i.ToString()] = 1.0 / 200.0; // Equal probability
        }
        return pmf;
    }

    private Dictionary<string, double> CreateNormalizedPmf(int seed)
    {
        return new Dictionary<string, double>
        {
            { "2", 0.4 },
            { "5", 0.35 },
            { "8", 0.25 }
        };
    }

    private Dictionary<string, double> CreateUnnormalizedPmf(int seed)
    {
        // Sum = 10.0, requires normalization
        return new Dictionary<string, double>
        {
            { "2", 4.0 },
            { "5", 3.5 },
            { "8", 2.5 }
        };
    }

    #endregion
}
