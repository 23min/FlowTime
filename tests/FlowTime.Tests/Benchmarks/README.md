# FlowTime Benchmark Organization

This directory contains the benchmark infrastructure for FlowTime performance testing using BenchmarkDotNet.

## Structure

### Performance Tests (`/tests/FlowTime.Tests/Performance/`)
- **M15PerformanceTests.cs** - Legacy stopwatch tests for M1.5 Expression Language baseline
- **M16BenchmarkDotNetTests.cs** - BenchmarkDotNet tests for M1.6 (M1.5 + benchmarking infrastructure)
- **M2PerformanceTests.cs** - Legacy stopwatch tests for M2 PMF implementation
- **M2BenchmarkDotNetTests.cs** - BenchmarkDotNet tests for M2 PMF implementation  

### Benchmark Runners (`/tests/FlowTime.Tests/Benchmarks/`)
- **M16BenchmarkRunner.cs** - xUnit runners for M1.6 BenchmarkDotNet tests
- **M2BenchmarkRunner.cs** - xUnit runners for M2 BenchmarkDotNet tests

## Usage

### Run M1.6 Benchmarks (Expression Language + BenchmarkDotNet Baseline)
```bash
# All M1.6 benchmarks (comprehensive, slow)
dotnet test --filter "FullyQualifiedName~M16BenchmarkRunner.RunAllM16Benchmarks"

# Specific categories (faster)
dotnet test --filter "FullyQualifiedName~M16BenchmarkRunner.RunM16ScaleBenchmarks"
dotnet test --filter "FullyQualifiedName~M16BenchmarkRunner.RunM16ExpressionTypeBenchmarks"  
dotnet test --filter "FullyQualifiedName~M16BenchmarkRunner.RunM16EndToEndBenchmarks"
```

### Run M2 Benchmarks (PMF vs Baseline Comparison)
```bash
# All M2 benchmarks (comprehensive, slow)
dotnet test --filter "FullyQualifiedName~M2BenchmarkRunner.RunAllM2Benchmarks"

# Specific categories (faster)
dotnet test --filter "FullyQualifiedName~M2BenchmarkRunner.RunM2PmfVsConstBenchmarks"
dotnet test --filter "FullyQualifiedName~M2BenchmarkRunner.RunM2PmfComplexityBenchmarks"
dotnet test --filter "FullyQualifiedName~M2BenchmarkRunner.RunM2EndToEndBenchmarks"
```

### Run Quick Performance Tests (Legacy, faster feedback)
```bash
# M1.5 legacy tests (stopwatch-based, ~2-3 seconds)
dotnet test --filter "FullyQualifiedName~M15PerformanceTests"

# M2 legacy tests (stopwatch-based, ~2-3 seconds) 
dotnet test --filter "FullyQualifiedName~M2PerformanceTests"
```

## Benchmark Categories

### M1.5 Categories
- **Scale** - Performance across different model sizes (small/medium/large)
- **ExpressionType** - Performance comparison of different expression types
- **EndToEnd** - Complete parse + evaluate workflow performance

### M2 Categories  
- **PmfVsConst** - PMF node performance vs constant node baseline
- **PmfComplexity** - Simple PMF vs complex PMF performance
- **EndToEnd** - Complete PMF workflow performance

## Naming Convention

- **M15** = M1.5 Expression Language implementation (baseline)
- **M2** = M2 PMF implementation 
- **M1.6** = Milestone documentation (M1.5 + benchmarking infrastructure)

The M1.6 milestone established the benchmarking infrastructure on top of M1.5 capabilities, while M2 adds PMF functionality with performance comparison to the M1.5 baseline.

## Best Practices

1. **Development**: Use legacy performance tests for quick feedback during development
2. **Regression Testing**: Use category-specific BenchmarkDotNet tests for targeted analysis
3. **Release Validation**: Use comprehensive BenchmarkDotNet tests before milestone completion
4. **Performance Analysis**: Compare M2 PMF results against M1.5 baseline to measure feature impact
