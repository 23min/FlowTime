# UI-M2.7 — Artifacts Registry UI

**Status:** 📋 Planned (Charter-Aligned)  
**Dependencies:** M2.7 (Artifacts Registry API), UI-M2.6 (Export UI Integration)  
**Target:** User interface for artifacts registry browsing and management  
**Date:** 2025-09-25

---

## Goal

Implement comprehensive **artifacts registry user interface** that enables users to browse, search, filter, and manage artifacts from the FlowTime Engine registry. This milestone creates the UI foundation for the charter's "never forget" principle by making all artifacts discoverable and accessible through intuitive interfaces.

## Context & Charter Alignment

The **Engine M2.7 Artifacts Registry** provides the API and data layer for persistent artifact storage. **UI-M2.7** implements the user-facing interfaces that make this registry accessible and useful for daily workflows.

**Charter Role**: Enables the **[Artifacts]** tab in charter navigation and provides artifact browsing throughout the charter workflow.

## Functional Requirements

### **FR-UI-M2.7-1: Artifacts Browser Interface**
Comprehensive artifacts browsing interface with search, filtering, and management capabilities.

**Main Artifacts Browser (`/artifacts`):**
```csharp
// /Pages/Artifacts.razor
@page "/artifacts"
@using FlowTime.UI.Services.Artifacts

<MudContainer MaxWidth="MaxWidth.False">
    <MudStack Spacing="3">
        <!-- Artifacts Browser Header -->
        <MudPaper Class="pa-4">
            <MudStack Row Justify="Justify.SpaceBetween" AlignItems="Center.Center">
                <MudText Typo="Typo.h4">Artifacts Registry</MudText>
                <MudStack Row Spacing="2">
                    <MudButton Variant="Variant.Outlined" StartIcon="@Icons.Material.Filled.Refresh"
                               OnClick="RefreshArtifacts">Refresh</MudButton>
                    <MudButton Variant="Variant.Filled" StartIcon="@Icons.Material.Filled.Add"
                               OnClick="OpenImportDialog">Import Artifacts</MudButton>
                </MudStack>
            </MudStack>
        </MudPaper>

        <!-- Search and Filter Panel -->
        <MudPaper Class="pa-4">
            <MudGrid>
                <MudItem xs="12" sm="6" md="4">
                    <MudTextField @bind-Value="searchQuery" 
                                  Label="Search artifacts..." 
                                  Variant="Variant.Outlined"
                                  Adornment="Adornment.Start" 
                                  AdornmentIcon="@Icons.Material.Filled.Search"
                                  OnKeyPress="HandleSearchKeyPress" />
                </MudItem>
                <MudItem xs="12" sm="3" md="2">
                    <MudSelect @bind-Value="selectedType" Label="Type" Variant="Variant.Outlined">
                        <MudSelectItem Value="@("all")">All Types</MudSelectItem>
                        <MudSelectItem Value="@("run")">Runs</MudSelectItem>
                        <MudSelectItem Value="@("model")">Models</MudSelectItem>
                        <MudSelectItem Value="@("telemetry")">Telemetry</MudSelectItem>
                    </MudSelect>
                </MudItem>
                <MudItem xs="12" sm="3" md="2">
                    <MudDatePicker @bind-Date="dateFilter" Label="Created After" Variant="Variant.Outlined" />
                </MudItem>
                <MudItem xs="12" md="4">
                    <MudChipSet @bind-SelectedValues="selectedTags" MultiSelection="true" Filter="true">
                        @foreach (var tag in availableTags)
                        {
                            <MudChip Text="@tag" Value="@tag" />
                        }
                    </MudChipSet>
                </MudItem>
            </MudGrid>
        </MudPaper>

        <!-- Artifacts Grid/List View -->
        <MudPaper Class="pa-4">
            @if (isLoading)
            {
                <MudProgressLinear Color="Color.Primary" Indeterminate="true" />
            }
            else if (filteredArtifacts?.Any() == true)
            {
                <MudDataGrid Items="@filteredArtifacts" 
                             Sortable="true" 
                             Filterable="false"
                             QuickFilter="@quickFilter"
                             Hover="true"
                             ReadOnly="true"
                             RowClick="@OnArtifactRowClick">
                    <Columns>
                        <PropertyColumn Property="x => x.Type" Title="Type">
                            <CellTemplate>
                                <MudIcon Icon="@GetArtifactIcon(context.Item.Type)" Size="Size.Small" />
                                <span class="ml-2">@context.Item.Type.ToUpper()</span>
                            </CellTemplate>
                        </PropertyColumn>
                        <PropertyColumn Property="x => x.Title" Title="Title" />
                        <PropertyColumn Property="x => x.Description" Title="Description" />
                        <PropertyColumn Property="x => x.Created" Title="Created" Format="yyyy-MM-dd HH:mm" />
                        <PropertyColumn Property="x => x.Tags" Title="Tags">
                            <CellTemplate>
                                @foreach (var tag in context.Item.Tags)
                                {
                                    <MudChip Size="Size.Small" Label="true" Text="@tag" />
                                }
                            </CellTemplate>
                        </PropertyColumn>
                        <PropertyColumn Property="x => x.Source" Title="Source" />
                        <TemplateColumn Title="Actions" Sortable="false">
                            <CellTemplate>
                                <MudButtonGroup Variant="Variant.Text" Size="Size.Small">
                                    <MudButton StartIcon="@Icons.Material.Filled.Visibility" 
                                               OnClick="@(() => ViewArtifact(context.Item.Id))">View</MudButton>
                                    <MudButton StartIcon="@Icons.Material.Filled.Compare" 
                                               OnClick="@(() => CompareArtifact(context.Item.Id))">Compare</MudButton>
                                    <MudButton StartIcon="@Icons.Material.Filled.Download" 
                                               OnClick="@(() => DownloadArtifact(context.Item.Id))">Download</MudButton>
                                </MudButtonGroup>
                            </CellTemplate>
                        </TemplateColumn>
                    </Columns>
                </MudDataGrid>
            }
            else
            {
                <MudAlert Severity="Severity.Info">
                    No artifacts found matching your criteria. Try adjusting your search filters.
                </MudAlert>
            }
        </MudPaper>
    </MudStack>
</MudContainer>
```

