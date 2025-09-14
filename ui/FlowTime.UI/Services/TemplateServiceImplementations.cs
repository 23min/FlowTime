using System.Globalization;
using System.Text;
using System.Text.Json;

using FlowTime.UI.Configuration;

namespace FlowTime.UI.Services;

public class TemplateService : ITemplateService
{
    private readonly IFlowTimeSimApiClient simClient;
    private readonly FeatureFlagService featureFlags;
    private readonly ILogger<TemplateService> logger;

    public TemplateService(IFlowTimeSimApiClient simClient, FeatureFlagService featureFlags, ILogger<TemplateService> logger)
    {
        this.simClient = simClient;
        this.featureFlags = featureFlags;
        this.logger = logger;
    }

    public async Task<List<TemplateInfo>> GetTemplatesAsync()
    {
        await featureFlags.EnsureLoadedAsync();

        if (featureFlags.UseDemoMode)
        {
            // Demo Mode: Return static UI templates (no API calls)
            return GetRichDomainTemplates();
        }
        else
        {
            // API Mode: Get real scenarios from FlowTime-Sim API
            return await GetRealScenariosAsync();
        }
    }

    private async Task<List<TemplateInfo>> GetRealScenariosAsync()
    {
        try
        {
            logger.LogInformation("API mode: Fetching scenarios from FlowTime-Sim API");

            // Add timeout to prevent hanging when API is down (same as LED check timeout)
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var scenariosResult = await simClient.GetScenariosAsync(cts.Token);

            if (!scenariosResult.Success)
            {
                logger.LogWarning("Failed to get scenarios from Sim API: {Error}. No fallback in API mode.", scenariosResult.Error);
                throw new InvalidOperationException($"FlowTime-Sim API error: {scenariosResult.Error}");
            }

            var scenarios = scenariosResult.Value ?? new List<ScenarioInfo>();
            logger.LogInformation("Successfully fetched {Count} scenarios from FlowTime-Sim API", scenarios.Count);
            return scenarios.Select(ConvertScenarioToTemplate).ToList();
        }
        catch (OperationCanceledException)
        {
            logger.LogError("Timeout while fetching scenarios from Sim API");
            throw new InvalidOperationException("FlowTime-Sim API request timed out. The service may be down or unresponsive.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get templates from Sim API. No fallback in API mode.");
            throw new InvalidOperationException("FlowTime-Sim API is not available. Please check that the service is running and accessible.", ex);
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
        // Use actual API data instead of hardcoded values
        var category = string.IsNullOrEmpty(scenario.Category) ? "Demo Templates" :
                      char.ToUpper(scenario.Category[0]) + scenario.Category.Substring(1) + " Templates";

        // Combine API tags with "simulation" tag to identify as demo templates
        var tags = new List<string>(scenario.Tags) { "simulation" };

        return new TemplateInfo
        {
            Id = scenario.Id,
            Name = scenario.Title,
            Description = scenario.Description,
            Category = category,
            Tags = tags,
            ParameterSchema = CreateParameterSchemaForScenario(scenario.Id)
        };
    }

    private static JsonSchema CreateParameterSchemaForScenario(string scenarioId)
    {
        return scenarioId switch
        {
            "const-quick" => new JsonSchema
            {
                Title = "Constant Arrivals Parameters",
                Properties = new Dictionary<string, JsonSchemaProperty>
                {
                    ["bins"] = new JsonSchemaProperty
                    {
                        Type = "integer",
                        Title = "Number of Time Bins",
                        Description = "How many time periods to simulate",
                        Default = 3,
                        Minimum = 1,
                        Maximum = 20
                    },
                    ["binMinutes"] = new JsonSchemaProperty
                    {
                        Type = "integer",
                        Title = "Minutes per Bin",
                        Description = "Duration of each time period in minutes",
                        Default = 60,
                        Minimum = 1,
                        Maximum = 1440
                    },
                    ["arrivalValue"] = new JsonSchemaProperty
                    {
                        Type = "integer",
                        Title = "Arrival Rate",
                        Description = "Constant number of arrivals per time bin",
                        Default = 2,
                        Minimum = 1,
                        Maximum = 100
                    },
                    ["seed"] = new JsonSchemaProperty
                    {
                        Type = "integer",
                        Title = "Random Seed",
                        Description = "Seed for reproducible results (optional)",
                        Default = 42,
                        Minimum = 1,
                        Maximum = 99999
                    }
                },
                Required = new List<string> { "bins", "binMinutes", "arrivalValue" }
            },
            "poisson-demo" => new JsonSchema
            {
                Title = "Poisson Arrivals Parameters",
                Properties = new Dictionary<string, JsonSchemaProperty>
                {
                    ["bins"] = new JsonSchemaProperty
                    {
                        Type = "integer",
                        Title = "Number of Time Bins",
                        Description = "How many time periods to simulate",
                        Default = 4,
                        Minimum = 1,
                        Maximum = 20
                    },
                    ["binMinutes"] = new JsonSchemaProperty
                    {
                        Type = "integer",
                        Title = "Minutes per Bin",
                        Description = "Duration of each time period in minutes",
                        Default = 30,
                        Minimum = 1,
                        Maximum = 1440
                    },
                    ["rate"] = new JsonSchemaProperty
                    {
                        Type = "number",
                        Title = "Arrival Rate (λ)",
                        Description = "Average number of arrivals per time bin (Poisson parameter)",
                        Default = 5.0,
                        Minimum = 0.1,
                        Maximum = 50.0
                    },
                    ["seed"] = new JsonSchemaProperty
                    {
                        Type = "integer",
                        Title = "Random Seed",
                        Description = "Seed for reproducible results (optional)",
                        Default = 123,
                        Minimum = 1,
                        Maximum = 99999
                    }
                },
                Required = new List<string> { "bins", "binMinutes", "rate" }
            },
            _ => new JsonSchema
            {
                Title = "Scenario Parameters",
                Properties = new Dictionary<string, JsonSchemaProperty>
                {
                    ["seed"] = new JsonSchemaProperty
                    {
                        Type = "integer",
                        Title = "Random Seed",
                        Description = "Seed for reproducible results",
                        Default = 42,
                        Minimum = 1,
                        Maximum = 99999
                    }
                },
                Required = new List<string>()
            }
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

    private static List<TemplateInfo> GetRichDomainTemplates()
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
            },
            new()
            {
                Id = "it-system-microservices",
                Name = "IT System with Microservices",
                Description = "Modern web application with request queues, services, and error handling",
                Category = "IT-Systems",
                Tags = new() { "beginner", "microservices", "web-scale", "modern" },
                ParameterSchema = new JsonSchema
                {
                    Title = "IT System Parameters",
                    Properties = new()
                    {
                        ["requestRate"] = new JsonSchemaProperty
                        {
                            Type = "number",
                            Title = "Request Rate (req/min)",
                            Description = "Incoming API requests per minute",
                            Default = 100.0,
                            Minimum = 10.0,
                            Maximum = 10000.0
                        },
                        ["serviceCapacity"] = new JsonSchemaProperty
                        {
                            Type = "number",
                            Title = "Service Capacity (req/min)",
                            Description = "Maximum requests each service can handle",
                            Default = 80.0,
                            Minimum = 5.0,
                            Maximum = 5000.0
                        },
                        ["errorRate"] = new JsonSchemaProperty
                        {
                            Type = "number",
                            Title = "Error Rate",
                            Description = "Percentage of requests that fail",
                            Default = 0.05,
                            Minimum = 0.0,
                            Maximum = 0.5
                        },
                        ["retryAttempts"] = new JsonSchemaProperty
                        {
                            Type = "integer",
                            Title = "Max Retry Attempts",
                            Description = "Maximum number of retries for failed requests",
                            Default = 3,
                            Minimum = 0,
                            Maximum = 10
                        },
                        ["queueCapacity"] = new JsonSchemaProperty
                        {
                            Type = "integer",
                            Title = "Queue Capacity",
                            Description = "Maximum requests that can be queued",
                            Default = 1000,
                            Minimum = 50,
                            Maximum = 50000
                        }
                    },
                    Required = new() { "requestRate", "serviceCapacity", "errorRate" }
                }
            }
        };
    }
}

