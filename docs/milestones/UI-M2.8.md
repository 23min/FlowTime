# UI-M2.8 — Charter Navigation & Tab Structure

**Status:** � In Progress (Phase 1 ✅ Complete, Phase 2 📋 Ready)  
**Dependencies:** ✅ M2.8 (Registry Integration), ✅ UI-M2.7 (Artifacts Registry UI)  
**Target:** Charter tab navigation system and UI migration framework  
**Date:** 2025-09-30

---

## Goal

Implement the **charter tab navigation structure** that enables seamless workflow transitions between [Models]→[Runs]→[Artifacts]→[Learn] tabs. This milestone creates the foundational navigation system that embodies the charter's artifacts-centric workflow paradigm and provides the UI framework for all future charter interactions.

**PHASE 1**: ✅ **COMPLETE** - Template API Integration migrated from hardcoded UI template generation to FlowTime-Sim API-driven templates.
**PHASE 2**: 📋 **READY** - Charter navigation system implementation.

## Context & Charter Alignment

The **Charter Roadmap M2.8** establishes the core registry integration and artifacts-centric workflow. **UI-M2.8** implements the user interface manifestation of this charter, creating the tab structure that guides users through the complete FlowTime workflow.

**Charter Role**: Provides the primary navigation framework that makes the charter **actionable** through intuitive UI patterns that match mental models of iterative workflow improvement.

## Functional Requirements

### **FR-UI-M2.8-0: Template API Integration (PHASE 1)**
**Priority**: Execute immediately after UI-M2.7 completion

Complete the migration from hardcoded UI template generation to FlowTime-Sim API-driven template system.

#### Technical Debt Resolution
- **Issue**: Template Studio currently uses hybrid architecture where template lists come from FlowTime-Sim API but YAML generation is hardcoded in UI
- **Root Cause**: UI was built to work independently while FlowTime-Sim APIs were being developed  
- **Impact**: Architectural inconsistency, duplicate template logic, maintenance burden
- **Bug Context**: YAML formatting bug in `GenerateTransportationYaml()` (fixed in UI-M2.7) highlights this architectural gap

#### Integration Requirements

**Replace UI YAML Generation with API Calls:**
```csharp
// BEFORE (UI-M2.7): Hardcoded generation in TemplateServiceImplementations.cs
private static string GenerateTransportationYaml(SimulationRunRequest request)
{
    var yaml = new StringBuilder();
    yaml.AppendLine("# Transportation Network - Generated Model");
    yaml.AppendLine($"grid:");
    yaml.AppendLine($"  bins: {simulationHours}");
    // ... hardcoded template logic
}

// AFTER (UI-M2.8): API-driven generation
public async Task<string> GenerateModelYamlAsync(SimulationRunRequest request)
{
    if (featureFlags.UseDemoMode)
    {
        return GenerateStaticDemoYaml(request); // Keep for demo mode
    }
    
    // Use FlowTime-Sim template generation API
    var response = await simClient.GenerateTemplateAsync(request.TemplateId, request.Parameters);
    return response.Value.Scenario;
}
```

**Demo Mode Preservation:**
- **Keep hardcoded templates for demo mode** - these provide value for offline demonstrations
- **Clear distinction**: Demo templates vs Live API templates
- **Mode detection**: Easy identification of template source in UI

**API Integration Points:**
- Use existing `POST /v1/sim/templates/{id}/generate` endpoint
- Remove hardcoded methods: `GenerateTransportationYaml()`, `GenerateITSystemYaml()`, etc.
- Migrate to schema-driven template system from FlowTime-Sim

#### Acceptance Criteria
- [x] All template YAML generation uses FlowTime-Sim API in API mode
- [x] Demo mode retains hardcoded templates with clear source indication
- [x] UI template generation methods preserved for demo mode fallbacks
- [x] Template Studio works identically to users (no UX regression)
- [x] Graceful API fallback ensures continuous user experience

---

### **FR-UI-M2.8-1: Charter Tab Navigation System**
Core navigation framework with tab-based charter workflow structure.

