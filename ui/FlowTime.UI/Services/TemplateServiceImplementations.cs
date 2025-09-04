using System.Text.Json;

namespace FlowTime.UI.Services;

public class TemplateService : ITemplateService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TemplateService> _logger;

    public TemplateService(HttpClient httpClient, ILogger<TemplateService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public Task<List<TemplateInfo>> GetTemplatesAsync()
    {
        try
        {
            // For UI-M1, we'll start with mock data since SIM-SVC-M2 templates aren't implemented yet
            // This will be replaced with actual HTTP calls to FlowTime-Sim service
            return Task.FromResult(GetMockTemplates());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get templates");
            throw;
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
            _logger.LogError(ex, "Failed to get template {TemplateId}", templateId);
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
            _logger.LogError(ex, "Failed to get template schema {TemplateId}", templateId);
            throw;
        }
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
    private readonly HttpClient _httpClient;
    private readonly ILogger<CatalogService> _logger;

    public CatalogService(HttpClient httpClient, ILogger<CatalogService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public Task<List<CatalogInfo>> GetCatalogsAsync()
    {
        try
        {
            // For UI-M1, we'll start with mock data since SIM-CAT-M2 isn't implemented yet
            return Task.FromResult(GetMockCatalogs());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get catalogs");
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
            _logger.LogError(ex, "Failed to get catalog {CatalogId}", catalogId);
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
    private readonly HttpClient _httpClient;
    private readonly ILogger<FlowTimeSimService> _logger;

    public FlowTimeSimService(HttpClient httpClient, ILogger<FlowTimeSimService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<SimulationRunResult> RunSimulationAsync(SimulationRunRequest request)
    {
        try
        {
            // For UI-M1, we'll simulate the run and return mock results that resemble real FlowTime output
            // This will be replaced with actual HTTP calls to FlowTime-Sim service
            await Task.Delay(2000); // Simulate processing time

            var runId = $"sim_{DateTime.UtcNow:yyyyMMddTHHmmssZ}_{Guid.NewGuid().ToString("N")[..8]}";
            var startTime = DateTime.UtcNow.AddSeconds(-2);
            var endTime = DateTime.UtcNow;
            
            return new SimulationRunResult
            {
                RunId = runId,
                Status = "completed",
                StartTime = startTime,
                EndTime = endTime,
                ResultsUrl = $"/api/runs/{runId}/index",
                Metadata = new()
                {
                    // Template and execution context
                    ["templateId"] = request.TemplateId,
                    ["catalogId"] = request.CatalogId,
                    ["engineVersion"] = "0.1.0",
                    ["schemaVersion"] = 1,
                    
                    // Grid configuration (typical for FlowTime simulations)
                    ["grid.bins"] = 24,
                    ["grid.binMinutes"] = 60,
                    ["grid.timezone"] = "UTC",
                    
                    // Mock simulation results that would come from FlowTime engine
                    ["series.count"] = 3,
                    ["series.names"] = new[] { "demand@DEMAND@DEFAULT", "served@SERVED@DEFAULT", "capacity@CAPACITY@DEFAULT" },
                    
                    // Mock statistical results
                    ["stats.totalDemand"] = GetMockStatistic("totalDemand", request),
                    ["stats.avgThroughput"] = GetMockStatistic("avgThroughput", request),
                    ["stats.utilizationRate"] = GetMockStatistic("utilizationRate", request),
                    ["stats.peakTime"] = "14:00",
                    
                    // Mock time series summary
                    ["timeSeries.demand.min"] = GetMockStatistic("demandMin", request),
                    ["timeSeries.demand.max"] = GetMockStatistic("demandMax", request),
                    ["timeSeries.demand.avg"] = GetMockStatistic("demandAvg", request),
                    
                    // Performance metrics
                    ["performance.executionTimeMs"] = 1847,
                    ["performance.memoryUsageMB"] = 45.2,
                    
                    // Model information
                    ["model.nodeCount"] = GetMockNodeCount(request),
                    ["model.edgeCount"] = GetMockEdgeCount(request),
                    
                    // Warning/info messages
                    ["warnings"] = new string[0], // No warnings in successful mock run
                    ["info.message"] = $"Successfully simulated {request.TemplateId} scenario"
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to run simulation");
            return new SimulationRunResult
            {
                RunId = $"failed_{Guid.NewGuid().ToString("N")[..8]}",
                Status = "failed",
                StartTime = DateTime.UtcNow,
                ErrorMessage = ex.Message
            };
        }
    }

    private static object GetMockStatistic(string statType, SimulationRunRequest request)
    {
        var random = new Random(request.TemplateId.GetHashCode());
        
        return statType switch
        {
            "totalDemand" => random.Next(1000, 5000),
            "avgThroughput" => Math.Round(random.NextDouble() * 50 + 10, 2),
            "utilizationRate" => Math.Round(random.NextDouble() * 0.4 + 0.6, 3), // 60-100%
            "demandMin" => random.Next(5, 15),
            "demandMax" => random.Next(80, 120),
            "demandAvg" => Math.Round(random.NextDouble() * 30 + 40, 1),
            _ => random.Next(1, 100)
        };
    }
    
    private static int GetMockNodeCount(SimulationRunRequest request)
    {
        return request.TemplateId switch
        {
            "transportation-basic" => 4,
            "supply-chain-multi-tier" => 8,
            "manufacturing-flow" => 6,
            _ => 3
        };
    }
    
    private static int GetMockEdgeCount(SimulationRunRequest request)
    {
        var nodeCount = GetMockNodeCount(request);
        return nodeCount - 1 + new Random(request.TemplateId.GetHashCode()).Next(0, 3);
    }

    public async Task<SimulationStatus> GetRunStatusAsync(string runId)
    {
        try
        {
            // Mock implementation for UI-M1
            await Task.Delay(100);
            return new SimulationStatus
            {
                RunId = runId,
                Status = "completed",
                Progress = 100,
                Message = "Simulation completed successfully"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get run status for {RunId}", runId);
            throw;
        }
    }
}
