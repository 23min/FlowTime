using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FlowTime.UI.Pages.TimeTravel;
using FlowTime.UI.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace FlowTime.UI.Tests.TimeTravel;

public class DashboardTests
{
    [Fact]
    public async Task MetricsClient_UsesApi_WhenEndpointAvailable()
    {
        var payload = new TimeTravelMetricsResponseDto
        {
            Window = new TimeTravelMetricsWindowDto
            {
                Start = DateTimeOffset.Parse("2025-01-01T00:00:00Z", CultureInfo.InvariantCulture),
                Timezone = "UTC"
            },
            Grid = new TimeTravelMetricsGridDto
            {
                BinMinutes = 60,
                Bins = 2
            },
            Services = new[]
            {
                new TimeTravelServiceMetricsDto
                {
                    Id = "Orders",
                    SlaPct = 0.96d,
                    BinsMet = 2,
                    BinsTotal = 2,
                    Mini = new[] { 1d, 0.9d }
                }
            }
        };

        var api = new StubFlowTimeApiClient
        {
            MetricsHandler = (_, _) => Task.FromResult(ApiCallResult<TimeTravelMetricsResponseDto>.Ok(payload, 200)),
            RunHandler = (_, _) => Task.FromResult(ApiCallResult<RunCreateResponseDto>.Ok(new RunCreateResponseDto(
                false,
                new RunMetadataDto(
                    RunId: "run-1",
                    TemplateId: "template-1",
                    TemplateTitle: "Template A",
                    TemplateVersion: null,
                    Mode: "simulation",
                    ProvenanceHash: null,
                    TelemetrySourcesResolved: true,
                    Schema: new SchemaMetadataDto("schema", "1", "hash"),
                    Storage: new StorageDescriptorDto(null, null, null),
                    Rng: null),
                Plan: null,
                Warnings: null,
                CanReplay: true,
                Telemetry: null), 200))
        };

        var data = new StubTimeTravelDataService();
        var client = new TimeTravelMetricsClient(api, data, NullLogger<TimeTravelMetricsClient>.Instance);

        var result = await client.GetMetricsAsync("run-1", CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Value);
        Assert.Equal(TimeTravelMetricsSource.Api, result.Value.Source);
        Assert.Single(result.Value.Payload.Services);
        Assert.Equal("Orders", result.Value.Payload.Services[0].Id);
        Assert.Equal("Template A", result.Value.Context.TemplateTitle);
        Assert.Equal("run-1", result.Value.Context.RunId);
        Assert.Equal(DateTimeOffset.Parse("2025-01-01T00:00:00Z", CultureInfo.InvariantCulture), result.Value.Context.WindowStart);
    }

    [Fact]
    public async Task MetricsClient_FallsBackToStateWindow_WhenEndpointMissing()
    {
        var api = new StubFlowTimeApiClient
        {
            MetricsHandler = (_, _) => Task.FromResult(ApiCallResult<TimeTravelMetricsResponseDto>.Fail(404, "not found")),
            IndexHandler = (_, _) => Task.FromResult(ApiCallResult<SeriesIndex>.Ok(new SeriesIndex
            {
                Grid = new SimGridInfo { Bins = 3, BinSize = 60, BinUnit = "minutes" },
                Series = new List<SeriesInfo>()
            }, 200))
        };

        var stateWindow = CreateStateWindow();

        var data = new StubTimeTravelDataService
        {
            StateWindowHandler = (_, _, _, _) => Task.FromResult(ApiCallResult<TimeTravelStateWindowDto>.Ok(stateWindow, 200))
        };

        var client = new TimeTravelMetricsClient(api, data, NullLogger<TimeTravelMetricsClient>.Instance);

        var result = await client.GetMetricsAsync("run-2", CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Value);
        Assert.Equal(TimeTravelMetricsSource.StateWindowFallback, result.Value.Source);
        var service = Assert.Single(result.Value.Payload.Services);
        Assert.Equal("OrderService", service.Id);
        Assert.Equal(1, service.BinsMet);
        Assert.Equal(3, service.BinsTotal);
        Assert.Equal(3, service.Mini.Count);
        Assert.Equal(1d, service.Mini[0]);
        Assert.True(service.SlaPct < 1d);
        Assert.Equal("template", result.Value.Context.TemplateId);
        Assert.Equal("run-2", result.Value.Context.RunId);
    }

