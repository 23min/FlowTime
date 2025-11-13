using FlowTime.Contracts.Services;
using FlowTime.Core.Analysis;
using FlowTime.Core.Execution;
using FlowTime.Core.Models;

namespace FlowTime.Sim.Core.Analysis;

public static class TemplateInvariantAnalyzer
{
    public static InvariantAnalysisResult Analyze(string modelYaml)
    {
        if (string.IsNullOrWhiteSpace(modelYaml))
        {
            return new InvariantAnalysisResult(Array.Empty<InvariantWarning>());
        }

        var dto = ModelService.ParseYaml(modelYaml);
        var modelDefinition = ModelService.ConvertToModelDefinition(dto);
        var (grid, graph) = ModelParser.ParseModel(modelDefinition);
        var evaluated = graph.Evaluate(grid);
        var context = evaluated.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.ToArray());

        return InvariantAnalyzer.Analyze(modelDefinition, context);
    }
}
