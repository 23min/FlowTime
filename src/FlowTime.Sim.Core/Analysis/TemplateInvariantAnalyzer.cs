using FlowTime.Contracts.Services;
using FlowTime.Core.Analysis;
using FlowTime.Core.Compiler;
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
        var compiledModel = ModelCompiler.Compile(modelDefinition);
        var (grid, graph) = ModelParser.ParseModel(compiledModel);
        var evaluation = RouterAwareGraphEvaluator.Evaluate(compiledModel, graph, grid);
        var context = evaluation.Context;

        return InvariantAnalyzer.Analyze(compiledModel, context);
    }
}
