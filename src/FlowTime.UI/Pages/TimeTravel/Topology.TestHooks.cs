using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FlowTime.UI.Components.Topology;
using FlowTime.UI.Services;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace FlowTime.UI.Pages.TimeTravel;

/// <summary>
/// Test accessors for the Topology page to simplify verification of private sparkline helpers.
/// </summary>
public partial class Topology
{
    internal void TestSetTopologyGraph(TopologyGraph graph)
    {
        topologyGraph = graph;
    }

    internal void TestSetWindowData(TimeTravelStateWindowDto data)
    {
        windowData = data;
    }

    internal void TestSetWindowWarnings(IReadOnlyList<TimeTravelStateWarningDto> warnings)
    {
        windowWarnings = FilterWindowWarnings(warnings);
        UpdateNodeWarningMap();
    }

    internal IReadOnlyList<TimeTravelStateWarningDto> TestGetInspectorWarnings(string nodeId)
    {
        return GetInspectorWarnings(nodeId);
    }

    internal void TestBuildNodeSparklines()
    {
        BuildNodeSparklines();
    }

    internal void TestBuildNodeSparklines(int? anchorBin)
    {
        BuildNodeSparklines(anchorBin);
    }

    internal IReadOnlyDictionary<string, NodeSparklineData> TestGetNodeSparklines()
    {
        return nodeSparklines;
    }

    internal IReadOnlyCollection<string> TestGetNodesMissingSparkline()
    {
        return nodesMissingSparkline;
    }

    internal void TestOnNodeFocused(string? nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            inspectorPinned = false;
            inspectorOpen = false;
            inspectorNodeId = null;
        }
        else
        {
            inspectorNodeId = nodeId;
            inspectorOpen = true;
            inspectorPinned = true;
        }
    }

    internal bool TestIsInspectorOpen() => inspectorOpen;

    internal string? TestGetInspectorNodeId() => inspectorNodeId;

    internal void TestSetNodeSparklines(IReadOnlyDictionary<string, NodeSparklineData> sparklines)
    {
        nodeSparklines = sparklines;
    }

    internal void TestSetActiveMetrics(IReadOnlyDictionary<string, NodeBinMetrics> metrics)
    {
        activeMetrics = metrics;
    }

    internal IReadOnlyList<InspectorMetricBlock> TestBuildInspectorMetrics(string nodeId)
    {
        return BuildInspectorMetricBlocks(nodeId);
    }

    internal BinDebugDump? TestBuildBinDump(string nodeId)
    {
        return BuildInspectorBinDump(nodeId);
    }

    internal string? TestResolveNodeRoleLabel(string nodeId)
    {
        return ResolveNodeRoleLabel(FindTopologyNode(nodeId));
    }

    internal IReadOnlyList<InspectorDependencyViewModel> TestBuildInspectorDependencies(string nodeId)
    {
        return BuildInspectorDependencies(nodeId);
    }

    internal (bool IncludeInspectorDetails, bool IncludeClassContributions, string? InspectorNodeId) TestCaptureBinDataFlags()
    {
        var context = CaptureBinDataContext();
        return (context.IncludeInspectorDetails, context.IncludeClassContributions, context.InspectorNodeId);
    }

    internal void TestCloseInspector()
    {
        CloseInspector();
    }

    internal void TestScheduleRunStateSave() => ScheduleRunStateSave();

    internal Task TestAwaitPendingRunStateSaveAsync() => pendingRunStateSaveTask ?? Task.CompletedTask;

    internal void TestSetLogger(ILogger<Topology> logger)
    {
        Logger = logger;
    }

    internal void TestSetOverlaySettings(TopologyOverlaySettings settings)
    {
        overlaySettings = settings.Clone();
    }

    internal void TestUpdateActiveMetrics(int bin)
    {
        UpdateActiveMetrics(bin);
    }

    internal void TestSetClassSelection(IReadOnlyList<string> classes)
    {
        selectedClasses = classes;
    }

    internal IReadOnlyCollection<string> TestGetClassFilteredNodes() => classFilteredNodes;

    internal IReadOnlyDictionary<string, NodeBinMetrics> TestGetActiveMetrics() => activeMetrics;

    internal IReadOnlyList<ClassContribution> TestGetClassContributions(string nodeId)
    {
        return nodeClassContributions.TryGetValue(nodeId, out var list)
            ? list
            : Array.Empty<ClassContribution>();
    }

    internal void TestSetJsRuntime(IJSRuntime runtime) => JS = runtime;

    internal void TestSetCurrentRunId(string runId) => currentRunId = runId;

    internal void TestMarkHasLoaded() => hasLoaded = true;

    internal void TestSetRunStateSaveDelay(TimeSpan delay) => runStateSaveDelayMs = Math.Max(0d, delay.TotalMilliseconds);

    internal void TestOverrideRunStateSaveInvoker(Func<Func<Task>, Task> invoker) => runStateSaveInvoker = invoker;

    internal string TestBuildFilteredCsv() => BuildFilteredCsvContent();

    internal void TestUpdateDispatchEntries() => UpdateDispatchEntries();

    internal IReadOnlyList<DispatchScheduleEntry> TestGetDispatchEntries() => dispatchScheduleEntries;

    internal double?[]? TestBuildUtilizationSeries(TimeTravelNodeSeriesDto node) => BuildUtilizationSeries(node);

    internal double?[]? TestBuildServiceTimeSeries(TimeTravelNodeSeriesDto node) => BuildServiceTimeSeries(node);
}