### **FR-UI-M2.7-2: Artifact Detail View**
Detailed artifact viewing interface with metadata, file listing, and contextual actions.

**Artifact Detail Page (`/artifacts/{id}`):**
```csharp
// /Pages/ArtifactDetail.razor
@page "/artifacts/{artifactId}"
@using FlowTime.UI.Services.Artifacts

<MudContainer MaxWidth="MaxWidth.False">
    @if (artifact != null)
    {
        <MudStack Spacing="3">
            <!-- Artifact Header -->
            <MudPaper Class="pa-4">
                <MudStack Row Justify="Justify.SpaceBetween" AlignItems="Center.Center">
                    <MudStack>
                        <MudStack Row AlignItems="Center.Center" Spacing="2">
                            <MudIcon Icon="@GetArtifactIcon(artifact.Type)" Size="Size.Large" />
                            <MudText Typo="Typo.h4">@artifact.Title</MudText>
                            <MudChip Size="Size.Medium" Label="true" Text="@artifact.Type.ToUpper()" />
                        </MudStack>
                        <MudText Typo="Typo.body1" Color="Color.Secondary">@artifact.Description</MudText>
                    </MudStack>
                    <MudStack Row Spacing="2">
                        <MudButton Variant="Variant.Filled" 
                                   StartIcon="@Icons.Material.Filled.Compare"
                                   OnClick="StartCompare">Compare</MudButton>
                        <MudButton Variant="Variant.Outlined" 
                                   StartIcon="@Icons.Material.Filled.Download"
                                   OnClick="DownloadArtifact">Download</MudButton>
                        <MudButton Variant="Variant.Text" 
                                   StartIcon="@Icons.Material.Filled.Edit"
                                   OnClick="EditMetadata">Edit</MudButton>
                    </MudStack>
                </MudStack>
            </MudPaper>

            <!-- Metadata and Files Tabs -->
            <MudTabs Elevation="2" Rounded="true" ApplyEffectsToContainer="true" PanelClass="pa-6">
                <MudTabPanel Text="Metadata">
                    <MudGrid>
                        <MudItem xs="12" md="6">
                            <MudStack Spacing="2">
                                <MudField Label="Artifact ID" Variant="Variant.Outlined">@artifact.Id</MudField>
                                <MudField Label="Created" Variant="Variant.Outlined">@artifact.Created.ToString("yyyy-MM-dd HH:mm:ss")</MudField>
                                <MudField Label="Source" Variant="Variant.Outlined">@artifact.Source</MudField>
                                <MudField Label="Schema Version" Variant="Variant.Outlined">@artifact.SchemaVersion</MudField>
                            </MudStack>
                        </MudItem>
                        <MudItem xs="12" md="6">
                            <MudStack Spacing="2">
                                <MudField Label="Tags" Variant="Variant.Outlined">
                                    @foreach (var tag in artifact.Tags)
                                    {
                                        <MudChip Size="Size.Small" Text="@tag" />
                                    }
                                </MudField>
                                <MudField Label="Capabilities" Variant="Variant.Outlined">
                                    @string.Join(", ", artifact.Capabilities)
                                </MudField>
                                @if (artifact.Relationships?.Any() == true)
                                {
                                    <MudField Label="Related Artifacts" Variant="Variant.Outlined">
                                        @foreach (var related in artifact.Relationships)
                                        {
                                            <MudLink Href="@($"/artifacts/{related}")">@related</MudLink>
                                        }
                                    </MudField>
                                }
                            </MudStack>
                        </MudItem>
                    </MudGrid>
                </MudTabPanel>
                
                <MudTabPanel Text="Files">
                    <MudDataGrid Items="@artifactFiles" Hover="true" ReadOnly="true">
                        <Columns>
                            <PropertyColumn Property="x => x.Name" Title="File Name" />
                            <PropertyColumn Property="x => x.Size" Title="Size" Format="N0" />
                            <PropertyColumn Property="x => x.Type" Title="Type" />
                            <TemplateColumn Title="Actions" Sortable="false">
                                <CellTemplate>
                                    <MudButtonGroup Size="Size.Small">
                                        <MudButton StartIcon="@Icons.Material.Filled.Visibility"
                                                   OnClick="@(() => PreviewFile(context.Item.Name))">Preview</MudButton>
                                        <MudButton StartIcon="@Icons.Material.Filled.Download"
                                                   OnClick="@(() => DownloadFile(context.Item.Name))">Download</MudButton>
                                    </MudButtonGroup>
                                </CellTemplate>
                            </TemplateColumn>
                        </Columns>
                    </MudDataGrid>
                </MudTabPanel>
            </MudTabs>
        </MudStack>
    }
    else
    {
        <MudProgressCircular Indeterminate="true" />
    }
</MudContainer>
```

