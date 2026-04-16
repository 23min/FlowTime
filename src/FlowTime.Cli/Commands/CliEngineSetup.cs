using FlowTime.Core.Configuration;
using FlowTime.Core.Execution;
using FlowTime.TimeMachine.Sweep;

namespace FlowTime.Cli.Commands;

/// <summary>
/// Shared setup for Time Machine CLI commands that need the Rust engine. Resolves the
/// engine binary path (explicit → env var → solution default) and constructs an
/// <see cref="IModelEvaluator"/> wrapped in a uniformly-disposable handle so callers can
/// use <c>await using</c> regardless of which concrete evaluator was chosen.
/// </summary>
public static class CliEngineSetup
{
    /// <summary>Environment variable name for overriding the engine binary path.</summary>
    public const string EnginePathEnvironmentVariable = "FLOWTIME_RUST_BINARY";

    /// <summary>
    /// Resolve the engine binary path with precedence:
    /// <list type="number">
    ///   <item><description>Non-empty <paramref name="explicitPath"/> (the <c>--engine</c> flag).</description></item>
    ///   <item><description>The <c>FLOWTIME_RUST_BINARY</c> environment variable if set and non-empty.</description></item>
    ///   <item><description>Solution-relative default: <c>&lt;solution&gt;/engine/target/release/flowtime-engine</c>.</description></item>
    ///   <item><description><c>flowtime-engine</c> (resolved against <c>$PATH</c> at spawn time).</description></item>
    /// </list>
    /// </summary>
    public static string ResolveEnginePath(string? explicitPath)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            return explicitPath;
        }

        var envPath = Environment.GetEnvironmentVariable(EnginePathEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(envPath))
        {
            return envPath;
        }

        var solutionRoot = DirectoryProvider.FindSolutionRoot();
        if (solutionRoot is not null)
        {
            return Path.Combine(solutionRoot, "engine", "target", "release", "flowtime-engine");
        }

        return "flowtime-engine";
    }

    /// <summary>
    /// Construct an evaluator backed by <paramref name="enginePath"/> and return it in a
    /// handle that implements <see cref="IAsyncDisposable"/>. Session-mode returns a
    /// <see cref="SessionModelEvaluator"/>; otherwise a <see cref="RustModelEvaluator"/>
    /// over <see cref="RustEngineRunner"/>.
    /// </summary>
    public static CliEvaluatorHandle CreateEvaluator(string enginePath, bool useSession)
    {
        IModelEvaluator evaluator = useSession
            ? new SessionModelEvaluator(enginePath)
            : new RustModelEvaluator(new RustEngineRunner(enginePath));
        return new CliEvaluatorHandle(evaluator);
    }
}

/// <summary>
/// Uniform disposable wrapper over an <see cref="IModelEvaluator"/>. Forwards
/// <see cref="DisposeAsync"/> to the underlying evaluator if it implements
/// <see cref="IAsyncDisposable"/>; no-op otherwise.
/// </summary>
public sealed class CliEvaluatorHandle : IAsyncDisposable
{
    private readonly IAsyncDisposable? disposable;
    private bool disposed;

    public IModelEvaluator Evaluator { get; }

    internal CliEvaluatorHandle(IModelEvaluator evaluator)
    {
        Evaluator = evaluator;
        disposable = evaluator as IAsyncDisposable;
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed) return;
        disposed = true;
        if (disposable is not null)
        {
            await disposable.DisposeAsync().ConfigureAwait(false);
        }
    }
}