**Charter Layout Component:**
```csharp
// /Shared/CharterLayout.razor
@inherits LayoutView
@using FlowTime.UI.Services

<MudThemeProvider />
<MudDialogProvider />
<MudSnackbarProvider />

<MudLayout>
    <!-- Application Header -->
    <MudAppBar Elevation="1">
        <MudIconButton Icon="@Icons.Material.Filled.Menu" 
                       Color="Color.Inherit" 
                       Edge="Edge.Start" 
                       OnClick="@((e) => DrawerToggle())" />
        <MudSpacer />
        <MudText Typo="Typo.h6">FlowTime Charter</MudText>
        <MudSpacer />
        <MudIconButton Icon="@Icons.Material.Filled.Settings" 
                       Color="Color.Inherit" />
    </MudAppBar>

    <!-- Side Navigation Drawer -->
    <MudDrawer @bind-Open="_drawerOpen" Elevation="1" Variant="@DrawerVariant.Mini" OpenMiniOnHover="true">
        <MudDrawerHeader>
            <MudText Typo="Typo.h6">FlowTime</MudText>
        </MudDrawerHeader>
        <MudNavMenu>
            <MudNavLink Href="/charter" Match="NavLinkMatch.All" Icon="@Icons.Material.Filled.Dashboard">Charter</MudNavLink>
            <MudNavLink Href="/artifacts" Match="NavLinkMatch.Prefix" Icon="@Icons.Material.Filled.Archive">Artifacts</MudNavLink>
            <MudNavLink Href="/settings" Match="NavLinkMatch.Prefix" Icon="@Icons.Material.Filled.Settings">Settings</MudNavLink>
        </MudNavMenu>
    </MudDrawer>

    <!-- Main Content Area -->
    <MudMainContent>
        <MudContainer MaxWidth="MaxWidth.False" Class="ma-4">
            @Body
        </MudContainer>
    </MudMainContent>
</MudLayout>

@code {
    bool _drawerOpen = true;
    void DrawerToggle() => _drawerOpen = !_drawerOpen;
}
```

