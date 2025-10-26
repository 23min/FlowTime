using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using FlowTime.UI.Services;

namespace FlowTime.UI.Pages.TimeTravel;

public partial class Dashboard : IDisposable
{
    [Parameter, SupplyParameterFromQuery(Name = "runId")]
    public string? RunId { get; set; }

    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private ITimeTravelMetricsClient MetricsClient { get; set; } = default!;

    private readonly List<ServiceTileViewModel> _tiles = new();
    private readonly List<ServiceTileViewModel> _visibleTiles = new();
    private readonly HashSet<SlaStatus> _activeStatuses = new(Enum.GetValues<SlaStatus>());
    private CancellationTokenSource? _loadCts;
    private bool _isLoading;
    private string? _errorMessage;
    private SortOption sortOrder = SortOption.WorstFirst;
    private TimeTravelMetricsResult? _metricsResult;
    private TimeTravelMetricsContext? _context;

    private static readonly StatusOption[] statusOptions = new[]
    {
        new StatusOption(SlaStatus.Breach, "Breach", MudBlazor.Color.Error, Icons.Material.Filled.Report, "Filter on breached SLAs"),
        new StatusOption(SlaStatus.Warning, "At Risk", MudBlazor.Color.Warning, Icons.Material.Filled.PriorityHigh, "Filter on at-risk SLAs"),
        new StatusOption(SlaStatus.Passing, "Passing", MudBlazor.Color.Success, Icons.Material.Filled.CheckCircle, "Filter on passing SLAs"),
        new StatusOption(SlaStatus.NoData, "No Data", MudBlazor.Color.Default, Icons.Material.Filled.HourglassEmpty, "Filter on services with no data")
    };

    protected override async Task OnParametersSetAsync()
    {
        if (string.IsNullOrWhiteSpace(RunId))
        {
            _tiles.Clear();
            _visibleTiles.Clear();
            _metricsResult = null;
            _context = null;
            _errorMessage = null;
            _isLoading = false;
            CancelLoad();
            return;
        }

        await LoadMetricsAsync().ConfigureAwait(false);
    }

