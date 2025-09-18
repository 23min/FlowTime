

namespace FlowTime.UI.Services;

public class GraphAnalysisService : IGraphAnalysisService
{
    private readonly IRunClient runClient;

    public GraphAnalysisService(IRunClient runClient)
    {
        this.runClient = runClient;
    }

    public async Task<GraphAnalysisResult> AnalyzeGraphAsync(string yamlModel, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await runClient.GraphAsync(yamlModel, cancellationToken);
            if (!result.Success || result.Value is null)
            {
                return new GraphAnalysisResult
                {
                    Success = false,
                    Error = result.Error ?? "Unknown error"
                };
            }
            var structure = result.Value;
            var nodeViews = BuildNodeViews(structure);
            var stats = ComputeGraphStats(nodeViews);

            return new GraphAnalysisResult
            {
                Success = true,
                Structure = structure,
                NodeViews = nodeViews,
                Stats = stats
            };
        }
        catch (Exception ex)
        {
            return new GraphAnalysisResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    private List<NodeInfoView> BuildNodeViews(GraphStructureResult structure)
    {
        var orderIndex = new Dictionary<string, int>();
        for (int i = 0; i < structure.Order.Count; i++) 
            orderIndex[structure.Order[i]] = i;

        var orderedNodes = structure.Nodes
            .Select(node => new NodeInfoView(
                Order: orderIndex.GetValueOrDefault(node.Id, int.MaxValue),
                Id: node.Id,
                Inputs: node.Inputs,
                InDegree: node.Inputs.Count,
                OutDegree: structure.Nodes.Count(n => n.Inputs.Contains(node.Id)),
                IsSource: node.Inputs.Count == 0,
                IsSink: !structure.Nodes.Any(n => n.Inputs.Contains(node.Id))
            ))
            .OrderBy(n => n.Order)
            .ThenBy(n => n.Id)
            .ToList();

        return orderedNodes;
    }

    private GraphStats ComputeGraphStats(List<NodeInfoView> nodeViews)
    {
        return new GraphStats(
            Total: nodeViews.Count,
            Sources: nodeViews.Count(n => n.IsSource),
            Sinks: nodeViews.Count(n => n.IsSink),
            MaxFanOut: nodeViews.Max(n => n.OutDegree)
        );
    }
}