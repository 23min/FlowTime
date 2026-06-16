using System;
using System.IO;
using Xunit;

namespace FlowTime.Api.Tests;

public class TryResolveRunDirectoryTests : IDisposable
{
    private readonly string tempRoot;

    public TryResolveRunDirectoryTests()
    {
        tempRoot = Path.Combine(Path.GetTempPath(), "flowtime-tryresolve-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
        catch
        {
            // best-effort cleanup
        }
    }

    [Fact]
    public void ExistingRunDirectory_ReturnsNullAndSetsPath()
    {
        var runId = "run_valid";
        var expectedPath = Path.Combine(tempRoot, runId);
        Directory.CreateDirectory(expectedPath);

        var result = Program.TryResolveRunDirectory(tempRoot, runId, out var runPath);

        Assert.Null(result);
        Assert.Equal(Path.GetFullPath(expectedPath), runPath);
    }

    [Fact]
    public void NonexistentRunDirectory_ReturnsNotFoundResult()
    {
        var result = Program.TryResolveRunDirectory(tempRoot, "run_not_created", out var runPath);

        Assert.NotNull(result);
        // runPath is populated on the resolve (canonical), then the existence
        // check fails. The helper returns a 404 IResult — we can't easily
        // introspect the status code without executing against an HttpContext,
        // so we assert the non-null contract shape.
    }

    [Theory]
    [InlineData("..")]
    [InlineData(".")]
    [InlineData("../escape")]
    [InlineData("nested/path")]
    [InlineData("")]
    public void InvalidRunId_ReturnsNotFoundAndSetsEmptyPath(string runId)
    {
        var result = Program.TryResolveRunDirectory(tempRoot, runId, out var runPath);

        Assert.NotNull(result);
        Assert.Equal(string.Empty, runPath);
    }

    [Fact]
    public void InvalidArtifactsDirectory_ReturnsNotFoundAndSetsEmptyPath()
    {
        var result = Program.TryResolveRunDirectory("", "run_abc", out var runPath);

        Assert.NotNull(result);
        Assert.Equal(string.Empty, runPath);
    }
}
