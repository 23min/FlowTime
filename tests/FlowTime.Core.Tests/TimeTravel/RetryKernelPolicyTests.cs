using System.Linq;
using FlowTime.Core.TimeTravel;

namespace FlowTime.Core.Tests.TimeTravel;

public class RetryKernelPolicyTests
{
    [Fact]
    public void Apply_WhenKernelNull_UsesDefault()
    {
        var result = RetryKernelPolicy.Apply(null);
        Assert.True(result.UsedDefault);
        Assert.False(result.HasMessages);
        Assert.Equal(RetryKernelPolicy.DefaultKernel, result.Kernel);
    }

    [Fact]
    public void Apply_TrimsAndWarns_WhenKernelTooLong()
    {
        var kernel = Enumerable.Repeat(0.05, RetryKernelPolicy.MaxKernelLength + 5).ToArray();

        var result = RetryKernelPolicy.Apply(kernel);

        Assert.False(result.UsedDefault);
        Assert.Equal(RetryKernelPolicy.MaxKernelLength, result.Kernel.Length);
        Assert.Contains(result.Messages, message => message.Contains("trimmed", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Apply_Scales_WhenKernelSumTooLarge()
    {
        var kernel = new[] { 0.8, 0.6, 0.4 };

        var result = RetryKernelPolicy.Apply(kernel);

        Assert.False(result.UsedDefault);
        var sum = result.Kernel.Sum();
        Assert.True(sum <= RetryKernelPolicy.MaxKernelSum + 1e-6);
        Assert.Contains(result.Messages, message => message.Contains("scaled", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Apply_ClampsInvalidCoefficients()
    {
        var kernel = new[] { 0.5, double.NaN, -0.2 };

        var result = RetryKernelPolicy.Apply(kernel);

        Assert.False(result.UsedDefault);
        Assert.All(result.Kernel, value => Assert.True(value >= 0));
        Assert.Contains(result.Messages, message => message.Contains("non-finite", StringComparison.OrdinalIgnoreCase));
    }
}
