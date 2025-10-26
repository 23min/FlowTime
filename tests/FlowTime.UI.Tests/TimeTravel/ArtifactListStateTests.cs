using System;
using System.Collections.Generic;
using System.Linq;
using FlowTime.UI.Pages.TimeTravel;
using FlowTime.UI.Services;

namespace FlowTime.UI.Tests.TimeTravel;

public sealed class ArtifactListStateTests
{
    [Fact]
    public void ApplyFilters_WithStatusAndMode_FiltersRuns()
    {
        var runs = new[]
        {
            CreateEntry("run_20250101T010101Z_alpha", "Alpha Template", "simulation", telemetryAvailable: true, warningCount: 0),
            CreateEntry("run_20250101T020202Z_bravo", "Bravo Template", "telemetry", telemetryAvailable: true, warningCount: 2),
            CreateEntry("run_20250101T030303Z_charlie", "Charlie Template", "simulation", telemetryAvailable: false, warningCount: 0)
        };

        var state = new ArtifactListState(runs)
        {
            PageSize = 100
        };

        state.SetStatuses(new[] { ArtifactRunStatus.Healthy });
        state.SetModes(new[] { "simulation" });

        var result = state.Apply();

        Assert.Single(result.Items);
        Assert.Equal("run_20250101T010101Z_alpha", result.Items[0].RunId);
        Assert.Equal(1, result.TotalItems);
    }

    [Fact]
    public void ApplyFilters_WithWarningsFilter_ReturnsOnlyWarnings()
    {
        var runs = new[]
        {
            CreateEntry("run_a", "Template A", "simulation", telemetryAvailable: true, warningCount: 0),
            CreateEntry("run_b", "Template B", "simulation", telemetryAvailable: true, warningCount: 3),
            CreateEntry("run_c", "Template C", "telemetry", telemetryAvailable: false, warningCount: 0)
        };

        var state = new ArtifactListState(runs)
        {
            PageSize = 100,
            WarningFilter = ArtifactWarningFilter.HasWarnings
        };

        var result = state.Apply();

        var single = Assert.Single(result.Items);
        Assert.Equal("run_b", single.RunId);
        Assert.Equal(1, result.TotalItems);
    }

    [Fact]
    public void ApplyFilters_WithSearch_MatchesTemplateAndRunId()
    {
        var runs = new[]
        {
            CreateEntry("run_123", "Solar Array", "simulation", telemetryAvailable: true, warningCount: 0),
            CreateEntry("run_456", "Water Reclamation", "telemetry", telemetryAvailable: false, warningCount: 0),
            CreateEntry("run_789", "Power Grid", "simulation", telemetryAvailable: true, warningCount: 0)
        };

        var state = new ArtifactListState(runs)
        {
            PageSize = 100,
            SearchText = "water"
        };

        var result = state.Apply();

        var match = Assert.Single(result.Items);
        Assert.Equal("run_456", match.RunId);

        state.SearchText = "789";
        result = state.Apply();
        match = Assert.Single(result.Items);
        Assert.Equal("run_789", match.RunId);
    }

    [Fact]
    public void Apply_SortsByTemplateAndStatus()
    {
        var runs = new[]
        {
            CreateEntry("run_a", "Zeta Template", "simulation", telemetryAvailable: true, warningCount: 0, createdUtc: new DateTimeOffset(2025, 1, 1, 1, 0, 0, TimeSpan.Zero)),
            CreateEntry("run_b", "Alpha Template", "telemetry", telemetryAvailable: false, warningCount: 0, createdUtc: new DateTimeOffset(2025, 1, 1, 2, 0, 0, TimeSpan.Zero)),
            CreateEntry("run_c", "Bravo Template", "simulation", telemetryAvailable: true, warningCount: 5, createdUtc: new DateTimeOffset(2025, 1, 1, 3, 0, 0, TimeSpan.Zero))
        };

        var state = new ArtifactListState(runs)
        {
            PageSize = 100,
            SortOption = ArtifactSortOption.Template
        };

        var result = state.Apply();

        Assert.Equal(new[] { "run_b", "run_c", "run_a" }, result.Items.Select(r => r.RunId));

        state.SortOption = ArtifactSortOption.Status;
        result = state.Apply();
        Assert.Equal(new[] { "run_a", "run_b", "run_c" }, result.Items.Select(r => r.RunId));
    }

