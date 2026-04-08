using System.Text;
using FlowTime.Contracts.Storage;
namespace FlowTime.Tests.Storage;

public sealed class FileSystemStorageBackendTests
{
    [Fact]
    public async Task WriteReadListDelete_WorksEndToEnd()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"ft_storage_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempPath);
        try
        {
            var backend = new FileSystemStorageBackend(tempPath);
            var payload = Encoding.UTF8.GetBytes("model-content");

            var write = await backend.WriteAsync(new StorageWriteRequest
            {
                Kind = StorageKind.Model,
                Id = "model_1",
                Content = payload,
                ContentType = "text/yaml"
            });

            var read = await backend.ReadAsync(write.Reference);

            Assert.NotNull(read);
            Assert.Equal(payload, read!.Content);
            Assert.Equal(write.ContentHash, read.ContentHash);

            var list = await backend.ListAsync(new StorageListRequest { Kind = StorageKind.Model });
            Assert.Single(list);
            Assert.Equal("model_1", list[0].Reference.Id);

            var deleted = await backend.DeleteAsync(write.Reference);
            Assert.True(deleted);

            var afterDelete = await backend.ReadAsync(write.Reference);
            Assert.Null(afterDelete);
        }
        finally
        {
            if (Directory.Exists(tempPath))
            {
                Directory.Delete(tempPath, recursive: true);
            }
        }
    }
}
