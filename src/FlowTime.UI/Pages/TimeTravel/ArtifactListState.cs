using System.Collections.ObjectModel;
using System.Globalization;

using FlowTime.UI.Services;

namespace FlowTime.UI.Pages.TimeTravel;

public enum ArtifactRunStatus
{
    Healthy,
    Pending,
    Attention
}

public enum ArtifactWarningFilter
{
    All,
    HasWarnings,
    NoWarnings
}

public enum ArtifactTelemetryFilter
{
    All,
    Available,
    Missing
}

public enum ArtifactSortOption
{
    Created,
    Status,
    Template,
    RunId,
    Grid,
    Telemetry
}

public enum ArtifactSortDirection
{
    Ascending,
    Descending
}

internal sealed record ArtifactListResult(
    IReadOnlyList<RunListEntry> Items,
    int TotalItems,
    int TotalPages,
    int PageIndex);

internal sealed class ArtifactListState
{
    private readonly IReadOnlyList<RunListEntry> runs;
    private readonly HashSet<ArtifactRunStatus> selectedStatuses = new();
    private readonly HashSet<string> selectedModes = new(StringComparer.OrdinalIgnoreCase);

    public ArtifactListState(IReadOnlyList<RunListEntry> runs)
    {
        this.runs = runs ?? throw new ArgumentNullException(nameof(runs));
    }

    public int PageSize { get; set; } = ArtifactList.DefaultPageSize;
    public int PageIndex { get; set; } = 1;
    public string SearchText { get; set; } = string.Empty;
    public ArtifactWarningFilter WarningFilter { get; set; } = ArtifactWarningFilter.All;
    public ArtifactTelemetryFilter TelemetryFilter { get; set; } = ArtifactTelemetryFilter.All;
    public ArtifactSortOption SortOption { get; set; } = ArtifactSortOption.Created;
    public ArtifactSortDirection SortDirection { get; set; } = ArtifactSortDirection.Descending;

    public IReadOnlyCollection<ArtifactRunStatus> SelectedStatuses => new ReadOnlyCollection<ArtifactRunStatus>(selectedStatuses.ToList());
    public IReadOnlyCollection<string> SelectedModes => new ReadOnlyCollection<string>(selectedModes.ToList());

    public void SetStatuses(IEnumerable<ArtifactRunStatus> statuses)
    {
        selectedStatuses.Clear();
        foreach (var status in statuses)
        {
            selectedStatuses.Add(status);
        }
    }

    public void SetModes(IEnumerable<string> modes)
    {
        selectedModes.Clear();
        foreach (var mode in modes)
        {
            var normalized = NormalizeMode(mode);
            if (!string.IsNullOrWhiteSpace(normalized) && IsKnownMode(normalized))
            {
                selectedModes.Add(normalized);
            }
        }
    }

    public bool ToggleStatus(ArtifactRunStatus status)
    {
        if (!selectedStatuses.Add(status))
        {
            selectedStatuses.Remove(status);
            return false;
        }

        return true;
    }

    public bool ToggleMode(string mode)
    {
        var normalized = NormalizeMode(mode);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        if (selectedModes.Contains(normalized))
        {
            selectedModes.Remove(normalized);
            return false;
        }

        if (!IsKnownMode(normalized))
        {
            return false;
        }

        selectedModes.Add(normalized);
        return true;
    }

    public bool IsStatusSelected(ArtifactRunStatus status) => selectedStatuses.Contains(status);

    public bool IsModeSelected(string mode)
    {
        var normalized = NormalizeMode(mode);
        return !string.IsNullOrWhiteSpace(normalized) && selectedModes.Contains(normalized);
    }

    public ArtifactListState CloneForRuns(IReadOnlyList<RunListEntry> newRuns)
    {
        var clone = new ArtifactListState(newRuns)
        {
            PageSize = PageSize,
            PageIndex = PageIndex,
            SearchText = SearchText,
            WarningFilter = WarningFilter,
            TelemetryFilter = TelemetryFilter,
            SortOption = SortOption,
            SortDirection = SortDirection
        };
        clone.SetStatuses(selectedStatuses);
        clone.SetModes(selectedModes);
        return clone;
    }