    [Fact]
    public void Apply_PaginatesResults()
    {
        var runs = Enumerable.Range(0, 5)
            .Select(i => CreateEntry($"run_{i}", $"Template {i}", i % 2 == 0 ? "simulation" : "telemetry", telemetryAvailable: i % 2 == 0, warningCount: 0))
            .ToArray();

        var state = new ArtifactListState(runs)
        {
            PageSize = 2,
            PageIndex = 2
        };

        var result = state.Apply();

        Assert.Equal(2, result.Items.Count);
        Assert.Equal("run_2", result.Items[0].RunId);
        Assert.Equal(5, result.TotalItems);
        Assert.Equal(3, result.TotalPages);
    }

    [Fact]
    public void QueryRoundTrip_PreservesState()
    {
        var runs = new[]
        {
            CreateEntry("run_ref", "Reference Template", "simulation", telemetryAvailable: true, warningCount: 0)
        };
        var state = new ArtifactListState(runs)
        {
            PageSize = 100,
            PageIndex = 3,
            SearchText = "grid",
            SortOption = ArtifactSortOption.Status,
            WarningFilter = ArtifactWarningFilter.NoWarnings
        };
        state.SetStatuses(new[] { ArtifactRunStatus.Healthy, ArtifactRunStatus.Pending });
        state.SetModes(new[] { "simulation" });

        var query = state.ToQueryParameters();

        Assert.True(query.TryGetValue("status", out var statusValue));
        Assert.Equal("healthy,pending", statusValue);
        Assert.Equal("simulation", query["mode"]);
        Assert.Equal("none", query["warnings"]);
        Assert.Equal("status", query["sort"]);
        Assert.Equal("grid", query["search"]);
        Assert.Equal("3", query["page"]);

        var rehydrated = ArtifactListState.FromQuery(
            runs,
            query.ToDictionary(kvp => kvp.Key, kvp => (string?)kvp.Value));
        Assert.Equal(state.PageIndex, rehydrated.PageIndex);
        Assert.Equal(state.SearchText, rehydrated.SearchText);
        Assert.Equal(state.SortOption, rehydrated.SortOption);
        Assert.Equal(state.WarningFilter, rehydrated.WarningFilter);
        Assert.Equal(state.SelectedStatuses.OrderBy(s => s), rehydrated.SelectedStatuses.OrderBy(s => s));
        Assert.Equal(state.SelectedModes.OrderBy(m => m), rehydrated.SelectedModes.OrderBy(m => m));
    }

    [Fact]
    public void FromQuery_UsesDefaults_WhenInvalid()
    {
        var runs = Array.Empty<RunListEntry>();
        var query = new Dictionary<string, string?>
        {
            ["status"] = "unknown",
            ["mode"] = "bogus",
            ["warnings"] = "other",
            ["sort"] = "bogus",
            ["page"] = "-2"
        };

        var state = ArtifactListState.FromQuery(runs, query);

        Assert.Empty(state.SelectedStatuses);
        Assert.Empty(state.SelectedModes);
        Assert.Equal(ArtifactWarningFilter.All, state.WarningFilter);
        Assert.Equal(ArtifactSortOption.Created, state.SortOption);
        Assert.Equal(1, state.PageIndex);
    }

    private static RunListEntry CreateEntry(
        string runId,
        string templateTitle,
        string mode,
        bool telemetryAvailable,
        int warningCount,
        DateTimeOffset? createdUtc = null)
    {
        createdUtc ??= DateTimeOffset.UtcNow;
        return new RunListEntry(
            RunId: runId,
            TemplateId: templateTitle.ToLowerInvariant().Replace(" ", "-"),
            TemplateTitle: templateTitle,
            TemplateVersion: "1.0",
            Source: mode,
            CreatedUtc: createdUtc,
            WarningCount: warningCount,
            Telemetry: new RunTelemetrySummaryDto(telemetryAvailable, createdUtc.Value.ToString("O"), warningCount, null),
            Rng: null,
            FirstWarningMessage: warningCount > 0 ? "Warning" : null,
            Warnings: Array.Empty<RunWarningInfo>(),
            Grid: new GridSummary(24, 60),
            Presence: new ArtifactPresence(HasModel: true, HasManifest: true, HasIndex: true, HasSeries: true),
            CanReplay: true,
            CanOpen: true);
    }
}
