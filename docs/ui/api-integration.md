# FlowTime UI API Integration Guide

**Version:** 1.0  
**Audience:** UI developers, integration engineers  
**Purpose:** Detailed guide for integrating FlowTime UI with FlowTime API endpoints  

---

## 1. API Overview

### 1.1 Base Configuration
```csharp
public class FlowTimeApiConfiguration
{
    public string BaseUrl { get; set; } = "http://localhost:8080";
    public string ApiVersion { get; set; } = "v1";
    public int TimeoutSeconds { get; set; } = 30;
    public int MaxRetries { get; set; } = 3;
}
```

### 1.2 HTTP Client Setup
```csharp
// Program.cs
builder.Services.AddHttpClient<IFlowTimeApiClient, FlowTimeApiClient>(client =>
{
    var config = builder.Configuration.Get<FlowTimeApiConfiguration>();
    client.BaseAddress = new Uri(config.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});
```

---

## 2. Core API Endpoints

### 2.1 Health Check Integration
```csharp
public interface IHealthService
{
    Task<HealthStatus> CheckHealthAsync();
}

public class HealthService : IHealthService
{
    private readonly HttpClient _httpClient;
    
    public async Task<HealthStatus> CheckHealthAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/healthz");
            return response.IsSuccessStatusCode 
                ? HealthStatus.Healthy 
                : HealthStatus.Degraded;
        }
        catch
        {
            return HealthStatus.Unhealthy;
        }
    }
}
```

**UI Integration:**
```csharp
@inject IHealthService HealthService

<div class="health-indicator @healthClass">
    <span>API Status: @healthStatus</span>
</div>

@code {
    private HealthStatus healthStatus = HealthStatus.Unknown;
    private string healthClass => healthStatus.ToString().ToLower();
    
    protected override async Task OnInitializedAsync()
    {
        healthStatus = await HealthService.CheckHealthAsync();
    }
}
```

### 2.2 Graph API Integration
```csharp
public interface IGraphService
{
    Task<GraphData> GetGraphAsync(string runId);
    Task<GraphData> GetGraphFromModelAsync(ModelDefinition model);
}

public class GraphService : IGraphService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GraphService> _logger;
    
    public async Task<GraphData> GetGraphAsync(string runId)
    {
        var response = await _httpClient.GetAsync($"/v1/runs/{runId}/graph");
        response.EnsureSuccessStatusCode();
        
        var yamlContent = await response.Content.ReadAsStringAsync();
        return YamlSerializer.Deserialize<GraphData>(yamlContent);
    }
    
    public async Task<GraphData> GetGraphFromModelAsync(ModelDefinition model)
    {
        var yamlContent = YamlSerializer.Serialize(model);
        var content = new StringContent(yamlContent, Encoding.UTF8, "text/plain");
        
        var response = await _httpClient.PostAsync("/v1/graph", content);
        response.EnsureSuccessStatusCode();
        
        var responseYaml = await response.Content.ReadAsStringAsync();
        return YamlSerializer.Deserialize<GraphData>(responseYaml);
    }
}
```

**UI Integration:**
```csharp
@inject IGraphService GraphService

<DAGVisualizer Nodes="@graphData?.Nodes" Edges="@graphData?.Edges" />

@code {
    private GraphData? graphData;
    
    private async Task LoadGraphAsync(string runId)
    {
        try
        {
            graphData = await GraphService.GetGraphAsync(runId);
            StateHasChanged();
        }
        catch (Exception ex)
        {
            // Handle error
            Logger.LogError(ex, "Failed to load graph for run {RunId}", runId);
        }
    }
}
```

