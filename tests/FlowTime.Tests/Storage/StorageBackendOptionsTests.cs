using FlowTime.Contracts.Storage;
using Microsoft.Extensions.Configuration;

namespace FlowTime.Tests.Storage;

public sealed class StorageBackendOptionsTests
{
    [Fact]
    public void FromConfiguration_UsesDefaults_WhenMissing()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();

        var options = StorageBackendOptions.FromConfiguration(config);

        Assert.Equal(StorageBackendKind.FileSystem, options.Backend);
        Assert.Equal(StorageIndexKind.File, options.Index);
        Assert.Equal("data", options.Root);
    }

    [Theory]
    [InlineData("filesystem", StorageBackendKind.FileSystem, StorageIndexKind.File)]
    [InlineData("blob", StorageBackendKind.Blob, StorageIndexKind.File)]
    [InlineData("blob+db", StorageBackendKind.BlobWithDatabase, StorageIndexKind.Database)]
    [InlineData("blobdb", StorageBackendKind.BlobWithDatabase, StorageIndexKind.Database)]
    public void FromConfiguration_ParsesBackend(string backend, StorageBackendKind expectedBackend, StorageIndexKind expectedIndex)
    {
        var settings = new Dictionary<string, string?>
        {
            ["Storage:Backend"] = backend
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        var options = StorageBackendOptions.FromConfiguration(config);

        Assert.Equal(expectedBackend, options.Backend);
        Assert.Equal(expectedIndex, options.Index);
    }

    [Fact]
    public void FromConfiguration_AllowsIndexOverride()
    {
        var settings = new Dictionary<string, string?>
        {
            ["Storage:Backend"] = "blob",
            ["Storage:Index"] = "database"
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        var options = StorageBackendOptions.FromConfiguration(config);

        Assert.Equal(StorageBackendKind.Blob, options.Backend);
        Assert.Equal(StorageIndexKind.Database, options.Index);
    }

    [Fact]
    public void FromConfiguration_UsesRootAndContainer()
    {
        var settings = new Dictionary<string, string?>
        {
            ["Storage:Root"] = "/data/storage",
            ["Storage:Container"] = "flowtime-artifacts"
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        var options = StorageBackendOptions.FromConfiguration(config);

        Assert.Equal("/data/storage", options.Root);
        Assert.Equal("flowtime-artifacts", options.Container);
    }
}
