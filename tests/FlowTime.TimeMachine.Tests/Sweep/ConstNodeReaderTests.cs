using FlowTime.TimeMachine.Sweep;

namespace FlowTime.TimeMachine.Tests.Sweep;

public sealed class ConstNodeReaderTests
{
    // ── Fixtures ───────────────────────────────────────────────────────────

    private static string ModelWith(double value, int bins = 4) => $"""
        schemaVersion: 1
        grid:
          bins: {bins}
          binSize: 15
          binUnit: minutes
        nodes:
          - id: arrivals
            kind: const
            values: [{string.Join(", ", Enumerable.Repeat(value.ToString(System.Globalization.CultureInfo.InvariantCulture), bins))}]
        """;

    // ── Happy path ─────────────────────────────────────────────────────────

    [Fact]
    public void ReadValue_KnownConstNode_ReturnsFirstBinValue()
    {
        var yaml = ModelWith(42.0);
        var result = ConstNodeReader.ReadValue(yaml, "arrivals");
        Assert.Equal(42.0, result);
    }

    [Fact]
    public void ReadValue_DecimalValue_ParsedCorrectly()
    {
        var yaml = ModelWith(3.14);
        var result = ConstNodeReader.ReadValue(yaml, "arrivals");
        Assert.Equal(3.14, result);
    }

    [Fact]
    public void ReadValue_MultipleNodes_ReturnsCorrectOne()
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

        Assert.Equal(10.0, ConstNodeReader.ReadValue(yaml, "arrivals"));
        Assert.Equal(20.0, ConstNodeReader.ReadValue(yaml, "capacity"));
    }

    // ── Graceful null cases ────────────────────────────────────────────────

    [Fact]
    public void ReadValue_UnknownNode_ReturnsNull()
    {
        var yaml = ModelWith(10.0);
        var result = ConstNodeReader.ReadValue(yaml, "nonexistent");
        Assert.Null(result);
    }

    [Fact]
    public void ReadValue_NonConstNode_ReturnsNull()
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

        Assert.Null(ConstNodeReader.ReadValue(yaml, "throughput"));
    }

    [Fact]
    public void ReadValue_NoNodesSection_ReturnsNull()
    {
        var yaml = """
            schemaVersion: 1
            grid:
              bins: 4
              binSize: 15
              binUnit: minutes
            """;

        Assert.Null(ConstNodeReader.ReadValue(yaml, "arrivals"));
    }

    [Fact]
    public void ReadValue_ZeroValue_ReturnsZero()
    {
        var yaml = ModelWith(0.0);
        var result = ConstNodeReader.ReadValue(yaml, "arrivals");
        Assert.Equal(0.0, result);
    }
}
