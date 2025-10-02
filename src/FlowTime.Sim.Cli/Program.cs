using System.Globalization;
using System.Text;
using System.Text.Json;
using FlowTime.Sim.Core.Services;
using FlowTime.Sim.Core.Templates;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using YamlDotNet.Serialization;

namespace FlowTime.Sim.Cli
{
    public sealed record CliOptions(
        string Verb,        // list | show | generate | validate
        string? Noun,       // templates | models | template | model
        string? TemplateId,
        string? ParamsPath,
        string? OutputPath,
        string Format, // yaml | json
        bool Verbose,
        string? TemplatesDir,
        string? ModelsDir,
        string? ProvenancePath,  // SIM-M2.7: Path to save provenance JSON
        bool EmbedProvenance     // SIM-M2.7: Embed provenance in model YAML
    )
    {
        public static CliOptions Defaults => new(
            "",
            null,
            null,
            null,
            null,
            "yaml",
            false,
            null,
            null,
            null,  // ProvenancePath
            false); // EmbedProvenance
    }

    internal static class Program
    {
        internal static async Task<int> Main(string[] args)
        {
            try
            {
                CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
                CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

                // Load configuration from files and environment
                var config = CliConfig.Load();
                
                // Debug: Show loaded config
                if (args.Contains("--debug-config"))
                {
                    Console.WriteLine($"Loaded config:");
                    Console.WriteLine($"  Templates: {config.TemplatesDirectory}");
                    Console.WriteLine($"  Models: {config.ModelsDirectory}");
                    Console.WriteLine($"  Format: {config.DefaultFormat}");
                    return 0;
                }
                
                var opts = ArgParser.ParseArgs(args);
                
                if (string.IsNullOrWhiteSpace(opts.Verb))
                {
                    PrintHelp();
                    return 0;
                }

                // Apply config defaults if not specified on command line
                if (string.IsNullOrWhiteSpace(opts.Format))
                {
                    opts = opts with { Format = config.DefaultFormat };
                }
                if (!opts.Verbose)
                {
                    opts = opts with { Verbose = config.Verbose };
                }

                // Handle init command early (doesn't need templates directory)
                if (opts.Verb.ToLowerInvariant() == "init")
                {
                    return ExecuteInitCommand(opts);
                }

                // Validate format early
                if (!opts.Format.Equals("yaml", StringComparison.OrdinalIgnoreCase) &&
                    !opts.Format.Equals("json", StringComparison.OrdinalIgnoreCase))
                {
                    Console.Error.WriteLine($"Unsupported --format '{opts.Format}'. Expected 'yaml' or 'json'.");
                    return 2;
                }

                var cts = new CancellationTokenSource();
                Console.CancelKeyPress += (s, e) => { e.Cancel = true; cts.Cancel(); };

                // Determine templates directory (CLI option > config > default)
                var templatesDir = opts.TemplatesDir ?? config.TemplatesDirectory;
                if (!Directory.Exists(templatesDir))
                {
                    Console.Error.WriteLine($"Templates directory not found: {templatesDir}");
                    Console.Error.WriteLine("Specify with --templates-dir <path> or ensure ./templates exists.");
                    return 2;
                }

                // Create NodeBasedTemplateService (use NullLogger for CLI simplicity)
                var logger = NullLogger<NodeBasedTemplateService>.Instance;
                var templateService = new NodeBasedTemplateService(templatesDir, logger);

                // Execute command using verb+noun routing
                var verb = opts.Verb.ToLowerInvariant();
                var noun = opts.Noun?.ToLowerInvariant() ?? "";

                return (verb, noun) switch
                {
                    // list templates (default for 'list' or explicit 'list templates')
                    ("list", "" or "templates") => await ExecuteListTemplatesCommand(templateService, opts, cts.Token),
                    
                    // list models
                    ("list", "models") => await ExecuteListModelsCommand(opts),
                    
                    // show template
                    ("show", "template") => await ExecuteShowTemplateCommand(templateService, opts, cts.Token),
                    
                    // show model
                    ("show", "model") => await Task.FromResult(ExecuteShowModelCommand(opts)),
                    
                    // generate (model from template)
                    ("generate", "" or "model") => await ExecuteGenerateCommand(templateService, opts, cts.Token),
                    
                    // validate (template parameters)
                    ("validate", "" or "template" or "params") => await ExecuteValidateCommand(templateService, opts, cts.Token),
                    
                    _ => HandleUnknownCommand(verb, noun)
                };
            }
            catch (OperationCanceledException)
            {
                Console.Error.WriteLine("Canceled.");
                return 130;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                return 1;
            }
        }

