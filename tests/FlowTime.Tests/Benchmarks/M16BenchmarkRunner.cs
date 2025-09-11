using BenchmarkDotNet.Running;
using BenchmarkDotNet.Configs;
using FlowTime.Tests.Performance;
using Xunit;

namespace FlowTime.Tests.Benchmarks;

/// <summary>
/// xUnit test runner for M1.6 BenchmarkDotNet performance tests.
/// M1.6 established the BenchmarkDotNet infrastructure on top of M1.5 expression language capabilities.
/// Run with: dotnet test --filter "FullyQualifiedName~M16BenchmarkRunner"
/// </summary>
public class M16BenchmarkRunner
{
    [Fact]
    [Trait("Category", "Benchmark")]
    public void RunM16ScaleBenchmarks()
    {
        // Run only scale benchmarks (baseline comparison across different model sizes)
        BenchmarkRunner.Run<M16BenchmarkDotNetTests>(
            DefaultConfig.Instance.AddFilter(new BenchmarkCategoryFilter("Scale")));
    }

    [Fact]
    [Trait("Category", "Benchmark")]
    public void RunM16ExpressionTypeBenchmarks()
    {
        // Run expression type comparison benchmarks (simple vs complex expressions)
        BenchmarkRunner.Run<M16BenchmarkDotNetTests>(
            DefaultConfig.Instance.AddFilter(new BenchmarkCategoryFilter("ExpressionType")));
    }

    [Fact]
    [Trait("Category", "Benchmark")]
    public void RunM16EndToEndBenchmarks()
    {
        // Run end-to-end workflow benchmarks (parse + evaluate together)
        BenchmarkRunner.Run<M16BenchmarkDotNetTests>(
            DefaultConfig.Instance.AddFilter(new BenchmarkCategoryFilter("EndToEnd")));
    }

    [Fact]
    [Trait("Category", "Benchmark")]
    public void RunAllM16Benchmarks()
    {
        // Run all M1.6 benchmarks - use sparingly, takes significant time
        BenchmarkRunner.Run<M16BenchmarkDotNetTests>(DefaultConfig.Instance);
    }
}
