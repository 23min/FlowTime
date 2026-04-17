using System.Globalization;
using FlowTime.TimeMachine.Sweep;

namespace FlowTime.TimeMachine.Tests.Sweep;

public sealed class ConstNodePatcherTests
{
    // ── Fixtures ───────────────────────────────────────────────────────────

    private static string ModelWithConstNode(int bins = 4, double fillValue = 10.0) => $"""
        schemaVersion: 1
        grid:
          bins: {bins}
          binSize: 15
          binUnit: minutes
        nodes:
          - id: arrivals
            kind: const
            values: [{string.Join(", ", Enumerable.Repeat(fillValue.ToString(CultureInfo.InvariantCulture), bins))}]
        """;

    // ── Patching happy path ────────────────────────────────────────────────

    [Fact]
    public void Patch_KnownConstNode_ReplacesAllValues()
    {
        var yaml = ModelWithConstNode(bins: 4, fillValue: 10.0);

        var patched = ConstNodePatcher.Patch(yaml, "arrivals", 25.0);

        // The patched YAML should round-trip back to a model where arrivals has value 25 in every bin
        Assert.Contains("25", patched);
        Assert.DoesNotContain("10", patched);
    }

    [Fact]
    public void Patch_PreservesBinCount()
    {
        var yaml = ModelWithConstNode(bins: 8, fillValue: 5.0);

        var patched = ConstNodePatcher.Patch(yaml, "arrivals", 99.0);

        // Count how many times "99" appears (one per bin) by parsing the YAML
        var count = CountOccurrences(patched, "99");
        Assert.Equal(8, count);
    }

    [Fact]
    public void Patch_DecimalValue_UsesInvariantCulture()
    {
        var yaml = ModelWithConstNode(bins: 2, fillValue: 1.0);

        var patched = ConstNodePatcher.Patch(yaml, "arrivals", 3.14);

        // Must use '.' decimal separator, not locale-dependent ','
        Assert.Contains("3.14", patched);
    }

    [Fact]
    public void Patch_MultipleNodes_OnlyPatchesTarget()
    {
        var yaml = """
            schemaVersion: 1
            grid:
              bins: 2
              binSize: 15
              binUnit: minutes
            nodes:
              - id: arrivals
                kind: const
                values: [10, 10]
              - id: capacity
                kind: const
                values: [20, 20]
            """;

        var patched = ConstNodePatcher.Patch(yaml, "arrivals", 50.0);

        // arrivals should be 50; capacity should remain 20
        Assert.Contains("50", patched);
        Assert.Contains("20", patched);
    }

    // ── Graceful fallback cases ────────────────────────────────────────────

    [Fact]
    public void Patch_UnknownNodeId_ReturnsOriginalUnchanged()
    {
        var yaml = ModelWithConstNode(bins: 4, fillValue: 10.0);

        var result = ConstNodePatcher.Patch(yaml, "nonexistent", 99.0);

        // Output should not contain 99 — original values preserved
        Assert.DoesNotContain("99", result);
        Assert.Contains("10", result);
    }

    [Fact]
    public void Patch_NonConstNodeKind_ReturnsOriginalUnchanged()
    {
        var yaml = """
            schemaVersion: 1
            grid:
              bins: 2
              binSize: 15
              binUnit: minutes
            nodes:
              - id: throughput
                kind: expr
                expr: "arrivals * 0.9"
            """;

        var result = ConstNodePatcher.Patch(yaml, "throughput", 99.0);

        Assert.DoesNotContain("99", result);
    }

    [Fact]
    public void Patch_NoNodesSection_ReturnsOriginalUnchanged()
    {
        var yaml = """
            schemaVersion: 1
            grid:
              bins: 4
              binSize: 15
              binUnit: minutes
            """;

        var result = ConstNodePatcher.Patch(yaml, "arrivals", 99.0);

        Assert.DoesNotContain("99", result);
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static int CountOccurrences(string text, string term)
    {
        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(term, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += term.Length;
        }
        return count;
    }
}