        static async Task<int> ExecuteListTemplatesCommand(INodeBasedTemplateService service, CliOptions opts, CancellationToken ct)
        {
            var templates = await service.GetAllTemplatesAsync();
            
            if (!templates.Any())
            {
                Console.WriteLine("No templates found.");
                return 0;
            }

            if (opts.Format.Equals("json", StringComparison.OrdinalIgnoreCase))
            {
                var json = JsonSerializer.Serialize(templates.Select(t => new {
                    t.Metadata.Id,
                    t.Metadata.Title,
                    t.Metadata.Description,
                    ParameterCount = t.Parameters.Count
                }), new JsonSerializerOptions { WriteIndented = true });
                Console.WriteLine(json);
            }
            else
            {
                Console.WriteLine($"\n{templates.Count} template(s) found:\n");
                foreach (var template in templates)
                {
                    Console.WriteLine($"ID: {template.Metadata.Id}");
                    Console.WriteLine($"Title: {template.Metadata.Title}");
                    if (!string.IsNullOrEmpty(template.Metadata.Description))
                        Console.WriteLine($"Description: {template.Metadata.Description}");
                    Console.WriteLine($"Parameters: {template.Parameters.Count}");
                    Console.WriteLine();
                }
            }
            return 0;
        }

        static async Task<int> ExecuteShowTemplateCommand(INodeBasedTemplateService service, CliOptions opts, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(opts.TemplateId))
            {
                Console.Error.WriteLine("Error: --id required for 'show template' command");
                return 2;
            }

            var template = await service.GetTemplateAsync(opts.TemplateId);
            if (template == null)
            {
                Console.Error.WriteLine($"Template not found: {opts.TemplateId}");
                return 1;
            }

            if (opts.Format.Equals("json", StringComparison.OrdinalIgnoreCase))
            {
                var json = JsonSerializer.Serialize(template, new JsonSerializerOptions { WriteIndented = true });
                Console.WriteLine(json);
            }
            else
            {
                Console.WriteLine($"\n=== Template: {template.Metadata.Id} ===");
                Console.WriteLine($"\nTitle: {template.Metadata.Title}");
                if (!string.IsNullOrEmpty(template.Metadata.Description))
                    Console.WriteLine($"Description: {template.Metadata.Description}\n");
                
                if (template.Parameters.Any())
                {
                    Console.WriteLine("Parameters:");
                    foreach (var param in template.Parameters)
                    {
                        Console.WriteLine($"  - {param.Name}: {param.Type}");
                        if (!string.IsNullOrEmpty(param.Description))
                            Console.WriteLine($"    Description: {param.Description}");
                        if (param.Default != null)
                            Console.WriteLine($"    Default: {param.Default}");
                    }
                    Console.WriteLine();
                }
            }
            return 0;
        }

        internal static async Task<int> ExecuteGenerateCommand(INodeBasedTemplateService service, CliOptions opts, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(opts.TemplateId))
            {
                Console.Error.WriteLine("Error: --id required for 'generate' command");
                return 2;
            }

            // SIM-M2.7: Validate mutual exclusivity of provenance options
            if (!string.IsNullOrWhiteSpace(opts.ProvenancePath) && opts.EmbedProvenance)
            {
                Console.Error.WriteLine("Error: --provenance and --embed-provenance are mutually exclusive");
                return 2;
            }

