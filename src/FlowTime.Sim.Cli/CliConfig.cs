namespace FlowTime.Sim.Cli;

/// <summary>
/// Configuration for FlowTime-Sim CLI loaded from files and environment variables.
/// Priority order (highest to lowest):
/// 1. Command-line flags
/// 2. Environment variables
/// 3. Project-local config (./.flow-sim.yaml)
/// 4. User config (~/.flow-sim.yaml)
/// 5. Built-in defaults
/// </summary>
public class CliConfig
{
    public string TemplatesDirectory { get; set; } = "./templates";
    public string ModelsDirectory { get; set; } = "./data/models";
    public string DefaultFormat { get; set; } = "yaml";
    public bool Verbose { get; set; } = false;
    
    /// <summary>
    /// Load configuration from all sources with proper priority.
    /// </summary>
    public static CliConfig Load(bool verbose = false)
    {
        var config = new CliConfig();
        
        // Start with built-in defaults (already set in properties)
        
        // 1. Try user config (~/.flow-sim.yaml)
        var userConfigPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".flow-sim.yaml"
        );
        if (File.Exists(userConfigPath))
        {
            if (verbose) Console.WriteLine($"Loading config from: {userConfigPath}");
            LoadFromFile(userConfigPath, config);
        }
        
        // 2. Try project-local config (./.flow-sim.yaml) - search up directory tree
        var localConfigPath = FindConfigInParentDirectories();
        if (localConfigPath != null)
        {
            if (verbose) Console.WriteLine($"Loading config from: {localConfigPath}");
            LoadFromFile(localConfigPath, config);
        }
        else if (verbose)
        {
            Console.WriteLine($"No .flow-sim.yaml found in current directory or parents (searched from: {Directory.GetCurrentDirectory()})");
        }
        
        // 3. Environment variables override file configs
        var envTemplates = Environment.GetEnvironmentVariable("FLOW_SIM_TEMPLATES_DIR");
        if (!string.IsNullOrWhiteSpace(envTemplates))
        {
            config.TemplatesDirectory = envTemplates;
        }
        
        var envModels = Environment.GetEnvironmentVariable("FLOW_SIM_DATA_DIR");
        if (!string.IsNullOrWhiteSpace(envModels))
        {
            config.ModelsDirectory = envModels;
        }
        
        var envFormat = Environment.GetEnvironmentVariable("FLOW_SIM_FORMAT");
        if (!string.IsNullOrWhiteSpace(envFormat))
        {
            config.DefaultFormat = envFormat;
        }
        
        var envVerbose = Environment.GetEnvironmentVariable("FLOW_SIM_VERBOSE");
        if (envVerbose == "1" || envVerbose?.ToLowerInvariant() == "true")
        {
            config.Verbose = true;
        }
        
        return config;
    }
    
    /// <summary>
    /// Search for .flow-sim.yaml in current directory and all parent directories.
    /// </summary>
    private static string? FindConfigInParentDirectories()
    {
        var currentDir = new DirectoryInfo(Directory.GetCurrentDirectory());
        
        while (currentDir != null)
        {
            var configPath = Path.Combine(currentDir.FullName, ".flow-sim.yaml");
            if (File.Exists(configPath))
            {
                return configPath;
            }
            
            currentDir = currentDir.Parent;
        }
        
        return null;
    }
    
    private static void LoadFromFile(string path, CliConfig config)
    {
        try
        {
            var yaml = File.ReadAllText(path);
            var deserializer = new YamlDotNet.Serialization.DeserializerBuilder()
                .IgnoreUnmatchedProperties()
                .Build();
            
            var fileConfig = deserializer.Deserialize<CliConfigFile>(yaml);
            
            if (fileConfig?.Templates?.Directory != null)
            {
                config.TemplatesDirectory = fileConfig.Templates.Directory;
            }
            
            if (fileConfig?.Data?.Models != null)
            {
                config.ModelsDirectory = fileConfig.Data.Models;
            }
            
            if (fileConfig?.Defaults?.Format != null)
            {
                config.DefaultFormat = fileConfig.Defaults.Format;
            }
            
            if (fileConfig?.Defaults?.Verbose != null)
            {
                config.Verbose = fileConfig.Defaults.Verbose.Value;
            }
        }
        catch (Exception ex)
        {
            // Warn but don't fail - fall back to defaults
            Console.Error.WriteLine($"Warning: Failed to load config from {path}: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Internal structure for YAML deserialization.
    /// Must match the YAML structure exactly (case-sensitive by default).
    /// </summary>
    private class CliConfigFile
    {
        [YamlDotNet.Serialization.YamlMember(Alias = "templates")]
        public TemplatesConfig? Templates { get; set; }
        
        [YamlDotNet.Serialization.YamlMember(Alias = "data")]
        public DataConfig? Data { get; set; }
        
        [YamlDotNet.Serialization.YamlMember(Alias = "defaults")]
        public DefaultsConfig? Defaults { get; set; }
    }
    
    private class TemplatesConfig
    {
        [YamlDotNet.Serialization.YamlMember(Alias = "directory")]
        public string? Directory { get; set; }
    }
    
    private class DataConfig
    {
        [YamlDotNet.Serialization.YamlMember(Alias = "models")]
        public string? Models { get; set; }
        
        [YamlDotNet.Serialization.YamlMember(Alias = "catalogs")]
        public string? Catalogs { get; set; }
    }
    
    private class DefaultsConfig
    {
        [YamlDotNet.Serialization.YamlMember(Alias = "format")]
        public string? Format { get; set; }
        
        [YamlDotNet.Serialization.YamlMember(Alias = "verbose")]
        public bool? Verbose { get; set; }
    }
}
