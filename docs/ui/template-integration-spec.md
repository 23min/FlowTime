# FlowTime UI: Template Integration Specification

> ⚠️ **SCHEMA MIGRATION IN PROGRESS**  
> This document contains legacy `binMinutes` references.  
> **Current Implementation**: Use `grid: { bins, binSize, binUnit }` format.  
> **See**: `docs/schemas/template-schema.md` for authoritative schema.  
> **Status**: Documentation update pending post-UI-M-02.09.

## Overview

This specification defines how the FlowTime UI integrates with FlowTime-Sim's parameterized template system. Templates provide a bridge between Learning Mode (guided tutorials) and Expert Mode (raw YAML editing) by offering parameterized scenarios that generate custom YAML.

## Integration with Existing UI Architecture

### Current Structure
- **Learning Mode** (`/learn/`) - Educational guided experiences
- **Expert Mode** (`/templates`, `/api-demo`, `/features`) - Advanced simulation configuration
- **Dual API Support** - Demo Mode (static content) vs API Mode (FlowTime-Sim integration)

### Template System Role
Templates enhance the Expert Interface by providing:
- **Parameterized Scenarios** - Configure realistic simulations without YAML syntax
- **Domain Patterns** - IT Systems, Manufacturing, Transportation templates
- **Parameter Validation** - Schema-driven form generation with validation
- **YAML Generation** - Convert template + parameters → FlowTime-compatible YAML

## Current Implementation Status

### Already Implemented (`TemplateRunner.razor`)
```csharp
// Template discovery and selection
public async Task<List<TemplateInfo>> GetTemplatesAsync()

// Parameter configuration and validation  
public async Task<JsonSchema> GetTemplateSchemaAsync(string templateId)

// Template execution (generates YAML and runs simulation)
public async Task<SimulationRunResult> RunSimulationAsync(SimulationRunRequest request)

// Dual mode support (Demo vs API)
FeatureFlagService.UseDemoMode // Toggles between static and FlowTime-Sim API
```

### Existing Components
- **TemplateGallery.razor** - Template selection with filtering
- **DynamicParameterForm** - Schema-driven parameter input forms
- **CatalogPicker** - System catalog selection
- **SimulationResults** - Results display and export

### Core Services
```csharp
// Template discovery and management
public interface ITemplateService
{
    Task<List<TemplateInfo>> GetTemplatesAsync(string? category = null);
    Task<JsonSchema> GetTemplateSchemaAsync(string templateId);
    Task<string> GenerateScenarioAsync(string templateId, Dictionary<string, object> parameters);
}

// System catalog management for template execution
public interface ICatalogService  
{
    Task<List<CatalogInfo>> GetCatalogsAsync();
    Task<CatalogInfo?> GetCatalogAsync(string catalogId);
}

// Template execution and result handling
public interface ISimulationService
{
    Task<SimulationRunResult> RunSimulationAsync(SimulationRunRequest request);
}
```

## Enhanced Template Integration

### Template Workflow Integration
```
Expert Mode Template Flow:
1. User navigates to /templates (existing TemplateRunner.razor)
2. Template gallery displays available templates by category
3. User selects template → Dynamic parameter form generates from schema
4. User configures parameters with real-time validation
5. User selects system catalog (existing CatalogPicker)
6. Template + parameters + catalog → Generate YAML → Execute simulation
7. Results display with export options (existing SimulationResults)
```

### Template States in Blazor Components
```csharp
public enum TemplateState
{
    Loading,           // Fetching templates from FlowTime-Sim API
    Selection,         // Template gallery browsing
    Configuration,     // Parameter form active
    Validation,        // Client/server parameter validation
    Generating,        // Converting template to YAML
    Ready,            // YAML generated, ready to simulate
    Executing,        // Simulation running
    Complete,         // Results available
    Error            // Template/validation/execution errors
}
```

## Blazor Component Enhancement Plan

