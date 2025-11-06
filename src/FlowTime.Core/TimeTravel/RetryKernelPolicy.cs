using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace FlowTime.Core.TimeTravel;

public static class RetryKernelPolicy
{
    public const int MaxKernelLength = 32;
    public const double MaxKernelSum = 1.0;

    private static readonly double[] defaultKernel = new[] { 0.0, 0.6, 0.3, 0.1 };

    public static double[] DefaultKernel => defaultKernel.ToArray();

    public static RetryKernelPolicyResult Apply(double[]? kernel)
    {
        if (kernel is null || kernel.Length == 0)
        {
            return new RetryKernelPolicyResult(DefaultKernel, Array.Empty<string>(), true);
        }

        var trimmed = kernel.Length > MaxKernelLength;
        var invalidCount = 0;
        var sanitized = new double[Math.Min(kernel.Length, MaxKernelLength)];

        for (var i = 0; i < sanitized.Length; i++)
        {
            var value = kernel[i];
            if (!double.IsFinite(value) || value < 0)
            {
                value = 0d;
                invalidCount++;
            }

            sanitized[i] = value;
        }

        var messages = new List<string>();

        if (trimmed)
        {
            messages.Add($"Retry kernel trimmed to {MaxKernelLength} coefficients (from {kernel.Length}).");
        }

        if (invalidCount > 0)
        {
            messages.Add("Retry kernel contained negative or non-finite values; those coefficients were reset to 0.");
        }

        var sum = 0d;
        for (var i = 0; i < sanitized.Length; i++)
        {
            sum += sanitized[i];
        }

        if (sum > MaxKernelSum && sum > 0)
        {
            var scale = MaxKernelSum / sum;
            for (var i = 0; i < sanitized.Length; i++)
            {
                sanitized[i] *= scale;
            }

            messages.Add($"Retry kernel coefficients scaled by {scale:F3} to keep total mass ≤ {MaxKernelSum:F2}.");
        }

        IReadOnlyList<string> readonlyMessages = messages.Count == 0
            ? Array.Empty<string>()
            : new ReadOnlyCollection<string>(messages);

        return new RetryKernelPolicyResult(sanitized, readonlyMessages, false);
    }
}

public sealed record RetryKernelPolicyResult(
    double[] Kernel,
    IReadOnlyList<string> Messages,
    bool UsedDefault)
{
    public bool HasMessages => Messages.Count > 0;
}
