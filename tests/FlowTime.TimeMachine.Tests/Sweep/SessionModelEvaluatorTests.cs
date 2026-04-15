using FlowTime.TimeMachine.Sweep;

namespace FlowTime.TimeMachine.Tests.Sweep;

/// <summary>
/// Unit tests for <see cref="SessionModelEvaluator"/> covering construction and disposal.
/// Protocol-level behavior (compile/eval/error handling) is exercised by integration
/// tests that require the Rust engine binary.
/// </summary>
public class SessionModelEvaluatorTests
{
    [Fact]
    public void Constructor_NullEnginePath_Throws()
    {
        Assert.Throws<ArgumentException>(() => new SessionModelEvaluator(null!));
    }

    [Fact]
    public void Constructor_EmptyEnginePath_Throws()
    {
        Assert.Throws<ArgumentException>(() => new SessionModelEvaluator(string.Empty));
    }

    [Fact]
    public void Constructor_WhitespaceEnginePath_Throws()
    {
        Assert.Throws<ArgumentException>(() => new SessionModelEvaluator("   "));
    }

    [Fact]
    public async Task Constructor_ValidEnginePath_Succeeds()
    {
        // Constructor does no I/O — a path that does not exist is still accepted.
        // The first EvaluateAsync call is what fails if the binary is missing.
        await using var evaluator = new SessionModelEvaluator("/nonexistent/path/to/engine");
    }

    [Fact]
    public async Task DisposeAsync_IsIdempotent()
    {
        var evaluator = new SessionModelEvaluator("/nonexistent/path/to/engine");
        await evaluator.DisposeAsync();
        await evaluator.DisposeAsync(); // second call must not throw
    }

    [Fact]
    public async Task EvaluateAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        var evaluator = new SessionModelEvaluator("/nonexistent/path/to/engine");
        await evaluator.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => evaluator.EvaluateAsync("grid: {bins: 1, binSize: 1, binUnit: hours}\nnodes: []"));
    }
}