### Enhanced TemplateRunner.razor Structure
```razor
@page "/templates"
@using FlowTime.UI.Services
@inject ITemplateService TemplateService
@inject ICatalogService CatalogService
@inject ISimulationService SimulationService

<div class="template-runner">
    @if (currentState == TemplateState.Selection)
    {
        <TemplateGallery Templates="@templates" 
                        OnTemplateSelected="@HandleTemplateSelected"
                        SelectedCategory="@selectedCategory" />
    }
    else if (currentState == TemplateState.Configuration)
    {
        <ParameterConfiguration Template="@selectedTemplate"
                               Schema="@templateSchema"
                               OnParametersChanged="@HandleParametersChanged"
                               ValidationErrors="@validationErrors" />
                               
        <CatalogPicker OnCatalogSelected="@HandleCatalogSelected" />
        
        <div class="actions">
            <button @onclick="@GenerateAndRun" disabled="@(!CanExecute)">
                Generate & Run
            </button>
        </div>
    }
    else if (currentState == TemplateState.Complete)
    {
        <SimulationResults Result="@simulationResult" />
    }
</div>
```

### Enhanced Template Gallery Component
```csharp
public partial class TemplateGallery : ComponentBase
{
    [Parameter] public List<TemplateInfo> Templates { get; set; } = new();
    [Parameter] public EventCallback<TemplateInfo> OnTemplateSelected { get; set; }
    [Parameter] public string? SelectedCategory { get; set; }
    
    private string searchFilter = "";
    private readonly string[] categories = { "theoretical", "domain" };
    
    private IEnumerable<TemplateInfo> FilteredTemplates =>
        Templates.Where(t => 
            (SelectedCategory == null || t.Category == SelectedCategory) &&
            (string.IsNullOrEmpty(searchFilter) || 
             t.Title.Contains(searchFilter, StringComparison.OrdinalIgnoreCase)));
}
```

### Dynamic Parameter Form Enhancement
```csharp
public partial class DynamicParameterForm : ComponentBase
{
    [Parameter] public JsonSchema Schema { get; set; } = null!;
    [Parameter] public Dictionary<string, object> Values { get; set; } = new();
    [Parameter] public EventCallback<Dictionary<string, object>> OnValuesChanged { get; set; }
    [Parameter] public Dictionary<string, string> ValidationErrors { get; set; } = new();
    
    private async Task HandleParameterChanged(string parameterName, object value)
    {
        Values[parameterName] = value;
        await OnValuesChanged.InvokeAsync(Values);
        
        // Real-time validation
        await ValidateParameter(parameterName, value);
    }
    
    private async Task ValidateParameter(string name, object value)
    {
        var parameter = Schema.Properties[name];
        var error = ValidateParameterValue(parameter, value);
        
        if (error != null)
            ValidationErrors[name] = error;
        else
            ValidationErrors.Remove(name);
    }
}
```

### Template Parameter Input Components
```csharp
// Number parameter component
public partial class NumberParameterInput : ComponentBase
{
    [Parameter] public JsonSchema ParameterSchema { get; set; } = null!;
    [Parameter] public object Value { get; set; } = null!;
    [Parameter] public EventCallback<object> OnValueChanged { get; set; }
    [Parameter] public string? ValidationError { get; set; }
    
    private async Task HandleInputChanged(ChangeEventArgs e)
    {
        if (double.TryParse(e.Value?.ToString(), out var numValue))
        {
            await OnValueChanged.InvokeAsync(numValue);
        }
    }
}

// Integer parameter component  
public partial class IntegerParameterInput : ComponentBase
{
    [Parameter] public JsonSchema ParameterSchema { get; set; } = null!;
    [Parameter] public object Value { get; set; } = null!;
    [Parameter] public EventCallback<object> OnValueChanged { get; set; }
    [Parameter] public string? ValidationError { get; set; }
}

// String parameter component
public partial class StringParameterInput : ComponentBase
{
    [Parameter] public JsonSchema ParameterSchema { get; set; } = null!;
    [Parameter] public object Value { get; set; } = null!;
    [Parameter] public EventCallback<object> OnValueChanged { get; set; }
    [Parameter] public string? ValidationError { get; set; }
}
```

## Service Integration Patterns