**Charter Tab Navigation Page:**
```csharp
// /Pages/Charter.razor
@page "/charter"
@page "/"
@using FlowTime.UI.Components.Charter
@using FlowTime.UI.Services

<MudContainer MaxWidth="MaxWidth.False">
    <MudStack Spacing="3">
        <!-- Charter Header -->
        <MudPaper Class="pa-4" Elevation="2">
            <MudStack Row Justify="Justify.SpaceBetween" AlignItems="Center.Center">
                <MudStack>
                    <MudText Typo="Typo.h4">FlowTime Charter</MudText>
                    <MudText Typo="Typo.body1" Color="Color.Secondary">
                        Artifacts-centric workflow: [Models] → [Runs] → [Artifacts] → [Learn]
                    </MudText>
                </MudStack>
                <MudStack Row Spacing="2">
                    <MudButton Variant="Variant.Outlined" 
                               StartIcon="@Icons.Material.Filled.History"
                               OnClick="ViewWorkflowHistory">History</MudButton>
                    <MudButton Variant="Variant.Filled" 
                               StartIcon="@Icons.Material.Filled.Add"
                               OnClick="StartNewWorkflow">New Workflow</MudButton>
                </MudStack>
            </MudStack>
        </MudPaper>

        <!-- Charter Tab Navigation -->
        <MudPaper Elevation="2" Class="charter-tabs-container">
            <MudTabs Elevation="0" 
                     Rounded="false" 
                     Border="false"
                     Class="charter-tabs"
                     ActivePanelIndex="activeTabIndex"
                     ActivePanelIndexChanged="OnTabChanged">
                
                <!-- [Models] Tab -->
                <MudTabPanel Text="Models" Icon="@Icons.Material.Filled.Schema">
                    <div class="tab-content-container pa-4">
                        <ModelsTabContent CurrentContext="@workflowContext" 
                                          OnModelSelected="HandleModelSelected"
                                          OnModelCreated="HandleModelCreated" />
                    </div>
                </MudTabPanel>

                <!-- [Runs] Tab -->
                <MudTabPanel Text="Runs" Icon="@Icons.Material.Filled.PlayArrow">
                    <div class="tab-content-container pa-4">
                        <RunsTabContent CurrentContext="@workflowContext"
                                        OnRunStarted="HandleRunStarted"
                                        OnRunCompleted="HandleRunCompleted" />
                    </div>
                </MudTabPanel>

                <!-- [Artifacts] Tab -->
                <MudTabPanel Text="Artifacts" Icon="@Icons.Material.Filled.Archive">
                    <div class="tab-content-container pa-4">
                        <ArtifactsTabContent CurrentContext="@workflowContext"
                                             OnArtifactSelected="HandleArtifactSelected"
                                             OnCompareRequested="HandleCompareRequested" />
                    </div>
                </MudTabPanel>

                <!-- [Learn] Tab -->
                <MudTabPanel Text="Learn" Icon="@Icons.Material.Filled.TrendingUp">
                    <div class="tab-content-container pa-4">
                        <LearnTabContent CurrentContext="@workflowContext"
                                         OnInsightDiscovered="HandleInsightDiscovered"
                                         OnRecommendationApplied="HandleRecommendationApplied" />
                    </div>
                </MudTabPanel>
            </MudTabs>
        </MudPaper>

        <!-- Workflow Context Status Bar -->
        <MudPaper Class="pa-3" Elevation="1" Style="background: var(--mud-palette-background-grey);">
            <WorkflowContextStatus Context="@workflowContext" OnContextReset="ResetWorkflowContext" />
        </MudPaper>
    </MudStack>
</MudContainer>

<style>
.charter-tabs-container {
    min-height: 60vh;
}

.charter-tabs .mud-tabs-toolbar {
    background: var(--mud-palette-surface);
    border-bottom: 1px solid var(--mud-palette-divider);
}

.tab-content-container {
    min-height: 50vh;
    background: var(--mud-palette-background);
}
</style>

@code {
    private int activeTabIndex = 0;
    private WorkflowContext workflowContext = new();

    private async Task OnTabChanged(int newIndex)
    {
        activeTabIndex = newIndex;
        await UpdateWorkflowContext(newIndex);
    }

    private async Task UpdateWorkflowContext(int tabIndex)
    {
        // Update context based on tab progression
        switch (tabIndex)
        {
            case 0: // Models
                workflowContext.CurrentStage = WorkflowStage.Models;
                break;
            case 1: // Runs
                workflowContext.CurrentStage = WorkflowStage.Runs;
                break;
            case 2: // Artifacts
                workflowContext.CurrentStage = WorkflowStage.Artifacts;
                break;
            case 3: // Learn
                workflowContext.CurrentStage = WorkflowStage.Learn;
                break;
        }
        
        StateHasChanged();
    }
}
```

### **FR-UI-M2.8-2: Charter Workflow Context System**
Workflow state management that persists across tab transitions and maintains charter progression context.

