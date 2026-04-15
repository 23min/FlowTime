using FlowTime.Cli.Commands;
using FlowTime.TimeMachine.Sweep;

namespace FlowTime.Cli.Tests.Commands;

/// <summary>
/// Unit tests for <see cref="CliEngineSetup"/>: engine binary path resolution precedence
/// and evaluator construction.
/// </summary>
public class CliEngineSetupTests
{
    // ── ResolveEnginePath ──────────────────────────────────────────

    [Fact]
    public void ResolveEnginePath_ExplicitWins()
    {
        // Explicit --engine flag takes precedence over everything.
        var orig = Environment.GetEnvironmentVariable("FLOWTIME_RUST_BINARY");
        try
        {
            Environment.SetEnvironmentVariable("FLOWTIME_RUST_BINARY", "/env/path/engine");
            var path = CliEngineSetup.ResolveEnginePath(explicitPath: "/cli/path/engine");
            Assert.Equal("/cli/path/engine", path);
        }
        finally
        {
            Environment.SetEnvironmentVariable("FLOWTIME_RUST_BINARY", orig);
        }
    }

    [Fact]
    public void ResolveEnginePath_EnvVarUsedWhenNoExplicit()
    {
        var orig = Environment.GetEnvironmentVariable("FLOWTIME_RUST_BINARY");
        try
        {
            Environment.SetEnvironmentVariable("FLOWTIME_RUST_BINARY", "/env/path/engine");
            var path = CliEngineSetup.ResolveEnginePath(explicitPath: null);
            Assert.Equal("/env/path/engine", path);
        }
        finally
        {
            Environment.SetEnvironmentVariable("FLOWTIME_RUST_BINARY", orig);
        }
    }

    [Fact]
    public void ResolveEnginePath_FallsBackToSolutionDefault()
    {
        // With no explicit and no env var, resolves to a solution-relative default path.
        // We don't assert the path exists — just that the method returns a non-empty path.
        var orig = Environment.GetEnvironmentVariable("FLOWTIME_RUST_BINARY");
        try
        {
            Environment.SetEnvironmentVariable("FLOWTIME_RUST_BINARY", null);
            var path = CliEngineSetup.ResolveEnginePath(explicitPath: null);
            Assert.False(string.IsNullOrWhiteSpace(path));
        }
        finally
        {
            Environment.SetEnvironmentVariable("FLOWTIME_RUST_BINARY", orig);
        }
    }

    [Fact]
    public void ResolveEnginePath_EmptyExplicitIgnored()
    {
        // Empty string explicit path must fall through, not produce an empty result.
        var orig = Environment.GetEnvironmentVariable("FLOWTIME_RUST_BINARY");
        try
        {
            Environment.SetEnvironmentVariable("FLOWTIME_RUST_BINARY", "/env/engine");
            var path = CliEngineSetup.ResolveEnginePath(explicitPath: "");
            Assert.Equal("/env/engine", path);
        }
        finally
        {
            Environment.SetEnvironmentVariable("FLOWTIME_RUST_BINARY", orig);
        }
    }

    // ── CreateEvaluator ────────────────────────────────────────────

    [Fact]
    public async Task CreateEvaluator_Session_ReturnsSessionModelEvaluator()
    {
        await using var handle = CliEngineSetup.CreateEvaluator(
            enginePath: "/nonexistent/engine",
            useSession: true);
        Assert.IsType<SessionModelEvaluator>(handle.Evaluator);
    }

    [Fact]
    public async Task CreateEvaluator_NoSession_ReturnsRustModelEvaluator()
    {
        await using var handle = CliEngineSetup.CreateEvaluator(
            enginePath: "/nonexistent/engine",
            useSession: false);
        Assert.IsType<RustModelEvaluator>(handle.Evaluator);
    }

    [Fact]
    public async Task CreateEvaluator_DisposeAsync_IsIdempotent()
    {
        var handle = CliEngineSetup.CreateEvaluator("/nonexistent/engine", useSession: true);
        await handle.DisposeAsync();
        await handle.DisposeAsync(); // must not throw
    }

    [Fact]
    public async Task CreateEvaluator_NoSession_DisposeAsync_NoOp()
    {
        // RustModelEvaluator is not IAsyncDisposable — handle must handle that gracefully.
        await using var handle = CliEngineSetup.CreateEvaluator(
            enginePath: "/nonexistent/engine",
            useSession: false);
        // Disposal via await using must complete without throwing.
    }
}