### Template Service Implementation
```csharp
public class TemplateService : ITemplateService
{
    private readonly HttpClient httpClient;
    private readonly IConfiguration configuration;
    
    public async Task<List<TemplateInfo>> GetTemplatesAsync(string? category = null)
    {
        if (FeatureFlagService.UseDemoMode)
        {
            return GetStaticDemoTemplates(category);
        }
        
        var endpoint = category != null ? $"/api/templates?category={category}" : "/api/templates";
        var response = await httpClient.GetAsync(endpoint);
        response.EnsureSuccessStatusCode();
        
        return await response.Content.ReadFromJsonAsync<List<TemplateInfo>>() ?? new();
    }
    
    public async Task<JsonSchema> GetTemplateSchemaAsync(string templateId)
    {
        if (FeatureFlagService.UseDemoMode)
        {
            return GetStaticTemplateSchema(templateId);
        }
        
        var response = await httpClient.GetAsync($"/api/templates/{templateId}/schema");
        response.EnsureSuccessStatusCode();
        
        return await response.Content.ReadFromJsonAsync<JsonSchema>() ?? new();
    }
    
    public async Task<string> GenerateScenarioAsync(string templateId, Dictionary<string, object> parameters)
    {
        if (FeatureFlagService.UseDemoMode)
        {
            return GenerateStaticScenario(templateId, parameters);
        }
        
        var request = new { templateId, parameters };
        var response = await httpClient.PostAsJsonAsync("/api/templates/generate", request);
        response.EnsureSuccessStatusCode();
        
        return await response.Content.ReadAsStringAsync();
    }
}

### Template State Management
```csharp
public class TemplateStateManager
{
    private TemplateState currentState = TemplateState.Loading;
    private TemplateInfo? selectedTemplate;
    private JsonSchema? templateSchema;
    private Dictionary<string, object> parameters = new();
    private Dictionary<string, string> validationErrors = new();
    
    public async Task LoadTemplatesAsync(string? category = null)
    {
        currentState = TemplateState.Loading;
        try
        {
            var templates = await templateService.GetTemplatesAsync(category);
            currentState = TemplateState.Selection;
            StateHasChanged();
        }
        catch (Exception ex)
        {
            currentState = TemplateState.Error;
            // Handle error...
        }
    }
    
    public async Task SelectTemplateAsync(TemplateInfo template)
    {
        selectedTemplate = template;
        currentState = TemplateState.Configuration;
        
        templateSchema = await templateService.GetTemplateSchemaAsync(template.Id);
        InitializeDefaultParameters();
        StateHasChanged();
    }
    
    private void InitializeDefaultParameters()
    {
        parameters.Clear();
        validationErrors.Clear();
        
        if (templateSchema?.Properties != null)
        {
            foreach (var prop in templateSchema.Properties)
            {
                if (prop.Value.Default != null)
                {
                    parameters[prop.Key] = prop.Value.Default;
                }
            }
        }
    }
}
```

## Parameter Validation

### Client-Side Validation Logic
```csharp
public static class ParameterValidator
{
    public static string? ValidateParameterValue(JsonSchema parameterSchema, object value)
    {
        if (parameterSchema.Type == "number")
        {
            if (!double.TryParse(value?.ToString(), out var numValue))
                return $"{parameterSchema.Title} must be a number";
                
            if (parameterSchema.Minimum.HasValue && numValue < parameterSchema.Minimum.Value)
                return $"{parameterSchema.Title} must be at least {parameterSchema.Minimum.Value}";
                
            if (parameterSchema.Maximum.HasValue && numValue > parameterSchema.Maximum.Value)
                return $"{parameterSchema.Title} must be at most {parameterSchema.Maximum.Value}";
        }
        
        if (parameterSchema.Type == "integer")
        {
            if (!int.TryParse(value?.ToString(), out var intValue))
                return $"{parameterSchema.Title} must be a whole number";
                
            if (parameterSchema.Minimum.HasValue && intValue < parameterSchema.Minimum.Value)
                return $"{parameterSchema.Title} must be at least {parameterSchema.Minimum.Value}";
                
            if (parameterSchema.Maximum.HasValue && intValue > parameterSchema.Maximum.Value)
                return $"{parameterSchema.Title} must be at most {parameterSchema.Maximum.Value}";
        }
        
        if (parameterSchema.Type == "string")
        {
            var strValue = value?.ToString() ?? "";
            if (parameterSchema.MinLength.HasValue && strValue.Length < parameterSchema.MinLength.Value)
                return $"{parameterSchema.Title} must be at least {parameterSchema.MinLength.Value} characters";
                
            if (parameterSchema.MaxLength.HasValue && strValue.Length > parameterSchema.MaxLength.Value)
                return $"{parameterSchema.Title} must be at most {parameterSchema.MaxLength.Value} characters";
        }
        
        return null;
    }
}
```

### Error Display Component
```razor
@if (!string.IsNullOrEmpty(ValidationError))
{
    <div class="validation-error" role="alert">
        <i class="fas fa-exclamation-triangle"></i>
        @ValidationError
    </div>
}