**Workflow Context Service:**
```csharp
// /Services/IWorkflowContextService.cs
public interface IWorkflowContextService
{
    WorkflowContext CurrentContext { get; }
    Task<WorkflowContext> CreateNewWorkflowAsync();
    Task UpdateContextAsync(WorkflowContext context);
    Task<IEnumerable<WorkflowContext>> GetRecentWorkflowsAsync();
    Task SaveWorkflowAsync(WorkflowContext context);
    event EventHandler<WorkflowContextChangedEventArgs> ContextChanged;
}

// /Models/WorkflowContext.cs
public class WorkflowContext
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = $"Workflow {DateTime.Now:yyyy-MM-dd HH:mm}";
    public WorkflowStage CurrentStage { get; set; } = WorkflowStage.Models;
    public DateTime Created { get; set; } = DateTime.UtcNow;
    public DateTime LastModified { get; set; } = DateTime.UtcNow;

    // Charter Stage Context
    public ModelContext? SelectedModel { get; set; }
    public RunContext? ActiveRun { get; set; }
    public ArtifactContext? SelectedArtifacts { get; set; } = new();
    public LearnContext? Insights { get; set; } = new();

    // Navigation State
    public Dictionary<string, object> TabState { get; set; } = new();
    public List<WorkflowAction> ActionHistory { get; set; } = new();
}

public enum WorkflowStage
{
    Models,
    Runs, 
    Artifacts,
    Learn
}

public class ModelContext
{
    public string? ModelPath { get; set; }
    public string? ModelType { get; set; }
    public Dictionary<string, object> Parameters { get; set; } = new();
    public bool IsValid { get; set; }
}

public class RunContext  
{
    public string? RunId { get; set; }
    public RunStatus Status { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public Dictionary<string, object> Configuration { get; set; } = new();
}

public class ArtifactContext
{
    public List<string> SelectedArtifactIds { get; set; } = new();
    public string? CompareBaselineId { get; set; }
    public List<string> DownloadQueue { get; set; } = new();
}
```

### **FR-UI-M2.8-3: Charter Tab Content Components**
Modular tab content components that implement charter workflow stages.

**Models Tab Content:**
```csharp
// /Components/Charter/ModelsTabContent.razor
<MudStack Spacing="3">
    <!-- Model Selection/Creation Header -->
    <MudStack Row Justify="Justify.SpaceBetween" AlignItems="Center.Center">
        <MudText Typo="Typo.h5">Model Selection</MudText>
        <MudButtonGroup Variant="Variant.Outlined">
            <MudButton StartIcon="@Icons.Material.Filled.Upload" OnClick="ImportModel">Import</MudButton>
            <MudButton StartIcon="@Icons.Material.Filled.Add" OnClick="CreateModel">Create</MudButton>
        </MudButtonGroup>
    </MudStack>

    <!-- Recent Models Quick Access -->
    <MudPaper Class="pa-4">
        <MudText Typo="Typo.h6" Class="mb-3">Recent Models</MudText>
        <MudGrid>
            @foreach (var model in recentModels)
            {
                <MudItem xs="12" sm="6" md="4">
                    <MudCard Outlined="true" Class="cursor-pointer" @onclick="@(() => SelectModel(model))">
                        <MudCardContent>
                            <MudStack Row AlignItems="Center.Center" Spacing="2">
                                <MudIcon Icon="@Icons.Material.Filled.Schema" />
                                <div>
                                    <MudText Typo="Typo.subtitle1">@model.Name</MudText>
                                    <MudText Typo="Typo.caption" Color="Color.Secondary">@model.Type</MudText>
                                </div>
                            </MudStack>
                        </MudCardContent>
                    </MudCard>
                </MudItem>
            }
        </MudGrid>
    </MudPaper>

    <!-- Model Configuration Panel -->
    @if (CurrentContext?.SelectedModel != null)
    {
        <MudPaper Class="pa-4">
            <MudText Typo="Typo.h6" Class="mb-3">Model Configuration</MudText>
            <ModelConfigurationForm Model="@CurrentContext.SelectedModel" 
                                    OnConfigurationChanged="HandleModelConfigurationChanged" />
        </MudPaper>
    }

    <!-- Charter Progression Actions -->
    <MudPaper Class="pa-4" Style="background: var(--mud-palette-action-hover);">
        <MudStack Row Justify="Justify.SpaceBetween" AlignItems="Center.Center">
            <MudText Typo="Typo.body1">
                @if (CurrentContext?.SelectedModel != null)
                {
                    <MudIcon Icon="@Icons.Material.Filled.CheckCircle" Color="Color.Success" Size="Size.Small" />
                    <span>Model ready: @CurrentContext.SelectedModel.ModelPath</span>
                }
                else
                {
                    <span>Select or create a model to continue...</span>
                }
            </MudText>
            <MudButton Variant="Variant.Filled" 
                       Disabled="@(CurrentContext?.SelectedModel == null)"
                       StartIcon="@Icons.Material.Filled.ArrowForward"
                       OnClick="ProceedToRuns">
                Continue to Runs
            </MudButton>
        </MudStack>
    </MudPaper>
</MudStack>
```