    [Fact]
    public async Task MetricsClient_ReturnsFailure_WhenFallbackUnavailable()
    {
        var api = new StubFlowTimeApiClient
        {
            MetricsHandler = (_, _) => Task.FromResult(ApiCallResult<TimeTravelMetricsResponseDto>.Fail(500, "error")),
            IndexHandler = (_, _) => Task.FromResult(ApiCallResult<SeriesIndex>.Fail(404, "missing index"))
        };

        var client = new TimeTravelMetricsClient(api, new StubTimeTravelDataService(), NullLogger<TimeTravelMetricsClient>.Instance);

        var result = await client.GetMetricsAsync("run-3", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(404, result.StatusCode);
        Assert.Equal("missing index", result.Error);
    }

    [Fact]
    public void DetermineStatus_MapsThresholds()
    {
        var passing = Dashboard.DetermineStatus(new TimeTravelServiceMetricsDto { Id = "svc", SlaPct = 0.97d, BinsTotal = 3 });
        var warning = Dashboard.DetermineStatus(new TimeTravelServiceMetricsDto { Id = "svc", SlaPct = 0.92d, BinsTotal = 3 });
        var breach = Dashboard.DetermineStatus(new TimeTravelServiceMetricsDto { Id = "svc", SlaPct = 0.75d, BinsTotal = 3 });
        var none = Dashboard.DetermineStatus(new TimeTravelServiceMetricsDto { Id = "svc", SlaPct = 0d, BinsTotal = 0 });

        Assert.Equal(Dashboard.SlaStatus.Passing, passing.Status);
        Assert.Equal(Dashboard.SlaStatus.Warning, warning.Status);
        Assert.Equal(Dashboard.SlaStatus.Breach, breach.Status);
        Assert.Equal(Dashboard.SlaStatus.NoData, none.Status);
    }

    [Fact]
    public void BuildTile_ComputesAccessibleSummary()
    {
        var dashboard = new Dashboard();
        var tile = dashboard.BuildTile(new TimeTravelServiceMetricsDto
        {
            Id = "Billing",
            SlaPct = 0.955d,
            BinsMet = 5,
            BinsTotal = 6,
            Mini = new[] { 1d, 0.8d, 0.6d }
        });

        Assert.NotNull(tile);
        Assert.Equal("Billing", tile!.Id);
        Assert.Equal("95.5%", tile.SlaText);
        Assert.Contains("Billing", tile.AriaLabel);
        Assert.Equal(3, tile.Mini.Count);
        Assert.Equal("Billing mini bar showing SLA trend over recent bins.", tile.SparklineLabel);
    }

    [Fact]
    public void GetTileAccentStyle_UsesPaletteVariables()
    {
        var passing = Dashboard.GetTileAccentStyle(Dashboard.SlaStatus.Passing);
        var warning = Dashboard.GetTileAccentStyle(Dashboard.SlaStatus.Warning);
        var breach = Dashboard.GetTileAccentStyle(Dashboard.SlaStatus.Breach);
        var none = Dashboard.GetTileAccentStyle(Dashboard.SlaStatus.NoData);

        Assert.Contains("var(--mud-palette-success)", passing);
        Assert.Contains("var(--mud-palette-warning)", warning);
        Assert.Contains("var(--mud-palette-error)", breach);
        Assert.Contains("--tile-accent-color", none);
    }

    private static TimeTravelStateWindowDto CreateStateWindow()
    {
        var arrivals = new double?[] { 100, 120, 80 };
        var served = new double?[] { 100, 100, 70 };

        return new TimeTravelStateWindowDto
        {
            Metadata = new TimeTravelStateMetadataDto
            {
                RunId = "run-2",
                TemplateId = "template",
                Mode = "simulation",
                TelemetrySourcesResolved = true,
                Schema = new TimeTravelSchemaMetadataDto { Id = "1", Version = "1", Hash = "hash" },
                Storage = new TimeTravelStorageDescriptorDto()
            },
            Window = new TimeTravelWindowSliceDto { StartBin = 0, EndBin = 2, BinCount = 3 },
            TimestampsUtc = new[] { DateTimeOffset.Parse("2025-01-01T00:00:00Z", CultureInfo.InvariantCulture) },
            Nodes = new[]
            {
                new TimeTravelNodeSeriesDto
                {
                    Id = "OrderService",
                    Kind = "service",
                    Series = new Dictionary<string, double?[]>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["arrivals"] = arrivals,
                        ["served"] = served
                    }
                }
            }
        };
    }