            // Load parameters from file if provided
            var parameters = new Dictionary<string, object>();
            if (!string.IsNullOrWhiteSpace(opts.ParamsPath))
            {
                if (!File.Exists(opts.ParamsPath))
                {
                    Console.Error.WriteLine($"Parameters file not found: {opts.ParamsPath}");
                    return 2;
                }
                var paramsJson = await File.ReadAllTextAsync(opts.ParamsPath, ct);
                var parsedParams = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(paramsJson);
                if (parsedParams != null)
                {
                    foreach (var kvp in parsedParams)
                    {
                        parameters[kvp.Key] = kvp.Value.ValueKind switch
                        {
                            JsonValueKind.Number => kvp.Value.GetDouble(),
                            JsonValueKind.String => kvp.Value.GetString() ?? "",
                            JsonValueKind.True => true,
                            JsonValueKind.False => false,
                            _ => kvp.Value.ToString() ?? ""
                        };
                    }
                }
            }

            // Generate model
            var model = await service.GenerateEngineModelAsync(opts.TemplateId, parameters);
            
            // SIM-M2.7: Generate provenance if requested
            Core.Models.ProvenanceMetadata? provenance = null;
            if (!string.IsNullOrWhiteSpace(opts.ProvenancePath) || opts.EmbedProvenance)
            {
                // Get template metadata for provenance
                var template = await service.GetTemplateAsync(opts.TemplateId);
                if (template == null)
                {
                    Console.Error.WriteLine($"Error: Template '{opts.TemplateId}' not found");
                    return 2;
                }

                // Create provenance service and generate metadata
                var provenanceService = new Core.Services.ProvenanceService();
                provenance = provenanceService.CreateProvenance(
                    template.Metadata.Id,
                    "1.0", // Template version (hardcoded for M2.7)
                    template.Metadata.Title,
                    parameters);

                // If embed mode, embed provenance in model YAML
                if (opts.EmbedProvenance)
                {
                    model = Core.Services.ProvenanceEmbedder.EmbedProvenance(model, provenance);
                }
            }
            
            // Write model output
            if (string.IsNullOrWhiteSpace(opts.OutputPath))
            {
                Console.WriteLine(model);
            }
            else
            {
                var outputPath = Path.GetFullPath(opts.OutputPath);
                var dir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);
                    
                await File.WriteAllTextAsync(outputPath, model, Encoding.UTF8, ct);
                
