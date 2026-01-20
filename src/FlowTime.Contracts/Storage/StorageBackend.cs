using System.Text.Json;

namespace FlowTime.Contracts.Storage;

public sealed record StorageWriteRequest
{
    public required StorageKind Kind { get; init; }
    public required string Id { get; init; }
    public required byte[] Content { get; init; }
    public string? ContentType { get; init; }
    public string? ExpectedHash { get; init; }
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }
}

public sealed record StorageWriteResult
{
    public required StorageRef Reference { get; init; }
    public required string ContentHash { get; init; }
    public required long SizeBytes { get; init; }
}

public sealed record StorageReadResult
{
    public required StorageRef Reference { get; init; }
    public required byte[] Content { get; init; }
    public string? ContentType { get; init; }
    public string? ContentHash { get; init; }
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }
}

public sealed record StorageListRequest
{
    public required StorageKind Kind { get; init; }
    public int? Limit { get; init; }
}

public sealed record StorageItemSummary
{
    public required StorageRef Reference { get; init; }
    public string? ContentHash { get; init; }
    public long SizeBytes { get; init; }
    public DateTimeOffset UpdatedUtc { get; init; }
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }
    public string? ContentType { get; init; }
}

public interface IStorageBackend
{
    StorageBackendKind BackendKind { get; }

    Task<StorageWriteResult> WriteAsync(StorageWriteRequest request, CancellationToken cancellationToken = default);
    Task<StorageReadResult?> ReadAsync(StorageRef reference, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StorageItemSummary>> ListAsync(StorageListRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(StorageRef reference, CancellationToken cancellationToken = default);
}

internal sealed record StorageIndexEntry
{
    public required StorageKind Kind { get; init; }
    public required string Id { get; init; }
    public string? ContentHash { get; init; }
    public long SizeBytes { get; init; }
    public DateTimeOffset UpdatedUtc { get; init; }
    public string? ContentType { get; init; }
    public Dictionary<string, string>? Metadata { get; init; }
}

internal interface IStorageIndexStore
{
    Task<StorageIndexEntry?> GetAsync(StorageRef reference, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StorageIndexEntry>> ListAsync(StorageKind kind, CancellationToken cancellationToken = default);
    Task UpsertAsync(StorageIndexEntry entry, CancellationToken cancellationToken = default);
    Task<bool> RemoveAsync(StorageRef reference, CancellationToken cancellationToken = default);
}

internal sealed class FileStorageIndexStore : IStorageIndexStore
{
    private readonly string indexFilePath;
    private readonly SemaphoreSlim indexLock = new(1, 1);
    private readonly JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public FileStorageIndexStore(string indexFilePath)
    {
        this.indexFilePath = indexFilePath;
    }

    public async Task<StorageIndexEntry?> GetAsync(StorageRef reference, CancellationToken cancellationToken = default)
    {
        var entries = await LoadEntriesAsync(cancellationToken).ConfigureAwait(false);
        return entries.FirstOrDefault(entry => entry.Kind == reference.Kind && entry.Id == reference.Id);
    }

    public async Task<IReadOnlyList<StorageIndexEntry>> ListAsync(StorageKind kind, CancellationToken cancellationToken = default)
    {
        var entries = await LoadEntriesAsync(cancellationToken).ConfigureAwait(false);
        return entries.Where(entry => entry.Kind == kind).ToList();
    }

    public async Task UpsertAsync(StorageIndexEntry entry, CancellationToken cancellationToken = default)
    {
        await indexLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var document = await LoadDocumentAsync(cancellationToken).ConfigureAwait(false);
            var existing = document.Entries.FirstOrDefault(e => e.Kind == entry.Kind && e.Id == entry.Id);
            if (existing is not null)
            {
                document.Entries.Remove(existing);
            }

            document.Entries.Add(entry);
            document.UpdatedUtc = DateTimeOffset.UtcNow;
            await SaveDocumentAsync(document, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            indexLock.Release();
        }
    }

    public async Task<bool> RemoveAsync(StorageRef reference, CancellationToken cancellationToken = default)
    {
        await indexLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var document = await LoadDocumentAsync(cancellationToken).ConfigureAwait(false);
            var removed = document.Entries.RemoveAll(entry => entry.Kind == reference.Kind && entry.Id == reference.Id) > 0;
            if (removed)
            {
                document.UpdatedUtc = DateTimeOffset.UtcNow;
                await SaveDocumentAsync(document, cancellationToken).ConfigureAwait(false);
            }
            return removed;
        }
        finally
        {
            indexLock.Release();
        }
    }

    private async Task<IReadOnlyList<StorageIndexEntry>> LoadEntriesAsync(CancellationToken cancellationToken)
    {
        var document = await LoadDocumentAsync(cancellationToken).ConfigureAwait(false);
        return document.Entries;
    }

    private async Task<StorageIndexDocument> LoadDocumentAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(indexFilePath))
        {
            return new StorageIndexDocument();
        }

        var json = await File.ReadAllTextAsync(indexFilePath, cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize<StorageIndexDocument>(json, jsonOptions) ?? new StorageIndexDocument();
    }

    private async Task SaveDocumentAsync(StorageIndexDocument document, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(indexFilePath)!);
        var json = JsonSerializer.Serialize(document, jsonOptions);
        await File.WriteAllTextAsync(indexFilePath, json, cancellationToken).ConfigureAwait(false);
    }

    private sealed class StorageIndexDocument
    {
        public int SchemaVersion { get; set; } = 1;
        public DateTimeOffset UpdatedUtc { get; set; } = DateTimeOffset.UtcNow;
        public List<StorageIndexEntry> Entries { get; set; } = new();
    }
}
