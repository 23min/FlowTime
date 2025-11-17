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

    private ArtifactListState _state = new(Array.Empty<RunListEntry>()) { PageSize = DefaultPageSize };
    private ArtifactListResult _view = new(Array.Empty<RunListEntry>(), 0, 1, 1);
    private IReadOnlyList<RunListEntry> _runs = Array.Empty<RunListEntry>();
    private IReadOnlyList<RunDiagnostic> _discoveryDiagnostics = Array.Empty<RunDiagnostic>();
    private RunDiscoveryResult? _discoveryResult;

    private bool _isLoading;
    private string? _errorMessage;
    private string _searchInput = string.Empty;
    private Dictionary<ArtifactRunStatus, int> _statusFacets = new();
    private int _warningsWithIssues;
    private int _warningsClear;
    private int _simulationModeCount;
    private int _telemetryAvailable;
    private bool _hasActiveFilters;
    private ArtifactViewMode _viewMode = ArtifactViewMode.Cards;
    private readonly HashSet<string> _selectedRunIds = new(StringComparer.OrdinalIgnoreCase);
    private bool _isDeletingArtifacts;

    private bool _isDrawerOpen;
    private RunListEntry? _selectedRun;
    private RunCreateResponseDto? _selectedDetail;
    private bool _isDetailLoading;
    private string? _detailError;
    private TelemetryCaptureSummaryDto? _captureSummary;
    private bool _isGeneratingTelemetry;
    private bool _overwriteCapture;

    internal bool IsDrawerOpen => _isDrawerOpen;

    private bool CanLoadModel =>
        (_selectedDetail?.CanReplay ?? _selectedRun?.CanReplay) == true;

    private bool CanLoadTelemetry
    {
        get
        {
            if (!CanLoadModel)
            {
                return false;
            }

            var telemetry = _selectedDetail?.Telemetry ?? _selectedRun?.Telemetry;
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

    private int TotalRuns => _discoveryResult?.TotalCount ?? _runs.Count;

    private async Task LoadAsync(bool forceRefresh = false)
    {
        _isLoading = true;
        _errorMessage = null;
        await InvokeAsync(StateHasChanged);

        try
        {
            _discoveryResult = await RunDiscovery.LoadRunsAsync().ConfigureAwait(false);
            if (_discoveryResult.Success)
            {
                _runs = _discoveryResult.Runs;
                _discoveryDiagnostics = _discoveryResult.Diagnostics;
            }
            else
            {
                _runs = Array.Empty<RunListEntry>();
                _discoveryDiagnostics = Array.Empty<RunDiagnostic>();
                _errorMessage = _discoveryResult.ErrorMessage ?? "Failed to load runs.";
            }

            var query = ReadQueryParameters();
            _state = ArtifactListState.FromQuery(_runs, query);
            _state.PageSize = DefaultPageSize;
            _searchInput = _state.SearchText;

            if (!HasFilterQuery(query) && !forceRefresh)
            {
                var persisted = await LoadPersistedStateAsync().ConfigureAwait(false);
                if (persisted is not null)
                {
                    _state = ArtifactListState.FromQuery(_runs, persisted);
                    _state.PageSize = DefaultPageSize;
                    _searchInput = _state.SearchText;
                }
            }

            ApplyState();
            await EnsureDeepLinkAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load run discovery for artifacts.");
            _runs = Array.Empty<RunListEntry>();
            _discoveryDiagnostics = Array.Empty<RunDiagnostic>();
            _errorMessage = ex.Message;
            _state = new ArtifactListState(_runs) { PageSize = DefaultPageSize };
            ApplyState();
        }
        finally
        {
            _isLoading = false;
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
        _state.PageSize = DefaultPageSize;
        _view = _state.Apply();
        BuildFacets();
        PruneSelection();
        var defaultDirection = ArtifactListState.GetDefaultDirection(_state.SortOption);
        _hasActiveFilters = _state.SelectedModes.Any() ||
                            _state.SelectedStatuses.Any() ||
                            _state.WarningFilter != ArtifactWarningFilter.All ||
                            _state.TelemetryFilter != ArtifactTelemetryFilter.All ||
                            !string.IsNullOrWhiteSpace(_state.SearchText) ||
                            _state.SortOption != ArtifactSortOption.Created ||
                            _state.SortDirection != defaultDirection;
    }

    private void BuildFacets()
    {
        _statusFacets = _runs
            .GroupBy(ArtifactListState.DetermineStatus)
            .ToDictionary(g => g.Key, g => g.Count());

        foreach (ArtifactRunStatus status in Enum.GetValues<ArtifactRunStatus>())
        {
            if (!_statusFacets.ContainsKey(status))
            {
                _statusFacets[status] = 0;
            }
        }

        _simulationModeCount = _runs.Count(r => string.Equals(r.Source, "simulation", StringComparison.OrdinalIgnoreCase));
        _warningsWithIssues = _runs.Count(r => r.WarningCount > 0);
        _warningsClear = _runs.Count - _warningsWithIssues;
        _telemetryAvailable = _runs.Count(r => r.Telemetry?.Available == true);
    }

    private int GetStatusCount(ArtifactRunStatus status) =>
        _statusFacets.TryGetValue(status, out var count) ? count : 0;

    private async Task ToggleStatusAsync(ArtifactRunStatus status)
    {
        _state.ToggleStatus(status);
        _state.PageIndex = 1;
        ApplyState();
        await UpdateQueryAsync(includeRunId: true).ConfigureAwait(false);
    }

    private async Task SetWarningFilterAsync(ArtifactWarningFilter filter)
    {
        _state.WarningFilter = _state.WarningFilter == filter ? ArtifactWarningFilter.All : filter;
        _state.PageIndex = 1;
        ApplyState();
        await UpdateQueryAsync(includeRunId: true).ConfigureAwait(false);
    }

    private async Task SetModeFilterAsync(string? modeKey)
    {
        if (string.IsNullOrWhiteSpace(modeKey))
        {
            _state.SetModes(Array.Empty<string>());
            _state.TelemetryFilter = ArtifactTelemetryFilter.All;
        }
        else if (string.Equals(modeKey, "telemetry-available", StringComparison.OrdinalIgnoreCase))
        {
            _state.SetModes(Array.Empty<string>());
            _state.TelemetryFilter = _state.TelemetryFilter == ArtifactTelemetryFilter.Available
                ? ArtifactTelemetryFilter.All
                : ArtifactTelemetryFilter.Available;
        }
        else
        {
            if (_state.IsModeSelected(modeKey))
            {
                _state.SetModes(Array.Empty<string>());
            }
            else
            {
                _state.SetModes(new[] { modeKey });
            }

            _state.TelemetryFilter = ArtifactTelemetryFilter.All;
        }

        _state.PageIndex = 1;
        ApplyState();
        await UpdateQueryAsync(includeRunId: true).ConfigureAwait(false);
    }

    private async Task OnSortChanged(ArtifactSortOption option)
    {
        _state.SortOption = option;
        _state.SortDirection = ArtifactListState.GetDefaultDirection(option);
        _state.PageIndex = 1;
        ApplyState();
        await UpdateQueryAsync(includeRunId: true).ConfigureAwait(false);
    }

    private async Task ToggleColumnSortAsync(ArtifactSortOption option)
    {
        if (_state.SortOption == option)
        {
            _state.SortDirection = _state.SortDirection == ArtifactSortDirection.Ascending
                ? ArtifactSortDirection.Descending
                : ArtifactSortDirection.Ascending;
        }
        else
        {
            _state.SortOption = option;
            _state.SortDirection = ArtifactListState.GetDefaultDirection(option);
        }

        _state.PageIndex = 1;
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

    private bool IsSortedBy(ArtifactSortOption option) => _state.SortOption == option;

    private string GetSortIcon() =>
        _state.SortDirection == ArtifactSortDirection.Ascending
            ? Icons.Material.Filled.ArrowUpward
            : Icons.Material.Filled.ArrowDownward;
    private async Task OnSearchChanged(string value)
    {
        _searchInput = value ?? string.Empty;
        _state.SearchText = _searchInput;
        _state.PageIndex = 1;
        ApplyState();
        await UpdateQueryAsync(includeRunId: true).ConfigureAwait(false);
    }

    private async Task ResetFiltersAsync()
    {
        _state = new ArtifactListState(_runs)
        {
            PageIndex = 1,
            PageSize = DefaultPageSize,
            SortOption = ArtifactSortOption.Created,
            SortDirection = ArtifactListState.GetDefaultDirection(ArtifactSortOption.Created),
            WarningFilter = ArtifactWarningFilter.All,
            TelemetryFilter = ArtifactTelemetryFilter.All,
            SearchText = string.Empty
        };
        _searchInput = string.Empty;
        ApplyState();
        await UpdateQueryAsync(includeRunId: true).ConfigureAwait(false);
    }

    private async Task PreviousPageAsync()
    {
        if (_state.PageIndex <= 1)
        {
            return;
        }

        _state.PageIndex -= 1;
        ApplyState();
        await UpdateQueryAsync(includeRunId: true).ConfigureAwait(false);
    }

    private async Task NextPageAsync()
    {
        if (_view.PageIndex >= _view.TotalPages)
        {
            return;
        }

        _state.PageIndex += 1;
        ApplyState();
        await UpdateQueryAsync(includeRunId: true).ConfigureAwait(false);
    }

    private async Task OpenDrawerAsync(RunListEntry run)
    {
        _selectedRun = run;
        _isDrawerOpen = true;
        _detailError = null;
        _captureSummary = null;
        _overwriteCapture = false;

        await UpdateQueryAsync(includeRunId: true).ConfigureAwait(false);

        if (detailCache.TryGetValue(run.RunId, out var cached))
        {
            _selectedDetail = cached;
            await InvokeAsync(StateHasChanged);
        }
        else
        {
            await LoadDetailAsync(run, forceReload: false).ConfigureAwait(false);
        }
    }

    private bool IsSelected(RunListEntry run) =>
        _selectedRun is not null &&
        string.Equals(_selectedRun.RunId, run.RunId, StringComparison.OrdinalIgnoreCase);

    internal async Task CloseDrawerAsync()
    {
        _isDrawerOpen = false;
        _selectedRun = null;
        _selectedDetail = null;
        _detailError = null;
        _captureSummary = null;
        _overwriteCapture = false;
        await UpdateQueryAsync(includeRunId: false).ConfigureAwait(false);
        await InvokeAsync(StateHasChanged);
    }

    private async Task ReloadDetailAsync()
    {
        if (_selectedRun is null)
        {
            return;
        }

        await LoadDetailAsync(_selectedRun, forceReload: true).ConfigureAwait(false);
    }

    private async Task LoadDetailAsync(RunListEntry run, bool forceReload)
    {
        if (!forceReload && detailCache.TryGetValue(run.RunId, out var cached))
        {
            _selectedDetail = cached;
            return;
        }

        _isDetailLoading = true;
        _detailError = null;
        await InvokeAsync(StateHasChanged);

        try
        {
            var result = await ApiClient.GetRunAsync(run.RunId).ConfigureAwait(false);
            if (result.Success && result.Value is not null)
            {
                _selectedDetail = result.Value;
                detailCache[run.RunId] = result.Value;
            }
            else
            {
                _selectedDetail = null;
                _detailError = result.Error ?? $"Unable to load run {run.RunId}.";
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load detail for run {RunId}", run.RunId);
            _selectedDetail = null;
            _detailError = ex.Message;
        }
        finally
        {
            _isDetailLoading = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task GenerateTelemetryAsync()
    {
        if (_selectedRun is null || _isGeneratingTelemetry)
        {
            return;
        }

        _isGeneratingTelemetry = true;
        _detailError = null;
        _captureSummary = null;
        await InvokeAsync(StateHasChanged);

        try
        {
            var request = new TelemetryCaptureRequestDto(
                new TelemetryCaptureSourceDto("run", _selectedRun.RunId),
                new TelemetryCaptureOutputDto(null, null, _overwriteCapture));

            var result = await ApiClient.GenerateTelemetryCaptureAsync(request).ConfigureAwait(false);
            if (!result.Success || result.Value?.Capture is null)
            {
                if (result.StatusCode == 409)
                {
                    _captureSummary = TryParseCapture(result.Error);
                    NotificationService.Add("Telemetry bundle already exists. Enable overwrite to regenerate.", Severity.Info);
                }
                else
                {
                    var message = result.Error ?? $"Telemetry generation failed (status {result.StatusCode}).";
                    NotificationService.Add(message, Severity.Error);
                    _detailError = message;
                }
                return;
            }

            _captureSummary = result.Value.Capture;
            if (_captureSummary.Generated)
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
            Logger.LogError(ex, "Telemetry generation failed for {RunId}", _selectedRun.RunId);
            _detailError = ex.Message;
            NotificationService.Add($"Telemetry generation failed: {ex.Message}", Severity.Error);
        }
        finally
        {
            _isGeneratingTelemetry = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task ReloadSelectedRunAsync()
    {
        if (_selectedRun is null)
        {
            return;
        }

        try
        {
            var result = await ApiClient.GetRunAsync(_selectedRun.RunId).ConfigureAwait(false);
            if (result.Success && result.Value is not null)
            {
                _selectedDetail = result.Value;
                detailCache[_selectedRun.RunId] = result.Value;

                var telemetry = result.Value.Telemetry ?? _selectedRun.Telemetry;
                var updated = _selectedRun with
                {
                    Telemetry = telemetry,
                    WarningCount = result.Value.Warnings?.Count ?? _selectedRun.WarningCount
                };

                UpdateRunEntry(updated);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to refresh run after telemetry generation for {RunId}", _selectedRun.RunId);
        }
    }

    private void UpdateRunEntry(RunListEntry updated)
    {
        var list = _runs.ToList();
        var index = list.FindIndex(r => string.Equals(r.RunId, updated.RunId, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            return;
        }

        list[index] = updated;
        _runs = list;
        _state = _state.CloneForRuns(_runs);
        ApplyState();

        if (_selectedRun is not null && string.Equals(_selectedRun.RunId, updated.RunId, StringComparison.OrdinalIgnoreCase))
        {
            _selectedRun = updated;
        }
    }

    private void LoadRun(OrchestrationMode mode)
    {
        if (_selectedRun is null)
        {
            return;
        }

        var templateId = _selectedRun.TemplateId;
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
        if (_isDrawerOpen &&
            _selectedRun is not null &&
            string.Equals(_selectedRun.RunId, run.RunId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await OpenDrawerAsync(run).ConfigureAwait(false);
    }

    private bool IsRowSelected(RunListEntry run) =>
        _selectedRunIds.Contains(run.RunId);

    private string GetRowClass(RunListEntry run)
    {
        var selected = IsRowSelected(run) ? " artifact-table-row--selected" : string.Empty;
        return $"artifact-table-row{selected}";
    }

    private bool SelectAllChecked => _view.Items.Count > 0 && _view.Items.All(IsRowSelected);

    private Task OnSelectAllChanged(bool value)
    {
        if (value)
        {
            foreach (var run in _view.Items)
            {
                _selectedRunIds.Add(run.RunId);
            }
        }
        else
        {
            foreach (var run in _view.Items)
            {
                _selectedRunIds.Remove(run.RunId);
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
            _selectedRunIds.Add(run.RunId);
        }
        else
        {
            _selectedRunIds.Remove(run.RunId);
        }

        StateHasChanged();
        return Task.CompletedTask;
    }

    private void SetViewMode(ArtifactViewMode mode)
    {
        if (_viewMode == mode)
        {
            return;
        }

        _viewMode = mode;
        ClearSelection();
        StateHasChanged();
    }

    private void ClearSelection()
    {
        if (_selectedRunIds.Count == 0)
        {
            return;
        }

        _selectedRunIds.Clear();
        StateHasChanged();
    }

    private void PruneSelection()
    {
        if (_selectedRunIds.Count == 0)
        {
            return;
        }

        var validIds = new HashSet<string>(_runs.Select(r => r.RunId), StringComparer.OrdinalIgnoreCase);
        _selectedRunIds.RemoveWhere(id => !validIds.Contains(id));
    }

    private string SelectionSummary => _selectedRunIds.Count switch
    {
        0 => "No runs selected",
        1 => "1 run selected",
        _ => $"{_selectedRunIds.Count} runs selected"
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
        if (_selectedRunIds.Count == 0 || _isDeletingArtifacts)
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
            _isDeletingArtifacts = true;
            await InvokeAsync(StateHasChanged);

            var ids = _selectedRunIds.ToArray();
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

            _selectedRunIds.Clear();
            await LoadAsync(forceRefresh: true).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to delete artifacts.");
            NotificationService.Add($"Failed to delete artifacts: {ex.Message}", Severity.Error);
        }
        finally
        {
            _isDeletingArtifacts = false;
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

        var selectedLabels = _runs
            .Where(r => _selectedRunIds.Contains(r.RunId))
            .Select(r => $"{FormatTemplateTitle(r)} · {GetShortRunId(r.RunId)}")
            .Take(25)
            .ToList();

        var parameters = new DialogParameters
        {
            [nameof(ConfirmBulkActionDialog.Title)] = "Delete artifacts",
            [nameof(ConfirmBulkActionDialog.Message)] = $"Delete {_selectedRunIds.Count} artifact(s)? This permanently removes their run directories.",
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
        if (_selectedRun is not null && string.Equals(_selectedRun.RunId, run.RunId, StringComparison.OrdinalIgnoreCase))
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
        var query = _state.ToQueryParameters();
        var navQuery = query.ToDictionary(kvp => kvp.Key, kvp => (object?)kvp.Value, StringComparer.OrdinalIgnoreCase);

        if (includeRunId && _selectedRun is not null)
        {
            query["runId"] = _selectedRun.RunId;
            navQuery["runId"] = _selectedRun.RunId;
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

        var match = _runs.FirstOrDefault(r => string.Equals(r.RunId, runId, StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            return;
        }

        await OpenDrawerAsync(match).ConfigureAwait(false);
    }


    private Task OnOverwriteChanged(bool value)
    {
        _overwriteCapture = value;
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