**Runs Tab Content:**
```csharp
// /Components/Charter/RunsTabContent.razor  
<MudStack Spacing="3">
    <!-- Run Configuration Wizard -->
    <MudPaper Class="pa-4">
        <MudText Typo="Typo.h5" Class="mb-3">Run Configuration</MudText>
        
        @if (CurrentContext?.SelectedModel != null)
        {
            <MudAlert Severity="Severity.Info" Class="mb-3">
                Using model: <strong>@CurrentContext.SelectedModel.ModelPath</strong>
            </MudAlert>
            
            <RunConfigurationWizard ModelContext="@CurrentContext.SelectedModel"
                                    OnConfigurationComplete="HandleRunConfigurationComplete"
                                    OnRunStarted="HandleRunStarted" />
        }
        else
        {
            <MudAlert Severity="Severity.Warning">
                No model selected. Return to the Models tab to select a model first.
            </MudAlert>
        }
    </MudPaper>

    <!-- Active Run Monitoring -->
    @if (CurrentContext?.ActiveRun != null)
    {
        <MudPaper Class="pa-4">
            <MudText Typo="Typo.h6" Class="mb-3">Active Run</MudText>
            <RunMonitoringPanel RunContext="@CurrentContext.ActiveRun" 
                                OnRunCompleted="HandleRunCompleted" />
        </MudPaper>
    }

    <!-- Run History -->
    <MudPaper Class="pa-4">
        <MudText Typo="Typo.h6" Class="mb-3">Recent Runs</MudText>
        <RecentRunsList OnRunSelected="HandlePreviousRunSelected" />
    </MudPaper>
</MudStack>
```

### **FR-UI-M2.8-4: Charter Navigation State Persistence**
Navigation state persistence and restoration across browser sessions.

**Navigation State Service:**
```csharp
// /Services/NavigationStateService.cs
public class NavigationStateService : INavigationStateService
{
    private readonly IJSRuntime _jsRuntime;
    private readonly ILogger<NavigationStateService> _logger;

    public async Task SaveNavigationStateAsync(WorkflowContext context)
    {
        try
        {
            var json = JsonSerializer.Serialize(context, JsonOptions);
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "flowtime_workflow_context", json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save navigation state");
        }
    }

    public async Task<WorkflowContext?> RestoreNavigationStateAsync()
    {
        try
        {
            var json = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", "flowtime_workflow_context");
            if (string.IsNullOrEmpty(json))
                return null;

            return JsonSerializer.Deserialize<WorkflowContext>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restore navigation state");
            return null;
        }
    }
}
```

## Integration Points

### **UI-M2.7 Artifacts Integration**
- Artifacts tab content leverages UI-M2.7 artifact browser components
- Artifact selector components used in Models and Runs tab workflows
- Consistent artifact management across all charter tabs

### **M2.8 Registry API Integration**
- Charter workflow context synchronized with M2.8 registry services
- Model and run metadata stored through registry APIs
- Workflow progression tracked via registry audit logs

### **Future UI Milestone Preparation**
- Tab content components designed for UI-M2.9 compare workflow integration
- Workflow context system ready for UI-M3.0 cross-platform scenarios
- Navigation framework supports dynamic tab addition for future capabilities

## Technical Debt Documentation

### **Template API Integration Debt**
**Identified**: September 24, 2025 during UI-M2.7 testing  
**Context**: YAML formatting bug in Transportation Network template revealed architectural inconsistency

#### Current State (Post UI-M2.7)
- ✅ **Template Lists**: Retrieved from FlowTime-Sim API (`GET /v1/sim/templates`)
- ❌ **YAML Generation**: Hardcoded in UI (`GenerateTransportationYaml()`, etc.)
- ✅ **Bug Fixed**: YAML formatting issue resolved in `TemplateServiceImplementations.cs`

