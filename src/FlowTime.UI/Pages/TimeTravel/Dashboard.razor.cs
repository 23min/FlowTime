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
using FlowTime.UI.TimeTravel;

namespace FlowTime.UI.Pages.TimeTravel;

public partial class Dashboard : IDisposable
{
    [Parameter, SupplyParameterFromQuery(Name = "runId")]
    public string? RunId { get; set; }

    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private ITimeTravelMetricsClient MetricsClient { get; set; } = default!;
    [Inject] private ITimeTravelDataService DataService { get; set; } = default!;

    private readonly List<ServiceTileViewModel> tiles = new();
    private readonly List<ServiceTileViewModel> classTiles = new();
    private readonly List<ServiceTileViewModel> visibleTiles = new();
    private readonly HashSet<SlaStatus> activeStatuses = new(Enum.GetValues<SlaStatus>());
    private CancellationTokenSource? loadCts;
    private bool isLoading;
    private string? errorMessage;
    private SortOption sortOrder = SortOption.WorstFirst;
    private TimeTravelMetricsResult? metricsResult;
    private TimeTravelMetricsContext? context;
    private IReadOnlyList<string> availableClasses = Array.Empty<string>();
    private IReadOnlyDictionary<string, string> classDisplayNames = emptyClassLabels;
    private IReadOnlyList<string> selectedClasses = Array.Empty<string>();
    private ClassSelectionMode classSelectionMode = ClassSelectionMode.All;
    private string? classCoverageState;
    private TimeTravelStateWindowDto? classWindowData;
    private const int maxClassBins = 720;
    private static readonly IReadOnlyDictionary<string, string> emptyClassLabels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

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
            tiles.Clear();
            visibleTiles.Clear();
            metricsResult = null;
            context = null;
            errorMessage = null;
            isLoading = false;
            CancelLoad();
            return;
        }

        await LoadMetricsAsync().ConfigureAwait(false);
    }

    private async Task LoadMetricsAsync()
    {
        CancelLoad();
        isLoading = true;
        errorMessage = null;
        metricsResult = null;
        context = null;
        tiles.Clear();
        visibleTiles.Clear();
        var cts = new CancellationTokenSource();
        loadCts = cts;

        try
        {
            var result = await MetricsClient.GetMetricsAsync(RunId!, cts.Token).ConfigureAwait(false);
            if (!result.Success || result.Value is null)
            {
                errorMessage = result.Error ?? "Metrics not available.";
                return;
            }

            metricsResult = result.Value;
            context = result.Value.Context;
            tiles.Clear();
            foreach (var service in result.Value.Payload.Services)
            {
                var tile = BuildTile(service);
                if (tile is not null)
                {
                    tiles.Add(tile);
                }
            }

            await LoadClassContextAsync(RunId!, cts.Token).ConfigureAwait(false);
            RebuildVisibleTiles();
        }
        catch (OperationCanceledException)
        {
            // Ignore cancellation.
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
        }
        finally
        {
            if (ReferenceEquals(loadCts, cts))
            {
                loadCts = null;
            }

            cts.Dispose();
            isLoading = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task LoadClassContextAsync(string runId, CancellationToken ct)
    {
        availableClasses = Array.Empty<string>();
        classDisplayNames = emptyClassLabels;
        classCoverageState = null;
        classWindowData = null;
        classTiles.Clear();

        var indexResult = await DataService.GetSeriesIndexAsync(runId, ct).ConfigureAwait(false);
        if (!indexResult.Success || indexResult.Value is null)
        {
            return;
        }

        var index = indexResult.Value;
        classCoverageState = index.ClassCoverage;

        if (index.Classes is not { Count: > 0 })
        {
            Console.WriteLine($"[Dashboard] Class metadata for run {runId}: coverage={classCoverageState ?? "n/a"}, classes=0 (none)");
            return;
        }

        availableClasses = index.Classes
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Id))
            .OrderBy(entry => entry.DisplayName ?? entry.Id, StringComparer.OrdinalIgnoreCase)
            .Select(entry => entry.Id)
            .ToArray();

        Console.WriteLine($"[Dashboard] Class metadata for run {runId}: coverage={classCoverageState ?? "n/a"}, classes={availableClasses.Count} -> {string.Join(", ", availableClasses)}");

        classDisplayNames = index.Classes
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Id))
            .ToDictionary(entry => entry.Id, entry => entry.DisplayName ?? entry.Id, StringComparer.OrdinalIgnoreCase);

        var totalBins = Math.Max(1, index.Grid.Bins);
        var span = Math.Min(totalBins, maxClassBins);
        var sliceEnd = totalBins - 1;
        var sliceStart = Math.Max(0, sliceEnd - (span - 1));

        var windowResult = await DataService.GetStateWindowAsync(runId, sliceStart, sliceEnd, mode: null, ct).ConfigureAwait(false);
        if (!windowResult.Success || windowResult.Value is null)
        {
            return;
        }

        classWindowData = windowResult.Value;
        if (string.IsNullOrWhiteSpace(classCoverageState))
        {
            classCoverageState = classWindowData.Metadata.ClassCoverage;
        }
        EnsureFallbackClassesFromWindow(classWindowData);

        RecomputeClassTiles();
    }

    private void EnsureFallbackClassesFromWindow(TimeTravelStateWindowDto window)
    {
        if (availableClasses.Count > 0 || window.Nodes is null || window.Nodes.Count == 0)
        {
            return;
        }

        var fallback = window.Nodes
            .Where(node => node.ByClass is not null && node.ByClass.Count > 0)
            .SelectMany(node => node.ByClass.Keys)
            .Where(key => !string.IsNullOrWhiteSpace(key) &&
                          !string.Equals(key, "DEFAULT", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(key => classDisplayNames.TryGetValue(key, out var label) && !string.IsNullOrWhiteSpace(label) ? label : key,
                     StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (fallback.Length == 0)
        {
            return;
        }

        if (classDisplayNames == emptyClassLabels)
        {
            classDisplayNames = fallback.ToDictionary(id => id, id => id, StringComparer.OrdinalIgnoreCase);
        }

        availableClasses = fallback;
        Console.WriteLine($"[Dashboard] Fallback class list from window: {string.Join(", ", availableClasses)}");
    }

    internal ServiceTileViewModel? BuildTile(TimeTravelServiceMetricsDto service)
    {
        if (string.IsNullOrWhiteSpace(service.Id))
        {
            return null;
        }

        var status = DetermineStatus(service);
        var mini = service.Mini?.ToArray() ?? Array.Empty<double?>();
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
        var source = HasClassSelection ? classTiles : tiles;
        IEnumerable<ServiceTileViewModel> query = source.Where(tile => activeStatuses.Contains(tile.Status));
        return sortOrder switch
        {
            SortOption.BestFirst => query.OrderByDescending(t => t.SlaPct).ThenBy(t => t.Id, StringComparer.OrdinalIgnoreCase),
            SortOption.Name => query.OrderBy(t => t.Id, StringComparer.OrdinalIgnoreCase),
            _ => query.OrderBy(t => t.SlaPct).ThenBy(t => t.Id, StringComparer.OrdinalIgnoreCase)
        };
    }

    private void RebuildVisibleTiles()
    {
        visibleTiles.Clear();
        visibleTiles.AddRange(GetVisibleTiles());
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
        if (activeStatuses.Contains(status))
        {
            if (activeStatuses.Count > 1)
            {
                activeStatuses.Remove(status);
            }
        }
        else
        {
            activeStatuses.Add(status);
        }

        RebuildVisibleTiles();
        StateHasChanged();
    }

    private void ResetFilters()
    {
        activeStatuses.Clear();
        foreach (var status in Enum.GetValues<SlaStatus>())
        {
            activeStatuses.Add(status);
        }

        RebuildVisibleTiles();
        StateHasChanged();
    }

    private bool ShowClassSelector => availableClasses.Count > 0;
    private bool HasClassSelection => selectedClasses.Count > 0 && classSelectionMode != ClassSelectionMode.All;
    private string ClassCoverageText => FormatCoverageLabel(classCoverageState);

    private static string FormatCoverageLabel(string? coverage) =>
        coverage switch
        {
            "full" => "Class coverage: Full",
            "partial" => "Class coverage: Partial",
            "missing" => "Class coverage: Missing",
            _ => string.IsNullOrWhiteSpace(coverage) ? string.Empty : $"Class coverage: {coverage}"
        };

    private Task OnClassSelectionChanged(IReadOnlyList<string> classes)
    {
        selectedClasses = classes;
        RecomputeClassTiles();
        StateHasChanged();
        return Task.CompletedTask;
    }

    private Task OnClassSelectionModeChanged(ClassSelectionMode mode)
    {
        classSelectionMode = mode;
        RecomputeClassTiles();
        StateHasChanged();
        return Task.CompletedTask;
    }

    private void RecomputeClassTiles()
    {
        classTiles.Clear();
        if (!HasClassSelection || classWindowData is null)
        {
            RebuildVisibleTiles();
            return;
        }

        foreach (var node in classWindowData.Nodes)
        {
            var dto = BuildClassAwareMetrics(node, classWindowData.Window.BinCount);
            if (dto is null)
            {
                continue;
            }

            var tile = BuildTile(dto);
            if (tile is not null)
            {
                classTiles.Add(tile);
            }
        }

        RebuildVisibleTiles();
    }

    private TimeTravelServiceMetricsDto? BuildClassAwareMetrics(TimeTravelNodeSeriesDto node, int windowBinCount)
    {
        if (!IsServiceLike(node.LogicalType) || node.ByClass is null)
        {
            return null;
        }

        var arrivals = AggregateClassSeries(node, "arrivals");
        var served = AggregateClassSeries(node, "served");

        if (arrivals is null || served is null)
        {
            return null;
        }

        return ComputeServiceMetrics(node.Id, arrivals, served, windowBinCount);
    }

    private double?[]? AggregateClassSeries(TimeTravelNodeSeriesDto node, string key)
    {
        if (node.ByClass is null)
        {
            return null;
        }

        double?[]? accumulator = null;
        foreach (var classId in selectedClasses)
        {
            if (!node.ByClass.TryGetValue(classId, out var classMetrics) ||
                classMetrics is null ||
                !classMetrics.TryGetValue(key, out var series) ||
                series is null ||
                series.Length == 0)
            {
                continue;
            }

            accumulator ??= new double?[series.Length];
            var limit = Math.Min(accumulator.Length, series.Length);
            for (var i = 0; i < limit; i++)
            {
                var value = series[i];
                if (!value.HasValue)
                {
                    continue;
                }

                accumulator[i] = (accumulator[i] ?? 0d) + value.Value;
            }
        }

        return accumulator;
    }

    private static bool IsServiceLike(string? logicalType)
    {
        if (string.IsNullOrWhiteSpace(logicalType))
        {
            return false;
        }

        return string.Equals(logicalType, "service", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(logicalType, "serviceWithBuffer", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(logicalType, "flow", StringComparison.OrdinalIgnoreCase);
    }

    private const double classSlaThreshold = 0.95d;

    private static TimeTravelServiceMetricsDto? ComputeServiceMetrics(string nodeId, double?[] arrivals, double?[] served, int windowBinCount)
    {
        var clampCount = Math.Min(Math.Min(arrivals.Length, served.Length), windowBinCount > 0 ? windowBinCount : int.MaxValue);
        if (clampCount <= 0)
        {
            return null;
        }

        var ratios = new double?[clampCount];
        var binsMet = 0;
        var binsEvaluated = 0;

        for (var i = 0; i < clampCount; i++)
        {
            var arrivalsValue = arrivals[i];
            var servedValue = served[i];

            if (!arrivalsValue.HasValue || !servedValue.HasValue)
            {
                ratios[i] = null;
                continue;
            }

            var ratio = arrivalsValue.Value <= 0 ? 1d : servedValue.Value / arrivalsValue.Value;
            ratio = Math.Max(0d, double.IsFinite(ratio) ? ratio : 0d);
            ratio = Math.Min(ratio, 1d);

            ratios[i] = ratio;
            binsEvaluated++;

            if (ratio >= classSlaThreshold)
            {
                binsMet++;
            }
        }

        var slaPct = binsEvaluated > 0 ? binsMet / (double)binsEvaluated : 0d;
        return new TimeTravelServiceMetricsDto
        {
            Id = nodeId,
            SlaPct = slaPct,
            BinsMet = binsMet,
            BinsTotal = binsEvaluated,
            Mini = ratios
        };
    }

    internal static IReadOnlyList<LineSegment> BuildLineSegments(IReadOnlyList<double?> values)
    {
        var segments = new List<LineSegment>();
        if (values.Count == 0)
        {
            return segments;
        }

        var currentPoints = new List<string>();
        string? currentColor = null;
        double Scale(double v) => 1d - Math.Clamp(v, 0d, 1d);

        void Flush()
        {
            if (currentPoints.Count > 1 && currentColor is not null)
            {
                segments.Add(new LineSegment(string.Join(' ', currentPoints), currentColor));
            }
            currentPoints.Clear();
            currentColor = null;
        }

        for (var i = 0; i < values.Count; i++)
        {
            var value = values[i];
            if (!value.HasValue)
            {
                Flush();
                continue;
            }

            var y = Scale(value.Value);
            var color = value.Value switch
            {
                >= 0.95d => "#27ae60",
                >= 0.9d => "#f39c12",
                _ => "#e74c3c"
            };

            if (currentColor is null)
            {
                currentColor = color;
            }
            else if (!string.Equals(currentColor, color, StringComparison.Ordinal))
            {
                Flush();
                currentColor = color;
            }

            currentPoints.Add(FormattableString.Invariant($"{i},{y:0.###}"));
        }

        Flush();
        return segments;
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
        if (loadCts is null)
        {
            return;
        }

        try
        {
            loadCts.Cancel();
        }
        catch
        {
            // Ignore cancellation errors.
        }
        finally
        {
            loadCts.Dispose();
            loadCts = null;
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
        if (context is null)
        {
            return string.Empty;
        }

        if (!context.WindowStart.HasValue)
        {
            return $"{context.BinCount} bins × {context.BinMinutes} min";
        }

        var start = context.WindowStart.Value;
        var end = context.WindowEnd ?? start.AddMinutes(context.BinMinutes * context.BinCount);
        return $"{start.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)} → {end.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)} {metricsResult?.Payload.Window.Timezone ?? "UTC"}";
    }

    private string GetTemplateSummary()
    {
        if (context is null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(context.TemplateTitle))
        {
            return context.TemplateTitle!;
        }

        if (!string.IsNullOrWhiteSpace(context.TemplateId))
        {
            return context.TemplateId!;
        }

        return context.RunId;
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
        IReadOnlyList<double?> Mini,
        SlaStatus Status,
        MudBlazor.Color ChipColor,
        string StatusLabel,
        string SlaText,
        string BinsSummary,
        string AriaLabel,
        string SparklineLabel);

    internal sealed record LineSegment(string Points, string Color);
}
