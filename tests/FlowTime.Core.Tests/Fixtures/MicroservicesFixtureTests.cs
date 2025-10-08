using System;
using System.IO;
using Xunit;

namespace FlowTime.Core.Tests.Fixtures;

public class MicroservicesFixtureTests
{
    private const int ExpectedBins = 144;

    [Fact]
    public void ModelYamlExists()
    {
        var path = GetFixturePath("model.yaml");
        Assert.True(File.Exists(path), $"Fixture model missing: {path}");
    }

    [Fact]
    public void AllServiceCsvsExistWithCorrectLength()
    {
        var files = new[]
        {
            "API_Gateway_arrivals.csv",
            "API_Gateway_served.csv",
            "API_Gateway_errors.csv",
            "AuthService_arrivals.csv",
            "AuthService_served.csv",
            "AuthService_errors.csv",
            "OrderService_arrivals.csv",
            "OrderService_served.csv",
            "OrderService_errors.csv",
            "PaymentService_arrivals.csv",
            "PaymentService_served.csv",
            "PaymentService_errors.csv",
            "InventoryService_arrivals.csv",
            "InventoryService_served.csv",
            "InventoryService_errors.csv",
            "ShippingService_arrivals.csv",
            "ShippingService_served.csv",
            "ShippingService_errors.csv"
        };

        foreach (var file in files)
        {
            var path = GetFixturePath(file);
            Assert.True(File.Exists(path), $"Fixture file missing: {path}");

            var lines = File.ReadAllLines(path);
            Assert.True(lines.Length > 0, $"Fixture CSV empty: {path}");
            Assert.Equal(ExpectedBins, lines.Length - 1);
        }
    }

    private static string GetFixturePath(string relative)
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        return Path.Combine(root, "fixtures", "microservices", relative);
    }
}
