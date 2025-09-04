using System.Text;
using System.Text.Json;

namespace FlowTime.UI.Services;

public class TemplateService : ITemplateService
{
    private readonly IFlowTimeSimApiClient simClient;
    private readonly ILogger<TemplateService> logger;

    public TemplateService(IFlowTimeSimApiClient simClient, ILogger<TemplateService> logger)
    {
        this.simClient = simClient;
        this.logger = logger;
    }

    public async Task<List<TemplateInfo>> GetTemplatesAsync()
    {
        try
        {
            // Get scenarios from FlowTime-Sim API and convert to templates
            var scenariosResult = await simClient.GetScenariosAsync();
            if (!scenariosResult.Success)
            {
                logger.LogWarning("Failed to get scenarios from Sim API: {Error}. Falling back to mock templates.", scenariosResult.Error);
                return GetMockTemplates();
            }

            var scenarios = scenariosResult.Value ?? new List<ScenarioInfo>();
            return scenarios.Select(ConvertScenarioToTemplate).ToList();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get templates from Sim API. Falling back to mock data.");
            return GetMockTemplates();
        }
    }

    public async Task<TemplateInfo?> GetTemplateAsync(string templateId)
    {
        try
        {
            var templates = await GetTemplatesAsync();
            return templates.FirstOrDefault(t => t.Id == templateId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get template {TemplateId}", templateId);
            throw;
        }
    }

    public async Task<JsonSchema> GetTemplateSchemaAsync(string templateId)
    {
        try
        {
            var template = await GetTemplateAsync(templateId);
            return template?.ParameterSchema ?? new JsonSchema();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get template schema {TemplateId}", templateId);
            throw;
        }
    }

    private static TemplateInfo ConvertScenarioToTemplate(ScenarioInfo scenario)
    {
        return new TemplateInfo
        {
            Id = scenario.Id,
            Name = scenario.Name,
            Description = scenario.Description,
            Category = scenario.Category,
            Tags = scenario.Tags,
            ParameterSchema = CreateParameterSchemaFromDefaults(scenario.DefaultParameters)
        };
    }

    private static JsonSchema CreateParameterSchemaFromDefaults(Dictionary<string, object> defaultParameters)
    {
        var schema = new JsonSchema
        {
            Title = "Simulation Parameters",
            Properties = new Dictionary<string, JsonSchemaProperty>(),
            Required = new List<string>()
        };

        foreach (var (key, value) in defaultParameters)
        {
            var property = new JsonSchemaProperty
            {
                Title = FormatPropertyTitle(key),
                Default = value
            };

            // Infer type from default value
            switch (value)
            {
                case int _:
                    property.Type = "integer";
                    break;
                case double _:
                case float _:
                    property.Type = "number";
                    break;
                case bool _:
                    property.Type = "boolean";
                    break;
                case string _:
                    property.Type = "string";
                    break;
                default:
                    property.Type = "string";
                    break;
            }

            schema.Properties[key] = property;
            schema.Required.Add(key);
        }

        return schema;
    }

    private static string FormatPropertyTitle(string key)
    {
        // Convert camelCase/snake_case to Title Case
        return System.Text.RegularExpressions.Regex.Replace(key, @"([A-Z])|_(.)", m => 
            (m.Groups[1].Success ? " " + m.Groups[1].Value : " " + m.Groups[2].Value.ToUpper())).Trim();
    }

    private static List<TemplateInfo> GetMockTemplates()
    {
        return new List<TemplateInfo>
        {
            new()
            {
                Id = "transportation-basic",
                Name = "Basic Transportation Network",
                Description = "Simple transportation flow with demand and capacity constraints",
                Category = "Transportation",
                Tags = new() { "beginner", "network", "capacity" },
                ParameterSchema = new JsonSchema
                {
                    Title = "Transportation Parameters",
                    Properties = new()
                    {
                        ["demandRate"] = new JsonSchemaProperty
                        {
                            Type = "number",
                            Title = "Demand Rate",
                            Description = "Average demand per time unit",
                            Default = 10.0,
                            Minimum = 1.0,
                            Maximum = 100.0
                        },
                        ["capacity"] = new JsonSchemaProperty
                        {
                            Type = "number",
                            Title = "System Capacity",
                            Description = "Maximum throughput capacity",
                            Default = 15.0,
                            Minimum = 1.0,
                            Maximum = 200.0
                        },
                        ["simulationHours"] = new JsonSchemaProperty
                        {
                            Type = "integer",
                            Title = "Simulation Duration (hours)",
                            Description = "How long to run the simulation",
                            Default = 24,
                            Minimum = 1,
                            Maximum = 168
                        }
                    },
                    Required = new() { "demandRate", "capacity" }
                }
            },
            new()
            {
                Id = "supply-chain-multi-tier",
                Name = "Multi-Tier Supply Chain",
                Description = "Complex supply chain with multiple suppliers and distribution centers",
                Category = "Supply-Chain",
                Tags = new() { "advanced", "multi-tier", "inventory" },
                ParameterSchema = new JsonSchema
                {
                    Title = "Supply Chain Parameters",
                    Properties = new()
                    {
                        ["supplierCount"] = new JsonSchemaProperty
                        {
                            Type = "integer",
                            Title = "Number of Suppliers",
                            Description = "How many suppliers in the network",
                            Default = 3,
                            Minimum = 1,
                            Maximum = 10
                        },
                        ["leadTime"] = new JsonSchemaProperty
                        {
                            Type = "number",
                            Title = "Lead Time (days)",
                            Description = "Average lead time for delivery",
                            Default = 5.0,
                            Minimum = 0.5,
                            Maximum = 30.0
                        },
                        ["demandPattern"] = new JsonSchemaProperty
                        {
                            Type = "string",
                            Title = "Demand Pattern",
                            Description = "Type of demand variation",
                            Default = "steady",
                            Enum = new List<object> { "steady", "seasonal", "random", "burst" }
                        }
                    },
                    Required = new() { "supplierCount", "leadTime", "demandPattern" }
                }
            },
            new()
            {
                Id = "manufacturing-line",
                Name = "Manufacturing Production Line",
                Description = "Production line with workstations and quality control",
                Category = "Manufacturing",
                Tags = new() { "intermediate", "production", "quality" },
                ParameterSchema = new JsonSchema
                {
                    Title = "Manufacturing Parameters",
                    Properties = new()
                    {
                        ["stationCount"] = new JsonSchemaProperty
                        {
                            Type = "integer",
                            Title = "Number of Workstations",
                            Description = "Workstations in the production line",
                            Default = 5,
                            Minimum = 2,
                            Maximum = 20
                        },
                        ["cycleTime"] = new JsonSchemaProperty
                        {
                            Type = "number",
                            Title = "Cycle Time (minutes)",
                            Description = "Time per unit at each station",
                            Default = 2.5,
                            Minimum = 0.5,
                            Maximum = 60.0
                        },
                        ["qualityRate"] = new JsonSchemaProperty
                        {
                            Type = "number",
                            Title = "Quality Pass Rate",
                            Description = "Percentage of units passing quality checks",
                            Default = 0.95,
                            Minimum = 0.5,
                            Maximum = 1.0
                        }
                    },
                    Required = new() { "stationCount", "cycleTime", "qualityRate" }
                }
            }
        };
    }
}

public class CatalogService : ICatalogService
{
    private readonly IFlowTimeSimApiClient simClient;
    private readonly ILogger<CatalogService> logger;

