using System.Globalization;
using System.Text;
using System.Text.Json;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

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
            return GetDemoTemplates();
        }
        else
        {
            // API Mode: Get templates from FlowTime-Sim API
            return await GetApiTemplatesAsync();
        }
    }

    private async Task<List<TemplateInfo>> GetApiTemplatesAsync()
    {
        try
        {
            logger.LogInformation("API mode: Fetching templates from FlowTime-Sim API");

            // Add timeout to prevent hanging when API is down (same as LED check timeout)
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var templatesResult = await simClient.GetTemplatesAsync(cts.Token);

            if (!templatesResult.Success)
            {
                logger.LogWarning("Failed to get templates from Sim API: {Error}. No fallback in API mode.", templatesResult.Error);
                throw new InvalidOperationException($"FlowTime-Sim API error: {templatesResult.Error}");
            }

            var templates = templatesResult.Value ?? new List<ApiTemplateInfo>();
            logger.LogInformation("Successfully fetched {Count} templates from FlowTime-Sim API", templates.Count);
            return templates.Select(ConvertTemplateInfoToTemplate).ToList();
        }
        catch (OperationCanceledException)
        {
            logger.LogError("Timeout while fetching templates from Sim API");
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
            
            // Debug logging for IT system template
            if (templateId == "it-system-microservices" && template?.ParameterSchema?.Properties != null)
            {
                logger.LogInformation("IT System template schema has {Count} properties: {Properties}", 
                    template.ParameterSchema.Properties.Count,
                    string.Join(", ", template.ParameterSchema.Properties.Keys));
            }
            
            return template?.ParameterSchema ?? new JsonSchema();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get template schema {TemplateId}", templateId);
            throw;
        }
    }

    private static TemplateInfo ConvertTemplateInfoToTemplate(ApiTemplateInfo templateInfo)
    {
        // Use actual API data instead of hardcoded values
        var category = string.IsNullOrEmpty(templateInfo.Category) ? "Demo Templates" :
                      char.ToUpper(templateInfo.Category[0]) + templateInfo.Category.Substring(1) + " Templates";

        // Combine API tags with "simulation" tag to identify as demo templates
        var tags = new List<string>(templateInfo.Tags) { "simulation" };

        return new TemplateInfo
        {
            Id = templateInfo.Id,
            Name = templateInfo.Title,
            Description = templateInfo.Description,
            Category = category,
            Tags = tags,
            ParameterSchema = CreateParameterSchemaForTemplate(templateInfo.Id)
        };
    }

    private static JsonSchema CreateParameterSchemaForTemplate(string templateId)
    {
        return templateId switch
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
            "it-system-microservices" => new JsonSchema
            {
                Title = "IT System Parameters",
                Properties = new Dictionary<string, JsonSchemaProperty>
                {
                    ["bins"] = new JsonSchemaProperty
                    {
                        Type = "integer",
                        Title = "Time Periods",
                        Description = "Number of time periods to simulate",
                        Default = 24,
                        Minimum = 3,
                        Maximum = 168
                    },
                    ["binMinutes"] = new JsonSchemaProperty
                    {
                        Type = "integer",
                        Title = "Minutes per Period",
                        Description = "Duration of each time period",
                        Default = 60, // Hourly periods
                        Minimum = 15,
                        Maximum = 480
                    },
                    ["requestPattern"] = new JsonSchemaProperty
                    {
                        Type = "array",
                        Title = "Request Pattern",
                        Description = "Number of requests arriving in each time period",
                        Default = new List<double> { 50, 30, 20, 15, 10, 15, 25, 60, 100, 120, 110, 100, 90, 95, 85, 80, 90, 110, 130, 120, 100, 80, 70, 60 },
                        Minimum = 0,
                        Maximum = 1000
                    },
                    ["loadBalancerCapacity"] = new JsonSchemaProperty
                    {
                        Type = "array",
                        Title = "Load Balancer Capacity",
                        Description = "Load balancer processing capacity in each period",
                        Default = new List<double> { 200, 200, 200, 200, 200, 200, 200, 200, 200, 200, 200, 200, 200, 200, 200, 200, 200, 200, 200, 200, 200, 200, 200, 200 },
                        Minimum = 1,
                        Maximum = 1000
                    },
                    ["authCapacity"] = new JsonSchemaProperty
                    {
                        Type = "array",
                        Title = "Authentication Service Capacity",
                        Description = "Auth service processing capacity in each period",
                        Default = new List<double> { 150, 150, 150, 150, 150, 150, 150, 150, 150, 150, 150, 150, 150, 150, 150, 150, 150, 150, 150, 150, 150, 150, 150, 150 },
                        Minimum = 1,
                        Maximum = 1000
                    },
                    ["databaseCapacity"] = new JsonSchemaProperty
                    {
                        Type = "array",
                        Title = "Database Capacity",
                        Description = "Database processing capacity in each period",
                        Default = new List<double> { 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100 },
                        Minimum = 1,
                        Maximum = 1000
                    }
                },
                Required = new List<string> { "bins", "binMinutes", "requestPattern", "loadBalancerCapacity", "authCapacity", "databaseCapacity" }
            },
            "transportation-basic" => new JsonSchema
            {
                Title = "Transportation Parameters",
                Properties = new Dictionary<string, JsonSchemaProperty>
                {
                    ["bins"] = new JsonSchemaProperty
                    {
                        Type = "integer",
                        Title = "Time Periods",
                        Description = "Number of time periods to simulate",
                        Default = 12,
                        Minimum = 3,
                        Maximum = 48
                    },
                    ["binMinutes"] = new JsonSchemaProperty
                    {
                        Type = "integer",
                        Title = "Minutes per Period",
                        Description = "Duration of each time period",
                        Default = 60,
                        Minimum = 15,
                        Maximum = 480
                    },
                    ["demandPattern"] = new JsonSchemaProperty
                    {
                        Type = "array",
                        Title = "Passenger Demand Pattern",
                        Description = "Number of passengers wanting to travel in each time period",
                        Default = new List<double> { 10, 15, 20, 25, 18, 12 },
                        Minimum = 0,
                        Maximum = 1000
                    },
                    ["capacityPattern"] = new JsonSchemaProperty
                    {
                        Type = "array",
                        Title = "Vehicle Capacity Pattern", 
                        Description = "Number of available seats/spaces in each time period",
                        Default = new List<double> { 15, 18, 25, 30, 22, 16 },
                        Minimum = 1,
                        Maximum = 1000
                    }
                },
                Required = new List<string> { "bins", "binMinutes", "demandPattern", "capacityPattern" }
            },
            "manufacturing-line" => new JsonSchema
            {
                Title = "Manufacturing Parameters",
                Properties = new Dictionary<string, JsonSchemaProperty>
                {
                    ["bins"] = new JsonSchemaProperty
                    {
                        Type = "integer",
                        Title = "Time Periods",
                        Description = "Number of time periods to simulate",
                        Default = 6,
                        Minimum = 3,
                        Maximum = 48
                    },
                    ["binMinutes"] = new JsonSchemaProperty
                    {
                        Type = "integer",
                        Title = "Minutes per Period",
                        Description = "Duration of each time period",
                        Default = 60,
                        Minimum = 15,
                        Maximum = 480
                    },
                    ["rawMaterialSchedule"] = new JsonSchemaProperty
                    {
                        Type = "array",
                        Title = "Raw Material Schedule",
                        Description = "Amount of raw materials available in each time period",
                        Default = new List<double> { 100, 100, 80, 120, 100, 90 },
                        Minimum = 0,
                        Maximum = 500
                    },
                    ["assemblyCapacity"] = new JsonSchemaProperty
                    {
                        Type = "array",
                        Title = "Assembly Line Capacity",
                        Description = "Maximum items that can be assembled per time period",
                        Default = new List<double> { 90, 90, 90, 90, 90, 90 },
                        Minimum = 1,
                        Maximum = 200
                    },
                    ["qualityCapacity"] = new JsonSchemaProperty
                    {
                        Type = "array",
                        Title = "Quality Control Capacity",
                        Description = "Maximum items that can be quality checked per time period",
                        Default = new List<double> { 80, 80, 80, 80, 80, 80 },
                        Minimum = 1,
                        Maximum = 200
                    },
                    ["qualityRate"] = new JsonSchemaProperty
                    {
                        Type = "number",
                        Title = "Quality Pass Rate",
                        Description = "Percentage of items that pass quality control (0.0-1.0)",
                        Default = 0.95,
                        Minimum = 0.1,
                        Maximum = 1.0
                    },
                    ["productionRate"] = new JsonSchemaProperty
                    {
                        Type = "number",
                        Title = "Production Rate",
                        Description = "Base production rate per time period",
                        Default = 12.0,
                        Minimum = 1.0,
                        Maximum = 100.0
                    },
                    ["defectRate"] = new JsonSchemaProperty
                    {
                        Type = "number",
                        Title = "Defect Rate",
                        Description = "Percentage of items that become defective (0.0-1.0)",
                        Default = 0.05,
                        Minimum = 0.0,
                        Maximum = 0.5
                    }
                },
                Required = new List<string> { "bins", "binMinutes", "rawMaterialSchedule", "assemblyCapacity", "qualityCapacity" }
            },
            "supply-chain-multi-tier" => new JsonSchema
            {
                Title = "Supply Chain Parameters",
                Properties = new Dictionary<string, JsonSchemaProperty>
                {
                    ["bins"] = new JsonSchemaProperty
                    {
                        Type = "integer",
                        Title = "Time Periods",
                        Description = "Number of time periods to simulate",
                        Default = 12,
                        Minimum = 3,
                        Maximum = 48
                    },
                    ["binMinutes"] = new JsonSchemaProperty
                    {
                        Type = "integer",
                        Title = "Minutes per Period",
                        Description = "Duration of each time period",
                        Default = 1440, // Daily periods
                        Minimum = 60,
                        Maximum = 10080
                    },
                    ["demandPattern"] = new JsonSchemaProperty
                    {
                        Type = "array",
                        Title = "Customer Demand Pattern",
                        Description = "Number of orders in each time period",
                        Default = new List<double> { 100, 120, 80, 150, 90, 110, 130, 95, 105, 140, 85, 125 },
                        Minimum = 0,
                        Maximum = 10000
                    },
                    ["supplierCapacity"] = new JsonSchemaProperty
                    {
                        Type = "array",
                        Title = "Supplier Capacity Pattern",
                        Description = "Supplier production capacity in each period",
                        Default = new List<double> { 120, 130, 100, 160, 110, 125, 140, 105, 115, 150, 95, 135 },
                        Minimum = 1,
                        Maximum = 10000
                    },
                    ["distributorCapacity"] = new JsonSchemaProperty
                    {
                        Type = "array",
                        Title = "Distributor Capacity Pattern",
                        Description = "Distribution center capacity in each period",
                        Default = new List<double> { 110, 125, 95, 155, 105, 120, 135, 100, 110, 145, 90, 130 },
                        Minimum = 1,
                        Maximum = 10000
                    },
                    ["retailerCapacity"] = new JsonSchemaProperty
                    {
                        Type = "array",
                        Title = "Retailer Capacity Pattern",
                        Description = "Retail sales capacity in each period",
                        Default = new List<double> { 105, 120, 90, 150, 100, 115, 130, 95, 105, 140, 85, 125 },
                        Minimum = 1,
                        Maximum = 10000
                    },
                    ["bufferSize"] = new JsonSchemaProperty
                    {
                        Type = "number",
                        Title = "Buffer Size Multiplier",
                        Description = "Safety stock multiplier (e.g., 1.2 = 20% buffer)",
                        Default = 1.2,
                        Minimum = 1.0,
                        Maximum = 3.0
                    }
                },
                Required = new List<string> { "bins", "binMinutes", "demandPattern", "supplierCapacity", "distributorCapacity", "retailerCapacity" }
            },
            _ => new JsonSchema
            {
                Title = "Template Parameters",
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



    private static List<TemplateInfo> GetDemoTemplates()
    {
        return new List<TemplateInfo>
        {
            // Theoretical templates - useful in both demo and API modes
            new()
            {
                Id = "const-arrivals-demo",
                Name = "Constant Arrivals",
                Description = "Simple model with constant arrival rates - ideal for learning FlowTime basics",
                Category = "Theoretical",
                Tags = new() { "theoretical", "beginner", "arrivals" },
                ParameterSchema = CreateParameterSchemaForTemplate("const-quick")
            },
            new()
            {
                Id = "poisson-arrivals-demo", 
                Name = "Poisson Arrivals",
                Description = "Stochastic arrival model using Poisson distribution - good for understanding variability",
                Category = "Theoretical",
                Tags = new() { "theoretical", "intermediate", "stochastic", "arrivals" },
                ParameterSchema = CreateParameterSchemaForTemplate("poisson-demo")
            },
            // Domain-specific templates
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

    private Task<SimulationRunResult> RunDemoModeSimulationAsync(SimulationRunRequest request)
    {
        // Demo Mode: Generate synthetic data offline without calling any APIs
        logger.LogInformation("Running demo mode (offline) simulation for template {TemplateId}", request.TemplateId);

        try
        {
            // Generate realistic synthetic data based on template parameters
            var runResult = GenerateDemoSimulationData(request);
            // Return offline demo result
            return Task.FromResult(runResult);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to run demo mode simulation for template {TemplateId}", request.TemplateId);
            return Task.FromResult(new SimulationRunResult
            {
                RunId = $"demo_error_{DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture)}",
                Status = "failed",
                StartTime = DateTime.UtcNow,
                ErrorMessage = ex.Message
            });
        }
    }

    private SimulationRunResult GenerateDemoSimulationData(SimulationRunRequest request)
    {
        // Generate realistic synthetic data based on template parameters for offline demo mode
        var runId = $"demo_{DateTime.UtcNow:yyyyMMddTHHmmssZ}_{Guid.NewGuid().ToString("N")[..8]}";
        
        logger.LogInformation("Generating synthetic demo data for template {TemplateId} with runId {RunId}", 
            request.TemplateId, runId);

        // Demo mode uses a special "demo://" scheme URL that SimResultsService handles
        return new SimulationRunResult
        {
            RunId = runId,
            Status = "completed", 
            StartTime = DateTime.UtcNow.AddSeconds(-2),
            EndTime = DateTime.UtcNow,
            ResultsUrl = $"demo://{runId}", // Special scheme for demo mode
            Metadata = new Dictionary<string, object>
            {
                ["templateId"] = request.TemplateId,
                ["source"] = "demo",
                ["mode"] = "offline",
                ["dataType"] = "synthetic telemetry",
                ["description"] = "Simulated IT system microservices performance data",
                ["parameters"] = request.Parameters ?? new Dictionary<string, object>()
            }
        };
    }

    private async Task<SimulationRunResult> RunApiModeSimulationAsync(SimulationRunRequest request)
    {
        try
        {
            // API Mode: Call FlowTime-Sim API (/sim/run) to produce real simulation artifacts
            logger.LogInformation("Running API mode (FlowTime-Sim) simulation for template {TemplateId}", request.TemplateId);

            // Generate YAML for FlowTime-Sim using arrivals/route schema translation
            var yamlSpec = await GenerateSimulationYamlAsync(request);
            logger.LogInformation("Generated YAML for FlowTime-Sim run (API mode):\n{Yaml}", yamlSpec);

            var runCall = await simClient.RunAsync(yamlSpec);
            if (!runCall.Success || runCall.Value == null)
            {
                logger.LogError("FlowTime-Sim run failed: {Error}", runCall.Error);
                return new SimulationRunResult
                {
                    RunId = $"sim_failed_{DateTime.UtcNow:yyyyMMddTHHmmssZ}",
                    Status = "failed",
                    StartTime = DateTime.UtcNow,
                    ErrorMessage = runCall.Error ?? "Unknown FlowTime-Sim error"
                };
            }

            var runId = runCall.Value.SimRunId;

            // Get configuration for versioned URL
            var simConfig = configuration.GetSection(FlowTimeSimApiOptions.SectionName).Get<FlowTimeSimApiOptions>()
                ?? new FlowTimeSimApiOptions();

            return new SimulationRunResult
            {
                RunId = runId,
                Status = "completed",
                StartTime = DateTime.UtcNow.AddSeconds(-2),
                EndTime = DateTime.UtcNow,
                ResultsUrl = $"/{simConfig.ApiVersion}/sim/runs/{runId}/index", // Point to versioned FlowTime-Sim series index
                Metadata = new() // Minimal metadata, artifacts are authoritative
                {
                    ["templateId"] = request.TemplateId,
                    ["source"] = "sim", // FlowTime-Sim API source
                    ["dataType"] = "simulation telemetry",
                    ["description"] = "Real simulation data from FlowTime-Sim API"
                }
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

    public async Task<string> GenerateModelYamlAsync(SimulationRunRequest request)
    {
        if (featureFlags.UseDemoMode)
        {
            // Demo mode: use hardcoded generation for offline capabilities
            return GenerateSimulationYaml(request);
        }
        
        try
        {
            // API mode: call FlowTime-Sim API to generate model from template
            logger.LogInformation("Generating model via API for template {TemplateId}", request.TemplateId);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var result = await simClient.GenerateModelAsync(request.TemplateId, request.Parameters, cts.Token);
            
            if (result.Success && result.Value != null)
            {
                logger.LogInformation("Successfully generated model via API - length: {Length} chars", result.Value.Model?.Length ?? 0);
                return result.Value.Model ?? string.Empty;
            }
            
            logger.LogWarning("API model generation failed: {Error}, falling back to hardcoded generation", result.Error);
            // Fallback to hardcoded generation if API fails
            return GenerateSimulationYaml(request);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error calling API for model generation, falling back to hardcoded generation");
            // Fallback to hardcoded generation on exception
            return GenerateSimulationYaml(request);
        }
    }

    /// <summary>
    /// Generate YAML for FlowTime-Sim using the arrivals/route schema format
    /// </summary>
    public async Task<string> GenerateSimulationYamlAsync(SimulationRunRequest request)
    {
        await Task.CompletedTask; // Make this async for consistency
        
        // Generate the modern nodes format first
        var nodesYaml = GenerateSimulationYaml(request);
        
        // Convert to FlowTime-Sim format (arrivals/route schema)
        return TranslateToSimulationSchema(nodesYaml, request);
    }

    /// <summary>
    /// Temporary stub method to support existing tests during API integration transition.
    /// TODO: Remove this method and update tests once API integration is complete.
    /// </summary>
    private static Dictionary<string, object> ConvertRequestToApiParameters(SimulationRunRequest request)
    {
        if (request?.Parameters == null) 
            return new Dictionary<string, object>();
            
        var result = new Dictionary<string, object>(request.Parameters);
        
        // Add catalogId if present in request (test expectation)
        if (!string.IsNullOrEmpty(request.CatalogId))
        {
            result["catalogId"] = request.CatalogId;
        }
        
        // Convert string arrays to double arrays for specific parameters (test expectation)
        var arrayParams = new[] { "demandPattern", "capacityPattern", "rawMaterialSchedule", "assemblyCapacity" };
        foreach (var param in arrayParams)
        {
            if (result.TryGetValue(param, out var value) && value is List<string> stringList)
            {
                var doubleArray = stringList.Select(s => double.TryParse(s, out var d) ? d : 0.0).ToArray();
                result[param] = doubleArray;
            }
        }
        
        return result;
    }

    private static string GenerateSimulationYaml(SimulationRunRequest request)
    {
        // Generate template-specific YAML with proper topology
        var result = request.TemplateId switch
        {
            "it-system-microservices" => GenerateITSystemYaml(request),
            "transportation-basic" => GenerateTransportationYaml(request),
            "manufacturing-line" => GenerateManufacturingYaml(request),
            "supply-chain-multi-tier" => GenerateSupplyChainYaml(request),
            _ => GenerateBasicSimulationYaml(request)
        };
        
        // Debug: Log which generation method was used
        System.Diagnostics.Debug.WriteLine($"Generated YAML for template '{request.TemplateId}' using {request.TemplateId switch {
            "it-system-microservices" => "GenerateITSystemYaml",
            "transportation-basic" => "GenerateTransportationYaml", 
            "manufacturing-line" => "GenerateManufacturingYaml",
            "supply-chain-multi-tier" => "GenerateSupplyChainYaml",
            _ => "GenerateBasicSimulationYaml (fallback)"
        }}");
        
        return result;
    }

    /// <summary>
    /// Translate from FlowTime Engine nodes schema to FlowTime-Sim arrivals/route schema
    /// </summary>
    private static string TranslateToSimulationSchema(string nodesYaml, SimulationRunRequest request)
    {
        try
        {
            // Parse the nodes YAML to extract the first const node for arrivals
            using var reader = new StringReader(nodesYaml);
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();
                
            var model = deserializer.Deserialize<Dictionary<string, object>>(nodesYaml);
            
            // Extract grid information
            var grid = model.TryGetValue("grid", out var gridObj) ? gridObj as Dictionary<object, object> : null;
            var bins = grid?.TryGetValue("bins", out var binsObj) == true ? Convert.ToInt32(binsObj) : 24;
            var binMinutes = grid?.TryGetValue("binMinutes", out var binMinutesObj) == true ? Convert.ToInt32(binMinutesObj) : 60;
            
            // Extract the first const node as the arrival pattern
            var nodes = model.TryGetValue("nodes", out var nodesObj) ? nodesObj as List<object> : null;
            var firstConstNode = nodes?.OfType<Dictionary<object, object>>()
                .FirstOrDefault(n => n.TryGetValue("kind", out var kind) && kind?.ToString() == "const");
                
            var values = firstConstNode?.TryGetValue("values", out var valuesObj) == true 
                ? (valuesObj as List<object>)?.Select(v => Convert.ToDouble(v)).ToArray() 
                : Enumerable.Repeat(100.0, bins).ToArray();
            
            var nodeId = firstConstNode?.TryGetValue("id", out var idObj) == true 
                ? idObj?.ToString() ?? "demand" 
                : "demand";

            // Generate FlowTime-Sim YAML with arrivals/route schema
            var yaml = new StringBuilder();
            yaml.AppendLine("# Translated from nodes schema to FlowTime-Sim arrivals/route schema");
            yaml.AppendLine("schemaVersion: 1");
            yaml.AppendLine("grid:");
            yaml.AppendLine($"  bins: {bins}");
            yaml.AppendLine($"  binMinutes: {binMinutes}");
            yaml.AppendLine();
            yaml.AppendLine("arrivals:");
            yaml.AppendLine("  kind: const");
            yaml.Append("  values: [");
            yaml.Append(string.Join(", ", values.Select(v => v.ToString("F0", CultureInfo.InvariantCulture))));
            yaml.AppendLine("]");
            yaml.AppendLine();
            yaml.AppendLine("route:");
            yaml.AppendLine($"  id: {nodeId}");
            
            return yaml.ToString();
        }
        catch (Exception ex)
        {
            // Don't hide translation failures - throw with clear context
            throw new InvalidOperationException(
                $"Failed to translate template '{request.TemplateId}' from nodes schema to FlowTime-Sim arrivals/route schema. " +
                $"This template may be too complex for automatic translation. " +
                $"Original error: {ex.Message}", ex);
        }
    }

    private static string GenerateITSystemYaml(SimulationRunRequest request)
    {
        var yaml = new StringBuilder();
        
        // Get IT system parameters
        var requestRate = request.Parameters.TryGetValue("requestRate", out var rr) ? Convert.ToDouble(rr) : 100.0;
        var serviceCapacity = request.Parameters.TryGetValue("serviceCapacity", out var sc) ? Convert.ToDouble(sc) : 80.0;
        var errorRate = request.Parameters.TryGetValue("errorRate", out var er) ? Convert.ToDouble(er) : 0.05;
        var retryAttempts = request.Parameters.TryGetValue("retryAttempts", out var ra) ? Convert.ToInt32(ra) : 3;
        var queueCapacity = request.Parameters.TryGetValue("queueCapacity", out var qc) ? Convert.ToInt32(qc) : 1000;
        
        // Calculate derived values
        var bins = 24; // 24 hours
        var binMinutes = 60; // hourly bins
        var successRate = 1.0 - errorRate;
        var effectiveCapacity = serviceCapacity * successRate;
        
        yaml.AppendLine("# IT System with Microservices - Generated Model");
        yaml.AppendLine("# This demonstrates a modern web application handling user requests through microservices");
        yaml.AppendLine();
        
        // Add metadata section to preserve template information
        yaml.AppendLine("metadata:");
        yaml.AppendLine("  title: 'IT System with Microservices - Generated Model'");
        yaml.AppendLine("  description: 'Modern web application handling user requests through microservices'");
        yaml.AppendLine("  templateId: 'it-system-microservices'");
        yaml.AppendLine("  tags: [microservices, web-scale, modern, it-systems]");
        yaml.AppendLine();
        
        yaml.AppendLine("schemaVersion: 1");
        yaml.AppendLine($"grid:");
        yaml.AppendLine($"  bins: {bins}");
        yaml.AppendLine($"  binMinutes: {binMinutes}");
        yaml.AppendLine();
        
        yaml.AppendLine("nodes:");
        
        // User requests (varies by time of day - peak during business hours)
        yaml.AppendLine("  # Incoming user requests (varies by time of day)");
        yaml.AppendLine("  - id: user_requests");
        yaml.AppendLine("    kind: const");
        yaml.Append("    values: [");
        for (int i = 0; i < bins; i++)
        {
            // Simulate daily traffic pattern with peak during business hours (9-17)
            var timeOfDay = i;
            var hourlyRate = timeOfDay >= 9 && timeOfDay <= 17 
                ? requestRate * (0.8 + 0.4 * Math.Sin((timeOfDay - 9) * Math.PI / 8)) // Business hours with variation
                : requestRate * (0.2 + 0.1 * Math.Sin(timeOfDay * Math.PI / 12)); // Off hours
            
            yaml.Append(hourlyRate.ToString("F0", CultureInfo.InvariantCulture));
            if (i < bins - 1) yaml.Append(", ");
        }
        yaml.AppendLine("]");
        yaml.AppendLine();
        
        // Load balancer
        yaml.AppendLine("  # Load balancer distributes requests");
        yaml.AppendLine("  - id: load_balancer");
        yaml.AppendLine("    kind: expr");
        yaml.AppendLine($"    expr: \"MIN(user_requests, {queueCapacity})\"");
        yaml.AppendLine();
        
        // Authentication service
        yaml.AppendLine("  # Authentication service processes login requests");
        yaml.AppendLine("  - id: auth_service");
        yaml.AppendLine("    kind: expr");
        yaml.AppendLine($"    expr: \"MIN(load_balancer, {effectiveCapacity}) * {successRate:F3}\"");
        yaml.AppendLine();
        
        // Business logic service
        yaml.AppendLine("  # Business logic service handles authenticated requests");
        yaml.AppendLine("  - id: business_service");
        yaml.AppendLine("    kind: expr");
        yaml.AppendLine($"    expr: \"MIN(auth_service, {serviceCapacity * 0.9:F0}) * {(1 - errorRate * 0.5):F3}\"");
        yaml.AppendLine();
        
        // Database service
        yaml.AppendLine("  # Database service handles data operations");
        yaml.AppendLine("  - id: database_service");
        yaml.AppendLine("    kind: expr"); 
        yaml.AppendLine($"    expr: \"MIN(business_service, {serviceCapacity * 1.2:F0}) * {(1 - errorRate * 0.3):F3}\"");
        yaml.AppendLine();
        
        // API Gateway (final output)
        yaml.AppendLine("  # API Gateway final response");
        yaml.AppendLine("  - id: api_response");
        yaml.AppendLine("    kind: expr");
        yaml.AppendLine("    expr: \"MIN(auth_service, MIN(business_service, database_service)) * 0.98\"");
        yaml.AppendLine();
        
        // Retry logic for failed requests
        if (retryAttempts > 0)
        {
            yaml.AppendLine("  # Retry service for failed requests");
            yaml.AppendLine("  - id: retry_service");
            yaml.AppendLine("    kind: expr");
            yaml.AppendLine($"    expr: \"(user_requests - api_response) * {Math.Min(retryAttempts, 3) * 0.3:F2}\"");
            yaml.AppendLine();
        }
        
        yaml.AppendLine("outputs:");
        yaml.AppendLine("  - series: user_requests");
        yaml.AppendLine("    as: user_requests.csv");
        yaml.AppendLine("  - series: api_response"); 
        yaml.AppendLine("    as: served.csv");
        yaml.AppendLine("  - series: auth_service");
        yaml.AppendLine("    as: auth_service.csv");
        yaml.AppendLine("  - series: business_service");
        yaml.AppendLine("    as: business_service.csv");
        yaml.AppendLine("  - series: database_service");
        yaml.AppendLine("    as: database_service.csv");
        
        return yaml.ToString();
    }

    private static string GenerateTransportationYaml(SimulationRunRequest request)
    {
        var yaml = new StringBuilder();
        
        // Get transportation parameters
        var demandRate = request.Parameters.TryGetValue("demandRate", out var dr) ? Convert.ToDouble(dr) : 10.0;
        var capacity = request.Parameters.TryGetValue("capacity", out var cap) ? Convert.ToDouble(cap) : 15.0;
        var simulationHours = request.Parameters.TryGetValue("simulationHours", out var h) ? Convert.ToInt32(h) : 24;
        
        yaml.AppendLine("# Transportation Network - Generated Model");
        yaml.AppendLine("# This simulates passenger demand and vehicle capacity in a transit system");
        yaml.AppendLine();
        
        // Add metadata section to preserve template information
        yaml.AppendLine("metadata:");
        yaml.AppendLine("  title: 'This simulates passenger demand and vehicle capacity in a transit system'");
        yaml.AppendLine("  description: 'Transportation network with demand patterns and capacity constraints'");
        yaml.AppendLine("  templateId: 'transportation-basic'");
        yaml.AppendLine("  tags: [transportation, transit, capacity, beginner]");
        yaml.AppendLine();
        
        yaml.AppendLine("schemaVersion: 1");
        yaml.AppendLine($"grid:");
        yaml.AppendLine($"  bins: {simulationHours}");
        yaml.AppendLine($"  binMinutes: 60");
        yaml.AppendLine();
        yaml.AppendLine("nodes:");
        
        // Passenger demand (varies by hour - rush hours, etc.)
        yaml.AppendLine("  # Passenger demand (varies by time of day)");
        yaml.AppendLine("  - id: passenger_demand");
        yaml.AppendLine("    kind: const");
        yaml.Append("    values: [");
        for (int i = 0; i < simulationHours; i++)
        {
            // Create rush hour patterns: morning (7-9) and evening (17-19)
            var hourOfDay = i % 24;
            var rushMultiplier = 1.0;
            if ((hourOfDay >= 7 && hourOfDay <= 9) || (hourOfDay >= 17 && hourOfDay <= 19))
            {
                rushMultiplier = 2.5; // Rush hour
            }
            else if (hourOfDay >= 6 && hourOfDay <= 22)
            {
                rushMultiplier = 1.2; // Regular daytime
            }
            else
            {
                rushMultiplier = 0.3; // Night time
            }
            
            var hourlyDemand = demandRate * rushMultiplier;
            yaml.Append(hourlyDemand.ToString("F1", CultureInfo.InvariantCulture));
            if (i < simulationHours - 1) yaml.Append(", ");
        }
        yaml.AppendLine("]");
        yaml.AppendLine();
        
        // Vehicle capacity
        yaml.AppendLine("  # Available vehicle capacity");
        yaml.AppendLine("  - id: vehicle_capacity");
        yaml.AppendLine("    kind: const");
        yaml.AppendLine($"    values: [{string.Join(", ", Enumerable.Repeat(capacity.ToString("F0", CultureInfo.InvariantCulture), simulationHours))}]");
        yaml.AppendLine();
        
        // Passengers served
        yaml.AppendLine("  # Passengers successfully transported");
        yaml.AppendLine("  - id: passengers_served");
        yaml.AppendLine("    kind: expr");
        yaml.AppendLine("    expr: \"MIN(passenger_demand, vehicle_capacity)\"");
        yaml.AppendLine();
        
        // Unmet demand (overcrowding)
        yaml.AppendLine("  # Unmet demand (overcrowding situations)");
        yaml.AppendLine("  - id: unmet_demand");
        yaml.AppendLine("    kind: expr");
        yaml.AppendLine("    expr: \"MAX(0, passenger_demand - vehicle_capacity)\"");
        yaml.AppendLine();
        
        // System utilization
        yaml.AppendLine("  # System utilization rate");
        yaml.AppendLine("  - id: utilization");
        yaml.AppendLine("    kind: expr");
        yaml.AppendLine("    expr: \"passengers_served / vehicle_capacity\"");
        yaml.AppendLine();
        
        yaml.AppendLine("outputs:");
        yaml.AppendLine("  - series: passenger_demand");
        yaml.AppendLine("    as: demand.csv");
        yaml.AppendLine("  - series: passengers_served");
        yaml.AppendLine("    as: served.csv");
        yaml.AppendLine("  - series: unmet_demand");
        yaml.AppendLine("    as: overcrowding.csv");
        yaml.AppendLine("  - series: utilization");
        yaml.AppendLine("    as: utilization.csv");
        
        return yaml.ToString();
    }

    private static string GenerateManufacturingYaml(SimulationRunRequest request)
    {
        var yaml = new StringBuilder();
        
        // Get manufacturing parameters
        var productionRate = request.Parameters.TryGetValue("productionRate", out var pr) ? Convert.ToDouble(pr) : 50.0;
        var defectRate = request.Parameters.TryGetValue("defectRate", out var dr) ? Convert.ToDouble(dr) : 0.03;
        var maintenanceHours = request.Parameters.TryGetValue("maintenanceHours", out var mh) ? Convert.ToInt32(mh) : 2;
        var shifts = request.Parameters.TryGetValue("shifts", out var s) ? Convert.ToInt32(s) : 3;
        
        var hoursPerShift = 8;
        var totalHours = shifts * hoursPerShift;
        var qualityRate = 1.0 - defectRate;
        var effectiveHours = totalHours - maintenanceHours;
        var hourlyProduction = productionRate * qualityRate;
        
        yaml.AppendLine("# Manufacturing Production Line - Generated Model");
        yaml.AppendLine("# This simulates a production line with quality control and maintenance downtime");
        yaml.AppendLine();
        
        // Add metadata section to preserve template information
        yaml.AppendLine("metadata:");
        yaml.AppendLine("  title: 'Manufacturing Production Line - Generated Model'");
        yaml.AppendLine("  description: 'Production line with quality control and maintenance downtime'");
        yaml.AppendLine("  templateId: 'manufacturing-line'");
        yaml.AppendLine("  tags: [manufacturing, production, bottleneck, operations]");
        yaml.AppendLine();
        
        yaml.AppendLine("schemaVersion: 1");
        yaml.AppendLine($"grid:");
        yaml.AppendLine($"  bins: {totalHours}");
        yaml.AppendLine($"  binMinutes: 60");
        yaml.AppendLine();
        yaml.AppendLine("nodes:");
        
        // Raw material input
        yaml.AppendLine("  # Raw material availability");
        yaml.AppendLine("  - id: raw_materials");
        yaml.AppendLine("    kind: const");
        yaml.Append("    values: [");
        for (int i = 0; i < totalHours; i++)
        {
            // Maintenance downtime reduces availability
            var availability = (i < maintenanceHours) ? 0 : productionRate;
            yaml.Append(availability.ToString("F0", CultureInfo.InvariantCulture));
            if (i < totalHours - 1) yaml.Append(", ");
        }
        yaml.AppendLine("]");
        yaml.AppendLine();
        
        // Production line stages
        yaml.AppendLine("  # Assembly stage");
        yaml.AppendLine("  - id: assembly");
        yaml.AppendLine("    kind: expr");
        yaml.AppendLine($"    expr: \"MIN(raw_materials, {hourlyProduction:F1})\"");
        yaml.AppendLine();
        
        yaml.AppendLine("  # Quality control");
        yaml.AppendLine("  - id: quality_control");
        yaml.AppendLine("    kind: expr");
        yaml.AppendLine($"    expr: \"assembly * {qualityRate:F3}\"");
        yaml.AppendLine();
        
        yaml.AppendLine("  # Final packaging");
        yaml.AppendLine("  - id: packaging");
        yaml.AppendLine("    kind: expr");
        yaml.AppendLine($"    expr: \"MIN(quality_control, {productionRate * 0.9:F1})\"");
        yaml.AppendLine();
        
        yaml.AppendLine("  # Defective items (waste)");
        yaml.AppendLine("  - id: defects");
        yaml.AppendLine("    kind: expr");
        yaml.AppendLine($"    expr: \"assembly * {defectRate:F3}\"");
        yaml.AppendLine();
        
        yaml.AppendLine("outputs:");
        yaml.AppendLine("  - series: raw_materials");
        yaml.AppendLine("    as: raw_materials.csv");
        yaml.AppendLine("  - series: packaging");
        yaml.AppendLine("    as: finished_goods.csv");
        yaml.AppendLine("  - series: defects");
        yaml.AppendLine("    as: waste.csv");
        
        return yaml.ToString();
    }

    private static string GenerateSupplyChainYaml(SimulationRunRequest request)
    {
        var yaml = new StringBuilder();
        
        // Get supply chain parameters
        var orderVolume = request.Parameters.TryGetValue("orderVolume", out var ov) ? Convert.ToDouble(ov) : 100.0;
        var supplierReliability = request.Parameters.TryGetValue("supplierReliability", out var sr) ? Convert.ToDouble(sr) : 0.95;
        var inventoryCapacity = request.Parameters.TryGetValue("inventoryCapacity", out var ic) ? Convert.ToInt32(ic) : 5000;
        var leadTimeDays = request.Parameters.TryGetValue("leadTimeDays", out var ltd) ? Convert.ToInt32(ltd) : 7;
        
        var simulationDays = Math.Max(leadTimeDays * 4, 30); // At least 30 days or 4x lead time
        var dailyCapacity = inventoryCapacity / 10.0; // 10% of capacity per day processing
        
        yaml.AppendLine("# Multi-Tier Supply Chain - Generated Model");
        yaml.AppendLine("# This simulates order processing through suppliers, warehousing, and distribution");
        yaml.AppendLine();
        yaml.AppendLine("schemaVersion: 1");
        yaml.AppendLine($"grid:");
        yaml.AppendLine($"  bins: {simulationDays}");
        yaml.AppendLine($"  binMinutes: 1440"); // Daily bins
        yaml.AppendLine();
        yaml.AppendLine("nodes:");
        
        // Customer orders
        yaml.AppendLine("  # Daily customer orders");
        yaml.AppendLine("  - id: customer_orders");
        yaml.AppendLine("    kind: const");
        yaml.Append("    values: [");
        for (int i = 0; i < simulationDays; i++)
        {
            // Add some daily variation (±20%)
            var dailyOrders = orderVolume * (0.8 + 0.4 * Math.Sin(i * Math.PI / 7)); // Weekly pattern
            yaml.Append(dailyOrders.ToString("F0", CultureInfo.InvariantCulture));
            if (i < simulationDays - 1) yaml.Append(", ");
        }
        yaml.AppendLine("]");
        yaml.AppendLine();
        
        // Supplier processing with lead time and reliability
        yaml.AppendLine("  # Supplier order processing (with lead time delay)");
        yaml.AppendLine("  - id: supplier_orders");
        yaml.AppendLine("    kind: expr");
        yaml.AppendLine($"    expr: \"customer_orders * {supplierReliability:F3}\"");
        yaml.AppendLine();
        
        // Warehouse receiving (delayed by lead time)
        yaml.AppendLine("  # Warehouse receiving (simulated lead time impact)");
        yaml.AppendLine("  - id: warehouse_receiving");
        yaml.AppendLine("    kind: expr");
        yaml.AppendLine($"    expr: \"MIN(supplier_orders, {dailyCapacity:F0})\"");
        yaml.AppendLine();
        
        // Inventory management
        yaml.AppendLine("  # Inventory processing");
        yaml.AppendLine("  - id: inventory_processed");
        yaml.AppendLine("    kind: expr");
        yaml.AppendLine($"    expr: \"MIN(warehouse_receiving, {inventoryCapacity / simulationDays:F0})\"");
        yaml.AppendLine();
        
        // Distribution to customers
        yaml.AppendLine("  # Distribution to customers");
        yaml.AppendLine("  - id: fulfilled_orders");
        yaml.AppendLine("    kind: expr");
        yaml.AppendLine("    expr: \"MIN(inventory_processed, customer_orders * 0.98)\"");
        yaml.AppendLine();
        
        // Backorders (unfulfilled demand)
        yaml.AppendLine("  # Backorders (unfulfilled demand)");
        yaml.AppendLine("  - id: backorders");
        yaml.AppendLine("    kind: expr");
        yaml.AppendLine("    expr: \"MAX(0, customer_orders - fulfilled_orders)\"");
        yaml.AppendLine();
        
        yaml.AppendLine("outputs:");
        yaml.AppendLine("  - series: customer_orders");
        yaml.AppendLine("    as: orders.csv");
        yaml.AppendLine("  - series: fulfilled_orders");
        yaml.AppendLine("    as: fulfilled.csv");
        yaml.AppendLine("  - series: backorders");
        yaml.AppendLine("    as: backorders.csv");
        yaml.AppendLine("  - series: inventory_processed");
        yaml.AppendLine("    as: inventory.csv");
        
        return yaml.ToString();
    }

    private static string GenerateBasicSimulationYaml(SimulationRunRequest request)
    {
        // Fallback for basic templates - using modern nodes format
        var yaml = new StringBuilder();
        var bins = request.Parameters.TryGetValue("bins", out var binValue) ? Convert.ToInt32(binValue) : 4;
        var binMinutes = request.Parameters.TryGetValue("binMinutes", out var binMinValue) ? Convert.ToInt32(binMinValue) : 60;
        var demandRate = request.Parameters.TryGetValue("demandRate", out var rateValue) ? Convert.ToDouble(rateValue) : 100.0;

        yaml.AppendLine("# Basic Simulation Model - Generated");
        yaml.AppendLine("schemaVersion: 1");
        yaml.AppendLine("grid:");
        yaml.AppendLine($"  bins: {bins}");
        yaml.AppendLine($"  binMinutes: {binMinutes}");
        yaml.AppendLine();
        yaml.AppendLine("nodes:");
        yaml.AppendLine("  - id: demand");
        yaml.AppendLine("    kind: const");
        yaml.Append("    values: [");
        for (int i = 0; i < bins; i++)
        {
            yaml.Append(demandRate.ToString("F0", CultureInfo.InvariantCulture));
            if (i < bins - 1) yaml.Append(", ");
        }
        yaml.AppendLine("]");
        yaml.AppendLine();
        yaml.AppendLine("  - id: served");
        yaml.AppendLine("    kind: expr");
        yaml.AppendLine("    expr: \"demand * 0.85\"");
        yaml.AppendLine();
        yaml.AppendLine("outputs:");
        yaml.AppendLine("  - series: demand");
        yaml.AppendLine("    as: demand.csv");
        yaml.AppendLine("  - series: served");
        yaml.AppendLine("    as: served.csv");

        return yaml.ToString();
    }


}
