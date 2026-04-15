using FlowTime.TimeMachine.Sweep;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace FlowTime.Api.Tests;

/// <summary>
/// DI-registration tests for the <see cref="IModelEvaluator"/> config switch introduced
/// in m-E18-13. Verifies that <c>RustEngine:UseSession</c> selects the expected concrete
/// implementation. These tests do not hit the engine — they only resolve the service.
/// </summary>
public sealed class ModelEvaluatorRegistrationTests
{
    /// <summary>
    /// Factory that enables the Rust engine bridge with a specific UseSession value.
    /// </summary>
    private sealed class EngineEnabledFactory : TestWebApplicationFactory
    {
        private readonly bool useSession;
        public EngineEnabledFactory(bool useSession) { this.useSession = useSession; }
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("RustEngine:Enabled", "true");
            builder.UseSetting("RustEngine:BinaryPath", "/nonexistent/flowtime-engine");
            builder.UseSetting("RustEngine:UseSession", useSession ? "true" : "false");
            base.ConfigureWebHost(builder);
        }
    }

    [Fact]
    public async Task UseSessionTrue_RegistersSessionModelEvaluator()
    {
        using var factory = new EngineEnabledFactory(useSession: true);
        await using var scope = factory.Services.CreateAsyncScope();
        var evaluator = scope.ServiceProvider.GetRequiredService<IModelEvaluator>();
        Assert.IsType<SessionModelEvaluator>(evaluator);
    }

    [Fact]
    public async Task UseSessionFalse_RegistersRustModelEvaluator()
    {
        using var factory = new EngineEnabledFactory(useSession: false);
        await using var scope = factory.Services.CreateAsyncScope();
        var evaluator = scope.ServiceProvider.GetRequiredService<IModelEvaluator>();
        Assert.IsType<RustModelEvaluator>(evaluator);
    }

    [Fact]
    public async Task UseSessionDefault_RegistersSessionModelEvaluator()
    {
        // No explicit UseSession setting — default is true.
        using var factory = new DefaultFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var evaluator = scope.ServiceProvider.GetRequiredService<IModelEvaluator>();
        Assert.IsType<SessionModelEvaluator>(evaluator);
    }

    private sealed class DefaultFactory : TestWebApplicationFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("RustEngine:Enabled", "true");
            builder.UseSetting("RustEngine:BinaryPath", "/nonexistent/flowtime-engine");
            // UseSession deliberately not set
            base.ConfigureWebHost(builder);
        }
    }

    [Fact]
    public async Task Runners_AreScopedLifetime()
    {
        // Verify the lifetime change landed: resolving the same runner in two scopes
        // yields distinct instances (true for Scoped/Transient, false for Singleton).
        using var factory = new EngineEnabledFactory(useSession: true);

        await using var scope1 = factory.Services.CreateAsyncScope();
        await using var scope2 = factory.Services.CreateAsyncScope();

        var sweep1 = scope1.ServiceProvider.GetRequiredService<SweepRunner>();
        var sweep2 = scope2.ServiceProvider.GetRequiredService<SweepRunner>();
        Assert.NotSame(sweep1, sweep2);

        var opt1 = scope1.ServiceProvider.GetRequiredService<Optimizer>();
        var opt2 = scope2.ServiceProvider.GetRequiredService<Optimizer>();
        Assert.NotSame(opt1, opt2);
    }
}
