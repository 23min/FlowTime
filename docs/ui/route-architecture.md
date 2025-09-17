# FlowTime Route-Based UI Architecture

**Version:** 1.0  
**Audience:** UI architects, Blazor developers, DevOps engineers  
**Purpose:** Technical specification for implementing dual-interface architecture using route-based separation  

---

## 1. Architecture Overview

FlowTime implements a **dual-interface architecture** that separates expert productivity tools from pedagogical learning experiences while sharing core infrastructure. The solution uses **route-based separation** with distinct URL patterns, layouts, and navigation paradigms.

### 1.1 Interface Separation Strategy

```
┌─────────────────────────────────────────────────────────────┐
│                    FlowTime Application                     │
├─────────────────────────────┬───────────────────────────────┤
│        Expert Interface     │      Learning Interface      │
│         /app/*              │          /learn/*            │
├─────────────────────────────┼───────────────────────────────┤
│ • Production workflows      │ • Guided discovery           │
│ • Advanced configuration    │ • Concept explanation        │
│ • Technical terminology     │ • Business language          │
│ • Dense information         │ • Progressive complexity     │
│ • Keyboard shortcuts        │ • Interactive tutorials      │
└─────────────────────────────┴───────────────────────────────┘
┌─────────────────────────────────────────────────────────────┐
│                   Shared Infrastructure                     │
│ • API Services • Data Models • Core Components • State     │
└─────────────────────────────────────────────────────────────┘
```

### 1.2 Route Structure

**Expert Interface Routes:**
```
/                       → Dashboard/Home
/analyze               → FlowTime Engine Overview (landing page)
  /features            → Features (FlowTime Engine)
  /api-demo            → API Testing (FlowTime Engine)  
  /scenarios           → Scenario Composer (FlowTime Engine) - future
/simulate              → FlowTime-Sim Overview (landing page)
  /sim/templates       → Template Studio (FlowTime-Sim)
  /sim/catalogs        → Catalog Browser (FlowTime-Sim) - future
/tools                 → Tools Overview (landing page)
  /health              → System Health Monitor
  /settings            → Configuration - future
```

**Navigation Structure (Flat with Visual Hierarchy):**
```
🏠 Home
📊 ANALYZE (FlowTime Engine) → /analyze
    Features → /features
    API Testing → /api-demo
🎲 SIMULATE (FlowTime-Sim) → /simulate  
    Template Studio → /sim/templates
🔧 TOOLS → /tools
    System Health → /health
🎓 LEARN → /learn
    Getting Started → /learn/welcome
```

**Learning Interface Routes:**
```
/learn                 → Welcome & Orientation
/learn/foundations     → Digital Twin Concepts
/learn/system          → Your System Explorer  
/learn/scenarios       → What-If Builder
/learn/uncertainty     → Risk & Variability
/learn/success         → Case Studies & ROI
/learn/sandbox         → Hands-On Experimentation
/learn/bridge          → Transition to Expert Mode
```

---

## 2. Technical Implementation

### 2.1 Route Configuration

**Program.cs Setup:**
```csharp
var builder = WebApplication.CreateBuilder(args);

// Shared services
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddMudServices();

// Core services (shared between interfaces)
builder.Services.AddScoped<IFlowTimeApiClient, FlowTimeApiClient>();
builder.Services.AddScoped<IRunService, RunService>();
builder.Services.AddScoped<IGraphService, GraphService>();
builder.Services.AddScoped<IScenarioService, ScenarioService>();

// Interface-specific services
builder.Services.AddScoped<ILearningService, LearningService>();
builder.Services.AddScoped<ITutorialService, TutorialService>();
builder.Services.AddScoped<IExpertWorkflowService, ExpertWorkflowService>();

var app = builder.Build();

// Configure routing
app.UseStaticFiles();
app.UseRouting();

// Map Blazor hubs for both interfaces
app.MapBlazorHub("/app/_blazor");
app.MapBlazorHub("/learn/_blazor");

// Fallback routing
app.MapFallbackToPage("/App/{*clientRoute:nonfile}", "/App");
app.MapFallbackToPage("/Learn/{*clientRoute:nonfile}", "/Learn");

app.Run();
```

### 2.2 Layout Architecture