### 2.3 Run Management Integration
```csharp
public interface IRunService
{
    Task<RunResult> TriggerRunAsync(ModelDefinition model);
    Task<RunSummary[]> GetRunsAsync();
    Task<RunDetails> GetRunDetailsAsync(string runId);
    Task<byte[]> ExportRunAsync(string runId, ExportFormat format);
}

public class RunService : IRunService
{
    private readonly HttpClient _httpClient;
    
    public async Task<RunResult> TriggerRunAsync(ModelDefinition model)
    {
        var yamlContent = YamlSerializer.Serialize(model);
        var content = new StringContent(yamlContent, Encoding.UTF8, "text/plain");
        
        var response = await _httpClient.PostAsync("/v1/run", content);
        response.EnsureSuccessStatusCode();
        
        var yamlResult = await response.Content.ReadAsStringAsync();
        return YamlSerializer.Deserialize<RunResult>(yamlResult);
    }
    
    public async Task<RunSummary[]> GetRunsAsync()
    {
        var response = await _httpClient.GetAsync("/v1/runs");
        response.EnsureSuccessStatusCode();
        
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<RunSummary[]>(json) ?? [];
    }
}
```

**UI Integration:**
```csharp
@inject IRunService RunService

<button @onclick="TriggerRun" disabled="@isRunning">
    @if (isRunning)
    {
        <span>Running...</span>
    }
    else
    {
        <span>Run Model</span>
    }
</button>

@code {
    private bool isRunning = false;
    private ModelDefinition currentModel = new();
    
    private async Task TriggerRun()
    {
        isRunning = true;
        try
        {
            var result = await RunService.TriggerRunAsync(currentModel);
            await OnRunCompleted(result);
        }
        catch (Exception ex)
        {
            await OnRunFailed(ex);
        }
        finally
        {
            isRunning = false;
        }
    }
}
```

### 2.4 Series Data Integration
```csharp
public interface ISeriesService
{
    Task<SeriesIndex> GetSeriesIndexAsync(string runId);
    Task<SeriesData> GetSeriesDataAsync(string runId, string seriesId);
    Task<Dictionary<string, SeriesData>> GetMultipleSeriesAsync(string runId, string[] seriesIds);
}

public class SeriesService : ISeriesService
{
    private readonly HttpClient _httpClient;
    
    public async Task<SeriesIndex> GetSeriesIndexAsync(string runId)
    {
        var response = await _httpClient.GetAsync($"/v1/runs/{runId}/index");
        response.EnsureSuccessStatusCode();
        
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<SeriesIndex>(json) ?? new();
    }
    
    public async Task<SeriesData> GetSeriesDataAsync(string runId, string seriesId)
    {
        var response = await _httpClient.GetAsync($"/v1/runs/{runId}/series/{seriesId}");
        response.EnsureSuccessStatusCode();
        
        var csvContent = await response.Content.ReadAsStringAsync();
        return ParseCsvToSeriesData(csvContent);
    }
}
```

**UI Integration:**
```csharp
@inject ISeriesService SeriesService

<MudChart ChartType="ChartType.Line"
    ChartData="@chartData"
    XAxisLabels="@xAxisLabels"
    Width="100%" Height="400px" />

<select @onchange="OnSeriesSelected">
    @foreach (var series in seriesIndex?.Series ?? [])
    {
        <option value="@series.Id">@series.Name</option>
    }
</select>

@code {
    private SeriesIndex? seriesIndex;
    private SeriesData? seriesData;
    private SeriesSummary? selectedSeries;
    private List<ChartSeries> chartData = new();
    private string[] xAxisLabels = Array.Empty<string>();
    
    private async Task OnSeriesSelected(ChangeEventArgs e)
    {
        var seriesId = e.Value?.ToString();
        if (!string.IsNullOrEmpty(seriesId))
        {
            seriesData = await SeriesService.GetSeriesDataAsync(currentRunId, seriesId);
            selectedSeries = seriesIndex?.Series.FirstOrDefault(s => s.Id == seriesId);
            
            // Update chart data
            chartData = new List<ChartSeries> { seriesData.ToMudChartData() };
            xAxisLabels = seriesData.ToXAxisLabels();
            StateHasChanged();
        }
    }
}
```

