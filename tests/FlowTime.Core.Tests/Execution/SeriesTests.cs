using FlowTime.Core.Execution;

namespace FlowTime.Core.Tests.Execution;

public class SeriesTests
{
    [Fact]
    public void Indexer_HasNoPublicSetter()
    {
        var indexer = typeof(Series).GetProperty("Item");
        Assert.NotNull(indexer);

        var setter = indexer!.GetSetMethod(nonPublic: false);
        Assert.Null(setter);
    }

    [Fact]
    public void Constructor_WithArray_PreservesValues()
    {
        var data = new double[] { 1.0, 2.0, 3.0 };
        var series = new Series(data);

        Assert.Equal(1.0, series[0]);
        Assert.Equal(2.0, series[1]);
        Assert.Equal(3.0, series[2]);
        Assert.Equal(3, series.Length);
    }

    [Fact]
    public void ToArray_ReturnsCopyNotReference()
    {
        var data = new double[] { 1.0, 2.0, 3.0 };
        var series = new Series(data);

        var copy = series.ToArray();
        copy[0] = 999.0;

        Assert.Equal(1.0, series[0]);
    }

    [Fact]
    public void Constructor_DoesNotAlias_InputArray()
    {
        var data = new double[] { 1.0, 2.0, 3.0 };
        var series = new Series(data);

        data[0] = 999.0;

        Assert.Equal(1.0, series[0]);
    }
}