#### API Readiness Assessment
- ✅ FlowTime-Sim has `POST /v1/sim/templates/{id}/generate` endpoint
- ✅ SIM-SVC-M2 and SIM-CAT-M2 provide complete template infrastructure
- ✅ UI already calls FlowTime-Sim for template discovery
- ❌ UI bypasses FlowTime-Sim for template generation (inconsistent architecture)

#### Migration Priority
- **Impact**: Medium (architectural consistency, maintenance burden)
- **Risk**: Low (current hybrid approach works correctly)
- **Effort**: Medium (requires API integration changes, not UX changes) 
- **Timing**: UI-M2.8 Phase 1 (before charter navigation work)

#### Value Proposition
- **Consistency**: Single source of truth for templates in FlowTime-Sim
- **Maintainability**: Remove duplicate template logic from UI
- **Extensibility**: New templates only need FlowTime-Sim changes
- **Demo Mode**: Preserve hardcoded templates for offline demonstrations

---

## Acceptance Criteria

### **Charter Navigation Functionality**
- ✅ Charter tab navigation works smoothly with proper state management
- ✅ Workflow context persists across tab transitions and browser sessions
- ✅ Tab content reflects current workflow stage and available actions
- ✅ Charter progression indicators guide users through workflow stages

### **Workflow Context Management** 
- ✅ Workflow context accurately tracks Models→Runs→Artifacts→Learn progression
- ✅ Multiple concurrent workflows can be managed simultaneously
- ✅ Workflow history is accessible and workflows can be resumed
- ✅ Context synchronization with registry APIs maintains data consistency

### **User Experience Excellence**
- ✅ Charter navigation is intuitive and requires minimal training
- ✅ Tab transitions are fast and responsive (< 200ms)
- ✅ Error states and loading indicators provide clear feedback
- ✅ Mobile-responsive design works effectively on tablets

### **Charter Workflow Integrity**
- ✅ Charter workflow prevents invalid progressions (e.g., Runs without Models)
- ✅ Context validation ensures workflow integrity at each stage
- ✅ Charter "never forget" principle maintained through persistent context
- ✅ Workflow actions create proper audit trails. ⚠️ Artifact relationship visuals/actions depend on the deferred UI work scheduled in UI-M2.9.

## Implementation Status

### ✅ **Phase 1: Template API Integration - COMPLETE**
**Completed:** 2025-09-25  
**Branch:** `feature/ui-m2.8/template-api-integration`

**Key Achievements:**
- ✅ **API Integration**: Migrated template generation from hardcoded UI to FlowTime-Sim API calls
- ✅ **Endpoint Integration**: Implemented `POST /v1/sim/templates/{id}/generate` endpoint usage
- ✅ **Graceful Fallback**: Added robust fallback to hardcoded templates for demo mode
- ✅ **Feature Flag Control**: Demo mode preserved via `UseFlowTimeSimApi` feature flag
- ✅ **Error Resilience**: API failures gracefully fall back without user disruption
- ✅ **Test Coverage**: Comprehensive tests for API integration and fallback scenarios

**Technical Debt Resolved:**
- ❌ **Removed**: Architectural inconsistency between UI and API template generation
- ❌ **Eliminated**: Duplicate template logic maintenance burden

**Downstream Impact:**
- 📋 **FlowTime-Sim API Cleanup**: Success of this integration enables removal of deprecated `/v1/sim/scenarios` endpoints (documented in SIM-M2.9 FR-1)
- ✅ **Preserved**: Demo mode functionality for offline demonstrations
- ✅ **Maintained**: Zero breaking changes to existing user experience

**Code Changes:**
- **Modified**: `GenerateModelYamlAsync()` now uses `simClient.GenerateTemplateAsync()`
- **Added**: Feature flag-controlled API vs hardcoded template selection
- **Preserved**: All hardcoded template methods for demo mode fallback
- **Enhanced**: Error handling with debug logging and graceful degradation