**Expert Interface Layout:**
```csharp
// Layouts/ExpertLayout.razor
@inherits LayoutView
@namespace FlowTime.UI.Layouts

<MudThemeProvider Theme="@ExpertTheme" />
<MudDialogProvider />
<MudSnackbarProvider />

<MudLayout>
    <MudAppBar Elevation="1">
        <MudIconButton Icon="Icons.Material.Filled.Menu" 
                      OnClick="@((e) => DrawerToggle())" />
        <MudText Typo="Typo.h6">FlowTime Expert Console</MudText>
        <MudSpacer />
        
        <!-- Context-aware help -->
        <MudTooltip Text="Need help getting started?">
            <MudButton Variant="Variant.Text" 
                      StartIcon="Icons.Material.Filled.School"
                      Href="/learn"
                      Color="Color.Inherit">
                Learning Mode
            </MudButton>
        </MudTooltip>
        
        <!-- User menu -->
        <ExpertUserMenu />
    </MudAppBar>
    
    <MudDrawer @bind-Open="drawerOpen" Elevation="1">
        <ExpertNavigation CurrentPath="@CurrentPath" />
    </MudDrawer>
    
    <MudMainContent Class="pt-16 px-4">
        <MudContainer MaxWidth="MaxWidth.False">
            @Body
        </MudContainer>
    </MudMainContent>
</MudLayout>

@code {
    private bool drawerOpen = true;
    
    [Parameter] public string CurrentPath { get; set; } = "";
    
    private void DrawerToggle()
    {
        drawerOpen = !drawerOpen;
    }
    
    private MudTheme ExpertTheme = new()
    {
        // Productivity-focused theme
        Palette = new()
        {
            Primary = Colors.Blue.Default,
            Secondary = Colors.Grey.Default,
            Background = Colors.Grey.Lighten5,
            Surface = Colors.Shades.White,
            AppbarBackground = Colors.Blue.Default,
        }
    };
}
```

**Learning Interface Layout:**
```csharp
// Layouts/LearningLayout.razor  
@inherits LayoutView
@namespace FlowTime.UI.Layouts

<MudThemeProvider Theme="@LearningTheme" />
<MudDialogProvider />
<MudSnackbarProvider />

<MudLayout>
    <MudAppBar Elevation="2">
        <MudIcon Icon="Icons.Material.Filled.School" />
        <MudText Typo="Typo.h6" Class="ml-2">FlowTime Learning Center</MudText>
        <MudSpacer />
        
        <!-- Progress indicator -->
        <LearningProgressIndicator />
        
        <!-- Switch to expert mode -->
        <MudTooltip Text="Ready for advanced features?">
            <MudButton Variant="Variant.Text"
                      StartIcon="Icons.Material.Filled.Engineering"
                      Href="/app"
                      Color="Color.Inherit">
                Expert Mode
            </MudButton>
        </MudTooltip>
    </MudAppBar>
    
    <MudDrawer @bind-Open="drawerOpen" Width="320px" Elevation="2">
        <div class="pa-4">
            <LearningPathSelector @bind-SelectedPath="selectedPath" />
            <MudDivider Class="my-4" />
            <LearningNavigation CurrentPath="@CurrentPath" 
                              SelectedPath="@selectedPath" />
        </div>
    </MudDrawer>
    
    <MudMainContent Class="pt-16">
        <div class="learning-content">
            @Body
        </div>
    </MudMainContent>
</MudLayout>

@code {
    private bool drawerOpen = true;
    private LearningPath selectedPath = LearningPath.Executive;
    
    [Parameter] public string CurrentPath { get; set; } = "";
    
    private MudTheme LearningTheme = new()
    {
        // Engaging, educational theme
        Palette = new()
        {
            Primary = Colors.Green.Default,
            Secondary = Colors.Orange.Default,
            Background = Colors.Grey.Lighten4,
            Surface = Colors.Shades.White,
            AppbarBackground = Colors.Green.Default,
        }
    };
}
```

### 2.3 Navigation Components

