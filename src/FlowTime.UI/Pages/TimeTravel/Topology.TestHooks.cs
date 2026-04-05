using System;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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

    internal IReadOnlyList<InspectorBinMetric> TestBuildInspectorBinMetrics(string nodeId, NodeBinMetrics? metrics)
    {
        return BuildInspectorBinMetrics(nodeId, metrics);
    }

    internal Task TestDumpInspectorBinAsync(string? nodeId, bool openInNewTab)
    {
        return DumpInspectorBinAsync(new BinDumpRequest(nodeId, openInNewTab));
    }

    internal string TestBuildProvenanceTooltip(string nodeId, string metricKey)
    {
        var block = BuildInspectorMetricBlocks(nodeId)
            .FirstOrDefault(entry => string.Equals(entry.SeriesKey, metricKey, StringComparison.OrdinalIgnoreCase));
        if (block?.Provenance is null)
        {
            return string.Empty;
        }

        return BuildProvenanceTooltip(block.Provenance);
    }

    internal string TestGetAggregationIndicatorLabel()
    {
        return GetAggregationIndicatorLabel();
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

    internal void TestSetBinDataRevision(long revision)
    {
        binDataRevision = revision;
    }

    internal int TestGetSelectedBin() => selectedBin;

    internal bool TestApplyBinDataResult(long revision, int selectedBinValue)
    {
        var sparklines = new NodeSparklineComputationResult(
            new Dictionary<string, NodeSparklineData>(StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            Array.Empty<string>());

        var metrics = new ActiveMetricsComputationResult(
            new Dictionary<string, NodeBinMetrics>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, IReadOnlyList<ClassContribution>>(StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, IReadOnlyList<NodeConstraintBadgeDto>>(StringComparer.OrdinalIgnoreCase),
            selectedBinValue,
            selectedBinValue.ToString(CultureInfo.InvariantCulture),
            string.Empty,
            false);

        return TryApplyBinDataResult(new BinDataRefreshResult(revision, sparklines, metrics));
    }

    internal void TestCloseInspector()
    {
        inspectorOpen = false;
        inspectorNodeId = null;
        inspectorPinned = false;
        inspectorMetricsExpanded = false;
        inspectorTab = InspectorTab.Charts;
        inspectorDataDirty = true;
        ClearInspectorEdgeHighlights();
    }

    internal string TestGetInspectorTab() => inspectorTab.ToString();

    internal void TestSetInspectorTab(string tab)
    {
        if (Enum.TryParse<InspectorTab>(tab, out var parsed))
        {
            inspectorTab = parsed;
        }
    }

    internal IReadOnlyList<string> TestGetInspectorTabsForNode(string nodeId)
    {
        var node = FindTopologyNode(nodeId);
        var includeExpression = node is not null && IsExpressionKind(ResolveRuntimeNodeKind(node));
        return GetInspectorTabs(includeExpression)
            .Select(tab => tab.ToString())
            .ToArray();
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

    internal GraphQueryOptions TestBuildGraphQueryOptions() => BuildGraphQueryOptions();

    internal bool TestIsFocusToggleEnabled() => CanToggleFocusView;

    internal bool TestIsFocusViewEnabled() => focusViewEnabled;

    internal void TestToggleFocusView() => ToggleFocusView();

    internal void TestSetFocusViewEnabled(bool value) => SetFocusViewEnabled(value);

    internal void TestOnNodeSelected(string? nodeId)
    {
        selectedNodeId = string.IsNullOrWhiteSpace(nodeId) ? null : nodeId;
        if (selectedNodeId is null)
        {
            SetFocusViewEnabled(false);
            focusIncludeDownstream = false;
        }
    }

    internal void TestSetFullViewportSnapshot(ViewportSnapshot snapshot) => currentViewportSnapshot = snapshot;

    internal ViewportSnapshot? TestGetFullViewportSnapshot() => currentViewportSnapshot;

    internal ViewportSnapshot? TestGetFocusViewportSnapshot() => focusViewportSnapshot;

    internal ViewportSnapshot? TestGetPendingViewportSnapshot() => pendingViewportSnapshot;

    internal Task TestOnCanvasViewportChanged(ViewportSnapshot snapshot) => OnCanvasViewportChanged(snapshot);

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
