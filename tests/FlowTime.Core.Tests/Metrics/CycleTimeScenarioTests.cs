using FlowTime.Core.Metrics;
using Xunit;

namespace FlowTime.Core.Tests.Metrics;

/// <summary>
/// Scenario tests for CycleTimeComputer modelling each node type
/// in the FlowTime topology. Each scenario maps to a distinct code path
/// in StateQueryService.ComputeFlowLatency (lines 2660-2697).
///
/// These tests define the correct expected behavior and guard against
/// regressions when integrating CycleTimeComputer into the runtime path.
/// </summary>
public class CycleTimeScenarioTests
{
    private const double BinMs = 60_000; // 1-minute bin

    // ── Scenario 1: Pure queue node (buffer) ──
    // Has queueDepth/served, no processingTimeMsSum/servedCount.
    // Maps to: isQueue=true, isService=false → baseValue = queueLatencyMs

    [Fact]
    public void PureQueue_CycleTimeEqualsQueueTime()
    {
        var qt = CycleTimeComputer.CalculateQueueTime(queueDepth: 10, served: 5, binMs: BinMs);
        var st = CycleTimeComputer.CalculateServiceTime(processingTimeMsSum: null, servedCount: null);
        var ct = CycleTimeComputer.CalculateCycleTime(qt, st);

        Assert.Equal(120_000.0, qt);  // (10/5)*60000
        Assert.Null(st);              // no service data
        Assert.Equal(120_000.0, ct);  // degrades to queue time only
    }

    [Fact]
    public void PureQueue_FlowEfficiency_NullWhenNoServiceTime()
    {
        var qt = CycleTimeComputer.CalculateQueueTime(queueDepth: 10, served: 5, binMs: BinMs);
        var ct = CycleTimeComputer.CalculateCycleTime(qt, serviceTimeMs: null);
        var fe = CycleTimeComputer.CalculateFlowEfficiency(serviceTimeMs: null, cycleTimeMs: ct);

        Assert.Null(fe); // can't compute efficiency without service time
    }

    // ── Scenario 2: Pure service node (processor) ──
    // Has processingTimeMsSum/servedCount, no queue data.
    // Maps to: isQueue=false, isService=true → baseValue = serviceTimeMs
    // THIS IS THE REGRESSION CASE: CalculateCycleTime must NOT return null here.

    [Fact]
    public void PureService_CycleTimeEqualsServiceTime()
    {
        var qt = (double?)null; // no queue for this node type
        var st = CycleTimeComputer.CalculateServiceTime(processingTimeMsSum: 500, servedCount: 10);
        var ct = CycleTimeComputer.CalculateCycleTime(qt, st);

        Assert.Null(qt);             // no queue data
        Assert.Equal(50.0, st);      // 500/10 = 50ms avg
        Assert.Equal(50.0, ct);      // cycle time = service time only
    }

    [Fact]
    public void PureService_FlowEfficiency_IsOne()
    {
        var st = CycleTimeComputer.CalculateServiceTime(processingTimeMsSum: 500, servedCount: 10);
        var ct = CycleTimeComputer.CalculateCycleTime(queueTimeMs: null, st);
        var fe = CycleTimeComputer.CalculateFlowEfficiency(st, ct);

        Assert.Equal(1.0, fe); // 100% processing, no queue time
    }

    // ── Scenario 3: ServiceWithBuffer (queue + service) ──
    // Has both queue and service data.
    // Maps to: isQueue=true, isService=true, isServiceWithBuffer=true
    //   → baseValue = queueLatencyMs + serviceTimeMs (or fallback)

    [Fact]
    public void ServiceWithBuffer_CycleTimeSumsBothComponents()
    {
        var qt = CycleTimeComputer.CalculateQueueTime(queueDepth: 8, served: 4, binMs: BinMs);
        var st = CycleTimeComputer.CalculateServiceTime(processingTimeMsSum: 200, servedCount: 4);
        var ct = CycleTimeComputer.CalculateCycleTime(qt, st);

        Assert.Equal(120_000.0, qt);  // (8/4)*60000
        Assert.Equal(50.0, st);       // 200/4
        Assert.Equal(120_050.0, ct);  // sum
    }

    [Fact]
    public void ServiceWithBuffer_FlowEfficiency_ShowsProcessingFraction()
    {
        var qt = CycleTimeComputer.CalculateQueueTime(queueDepth: 8, served: 4, binMs: BinMs);
        var st = CycleTimeComputer.CalculateServiceTime(processingTimeMsSum: 200, servedCount: 4);
        var ct = CycleTimeComputer.CalculateCycleTime(qt, st);
        var fe = CycleTimeComputer.CalculateFlowEfficiency(st, ct);

        Assert.NotNull(fe);
        // 50 / 120050 ≈ 0.000416 — almost all time is in queue
        Assert.Equal(50.0 / 120_050.0, fe!.Value, 10);
    }

    [Fact]
    public void ServiceWithBuffer_ServiceTimeUnavailable_FallsBackToQueueTime()
    {
        // ServiceWithBuffer where processingTimeMsSum is null (rare but possible)
        var qt = CycleTimeComputer.CalculateQueueTime(queueDepth: 8, served: 4, binMs: BinMs);
        var st = CycleTimeComputer.CalculateServiceTime(processingTimeMsSum: null, servedCount: null);
        var ct = CycleTimeComputer.CalculateCycleTime(qt, st);

        Assert.Equal(120_000.0, ct); // falls back to queue time
        Assert.Null(st);
    }

    // ── Scenario 4: Synthetic model (no telemetry) ──
    // Has queue data from simulation but no processingTimeMsSum.
    // Cycle time degrades gracefully to queue time only.