**Expert Navigation:**
```csharp
// Components/Navigation/ExpertNavigation.razor
@inject NavigationManager Navigation

<MudNavMenu>
    <MudNavGroup Text="Analysis" Icon="@Icons.Material.Filled.Analytics">
        <MudNavLink Href="/app/runs" 
                   Match="NavLinkMatch.Prefix"
                   Icon="@Icons.Material.Filled.PlayArrow">
            Run Manager
        </MudNavLink>
        <MudNavLink Href="/app/graph" 
                   Match="NavLinkMatch.Prefix"
                   Icon="@Icons.Material.Filled.AccountTree">
            Graph Explorer
        </MudNavLink>
        <MudNavLink Href="/app/telemetry"
                   Match="NavLinkMatch.Prefix" 
                   Icon="@Icons.Material.Filled.Timeline">
            Telemetry Overlay
        </MudNavLink>
    </MudNavGroup>
    
    <MudNavGroup Text="Modeling" Icon="@Icons.Material.Filled.Science">
        <MudNavLink Href="/app/scenarios"
                   Match="NavLinkMatch.Prefix"
                   Icon="@Icons.Material.Filled.Tune">
            Scenarios
        </MudNavLink>
        <MudNavLink Href="/app/pmf-library"
                   Match="NavLinkMatch.Prefix"
                   Icon="@Icons.Material.Filled.Functions">
            PMF Library
        </MudNavLink>
        <MudNavLink Href="/app/pmf-editor"
                   Match="NavLinkMatch.Prefix"
                   Icon="@Icons.Material.Filled.Edit">
            PMF Editor
        </MudNavLink>
    </MudNavGroup>
    
    <MudNavGroup Text="Administration" Icon="@Icons.Material.Filled.Settings">
        <MudNavLink Href="/app/settings"
                   Match="NavLinkMatch.Prefix"
                   Icon="@Icons.Material.Filled.Tune">
            Settings
        </MudNavLink>
    </MudNavGroup>
</MudNavMenu>
```

**Learning Navigation:**
```csharp
// Components/Navigation/LearningNavigation.razor
@inject NavigationManager Navigation

<MudNavMenu>
    <!-- Progress-based navigation -->
    <div class="learning-steps">
        <LearningStep StepNumber="1" 
                     Title="What is FlowTime?"
                     Href="/learn/foundations"
                     IsCompleted="@IsStepCompleted(1)"
                     IsActive="@IsCurrentStep(1)" />
                     
        <LearningStep StepNumber="2"
                     Title="Your System"
                     Href="/learn/system"
                     IsCompleted="@IsStepCompleted(2)"
                     IsActive="@IsCurrentStep(2)"
                     IsEnabled="@IsStepEnabled(2)" />
                     
        <LearningStep StepNumber="3"
                     Title="What-If Analysis"
                     Href="/learn/scenarios"
                     IsCompleted="@IsStepCompleted(3)"
                     IsActive="@IsCurrentStep(3)"
                     IsEnabled="@IsStepEnabled(3)" />
                     
        <!-- Additional steps... -->
    </div>
    
    <MudDivider Class="my-4" />
    
    <!-- Quick access links -->
    <MudNavGroup Text="Quick Access" Icon="@Icons.Material.Filled.Speed">
        <MudNavLink Href="/learn/sandbox"
                   Icon="@Icons.Material.Filled.Science">
            Try It Yourself
        </MudNavLink>
        <MudNavLink Href="/learn/success"
                   Icon="@Icons.Material.Filled.TrendingUp">
            Success Stories
        </MudNavLink>
    </MudNavGroup>
</MudNavMenu>

@code {
    [Parameter] public string CurrentPath { get; set; } = "";
    [Parameter] public LearningPath SelectedPath { get; set; }
    
    [Inject] private ILearningProgressService ProgressService { get; set; }
    
    private bool IsStepCompleted(int stepNumber) =>
        ProgressService.IsStepCompleted(stepNumber);
        
    private bool IsCurrentStep(int stepNumber) =>
        ProgressService.GetCurrentStep() == stepNumber;
        
    private bool IsStepEnabled(int stepNumber) =>
        ProgressService.IsStepEnabled(stepNumber);
}
```

### 2.4 Shared Service Architecture

**Core API Services (Shared):**
```csharp
// Services/Core/IFlowTimeApiClient.cs
public interface IFlowTimeApiClient
{
    Task<RunResult> TriggerRunAsync(ModelDefinition model);
    Task<GraphData> GetGraphAsync(string runId);
    Task<RunSummary[]> GetRunsAsync();
    Task<SeriesData> GetSeriesDataAsync(string runId, string seriesId);
}

// Services/Core/FlowTimeApiClient.cs
public class FlowTimeApiClient : IFlowTimeApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<FlowTimeApiClient> _logger;
    
    // Implementation shared by both interfaces
}
```

