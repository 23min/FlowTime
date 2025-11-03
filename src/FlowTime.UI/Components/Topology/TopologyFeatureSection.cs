using System;

namespace FlowTime.UI.Components.Topology;

public enum TopologyFeatureSection
{
    DependencyEdges,
    Overlays,
    EdgeRouting,
    Topology,
    CanvasZoom,
    FocusThresholds,
    InspectorOverview
}

public readonly record struct SectionExpansionChangedArgs(TopologyFeatureSection Section, bool IsExpanded);
