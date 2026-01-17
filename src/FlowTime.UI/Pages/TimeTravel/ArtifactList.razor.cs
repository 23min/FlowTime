using System.Globalization;
using System.Linq;
using System.Text.Json;
using FlowTime.UI.Components.Dialogs;
using FlowTime.UI.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace FlowTime.UI.Pages.TimeTravel;

public sealed partial class ArtifactList : ComponentBase
{
    internal const string LocalStorageKey = "time-travel:artifacts:state";
    internal const int DefaultPageSize = 100;

    private readonly Dictionary<string, RunCreateResponseDto> detailCache = new(StringComparer.OrdinalIgnoreCase);

    private ArtifactListState state = new(Array.Empty<RunListEntry>()) { PageSize = DefaultPageSize };
    private ArtifactListResult view = new(Array.Empty<RunListEntry>(), 0, 1, 1);
    private IReadOnlyList<RunListEntry> runs = Array.Empty<RunListEntry>();
    private IReadOnlyList<RunDiagnostic> discoveryDiagnostics = Array.Empty<RunDiagnostic>();
    private RunDiscoveryResult? discoveryResult;

    private bool isLoading;
    private string? errorMessage;
    private string searchInput = string.Empty;
    private Dictionary<ArtifactRunStatus, int> statusFacets = new();
    private int warningsWithIssues;
    private int warningsClear;
    private int simulationModeCount;
    private int telemetryAvailable;
    private bool hasActiveFilters;
    private ArtifactViewMode viewMode = ArtifactViewMode.Cards;
    private readonly HashSet<string> selectedRunIds = new(StringComparer.OrdinalIgnoreCase);
    private bool isDeletingArtifacts;

    private bool isDrawerOpen;
    private RunListEntry? selectedRun;
    private RunCreateResponseDto? selectedDetail;
    private bool isDetailLoading;
    private string? detailError;
    private TelemetryCaptureSummaryDto? captureSummary;
    private bool isGeneratingTelemetry;
    private bool overwriteCapture;

    internal bool IsDrawerOpen => isDrawerOpen;

    private bool CanLoadModel =>
        (selectedDetail?.CanReplay ?? selectedRun?.CanReplay) == true;

