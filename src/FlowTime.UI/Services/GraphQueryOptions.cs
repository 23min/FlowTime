using System.Collections.Generic;

namespace FlowTime.UI.Services;

public sealed class GraphQueryOptions
{
    public string Mode { get; set; } = "operational";
    public IReadOnlyCollection<string>? Kinds { get; set; }
    public IReadOnlyCollection<string>? DependencyFields { get; set; }
    public string? EdgeWeight { get; set; }
}
