using System.Security.Cryptography;
using System.Text;
using FlowTime.Sim.Core;
using Xunit;

namespace FlowTime.Sim.Tests;

public class PcgRngSnapshotTests
{
    private static string Hash(string payload)
    {
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(payload)));
    }

    [Fact]
    public void First_16_Samples_Are_Stable_For_Seed_123()
    {
        var rng = new Pcg32Rng(123);
        var samples = new double[16];
        for (int i = 0; i < samples.Length; i++) samples[i] = rng.NextDouble();
        // Fixed formatting to ensure culture invariance and stable string prior to hashing.
        var payload = string.Join(",", samples.Select(v => v.ToString("R", System.Globalization.CultureInfo.InvariantCulture)));
        var hash = Hash(payload);
    // Snapshot locked for PCG implementation (seed=123, first 16 samples). Update only with intentional RNG change + release note.
    const string Expected = "6D6D830EAB34805864FB8AB2C38AE4E95395864B9960FF99B5C96F8E54E6F4E9";
    Assert.Equal(Expected, hash);
    }
}
