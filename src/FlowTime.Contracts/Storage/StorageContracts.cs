using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace FlowTime.Contracts.Storage;

public enum StorageKind
{
    Model,
    Series
}

public enum StorageBackendKind
{
    FileSystem,
    Blob,
    BlobWithDatabase
}

public enum StorageIndexKind
{
    File,
    Database
}

public sealed record StorageBackendOptions
{
    public StorageBackendKind Backend { get; init; } = StorageBackendKind.FileSystem;
    public StorageIndexKind Index { get; init; } = StorageIndexKind.File;
    public string Root { get; init; } = "data";
    public string? Container { get; init; }
    public string? ConnectionString { get; init; }

    public static StorageBackendOptions FromConfiguration(IConfiguration configuration, string sectionName = "Storage")
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection(sectionName);
        var backendValue = section["Backend"];
        var backend = ParseBackend(backendValue, out var defaultIndex);
        var indexValue = section["Index"];
        var index = ParseIndex(indexValue, defaultIndex);

        var root = section["Root"] ?? section["Directory"] ?? "data";
        var container = section["Container"];
        var connectionString = section["ConnectionString"] ?? section["DbConnection"];

        return new StorageBackendOptions
        {
            Backend = backend,
            Index = index,
            Root = root,
            Container = container,
            ConnectionString = connectionString
        };
    }

    private static StorageBackendKind ParseBackend(string? value, out StorageIndexKind defaultIndex)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            defaultIndex = StorageIndexKind.File;
            return StorageBackendKind.FileSystem;
        }

        var normalized = value.Trim().ToLowerInvariant();
        switch (normalized)
        {
            case "filesystem":
            case "file":
            case "fs":
                defaultIndex = StorageIndexKind.File;
                return StorageBackendKind.FileSystem;
            case "blob":
            case "object":
            case "objectstorage":
                defaultIndex = StorageIndexKind.File;
                return StorageBackendKind.Blob;
            case "blob+db":
            case "blobdb":
            case "blob-db":
            case "blobwithdb":
            case "blobwithdatabase":
                defaultIndex = StorageIndexKind.Database;
                return StorageBackendKind.BlobWithDatabase;
            default:
                defaultIndex = StorageIndexKind.File;
                return StorageBackendKind.FileSystem;
        }
    }

    private static StorageIndexKind ParseIndex(string? value, StorageIndexKind defaultIndex)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultIndex;
        }

        var normalized = value.Trim().ToLowerInvariant();
        return normalized switch
        {
            "db" or "database" => StorageIndexKind.Database,
            _ => StorageIndexKind.File
        };
    }
}

public sealed record StorageRef
{
    public required StorageKind Kind { get; init; }
    public required string Id { get; init; }
    public string? Version { get; init; }
    public string? Hash { get; init; }

    public override string ToString() => ToUriString();

    public string ToUriString()
    {
        var builder = new StringBuilder("storage://");
        builder.Append(Kind.ToString().ToLowerInvariant());
        builder.Append('/');
        builder.Append(Uri.EscapeDataString(Id));

        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(Version))
        {
            query.Add($"v={Uri.EscapeDataString(Version)}");
        }
        if (!string.IsNullOrWhiteSpace(Hash))
        {
            query.Add($"h={Uri.EscapeDataString(Hash)}");
        }

        if (query.Count > 0)
        {
            builder.Append('?');
            builder.Append(string.Join("&", query));
        }

        return builder.ToString();
    }

    public static bool TryParse(string? value, out StorageRef? reference, out string? error)
    {
        reference = null;
        error = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            error = "Storage reference is required.";
            return false;
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            error = "Storage reference must be a valid URI.";
            return false;
        }

        if (!string.Equals(uri.Scheme, "storage", StringComparison.OrdinalIgnoreCase))
        {
            error = "Storage reference must use the storage:// scheme.";
            return false;
        }

        var host = uri.Host;
        var path = uri.AbsolutePath.Trim('/');
        string kindValue;
        string id;

        if (!string.IsNullOrWhiteSpace(host))
        {
            kindValue = host;
            id = Uri.UnescapeDataString(path);
        }
        else
        {
            var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length != 2)
            {
                error = "Storage reference must be of the form storage://{kind}/{id}.";
                return false;
            }

            kindValue = segments[0];
            id = Uri.UnescapeDataString(segments[1]);
        }

        if (!Enum.TryParse<StorageKind>(kindValue, true, out var kind))
        {
            error = $"Unsupported storage kind '{kindValue}'.";
            return false;
        }
        if (!IsValidId(id))
        {
            error = "Storage id contains invalid characters.";
            return false;
        }

        string? version = null;
        string? hash = null;
        if (!string.IsNullOrWhiteSpace(uri.Query))
        {
            var query = uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries);
            foreach (var entry in query)
            {
                var parts = entry.Split('=', 2);
                if (parts.Length != 2)
                {
                    continue;
                }

                var key = parts[0].Trim().ToLowerInvariant();
                var valuePart = Uri.UnescapeDataString(parts[1]);
                if (key == "v")
                {
                    version = valuePart;
                }
                else if (key == "h")
                {
                    hash = valuePart;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(hash) && !StorageHash.IsValid(hash))
        {
            error = "Storage hash must be a lowercase sha256 hex value.";
            return false;
        }

        reference = new StorageRef
        {
            Kind = kind,
            Id = id,
            Version = version,
            Hash = hash
        };
        return true;
    }

    public static bool IsValidId(string id)
        => !string.IsNullOrWhiteSpace(id)
           && id.Length < 128
           && id.All(ch => char.IsLetterOrDigit(ch) || ch is '_' or '-' or '.');
}

public static class StorageHash
{
    public static string ComputeSha256(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        var bytes = Encoding.UTF8.GetBytes(content);
        return ComputeSha256(bytes);
    }

    public static string ComputeSha256(ReadOnlySpan<byte> content)
    {
        Span<byte> hash = stackalloc byte[32];
        if (!SHA256.TryHashData(content, hash, out _))
        {
            throw new InvalidOperationException("Failed to compute SHA256 hash.");
        }

        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static bool IsValid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length != 64)
        {
            return false;
        }

        foreach (var ch in value)
        {
            if (!Uri.IsHexDigit(ch) || (char.IsLetter(ch) && !char.IsLower(ch)))
            {
                return false;
            }
        }

        return true;
    }
}
