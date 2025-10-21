using System.Text.Json;
using FlowTime.UI.Pages.TimeTravel;
using FlowTime.UI.Services;

namespace FlowTime.UI.Tests;

public sealed class RunOrchestrationRequestBuilderTests
{
    [Fact]
    public void BuildSimulationRequest_ProducesValidDto()
    {
        var model = new RunOrchestrationFormModel
        {
            TemplateId = "order-system",
            Mode = OrchestrationMode.Simulation,
            ParameterText = """
            {
              "speed": 2,
              "notes": "fast"
            }
            """
        };

        var success = RunOrchestrationRequestBuilder.TryBuild(model, dryRun: false, out var request, out var error);

        Assert.True(success);
        Assert.Null(error);
        Assert.NotNull(request);
        Assert.Equal("order-system", request.TemplateId);
        Assert.Equal("simulation", request.Mode, ignoreCase: true);
        Assert.NotNull(request.Parameters);
        Assert.Equal(2, request.Parameters!["speed"].GetInt32());
        Assert.Equal("fast", request.Parameters!["notes"].GetString());
        Assert.NotNull(request.Options);
        Assert.False(request.Options!.DryRun);
    }

    [Fact]
    public void TelemetryWithoutCaptureDirectory_IsRejected()
    {
        var model = new RunOrchestrationFormModel
        {
            TemplateId = "order-system",
            Mode = OrchestrationMode.Telemetry,
            TelemetryBindingsText = "foo=bar"
        };

        var success = RunOrchestrationRequestBuilder.TryBuild(model, dryRun: true, out var request, out var error);

        Assert.False(success);
        Assert.Null(request);
        Assert.NotNull(error);
        Assert.Contains("capture", error, StringComparison.OrdinalIgnoreCase);
    }
}
