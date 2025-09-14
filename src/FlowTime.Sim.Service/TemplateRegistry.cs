using FlowTime.Sim.Core;

namespace FlowTime.Sim.Service;

public static class TemplateRegistry
{
    // Categorized templates for different educational purposes
    private static readonly TemplateDef[] Templates =
    [
        // === THEORETICAL TEMPLATES ===
        // Purpose: Teaching simulation fundamentals and mathematical concepts
        new(
            Id: "const-quick",
            Title: "Constant Arrivals (Quick Demo)",
            Description: "Three-bin constant arrivals demo (values 1,2,3) - perfect for understanding basic simulation mechanics",
            Category: "theoretical",
            Tags: ["beginner", "constant", "deterministic", "quick"],
            Yaml: """schemaVersion: 1\nrng: pcg\nseed: 42\ngrid:\n  bins: 3\n  binMinutes: 60\narrivals:\n  kind: const\n  values: [1,2,3]\nroute:\n  id: COMP_A\n"""
        ),
        new(
            Id: "poisson-demo",
            Title: "Poisson Arrivals (Stochastic Demo)",
            Description: "Single-rate Poisson arrivals (λ=5) over 4 bins - demonstrates stochastic arrival patterns",
            Category: "theoretical", 
            Tags: ["beginner", "poisson", "stochastic", "mathematical"],
            Yaml: """schemaVersion: 1\nrng: pcg\nseed: 123\ngrid:\n  bins: 4\n  binMinutes: 30\narrivals:\n  kind: poisson\n  rate: 5\nroute:\n  id: COMP_A\n"""
        ),
        
        // === DOMAIN TEMPLATES ===
        // Purpose: Real-world system modeling and business education
        new(
            Id: "it-system-microservices",
            Title: "IT System with Microservices",
            Description: "Modern web application with request queues, load balancer, auth service, and database bottlenecks",
            Category: "domain",
            Tags: ["intermediate", "microservices", "web-scale", "modern", "it-systems"],
            Yaml: """
schemaVersion: 1
rng: pcg
seed: {{seed}}
grid:
  bins: {{bins}}
  binMinutes: {{binMinutes}}
arrivals:
  kind: poisson
  rate: {{requestRate}}
route:
  id: LOAD_BALANCER
""",
            Parameters: [
                new("requestRate", ParameterType.Number, "Request Rate (req/min)", "Incoming API requests per minute", 100.0, 10.0, 10000.0),
                new("bins", ParameterType.Integer, "Time Bins", "Number of time periods to simulate", 6, 3, 24),
                new("binMinutes", ParameterType.Integer, "Minutes per Bin", "Duration of each time period", 60, 15, 480),
                new("seed", ParameterType.Integer, "Random Seed", "Seed for reproducible results", 789, 1, 999999)
            ]
        ),
        new(
            Id: "manufacturing-basic",
            Title: "Manufacturing Production Line",
            Description: "Basic production line with workstations, showing throughput and bottleneck identification",
            Category: "domain",
            Tags: ["beginner", "manufacturing", "production", "bottleneck"],
            Yaml: """
schemaVersion: 1
rng: pcg
seed: {{seed}}
grid:
  bins: {{bins}}
  binMinutes: {{binMinutes}}
arrivals:
  kind: const
  values: {{productionSchedule}}
route:
  id: STATION_1
""",
            Parameters: [
                new("productionRate", ParameterType.Number, "Production Rate (units/hour)", "Target production rate per hour", 12.0, 1.0, 100.0),
                new("bins", ParameterType.Integer, "Production Shifts", "Number of production time periods", 8, 4, 24),
                new("binMinutes", ParameterType.Integer, "Minutes per Shift", "Duration of each production period", 30, 15, 120),
                new("seed", ParameterType.Integer, "Random Seed", "Seed for reproducible results", 456, 1, 999999)
            ]
        ),
        new(
            Id: "transportation-basic", 
            Title: "Transportation Network",
            Description: "Simple transportation flow with demand and capacity constraints, ideal for logistics learning",
            Category: "domain",
            Tags: ["beginner", "transportation", "logistics", "capacity"],
            Yaml: """
schemaVersion: 1
rng: pcg
seed: {{seed}}
grid:
  bins: {{bins}}
  binMinutes: {{binMinutes}}
arrivals:
  kind: poisson
  rate: {{demandRate}}
route:
  id: HUB_A
""",
            Parameters: [
                new("demandRate", ParameterType.Number, "Demand Rate (items/hour)", "Rate of items entering the transportation network", 25.0, 5.0, 500.0),
                new("bins", ParameterType.Integer, "Time Periods", "Number of time periods to simulate", 5, 3, 12),
                new("binMinutes", ParameterType.Integer, "Minutes per Period", "Duration of each time period", 120, 30, 480),
                new("seed", ParameterType.Integer, "Random Seed", "Seed for reproducible results", 321, 1, 999999)
            ]
        )
    ];

    public static IEnumerable<object> List(string? category = null) => 
        (category == null ? Templates : Templates.Where(t => string.Equals(t.Category, category, StringComparison.OrdinalIgnoreCase)))
        .Select(t => new
        {
            t.Id,
            t.Title,
            t.Description,
            t.Category,
            t.Tags,
            Parameters = t.Parameters?.Select(p => new
            {
                p.Name,
                Type = p.Type.ToString().ToLowerInvariant(),
                p.Title,
                p.Description,
                p.DefaultValue,
                p.Minimum,
                p.Maximum,
                p.AllowedValues
            }).ToArray() ?? Array.Empty<object>(),
            // Surface minimal knobs to UI without full parse cost.
            Preview = ExtractPreview(t.Yaml)
        });