**Interface-Specific Services:**
```csharp
// Services/Learning/ILearningService.cs
public interface ILearningService
{
    Task<LearningScenario[]> GetGuidedScenariosAsync(string domain);
    Task<ConceptExplanation> GetConceptExplanationAsync(string conceptId);
    Task<TutorialStep[]> GetTutorialStepsAsync(string tutorialId);
    Task TrackLearningProgressAsync(string userId, int stepNumber);
}

// Services/Expert/IExpertWorkflowService.cs  
public interface IExpertWorkflowService
{
    Task<WorkflowTemplate[]> GetWorkflowTemplatesAsync();
    Task<KeyboardShortcut[]> GetKeyboardShortcutsAsync();
    Task SaveWorkspaceLayoutAsync(string userId, WorkspaceLayout layout);
}
```

### 2.5 State Management Strategy

**Shared State (Cross-Interface):**
```csharp
// State/Shared/AppState.cs
public class AppState
{
    // Data that persists across interface switches
    public string? CurrentRunId { get; set; }
    public ModelDefinition? ActiveModel { get; set; }
    public UserPreferences Preferences { get; set; } = new();
    
    public event Action? OnChange;
    
    public void NotifyStateChanged() => OnChange?.Invoke();
}
```

**Interface-Specific State:**
```csharp
// State/Learning/LearningState.cs
public class LearningState
{
    public LearningPath CurrentPath { get; set; }
    public int CurrentStep { get; set; }
    public Dictionary<int, bool> CompletedSteps { get; set; } = new();
    public string? SelectedDomain { get; set; }
    public TutorialProgress TutorialProgress { get; set; } = new();
}

// State/Expert/ExpertState.cs
public class ExpertState  
{
    public WorkspaceLayout Layout { get; set; } = new();
    public Dictionary<string, object> ComponentStates { get; set; } = new();
    public KeyboardShortcutSet Shortcuts { get; set; } = new();
    public List<string> RecentActions { get; set; } = new();
}
```

---

## 3. Component Sharing Strategy

### 3.1 Shared Component Categories

**Core Visualization Components (100% Shared):**
- Chart components (time series, histograms)
- Graph rendering (DAG visualization)
- Data grid components
- Loading and error states

**Functional Components (Contextual Sharing):**
- Run controls (different UX for each interface)
- Scenario builders (expert vs guided versions)
- PMF editors (advanced vs simplified)

**Interface-Specific Components (No Sharing):**
- Navigation systems
- Tutorial overlays  
- Expert keyboard shortcuts
- Learning progress tracking

### 3.2 Component Adaptation Pattern

**Shared Base with Context Variants:**
```csharp
// Components/Shared/RunTrigger/RunTriggerBase.cs
public abstract class RunTriggerBase : ComponentBase
{
    [Parameter] public ModelDefinition Model { get; set; }
    [Parameter] public EventCallback<RunResult> OnRunCompleted { get; set; }
    [Inject] protected IRunService RunService { get; set; }
    
    protected async Task TriggerRun()
    {
        var result = await RunService.TriggerRunAsync(Model);
        await OnRunCompleted.InvokeAsync(result);
    }
}

// Components/Expert/ExpertRunTrigger.razor
@inherits RunTriggerBase

<MudButton Variant="Variant.Filled" 
          StartIcon="@Icons.Material.Filled.PlayArrow"
          OnClick="TriggerRun"
          Size="Size.Small">
    Run Model
</MudButton>

// Components/Learning/LearningRunTrigger.razor  
@inherits RunTriggerBase

<div class="learning-run-section">
    <MudText Typo="Typo.h6" Class="mb-2">Ready to see what happens?</MudText>
    <MudText Typo="Typo.body1" Class="mb-4">
        Click the button below to run your scenario and see the results!
    </MudText>
    <MudButton Variant="Variant.Filled"
              StartIcon="@Icons.Material.Filled.PlayArrow" 
              OnClick="TriggerRun"
              Size="Size.Large"
              Color="Color.Primary">
        Run My Scenario
    </MudButton>
</div>
```

---

