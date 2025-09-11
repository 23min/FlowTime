using BenchmarkDotNet.Running;
using BenchmarkDotNet.Configs;
using FlowTime.Tests.Performance;
using Xunit;

namespace FlowTime.Tests.Benchmarks;

/// <summary>
/// xUnit test runner for M2 PMF BenchmarkDotNet performance tests.
/// Run with: dotnet test --filter "FullyQualifiedName~M2BenchmarkRunner"
/// </summary>
public class M2BenchmarkRunner
{
    [Fact]
    [Trait("Category", "Benchmark")]
    public void RunM2PmfVsConstBenchmarks()
    {
        // Run PMF vs Const baseline comparison benchmarks
        BenchmarkRunner.Run<M2BenchmarkDotNetTests>(
            DefaultConfig.Instance.AddFilter(new BenchmarkCategoryFilter("PmfVsConst")));
    }

    [Fact]
    [Trait("Category", "Benchmark")]
    public void RunM2PmfComplexityBenchmarks()
    {
        // Run PMF complexity comparison benchmarks (simple vs complex PMFs)
        BenchmarkRunner.Run<M2BenchmarkDotNetTests>(
            DefaultConfig.Instance.AddFilter(new BenchmarkCategoryFilter("PmfComplexity")));
    }

    [Fact]
    [Trait("Category", "Benchmark")]
    public void RunM2EndToEndBenchmarks()
    {
        // Run end-to-end workflow benchmarks (parse + evaluate together)
        BenchmarkRunner.Run<M2BenchmarkDotNetTests>(
            DefaultConfig.Instance.AddFilter(new BenchmarkCategoryFilter("EndToEnd")));
    }

    [Fact]
    [Trait("Category", "Benchmark")]
    public void RunAllM2Benchmarks()
    {
        // Run all M2 PMF benchmarks - use sparingly, takes significant time
        BenchmarkRunner.Run<M2BenchmarkDotNetTests>(DefaultConfig.Instance);
    }
}