    private async Task LoadMetricsAsync()
    {
        CancelLoad();
        _isLoading = true;
        _errorMessage = null;
        _metricsResult = null;
        _context = null;
        _tiles.Clear();
        _visibleTiles.Clear();
        var cts = new CancellationTokenSource();
        _loadCts = cts;

        try
        {
            var result = await MetricsClient.GetMetricsAsync(RunId!, cts.Token).ConfigureAwait(false);
            if (!result.Success || result.Value is null)
            {
                _errorMessage = result.Error ?? "Metrics not available.";
                return;
            }

            _metricsResult = result.Value;
            _context = result.Value.Context;
            _tiles.Clear();
            foreach (var service in result.Value.Payload.Services)
            {
                var tile = BuildTile(service);
                if (tile is not null)
                {
                    _tiles.Add(tile);
                }
            }

            RebuildVisibleTiles();
        }
        catch (OperationCanceledException)
        {
            // Ignore cancellation.
        }
        catch (Exception ex)
        {
            _errorMessage = ex.Message;
        }
        finally
        {
            if (ReferenceEquals(_loadCts, cts))
            {
                _loadCts = null;
            }

            cts.Dispose();
            _isLoading = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    internal ServiceTileViewModel? BuildTile(TimeTravelServiceMetricsDto service)
    {
        if (string.IsNullOrWhiteSpace(service.Id))
        {
            return null;
        }

        var status = DetermineStatus(service);
        var mini = service.Mini?.ToArray() ?? Array.Empty<double>();
        var slaText = service.BinsTotal > 0
            ? string.Format(CultureInfo.InvariantCulture, "{0:0.#}%", service.SlaPct * 100d)
            : "—";

        var binsSummary = service.BinsTotal > 0
            ? string.Format(CultureInfo.InvariantCulture, "{0} of {1} bins meeting threshold", service.BinsMet, service.BinsTotal)
            : "No recent data";

        var aria = $"{service.Id} {status.Label}, {slaText}, {binsSummary}.";
        var sparkLabel = $"{service.Id} mini bar showing SLA trend over recent bins.";

        return new ServiceTileViewModel(
            service.Id,
            Math.Clamp(service.SlaPct, 0d, 1d),
            Math.Max(service.BinsMet, 0),
            Math.Max(service.BinsTotal, 0),
            mini,
            status.Status,
            status.Color,
            status.Label,
            slaText,
            binsSummary,
            aria,
            sparkLabel);
    }

    internal static StatusDescriptor DetermineStatus(TimeTravelServiceMetricsDto service)
    {
        if (service.BinsTotal <= 0)
        {
            return new StatusDescriptor(SlaStatus.NoData, MudBlazor.Color.Default, "No data");
        }

        if (service.SlaPct >= 0.95d)
        {
            return new StatusDescriptor(SlaStatus.Passing, MudBlazor.Color.Success, "On track");
        }

        if (service.SlaPct >= 0.90d)
        {
            return new StatusDescriptor(SlaStatus.Warning, MudBlazor.Color.Warning, "At risk");
        }

        return new StatusDescriptor(SlaStatus.Breach, MudBlazor.Color.Error, "Breached");
    }

    internal IEnumerable<ServiceTileViewModel> GetVisibleTiles()
    {
        IEnumerable<ServiceTileViewModel> query = _tiles.Where(tile => _activeStatuses.Contains(tile.Status));
        return sortOrder switch
        {
            SortOption.BestFirst => query.OrderByDescending(t => t.SlaPct).ThenBy(t => t.Id, StringComparer.OrdinalIgnoreCase),
            SortOption.Name => query.OrderBy(t => t.Id, StringComparer.OrdinalIgnoreCase),
            _ => query.OrderBy(t => t.SlaPct).ThenBy(t => t.Id, StringComparer.OrdinalIgnoreCase)
        };
    }

    private void RebuildVisibleTiles()
    {
        _visibleTiles.Clear();
        _visibleTiles.AddRange(GetVisibleTiles());
    }

    internal static string GetTileAccentStyle(SlaStatus status) =>
        status switch
        {
            SlaStatus.Passing => "--tile-accent-color: var(--mud-palette-success);",
            SlaStatus.Warning => "--tile-accent-color: var(--mud-palette-warning);",
            SlaStatus.Breach => "--tile-accent-color: var(--mud-palette-error);",
            _ => "--tile-accent-color: var(--mud-palette-text-secondary);"
        };

    private void ToggleStatus(SlaStatus status)
    {
        if (_activeStatuses.Contains(status))
        {
            if (_activeStatuses.Count > 1)
            {
                _activeStatuses.Remove(status);
            }
        }
        else
        {
            _activeStatuses.Add(status);
        }

        RebuildVisibleTiles();
        StateHasChanged();
    }

    private void ResetFilters()
    {
        _activeStatuses.Clear();
        foreach (var status in Enum.GetValues<SlaStatus>())
        {
            _activeStatuses.Add(status);
        }

        RebuildVisibleTiles();
        StateHasChanged();
    }

    private static string FormatMiniBarStyle(double value)
    {
        var height = Math.Clamp(value, 0d, 1d) * 100d;
        var color = value switch
        {
            >= 0.95d => "#27ae60",
            >= 0.9d => "#f39c12",
            _ => "#e74c3c"
        };
        return $"height:{height.ToString("0.##", CultureInfo.InvariantCulture)}%;background-color:{color};";
    }

    private void HandleTileKeyDown(KeyboardEventArgs args, ServiceTileViewModel tile)
    {
        if (args.Key == "Enter" || args.Key == " ")
        {
            NavigateToTopology(tile);
        }
    }

    private void NavigateToTopology(ServiceTileViewModel tile)
    {
        if (string.IsNullOrWhiteSpace(RunId))
        {
            return;
        }

        var uri = $"/time-travel/topology?runId={Uri.EscapeDataString(RunId)}&focus={Uri.EscapeDataString(tile.Id)}";
        Navigation.NavigateTo(uri);
    }

    private void NavigateToArtifacts() => Navigation.NavigateTo("/time-travel/artifacts");

    private async Task ReloadAsync()
    {
        if (string.IsNullOrWhiteSpace(RunId))
        {
            return;
        }

        await LoadMetricsAsync().ConfigureAwait(false);
    }

    private void CancelLoad()
    {
        if (_loadCts is null)
        {
            return;
        }

        try
        {
            _loadCts.Cancel();
        }
        catch
        {
            // Ignore cancellation errors.
        }
        finally
        {
            _loadCts.Dispose();
            _loadCts = null;
        }
    }

    public void Dispose() => CancelLoad();

    private string GetSortLabel() => sortOrder switch
    {
        SortOption.BestFirst => "Highest SLA first",
        SortOption.Name => "Name A-Z",
        _ => "Lowest SLA first"
    };

    private static string GetStatusIcon(SlaStatus status) => status switch
    {
        SlaStatus.Passing => Icons.Material.Filled.CheckCircle,
        SlaStatus.Warning => Icons.Material.Filled.PriorityHigh,
        SlaStatus.Breach => Icons.Material.Filled.Error,
        _ => Icons.Material.Filled.HourglassEmpty
    };

    private static string GetStatusIconStyle(SlaStatus status) => status switch
    {
        SlaStatus.Passing => "color:var(--mud-palette-success);",
        SlaStatus.Warning => "color:var(--mud-palette-warning);",
        SlaStatus.Breach => "color:var(--mud-palette-error);",
        _ => "color:var(--mud-palette-text-secondary);"
    };

    private string GetWindowSummary()
    {
        if (_context is null)
        {
            return string.Empty;
        }

        if (!_context.WindowStart.HasValue)
        {
            return $"{_context.BinCount} bins × {_context.BinMinutes} min";
        }

        var start = _context.WindowStart.Value;
        var end = _context.WindowEnd ?? start.AddMinutes(_context.BinMinutes * _context.BinCount);
        return $"{start.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)} → {end.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)} {_metricsResult?.Payload.Window.Timezone ?? "UTC"}";
    }

    private string GetTemplateSummary()
    {
        if (_context is null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(_context.TemplateTitle))
        {
            return _context.TemplateTitle!;
        }

        if (!string.IsNullOrWhiteSpace(_context.TemplateId))
        {
            return _context.TemplateId!;
        }

        return _context.RunId;
    }

    private SortOption SortOrder
    {
        get => sortOrder;
        set
        {
            if (sortOrder == value)
            {
                return;
            }

            sortOrder = value;
            RebuildVisibleTiles();
            StateHasChanged();
        }
    }

    internal enum SlaStatus
    {
        Passing,
        Warning,
        Breach,
        NoData
    }

    internal enum SortOption
    {
        WorstFirst,
        BestFirst,
        Name
    }

    internal sealed record StatusOption(SlaStatus Status, string Label, MudBlazor.Color Color, string Icon, string AccessibleLabel);

    internal sealed record StatusDescriptor(SlaStatus Status, MudBlazor.Color Color, string Label);

    internal sealed record ServiceTileViewModel(
        string Id,
        double SlaPct,
        int BinsMet,
        int BinsTotal,
        IReadOnlyList<double> Mini,
        SlaStatus Status,
        MudBlazor.Color ChipColor,
        string StatusLabel,
        string SlaText,
        string BinsSummary,
        string AriaLabel,
        string SparklineLabel);
}
