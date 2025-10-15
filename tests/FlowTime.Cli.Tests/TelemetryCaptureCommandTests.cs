using FlowTime.Cli.Commands;
using FlowTime.Cli.Configuration;
using FlowTime.Tests.Support;

namespace FlowTime.Cli.Tests;

public sealed class TelemetryCaptureCommandTests
{
    [Fact]
    public async Task ExecuteAsync_DryRun_PrintsPlannedFiles()
    {
        using var temp = new TempDirectory();
        using var env = new EnvVarScope("FLOWTIME_DATA_DIR", temp.Path);

        var runDir = TelemetryRunFactory.CreateRunArtifacts(temp.Path, "run_cli_capture", includeTopology: true);

        var exitCode = await TelemetryCaptureCommand.ExecuteAsync(new[]
        {
            "capture",
            "--run-dir", runDir,
            "--dry-run"
        });

        Assert.Equal(0, exitCode);
        var telemetryRoot = Path.Combine(temp.Path, "telemetry");
        Assert.False(Directory.Exists(telemetryRoot));
    }

    [Fact]
    public async Task ExecuteAsync_MissingRunDir_ReturnsError()
    {
        using var temp = new TempDirectory();
        using var env = new EnvVarScope("FLOWTIME_DATA_DIR", temp.Path);

        var runDir = Path.Combine(temp.Path, "does-not-exist");

        var exitCode = await TelemetryCaptureCommand.ExecuteAsync(new[]
        {
            "capture",
            "--run-dir", runDir
        });

        Assert.Equal(1, exitCode);
        Assert.False(Directory.Exists(Path.Combine(temp.Path, "telemetry")));
    }

    [Fact]
    public async Task ExecuteAsync_MissingRunDirArgument_ReturnsUsageError()
    {
        var exitCode = await TelemetryCaptureCommand.ExecuteAsync(new[] { "capture" });
        Assert.Equal(2, exitCode);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownOption_ReturnsUsageError()
    {
        var exitCode = await TelemetryCaptureCommand.ExecuteAsync(new[] { "capture", "--unknown" });
        Assert.Equal(2, exitCode);
    }

    private sealed class EnvVarScope : IDisposable
    {
        private readonly string name;
        private readonly string? previous;

        public EnvVarScope(string name, string value)
        {
            this.name = name;
            previous = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable(name, previous);
        }
    }
}