    public CatalogService(IFlowTimeSimApiClient simClient, ILogger<CatalogService> logger)
    {
        this.simClient = simClient;
        this.logger = logger;
    }

    public Task<List<CatalogInfo>> GetCatalogsAsync()
    {
        try
        {
            // For now, return mock catalogs as catalog support is planned for SIM-CAT-M2
            // This will be updated when catalog API endpoints are available
            logger.LogInformation("Using mock catalogs (real catalog API planned for SIM-CAT-M2)");
            return Task.FromResult(GetMockCatalogs());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get catalogs");
            throw;
        }
    }

    public async Task<CatalogInfo?> GetCatalogAsync(string catalogId)
    {
        try
        {
            var catalogs = await GetCatalogsAsync();
            return catalogs.FirstOrDefault(c => c.Id == catalogId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get catalog {CatalogId}", catalogId);
            throw;
        }
    }

    private static List<CatalogInfo> GetMockCatalogs()
    {
        return new List<CatalogInfo>
        {
            new()
            {
                Id = "tiny-demo",
                Name = "Tiny Demo System",
                Description = "Minimal 3-node system for testing and demonstrations",
                Type = "Demo",
                NodeCount = 3,
                Capabilities = new() { "basic-flow", "simple-routing" }
            },
            new()
            {
                Id = "small-network",
                Name = "Small Network",
                Description = "Small-scale network with 10 nodes and hub topology",
                Type = "Network",
                NodeCount = 10,
                Capabilities = new() { "hub-spoke", "load-balancing", "failover" }
            },
            new()
            {
                Id = "enterprise-system",
                Name = "Enterprise System",
                Description = "Large enterprise system with 50+ nodes and complex routing",
                Type = "Enterprise",
                NodeCount = 55,
                Capabilities = new() { "multi-tier", "load-balancing", "auto-scaling", "monitoring" }
            }
        };
    }
}

public class FlowTimeSimService : IFlowTimeSimService
{
    private readonly IFlowTimeSimApiClient simClient;
    private readonly ILogger<FlowTimeSimService> logger;

    public FlowTimeSimService(IFlowTimeSimApiClient simClient, ILogger<FlowTimeSimService> logger)
    {
        this.simClient = simClient;
        this.logger = logger;
    }