---

## 3. Advanced Integration Patterns

### 3.1 Polling for Long-Running Operations
```csharp
public class PollingService<T>
{
    private readonly ILogger<PollingService<T>> _logger;
    
    public async Task<T> PollUntilCompleteAsync<TStatus>(
        Func<Task<TStatus>> statusChecker,
        Func<TStatus, bool> isComplete,
        Func<Task<T>> resultGetter,
        TimeSpan interval = default,
        CancellationToken cancellationToken = default)
    {
        interval = interval == default ? TimeSpan.FromSeconds(2) : interval;
        
        while (!cancellationToken.IsCancellationRequested)
        {
            var status = await statusChecker();
            
            if (isComplete(status))
            {
                return await resultGetter();
            }
            
            await Task.Delay(interval, cancellationToken);
        }
        
        throw new OperationCanceledException();
    }
}
```

**Usage for Run Monitoring:**
```csharp
public async Task<RunResult> WaitForRunCompletionAsync(string runId)
{
    return await _pollingService.PollUntilCompleteAsync(
        statusChecker: () => GetRunStatusAsync(runId),
        isComplete: status => status.State == RunState.Completed || status.State == RunState.Failed,
        resultGetter: () => GetRunResultAsync(runId),
        interval: TimeSpan.FromSeconds(1)
    );
}
```

### 3.2 Caching Strategy
```csharp
public class CachedApiService : IRunService
{
    private readonly IRunService _innerService;
    private readonly IMemoryCache _cache;
    private readonly TimeSpan _cacheExpiry = TimeSpan.FromMinutes(5);
    
    public async Task<RunSummary[]> GetRunsAsync()
    {
        const string cacheKey = "runs_list";
        
        if (_cache.TryGetValue(cacheKey, out RunSummary[]? cachedRuns))
        {
            return cachedRuns ?? [];
        }
        
        var runs = await _innerService.GetRunsAsync();
        _cache.Set(cacheKey, runs, _cacheExpiry);
        return runs;
    }
    
    public async Task<RunDetails> GetRunDetailsAsync(string runId)
    {
        var cacheKey = $"run_details_{runId}";
        
        if (_cache.TryGetValue(cacheKey, out RunDetails? cachedDetails))
        {
            return cachedDetails ?? new();
        }
        
        var details = await _innerService.GetRunDetailsAsync(runId);
        
        // Cache run details indefinitely (immutable once completed)
        _cache.Set(cacheKey, details, TimeSpan.FromHours(24));
        return details;
    }
}
```

### 3.3 Error Handling and Retry Logic
```csharp
public class ResilientApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;
    
    public async Task<T> ExecuteWithRetryAsync<T>(
        Func<Task<T>> operation,
        int maxRetries = 3,
        TimeSpan? delay = null)
    {
        delay ??= TimeSpan.FromSeconds(1);
        
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (HttpRequestException ex) when (attempt < maxRetries)
            {
                _logger.LogWarning(ex, "Attempt {Attempt} failed, retrying in {Delay}ms", 
                    attempt, delay.Value.TotalMilliseconds);
                
                await Task.Delay(delay.Value);
                delay = TimeSpan.FromMilliseconds(delay.Value.TotalMilliseconds * 1.5); // Exponential backoff
            }
        }
        
        throw new InvalidOperationException($"Operation failed after {maxRetries} attempts");
    }
}
```

---

## 4. Real-time Integration (SignalR)

### 4.1 SignalR Client Setup
```csharp
public class FlowTimeHubClient : IAsyncDisposable
{
    private readonly HubConnection _connection;
    
    public FlowTimeHubClient(string hubUrl)
    {
        _connection = new HubConnectionBuilder()
            .WithUrl(hubUrl)
            .WithAutomaticReconnect()
            .Build();
    }
    
    public async Task StartAsync()
    {
        await _connection.StartAsync();
    }
    
    public async Task SubscribeToRunUpdatesAsync(string runId, 
        Func<RunStatusUpdate, Task> onUpdate)
    {
        _connection.On<RunStatusUpdate>("RunStatusUpdate", onUpdate);
        await _connection.InvokeAsync("JoinRunGroup", runId);
    }
    
    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
    }
}
```