public class CatalogService : ICatalogService
{
    private readonly IFlowTimeSimApiClient simClient;
    private readonly FeatureFlagService featureFlags;
    private readonly ILogger<CatalogService> logger;

    public CatalogService(IFlowTimeSimApiClient simClient, FeatureFlagService featureFlags, ILogger<CatalogService> logger)
    {
        this.simClient = simClient;
        this.featureFlags = featureFlags;
        this.logger = logger;
    }

    public async Task<List<CatalogInfo>> GetCatalogsAsync()
    {
        await featureFlags.EnsureLoadedAsync();

        if (featureFlags.UseDemoMode)
        {
            // Demo Mode: Use mock catalogs (placeholder until SIM-CAT-M2 catalog API is implemented)
            logger.LogDebug("Demo Mode: Using mock catalogs");
            return await GetMockCatalogsAsync();
        }
        else
        {
            // API Mode: Use mock catalogs (FlowTime API doesn't have catalog endpoints yet)
            logger.LogDebug("API Mode: Using mock catalogs");
            return await GetMockCatalogsAsync();
        }
    }

    private Task<List<CatalogInfo>> GetMockCatalogsAsync()
    {
        try
        {
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
    private readonly IFlowTimeApiClient apiClient;
    private readonly FeatureFlagService featureFlags;
    private readonly ILogger<FlowTimeSimService> logger;
    private readonly IConfiguration configuration;

    public FlowTimeSimService(IFlowTimeSimApiClient simClient, IFlowTimeApiClient apiClient, FeatureFlagService featureFlags, ILogger<FlowTimeSimService> logger, IConfiguration configuration)
    {
        this.simClient = simClient;
        this.apiClient = apiClient;
        this.featureFlags = featureFlags;
        this.logger = logger;
        this.configuration = configuration;
    }

    public async Task<SimulationRunResult> RunSimulationAsync(SimulationRunRequest request)
    {
        await featureFlags.EnsureLoadedAsync();

        if (featureFlags.UseDemoMode)
        {
            return await RunDemoModeSimulationAsync(request);
        }
        else
        {
            return await RunApiModeSimulationAsync(request);
        }
    }

    private async Task<SimulationRunResult> RunDemoModeSimulationAsync(SimulationRunRequest request)
    {
        try
        {
            // Generate YAML spec from the request parameters
            var yamlSpec = GenerateSimulationYaml(request);

            // Log the generated YAML for debugging
            logger.LogInformation("Generated YAML for simulation:\n{Yaml}", yamlSpec);

            // Call FlowTime-Sim API following artifact-first pattern
            var runResult = await simClient.RunAsync(yamlSpec);
            if (!runResult.Success)
            {
                logger.LogError("Simulation run failed: {Error}", runResult.Error);
                return new SimulationRunResult
                {
                    RunId = $"failed_{DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture)}",
                    Status = "failed",
                    StartTime = DateTime.UtcNow,
                    ErrorMessage = runResult.Error
                };
            }

            var runId = runResult.Value?.SimRunId ?? throw new InvalidOperationException("No run ID returned");

            // Get configuration for versioned URL
            var simConfig = configuration.GetSection(FlowTimeSimApiOptions.SectionName).Get<FlowTimeSimApiOptions>()
                ?? new FlowTimeSimApiOptions();

            // Return artifact-first result - no custom metadata blobs
            return new SimulationRunResult
            {
                RunId = runId,
                Status = "completed",
                StartTime = DateTime.UtcNow.AddSeconds(-1), // Approximate start time
                EndTime = DateTime.UtcNow,
                ResultsUrl = $"/{simConfig.ApiVersion}/sim/runs/{runId}/index", // Point to versioned series index
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
                RunId = $"error_{DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture)}",
                Status = "failed",
                StartTime = DateTime.UtcNow,
                ErrorMessage = ex.Message
            };
        }
    }

    private async Task<SimulationRunResult> RunApiModeSimulationAsync(SimulationRunRequest request)
    {
        try
        {
            // API Mode: Call FlowTime Engine API (/run) to produce real engine artifacts
            logger.LogInformation("Running API mode (engine) simulation for template {TemplateId}", request.TemplateId);

            var yamlSpec = GenerateEngineYaml(request);
            logger.LogInformation("Generated YAML for engine run (API mode):\n{Yaml}", yamlSpec);

            var runCall = await apiClient.RunAsync(yamlSpec);
            if (!runCall.Success || runCall.Value == null)
            {
                logger.LogError("Engine run failed: {Error}", runCall.Error);
                return new SimulationRunResult
                {
                    RunId = $"run_failed_{DateTime.UtcNow:yyyyMMddTHHmmssZ}",
                    Status = "failed",
                    StartTime = DateTime.UtcNow,
                    ErrorMessage = runCall.Error ?? "Unknown engine error"
                };
            }

            var runId = runCall.Value.RunId;

            var metadata = GenerateApiModeMetadata(request);
            metadata["runId"] = runId;
            metadata["source"] = "engine"; // authoritative source tag

            return new SimulationRunResult
            {
                RunId = runId,
                Status = "completed",
                StartTime = DateTime.UtcNow.AddSeconds(-2),
                EndTime = DateTime.UtcNow,
                ResultsUrl = $"/runs/{runId}/index",
                Metadata = metadata
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to run API mode simulation for template {TemplateId}", request.TemplateId);
            return new SimulationRunResult
            {
                RunId = $"run_error_{DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture)}",
                Status = "failed",
                StartTime = DateTime.UtcNow,
                ErrorMessage = ex.Message
            };
        }
    }

    private static Dictionary<string, object> GenerateApiModeMetadata(SimulationRunRequest request)
    {
        var metadata = new Dictionary<string, object>
        {
            ["templateId"] = request.TemplateId,
            ["catalogId"] = request.CatalogId,
            ["source"] = "api",
            ["engineVersion"] = "FlowTime-1.0.0",
            ["modelType"] = GetModelTypeFromTemplate(request.TemplateId),
            ["parameterCount"] = request.Parameters.Count,
            ["createdAt"] = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture)
        };

        // Add template-specific enriched metadata
        switch (request.TemplateId)
        {
            case "transportation-basic":
                metadata["networkType"] = "transportation";
                metadata["demandRate"] = request.Parameters.GetValueOrDefault("demandRate", 10.0);
                metadata["capacity"] = request.Parameters.GetValueOrDefault("capacity", 15.0);
                metadata["utilizationRate"] = Math.Min(1.0, Convert.ToDouble(metadata["demandRate"]) / Convert.ToDouble(metadata["capacity"]));
                metadata["expectedThroughput"] = Math.Min(Convert.ToDouble(metadata["demandRate"]), Convert.ToDouble(metadata["capacity"]));
                break;

            case "supply-chain-multi-tier":
                metadata["networkType"] = "supply-chain";
                metadata["supplierCount"] = request.Parameters.GetValueOrDefault("supplierCount", 3);
                metadata["leadTime"] = request.Parameters.GetValueOrDefault("leadTime", 5.0);
                metadata["demandPattern"] = request.Parameters.GetValueOrDefault("demandPattern", "steady");
                metadata["complexity"] = "multi-tier";
                break;

            case "manufacturing-line":
                metadata["networkType"] = "manufacturing";
                metadata["stationCount"] = request.Parameters.GetValueOrDefault("stationCount", 5);
                metadata["cycleTime"] = request.Parameters.GetValueOrDefault("cycleTime", 2.5);
                metadata["qualityRate"] = request.Parameters.GetValueOrDefault("qualityRate", 0.95);
                metadata["expectedOutput"] = Math.Round(60.0 / Convert.ToDouble(metadata["cycleTime"]) * Convert.ToDouble(metadata["qualityRate"]), 2);
                break;

            case "it-system-microservices":
                metadata["networkType"] = "it-system";
                metadata["requestRate"] = request.Parameters.GetValueOrDefault("requestRate", 100.0);
                metadata["serviceCapacity"] = request.Parameters.GetValueOrDefault("serviceCapacity", 80.0);
                metadata["errorRate"] = request.Parameters.GetValueOrDefault("errorRate", 0.05);
                metadata["retryAttempts"] = request.Parameters.GetValueOrDefault("retryAttempts", 3);
                metadata["queueCapacity"] = request.Parameters.GetValueOrDefault("queueCapacity", 1000);
                var capacity = Convert.ToDouble(metadata["serviceCapacity"]);
                var demand = Convert.ToDouble(metadata["requestRate"]);
                metadata["systemUtilization"] = Math.Round(Math.Min(1.0, demand / capacity), 3);
                metadata["avgResponseTime"] = Math.Round(capacity > 0 ? (1.0 / capacity) * 60 : 0, 2); // Convert to seconds
                break;
        }

        // Add performance metrics
        metadata["simulationMetrics"] = new Dictionary<string, object>
        {
            ["totalEntities"] = Random.Shared.Next(1000, 5000),
            ["avgProcessingTime"] = Math.Round(Random.Shared.NextDouble() * 10 + 5, 2),
            ["successRate"] = Math.Round(Random.Shared.NextDouble() * 0.1 + 0.90, 3),
            ["peakUtilization"] = Math.Round(Random.Shared.NextDouble() * 0.3 + 0.70, 3)
        };

        return metadata;
    }

    private static string GetModelTypeFromTemplate(string templateId)
    {
        return templateId switch
        {
            "transportation-basic" => "Flow Network",
            "supply-chain-multi-tier" => "Multi-Tier System",
            "manufacturing-line" => "Production Line",
            "it-system-microservices" => "Microservice Architecture",
            _ => "Generic Model"
        };
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
        // Generate FlowTime-Sim compatible YAML based on the scenario and parameters
        var yaml = new StringBuilder();

        // Schema version (recommended)
        yaml.AppendLine("schemaVersion: 1");

        // RNG configuration
        yaml.AppendLine("rng: pcg");

        // Get parameters with defaults based on scenario
        var bins = request.Parameters.TryGetValue("bins", out var binValue) ?
            Convert.ToInt32(binValue) : (request.TemplateId == "const-quick" ? 3 : 4);
        var binMinutes = request.Parameters.TryGetValue("binMinutes", out var binMinValue) ?
            Convert.ToInt32(binMinValue) : (request.TemplateId == "const-quick" ? 60 : 30);
        var seed = request.Parameters.TryGetValue("seed", out var seedValue) ?
            Convert.ToInt32(seedValue) : (request.TemplateId == "const-quick" ? 42 : 123);

        // Grid configuration (REQUIRED)
        yaml.AppendLine("grid:");
        yaml.AppendLine($"  bins: {bins}");
        yaml.AppendLine($"  binMinutes: {binMinutes}");

        // Seed for deterministic runs
        yaml.AppendLine($"seed: {seed}");

        // Arrivals configuration based on scenario type
        yaml.AppendLine("arrivals:");

        if (request.TemplateId == "poisson-demo")
        {
            // Poisson arrivals
            var rate = request.Parameters.TryGetValue("rate", out var rateValue) ?
                Convert.ToDouble(rateValue) : 5.0;
            yaml.AppendLine("  kind: poisson");
            yaml.AppendLine($"  rate: {rate.ToString(CultureInfo.InvariantCulture)}");
        }
        else
        {
            // Constant arrivals (default for const-quick and fallback)
            var arrivalValue = request.Parameters.TryGetValue("arrivalValue", out var arrivalValueParam) ?
                Convert.ToInt32(arrivalValueParam) : 2;
            yaml.AppendLine("  kind: const");
            yaml.Append("  values: [");
            for (int i = 0; i < bins; i++)
            {
                yaml.Append(arrivalValue.ToString(CultureInfo.InvariantCulture));
                if (i < bins - 1) yaml.Append(", ");
            }
            yaml.AppendLine("]");
        }

        // Route configuration (REQUIRED)
        yaml.AppendLine("route:");
        yaml.AppendLine("  id: COMP_A");

        return yaml.ToString();
    }

    // Engine (FlowTime API) YAML generator
    private static string GenerateEngineYaml(SimulationRunRequest request)
    {
        var yaml = new StringBuilder();
        // Grid
        var bins = request.Parameters.TryGetValue("timeBins", out var binValue) ? Convert.ToInt32(binValue) : 4;
        yaml.AppendLine("grid: { bins: " + bins + ", binMinutes: 60 }");

        // Demand rate parameter
        var demandRate = request.Parameters.TryGetValue("demandRate", out var rateValue) ? Convert.ToDouble(rateValue, CultureInfo.InvariantCulture) : 10.0;
        var capacity = request.Parameters.TryGetValue("capacity", out var capValue) ? Convert.ToDouble(capValue, CultureInfo.InvariantCulture) : (demandRate * 1.2);
        var servedFactor = Math.Min(1.0, capacity <= 0 ? 0 : demandRate / capacity);
        // Nodes section
        yaml.AppendLine("nodes:");
        yaml.Append("  - id: demand\n    kind: const\n    values: [");
        for (int i = 0; i < bins; i++)
        {
            yaml.Append(demandRate.ToString("0", CultureInfo.InvariantCulture));
            if (i < bins - 1) yaml.Append(",");
        }
        yaml.AppendLine("]");
        yaml.AppendLine("  - id: served");
        yaml.AppendLine("    kind: expr");
        yaml.AppendLine("    expr: \"demand * " + servedFactor.ToString("0.###", CultureInfo.InvariantCulture) + "\"");
        // outputs section (optional)
        yaml.AppendLine("outputs:");
        yaml.AppendLine("  - series: served");
        return yaml.ToString();
    }
}