    private bool CanLoadTelemetry
    {
        get
        {
            if (!CanLoadModel)
            {
                return false;
            }

            var telemetry = selectedDetail?.Telemetry ?? selectedRun?.Telemetry;
            return telemetry?.Available == true;
        }
    }

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync().ConfigureAwait(false);
    }

    internal static string GetShortRunId(string runId)
    {
        if (string.IsNullOrWhiteSpace(runId))
        {
            return string.Empty;
        }

        var trimmed = runId.Trim();
        var underscoreIndex = trimmed.LastIndexOf('_');
        if (underscoreIndex >= 0 && underscoreIndex < trimmed.Length - 1)
        {
            return trimmed[(underscoreIndex + 1)..];
        }

        return trimmed.Length > 8 ? trimmed[^8..] : trimmed;
    }

    private int TotalRuns => discoveryResult?.TotalCount ?? runs.Count;

    private async Task LoadAsync(bool forceRefresh = false)
    {
        isLoading = true;
        errorMessage = null;
        await InvokeAsync(StateHasChanged);

        try
        {
            discoveryResult = await RunDiscovery.LoadRunsAsync().ConfigureAwait(false);
            if (discoveryResult.Success)
            {
                runs = discoveryResult.Runs;
                discoveryDiagnostics = discoveryResult.Diagnostics;
            }
            else
            {
                runs = Array.Empty<RunListEntry>();
                discoveryDiagnostics = Array.Empty<RunDiagnostic>();
                errorMessage = discoveryResult.ErrorMessage ?? "Failed to load runs.";
            }

            var query = ReadQueryParameters();
            state = ArtifactListState.FromQuery(runs, query);
            state.PageSize = DefaultPageSize;
            searchInput = state.SearchText;

            if (!HasFilterQuery(query) && !forceRefresh)
            {
                var persisted = await LoadPersistedStateAsync().ConfigureAwait(false);
                if (persisted is not null)
                {
                    state = ArtifactListState.FromQuery(runs, persisted);
                    state.PageSize = DefaultPageSize;
                    searchInput = state.SearchText;
                }
            }

            ApplyState();
            await EnsureDeepLinkAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load run discovery for artifacts.");
            runs = Array.Empty<RunListEntry>();
            discoveryDiagnostics = Array.Empty<RunDiagnostic>();
            errorMessage = ex.Message;
            state = new ArtifactListState(runs) { PageSize = DefaultPageSize };
            ApplyState();
        }
        finally
        {
            isLoading = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task RefreshAsync()
    {
        detailCache.Clear();
        await LoadAsync(forceRefresh: true).ConfigureAwait(false);
    }

    private void ApplyState()
    {
        state.PageSize = DefaultPageSize;
        view = state.Apply();
        BuildFacets();
        PruneSelection();
        var defaultDirection = ArtifactListState.GetDefaultDirection(state.SortOption);
        hasActiveFilters = state.SelectedModes.Any() ||
                            state.SelectedStatuses.Any() ||
                            state.WarningFilter != ArtifactWarningFilter.All ||
                            state.TelemetryFilter != ArtifactTelemetryFilter.All ||
                            !string.IsNullOrWhiteSpace(state.SearchText) ||
                            state.SortOption != ArtifactSortOption.Created ||
                            state.SortDirection != defaultDirection;
    }

    private void BuildFacets()
    {
        statusFacets = runs
            .GroupBy(ArtifactListState.DetermineStatus)
            .ToDictionary(g => g.Key, g => g.Count());

        foreach (ArtifactRunStatus status in Enum.GetValues<ArtifactRunStatus>())
        {
            if (!statusFacets.ContainsKey(status))
            {
                statusFacets[status] = 0;
            }
        }

        simulationModeCount = runs.Count(r => string.Equals(r.Source, "simulation", StringComparison.OrdinalIgnoreCase));
        warningsWithIssues = runs.Count(r => r.WarningCount > 0);
        warningsClear = runs.Count - warningsWithIssues;
        telemetryAvailable = runs.Count(r => r.Telemetry?.Available == true);
    }

    private int GetStatusCount(ArtifactRunStatus status) =>
        statusFacets.TryGetValue(status, out var count) ? count : 0;

    private async Task ToggleStatusAsync(ArtifactRunStatus status)
    {
        state.ToggleStatus(status);
        state.PageIndex = 1;
        ApplyState();
        await UpdateQueryAsync(includeRunId: true).ConfigureAwait(false);
    }

    private async Task SetWarningFilterAsync(ArtifactWarningFilter filter)
    {
        state.WarningFilter = state.WarningFilter == filter ? ArtifactWarningFilter.All : filter;
        state.PageIndex = 1;
        ApplyState();
        await UpdateQueryAsync(includeRunId: true).ConfigureAwait(false);
    }

    private async Task SetModeFilterAsync(string? modeKey)
    {
        if (string.IsNullOrWhiteSpace(modeKey))
        {
            state.SetModes(Array.Empty<string>());
            state.TelemetryFilter = ArtifactTelemetryFilter.All;
        }
        else if (string.Equals(modeKey, "telemetry-available", StringComparison.OrdinalIgnoreCase))
        {
            state.SetModes(Array.Empty<string>());
            state.TelemetryFilter = state.TelemetryFilter == ArtifactTelemetryFilter.Available
                ? ArtifactTelemetryFilter.All
                : ArtifactTelemetryFilter.Available;
        }
        else
        {
            if (state.IsModeSelected(modeKey))
            {
                state.SetModes(Array.Empty<string>());
            }
            else
            {
                state.SetModes(new[] { modeKey });
            }

            state.TelemetryFilter = ArtifactTelemetryFilter.All;
        }

        state.PageIndex = 1;
        ApplyState();
        await UpdateQueryAsync(includeRunId: true).ConfigureAwait(false);
    }

    private async Task OnSortChanged(ArtifactSortOption option)
    {
        state.SortOption = option;
        state.SortDirection = ArtifactListState.GetDefaultDirection(option);
        state.PageIndex = 1;
        ApplyState();
        await UpdateQueryAsync(includeRunId: true).ConfigureAwait(false);
    }

    private async Task ToggleColumnSortAsync(ArtifactSortOption option)
    {
        if (state.SortOption == option)
        {
            state.SortDirection = state.SortDirection == ArtifactSortDirection.Ascending
                ? ArtifactSortDirection.Descending
                : ArtifactSortDirection.Ascending;
        }
        else
        {
            state.SortOption = option;
            state.SortDirection = ArtifactListState.GetDefaultDirection(option);
        }

        state.PageIndex = 1;
        ApplyState();
        await UpdateQueryAsync(includeRunId: true).ConfigureAwait(false);
    }

    private async Task OnHeaderKeyDown(KeyboardEventArgs args, ArtifactSortOption option)
    {
        if (args.Key is "Enter" or " " or "Spacebar" or "Space")
        {
            await ToggleColumnSortAsync(option).ConfigureAwait(false);
        }
    }

    private bool IsSortedBy(ArtifactSortOption option) => state.SortOption == option;

    private string GetSortIcon() =>
        state.SortDirection == ArtifactSortDirection.Ascending
            ? Icons.Material.Filled.ArrowUpward
            : Icons.Material.Filled.ArrowDownward;
    private async Task OnSearchChanged(string value)
    {
        searchInput = value ?? string.Empty;
        state.SearchText = searchInput;
        state.PageIndex = 1;
        ApplyState();
        await UpdateQueryAsync(includeRunId: true).ConfigureAwait(false);
    }

    private async Task ResetFiltersAsync()
    {
        state = new ArtifactListState(runs)
        {
            PageIndex = 1,
            PageSize = DefaultPageSize,
            SortOption = ArtifactSortOption.Created,
            SortDirection = ArtifactListState.GetDefaultDirection(ArtifactSortOption.Created),
            WarningFilter = ArtifactWarningFilter.All,
            TelemetryFilter = ArtifactTelemetryFilter.All,
            SearchText = string.Empty
        };
        searchInput = string.Empty;
        ApplyState();
        await UpdateQueryAsync(includeRunId: true).ConfigureAwait(false);
    }

    private async Task PreviousPageAsync()
    {
        if (state.PageIndex <= 1)
        {
            return;
        }

        state.PageIndex -= 1;
        ApplyState();
        await UpdateQueryAsync(includeRunId: true).ConfigureAwait(false);
    }

    private async Task NextPageAsync()
    {
        if (view.PageIndex >= view.TotalPages)
        {
            return;
        }

        state.PageIndex += 1;
        ApplyState();
        await UpdateQueryAsync(includeRunId: true).ConfigureAwait(false);
    }

    private async Task OpenDrawerAsync(RunListEntry run)
    {
        selectedRun = run;
        isDrawerOpen = true;
        detailError = null;
        captureSummary = null;
        overwriteCapture = false;

        await UpdateQueryAsync(includeRunId: true).ConfigureAwait(false);

        if (detailCache.TryGetValue(run.RunId, out var cached))
        {
            selectedDetail = cached;
            await InvokeAsync(StateHasChanged);
        }
        else
        {
            await LoadDetailAsync(run, forceReload: false).ConfigureAwait(false);
        }
    }

    private bool IsSelected(RunListEntry run) =>
        selectedRun is not null &&
        string.Equals(selectedRun.RunId, run.RunId, StringComparison.OrdinalIgnoreCase);

    internal async Task CloseDrawerAsync()
    {
        isDrawerOpen = false;
        selectedRun = null;
        selectedDetail = null;
        detailError = null;
        captureSummary = null;
        overwriteCapture = false;
        await UpdateQueryAsync(includeRunId: false).ConfigureAwait(false);
        await InvokeAsync(StateHasChanged);
    }

    private async Task ReloadDetailAsync()
    {
        if (selectedRun is null)
        {
            return;
        }

        await LoadDetailAsync(selectedRun, forceReload: true).ConfigureAwait(false);
    }

    private async Task LoadDetailAsync(RunListEntry run, bool forceReload)
    {
        if (!forceReload && detailCache.TryGetValue(run.RunId, out var cached))
        {
            selectedDetail = cached;
            return;
        }

        isDetailLoading = true;
        detailError = null;
        await InvokeAsync(StateHasChanged);

        try
        {
            var result = await ApiClient.GetRunAsync(run.RunId).ConfigureAwait(false);
            if (result.Success && result.Value is not null)
            {
                selectedDetail = result.Value;
                detailCache[run.RunId] = result.Value;
            }
            else
            {
                selectedDetail = null;
                detailError = result.Error ?? $"Unable to load run {run.RunId}.";
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load detail for run {RunId}", run.RunId);
            selectedDetail = null;
            detailError = ex.Message;
        }
        finally
        {
            isDetailLoading = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task GenerateTelemetryAsync()
    {
        if (selectedRun is null || isGeneratingTelemetry)
        {
            return;
        }

        isGeneratingTelemetry = true;
        detailError = null;
        captureSummary = null;
        await InvokeAsync(StateHasChanged);

        try
        {
            var request = new TelemetryCaptureRequestDto(
                new TelemetryCaptureSourceDto("run", selectedRun.RunId),
                new TelemetryCaptureOutputDto(null, null, overwriteCapture));

            var result = await ApiClient.GenerateTelemetryCaptureAsync(request).ConfigureAwait(false);
            if (!result.Success || result.Value?.Capture is null)
            {
                if (result.StatusCode == 409)
                {
                    captureSummary = TryParseCapture(result.Error);
                    NotificationService.Add("Telemetry bundle already exists. Enable overwrite to regenerate.", Severity.Info);
                }
                else
                {
                    var message = result.Error ?? $"Telemetry generation failed (status {result.StatusCode}).";
                    NotificationService.Add(message, Severity.Error);
                    detailError = message;
                }
                return;
            }

            captureSummary = result.Value.Capture;
            if (captureSummary.Generated)
            {
                NotificationService.Add("Telemetry bundle generated successfully.", Severity.Success);
            }
            else
            {
                NotificationService.Add("Telemetry bundle already existed. Enable overwrite to regenerate.", Severity.Info);
            }

            await ReloadSelectedRunAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Telemetry generation failed for {RunId}", selectedRun.RunId);
            detailError = ex.Message;
            NotificationService.Add($"Telemetry generation failed: {ex.Message}", Severity.Error);
        }
        finally
        {
            isGeneratingTelemetry = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task ReloadSelectedRunAsync()
    {
        if (selectedRun is null)
        {
            return;
        }

        try
        {
            var result = await ApiClient.GetRunAsync(selectedRun.RunId).ConfigureAwait(false);
            if (result.Success && result.Value is not null)
            {
                selectedDetail = result.Value;
                detailCache[selectedRun.RunId] = result.Value;

                var telemetry = result.Value.Telemetry ?? selectedRun.Telemetry;
                var updated = selectedRun with
                {
                    Telemetry = telemetry,
                    WarningCount = result.Value.Warnings?.Count ?? selectedRun.WarningCount
                };

                UpdateRunEntry(updated);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to refresh run after telemetry generation for {RunId}", selectedRun.RunId);
        }
    }

    private void UpdateRunEntry(RunListEntry updated)
    {
        var list = runs.ToList();
        var index = list.FindIndex(r => string.Equals(r.RunId, updated.RunId, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            return;
        }

        list[index] = updated;
        runs = list;
        state = state.CloneForRuns(runs);
        ApplyState();

        if (selectedRun is not null && string.Equals(selectedRun.RunId, updated.RunId, StringComparison.OrdinalIgnoreCase))
        {
            selectedRun = updated;
        }
    }

    private void LoadRun(OrchestrationMode mode)
    {
        if (selectedRun is null)
        {
            return;
        }

        var templateId = selectedRun.TemplateId;
        if (string.IsNullOrWhiteSpace(templateId))
        {
            return;
        }

        var query = new Dictionary<string, string?>
        {
            ["templateId"] = templateId,
            ["mode"] = mode.ToApiString()
        };

        var qs = string.Join("&", query
            .Where(kvp => !string.IsNullOrWhiteSpace(kvp.Value))
            .Select(kvp => $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value!)}"));

        Navigation.NavigateTo($"/time-travel/run?{qs}");
    }

    private TelemetryCaptureSummaryDto? TryParseCapture(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<TelemetryCaptureResponseDto>(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            return parsed?.Capture;
        }
        catch (JsonException ex)
        {
            Logger.LogDebug(ex, "Failed to parse telemetry capture payload: {Payload}", payload);
            return null;
        }
    }

    private async Task OnRowClickedAsync(RunListEntry run)
    {
        if (isDrawerOpen &&
            selectedRun is not null &&
            string.Equals(selectedRun.RunId, run.RunId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await OpenDrawerAsync(run).ConfigureAwait(false);
    }

    private bool IsRowSelected(RunListEntry run) =>
        selectedRunIds.Contains(run.RunId);

    private string GetRowClass(RunListEntry run)
    {
        var selected = IsRowSelected(run) ? " artifact-table-row--selected" : string.Empty;
        return $"artifact-table-row{selected}";
    }

    private bool SelectAllChecked => view.Items.Count > 0 && view.Items.All(IsRowSelected);

    private Task OnSelectAllChanged(bool value)
    {
        if (value)
        {
            foreach (var run in view.Items)
            {
                selectedRunIds.Add(run.RunId);
            }
        }
        else
        {
            foreach (var run in view.Items)
            {
                selectedRunIds.Remove(run.RunId);
            }
        }

        StateHasChanged();
        return Task.CompletedTask;
    }

    private Task OnSelectAllStateChanged(bool? value) =>
        OnSelectAllChanged(value == true);

    private Task OnRowSelectionChanged(RunListEntry run, bool value)
    {
        if (value)
        {
            selectedRunIds.Add(run.RunId);
        }
        else
        {
            selectedRunIds.Remove(run.RunId);
        }

        StateHasChanged();
        return Task.CompletedTask;
    }

    private void SetViewMode(ArtifactViewMode mode)
    {
        if (viewMode == mode)
        {
            return;
        }

        viewMode = mode;
        ClearSelection();
        StateHasChanged();
    }

    private void ClearSelection()
    {
        if (selectedRunIds.Count == 0)
        {
            return;
        }

        selectedRunIds.Clear();
        StateHasChanged();
    }

    private void PruneSelection()
    {
        if (selectedRunIds.Count == 0)
        {
            return;
        }

        var validIds = new HashSet<string>(runs.Select(r => r.RunId), StringComparer.OrdinalIgnoreCase);
        selectedRunIds.RemoveWhere(id => !validIds.Contains(id));
    }

    private string SelectionSummary => selectedRunIds.Count switch
    {
        0 => "No runs selected",
        1 => "1 run selected",
        _ => $"{selectedRunIds.Count} runs selected"
    };

    private static string FormatTemplateTitle(RunListEntry run) =>
        string.IsNullOrWhiteSpace(run.TemplateTitle) ? run.TemplateId : run.TemplateTitle!;

    private static string FormatCreated(RunListEntry run) =>
        run.CreatedUtc?.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) ?? "—";

    private static string FormatGrid(GridSummary grid) =>
        grid.IsAvailable ? $"{grid.Bins} bins · {grid.BinMinutes} min" : "—";

    private static string FormatModeLabel(string? mode)
    {
        if (string.IsNullOrWhiteSpace(mode))
        {
            return "Unknown";
        }

        var trimmed = mode.Trim();
        if (trimmed.Length == 1)
        {
            return trimmed.ToUpperInvariant();
        }

        return char.ToUpperInvariant(trimmed[0]) + trimmed[1..].ToLowerInvariant();
    }

    private async Task DeleteSelectedAsync()
    {
        if (selectedRunIds.Count == 0 || isDeletingArtifacts)
        {
            return;
        }

        var confirmed = await ConfirmBulkDeleteAsync().ConfigureAwait(false);
        if (!confirmed)
        {
            return;
        }

        try
        {
            isDeletingArtifacts = true;
            await InvokeAsync(StateHasChanged);

            var ids = selectedRunIds.ToArray();
            var response = await ApiClient.BulkDeleteArtifactsAsync(ids).ConfigureAwait(false);
            if (!response.Success || response.Value is null)
            {
                NotificationService.Add(response.Error ?? "Failed to delete artifacts.", Severity.Error);
                return;
            }

            if (response.Value.Deleted > 0)
            {
                NotificationService.Add($"Deleted {response.Value.Deleted} artifact(s).", Severity.Success);
            }

            var failed = response.Value.Processed - response.Value.Deleted;
            if (failed > 0)
            {
                NotificationService.Add($"{failed} artifact(s) could not be deleted.", Severity.Warning);
            }

            selectedRunIds.Clear();
            await LoadAsync(forceRefresh: true).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to delete artifacts.");
            NotificationService.Add($"Failed to delete artifacts: {ex.Message}", Severity.Error);
        }
        finally
        {
            isDeletingArtifacts = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task<bool> ConfirmBulkDeleteAsync()
    {
        var dialogService = DialogService;
        if (dialogService is null)
        {
            return false;
        }

        var selectedLabels = runs
            .Where(r => selectedRunIds.Contains(r.RunId))
            .Select(r => $"{FormatTemplateTitle(r)} · {GetShortRunId(r.RunId)}")
            .Take(25)
            .ToList();

        var parameters = new DialogParameters
        {
            [nameof(ConfirmBulkActionDialog.Title)] = "Delete artifacts",
            [nameof(ConfirmBulkActionDialog.Message)] = $"Delete {selectedRunIds.Count} artifact(s)? This permanently removes their run directories.",
            [nameof(ConfirmBulkActionDialog.ActionButtonText)] = "Delete",
            [nameof(ConfirmBulkActionDialog.ActionButtonColor)] = Color.Error,
            [nameof(ConfirmBulkActionDialog.SelectedItems)] = selectedLabels
        };

        var options = new DialogOptions
        {
            CloseOnEscapeKey = true,
            MaxWidth = MaxWidth.Small,
            FullWidth = true
        };

        var dialog = await dialogService.ShowAsync<ConfirmBulkActionDialog>("Delete artifacts", parameters, options).ConfigureAwait(false);
        if (dialog is null)
        {
            return false;
        }

        var result = await dialog.Result.ConfigureAwait(false);
        if (result is null)
        {
            return false;
        }

        return !result.Canceled;
    }

    private Task NavigateToDashboardAsync(RunListEntry run) =>
        NavigateToModeAsync(run, OrchestrationMode.Simulation, "/time-travel/dashboard");

    private Task NavigateToTopologyAsync(RunListEntry run) =>
        NavigateToModeAsync(run, OrchestrationMode.Simulation, "/time-travel/topology");

    private Task NavigateToTelemetryDashboardAsync(RunListEntry run) =>
        NavigateToModeAsync(run, OrchestrationMode.Telemetry, "/time-travel/dashboard");

    private Task NavigateToTelemetryTopologyAsync(RunListEntry run) =>
        NavigateToModeAsync(run, OrchestrationMode.Telemetry, "/time-travel/topology");

    private async Task NavigateToModeAsync(RunListEntry run, OrchestrationMode mode, string path)
    {
        await CloseDrawerIfDifferentAsync(run).ConfigureAwait(false);

        var uri = new UriBuilder(Navigation.Uri)
        {
            Path = path,
            Query = BuildModeQuery(run.RunId, mode)
        };

        Navigation.NavigateTo(uri.Uri.PathAndQuery);
    }

    private static string BuildModeQuery(string runId, OrchestrationMode mode)
    {
        var qs = new Dictionary<string, string?>
        {
            ["runId"] = runId,
            ["mode"] = mode.ToApiString()
        };

        return string.Join("&", qs.Where(kvp => !string.IsNullOrWhiteSpace(kvp.Value))
            .Select(kvp => $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value!)}"));
    }

    private Task CloseDrawerIfDifferentAsync(RunListEntry run)
    {
        if (selectedRun is not null && string.Equals(selectedRun.RunId, run.RunId, StringComparison.OrdinalIgnoreCase))
        {
            return Task.CompletedTask;
        }

        return CloseDrawerAsync();
    }

    private Dictionary<string, string?> ReadQueryParameters() =>
        ParseQueryString(new Uri(Navigation.Uri).Query);

    private static bool HasFilterQuery(IReadOnlyDictionary<string, string?> query)
    {
        foreach (var key in new[] { "status", "mode", "warnings", "telemetry", "search", "sort", "page" })
        {
            if (query.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return true;
            }
        }

        return false;
    }

    private static Dictionary<string, string?> ParseQueryString(string query)
    {
        var dict = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(query))
        {
            return dict;
        }

        var trimmed = query[0] == '?' ? query[1..] : query;
        if (string.IsNullOrEmpty(trimmed))
        {
            return dict;
        }

        var pairs = trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries);
        foreach (var pair in pairs)
        {
            var separator = pair.IndexOf('=');
            if (separator > -1)
            {
                var key = Uri.UnescapeDataString(pair[..separator]);
                var value = Uri.UnescapeDataString(pair[(separator + 1)..]);
                dict[key] = value;
            }
            else
            {
                var key = Uri.UnescapeDataString(pair);
                dict[key] = string.Empty;
            }
        }

        return dict;
    }

    private async Task<IReadOnlyDictionary<string, string?>?> LoadPersistedStateAsync()
    {
        try
        {
            var json = await JSRuntime.InvokeAsync<string?>("localStorage.getItem", LocalStorageKey).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            var dictionary = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            return dictionary is null
                ? null
                : dictionary.ToDictionary(kvp => kvp.Key, kvp => (string?)kvp.Value, StringComparer.OrdinalIgnoreCase);
        }
        catch (JSException ex)
        {
            Logger.LogDebug(ex, "Unable to read artifacts state from localStorage.");
            return null;
        }
        catch (JsonException ex)
        {
            Logger.LogDebug(ex, "Unable to deserialize stored artifacts state.");
            return null;
        }
    }

    private async Task PersistStateAsync(IDictionary<string, string> queryWithoutRunId)
    {
        try
        {
            var json = JsonSerializer.Serialize(queryWithoutRunId);
            await JSRuntime.InvokeVoidAsync("localStorage.setItem", LocalStorageKey, json).ConfigureAwait(false);
        }
        catch (JSException ex)
        {
            Logger.LogDebug(ex, "Unable to write artifacts state to localStorage.");
        }
    }

    private async Task UpdateQueryAsync(bool includeRunId)
    {
        var query = state.ToQueryParameters();
        var navQuery = query.ToDictionary(kvp => kvp.Key, kvp => (object?)kvp.Value, StringComparer.OrdinalIgnoreCase);

        if (includeRunId && selectedRun is not null)
        {
            query["runId"] = selectedRun.RunId;
            navQuery["runId"] = selectedRun.RunId;
        }

        var targetUri = Navigation.GetUriWithQueryParameters(navQuery);
        if (!string.Equals(targetUri, Navigation.Uri, StringComparison.Ordinal))
        {
            Navigation.NavigateTo(targetUri, replace: true);
        }

        if (includeRunId)
        {
            query.Remove("runId");
        }

        await PersistStateAsync(query).ConfigureAwait(false);
    }

    private async Task EnsureDeepLinkAsync()
    {
        var query = ReadQueryParameters();
        if (!query.TryGetValue("runId", out var runId) || string.IsNullOrWhiteSpace(runId))
        {
            return;
        }

        var match = runs.FirstOrDefault(r => string.Equals(r.RunId, runId, StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            return;
        }

        await OpenDrawerAsync(match).ConfigureAwait(false);
    }


    private Task OnOverwriteChanged(bool value)
    {
        overwriteCapture = value;
        return Task.CompletedTask;
    }

    private static IReadOnlyDictionary<string, object> GetAriaLabelAttributes(string label)
        => new Dictionary<string, object> { ["aria-label"] = label };
}

internal enum ArtifactViewMode
{
    Cards,
    List
}
