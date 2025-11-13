using System.Text;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using YamlDotNet.Serialization.TypeInspectors;

namespace FlowTime.Sim.Core;

/// <summary>
/// Utilities for reading and writing Catalog.v1 YAML files.
/// </summary>
public static class CatalogIO
{
    private static readonly ISerializer YamlSerializer = new SerializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitDefaults)
        .WithTypeInspector(inner => new ReadablePropertiesTypeInspector(inner))
        .Build();

    private static readonly IDeserializer YamlDeserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    /// <summary>
    /// Reads a catalog from a YAML file.
    /// </summary>
    public static async Task<Catalog> ReadCatalogAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Catalog file not found: {filePath}");
        }

        var yaml = await File.ReadAllTextAsync(filePath, Encoding.UTF8);
        return ReadCatalogFromYaml(yaml);
    }

    /// <summary>
    /// Reads a catalog from a YAML file (synchronous).
    /// </summary>
    public static Catalog ReadCatalogFromFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Catalog file not found: {filePath}");
        }

        var yaml = File.ReadAllText(filePath, Encoding.UTF8);
        return ReadCatalogFromYaml(yaml);
    }

    /// <summary>
    /// Reads a catalog from YAML text.
    /// </summary>
    public static Catalog ReadCatalogFromYaml(string yaml)
    {
        try
        {
            var catalog = YamlDeserializer.Deserialize<Catalog>(yaml);
            
            // Validate the catalog structure
            var validation = catalog.Validate();
            if (!validation.IsValid)
            {
                var errors = string.Join(", ", validation.Errors);
                throw new InvalidOperationException($"Invalid catalog: {errors}");
            }

            return catalog;
        }
        catch (Exception ex) when (!(ex is InvalidOperationException))
        {
            throw new InvalidOperationException($"Failed to parse catalog YAML: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Parses a catalog from YAML text without validation.
    /// Use this when you want to validate separately.
    /// </summary>
    public static Catalog ParseCatalogFromYaml(string yaml)
    {
        try
        {
            return YamlDeserializer.Deserialize<Catalog>(yaml);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to parse catalog YAML: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Writes a catalog to a YAML file.
    /// </summary>
    public static async Task WriteCatalogAsync(string filePath, Catalog catalog)
    {
        // Validate before writing
        var validation = catalog.Validate();
        if (!validation.IsValid)
        {
            var errors = string.Join(", ", validation.Errors);
            throw new InvalidOperationException($"Cannot write invalid catalog: {errors}");
        }

        var yaml = WriteCatalogToYaml(catalog);
        
        // Ensure directory exists
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(filePath, yaml, Encoding.UTF8);
    }

    /// <summary>
    /// Converts a catalog to YAML text.
    /// </summary>
    public static string WriteCatalogToYaml(Catalog catalog)
    {
        return YamlSerializer.Serialize(catalog);
    }

    /// <summary>
    /// Computes a deterministic hash for a catalog (for caching and change detection).
    /// </summary>
    public static string ComputeCatalogHash(Catalog catalog)
    {
        // Normalize catalog to YAML for consistent hashing
        var yaml = WriteCatalogToYaml(catalog);
        var bytes = Encoding.UTF8.GetBytes(yaml);
        
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hash = sha256.ComputeHash(bytes);
        return $"sha256:{Convert.ToHexString(hash).ToLowerInvariant()}";
    }
}

/// <summary>
/// Custom type inspector that excludes read-only properties from serialization.
/// </summary>
internal sealed class ReadablePropertiesTypeInspector : TypeInspectorSkeleton
{
    private readonly ITypeInspector innerTypeInspector;

    public ReadablePropertiesTypeInspector(ITypeInspector innerTypeInspector)
    {
        this.innerTypeInspector = innerTypeInspector;
    }

    public override IEnumerable<IPropertyDescriptor> GetProperties(Type type, object? container)
    {
        var properties = innerTypeInspector.GetProperties(type, container);
        
        // Filter out read-only properties that end with "ReadOnly"
        return properties.Where(p => !p.Name.EndsWith("ReadOnly", StringComparison.OrdinalIgnoreCase));
    }

    public override string GetEnumName(Type enumType, string value)
    {
        return innerTypeInspector.GetEnumName(enumType, value);
    }

    public override string GetEnumValue(object value)
    {
        return innerTypeInspector.GetEnumValue(value);
    }
}
