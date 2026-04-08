using FlowTime.Contracts.Storage;

namespace FlowTime.Tests.Storage;

public sealed class StorageRefTests
{
    [Fact]
    public void TryParse_ReturnsFalse_ForEmpty()
    {
        var success = StorageRef.TryParse("", out var reference, out var error);

        Assert.False(success);
        Assert.Null(reference);
        Assert.NotNull(error);
    }

    [Fact]
    public void TryParse_ParsesValidStorageUri()
    {
        var success = StorageRef.TryParse("storage://model/model_001", out var reference, out var error);

        Assert.True(success);
        Assert.Null(error);
        Assert.NotNull(reference);
        Assert.Equal(StorageKind.Model, reference!.Kind);
        Assert.Equal("model_001", reference.Id);
    }

    [Fact]
    public void TryParse_ParsesOptionalQueryFields()
    {
        var hash = StorageHash.ComputeSha256("hello");
        var success = StorageRef.TryParse($"storage://run/run_123?h={hash}&v=2", out var reference, out var error);

        Assert.True(success);
        Assert.Null(error);
        Assert.NotNull(reference);
        Assert.Equal(StorageKind.Run, reference!.Kind);
        Assert.Equal("run_123", reference.Id);
        Assert.Equal("2", reference.Version);
        Assert.Equal(hash, reference.Hash);
    }

    [Fact]
    public void ToString_RoundTrips()
    {
        var hash = StorageHash.ComputeSha256("hello");
        var reference = new StorageRef
        {
            Kind = StorageKind.Model,
            Id = "model-01",
            Version = "v3",
            Hash = hash
        };

        var success = StorageRef.TryParse(reference.ToString(), out var parsed, out var error);

        Assert.True(success);
        Assert.Null(error);
        Assert.Equal(reference.Kind, parsed!.Kind);
        Assert.Equal(reference.Id, parsed.Id);
        Assert.Equal(reference.Version, parsed.Version);
        Assert.Equal(reference.Hash, parsed.Hash);
    }
}
