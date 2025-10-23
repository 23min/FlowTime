using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FlowTime.UI.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace FlowTime.UI.Tests;

public class TimeTravelDataServiceTests
{
    [Fact]
    public async Task GetStateAsync_ReturnsFailure_WhenRunIdMissing()
    {
        var client = new StubFlowTimeApiClient();
        var service = new TimeTravelDataService(client, NullLogger<TimeTravelDataService>.Instance);

        var result = await service.GetStateAsync(string.Empty, 0, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal("runId is required.", result.Error);
    }

    [Fact]
    public async Task GetStateWindowAsync_ReturnsFailure_WhenEndBinBeforeStart()
    {
        var client = new StubFlowTimeApiClient();
        var service = new TimeTravelDataService(client, NullLogger<TimeTravelDataService>.Instance);

        var result = await service.GetStateWindowAsync("run", 5, 4, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal("endBin must be greater than or equal to startBin.", result.Error);
    }

    [Fact]
    public async Task GetStateWindowAsync_ReturnsPayload_OnSuccess()
    {
        var dto = new TimeTravelStateWindowDto
        {
            Metadata = new TimeTravelStateMetadataDto
            {
                RunId = "run",
                TemplateId = "template",
                Mode = "simulation",
                TelemetrySourcesResolved = true,
                Schema = new TimeTravelSchemaMetadataDto { Id = "time-travel/v1", Version = "1", Hash = "sha256:abc" },
                Storage = new TimeTravelStorageDescriptorDto { ModelPath = "runs/run/model.yaml" }
            },
            Window = new TimeTravelWindowSliceDto { StartBin = 0, EndBin = 1, BinCount = 2 },
            TimestampsUtc = new[]
            {
                DateTimeOffset.Parse("2025-01-01T00:00:00Z"),
                DateTimeOffset.Parse("2025-01-01T00:05:00Z")
            },
            Nodes = new[]
            {
                new TimeTravelNodeSeriesDto
                {
                    Id = "OrderSvc",
                    Kind = "service",
                    Series = new Dictionary<string, double?[]>
                    {
                        ["lat"] = new double?[] { 0.5, 0.6 }
                    },
                    Telemetry = new TimeTravelNodeTelemetryDto()
                }
            }
        };

        var client = new StubFlowTimeApiClient
        {
            OnGetRunStateWindowAsync = (_, _, _, _) => Task.FromResult(ApiCallResult<TimeTravelStateWindowDto>.Ok(dto, 200))
        };

        var service = new TimeTravelDataService(client, NullLogger<TimeTravelDataService>.Instance);

        var result = await service.GetStateWindowAsync("run", 0, 1, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(2, result.Value?.Window.BinCount);
        Assert.Equal(0.6, result.Value?.Nodes.Single().Series["lat"][1]);
    }

    [Fact]
    public async Task GetSeriesAsync_ReturnsFailure_WhenSeriesIdMissing()
    {
        var client = new StubFlowTimeApiClient();
        var service = new TimeTravelDataService(client, NullLogger<TimeTravelDataService>.Instance);

        var result = await service.GetSeriesAsync("run", string.Empty, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal("seriesId is required.", result.Error);
    }

    [Fact]
    public async Task GetSeriesAsync_LogsFailure_WhenApiFails()
    {
        var client = new StubFlowTimeApiClient
        {
            OnGetRunSeriesAsync = (_, _, _) => Task.FromResult(ApiCallResult<Stream>.Fail(404, "not found"))
        };
        var service = new TimeTravelDataService(client, NullLogger<TimeTravelDataService>.Instance);

        var result = await service.GetSeriesAsync("run", "series", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(404, result.StatusCode);
        Assert.Equal("not found", result.Error);
    }

    [Fact]
    public async Task GetSeriesIndexAsync_ReturnsPayload()
    {
        var index = new SeriesIndex
        {
            SchemaVersion = 1,
            Grid = new SimGridInfo { Bins = 12, BinSize = 5, BinUnit = "minutes" },
            Series = new List<SeriesInfo>
            {
                new SeriesInfo { Id = "series1" }
            }
        };

        var client = new StubFlowTimeApiClient
        {
            OnGetRunIndexAsync = (_, _) => Task.FromResult(ApiCallResult<SeriesIndex>.Ok(index, 200))
        };

        var service = new TimeTravelDataService(client, NullLogger<TimeTravelDataService>.Instance);

        var result = await service.GetSeriesIndexAsync("run", CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(1, result.Value?.Series.Count ?? 0);
    }

    private sealed class StubFlowTimeApiClient : IFlowTimeApiClient
    {
        public string? BaseAddress => null;

        public Func<string, int, CancellationToken, Task<ApiCallResult<TimeTravelStateSnapshotDto>>> OnGetRunStateAsync { get; set; } =
            (_, _, _) => throw new NotImplementedException();

        public Func<string, int, int, CancellationToken, Task<ApiCallResult<TimeTravelStateWindowDto>>> OnGetRunStateWindowAsync { get; set; } =
            (_, _, _, _) => throw new NotImplementedException();

        public Func<string, CancellationToken, Task<ApiCallResult<SeriesIndex>>> OnGetRunIndexAsync { get; set; } =
            (_, _) => throw new NotImplementedException();

        public Func<string, string, CancellationToken, Task<ApiCallResult<Stream>>> OnGetRunSeriesAsync { get; set; } =
            (_, _, _) => throw new NotImplementedException();

        public Task<ApiCallResult<HealthResponse>> HealthAsync(CancellationToken ct = default) => throw new NotImplementedException();

        public Task<ApiCallResult<HealthResponse>> LegacyHealthAsync(CancellationToken ct = default) => throw new NotImplementedException();

        public Task<ApiCallResult<object>> GetDetailedHealthAsync(CancellationToken ct = default) => throw new NotImplementedException();

        public Task<ApiCallResult<RunResponse>> RunAsync(string yaml, CancellationToken ct = default) => throw new NotImplementedException();

        public Task<ApiCallResult<GraphResponse>> GraphAsync(string yaml, CancellationToken ct = default) => throw new NotImplementedException();

        public Task<ApiCallResult<RunSummaryResponseDto>> GetRunSummariesAsync(int page = 1, int pageSize = 50, CancellationToken ct = default) => throw new NotImplementedException();

        public Task<ApiCallResult<RunCreateResponseDto>> CreateRunAsync(RunCreateRequestDto request, CancellationToken ct = default) => throw new NotImplementedException();

        public Task<ApiCallResult<RunCreateResponseDto>> GetRunAsync(string runId, CancellationToken ct = default) => throw new NotImplementedException();

        public Task<ApiCallResult<TelemetryCaptureResponseDto>> GenerateTelemetryCaptureAsync(TelemetryCaptureRequestDto request, CancellationToken ct = default) => throw new NotImplementedException();

        public Task<ApiCallResult<TimeTravelStateSnapshotDto>> GetRunStateAsync(string runId, int binIndex, CancellationToken ct = default)
            => OnGetRunStateAsync(runId, binIndex, ct);

        public Task<ApiCallResult<TimeTravelStateWindowDto>> GetRunStateWindowAsync(string runId, int startBin, int endBin, CancellationToken ct = default)
            => OnGetRunStateWindowAsync(runId, startBin, endBin, ct);

        public Task<ApiCallResult<SeriesIndex>> GetRunIndexAsync(string runId, CancellationToken ct = default)
            => OnGetRunIndexAsync(runId, ct);

        public Task<ApiCallResult<Stream>> GetRunSeriesAsync(string runId, string seriesId, CancellationToken ct = default)
            => OnGetRunSeriesAsync(runId, seriesId, ct);
    }
}
