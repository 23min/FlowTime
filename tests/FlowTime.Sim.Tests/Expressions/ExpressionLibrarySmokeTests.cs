using FlowTime.Expressions;
using Xunit;

namespace FlowTime.Sim.Tests.Expressions;

/// <summary>
/// Temporary smoke test ensuring FlowTime.Sim projects can consume the shared FlowTime.Expressions library.
/// Will be replaced with full adoption scenarios during SIM-M-03 workstreams.
/// </summary>
public class ExpressionLibrarySmokeTests
{
    [Fact(Skip = "FlowTime.Sim adoption of shared expressions scheduled for SIM-M-03.")]
    public void ExpressionParser_SupportsBasicArithmetic()
    {
        var parser = new ExpressionParser("a + b");
        var ast = parser.Parse();
        Assert.NotNull(ast);
    }
}
