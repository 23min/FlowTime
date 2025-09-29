using FlowTime.Sim.Core;

namespace FlowTime.Sim.Service;

public interface ITemplateRepository
{
    Task<IReadOnlyList<TemplateDef>> GetAllTemplatesAsync();
    Task<TemplateDef?> GetTemplateAsync(string id);
    Task<string> GenerateModelAsync(string templateId, Dictionary<string, object> parameters);
}