    public static IEnumerable<string> GetCategories() => 
        Templates.Select(t => t.Category).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(c => c);

    public static TemplateDef? Get(string id) => Templates.FirstOrDefault(t => string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase));

    public static string GenerateScenario(string templateId, Dictionary<string, object> parameters)
    {
        var template = Get(templateId);
        if (template == null)
            throw new ArgumentException($"Template '{templateId}' not found");

        // Validate parameters
        if (template.Parameters != null)
        {
            foreach (var param in template.Parameters)
            {
                ValidateParameter(param, parameters.GetValueOrDefault(param.Name, param.DefaultValue));
            }
        }

        // Substitute parameters in YAML template
        var yaml = template.Yaml;
        
        // Handle special case for manufacturing production schedule
        if (templateId == "manufacturing-basic" && parameters.ContainsKey("productionRate") && parameters.ContainsKey("bins"))
        {
            var rate = Convert.ToDouble(parameters["productionRate"]);
            var bins = Convert.ToInt32(parameters["bins"]);
            var schedule = string.Join(",", Enumerable.Repeat(rate.ToString("F1"), bins));
            yaml = yaml.Replace("{{productionSchedule}}", $"[{schedule}]");
        }

        // Standard parameter substitution
        foreach (var param in parameters)
        {
            var placeholder = $"{{{{{param.Key}}}}}";
            yaml = yaml.Replace(placeholder, param.Value?.ToString() ?? "");
        }

        // Fill in any remaining placeholders with defaults
        if (template.Parameters != null)
        {
            foreach (var param in template.Parameters)
            {
                var placeholder = $"{{{{{param.Name}}}}}";
                if (yaml.Contains(placeholder))
                {
                    yaml = yaml.Replace(placeholder, param.DefaultValue?.ToString() ?? "");
                }
            }
        }

        return yaml.Trim();
    }

    private static void ValidateParameter(TemplateParameter param, object value)
    {
        // Type validation
        switch (param.Type)
        {
            case ParameterType.Number:
                if (!IsNumeric(value))
                    throw new ArgumentException($"Parameter '{param.Name}' must be a number");
                var numValue = ToDouble(value);
                if (param.Minimum != null && numValue < Convert.ToDouble(param.Minimum))
                    throw new ArgumentException($"Parameter '{param.Name}' must be at least {param.Minimum}");
                if (param.Maximum != null && numValue > Convert.ToDouble(param.Maximum))
                    throw new ArgumentException($"Parameter '{param.Name}' must be at most {param.Maximum}");
                break;
                
            case ParameterType.Integer:
                if (!IsInteger(value))
                    throw new ArgumentException($"Parameter '{param.Name}' must be an integer");
                var intValue = ToInt32(value);
                if (param.Minimum != null && intValue < Convert.ToInt32(param.Minimum))
                    throw new ArgumentException($"Parameter '{param.Name}' must be at least {param.Minimum}");
                if (param.Maximum != null && intValue > Convert.ToInt32(param.Maximum))
                    throw new ArgumentException($"Parameter '{param.Name}' must be at most {param.Maximum}");
                break;
                
            case ParameterType.String:
                if (param.AllowedValues != null && !param.AllowedValues.Contains(value?.ToString()))
                    throw new ArgumentException($"Parameter '{param.Name}' must be one of: {string.Join(", ", param.AllowedValues)}");
                break;
        }
    }

    private static bool IsNumeric(object value) => 
        value is int or long or float or double or decimal ||
        (value is string s && double.TryParse(s, out _)) ||
        (value is System.Text.Json.JsonElement je && je.ValueKind == System.Text.Json.JsonValueKind.Number);

    private static bool IsInteger(object value) => 
        value is int or long ||
        (value is string s && int.TryParse(s, out _)) ||
        (value is System.Text.Json.JsonElement je && je.ValueKind == System.Text.Json.JsonValueKind.Number && je.TryGetInt32(out _));

    private static double ToDouble(object value) => value switch
    {
        double d => d,
        float f => f,
        int i => i,
        long l => l,
        decimal m => (double)m,
        string s when double.TryParse(s, out var parsed) => parsed,
        System.Text.Json.JsonElement je when je.ValueKind == System.Text.Json.JsonValueKind.Number => je.GetDouble(),
        _ => Convert.ToDouble(value)
    };

    private static int ToInt32(object value) => value switch
    {
        int i => i,
        long l => (int)l,
        string s when int.TryParse(s, out var parsed) => parsed,
        System.Text.Json.JsonElement je when je.ValueKind == System.Text.Json.JsonValueKind.Number => je.GetInt32(),
        _ => Convert.ToInt32(value)
    };

    private static object ExtractPreview(string yaml)
    {
        try
        {
            var spec = SimulationSpecLoader.LoadFromString(yaml);
            return new
            {
                bins = spec.grid?.bins,
                binMinutes = spec.grid?.binMinutes,
                arrivals = spec.arrivals?.kind,
                route = spec.route?.id
            };
        }
        catch
        {
            return new { bins = (int?)null, binMinutes = (int?)null, arrivals = (string?)null, route = (string?)null };
        }
    }
}

public sealed record TemplateDef(
    string Id, 
    string Title, 
    string Description, 
    string Category,
    string[] Tags,
    string Yaml,
    TemplateParameter[]? Parameters = null
);

public sealed record TemplateParameter(
    string Name,
    ParameterType Type,
    string Title,
    string Description,
    object DefaultValue,
    object? Minimum = null,
    object? Maximum = null,
    string[]? AllowedValues = null
);

public enum ParameterType
{
    Number,
    Integer,
    String,
    Boolean,
    Enum
}