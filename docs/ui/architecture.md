# FlowTime UI Architecture

**Version:** 1.0  
**Audience:** UI architects, senior developers, technical leads  
**Purpose:** Define architectural patterns, component structure, and design principles for FlowTime UI  

---

## 1. Architectural Principles

### 1.1 API-First Design
- All UI functionality maps to FlowTime API endpoints
- No client-side business logic that can't be reproduced via API
- State synchronization between UI and backend is explicit

### 1.2 Component-Based Architecture
- Modular, reusable components with clear interfaces
- Single Responsibility Principle for each component
- Composition over inheritance for complex UI elements

### 1.3 Performance-First Rendering
- Canvas-based rendering for large datasets (>1000 points)
- Virtual scrolling for large lists
- Lazy loading of non-critical components

### 1.4 Schema-Driven Development
- UI adapts to API schema changes automatically
- JSON Schema validation for all data structures
- Type-safe data binding throughout the application

---

## 2. System Architecture

### 2.1 High-Level Architecture
```
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   Browser       │    │   FlowTime.UI   │    │  FlowTime.API   │
│                 │    │                 │    │                 │
│ ┌─────────────┐ │    │ ┌─────────────┐ │    │ ┌─────────────┐ │
│ │ Charts      │◄┼────┼►│ Components  │ │    │ │ /run        │ │
│ │ (MudBlazor) │ │    │ │             │ │    │ │ /graph      │ │
│ └─────────────┘ │    │ └─────────────┘ │    │ │ /scenarios  │ │
│                 │    │ ┌─────────────┐ │    │ └─────────────┘ │
│ ┌─────────────┐ │    │ │ Services    │◄┼────┼─ HTTP/SignalR   │
│ │ Graph       │◄┼────┼►│             │ │    │                 │
│ │ (PixiJS)    │ │    │ └─────────────┘ │    └─────────────────┘
│ └─────────────┘ │    │ ┌─────────────┐ │
│                 │    │ │ State Mgmt  │ │
│ ┌─────────────┐ │    │ │             │ │
│ │ Editors     │◄┼────┼►│             │ │
│ │ (Monaco)    │ │    │ └─────────────┘ │
│ └─────────────┘ │    └─────────────────┘
└─────────────────┘
```

### 2.2 Component Hierarchy
```
App
├── Layout (navigation, header, footer)
├── Pages
│   ├── GraphExplorer
│   │   ├── DAGVisualizer
│   │   ├── NodeInspector
│   │   └── TimeScrubber
│   ├── RunManager
│   │   ├── ModelEditor
│   │   ├── RunTrigger
│   │   └── ResultsViewer
│   ├── ScenarioComposer
│   │   ├── OverlayEditor
│   │   ├── PMFAttacher
│   │   └── ComparisonView
│   ├── PMFLibrary
│   │   ├── PMFList
│   │   ├── PMFSearch
│   │   └── PMFVersioning
│   └── PMFEditor
│       ├── HistogramCanvas
│       ├── StatsPanel
│       └── TemplateSelector
└── Shared
    ├── Charts (TimeSeriesChart, HistogramChart)
    ├── Dialogs (SaveDialog, ConfirmDialog)
    └── Utils (LoadingSpinner, ErrorBoundary)
```

---

## 3. Component Architecture Patterns

### 3.1 Smart vs Dumb Components

**Smart Components (Containers):**
- Manage application state
- Handle API communication
- Contain business logic
- Examples: `RunManager`, `ScenarioComposer`

```csharp
@page "/runs"
@inject IRunService RunService
@inject IStateContainer StateContainer

public partial class RunManager : ComponentBase
{
    private List<RunSummary> runs = new();
    
    protected override async Task OnInitializedAsync()
    {
        runs = await RunService.GetRunsAsync();
        StateContainer.OnChange += StateHasChanged;
    }
}
```

**Dumb Components (Presentational):**
- Pure rendering logic
- No direct API calls
- Receive data via parameters
- Examples: `TimeSeriesChart`, `PMFHistogram`

```csharp
@typeparam TData

<div class="chart-container">
    @if (Data?.Any() == true)
    {
        <canvas @ref="chartCanvas"></canvas>
    }
</div>

@code {
    [Parameter] public IEnumerable<TData> Data { get; set; } = [];
    [Parameter] public EventCallback<TData> OnPointClick { get; set; }
    
    // Pure rendering logic only
}
```

### 3.2 State Management Pattern

**Centralized State:**
```csharp
public class AppStateContainer
{
    private RunSummary? _currentRun;
    private PMF? _activePMF;
    
    public RunSummary? CurrentRun 
    { 
        get => _currentRun;
        set 
        {
            _currentRun = value;
            NotifyStateChanged();
        }
    }
    
    public event Action? OnChange;
    private void NotifyStateChanged() => OnChange?.Invoke();
}
```

**Component State Subscription:**
```csharp
@implements IDisposable
@inject AppStateContainer StateContainer

protected override void OnInitialized()
{
    StateContainer.OnChange += StateHasChanged;
}

public void Dispose()
{
    StateContainer.OnChange -= StateHasChanged;
}
```

### 3.3 Error Handling Pattern

**Error Boundary Component:**
```csharp
public class ErrorBoundary : ErrorBoundaryBase
{
    protected override Task OnErrorAsync(Exception exception)
    {
        // Log error
        Logger.LogError(exception, "UI Error");
        
        // Show user-friendly message
        return Task.CompletedTask;
    }
}
```

