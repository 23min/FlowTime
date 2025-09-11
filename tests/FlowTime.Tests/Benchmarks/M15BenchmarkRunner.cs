using FlowTime.Tests.Performance;

namespace FlowTime.Tests.Benchmarks;

/// <summary>
/// Simple test runner for M1.5 benchmarks during development.
/// Run with: dotnet test --filter "FullyQualifiedName~M15BenchmarkRunner"
/// </summary>
public class M15BenchmarkRunner
{
    [Fact]
    [Trait("Category", "Benchmark")]
    public void RunM15ScaleBenchmarks()
    {
        // This will run the scale benchmarks with proper statistical analysis
        Performance.M15BenchmarkRunner.RunScaleBenchmarks();
    }

    [Fact]
    [Trait("Category", "Benchmark")]
    public void RunM15ExpressionTypeBenchmarks()
    {
        // This will run expression type comparison benchmarks
        Performance.M15BenchmarkRunner.RunExpressionTypeBenchmarks();
    }

    [Fact]
    [Trait("Category", "Benchmark")]
    public void RunM15EndToEndBenchmarks()
    {
        // This will run end-to-end performance benchmarks
        Performance.M15BenchmarkRunner.RunEndToEndBenchmarks();
    }

    [Fact]
    [Trait("Category", "Benchmark")]
    public void RunAllM15Benchmarks()
    {
        // This will run all M1.5 benchmarks - use sparingly, takes time
        Performance.M15BenchmarkRunner.RunAllBenchmarks();
    }
}