    private sealed class StubFlowTimeApiClient : IFlowTimeApiClient
    {
        public string? BaseAddress => null;

        public Func<string, CancellationToken, Task<ApiCallResult<TimeTravelMetricsResponseDto>>> MetricsHandler { get; set; } =
            (_, _) => Task.FromResult(ApiCallResult<TimeTravelMetricsResponseDto>.Fail(404, "not implemented"));

        public Func<string, CancellationToken, Task<ApiCallResult<SeriesIndex>>> IndexHandler { get; set; } =
            (_, _) => Task.FromResult(ApiCallResult<SeriesIndex>.Fail(404, "not implemented"));

        public Func<string, int, int, CancellationToken, Task<ApiCallResult<TimeTravelStateWindowDto>>> StateWindowHandler { get; set; } =
            (_, _, _, _) => Task.FromResult(ApiCallResult<TimeTravelStateWindowDto>.Fail(404, "not implemented"));

        public Func<string, CancellationToken, Task<ApiCallResult<RunCreateResponseDto>>> RunHandler { get; set; } =
            (_, _) => Task.FromResult(ApiCallResult<RunCreateResponseDto>.Fail(404, "not implemented"));

        public Task<ApiCallResult<TimeTravelMetricsResponseDto>> GetRunMetricsAsync(string runId, CancellationToken ct = default)
            => MetricsHandler(runId, ct);

        public Task<ApiCallResult<SeriesIndex>> GetRunIndexAsync(string runId, CancellationToken ct = default)
            => IndexHandler(runId, ct);

        public Task<ApiCallResult<TimeTravelStateWindowDto>> GetRunStateWindowAsync(string runId, int startBin, int endBin, CancellationToken ct = default)
            => StateWindowHandler(runId, startBin, endBin, ct);

        public Task<ApiCallResult<RunCreateResponseDto>> GetRunAsync(string runId, CancellationToken ct = default)
            => RunHandler(runId, ct);

        public Task<ApiCallResult<HealthResponse>> HealthAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ApiCallResult<HealthResponse>> LegacyHealthAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ApiCallResult<object>> GetDetailedHealthAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ApiCallResult<RunResponse>> RunAsync(string yaml, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ApiCallResult<GraphResponse>> GraphAsync(string yaml, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ApiCallResult<RunSummaryResponseDto>> GetRunSummariesAsync(int page = 1, int pageSize = 50, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ApiCallResult<RunCreateResponseDto>> CreateRunAsync(RunCreateRequestDto request, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ApiCallResult<TelemetryCaptureResponseDto>> GenerateTelemetryCaptureAsync(TelemetryCaptureRequestDto request, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ApiCallResult<TimeTravelStateSnapshotDto>> GetRunStateAsync(string runId, int binIndex, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ApiCallResult<Stream>> GetRunSeriesAsync(string runId, string seriesId, CancellationToken ct = default) => throw new NotImplementedException();
    }

    private sealed class StubTimeTravelDataService : ITimeTravelDataService
    {
        public Func<string, int, int, CancellationToken, Task<ApiCallResult<TimeTravelStateWindowDto>>> StateWindowHandler { get; set; } =
            (_, _, _, _) => Task.FromResult(ApiCallResult<TimeTravelStateWindowDto>.Fail(404, "not implemented"));

        public Task<ApiCallResult<TimeTravelStateWindowDto>> GetStateWindowAsync(string runId, int startBin, int endBin, CancellationToken ct = default)
            => StateWindowHandler(runId, startBin, endBin, ct);

        public Task<ApiCallResult<TimeTravelStateSnapshotDto>> GetStateAsync(string runId, int binIndex, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<ApiCallResult<SeriesIndex>> GetSeriesIndexAsync(string runId, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<ApiCallResult<Stream>> GetSeriesAsync(string runId, string seriesId, CancellationToken ct = default)
            => throw new NotImplementedException();
    }
}
