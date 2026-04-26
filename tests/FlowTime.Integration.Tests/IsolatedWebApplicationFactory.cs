using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FlowTime.Integration.Tests;

/// <summary>
/// Test-isolation factory for <c>FlowTime.API</c> integration tests.
/// Mirrors the pattern in <c>FlowTime.Api.Tests.TestWebApplicationFactory</c> — overrides
/// the artifact + storage paths to a per-test temp directory so the test does not depend
/// on <c>appsettings.Development.json</c>'s hardcoded devcontainer paths
/// (<c>/workspaces/flowtime-vnext/...</c>) which do not exist on CI runners.
///
/// Cleaned up automatically when the fixture is disposed.
/// </summary>
public sealed class IsolatedWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string testDataDirectory;

    public IsolatedWebApplicationFactory()
    {
        var testId = Guid.NewGuid().ToString("N")[..12];
        testDataDirectory = Path.Combine(Path.GetTempPath(), $"flowtime_integration_{testId}");
        Directory.CreateDirectory(testDataDirectory);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ArtifactsDirectory", testDataDirectory);
        builder.UseSetting("DataDirectory", testDataDirectory);
        builder.UseSetting("ArtifactRegistry:AutoAddEnabled", "false");
        builder.UseSetting("Storage:Backend", "filesystem");
        builder.UseSetting("Storage:Root", Path.Combine(testDataDirectory, "storage"));

        base.ConfigureWebHost(builder);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try
            {
                if (Directory.Exists(testDataDirectory))
                {
                    Directory.Delete(testDataDirectory, recursive: true);
                }
            }
            catch
            {
                // Best-effort cleanup; locked files shouldn't fail tests.
            }
        }

        base.Dispose(disposing);
    }
}
