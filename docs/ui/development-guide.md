# FlowTime UI Development Guide

> **📋 Charter Development**: UI development follows charter-aligned patterns. See [M2.8](../milestones/M02.08.md) for backend APIs and [UI-M02.08.md](../milestones/UI-M02.08.md) for UI implementation. This guide covers setup for [Models|Runs|Artifacts|Learn] charter development.

**Version:** 2.0 (Charter-Aligned)  
**Audience:** UI developers, frontend engineers  
**Purpose:** Setup, tooling, debugging, and development workflows for charter-aligned FlowTime UI development  

---

## 1. Quick Start

### Prerequisites
- .NET 9 SDK
- Node.js 18+ (for UI tooling)
- VS Code with Blazor extensions
- Docker (for local API development)

### Initial Setup
```bash
# Clone and navigate to UI project
cd src/FlowTime.UI

# Restore packages
dotnet restore

# Run in development mode
dotnet run --urls http://localhost:5000

# Or with hot reload
dotnet watch run --urls http://localhost:5000
```

### API Integration Setup
```bash
# Start FlowTime API locally
cd src/FlowTime.API
dotnet run --urls http://localhost:8080

# Or use Docker compose for full stack
docker-compose up flowtime-api
```

---

## 2. Development Environment

### Project Structure
```
src/FlowTime.UI/
├── Components/          # Blazor components
│   ├── Charts/         # Chart rendering components
│   ├── Graph/          # DAG visualization components
│   ├── PMF/            # PMF editor and viewer components
│   └── Scenarios/      # Scenario composition tools
├── Services/           # API clients and business logic
├── Models/             # DTOs and view models  
├── wwwroot/           # Static assets (CSS, JS, images)
├── Pages/             # Blazor pages/routes
└── Shared/            # Layout components
```

### Key Technologies
- **Blazor Server** - Primary UI framework
- **PixiJS** - High-performance graph rendering
- **elkjs** - DAG layout algorithms
- **MudBlazor** - Material Design components with charting
- **Monaco Editor** - YAML/JSON editing
- **SignalR** - Real-time updates

---

## 3. Development Modes

### Mock Mode (Offline Development)
```csharp
// In Program.cs or Startup.cs
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddScoped<IFlowTimeApiClient, MockFlowTimeApiClient>();
}
```

Mock mode features:
- Synthetic run artifacts for testing
- Deterministic data generation
- No API dependencies
- Edge case simulation

### Live API Mode
```csharp
builder.Services.AddHttpClient<IFlowTimeApiClient, FlowTimeApiClient>(client =>
{
    client.BaseAddress = new Uri("http://localhost:8080");
});
```

### Hybrid Mode
Toggle between mock and live data per component for flexible testing.

---

## 4. Component Development

### Chart Components
```csharp
@using MudBlazor

<MudChart ChartType="ChartType.Line"
    ChartData="@chartData"
    XAxisLabels="@xAxisLabels"
    Width="100%" Height="400px" />
```

### PMF Components
```csharp
@using FlowTime.UI.Components.PMF

<PMFEditor 
    @bind-Distribution="@currentPMF"
    ShowStats="true"
    AllowNormalization="true"
    OnSave="@SaveToLibrary" />
```

### Graph Components
```csharp
@using FlowTime.UI.Components.Graph

<DAGVisualizer 
    Nodes="@graphNodes"
    Edges="@graphEdges"
    Layout="elkjs"
    OnNodeClick="@ShowNodeDetails" />
```

---

## 5. Debugging & Troubleshooting

### Browser Developer Tools
- **F12** - Open dev tools
- **Network tab** - Monitor API calls
- **Console** - JavaScript errors and logging
- **Performance tab** - Chart rendering profiling

### Blazor Debugging
```csharp
// Add logging to components
@inject ILogger<ComponentName> Logger

@code {
    protected override async Task OnInitializedAsync()
    {
        Logger.LogInformation("Component initialized");
        // Component logic
    }
}
```

### API Debugging
```csharp
// Enable detailed HTTP logging
builder.Services.AddLogging(config =>
{
    config.AddConsole();
    config.SetMinimumLevel(LogLevel.Debug);
});
```

### Common Issues

**Chart not rendering:**
- Check data format matches MudBlazor chart expectations
- Verify component references are correct

**PMF normalization errors:**
- Validate probabilities sum to 1.0 ± tolerance
- Check for negative probability values
- Ensure bin count matches expected range

**Graph layout problems:**
- Verify node/edge data structure
- Check for circular dependencies
- Ensure elkjs worker is loaded

---

## 6. Testing

### Unit Testing
```csharp
[Test]
public void PMFEditor_Normalization_SumsToOne()
{
    var editor = new PMFEditor();
    var pmf = new double[] { 0.3, 0.5, 0.2 };
    
    var normalized = editor.Normalize(pmf);
    
    Assert.AreEqual(1.0, normalized.Sum(), 0.001);
}
```

### Integration Testing
```csharp
[Test]
public async Task RunManager_TriggerRun_ReturnsValidResult()
{
    var client = new TestFlowTimeApiClient();
    var manager = new RunManager(client);
    
    var result = await manager.TriggerRunAsync(testModel);
    
    Assert.IsNotNull(result.RunId);
    Assert.IsTrue(File.Exists(result.ArtifactPath));
}
```

### Visual Testing
- Screenshot regression tests for charts
- Component snapshots for UI consistency
- Cross-browser compatibility checks

---

## 7. Performance Optimization

### Large Dataset Handling
- Implement data virtualization for large time series
- Use canvas-based rendering for >1000 data points
- Implement downsampling for chart display

### Memory Management
- Dispose of chart instances properly
- Clear large datasets when navigating away
- Monitor memory usage in dev tools

### Bundle Optimization
- Use production Blazor build
- Enable MudBlazor optimizations
- Optimize PixiJS asset loading

---

## 8. Deployment

### Development Build
```bash
dotnet run --environment Development
```

### Production Build
```bash
dotnet publish -c Release -o dist/
```

### Docker Deployment
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:9.0
COPY dist/ /app/
WORKDIR /app
EXPOSE 80
ENTRYPOINT ["dotnet", "FlowTime.UI.dll"]
```

---

## 9. Troubleshooting Guide

### API Connection Issues
1. Verify API is running on expected port
2. Check CORS configuration
3. Validate API endpoint URLs
4. Review network logs for failed requests

### Component State Issues
1. Check component lifecycle methods
2. Verify @bind directives are correct
3. Review StateHasChanged() calls
4. Use browser debugger for JavaScript interop

### Performance Problems
1. Profile chart rendering performance
2. Monitor memory usage patterns
3. Check for memory leaks in components
4. Optimize data binding frequency

---

## 10. Resources

- [Blazor Documentation](https://docs.microsoft.com/en-us/aspnet/core/blazor/)
- [PixiJS Documentation](https://pixijs.com/guides)
- [MudBlazor Documentation](https://mudblazor.com/)
- [SignalR Documentation](https://docs.microsoft.com/en-us/aspnet/core/signalr/)

---

**Next Steps:** Once familiar with basic setup, review the [Design Specification](design-specification.md) for detailed component requirements and the [API Integration Guide](api-integration.md) for backend connectivity patterns.