### 4.2 Real-time UI Updates
```csharp
@inject FlowTimeHubClient HubClient
@implements IAsyncDisposable

<div class="run-status">
    <span class="status-indicator @statusClass">@currentStatus</span>
    <div class="progress-bar">
        <div class="progress-fill" style="width: @progressPercent%"></div>
    </div>
</div>

@code {
    private string currentStatus = "Unknown";
    private int progressPercent = 0;
    private string statusClass = "unknown";
    
    protected override async Task OnInitializedAsync()
    {
        await HubClient.StartAsync();
        await HubClient.SubscribeToRunUpdatesAsync(runId, OnRunStatusUpdate);
    }
    
    private async Task OnRunStatusUpdate(RunStatusUpdate update)
    {
        currentStatus = update.Status;
        progressPercent = update.ProgressPercent;
        statusClass = update.Status.ToLower();
        
        await InvokeAsync(StateHasChanged);
    }
    
    public async ValueTask DisposeAsync()
    {
        await HubClient.DisposeAsync();
    }
}
```

---

## 5. Data Transformation and Binding

### 5.1 API Response Mapping
```csharp
public static class ApiResponseMappers
{
    public static ChartSeries ToMudChartData(this SeriesData series)
    {
        return new ChartSeries
        {
            Name = series.Name,
            Data = series.Points.Select(p => (double)p).ToArray()
        };
    }
    
    public static string[] ToXAxisLabels(this SeriesData series)
    {
        return series.Points.Select((_, index) => 
            series.StartTime.AddMinutes(index * series.BinMinutes).ToString("HH:mm")
        ).ToArray();
    }
    
    public static GraphNode[] ToGraphNodes(this GraphData graph)
    {
        return graph.Nodes.Select(n => new GraphNode
        {
            Id = n.Id,
            Label = n.Name,
            X = n.Layout?.X ?? 0,
            Y = n.Layout?.Y ?? 0,
            Type = n.Kind,
            Metadata = n.Properties
        }).ToArray();
    }
}
```

### 5.2 Model Validation
```csharp
public class ModelValidator
{
    public ValidationResult ValidateModel(ModelDefinition model)
    {
        var errors = new List<string>();
        
        // Basic structure validation
        if (model.Grid == null)
            errors.Add("Grid configuration is required");
            
        if (model.Nodes?.Any() != true)
            errors.Add("At least one node is required");
        
        // Node validation
        foreach (var node in model.Nodes ?? [])
        {
            if (string.IsNullOrWhiteSpace(node.Id))
                errors.Add($"Node {node.Id} must have an ID");
                
            if (node.Kind == NodeKind.PMF && node.Values?.Any() != true)
                errors.Add($"PMF node {node.Id} must have probability values");
        }
        
        // Reference validation
        ValidateNodeReferences(model, errors);
        
        return new ValidationResult
        {
            IsValid = !errors.Any(),
            Errors = errors
        };
    }
}
```

---

## 6. Performance Optimization

### 6.1 Request Batching
```csharp
public class BatchedSeriesService
{
    private readonly ISeriesService _seriesService;
    private readonly SemaphoreSlim _semaphore = new(5); // Limit concurrent requests
    
    public async Task<Dictionary<string, SeriesData>> GetMultipleSeriesAsync(
        string runId, 
        string[] seriesIds)
    {
        var tasks = seriesIds.Select(async seriesId =>
        {
            await _semaphore.WaitAsync();
            try
            {
                var data = await _seriesService.GetSeriesDataAsync(runId, seriesId);
                return new { SeriesId = seriesId, Data = data };
            }
            finally
            {
                _semaphore.Release();
            }
        });
        
        var results = await Task.WhenAll(tasks);
        return results.ToDictionary(r => r.SeriesId, r => r.Data);
    }
}
```

