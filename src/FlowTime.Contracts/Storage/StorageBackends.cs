namespace FlowTime.Contracts.Storage;

public sealed class FileSystemStorageBackend : IStorageBackend
{
    private readonly string root;
    private readonly IStorageIndexStore indexStore;

    public FileSystemStorageBackend(string root)
    {
        this.root = root;
        Directory.CreateDirectory(root);
        indexStore = new FileStorageIndexStore(Path.Combine(root, "storage-index.json"));
    }

    public StorageBackendKind BackendKind => StorageBackendKind.FileSystem;

    public async Task<StorageWriteResult> WriteAsync(StorageWriteRequest request, CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);

        var hash = StorageHash.ComputeSha256(request.Content);
        if (!string.IsNullOrWhiteSpace(request.ExpectedHash) && !string.Equals(request.ExpectedHash, hash, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Content hash did not match expected value.");
        }

        var path = BuildPath(request.Kind, request.Id);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, request.Content, cancellationToken).ConfigureAwait(false);

        var entry = new StorageIndexEntry
        {
            Kind = request.Kind,
            Id = request.Id,
            ContentHash = hash,
            ContentType = request.ContentType,
            SizeBytes = request.Content.LongLength,
            UpdatedUtc = DateTimeOffset.UtcNow,
            Metadata = request.Metadata is null ? null : new Dictionary<string, string>(request.Metadata, StringComparer.OrdinalIgnoreCase)
        };
        await indexStore.UpsertAsync(entry, cancellationToken).ConfigureAwait(false);

        var reference = new StorageRef
        {
            Kind = request.Kind,
            Id = request.Id,
            Hash = hash
        };

        return new StorageWriteResult
        {
            Reference = reference,
            ContentHash = hash,
            SizeBytes = request.Content.LongLength
        };
    }

    public async Task<StorageReadResult?> ReadAsync(StorageRef reference, CancellationToken cancellationToken = default)
    {
        var entry = await indexStore.GetAsync(reference, cancellationToken).ConfigureAwait(false);
        if (entry is null)
        {
            return null;
        }

        var path = BuildPath(reference.Kind, reference.Id);
        if (!File.Exists(path))
        {
            return null;
        }

        var content = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        return new StorageReadResult
        {
            Reference = reference,
            Content = content,
            ContentHash = entry.ContentHash,
            ContentType = entry.ContentType,
            Metadata = entry.Metadata
        };
    }

    public async Task<IReadOnlyList<StorageItemSummary>> ListAsync(StorageListRequest request, CancellationToken cancellationToken = default)
    {
        var entries = await indexStore.ListAsync(request.Kind, cancellationToken).ConfigureAwait(false);
        var items = entries
            .OrderByDescending(entry => entry.UpdatedUtc)
            .Select(entry => new StorageItemSummary
            {
                Reference = new StorageRef { Kind = entry.Kind, Id = entry.Id, Hash = entry.ContentHash },
                ContentHash = entry.ContentHash,
                SizeBytes = entry.SizeBytes,
                UpdatedUtc = entry.UpdatedUtc,
                Metadata = entry.Metadata,
                ContentType = entry.ContentType
            })
            .ToList();

        if (request.Limit is > 0)
        {
            items = items.Take(request.Limit.Value).ToList();
        }

        return items;
    }

    public async Task<bool> DeleteAsync(StorageRef reference, CancellationToken cancellationToken = default)
    {
        var path = BuildPath(reference.Kind, reference.Id);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return await indexStore.RemoveAsync(reference, cancellationToken).ConfigureAwait(false);
    }

    private string BuildPath(StorageKind kind, string id)
    {
        var folder = StoragePathHelper.GetKindFolder(kind);
        return Path.Combine(root, folder, id);
    }

    private static void ValidateRequest(StorageWriteRequest request)
    {
        if (!StorageRef.IsValidId(request.Id))
        {
            throw new ArgumentException("Storage id contains invalid characters.", nameof(request));
        }
    }
}

public sealed class BlobStorageBackend : IStorageBackend
{
    private readonly StorageBackendKind backendKind;
    private readonly string root;
    private readonly string blobRoot;
    private readonly IStorageIndexStore indexStore;