**API Error Handling:**
```csharp
public async Task<ApiResult<T>> SafeApiCall<T>(Func<Task<T>> apiCall)
{
    try
    {
        var result = await apiCall();
        return ApiResult<T>.Success(result);
    }
    catch (HttpRequestException ex)
    {
        return ApiResult<T>.Error($"Network error: {ex.Message}");
    }
    catch (JsonException ex)
    {
        return ApiResult<T>.Error($"Data format error: {ex.Message}");
    }
}
```

---

## 4. Data Flow Architecture

### 4.1 Request/Response Flow
```
UI Component → Service Layer → HTTP Client → FlowTime API
     ↓              ↓              ↓              ↓
State Update ← Data Mapping ← JSON Response ← API Response
```

### 4.2 Real-time Updates (SignalR)
```
FlowTime API → SignalR Hub → UI Connection → State Update → Component Refresh
```

### 4.3 Caching Strategy
- **API responses**: Cache for 5 minutes with cache invalidation on updates
- **Run artifacts**: Cache indefinitely (immutable once created)
- **PMF definitions**: Cache with versioning support

---

## 5. Rendering Architecture

### 5.1 Chart Rendering Pipeline
```csharp
Data Source → Data Transformation → Chart Configuration → Canvas Rendering
     ↓                  ↓                    ↓                   ↓
Time Series → Downsampling/Binning → MudChart Config → SVG/Canvas Draw
```

### 5.2 Graph Rendering Pipeline
```csharp
Graph Data → Layout Calculation → Sprite Generation → PixiJS Rendering
     ↓              ↓                   ↓                 ↓
DAG Nodes → elkjs Layout → Node/Edge Sprites → Interactive Canvas
```

### 5.3 Performance Optimization Strategies

**Large Dataset Handling:**
- Virtual scrolling for lists >100 items
- Canvas rendering for charts >1000 points
- Progressive loading with pagination

**Memory Management:**
- Dispose chart instances on component destroy
- Implement object pooling for frequently created objects
- Use WeakMap for component references

---

## 6. Integration Patterns

### 6.1 API Integration
```csharp
public interface IFlowTimeApiClient
{
    Task<RunResult> TriggerRunAsync(ModelDefinition model);
    Task<GraphData> GetGraphAsync(string runId);
    Task<SeriesData> GetSeriesAsync(string runId, string seriesId);
}

public class FlowTimeApiClient : IFlowTimeApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;
    
    // Implementation with proper error handling, retries, etc.
}
```

### 6.2 JavaScript Interop
```csharp
@inject IJSRuntime JSRuntime

private async Task InitializeChart()
{
    // MudBlazor charts are native Blazor components, no JS interop needed
    chartData = ProcessTimeSeriesData(rawData);
    StateHasChanged();
}
```

### 6.3 File Handling
```csharp
public async Task<string> UploadModelAsync(IBrowserFile file)
{
    using var stream = file.OpenReadStream();
    var content = await new StreamReader(stream).ReadToEndAsync();
    
    // Validate YAML format
    var model = YamlSerializer.Deserialize<ModelDefinition>(content);
    
    return content;
}
```

---

## 7. Security Architecture

### 7.1 Input Validation
- All user inputs validated on client and server
- YAML/JSON schema validation before API calls
- XSS prevention through proper encoding

### 7.2 API Security
- HTTPS only for production
- API key authentication
- CORS properly configured

### 7.3 Data Sanitization
- Sanitize all user-generated content
- Validate file uploads for malicious content
- Escape HTML in dynamic content

---

## 8. Testing Architecture

### 8.1 Testing Pyramid
```
┌─────────────────────┐
│   E2E Tests         │ ← Few, critical user journeys
├─────────────────────┤
│ Integration Tests   │ ← API integration, component interaction
├─────────────────────┤
│ Unit Tests          │ ← Many, fast, isolated component tests
└─────────────────────┘
```

### 8.2 Component Testing Strategy
```csharp
[Test]
public void TimeSeriesChart_WithValidData_RendersCorrectly()
{
    // Arrange
    var testData = GenerateTestTimeSeries();
    var component = RenderComponent<TimeSeriesChart>(parameters => 
        parameters.Add(p => p.Data, testData));
    
    // Act & Assert
    Assert.That(component.Find("canvas"), Is.Not.Null);
    Assert.That(component.Instance.Data.Count(), Is.EqualTo(testData.Count()));
}
```

---

## 9. Deployment Architecture

### 9.1 Build Pipeline
```
Source Code → Build → Test → Package → Deploy
     ↓           ↓      ↓        ↓        ↓
TypeScript → dotnet → xUnit → Docker → Container
Compilation   build   tests   Image    Registry
```

### 9.2 Environment Configuration
- Development: Mock APIs, debug logging, hot reload
- Staging: Real APIs, structured logging, performance monitoring
- Production: Optimized builds, error tracking, CDN assets

---

## 10. Future Considerations

### 10.1 Scalability
- Consider micro-frontend architecture for large teams
- Implement federated module loading
- Plan for multi-tenant scenarios

### 10.2 Technology Evolution
- Prepare for Blazor WebAssembly migration
- Plan WebGL2 adoption for advanced graphics
- Consider Progressive Web App features

### 10.3 Accessibility
- WCAG 2.1 AA compliance
- Keyboard navigation for all features
- Screen reader support for data visualizations

---

**Related Documents:**
- [Design Specification](design-specification.md) - Detailed component requirements
- [Development Guide](development-guide.md) - Setup and tooling
- [API Integration Guide](api-integration.md) - Backend connectivity patterns
