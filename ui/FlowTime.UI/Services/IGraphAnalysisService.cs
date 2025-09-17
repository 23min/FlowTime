using FlowTime.UI.Services;

namespace FlowTime.UI.Services;

public interface IGraphAnalysisService
{
    Task<GraphAnalysisResult> AnalyzeGraphAsync(string yamlModel, CancellationToken cancellationToken = default);
}

public record GraphAnalysisResult
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public GraphStructureResult? Structure { get; init; }
    public List<NodeInfoView> NodeViews { get; init; } = new();
    public GraphStats Stats { get; init; } = GraphStats.Empty;
}

public record NodeInfoView(
    int Order, 
    string Id, 
    IReadOnlyList<string> Inputs, 
    int InDegree, 
    int OutDegree, 
    bool IsSource, 
    bool IsSink);

public record GraphStats(
    int Total, 
    int Sources, 
    int Sinks, 
    int MaxFanOut)
{
    public static readonly GraphStats Empty = new(0, 0, 0, 0);
}