### 6.2 Response Compression
```csharp
// Program.cs
builder.Services.AddHttpClient<IFlowTimeApiClient, FlowTimeApiClient>(client =>
{
    client.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate");
});
```

---

## 7. Error Handling Patterns

### 7.1 Global Error Handler
```csharp
public class ApiExceptionHandler
{
    public static ApiError HandleException(Exception exception)
    {
        return exception switch
        {
            HttpRequestException httpEx => new ApiError
            {
                Type = ErrorType.Network,
                Message = "Network connectivity issue",
                Details = httpEx.Message
            },
            TaskCanceledException => new ApiError
            {
                Type = ErrorType.Timeout,
                Message = "Request timed out",
                Details = "The server took too long to respond"
            },
            JsonException jsonEx => new ApiError
            {
                Type = ErrorType.DataFormat,
                Message = "Invalid response format",
                Details = jsonEx.Message
            },
            _ => new ApiError
            {
                Type = ErrorType.Unknown,
                Message = "An unexpected error occurred",
                Details = exception.Message
            }
        };
    }
}
```

### 7.2 Component Error Boundaries
```csharp
<ErrorBoundary>
    <ChildContent>
        <RunManager />
    </ChildContent>
    <ErrorContent Context="exception">
        <div class="error-panel">
            <h3>Something went wrong</h3>
            <p>@ApiExceptionHandler.HandleException(exception).Message</p>
            <button @onclick="() => RecoverFromError()">Try Again</button>
        </div>
    </ErrorContent>
</ErrorBoundary>
```

---

## 8. Testing API Integration

### 8.1 Mock API Client
```csharp
public class MockFlowTimeApiClient : IFlowTimeApiClient
{
    public Task<RunResult> TriggerRunAsync(ModelDefinition model)
    {
        return Task.FromResult(new RunResult
        {
            RunId = Guid.NewGuid().ToString(),
            Status = RunStatus.Completed,
            ArtifactPath = "/mock/data/run_123"
        });
    }
    
    public Task<GraphData> GetGraphAsync(string runId)
    {
        return Task.FromResult(GenerateMockGraphData());
    }
}
```

### 8.2 Integration Testing
```csharp
[Test]
public async Task GraphService_GetGraph_ReturnsValidData()
{
    // Arrange
    var httpClient = new HttpClient(new MockHttpMessageHandler());
    var service = new GraphService(httpClient, NullLogger<GraphService>.Instance);
    
    // Act
    var result = await service.GetGraphAsync("test-run-123");
    
    // Assert
    Assert.IsNotNull(result);
    Assert.Greater(result.Nodes.Length, 0);
    Assert.Greater(result.Edges.Length, 0);
}
```

---

## 9. Configuration and Environment Management

### 9.1 Environment-Specific Configuration
```json
// appsettings.Development.json
{
  "FlowTimeApi": {
    "BaseUrl": "http://localhost:8080",
    "TimeoutSeconds": 30,
    "EnableMockMode": true
  }
}

// appsettings.Production.json
{
  "FlowTimeApi": {
    "BaseUrl": "https://api.flowtime.production.com",
    "TimeoutSeconds": 60,
    "EnableMockMode": false
  }
}
```

### 9.2 Feature Flags
```csharp
public class FeatureFlags
{
    public bool EnableRealTimeUpdates { get; set; } = true;
    public bool EnableAdvancedPMFEditor { get; set; } = false;
    public bool EnableTelemetryOverlay { get; set; } = true;
}
```

---

**Related Documents:**
- [Design Specification](design-specification.md) - UI component requirements
- [Development Guide](development-guide.md) - Setup and tooling
- [Architecture](architecture.md) - High-level system design
