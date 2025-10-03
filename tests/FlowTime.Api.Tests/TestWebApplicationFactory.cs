using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace FlowTime.Api.Tests;

/// <summary>
/// Custom WebApplicationFactory that isolates test data from production data.
/// Automatically configures each test to use a unique temporary directory.
/// </summary>
public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _testDataDirectory;

    public TestWebApplicationFactory()
    {
        // Create a unique test data directory for this test run
        // Use a path that's clearly temporary and won't pollute production data
        var testId = Guid.NewGuid().ToString("N")[..12]; // Short GUID for readability
        _testDataDirectory = Path.Combine(Path.GetTempPath(), $"flowtime_test_{testId}");
        Directory.CreateDirectory(_testDataDirectory);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Override the artifacts/data directory to use our test directory
        builder.UseSetting("ArtifactsDirectory", _testDataDirectory);
        builder.UseSetting("DataDirectory", _testDataDirectory);

        base.ConfigureWebHost(builder);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Clean up test data directory after tests complete
            try
            {
                if (Directory.Exists(_testDataDirectory))
                {
                    Directory.Delete(_testDataDirectory, recursive: true);
                }
            }
            catch
            {
                // Best effort cleanup - don't fail tests if cleanup fails
                // (files might be locked, etc.)
            }
        }

        base.Dispose(disposing);
    }
}