    [Fact]
    public void Synthetic_NoProcTimeMsSum_DegradesToQueueTime()
    {
        var qt = CycleTimeComputer.CalculateQueueTime(queueDepth: 15, served: 3, binMs: BinMs);
        var st = CycleTimeComputer.CalculateServiceTime(processingTimeMsSum: null, servedCount: null);
        var ct = CycleTimeComputer.CalculateCycleTime(qt, st);

        Assert.Equal(300_000.0, qt);  // (15/3)*60000
        Assert.Null(st);
        Assert.Equal(300_000.0, ct);  // queue time only
    }

    // ── Scenario 5: Zero-served bin ──
    // A bin where nothing was served (idle or no traffic).
    // Both queue time and service time are undefined.

    [Fact]
    public void ZeroServedBin_AllNull()
    {
        var qt = CycleTimeComputer.CalculateQueueTime(queueDepth: 5, served: 0, binMs: BinMs);
        var st = CycleTimeComputer.CalculateServiceTime(processingTimeMsSum: 0, servedCount: 0);
        var ct = CycleTimeComputer.CalculateCycleTime(qt, st);
        var fe = CycleTimeComputer.CalculateFlowEfficiency(st, ct);

        Assert.Null(qt);  // served=0 → null
        Assert.Null(st);  // servedCount=0 → null
        Assert.Null(ct);  // both null → null
        Assert.Null(fe);  // can't compute
    }

    // ── Scenario 6: Per-class mixed availability ──
    // Different classes at the same node have different data availability.

    [Fact]
    public void PerClass_MixedAvailability_EachClassIndependent()
    {
        // Class "web": has both queue and service data (serviceWithBuffer-like)
        var qtWeb = CycleTimeComputer.CalculateQueueTime(queueDepth: 20, served: 10, binMs: BinMs);
        var stWeb = CycleTimeComputer.CalculateServiceTime(processingTimeMsSum: 1000, servedCount: 10);
        var ctWeb = CycleTimeComputer.CalculateCycleTime(qtWeb, stWeb);
        var feWeb = CycleTimeComputer.CalculateFlowEfficiency(stWeb, ctWeb);

        // Class "batch": queue data only (synthetic / no telemetry for this class)
        var qtBatch = CycleTimeComputer.CalculateQueueTime(queueDepth: 50, served: 5, binMs: BinMs);
        var stBatch = CycleTimeComputer.CalculateServiceTime(processingTimeMsSum: null, servedCount: null);
        var ctBatch = CycleTimeComputer.CalculateCycleTime(qtBatch, stBatch);
        var feBatch = CycleTimeComputer.CalculateFlowEfficiency(stBatch, ctBatch);

        // Class "api": service data only (pure service, no queue)
        var qtApi = (double?)null;
        var stApi = CycleTimeComputer.CalculateServiceTime(processingTimeMsSum: 300, servedCount: 6);
        var ctApi = CycleTimeComputer.CalculateCycleTime(qtApi, stApi);
        var feApi = CycleTimeComputer.CalculateFlowEfficiency(stApi, ctApi);

        // web: full decomposition
        Assert.Equal(120_000.0, qtWeb);
        Assert.Equal(100.0, stWeb);
        Assert.Equal(120_100.0, ctWeb);
        Assert.NotNull(feWeb);

        // batch: queue only
        Assert.Equal(600_000.0, qtBatch);
        Assert.Null(stBatch);
        Assert.Equal(600_000.0, ctBatch);
        Assert.Null(feBatch); // no service time → no efficiency

        // api: service only (regression case per-class)
        Assert.Null(qtApi);
        Assert.Equal(50.0, stApi);
        Assert.Equal(50.0, ctApi);    // must NOT be null
        Assert.Equal(1.0, feApi);     // 100% processing
    }

    // ── Concert test: multi-bin series with varying conditions ──
    // Simulates a 4-bin window where conditions change per bin.

    [Fact]
    public void MultiBin_VaryingConditions_CorrectPerBin()
    {
        double[] queueDepth = [10, 0, 5, 20];
        double[] served =     [ 5, 0, 5, 10];
        double?[] procTimeSum = [500, null, 250, 1000];
        double?[] servedCount = [ 10, null,   5,   10];

        var results = new (double? qt, double? st, double? ct, double? fe)[4];
        for (int i = 0; i < 4; i++)
        {
            var qt = CycleTimeComputer.CalculateQueueTime(queueDepth[i], served[i], BinMs);
            var st = CycleTimeComputer.CalculateServiceTime(procTimeSum[i], servedCount[i]);
            var ct = CycleTimeComputer.CalculateCycleTime(qt, st);
            var fe = CycleTimeComputer.CalculateFlowEfficiency(st, ct);
            results[i] = (qt, st, ct, fe);
        }

        // Bin 0: normal — both components
        Assert.Equal(120_000.0, results[0].qt);
        Assert.Equal(50.0, results[0].st);
        Assert.Equal(120_050.0, results[0].ct);
        Assert.NotNull(results[0].fe);

        // Bin 1: zero served, null proc — all null
        Assert.Null(results[1].qt);
        Assert.Null(results[1].st);
        Assert.Null(results[1].ct);
        Assert.Null(results[1].fe);

        // Bin 2: normal — both components
        Assert.Equal(60_000.0, results[2].qt);
        Assert.Equal(50.0, results[2].st);
        Assert.Equal(60_050.0, results[2].ct);
        Assert.NotNull(results[2].fe);

        // Bin 3: high queue — both components
        Assert.Equal(120_000.0, results[3].qt);
        Assert.Equal(100.0, results[3].st);
        Assert.Equal(120_100.0, results[3].ct);
        Assert.NotNull(results[3].fe);
    }
}
