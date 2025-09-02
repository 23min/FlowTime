using FlowTime.Sim.Core;

namespace FlowTime.Sim.Service;

public static class ScenarioRegistry
{
    // Minimal built-in scenarios (YAML specs) – kept small for bootstrap milestone.
    private static readonly ScenarioDef[] Scenarios =
    [
        new(
            Id: "const-quick",
            Title: "Const Arrivals (Quick 3 bins)",
            Description: "Three-bin constant arrivals demo (values 1,2,3)",
            Yaml: """schemaVersion: 1\nrng: pcg\nseed: 42\ngrid:\n  bins: 3\n  binMinutes: 60\narrivals:\n  kind: const\n  values: [1,2,3]\nroute:\n  id: COMP_A\n"""
        ),
        new(
            Id: "poisson-demo",
            Title: "Poisson Arrivals (λ=5 over 4 bins)",
            Description: "Single-rate Poisson toy example with 4 bins",
            Yaml: """schemaVersion: 1\nrng: pcg\nseed: 123\ngrid:\n  bins: 4\n  binMinutes: 30\narrivals:\n  kind: poisson\n  rate: 5\nroute:\n  id: COMP_A\n"""
        )
    ];

    public static IEnumerable<object> List() => Scenarios.Select(s => new
    {
        s.Id,
        s.Title,
        s.Description,
        // Surface minimal knobs to UI without full parse cost.
        Preview = ExtractPreview(s.Yaml)
    });

    public static ScenarioDef? Get(string id) => Scenarios.FirstOrDefault(s => string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase));

    private static object ExtractPreview(string yaml)
    {
        try
        {
            var spec = SimulationSpecLoader.LoadFromString(yaml);
            return new
            {
                bins = spec.grid?.bins,
                binMinutes = spec.grid?.binMinutes,
                arrivals = spec.arrivals?.kind,
                route = spec.route?.id
            };
        }
        catch
        {
            return new { bins = (int?)null, binMinutes = (int?)null, arrivals = (string?)null, route = (string?)null };
        }
    }
}

public sealed record ScenarioDef(string Id, string Title, string Description, string Yaml);