### **FR-UI-M2.7-3: Artifact Services and Integration**
Client-side services for artifact registry API integration.

**Artifact Service Interface:**
```csharp
// /Services/IArtifactBrowserService.cs
public interface IArtifactBrowserService
{
    Task<IEnumerable<ArtifactSummary>> ListArtifactsAsync(ArtifactFilter filter = null);
    Task<ArtifactDetail> GetArtifactAsync(string id);
    Task<IEnumerable<ArtifactFile>> GetArtifactFilesAsync(string id);
    Task<Stream> DownloadFileAsync(string id, string fileName);
    Task<bool> UpdateMetadataAsync(string id, ArtifactMetadataUpdate update);
    Task<bool> DeleteArtifactAsync(string id);
    Task<IEnumerable<string>> GetAvailableTagsAsync();
    Task RefreshRegistryAsync();
}

// /Services/ArtifactBrowserService.cs
public class ArtifactBrowserService : IArtifactBrowserService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ArtifactBrowserService> _logger;

    public async Task<IEnumerable<ArtifactSummary>> ListArtifactsAsync(ArtifactFilter filter = null)
    {
        var query = BuildQueryString(filter);
        var response = await _httpClient.GetAsync($"/v1/artifacts{query}");
        response.EnsureSuccessStatusCode();
        
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<IEnumerable<ArtifactSummary>>(json, JsonOptions);
    }

    private string BuildQueryString(ArtifactFilter filter)
    {
        if (filter == null) return "";
        
        var query = new List<string>();
        if (!string.IsNullOrEmpty(filter.Type)) query.Add($"type={filter.Type}");
        if (!string.IsNullOrEmpty(filter.Search)) query.Add($"search={filter.Search}");
        if (filter.Tags?.Any() == true) query.Add($"tags={string.Join(",", filter.Tags)}");
        if (filter.CreatedAfter.HasValue) query.Add($"after={filter.CreatedAfter:yyyy-MM-dd}");
        
        return query.Any() ? "?" + string.Join("&", query) : "";
    }
}
```

### **FR-UI-M2.7-4: Charter Integration Components**
Reusable UI components for artifact selection throughout charter workflows.

