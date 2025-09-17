using FlowTime.UI.Services;

namespace FlowTime.UI.Services;

public interface ISimulationResultsService
{
    Task<SimulationResult> RunSimulationAsync(string yamlModel, CancellationToken cancellationToken = default);
    ChartResult BuildChart(List<string> csvLines);
}

public record SimulationResult
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public List<string> CsvLines { get; init; } = new();
    public ChartResult Chart { get; init; } = new(new List<double>(), new List<string>());
}

public record ChartResult(List<double> Series, List<string> Labels);