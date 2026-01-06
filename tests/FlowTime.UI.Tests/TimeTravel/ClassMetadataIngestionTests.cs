using FlowTime.UI.Services;
using Xunit;

namespace FlowTime.UI.Tests.TimeTravel;

public sealed class ClassMetadataIngestionTests
{
    [Fact]
    public void RunMetadata_WithClasses_ExposesCoverage()
    {
        var dto = new RunSummaryDto(
            RunId: "run123",
            TemplateId: "order-system",
            TemplateTitle: "Order System",
            TemplateNarrative: null,
            TemplateVersion: "1.0.0",
            Mode: "telemetry",
            CreatedUtc: DateTimeOffset.UtcNow,
            WarningCount: 0,
            Telemetry: null,
            Rng: null,
            InputHash: "sha256:test",
            Classes: new[] { "Order", "Refund" },
            ClassCoverage: "full");

        Assert.Equal("full", dto.ClassCoverage);
        Assert.Equal(new[] { "Order", "Refund" }, dto.Classes);
    }

    [Fact]
    public void RunMetadata_NoClasses_DefaultsToMissingCoverage()
    {
        var dto = new RunSummaryDto(
            RunId: "run123",
            TemplateId: "order-system",
            TemplateTitle: "Order System",
            TemplateNarrative: null,
            TemplateVersion: "1.0.0",
            Mode: "telemetry",
            CreatedUtc: DateTimeOffset.UtcNow,
            WarningCount: 0,
            Telemetry: null,
            Rng: null,
            InputHash: null,
            Classes: null,
            ClassCoverage: null);

        Assert.Null(dto.Classes);
        Assert.Null(dto.ClassCoverage);
    }
}