## 4. Cross-Interface Integration

### 4.1 Context Handoff Patterns

**Learning to Expert Transition:**
```csharp
// Services/Integration/InterfaceTransitionService.cs
public class InterfaceTransitionService
{
    public async Task<string> CreateExpertSessionFromLearning(
        LearningSession learningSession)
    {
        // Package learning context for expert interface
        var expertContext = new ExpertSessionContext
        {
            PreloadedModel = learningSession.CurrentModel,
            SuggestedWorkflows = GetRelevantWorkflows(learningSession.CompletedSteps),
            BackgroundInfo = learningSession.ConceptsMastered,
            TransitionReason = TransitionReason.FromLearning
        };
        
        var sessionId = await SaveExpertContext(expertContext);
        return $"/app?transition={sessionId}";
    }
}
```

**Expert to Learning Transition:**
```csharp
public async Task<string> CreateLearningSessionFromExpert(
    ExpertSession expertSession)
{
    // Suggest relevant learning based on expert activity
    var learningContext = new LearningSessionContext
    {
        RecommendedPath = DetermineLearningPath(expertSession.RecentActions),
        PreloadedScenarios = ExtractScenarios(expertSession.ActiveModels),
        SkipBasics = expertSession.UserExperienceLevel > ExperienceLevel.Beginner
    };
    
    var sessionId = await SaveLearningContext(learningContext);
    return $"/learn?context={sessionId}";
}
```

### 4.2 Progress Tracking Integration

**Cross-Interface User Journey:**
```csharp
// Models/UserJourney.cs
public class UserJourney
{
    public string UserId { get; set; }
    public List<InterfaceSession> Sessions { get; set; } = new();
    public SkillLevel CurrentSkillLevel { get; set; }
    public List<string> MasteredConcepts { get; set; } = new();
    public Dictionary<string, int> FeatureUsageCounts { get; set; } = new();
}

public class InterfaceSession
{
    public InterfaceType Type { get; set; } // Learning or Expert
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public List<string> ActionsPerformed { get; set; } = new();
    public string? TransitionReason { get; set; }
    public Dictionary<string, object> Outcomes { get; set; } = new();
}
```

---

## 5. Deployment and Infrastructure

### 5.1 Single Application Deployment

**Benefits:**
- Shared infrastructure and dependencies
- Single authentication system
- Simplified deployment pipeline
- Resource efficiency

**Dockerfile:**
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["FlowTime.UI/FlowTime.UI.csproj", "FlowTime.UI/"]
RUN dotnet restore "FlowTime.UI/FlowTime.UI.csproj"
COPY . .
WORKDIR "/src/FlowTime.UI"
RUN dotnet build "FlowTime.UI.csproj" -c Release -o /app/build

