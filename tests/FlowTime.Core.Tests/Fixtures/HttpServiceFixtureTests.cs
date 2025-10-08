using System;
using System.IO;
using Xunit;

namespace FlowTime.Core.Tests.Fixtures;

public class HttpServiceFixtureTests
{
    private const int ExpectedBins = 96;

    [Fact]
    public void ModelYamlExists()
    {
        var path = GetFixturePath("model.yaml");
        Assert.True(File.Exists(path), $"Fixture model missing: {path}");
    }

    [Fact]
    public void CsvFilesExistWithCorrectLength()
    {
        var files = new[]
        {
            "HttpService_arrivals.csv",
            "HttpService_served.csv",
            "HttpService_errors.csv"
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
        return Path.Combine(root, "fixtures", "http-service", relative);
    }
}