**Artifact Selector Component:**
```csharp
// /Components/ArtifactSelector.razor
<MudStack Spacing="2">
    <MudTextField @bind-Value="searchText" 
                  Label="Search artifacts..." 
                  Variant="Variant.Outlined"
                  Adornment="Adornment.Start" 
                  AdornmentIcon="@Icons.Material.Filled.Search" />
    
    <MudSelect @bind-Value="selectedArtifactId" 
               Label="@Label" 
               Variant="Variant.Outlined"
               ToStringFunc="@(id => GetArtifactDisplayName(id))">
        @foreach (var artifact in filteredArtifacts)
        {
            <MudSelectItem Value="@artifact.Id">
                <div class="d-flex align-center">
                    <MudIcon Icon="@GetArtifactIcon(artifact.Type)" Size="Size.Small" Class="mr-2" />
                    <div>
                        <div>@artifact.Title</div>
                        <MudText Typo="Typo.caption" Color="Color.Secondary">@artifact.Type • @artifact.Created.ToString("MM/dd/yyyy")</MudText>
                    </div>
                </div>
            </MudSelectItem>
        }
    </MudSelect>
</MudStack>

@code {
    [Parameter] public string Label { get; set; } = "Select Artifact";
    [Parameter] public string SelectedArtifactId { get; set; }
    [Parameter] public EventCallback<string> SelectedArtifactIdChanged { get; set; }
    [Parameter] public string[] AllowedTypes { get; set; } = null; // null = all types
    
    private string searchText = "";
    private IEnumerable<ArtifactSummary> artifacts = new List<ArtifactSummary>();
    private IEnumerable<ArtifactSummary> filteredArtifacts => FilterArtifacts();
}
```

## Integration Points

### **M2.7 Artifacts Registry API Integration**
- All UI components consume M2.7 registry REST API endpoints
- UI service layer abstracts API details from Blazor components
- Error handling and loading states for API operations

### **Charter Navigation Integration**  
- Artifacts browser accessible via charter `[Artifacts]` tab
- Artifact selector components used in `[Runs]` wizard input selection
- Contextual artifact actions integrate with charter workflows

### **UI-M2.8 Charter Navigation Preparation**
- Components designed for integration with UI-M2.8 tab structure
- Consistent design language and interaction patterns
- Reusable artifact components for use across charter tabs

## Acceptance Criteria

### **Artifacts Browser Functionality**
- ✅ Users can browse all artifacts with search, filter, and sort capabilities
- ✅ Artifact detail view shows complete metadata and file listings
- ✅ File download and preview functionality works for all artifact types
- ✅ Registry refresh and import functionality accessible from UI

### **Charter Workflow Integration**
- ✅ Artifact selector components work in Runs wizard input selection
- ✅ Contextual actions (Compare, Download) launch appropriate workflows
- ✅ Artifacts browser integrates seamlessly with charter navigation
- ✅ UI performance remains responsive with 1000+ artifacts

### **User Experience**
- ✅ Artifact browsing is intuitive and requires minimal training
- ✅ Search and filtering help users find artifacts quickly
- ✅ Error states and loading indicators provide clear feedback
- ✅ Mobile-responsive design works on tablets and smaller screens

## Implementation Plan

### **Phase 1: Core Browser Components**
1. **Artifacts browser page** with search, filter, and grid display
2. **Artifact detail view** with metadata and file management
3. **Basic artifact service** integration with M2.7 API
4. **Component styling** and responsive design

### **Phase 2: Advanced Features**
1. **Artifact selector components** for charter workflow integration
2. **File preview capabilities** for common file types (CSV, YAML, JSON)
3. **Bulk operations** and artifact management features
4. **Performance optimization** for large artifact collections

### **Phase 3: Charter Integration**
1. **Navigation integration** preparation for UI-M2.8 charter tabs
2. **Contextual actions** integration with compare and export workflows  
3. **Cross-component consistency** and design system alignment
4. **Accessibility improvements** and keyboard navigation

### **Phase 4: Testing and Polish**
1. **End-to-end testing** with M2.7 registry API
2. **Performance testing** with large artifact datasets
3. **User experience refinement** based on usability testing
4. **Documentation** for artifact management workflows

---

## Next Steps

1. **UI-M2.8**: Charter navigation structure and tab migration
2. **UI-M2.9**: Compare workflow UI integration with artifact selection
3. **Cross-platform integration**: Coordination with Sim UI development

This milestone establishes the **user interface foundation** for the charter's "never forget" principle, making all FlowTime artifacts discoverable and manageable through intuitive interfaces.