### 📋 **Phase 2: Charter Navigation Framework - READY TO START**
**Dependencies:** ✅ Phase 1 Complete, ✅ M2.8 Complete, ✅ UI-M2.7 Complete

---

## Implementation Plan

### **Phase 1: Template API Integration** 
**Status:** ✅ **COMPLETE** - 2025-09-25**

#### 1.1 API Service Updates
```csharp
// Update FlowTimeSimService.GenerateModelYamlAsync()
public async Task<string> GenerateModelYamlAsync(SimulationRunRequest request)
{
    if (featureFlags.UseDemoMode)
    {
        // Keep hardcoded templates for demo mode
        return GenerateStaticDemoYaml(request);
    }
    
    // Use FlowTime-Sim API for live templates
    var apiResult = await simClient.GenerateTemplateAsync(request.TemplateId, request.Parameters);
    if (!apiResult.Success)
    {
        throw new InvalidOperationException($"Template generation failed: {apiResult.Error}");
    }
    
    return apiResult.Value.Scenario;
}
```

#### 1.2 Demo Mode Refactoring
- Extract hardcoded template methods to `DemoTemplateService`
- Maintain template quality for offline demonstrations  
- Add clear UI indicators: "Demo Templates" vs "Live Templates"

#### 1.3 Code Cleanup
- Remove: `GenerateTransportationYaml()`, `GenerateITSystemYaml()`, etc.
- Update: Template generation calls to use API service
- Test: Both demo and API modes work identically from user perspective

#### 1.4 Integration Testing
- Verify FlowTime-Sim API integration works correctly
- Ensure demo mode fallback maintains UX quality
- Test error handling when API unavailable

### **Phase 2: Charter Navigation Framework**
1. **Charter layout component** with app-wide navigation structure
2. **Tab navigation system** with charter workflow stages
3. **Basic workflow context** model and state management
4. **Navigation persistence** using browser localStorage

### **Phase 3: Charter Tab Content**
1. **Models tab content** with model selection and configuration
2. **Runs tab content** with run wizard and monitoring
3. **Artifacts tab content** integration with UI-M2.7 components
4. **Learn tab content** foundation for future analytics

### **Phase 4: Workflow Context System**
1. **Advanced workflow context** management with validation
2. **Multi-workflow support** and workflow history
3. **Registry API integration** for context persistence
4. **Context synchronization** across browser tabs

### **Phase 5: User Experience Polish**
1. **Charter progression indicators** and workflow guidance
2. **Advanced navigation features** (bookmarks, shortcuts)
3. **Performance optimization** for large workflow contexts
4. **Accessibility improvements** and keyboard navigation

---

## Next Steps

1. **UI-M2.9**: Compare workflow UI that leverages charter navigation context
2. **UI-M3.0**: Cross-platform charter integration with Sim UI components
3. **Enhanced workflows**: Advanced charter capabilities building on navigation foundation

This milestone creates the **navigational foundation** for the charter's artifacts-centric workflow, making the charter's mental model actionable through intuitive UI that guides users through iterative improvement cycles.

---

## Summary of Changes

### **Template API Integration (Phase 1)**
**Immediate Priority**: Addresses technical debt identified during UI-M2.7 testing

**Current State**:
- ✅ YAML formatting bug fixed in `TemplateServiceImplementations.cs`
- ❌ Architectural inconsistency: Template lists from API, YAML generation hardcoded
- ✅ FlowTime-Sim APIs ready: `POST /v1/sim/templates/{id}/generate`

**Phase 1 Deliverables**:
- Migrate all template YAML generation to FlowTime-Sim API calls
- Preserve hardcoded templates for demo mode (clearly distinguished)
- Remove duplicate template logic from UI codebase
- Maintain identical user experience (no UX regression)

**Value**: Consistent architecture, reduced maintenance burden, single source of truth for templates

### **Charter Navigation (Phases 2-5)**
**Charter Foundation**: Core navigation system for [Models]→[Runs]→[Artifacts]→[Learn] workflow

Builds on the clean template architecture from Phase 1 to provide the charter's artifacts-centric navigation paradigm.
