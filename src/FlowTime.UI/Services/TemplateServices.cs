namespace FlowTime.UI.Services;

public interface ITemplateService
{
    Task<List<TemplateInfo>> GetTemplatesAsync();
    Task<TemplateInfo?> GetTemplateAsync(string templateId);
    Task<JsonSchema> GetTemplateSchemaAsync(string templateId);
}

public interface ICatalogService
{
    Task<List<CatalogInfo>> GetCatalogsAsync();
    Task<CatalogInfo?> GetCatalogAsync(string catalogId);
}

public interface IFlowTimeSimService
{
    Task<SimulationRunResult> RunSimulationAsync(SimulationRunRequest request);
    Task<SimulationStatus> GetRunStatusAsync(string runId);
    Task<string> GenerateModelYamlAsync(SimulationRunRequest request);
}

public class TemplateInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public string Version { get; set; } = "1.0";
    public JsonSchema? ParameterSchema { get; set; }
}

public class CatalogInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public int NodeCount { get; set; }
    public List<string> Capabilities { get; set; } = new();
}

public class JsonSchema
{
    public string Type { get; set; } = "object";
    public Dictionary<string, JsonSchemaProperty> Properties { get; set; } = new();
    public List<string> Required { get; set; } = new();
    public string? Title { get; set; }
    public string? Description { get; set; }
}

public class JsonSchemaProperty
{
    public string Type { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? Description { get; set; }
    public object? Default { get; set; }
    public double? Minimum { get; set; }
    public double? Maximum { get; set; }
    public List<object>? Enum { get; set; }
    public JsonSchema? Items { get; set; }
    public Dictionary<string, JsonSchemaProperty>? Properties { get; set; }
}

public class SimulationRunRequest
{
    public string TemplateId { get; set; } = string.Empty;
    public string CatalogId { get; set; } = string.Empty;
    public Dictionary<string, object> Parameters { get; set; } = new();
}

public class SimulationRunResult
{
    public string RunId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string? ResultsUrl { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
    public string? ErrorMessage { get; set; }
}

public class SimulationStatus
{
    public string RunId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int Progress { get; set; }
    public string? Message { get; set; }
}
