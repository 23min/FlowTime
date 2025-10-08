using System;
using System.IO;
using Xunit;

namespace FlowTime.Core.Tests.Fixtures;

public class OrderSystemFixtureTests
{
    private const int ExpectedBins = 288;

    [Fact]
    public void ModelYamlExists()
    {
        var path = GetFixturePath("model.yaml");
        Assert.True(File.Exists(path), $"Fixture model missing: {path}");
    }

    [Fact]
    public void SeriesCsvsHaveExpectedRowCount()
    {
        var files = new[]
        {
            "OrderService_arrivals.csv",
            "OrderService_served.csv",
            "OrderService_errors.csv",
            "PaymentService_arrivals.csv",
            "PaymentService_served.csv",
            "PaymentService_errors.csv"
        };

        foreach (var file in files)
        {
            var path = GetFixturePath(file);
            Assert.True(File.Exists(path), $"Fixture file missing: {path}");

            var lines = File.ReadAllLines(path);
            Assert.True(lines.Length > 0, $"Fixture CSV empty: {path}");
            Assert.Equal(ExpectedBins, lines.Length - 1); // subtract header
        }
    }

    private static string GetFixturePath(string relative)
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        return Path.Combine(root, "fixtures", "order-system", relative);
    }
}
