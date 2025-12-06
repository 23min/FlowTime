using FlowTime.Contracts.Services;
using FlowTime.Core.Analysis;
using FlowTime.Core.Execution;
using FlowTime.Core.Models;
using FlowTime.Core.Routing;

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
        var evaluation = RouterAwareGraphEvaluator.Evaluate(modelDefinition, graph, grid);
        var context = evaluation.Context;

        return InvariantAnalyzer.Analyze(modelDefinition, context);
    }
}
