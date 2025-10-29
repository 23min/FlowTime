using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bunit;
using FlowTime.UI.Components.Artifacts;
using FlowTime.UI.Pages.TimeTravel;
using FlowTime.UI.Services;
using FlowTime.UI.Components.Topology;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor;
using MudBlazor.Services;

namespace FlowTime.UI.Tests.TimeTravel;

public sealed class ArtifactListRenderTests : TestContext
{
    private readonly StubRunDiscoveryService runDiscovery;
    private readonly StubFlowTimeApiClient apiClient;
    private readonly NavigationManager navigation;

    public ArtifactListRenderTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;

        runDiscovery = new StubRunDiscoveryService();
        apiClient = new StubFlowTimeApiClient();

        Services.AddSingleton<IRunDiscoveryService>(runDiscovery);
        Services.AddSingleton<IFlowTimeApiClient>(apiClient);
        Services.AddSingleton<ILogger<ArtifactList>>(NullLogger<ArtifactList>.Instance);
        Services.AddSingleton<ILogger<RunDetailsDrawer>>(NullLogger<RunDetailsDrawer>.Instance);
        Services.AddSingleton<ILogger<RunCard>>(NullLogger<RunCard>.Instance);
        Services.AddSingleton<INotificationService>(new StubNotificationService());