    public ArtifactListResult Apply()
    {
        var filtered = runs.Where(MatchesFilters).ToList();

        var ordered = Sort(filtered);

        var total = ordered.Count;
        var pageSize = Math.Max(1, PageSize);
        var pageIndex = Math.Max(1, PageIndex);

        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        if (pageIndex > totalPages)
        {
            pageIndex = totalPages;
        }

        var skip = (pageIndex - 1) * pageSize;
        var items = ordered.Skip(skip).Take(pageSize).ToList();

        return new ArtifactListResult(items, total, totalPages, pageIndex);
    }

    public Dictionary<string, string> ToQueryParameters()
    {
        var query = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (selectedStatuses.Count > 0)
        {
            query["status"] = string.Join(",", selectedStatuses
                .Select(ToStatusKey)
                .OrderBy(v => v, StringComparer.OrdinalIgnoreCase));
        }

        if (selectedModes.Count > 0)
        {
            query["mode"] = string.Join(",", selectedModes
                .Select(m => m.ToLowerInvariant())
                .OrderBy(v => v, StringComparer.OrdinalIgnoreCase));
        }

        if (WarningFilter != ArtifactWarningFilter.All)
        {
            query["warnings"] = WarningFilter switch
            {
                ArtifactWarningFilter.HasWarnings => "present",
                ArtifactWarningFilter.NoWarnings => "none",
                _ => "all"
            };
        }

        if (TelemetryFilter != ArtifactTelemetryFilter.All)
        {
            query["telemetry"] = TelemetryFilter switch
            {
                ArtifactTelemetryFilter.Available => "available",
                ArtifactTelemetryFilter.Missing => "missing",
                _ => "all"
            };
        }

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            query["search"] = SearchText;
        }

        if (SortOption != ArtifactSortOption.Created)
        {
            query["sort"] = SortOption switch
            {
                ArtifactSortOption.Status => "status",
                ArtifactSortOption.Template => "template",
                ArtifactSortOption.RunId => "run",
                ArtifactSortOption.Grid => "grid",
                ArtifactSortOption.Telemetry => "telemetry",
                _ => "created"
            };
        }

        var defaultDirection = GetDefaultDirection(SortOption);
        if (SortDirection != defaultDirection)
        {
            query["sortDir"] = SortDirection == ArtifactSortDirection.Ascending ? "asc" : "desc";
        }

        if (PageIndex > 1)
        {
            query["page"] = PageIndex.ToString(CultureInfo.InvariantCulture);
        }