@code {
    [Parameter] public string? ValidationError { get; set; }
}
```

## Template Integration Execution Flow

### Complete Template-to-Simulation Workflow
```csharp
public class TemplateExecutionService
{
    public async Task<SimulationRunResult> ExecuteTemplateAsync(
        string templateId, 
        Dictionary<string, object> parameters,
        string catalogId)
    {
        // 1. Generate YAML from template + parameters
        var generatedYaml = await templateService.GenerateScenarioAsync(templateId, parameters);
        
        // 2. Create simulation request
        var request = new SimulationRunRequest
        {
            ModelYaml = generatedYaml,
            CatalogId = catalogId,
            OutputFormat = "csv" // or json based on user preference
        };
        
        // 3. Execute simulation via FlowTime API
        var result = await simulationService.RunSimulationAsync(request);
        
        return result;
    }
}
```

### Demo Mode vs API Mode Integration
```csharp
public async Task<string> GenerateScenarioAsync(string templateId, Dictionary<string, object> parameters)
{
    if (featureFlagService.UseDemoMode)
    {
        // Use static template processing for demo mode
        return await staticTemplateService.ProcessTemplateAsync(templateId, parameters);
    }
    else
    {
        // Use FlowTime-Sim API for full template processing
        return await apiTemplateService.GenerateScenarioAsync(templateId, parameters);
    }
}
```

## Template Schema Integration

### JsonSchema Extension for FlowTime Templates
```csharp
public class FlowTimeTemplateSchema : JsonSchema
{
    public string? Category { get; set; }  // "theoretical" | "domain"
    public List<string> Tags { get; set; } = new();
    public string? Description { get; set; }
    public string? Author { get; set; }
    public TemplateComplexity Complexity { get; set; }
    public Dictionary<string, ParameterGroupInfo> ParameterGroups { get; set; } = new();
}

public class ParameterGroupInfo
{
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public bool DefaultExpanded { get; set; } = true;
    public List<string> ParameterNames { get; set; } = new();
}

public enum TemplateComplexity
{
    Beginner,    // 1-3 parameters, basic concepts
    Intermediate,// 4-8 parameters, moderate complexity  
    Advanced     // 9+ parameters, complex scenarios
}
```

### Template Metadata Integration
```csharp
public class TemplateInfo
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Category { get; set; } = "";
    public List<string> Tags { get; set; } = new();
    public TemplateComplexity Complexity { get; set; }
    public int ParameterCount { get; set; }
    public bool HasDefaults { get; set; }
    public string PreviewImageUrl { get; set; } = "";
}
```

## Future Enhancement Opportunities

### Template Preset System
```csharp
public class TemplatePreset
{
    public string Id { get; set; } = "";
    public string TemplateId { get; set; } = "";
    public string Name { get; set; } = "";
    public Dictionary<string, object> Parameters { get; set; } = new();
    public string CreatedBy { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

// Save/Load presets for frequently used parameter combinations
public interface ITemplatePresetService
{
    Task SavePresetAsync(TemplatePreset preset);
    Task<List<TemplatePreset>> GetPresetsAsync(string templateId);
    Task<TemplatePreset?> LoadPresetAsync(string presetId);
}
```

### Template History and Favorites
```csharp
public class TemplateHistory
{
    public string TemplateId { get; set; } = "";
    public Dictionary<string, object> Parameters { get; set; } = new();
    public DateTime UsedAt { get; set; }
    public bool IsFavorite { get; set; }
}
```

This specification provides the foundation for enhancing the existing FlowTime UI template system with deeper FlowTime-Sim integration, maintaining the dual-mode architecture while providing rich template-based simulation configuration capabilities.