        RenderComponent<MudPopoverProvider>();
        navigation = Services.GetRequiredService<NavigationManager>();
    }

    [Fact]
    public void RendersCardsWithActions()
    {
        navigation.NavigateTo("http://localhost/time-travel/artifacts");
        runDiscovery.SetRuns(CreateRuns());
        SetupLocalStorage();

        var cut = RenderArtifactList();

        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll("[data-testid='artifact-card']").Count));

        var firstCard = cut.Find("[data-testid='artifact-card']");
        var buttons = firstCard.QuerySelectorAll("button");
        Assert.Equal(2, buttons.Length);
        Assert.All(buttons, b =>
        {
            var className = b.GetAttribute("class") ?? string.Empty;
            Assert.True(className.Contains("mud-icon-button", StringComparison.OrdinalIgnoreCase));
        });
    }

    [Fact]
    public void EnterKeyOpensDrawer()
    {
        navigation.NavigateTo("http://localhost/time-travel/artifacts");
        var runs = CreateRuns();
        var targetRun = runs.OrderByDescending(r => r.CreatedUtc).First();
        runDiscovery.SetRuns(runs);
        SetupLocalStorage();
        apiClient.SetRunDetail(targetRun.RunId, CreateDetail(targetRun));

        var cut = RenderArtifactList();
        cut.WaitForAssertion(() => Assert.Equal(runs.Count, cut.FindAll("[data-testid='artifact-card']").Count));

        var firstCard = cut.Find("[data-testid='artifact-card']");
        firstCard.KeyDown(new KeyboardEventArgs { Key = "Enter", Code = "Enter" });

        cut.WaitForAssertion(() => Assert.True(cut.Instance.IsDrawerOpen));
        cut.WaitForAssertion(() =>
        {
            var container = cut.Find("[data-testid='run-details-container']");
            Console.WriteLine(container.GetAttribute("class"));
            Assert.Contains("artifact-details-container", container.GetAttribute("class") ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("is-open", container.GetAttribute("class") ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void FilterSelectionUpdatesQueryParameters()
    {
        navigation.NavigateTo("http://localhost/time-travel/artifacts");
        runDiscovery.SetRuns(CreateRuns());
        SetupLocalStorage();

        var cut = RenderArtifactList();
        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll("[data-testid='artifact-card']").Count));

        var warningChip = cut.Find("[data-testid='filter-warnings-present']");
        warningChip.Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("warnings=present", navigation.Uri);
        });
    }

    [Fact]
    public void RunIdQueryStringOpensDrawer()
    {
        var runs = CreateRuns();
        navigation.NavigateTo($"http://localhost/time-travel/artifacts?runId={runs[1].RunId}");
        runDiscovery.SetRuns(runs);
        SetupLocalStorage();
        apiClient.SetRunDetail(runs[1].RunId, CreateDetail(runs[1]));

        var cut = RenderArtifactList();

        cut.WaitForAssertion(() => Assert.True(cut.Instance.IsDrawerOpen));
    }

    private void SetupLocalStorage()
    {
        JSInterop.Setup<string?>("localStorage.getItem", call =>
            call.Arguments[0]?.ToString() == ArtifactList.LocalStorageKey).SetResult(null);

        JSInterop.SetupVoid("localStorage.setItem", call =>
            call.Arguments[0]?.ToString() == ArtifactList.LocalStorageKey);

        JSInterop.SetupVoid("mudKeyInterceptor.connect", _ => true);
        JSInterop.SetupVoid("mudKeyInterceptor.disconnect", _ => true);
        JSInterop.SetupVoid("mudElementRef.addOnBlurEvent", _ => true);
        JSInterop.SetupVoid("mudElementRef.removeOnBlurEvent", _ => true);
        JSInterop.SetupVoid("mudPopover.initialize", _ => true);
        JSInterop.SetupVoid("mudPopover.destroy", _ => true);
    }

    private static IReadOnlyList<RunListEntry> CreateRuns()
    {
        return new[]
        {
            new RunListEntry(
                "run_a",
                "template.a",
                "Alpha Template",
                "1.0",
                "simulation",
                new DateTimeOffset(2025, 1, 1, 1, 0, 0, TimeSpan.Zero),
                0,
                new RunTelemetrySummaryDto(true, "2025-01-01T01:00:00Z", 0, null),
                null,
                null,
                Array.Empty<RunWarningInfo>(),
                new GridSummary(24, 60),
                new ArtifactPresence(true, true, true, true),
                true,
                true),
            new RunListEntry(
                "run_b",
                "template.b",
                "Bravo Template",
                "2.0",
                "telemetry",
                new DateTimeOffset(2025, 1, 2, 2, 0, 0, TimeSpan.Zero),
                2,
                new RunTelemetrySummaryDto(false, null, 2, null),
                null,
                "Two warnings",
                new[] { new RunWarningInfo("W1", "Warning", "warning", null) },
                new GridSummary(12, 30),
                new ArtifactPresence(true, true, false, false),
                false,
                true),
            new RunListEntry(
                "run_c",
                "template.c",
                "Charlie Template",
                "1.2",
                "simulation",
                new DateTimeOffset(2025, 1, 3, 3, 0, 0, TimeSpan.Zero),
                0,
                new RunTelemetrySummaryDto(false, null, 0, null),
                null,
                null,
                Array.Empty<RunWarningInfo>(),
                new GridSummary(48, 15),
                new ArtifactPresence(false, false, false, false),
                false,
                false)
        };
    }

    private static RunCreateResponseDto CreateDetail(RunListEntry run)
    {
        return new RunCreateResponseDto(
            false,
            new RunMetadataDto(
                run.RunId,
                run.TemplateId,
                run.TemplateTitle,
                run.TemplateVersion,
                run.Source,
                "hash",
                true,
                new SchemaMetadataDto("schema", "1", "hash"),
                new StorageDescriptorDto("manifest.json", "model.csv", "series"),
                new RunRngOptionsDto("xorshift", 42)),
            null,
            run.WarningCount > 0
                ? new[]
                {
                    new StateWarningDto("W1", "Warning", null, null)
                }
                : null,
            run.CanReplay,
            run.Telemetry);
    }

    private IRenderedComponent<ArtifactList> RenderArtifactList()
    {
        return RenderComponent<ArtifactList>();
    }

    private sealed class StubRunDiscoveryService : IRunDiscoveryService
    {
        private IReadOnlyList<RunListEntry> runs = Array.Empty<RunListEntry>();

        public void SetRuns(IReadOnlyList<RunListEntry> items) => runs = items;

        public Task<RunDiscoveryResult> LoadRunsAsync(CancellationToken cancellationToken = default)
        {
            var result = RunDiscoveryResult.CreateSuccess(runs, Array.Empty<RunDiagnostic>(), runs.Count);
            return Task.FromResult(result);
        }
    }

    private sealed class StubFlowTimeApiClient : IFlowTimeApiClient
    {
        private readonly Dictionary<string, RunCreateResponseDto> runDetails = new(StringComparer.OrdinalIgnoreCase);

        public string? BaseAddress => "http://localhost";

        public void SetRunDetail(string runId, RunCreateResponseDto detail) => runDetails[runId] = detail;

        public Task<ApiCallResult<RunCreateResponseDto>> GetRunAsync(string runId, CancellationToken ct = default)
        {
            if (runDetails.TryGetValue(runId, out var value))
            {
                return Task.FromResult(ApiCallResult<RunCreateResponseDto>.Ok(value, 200));
            }

            return Task.FromResult(ApiCallResult<RunCreateResponseDto>.Fail(404, "not found"));
        }

        public Task<ApiCallResult<RunSummaryResponseDto>> GetRunSummariesAsync(int page = 1, int pageSize = 50, CancellationToken ct = default)
            => Task.FromResult(ApiCallResult<RunSummaryResponseDto>.Fail(404, "not implemented"));

        public Task<ApiCallResult<TelemetryCaptureResponseDto>> GenerateTelemetryCaptureAsync(TelemetryCaptureRequestDto request, CancellationToken ct = default)
            => Task.FromResult(ApiCallResult<TelemetryCaptureResponseDto>.Fail(400, "not implemented"));

        public Task<ApiCallResult<RunCreateResponseDto>> CreateRunAsync(RunCreateRequestDto request, CancellationToken ct = default)
            => Task.FromResult(ApiCallResult<RunCreateResponseDto>.Fail(400, "not implemented"));

        public Task<ApiCallResult<Stream>> GetRunSeriesAsync(string runId, string seriesId, CancellationToken ct = default)
            => Task.FromResult(ApiCallResult<Stream>.Fail(404, "not implemented"));

        public Task<ApiCallResult<SeriesIndex>> GetRunIndexAsync(string runId, CancellationToken ct = default)
            => Task.FromResult(ApiCallResult<SeriesIndex>.Fail(404, "not implemented"));

        public Task<ApiCallResult<TimeTravelStateSnapshotDto>> GetRunStateAsync(string runId, int binIndex, CancellationToken ct = default)
            => Task.FromResult(ApiCallResult<TimeTravelStateSnapshotDto>.Fail(404, "not implemented"));

        public Task<ApiCallResult<TimeTravelStateWindowDto>> GetRunStateWindowAsync(string runId, int startBin, int endBin, CancellationToken ct = default)
            => Task.FromResult(ApiCallResult<TimeTravelStateWindowDto>.Fail(404, "not implemented"));

        public Task<ApiCallResult<TimeTravelMetricsResponseDto>> GetRunMetricsAsync(string runId, CancellationToken ct = default)
            => Task.FromResult(ApiCallResult<TimeTravelMetricsResponseDto>.Fail(404, "not implemented"));

        public Task<ApiCallResult<GraphResponseModel>> GetRunGraphAsync(string runId, GraphQueryOptions? options = null, CancellationToken ct = default)
            => Task.FromResult(ApiCallResult<GraphResponseModel>.Fail(404, "not implemented"));

        public Task<ApiCallResult<RunResponse>> RunAsync(string yaml, CancellationToken ct = default)
            => Task.FromResult(ApiCallResult<RunResponse>.Fail(400, "not implemented"));

        public Task<ApiCallResult<GraphResponse>> GraphAsync(string yaml, CancellationToken ct = default)
            => Task.FromResult(ApiCallResult<GraphResponse>.Fail(400, "not implemented"));

        public Task<ApiCallResult<HealthResponse>> HealthAsync(CancellationToken ct = default)
            => Task.FromResult(ApiCallResult<HealthResponse>.Fail(503, "not implemented"));

        public Task<ApiCallResult<HealthResponse>> LegacyHealthAsync(CancellationToken ct = default)
            => Task.FromResult(ApiCallResult<HealthResponse>.Fail(503, "not implemented"));

        public Task<ApiCallResult<object>> GetDetailedHealthAsync(CancellationToken ct = default)
            => Task.FromResult(ApiCallResult<object>.Fail(503, "not implemented"));

    }

    private sealed class StubNotificationService : INotificationService
    {
        public void Add(string message, Severity severity)
        {
        }

        public void Add(string message, Severity severity, Action<SnackbarOptions> configure)
        {
        }
    }
}
