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

public enum ArtifactSortOption
{
    Created,
    Status,
    Template
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
    public ArtifactSortOption SortOption { get; set; } = ArtifactSortOption.Created;

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
            SortOption = SortOption
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
                _ => "created"
            };
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

        if (query.TryGetValue("sort", out var sortValue) && !string.IsNullOrWhiteSpace(sortValue))
        {
            state.SortOption = sortValue.ToLowerInvariant() switch
            {
                "status" => ArtifactSortOption.Status,
                "template" => ArtifactSortOption.Template,
                _ => ArtifactSortOption.Created
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

    private IList<RunListEntry> Sort(IList<RunListEntry> runsToSort)
    {
        var ordered = SortOption switch
        {
            ArtifactSortOption.Status => runsToSort
                .OrderBy(r => DetermineStatus(r))
                .ThenByDescending(r => r.CreatedUtc ?? DateTimeOffset.MinValue)
                .ThenBy(r => r.RunId, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            ArtifactSortOption.Template => runsToSort
                .OrderBy(r => r.TemplateTitle ?? r.TemplateId ?? r.RunId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(r => r.TemplateId ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ThenByDescending(r => r.CreatedUtc ?? DateTimeOffset.MinValue)
                .ThenBy(r => r.RunId, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            _ => runsToSort
                .OrderByDescending(r => r.CreatedUtc ?? DateTimeOffset.MinValue)
                .ThenBy(r => r.RunId, StringComparer.OrdinalIgnoreCase)
                .ToList()
        };

        return ordered;
    }
}