        return query;
    }

    public static ArtifactListState FromQuery(IReadOnlyList<RunListEntry> runs, IReadOnlyDictionary<string, string?> query)
    {
        var state = new ArtifactListState(runs);

        if (query.TryGetValue("status", out var statusValue) && !string.IsNullOrWhiteSpace(statusValue))
        {
            var statuses = statusValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(ParseStatus)
                .Where(s => s is not null)
                .Select(s => s!.Value);
            state.SetStatuses(statuses);
        }

        if (query.TryGetValue("mode", out var modeValue) && !string.IsNullOrWhiteSpace(modeValue))
        {
            var modes = modeValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            state.SetModes(modes);
        }

        if (query.TryGetValue("warnings", out var warningsValue) && !string.IsNullOrWhiteSpace(warningsValue))
        {
            state.WarningFilter = warningsValue.ToLowerInvariant() switch
            {
                "present" => ArtifactWarningFilter.HasWarnings,
                "none" => ArtifactWarningFilter.NoWarnings,
                _ => ArtifactWarningFilter.All
            };
        }

        if (query.TryGetValue("telemetry", out var telemetryValue) && !string.IsNullOrWhiteSpace(telemetryValue))
        {
            state.TelemetryFilter = telemetryValue.ToLowerInvariant() switch
            {
                "available" => ArtifactTelemetryFilter.Available,
                "missing" => ArtifactTelemetryFilter.Missing,
                _ => ArtifactTelemetryFilter.All
            };
        }

        if (query.TryGetValue("sort", out var sortValue) && !string.IsNullOrWhiteSpace(sortValue))
        {
            state.SortOption = sortValue.ToLowerInvariant() switch
            {
                "status" => ArtifactSortOption.Status,
                "template" => ArtifactSortOption.Template,
                "run" => ArtifactSortOption.RunId,
                "grid" => ArtifactSortOption.Grid,
                "telemetry" => ArtifactSortOption.Telemetry,
                _ => ArtifactSortOption.Created
            };
        }

        state.SortDirection = GetDefaultDirection(state.SortOption);
        if (query.TryGetValue("sortDir", out var sortDirValue) && !string.IsNullOrWhiteSpace(sortDirValue))
        {
            state.SortDirection = sortDirValue.ToLowerInvariant() switch
            {
                "asc" => ArtifactSortDirection.Ascending,
                "desc" => ArtifactSortDirection.Descending,
                _ => state.SortDirection
            };
        }
        if (query.TryGetValue("search", out var searchValue) && !string.IsNullOrWhiteSpace(searchValue))
        {
            state.SearchText = searchValue;
        }

        if (query.TryGetValue("page", out var pageValue) &&
            int.TryParse(pageValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var page) &&
            page > 0)
        {
            state.PageIndex = page;
        }

        return state;
    }

    public static ArtifactRunStatus DetermineStatus(RunListEntry run)
    {
        if (run is null)
        {
            throw new ArgumentNullException(nameof(run));
        }

        if (run.WarningCount > 0)
        {
            return ArtifactRunStatus.Attention;
        }

        if (run.Telemetry?.Available == true)
        {
            return ArtifactRunStatus.Healthy;
        }

        return ArtifactRunStatus.Pending;
    }

    private static string ToStatusKey(ArtifactRunStatus status) => status switch
    {
        ArtifactRunStatus.Healthy => "healthy",
        ArtifactRunStatus.Pending => "pending",
        ArtifactRunStatus.Attention => "attention",
        _ => status.ToString().ToLowerInvariant()
    };

    private static ArtifactRunStatus? ParseStatus(string value) => value.ToLowerInvariant() switch
    {
        "healthy" => ArtifactRunStatus.Healthy,
        "pending" => ArtifactRunStatus.Pending,
        "attention" => ArtifactRunStatus.Attention,
        _ => null
    };

    private static string NormalizeMode(string mode) => string.IsNullOrWhiteSpace(mode)
        ? string.Empty
        : mode.Trim();

    private bool IsKnownMode(string normalized)
    {
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        return runs.Any(r => string.Equals(NormalizeMode(r.Source), normalized, StringComparison.OrdinalIgnoreCase));
    }

    private bool MatchesFilters(RunListEntry run)
    {
        var status = DetermineStatus(run);
        if (selectedStatuses.Count > 0 && !selectedStatuses.Contains(status))
        {
            return false;
        }

        if (selectedModes.Count > 0 && !selectedModes.Contains(NormalizeMode(run.Source)))
        {
            return false;
        }

        var hasWarnings = run.WarningCount > 0;
        if (WarningFilter == ArtifactWarningFilter.HasWarnings && !hasWarnings)
        {
            return false;
        }

        if (WarningFilter == ArtifactWarningFilter.NoWarnings && hasWarnings)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var filter = SearchText.Trim();
            if (!MatchesSearch(run, filter))
            {
                return false;
            }
        }

        var telemetryAvailable = run.Telemetry?.Available == true;
        if (TelemetryFilter == ArtifactTelemetryFilter.Available && !telemetryAvailable)
        {
            return false;
        }

        if (TelemetryFilter == ArtifactTelemetryFilter.Missing && telemetryAvailable)
        {
            return false;
        }

        return true;
    }

    private static bool MatchesSearch(RunListEntry run, string filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            return true;
        }

        filter = filter.Trim();
        var comparison = StringComparison.OrdinalIgnoreCase;

        if (!string.IsNullOrWhiteSpace(run.TemplateTitle) &&
            run.TemplateTitle.Contains(filter, comparison))
        {
            return true;
        }

        if (run.RunId.Contains(filter, comparison))
        {
            return true;
        }

        var shortId = ArtifactList.GetShortRunId(run.RunId);
        if (!string.IsNullOrEmpty(shortId) && shortId.Contains(filter, comparison))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(run.TemplateId) &&
            run.TemplateId.Contains(filter, comparison))
        {
            return true;
        }

        return false;
    }

    public static ArtifactSortDirection GetDefaultDirection(ArtifactSortOption option) =>
        option switch
        {
            ArtifactSortOption.Created => ArtifactSortDirection.Descending,
            ArtifactSortOption.Template => ArtifactSortDirection.Ascending,
            ArtifactSortOption.Status => ArtifactSortDirection.Ascending,
            ArtifactSortOption.RunId => ArtifactSortDirection.Ascending,
            ArtifactSortOption.Grid => ArtifactSortDirection.Descending,
            ArtifactSortOption.Telemetry => ArtifactSortDirection.Descending,
            _ => ArtifactSortDirection.Descending
        };

    private IList<RunListEntry> Sort(IList<RunListEntry> runsToSort)
    {
        IOrderedEnumerable<RunListEntry> ordered;

        switch (SortOption)
        {
            case ArtifactSortOption.Status:
                ordered = SortDirection == ArtifactSortDirection.Descending
                    ? runsToSort.OrderByDescending(r => DetermineStatus(r))
                    : runsToSort.OrderBy(r => DetermineStatus(r));
                ordered = ordered
                    .ThenByDescending(r => r.CreatedUtc ?? DateTimeOffset.MinValue)
                    .ThenBy(r => r.RunId, StringComparer.OrdinalIgnoreCase);
                break;
            case ArtifactSortOption.Template:
                ordered = SortDirection == ArtifactSortDirection.Descending
                    ? runsToSort.OrderByDescending(r => r.TemplateTitle ?? r.TemplateId ?? r.RunId, StringComparer.OrdinalIgnoreCase)
                    : runsToSort.OrderBy(r => r.TemplateTitle ?? r.TemplateId ?? r.RunId, StringComparer.OrdinalIgnoreCase);
                ordered = ordered
                    .ThenBy(r => r.TemplateId ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                    .ThenByDescending(r => r.CreatedUtc ?? DateTimeOffset.MinValue)
                    .ThenBy(r => r.RunId, StringComparer.OrdinalIgnoreCase);
                break;
            case ArtifactSortOption.RunId:
                ordered = SortDirection == ArtifactSortDirection.Descending
                    ? runsToSort.OrderByDescending(r => r.RunId, StringComparer.OrdinalIgnoreCase)
                    : runsToSort.OrderBy(r => r.RunId, StringComparer.OrdinalIgnoreCase);
                ordered = ordered.ThenByDescending(r => r.CreatedUtc ?? DateTimeOffset.MinValue);
                break;
            case ArtifactSortOption.Grid:
                ordered = SortDirection == ArtifactSortDirection.Descending
                    ? runsToSort
                        .OrderByDescending(r => r.Grid.IsAvailable)
                        .ThenByDescending(r => r.Grid.Bins)
                        .ThenByDescending(r => r.Grid.BinMinutes)
                    : runsToSort
                        .OrderBy(r => r.Grid.IsAvailable)
                        .ThenBy(r => r.Grid.Bins)
                        .ThenBy(r => r.Grid.BinMinutes);
                ordered = ordered
                    .ThenByDescending(r => r.CreatedUtc ?? DateTimeOffset.MinValue)
                    .ThenBy(r => r.RunId, StringComparer.OrdinalIgnoreCase);
                break;
            case ArtifactSortOption.Telemetry:
                ordered = SortDirection == ArtifactSortDirection.Descending
                    ? runsToSort.OrderByDescending(r => r.Telemetry?.Available == true)
                    : runsToSort.OrderBy(r => r.Telemetry?.Available == true);
                ordered = ordered
                    .ThenBy(r => r.Telemetry?.WarningCount ?? r.WarningCount)
                    .ThenByDescending(r => r.CreatedUtc ?? DateTimeOffset.MinValue)
                    .ThenBy(r => r.RunId, StringComparer.OrdinalIgnoreCase);
                break;
            default:
                ordered = SortDirection == ArtifactSortDirection.Ascending
                    ? runsToSort.OrderBy(r => r.CreatedUtc ?? DateTimeOffset.MinValue)
                    : runsToSort.OrderByDescending(r => r.CreatedUtc ?? DateTimeOffset.MinValue);
                ordered = ordered.ThenBy(r => r.RunId, StringComparer.OrdinalIgnoreCase);
                break;
        }

        return ordered.ToList();
    }
}