                if (opts.Verbose)
                    Console.WriteLine($"Model written to: {outputPath}");
            }

            // SIM-M2.7: Write separate provenance file if requested
            if (!string.IsNullOrWhiteSpace(opts.ProvenancePath) && provenance != null)
            {
                var provenancePath = Path.GetFullPath(opts.ProvenancePath);
                var dir = Path.GetDirectoryName(provenancePath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                var provenanceJson = JsonSerializer.Serialize(provenance, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = true
                });
                
                await File.WriteAllTextAsync(provenancePath, provenanceJson, Encoding.UTF8, ct);
                
                if (opts.Verbose)
                    Console.WriteLine($"Provenance written to: {provenancePath}");
            }

            return 0;
        }

        static async Task<int> ExecuteValidateCommand(INodeBasedTemplateService service, CliOptions opts, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(opts.TemplateId))
            {
                Console.Error.WriteLine("Error: --id required for 'validate' command");
                return 2;
            }

            // Load parameters from file if provided
            var parameters = new Dictionary<string, object>();
            if (!string.IsNullOrWhiteSpace(opts.ParamsPath))
            {
                if (!File.Exists(opts.ParamsPath))
                {
                    Console.Error.WriteLine($"Parameters file not found: {opts.ParamsPath}");
                    return 2;
                }
                var paramsJson = await File.ReadAllTextAsync(opts.ParamsPath, ct);
                var parsedParams = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(paramsJson);
                if (parsedParams != null)
                {
                    foreach (var kvp in parsedParams)
                    {
                        parameters[kvp.Key] = kvp.Value.ValueKind switch
                        {
                            JsonValueKind.Number => kvp.Value.GetDouble(),
                            JsonValueKind.String => kvp.Value.GetString() ?? "",
                            JsonValueKind.True => true,
                            JsonValueKind.False => false,
                            _ => kvp.Value.ToString() ?? ""
                        };
                    }
                }
            }

            var result = await service.ValidateParametersAsync(opts.TemplateId, parameters);
            
            if (result.IsValid)
            {
                Console.WriteLine("✓ Parameters valid");
                return 0;
            }
            else
            {
                Console.Error.WriteLine("✗ Validation failed:");
                foreach (var error in result.Errors)
                {
                    Console.Error.WriteLine($"  - {error}");
                }
                return 1;
            }
        }

        static int HandleUnknownCommand(string verb, string noun)
        {
            if (string.IsNullOrEmpty(noun))
                Console.Error.WriteLine($"Unknown command: {verb}");
            else
                Console.Error.WriteLine($"Unknown command: {verb} {noun}");
            PrintHelp();
            return 2;
        }

    static int ExecuteInitCommand(CliOptions opts)
    {
        var configPath = Path.Combine(Directory.GetCurrentDirectory(), ".flow-sim.yaml");
        
        if (File.Exists(configPath))
        {
            Console.WriteLine($"Configuration file already exists: {configPath}");
            Console.WriteLine("Remove it first if you want to recreate it.");
            return 1;
        }

        // Determine paths (use provided options or smart defaults)
        var templatesDir = opts.TemplatesDir ?? Path.Combine(Directory.GetCurrentDirectory(), "templates");
        var modelsDir = opts.ModelsDir ?? Path.Combine(Directory.GetCurrentDirectory(), "data/models");
        var defaultFormat = opts.Format;
        var defaultVerbose = opts.Verbose;

        // Create config content
        var configContent = new StringBuilder();
        configContent.AppendLine("# FlowTime-Sim CLI Configuration");
        configContent.AppendLine();
        configContent.AppendLine("templates:");
        configContent.AppendLine($"  directory: {templatesDir}");
        configContent.AppendLine();
        configContent.AppendLine("data:");
        configContent.AppendLine($"  models: {modelsDir}");
        configContent.AppendLine();
        configContent.AppendLine("defaults:");
        configContent.AppendLine($"  format: {defaultFormat}");
        configContent.AppendLine($"  verbose: {defaultVerbose.ToString().ToLowerInvariant()}");

        // Write config file
        File.WriteAllText(configPath, configContent.ToString());

        // Show what was configured
        Console.WriteLine($"Created configuration file: {configPath}\n");
        Console.WriteLine("Configured values:");
        Console.WriteLine($"  Templates directory: {templatesDir}");
        Console.WriteLine($"  Models directory:    {modelsDir}");
        Console.WriteLine($"  Default format:      {defaultFormat}");
        Console.WriteLine($"  Default verbose:     {defaultVerbose}");
        Console.WriteLine();
        
        // Check if directories exist
        var warnings = new List<string>();
        if (!Directory.Exists(templatesDir))
            warnings.Add($"⚠️  Templates directory does not exist: {templatesDir}");
        if (!Directory.Exists(modelsDir))
            warnings.Add($"⚠️  Models directory does not exist: {modelsDir}");

        if (warnings.Count > 0)
        {
            Console.WriteLine("Warnings:");
            foreach (var warning in warnings)
                Console.WriteLine($"  {warning}");
            Console.WriteLine();
            Console.WriteLine("Directories will be created automatically when needed.");
        }
        else
        {
            Console.WriteLine("✓ All directories exist.");
        }

        return 0;
    }

    static int ExecuteShowModelCommand(CliOptions opts)
    {
        if (string.IsNullOrWhiteSpace(opts.TemplateId))
        {
            Console.Error.WriteLine("Error: --id is required for 'show model'");
            Console.Error.WriteLine("Usage: flow-sim show model --id <model-name>");
            return 1;
        }

        var config = CliConfig.Load();
        var modelsDir = opts.ModelsDir ?? config.ModelsDirectory ?? "./data/models";
        var modelDir = Path.Combine(modelsDir, opts.TemplateId);

        if (!Directory.Exists(modelDir))
        {
            Console.Error.WriteLine($"Model not found: {opts.TemplateId}");
            Console.Error.WriteLine($"Looked in: {modelsDir}");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Use 'flow-sim list models' to see available models.");
            return 1;
        }

        var metadataPath = Path.Combine(modelDir, "metadata.json");
        var modelPath = Path.Combine(modelDir, "model.yaml");

        // Display model information
        Console.WriteLine($"Model: {opts.TemplateId}");
        Console.WriteLine($"Location: {modelDir}\n");

        // Read and display metadata
        if (File.Exists(metadataPath))
        {
            try
            {
                var metadataJson = File.ReadAllText(metadataPath);
                var metadata = JsonSerializer.Deserialize<JsonElement>(metadataJson);

                Console.WriteLine("Metadata:");
                if (metadata.TryGetProperty("templateId", out var templateId))
                    Console.WriteLine($"  Template ID:      {templateId.GetString()}");
                if (metadata.TryGetProperty("generatedAtUtc", out var generatedAt))
                    Console.WriteLine($"  Generated:        {generatedAt.GetString()}");
                if (metadata.TryGetProperty("modelHash", out var hash))
                    Console.WriteLine($"  Model Hash:       {hash.GetString()}");
                if (metadata.TryGetProperty("parameters", out var parameters))
                {
                    var paramsJson = JsonSerializer.Serialize(parameters, new JsonSerializerOptions { WriteIndented = true });
                    if (paramsJson != "{}")
                    {
                        Console.WriteLine($"  Parameters:       {paramsJson}");
                    }
                    else
                    {
                        Console.WriteLine("  Parameters:       (none - using template defaults)");
                    }
                }
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️  Could not read metadata: {ex.Message}\n");
            }
        }

        // Display model preview
        if (File.Exists(modelPath))
        {
            Console.WriteLine("Model Preview:");
            var lines = File.ReadAllLines(modelPath);
            var previewLines = Math.Min(20, lines.Length);
            for (int i = 0; i < previewLines; i++)
            {
                Console.WriteLine($"  {lines[i]}");
            }
            if (lines.Length > previewLines)
            {
                Console.WriteLine($"  ... ({lines.Length - previewLines} more lines)");
            }
            Console.WriteLine();
            Console.WriteLine($"Full model: {modelPath}");
        }
        else
        {
            Console.WriteLine("⚠️  Model file not found: model.yaml");
        }

        return 0;
    }

    static Task<int> ExecuteListModelsCommand(CliOptions opts)
    {
        var config = CliConfig.Load();
        var modelsDir = opts.ModelsDir ?? config.ModelsDirectory ?? "./data/models";

        if (!Directory.Exists(modelsDir))
        {
            Console.WriteLine($"Models directory not found: {modelsDir}");
            Console.WriteLine("No models have been generated yet.");
            Console.WriteLine("Generate a model using: flow-sim generate --id <template-id> --out <file>");
            return Task.FromResult(1);
        }

        var modelDirs = Directory.GetDirectories(modelsDir);
        if (modelDirs.Length == 0)
        {
            Console.WriteLine("No models generated yet.");
            Console.WriteLine("Generate a model using: flow-sim generate --id <template-id> --out <file>");
            return Task.FromResult(0);
        }

        Console.WriteLine($"Available models in {modelsDir}:\n");
        foreach (var dir in modelDirs)
        {
            var modelName = Path.GetFileName(dir);
            Console.WriteLine($"  {modelName}");
        }

        return Task.FromResult(0);
    }

    public static void PrintHelp()
        {
            Console.WriteLine("FlowTime-Sim CLI - Charter-compliant model authoring tool\n");
            Console.WriteLine("Usage: flow-sim <verb> <noun> [options]\n");
            Console.WriteLine("Commands:");
            Console.WriteLine("  init                     Create .flow-sim.yaml configuration file");
            Console.WriteLine("  list templates           List all available templates");
            Console.WriteLine("  list models              List all generated models");
            Console.WriteLine("  show template --id <id>  Show template details including parameters");
            Console.WriteLine("  show model --id <name>   Show generated model details and preview");
            Console.WriteLine("  generate [model] --id <template-id> [--params <file>] [--out <file>]");
            Console.WriteLine("                           Generate Engine model from template");
            Console.WriteLine("  validate [template] --id <template-id> [--params <file>]");
            Console.WriteLine("                           Validate template parameters\n");
            Console.WriteLine("Options:");
            Console.WriteLine("  --id <id>                Template identifier");
            Console.WriteLine("  --params <file>          JSON file with parameter overrides (optional)");
            Console.WriteLine("                           Templates have defaults; use this to override");
            Console.WriteLine("  --out <file>             Output file (default: stdout)");
            Console.WriteLine("  --format yaml|json       Output format (default: yaml)");
            Console.WriteLine("  --templates-dir <path>   Templates directory (default: ./templates)");
            Console.WriteLine("  --models-dir <path>      Models directory (default: ./data/models)");
            Console.WriteLine("  --verbose, -v            Verbose output");
            Console.WriteLine("  --help, -h               Show this help\n");
            Console.WriteLine("Examples:");
            Console.WriteLine("  flow-sim list templates                    # List all templates");
            Console.WriteLine("  flow-sim list models                       # List generated models");
            Console.WriteLine("  flow-sim show template --id transportation-basic");
            Console.WriteLine("                                             # Show template with defaults");
            Console.WriteLine("  flow-sim generate --id transportation-basic --out model.yaml");
            Console.WriteLine("                                             # Generate with defaults");
            Console.WriteLine("  flow-sim generate --id transportation-basic --params overrides.json");
            Console.WriteLine("                                             # Override specific params");
        }
    }

    public static class ProgramWrapper
    {
        public static Task<int> InvokeMain(string[] args) => Program.Main(args);
        
        // SIM-M2.7: Expose ExecuteGenerateCommand for testing
        public static Task<int> ExecuteGenerate(INodeBasedTemplateService service, CliOptions opts, CancellationToken ct = default)
            => Program.ExecuteGenerateCommand(service, opts, ct);
    }

    public static class ArgParser
    {
        public static CliOptions ParseArgs(string[] args)
        {
            var opts = CliOptions.Defaults;

            // First positional argument is the verb
            if (args.Length > 0 && !args[0].StartsWith("--") && !args[0].StartsWith("-"))
            {
                opts = opts with { Verb = args[0] };
                
                // Second positional argument is the noun (optional)
                if (args.Length > 1 && !args[1].StartsWith("--") && !args[1].StartsWith("-"))
                {
                    opts = opts with { Noun = args[1] };
                }
            }
            
            for (int i = 0; i < args.Length; i++)
            {
                var a = args[i];
                if (a is "--id") opts = opts with { TemplateId = ArgValue(args, ref i) };
                else if (a is "--params") opts = opts with { ParamsPath = ArgValue(args, ref i) };
                else if (a is "--out" or "-o") opts = opts with { OutputPath = ArgValue(args, ref i) };
                else if (a is "--format") opts = opts with { Format = ArgValue(args, ref i) };
                else if (a is "--templates-dir") opts = opts with { TemplatesDir = ArgValue(args, ref i) };
                else if (a is "--models-dir") opts = opts with { ModelsDir = ArgValue(args, ref i) };
                else if (a is "--provenance") opts = opts with { ProvenancePath = ArgValue(args, ref i) };  // SIM-M2.7
                else if (a is "--embed-provenance") opts = opts with { EmbedProvenance = true };  // SIM-M2.7
                else if (a is "--verbose" or "-v") opts = opts with { Verbose = true };
                else if (a is "--help" or "-h")
                {
                    Program.PrintHelp();
                    Environment.Exit(0);
                }
            }
            return opts;
        }

        private static string ArgValue(string[] args, ref int i)
        {
            if (i + 1 >= args.Length) throw new ArgumentException($"Missing value for {args[i]}");
            return args[++i];
        }
    }
}