    public async Task<SimulationRunResult> RunSimulationAsync(SimulationRunRequest request)
    {
        try
        {
            // Generate YAML spec from the request parameters
            var yamlSpec = GenerateSimulationYaml(request);
            
            // Call FlowTime-Sim API following artifact-first pattern
            var runResult = await simClient.RunAsync(yamlSpec);
            if (!runResult.Success)
            {
                logger.LogError("Simulation run failed: {Error}", runResult.Error);
                return new SimulationRunResult
                {
                    RunId = $"failed_{DateTime.UtcNow:yyyyMMddTHHmmssZ}",
                    Status = "failed",
                    StartTime = DateTime.UtcNow,
                    ErrorMessage = runResult.Error
                };
            }

            var runId = runResult.Value?.SimRunId ?? throw new InvalidOperationException("No run ID returned");
            
            // Return artifact-first result - no custom metadata blobs
            return new SimulationRunResult
            {
                RunId = runId,
                Status = "completed",
                StartTime = DateTime.UtcNow.AddSeconds(-1), // Approximate start time
                EndTime = DateTime.UtcNow,
                ResultsUrl = $"/sim/runs/{runId}/index", // Point to series index
                Metadata = new() // Minimal metadata, artifacts are authoritative
                {
                    ["templateId"] = request.TemplateId,
                    ["catalogId"] = request.CatalogId,
                    ["source"] = "sim"
                }
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to run simulation for template {TemplateId}", request.TemplateId);
            return new SimulationRunResult
            {
                RunId = $"error_{DateTime.UtcNow:yyyyMMddTHHmmssZ}",
                Status = "failed",
                StartTime = DateTime.UtcNow,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<SimulationStatus> GetRunStatusAsync(string runId)
    {
        try
        {
            // Try to get the series index to determine if run is complete
            var indexResult = await simClient.GetIndexAsync(runId);
            
            if (indexResult.Success)
            {
                return new SimulationStatus
                {
                    RunId = runId,
                    Status = "completed",
                    Progress = 100,
                    Message = "Simulation completed successfully"
                };
            }
            else if (indexResult.StatusCode == 404)
            {
                return new SimulationStatus
                {
                    RunId = runId,
                    Status = "not_found",
                    Progress = 0,
                    Message = "Run not found"
                };
            }
            else
            {
                return new SimulationStatus
                {
                    RunId = runId,
                    Status = "running",
                    Progress = 50,
                    Message = "Processing..."
                };
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get status for run {RunId}", runId);
            return new SimulationStatus
            {
                RunId = runId,
                Status = "error",
                Progress = 0,
                Message = ex.Message
            };
        }
    }

    private static string GenerateSimulationYaml(SimulationRunRequest request)
    {
        // Generate basic simulation spec YAML from request parameters
        // This follows the FlowTime-Sim spec format
        var yaml = new StringBuilder();
        
        yaml.AppendLine("# Generated simulation spec");
        yaml.AppendLine($"# Template: {request.TemplateId}");
        if (!string.IsNullOrEmpty(request.CatalogId))
        {
            yaml.AppendLine($"# Catalog: {request.CatalogId}");
        }
        yaml.AppendLine();
        
        // Basic grid configuration
        yaml.AppendLine("grid:");
        yaml.AppendLine("  bins: 288");
        yaml.AppendLine("  binMinutes: 5");
        yaml.AppendLine();
        
        // RNG configuration
        var seed = request.Parameters.TryGetValue("seed", out var seedValue) ? 
            Convert.ToInt32(seedValue) : Random.Shared.Next(1, 100000);
        yaml.AppendLine("rng:");
        yaml.AppendLine($"  seed: {seed}");
        yaml.AppendLine();
        
        // Components (basic single component for now)
        yaml.AppendLine("components:");
        yaml.AppendLine("  - COMP_A");
        yaml.AppendLine();
        
        // Measures
        yaml.AppendLine("measures:");
        yaml.AppendLine("  - arrivals");
        yaml.AppendLine("  - served");
        yaml.AppendLine();
        
        // Arrivals configuration
        var arrivalRate = request.Parameters.TryGetValue("demandRate", out var rateValue) ? 
            Convert.ToDouble(rateValue) : 1.2;
        yaml.AppendLine("arrivals:");
        yaml.AppendLine("  kind: rate");
        yaml.AppendLine($"  ratePerBin: {arrivalRate:F2}");
        yaml.AppendLine();
        
        // Served configuration  
        var servedFraction = request.Parameters.TryGetValue("servedFraction", out var fracValue) ? 
            Convert.ToDouble(fracValue) : 0.85;
        yaml.AppendLine("served:");
        yaml.AppendLine("  kind: fractionOf");
        yaml.AppendLine("  of: arrivals");
        yaml.AppendLine($"  fraction: {servedFraction:F2}");
        yaml.AppendLine();
        
        // Notes
        yaml.AppendLine($"notes: \"UI-M2 simulation from template {request.TemplateId}\"");
        
        return yaml.ToString();
    }
}
