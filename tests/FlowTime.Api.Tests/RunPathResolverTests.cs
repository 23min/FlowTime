using System;
using System.IO;
using FlowTime.Contracts.Storage;
using Xunit;

namespace FlowTime.Api.Tests;

public class RunPathResolverTests : IDisposable
{
    private readonly string tempRoot;

    public RunPathResolverTests()
    {
        tempRoot = Path.Combine(Path.GetTempPath(), "flowtime-runpath-" + Guid.NewGuid().ToString("N"));
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
            // best-effort cleanup; ignore
        }
    }

    [Fact]
    public void GetSafeRunDirectory_ValidRunId_ReturnsCanonicalPath()
    {
        var runId = "run_abc_123";
        var result = RunPathResolver.GetSafeRunDirectory(tempRoot, runId);
        var expected = Path.GetFullPath(Path.Combine(tempRoot, runId));
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetSafeRunDirectory_ResultIsAlwaysAbsolute()
    {
        var relativeRoot = Path.GetRelativePath(Directory.GetCurrentDirectory(), tempRoot);
        var result = RunPathResolver.GetSafeRunDirectory(relativeRoot, "run_xyz");
        Assert.True(Path.IsPathRooted(result));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void GetSafeRunDirectory_InvalidArtifactsDirectory_Throws(string? artifactsDirectory)
    {
        Assert.Throws<ArgumentException>(() =>
            RunPathResolver.GetSafeRunDirectory(artifactsDirectory!, "run_abc"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void GetSafeRunDirectory_InvalidRunId_Throws(string? runId)
    {
        Assert.Throws<ArgumentException>(() =>
            RunPathResolver.GetSafeRunDirectory(tempRoot, runId!));
    }

    [Theory]
    [InlineData("..")]
    [InlineData(".")]
    [InlineData("../other")]
    [InlineData("..\\other")]
    [InlineData("../../etc/passwd")]
    [InlineData("/absolute/path")]
    [InlineData("\\windows\\absolute")]
    public void GetSafeRunDirectory_PathTraversalAttempt_Throws(string runId)
    {
        Assert.Throws<ArgumentException>(() =>
            RunPathResolver.GetSafeRunDirectory(tempRoot, runId));
    }

    [Theory]
    [InlineData("run/nested")]
    [InlineData("run\\nested")]
    [InlineData("run/.")]
    [InlineData("run\\..")]
    public void GetSafeRunDirectory_RunIdContainsSeparator_Throws(string runId)
    {
        Assert.Throws<ArgumentException>(() =>
            RunPathResolver.GetSafeRunDirectory(tempRoot, runId));
    }

    [Fact]
    public void GetSafeRunDirectory_RunIdIsSiblingAttempt_Throws()
    {
        // Attempt to escape by resolving to sibling directory: root/../other
        // Even if the ASP.NET route allowed it (it doesn't), canonicalization
        // must reject anything not strictly under the root.
        Assert.Throws<ArgumentException>(() =>
            RunPathResolver.GetSafeRunDirectory(tempRoot, ".." + Path.DirectorySeparatorChar + "sibling"));
    }

    [Fact]
    public void GetSafeRunDirectory_ResultRootedUnderArtifactsDirectory()
    {
        var result = RunPathResolver.GetSafeRunDirectory(tempRoot, "run_inside");
        var rootFull = Path.GetFullPath(tempRoot) + Path.DirectorySeparatorChar;
        Assert.StartsWith(rootFull, result + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }

    [Fact]
    public void GetSafeRunDirectory_SiblingPrefixDoesNotSatisfyRootCheck()
    {
        // Guard against StartsWith false positives: root "/tmp/flowtime-a" must
        // not accept candidate "/tmp/flowtime-abc". GetFullPath + trailing
        // separator check prevents this.
        var siblingRoot = tempRoot + "-sibling";
        Directory.CreateDirectory(siblingRoot);
        try
        {
            // Resolve "../<siblingRoot-name>" relative to tempRoot would land in
            // the sibling. Must be rejected.
            var siblingName = Path.GetFileName(siblingRoot);
            var escaping = ".." + Path.DirectorySeparatorChar + siblingName;
            Assert.Throws<ArgumentException>(() =>
                RunPathResolver.GetSafeRunDirectory(tempRoot, escaping));
        }
        finally
        {
            Directory.Delete(siblingRoot);
        }
    }

    [Fact]
    public void GetSafeRunDirectory_RootWithTrailingSeparator_DoesNotDuplicateSeparator()
    {
        // Covers the branch where rootFull already ends with a directory
        // separator (e.g., filesystem root "/"). Exercises the ternary
        // that skips appending.
        var rootWithTrailingSep = tempRoot + Path.DirectorySeparatorChar;
        var result = RunPathResolver.GetSafeRunDirectory(rootWithTrailingSep, "run_inside");
        var expected = Path.GetFullPath(Path.Combine(tempRoot, "run_inside"));
        Assert.Equal(expected, result);
        Assert.DoesNotContain(
            string.Concat(Path.DirectorySeparatorChar, Path.DirectorySeparatorChar),
            result);
    }

    [Fact]
    public void GetSafeRunDirectory_DoesNotRequireDirectoryToExist()
    {
        // Canonicalization is the security contract. Existence is a separate
        // caller concern (→ 404). The helper must not error on missing dirs.
        var result = RunPathResolver.GetSafeRunDirectory(tempRoot, "run_does_not_exist_yet");
        Assert.False(Directory.Exists(result));
        Assert.StartsWith(Path.GetFullPath(tempRoot), result, StringComparison.Ordinal);
    }
}
