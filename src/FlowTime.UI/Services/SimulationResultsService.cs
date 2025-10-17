
using System.Globalization;

namespace FlowTime.UI.Services;

public class SimulationResultsService : ISimulationResultsService
{
    private readonly IRunClient runClient;

    public SimulationResultsService(IRunClient runClient)
    {
        this.runClient = runClient;
    }

    public async Task<SimulationResult> RunSimulationAsync(string yamlModel, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await runClient.RunAsync(yamlModel, cancellationToken);
            if (!result.Success || result.Value is null)
            {
                return new SimulationResult
                {
                    Success = false,
                    Error = result.Error ?? "Unknown error"
                };
            }
            var runResult = result.Value;
            var chart = BuildChartFromRunResult(runResult);

            return new SimulationResult
            {
                Success = true,
                CsvLines = new List<string>(), // Not available from GraphRunResult
                Chart = chart
            };
        }
        catch (Exception ex)
        {
            return new SimulationResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    public ChartResult BuildChart(List<string> csvLines)
    {
        if (csvLines.Count <= 1) // No data or header only
        {
            return new ChartResult(new List<double>(), new List<string>());
        }

        var chartSeries = new List<double>();
        var chartLabels = new List<string>();

        // Skip header row and parse flow times
        foreach (var line in csvLines.Skip(1))
        {
            var parts = line.Split(',');
            if (parts.Length >= 2)
            {
                if (double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var flowTime))
                {
                    chartSeries.Add(flowTime);
                    chartLabels.Add($"#{chartLabels.Count + 1}");
                }
            }
        }

        return new ChartResult(chartSeries, chartLabels);
    }

    private ChartResult BuildChartFromRunResult(GraphRunResult runResult)
    {
        if (runResult.Series.Count == 0)
        {
            return new ChartResult(new List<double>(), new List<string>());
        }

        // Take the first series for now (could be enhanced to handle multiple series)
        var firstSeries = runResult.Series.First();
        var chartSeries = firstSeries.Value.ToList();
        var chartLabels = Enumerable.Range(0, runResult.Bins).Select(i => i.ToString()).ToList();

        return new ChartResult(chartSeries, chartLabels);
    }
}