    public BlobStorageBackend(StorageBackendOptions options)
    {
        backendKind = options.Backend;
        root = options.Root;
        blobRoot = Path.Combine(root, "blobs");
        Directory.CreateDirectory(blobRoot);

        indexStore = options.Index switch
        {
            StorageIndexKind.Database => new SqliteStorageIndexStore(options.ConnectionString ?? Path.Combine(root, "storage.db")),
            _ => new FileStorageIndexStore(Path.Combine(root, "storage-index.json"))
        };
    }

    public StorageBackendKind BackendKind => backendKind;

    public async Task<StorageWriteResult> WriteAsync(StorageWriteRequest request, CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);

        var hash = StorageHash.ComputeSha256(request.Content);
        if (!string.IsNullOrWhiteSpace(request.ExpectedHash) && !string.Equals(request.ExpectedHash, hash, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Content hash did not match expected value.");
        }

        var path = BuildPath(request.Kind, request.Id);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, request.Content, cancellationToken).ConfigureAwait(false);

        var entry = new StorageIndexEntry
        {
            Kind = request.Kind,
            Id = request.Id,
            ContentHash = hash,
            ContentType = request.ContentType,
            SizeBytes = request.Content.LongLength,
            UpdatedUtc = DateTimeOffset.UtcNow,
            Metadata = request.Metadata is null ? null : new Dictionary<string, string>(request.Metadata, StringComparer.OrdinalIgnoreCase)
        };
        await indexStore.UpsertAsync(entry, cancellationToken).ConfigureAwait(false);

        var reference = new StorageRef
        {
            Kind = request.Kind,
            Id = request.Id,
            Hash = hash
        };

        return new StorageWriteResult
        {
            Reference = reference,
            ContentHash = hash,
            SizeBytes = request.Content.LongLength
        };
    }

    public async Task<StorageReadResult?> ReadAsync(StorageRef reference, CancellationToken cancellationToken = default)
    {
        var entry = await indexStore.GetAsync(reference, cancellationToken).ConfigureAwait(false);
        if (entry is null)
        {
            return null;
        }

        var path = BuildPath(reference.Kind, reference.Id);
        if (!File.Exists(path))
        {
            return null;
        }

        var content = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        return new StorageReadResult
        {
            Reference = reference,
            Content = content,
            ContentHash = entry.ContentHash,
            ContentType = entry.ContentType,
            Metadata = entry.Metadata
        };
    }

    public async Task<IReadOnlyList<StorageItemSummary>> ListAsync(StorageListRequest request, CancellationToken cancellationToken = default)
    {
        var entries = await indexStore.ListAsync(request.Kind, cancellationToken).ConfigureAwait(false);
        var items = entries
            .OrderByDescending(entry => entry.UpdatedUtc)
            .Select(entry => new StorageItemSummary
            {
                Reference = new StorageRef { Kind = entry.Kind, Id = entry.Id, Hash = entry.ContentHash },
                ContentHash = entry.ContentHash,
                SizeBytes = entry.SizeBytes,
                UpdatedUtc = entry.UpdatedUtc,
                Metadata = entry.Metadata,
                ContentType = entry.ContentType
            })
            .ToList();

        if (request.Limit is > 0)
        {
            items = items.Take(request.Limit.Value).ToList();
        }

        return items;
    }

    public async Task<bool> DeleteAsync(StorageRef reference, CancellationToken cancellationToken = default)
    {
        var path = BuildPath(reference.Kind, reference.Id);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return await indexStore.RemoveAsync(reference, cancellationToken).ConfigureAwait(false);
    }

    private string BuildPath(StorageKind kind, string id)
    {
        var folder = StoragePathHelper.GetKindFolder(kind);
        return Path.Combine(blobRoot, folder, id);
    }

    private static void ValidateRequest(StorageWriteRequest request)
    {
        if (!StorageRef.IsValidId(request.Id))
        {
            throw new ArgumentException("Storage id contains invalid characters.", nameof(request));
        }
    }
}

public static class StorageBackendFactory
{
    public static IStorageBackend Create(StorageBackendOptions options)
    {
        return options.Backend switch
        {
            StorageBackendKind.Blob => new BlobStorageBackend(options),
            StorageBackendKind.BlobWithDatabase => new BlobStorageBackend(options),
            _ => new FileSystemStorageBackend(options.Root)
        };
    }
}

internal static class StoragePathHelper
{
    public static string GetKindFolder(StorageKind kind)
    {
        return kind switch
        {
            StorageKind.Model => "models",
            StorageKind.Series => "series",
            _ => "items"
        };
    }
}