FROM build AS publish  
RUN dotnet publish "FlowTime.UI.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "FlowTime.UI.dll"]
```

### 5.2 Reverse Proxy Configuration

**Nginx Configuration (Optional):**
```nginx
# /etc/nginx/sites-available/flowtime
server {
    listen 80;
    server_name flowtime.company.com;
    
    # Expert interface
    location /app {
        proxy_pass http://localhost:5000/app;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    }
    
    # Learning interface  
    location /learn {
        proxy_pass http://localhost:5000/learn;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    }
    
    # Shared static assets
    location /css {
        proxy_pass http://localhost:5000/css;
    }
    
    location /js {
        proxy_pass http://localhost:5000/js;
    }
    
    # Blazor SignalR hubs
    location /_blazor {
        proxy_pass http://localhost:5000/_blazor;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
    }
}
```

---

## 6. Testing Strategy

### 6.1 Interface-Specific Testing

**Expert Interface Tests:**
```csharp
[TestClass]
public class ExpertInterfaceTests : TestContext
{
    [TestMethod]
    public void ExpertNavigation_ShowsAdvancedFeatures()
    {
        // Arrange
        var component = RenderComponent<ExpertLayout>();
        
        // Act & Assert
        component.Find("[data-testid='pmf-editor-link']").Should().NotBeNull();
        component.Find("[data-testid='advanced-scenarios-link']").Should().NotBeNull();
    }
    
    [TestMethod]
    public void ExpertWorkflow_EnablesKeyboardShortcuts()
    {
        // Test keyboard shortcut functionality
    }
}
```

**Learning Interface Tests:**
```csharp
[TestClass]
public class LearningInterfaceTests : TestContext
{
    [TestMethod]
    public void LearningNavigation_ShowsProgressTracker()
    {
        // Arrange
        var component = RenderComponent<LearningLayout>();
        
        // Act & Assert
        component.Find("[data-testid='progress-indicator']").Should().NotBeNull();
        component.Find("[data-testid='step-tracker']").Should().NotBeNull();
    }
    
    [TestMethod]
    public void LearningFlow_PreventsSkippingSteps()
    {
        // Test guided progression logic
    }
}
```

### 6.2 Cross-Interface Integration Testing

**Transition Testing:**
```csharp
[TestMethod]
public async Task Transition_LearningToExpert_PreservesContext()
{
    // Arrange
    var learningSession = CreateLearningSession();
    var transitionService = new InterfaceTransitionService();
    
    // Act
    var expertUrl = await transitionService.CreateExpertSessionFromLearning(learningSession);
    
    // Assert
    expertUrl.Should().Contain("/app?transition=");
    // Verify context preservation
}
```

---

## 7. Alternative Solutions Considered

### 7.1 Subdomain-Based Separation

**Approach:** `app.flowtime.com` vs `learn.flowtime.com`

**Pros:**
- Complete architectural separation
- Independent deployment pipelines
- Different technology stacks possible
- Clear branding distinction

**Cons:**
- Increased infrastructure complexity
- Duplicate service implementations
- Authentication complexity across domains
- Higher operational overhead

**Decision:** Rejected due to increased complexity without proportional benefits for a single-team development scenario.

### 7.2 Single-Page Application with Mode Toggle

**Approach:** Toggle button to switch between interfaces within same SPA

**Pros:**
- Immediate mode switching
- Shared application state
- Simplified deployment
- Single codebase

**Cons:**
- UI complexity managing dual paradigms
- Risk of feature bleed between interfaces
- Confusing user experience
- Testing complexity

**Decision:** Rejected due to poor user experience and maintenance complexity.

### 7.3 Micro-Frontend Architecture

**Approach:** Separate applications composed at runtime

**Pros:**
- True independent development
- Technology diversity possible
- Isolated deployment
- Team autonomy

**Cons:**
- Significant architectural complexity
- Shared state management challenges
- Performance overhead
- Integration testing complexity

**Decision:** Rejected as over-engineering for current team size and requirements.

### 7.4 Component-Level Mode Switching

**Approach:** Each component adapts behavior based on mode parameter

**Pros:**
- Maximum code reuse
- Single source of truth for functionality
- Simplified testing of core logic

**Cons:**
- Complex component interfaces
- Risk of mode-specific bugs
- Difficult to maintain clean UX paradigms
- Feature flag complexity

**Decision:** Rejected due to maintainability concerns and UX paradigm conflicts.

---

## 8. Implementation Recommendations

### 8.1 Development Approach

**Phase 1: Route Infrastructure**
1. Set up dual routing system
2. Create basic layouts for both interfaces
3. Implement shared service layer
4. Establish component sharing patterns

**Phase 2: Expert Interface**
1. Implement expert navigation and workflows
2. Build advanced features and keyboard shortcuts
3. Optimize for productivity and efficiency

**Phase 3: Learning Interface**
1. Create guided learning experiences
2. Implement tutorial and progress systems
3. Build domain-specific examples

**Phase 4: Integration**
1. Implement smooth transitions between interfaces
2. Add cross-interface progress tracking
3. Optimize performance and user experience

### 8.2 Success Criteria

**Technical Metrics:**
- Page load time <2 seconds for both interfaces
- Smooth transitions with <500ms context switch
- Shared code percentage >70% for core functionality
- Test coverage >90% for both interface paths

**User Experience Metrics:**
- Expert interface: Time to complete common tasks
- Learning interface: Concept comprehension rates
- Transition success: Users successfully moving between modes
- Overall satisfaction: Net Promoter Score for both interfaces

---

**Related Documents:**
- [Learning Interface Specification](learning-interface.md) - Pedagogical UI requirements
- [Expert Interface Specification](design-specification.md) - Productivity-focused UI
- [Development Guide](development-guide.md) - Implementation guidelines
- [Architecture Overview](architecture.md) - System design principles
