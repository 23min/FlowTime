using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace FlowTime.Contracts.Storage;

internal sealed class SqliteStorageIndexStore : IStorageIndexStore
{
    private readonly string connectionString;
    private readonly JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web);

    public SqliteStorageIndexStore(string connectionStringOrPath)
    {
        connectionString = NormalizeConnectionString(connectionStringOrPath);
        EnsureDatabase();
    }

    public async Task<StorageIndexEntry?> GetAsync(StorageRef reference, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var command = connection.CreateCommand();
        command.CommandText = """
                              SELECT kind, id, hash, size_bytes, updated_utc, content_type, metadata_json
                              FROM storage_index
                              WHERE kind = $kind AND id = $id
                              """;
        command.Parameters.AddWithValue("$kind", reference.Kind.ToString().ToLowerInvariant());
        command.Parameters.AddWithValue("$id", reference.Id);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return ReadEntry(reader);
    }

    public async Task<IReadOnlyList<StorageIndexEntry>> ListAsync(StorageKind kind, CancellationToken cancellationToken = default)
    {
        var results = new List<StorageIndexEntry>();
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var command = connection.CreateCommand();
        command.CommandText = """
                              SELECT kind, id, hash, size_bytes, updated_utc, content_type, metadata_json
                              FROM storage_index
                              WHERE kind = $kind
                              """;
        command.Parameters.AddWithValue("$kind", kind.ToString().ToLowerInvariant());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            results.Add(ReadEntry(reader));
        }

        return results;
    }

    public async Task UpsertAsync(StorageIndexEntry entry, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var command = connection.CreateCommand();
        command.CommandText = """
                              INSERT INTO storage_index (kind, id, hash, size_bytes, updated_utc, content_type, metadata_json)
                              VALUES ($kind, $id, $hash, $size, $updated, $content_type, $metadata)
                              ON CONFLICT(kind, id) DO UPDATE SET
                                  hash = excluded.hash,
                                  size_bytes = excluded.size_bytes,
                                  updated_utc = excluded.updated_utc,
                                  content_type = excluded.content_type,
                                  metadata_json = excluded.metadata_json
                              """;
        command.Parameters.AddWithValue("$kind", entry.Kind.ToString().ToLowerInvariant());
        command.Parameters.AddWithValue("$id", entry.Id);
        command.Parameters.AddWithValue("$hash", entry.ContentHash ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$size", entry.SizeBytes);
        command.Parameters.AddWithValue("$updated", entry.UpdatedUtc.ToString("o"));
        command.Parameters.AddWithValue("$content_type", entry.ContentType ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$metadata", SerializeMetadata(entry.Metadata));

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> RemoveAsync(StorageRef reference, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM storage_index WHERE kind = $kind AND id = $id";
        command.Parameters.AddWithValue("$kind", reference.Kind.ToString().ToLowerInvariant());
        command.Parameters.AddWithValue("$id", reference.Id);

        var rows = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return rows > 0;
    }

    private void EnsureDatabase()
    {
        using var connection = new SqliteConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
                              CREATE TABLE IF NOT EXISTS storage_index (
                                  kind TEXT NOT NULL,
                                  id TEXT NOT NULL,
                                  hash TEXT NULL,
                                  size_bytes INTEGER NOT NULL,
                                  updated_utc TEXT NOT NULL,
                                  content_type TEXT NULL,
                                  metadata_json TEXT NULL,
                                  PRIMARY KEY (kind, id)
                              );
                              CREATE INDEX IF NOT EXISTS idx_storage_kind ON storage_index(kind);
                              """;
        command.ExecuteNonQuery();
    }

    private StorageIndexEntry ReadEntry(SqliteDataReader reader)
    {
        var kindValue = reader.GetString(0);
        _ = Enum.TryParse<StorageKind>(kindValue, true, out var kind);
        var id = reader.GetString(1);
        var hash = reader.IsDBNull(2) ? null : reader.GetString(2);
        var size = reader.GetInt64(3);
        var updated = DateTimeOffset.Parse(reader.GetString(4));
        var contentType = reader.IsDBNull(5) ? null : reader.GetString(5);
        var metadataJson = reader.IsDBNull(6) ? null : reader.GetString(6);

        return new StorageIndexEntry
        {
            Kind = kind,
            Id = id,
            ContentHash = hash,
            SizeBytes = size,
            UpdatedUtc = updated,
            ContentType = contentType,
            Metadata = DeserializeMetadata(metadataJson)
        };
    }

    private string? SerializeMetadata(Dictionary<string, string>? metadata)
    {
        if (metadata is null || metadata.Count == 0)
        {
            return null;
        }

        return JsonSerializer.Serialize(metadata, jsonOptions);
    }

    private Dictionary<string, string>? DeserializeMetadata(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        return JsonSerializer.Deserialize<Dictionary<string, string>>(json, jsonOptions);
    }

    private static string NormalizeConnectionString(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Data Source=storage.db";
        }

        return value.Contains("Data Source=", StringComparison.OrdinalIgnoreCase)
            ? value
            : $"Data Source={value}";
    }
}
