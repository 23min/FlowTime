using System.Globalization;
using FlowTime.Sim.Core.Hashing;
using Xunit;

namespace FlowTime.Sim.Tests.Hashing;

public class RunHashCalculatorTests
{
    private static readonly IReadOnlyDictionary<string, object?> BaseParameters = new Dictionary<string, object?>
    {
        ["bins"] = 12,
        ["binSize"] = 60,
        ["title"] = "Warehouse Picker"
    };

    private static readonly IReadOnlyDictionary<string, string> BaseBindings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["arrivals"] = "OrderService_arrivals.csv",
        ["served"] = "OrderService_served.csv"
    };

    [Fact]
    public void ComputeHash_SameInputs_ProducesStableHash()
    {
        var inputA = CreateInput();
        var inputB = CreateInput();

        var hashA = RunHashCalculator.ComputeHash(inputA);
        var hashB = RunHashCalculator.ComputeHash(inputB);

        Assert.Equal(hashA, hashB);
        Assert.StartsWith("sha256:", hashA);
    }

    [Fact]
    public void ComputeHash_ParameterOrdering_DoesNotAffectHash()
    {
        var shuffledParams = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["title"] = "Warehouse Picker",
            ["binSize"] = 60,
            ["bins"] = 12
        };

        var inputA = CreateInput(BaseParameters);
        var inputB = CreateInput(shuffledParams);

        var hashA = RunHashCalculator.ComputeHash(inputA);
        var hashB = RunHashCalculator.ComputeHash(inputB);

        Assert.Equal(hashA, hashB);
    }

    [Fact]
    public void ComputeHash_RngChange_ChangesHash()
    {
        var inputA = CreateInput(rngSeed: 42);
        var inputB = CreateInput(rngSeed: 99);

        var hashA = RunHashCalculator.ComputeHash(inputA);
        var hashB = RunHashCalculator.ComputeHash(inputB);

        Assert.NotEqual(hashA, hashB);
    }

    [Fact]
    public void ComputeHash_TelemetryBindingsAffectHash()
    {
        var inputA = CreateInput(bindings: BaseBindings);
        var modifiedBindings = new Dictionary<string, string>(BaseBindings, StringComparer.OrdinalIgnoreCase)
        {
            ["served"] = "OrderService_served_v2.csv"
        };
        var inputB = CreateInput(bindings: modifiedBindings);

        var hashA = RunHashCalculator.ComputeHash(inputA);
        var hashB = RunHashCalculator.ComputeHash(inputB);

        Assert.NotEqual(hashA, hashB);
    }

    [Fact]
    public void ComputeHash_IsCultureInvariant()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("fr-FR");
            var frenchHash = RunHashCalculator.ComputeHash(CreateInput());

            CultureInfo.CurrentCulture = new CultureInfo("en-US");
            var englishHash = RunHashCalculator.ComputeHash(CreateInput());

            Assert.Equal(englishHash, frenchHash);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    private static RunHashInput CreateInput(
        IReadOnlyDictionary<string, object?>? parameters = null,
        IReadOnlyDictionary<string, string>? bindings = null,
        int rngSeed = 123)
    {
        return new RunHashInput(
            TemplateId: "warehouse-picker",
            TemplateVersion: "1.0.0",
            Mode: "telemetry",
            Parameters: parameters ?? BaseParameters,
            TelemetryBindings: bindings ?? BaseBindings,
            RngKind: "pcg32",
            RngSeed: rngSeed);
    }
}
