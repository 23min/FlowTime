using System.Text.Json;
using FlowTime.Core.Models;
using Xunit;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace FlowTime.Core.Tests.Models;

public class ParallelismReferenceSerializationTests
{
    [Fact]
    public void JsonSerializer_SerializesLiteralAsNumber()
    {
        var json = JsonSerializer.Serialize(ParallelismReference.Literal(3d));

        Assert.Equal("3", json);
    }

    [Fact]
    public void JsonSerializer_SerializesSeriesAsString()
    {
        var json = JsonSerializer.Serialize(ParallelismReference.Series("worker_count"));

        Assert.Equal("\"worker_count\"", json);
    }

    [Fact]
    public void JsonSerializer_DeserializesNumberToLiteral()
    {
        var reference = JsonSerializer.Deserialize<ParallelismReference>("3");

        Assert.NotNull(reference);
        Assert.Equal(3d, reference!.Constant);
        Assert.Null(reference.SeriesReference);
    }

    [Fact]
    public void JsonSerializer_DeserializesStringToSeries()
    {
        var reference = JsonSerializer.Deserialize<ParallelismReference>("\"worker_count\"");

        Assert.NotNull(reference);
        Assert.NotNull(reference!.SeriesReference);
        Assert.Equal("worker_count", reference.SeriesReference!.RawText);
        Assert.Equal("worker_count", reference.SeriesReference.NodeId);
        Assert.Null(reference.Constant);
    }

    [Fact]
    public void YamlDeserializer_ParsesScalarIntoParallelismReference()
    {
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        var holder = deserializer.Deserialize<ParallelismHolder>("parallelism: worker_count");

        Assert.NotNull(holder.Parallelism);
        Assert.NotNull(holder.Parallelism!.SeriesReference);
        Assert.Equal("worker_count", holder.Parallelism.SeriesReference!.RawText);
        Assert.Null(holder.Parallelism.Constant);
    }

    [Fact]
    public void YamlSerializer_EmitsScalarForParallelismReference()
    {
        var serializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        var yaml = serializer.Serialize(new ParallelismHolder
        {
            Parallelism = ParallelismReference.Literal(2d)
        });

        Assert.Contains("parallelism: 2", yaml);
        Assert.DoesNotContain("constant:", yaml);
        Assert.DoesNotContain("reference:", yaml);
    }

    private sealed class ParallelismHolder
    {
        public ParallelismReference? Parallelism { get; set; }
    }
}