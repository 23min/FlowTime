using FlowTime.Core.Analysis;

namespace FlowTime.Sim.Service.Services;

public interface ITemplateWarningRegistry
{
    void UpdateWarnings(string templateId, IReadOnlyList<InvariantWarning> warnings);
    bool HasWarnings { get; }
    IReadOnlyDictionary<string, IReadOnlyList<InvariantWarning>> GetWarnings();
}

public sealed class TemplateWarningRegistry : ITemplateWarningRegistry
{
    private readonly Dictionary<string, IReadOnlyList<InvariantWarning>> registry = new(StringComparer.OrdinalIgnoreCase);
    private readonly object gate = new();

    public void UpdateWarnings(string templateId, IReadOnlyList<InvariantWarning> warnings)
    {
        ArgumentException.ThrowIfNullOrEmpty(templateId);
        warnings ??= Array.Empty<InvariantWarning>();

        lock (gate)
        {
            registry[templateId] = warnings;
        }
    }

    public bool HasWarnings
    {
        get
        {
            lock (gate)
            {
                return registry.Values.Any(list => list.Count > 0);
            }
        }
    }

    public IReadOnlyDictionary<string, IReadOnlyList<InvariantWarning>> GetWarnings()
    {
        lock (gate)
        {
            return new Dictionary<string, IReadOnlyList<InvariantWarning>>(registry, StringComparer.OrdinalIgnoreCase);
        }
    }
}
