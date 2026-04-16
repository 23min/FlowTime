using System.Text.Json;
using FlowTime.Cli.Commands;

namespace FlowTime.Cli.Tests.Commands;

public class ValidateCommandTests
{
    private const string ValidYaml = """
        schemaVersion: 1
        grid:
          bins: 4
          binSize: 15
          binUnit: minutes
        nodes:
          - id: arrivals
            kind: const
            values: [10, 10, 10, 10]
        """;

    private static (StringWriter stdout, StringWriter stderr) NewWriters() =>
        (new StringWriter(), new StringWriter());

    // ── Help ───────────────────────────────────────────────────────

    [Fact]
    public async Task Help_PrintsUsageAndReturnsZero()
    {
        var (stdout, stderr) = NewWriters();
        var code = await ValidateCommand.ExecuteAsync(
            ["--help"], new StringReader(""), stdout, stderr);
        Assert.Equal(0, code);
        Assert.Contains("Usage: flowtime validate", stdout.ToString());
        Assert.Empty(stderr.ToString());
    }

    // ── Argument errors (exit 2) ───────────────────────────────────

    [Fact]
    public async Task UnknownFlag_ReturnsTwoAndWritesStderr()
    {
        var (stdout, stderr) = NewWriters();
        var code = await ValidateCommand.ExecuteAsync(
            ["--wat"], new StringReader(""), stdout, stderr);
        Assert.Equal(2, code);
        Assert.Contains("--wat", stderr.ToString());
        Assert.Empty(stdout.ToString());
    }

    [Fact]
    public async Task InvalidTier_ReturnsTwo()
    {
        var (stdout, stderr) = NewWriters();
        var code = await ValidateCommand.ExecuteAsync(
            ["--tier", "bogus"], new StringReader(ValidYaml), stdout, stderr);
        Assert.Equal(2, code);
        Assert.Contains("bogus", stderr.ToString());
    }

    [Fact]
    public async Task MissingTierValue_ReturnsTwo()
    {
        var (stdout, stderr) = NewWriters();
        var code = await ValidateCommand.ExecuteAsync(
            ["--tier"], new StringReader(ValidYaml), stdout, stderr);
        Assert.Equal(2, code);
        Assert.Contains("--tier", stderr.ToString());
    }

    [Fact]
    public async Task MissingModelFile_ReturnsTwo()
    {
        var (stdout, stderr) = NewWriters();
        var code = await ValidateCommand.ExecuteAsync(
            ["--model", "/nonexistent/model.yaml"],
            new StringReader(""), stdout, stderr);
        Assert.Equal(2, code);
        Assert.Contains("not found", stderr.ToString());
    }

    // ── Valid YAML → exit 0, JSON on stdout ────────────────────────

    [Fact]
    public async Task ValidYaml_FromStdin_ReturnsZeroAndWritesJson()
    {
        var (stdout, stderr) = NewWriters();
        var code = await ValidateCommand.ExecuteAsync(
            Array.Empty<string>(), new StringReader(ValidYaml), stdout, stderr);
        Assert.Equal(0, code);
        Assert.Empty(stderr.ToString());

        using var doc = JsonDocument.Parse(stdout.ToString());
        Assert.True(doc.RootElement.GetProperty("isValid").GetBoolean());
        Assert.Equal("analyse", doc.RootElement.GetProperty("tier").GetString());
    }

    [Fact]
    public async Task ValidYaml_Tier_Schema_ReturnsSchemaTierInJson()
    {
        var (stdout, stderr) = NewWriters();
        var code = await ValidateCommand.ExecuteAsync(
            ["--tier", "schema"], new StringReader(ValidYaml), stdout, stderr);
        Assert.Equal(0, code);

        using var doc = JsonDocument.Parse(stdout.ToString());
        Assert.Equal("schema", doc.RootElement.GetProperty("tier").GetString());
        Assert.True(doc.RootElement.GetProperty("isValid").GetBoolean());
    }

    [Fact]
    public async Task ValidYaml_PositionalPath_ReadsFile()
    {
        var tempPath = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempPath, ValidYaml);

            var (stdout, stderr) = NewWriters();
            var code = await ValidateCommand.ExecuteAsync(
                [tempPath], new StringReader(""), stdout, stderr);
            Assert.Equal(0, code);
            using var doc = JsonDocument.Parse(stdout.ToString());
            Assert.True(doc.RootElement.GetProperty("isValid").GetBoolean());
        }
        finally { File.Delete(tempPath); }
    }

    [Fact]
    public async Task ValidYaml_DashMeansStdin()
    {
        var (stdout, stderr) = NewWriters();
        var code = await ValidateCommand.ExecuteAsync(
            ["-"], new StringReader(ValidYaml), stdout, stderr);
        Assert.Equal(0, code);
        using var doc = JsonDocument.Parse(stdout.ToString());
        Assert.True(doc.RootElement.GetProperty("isValid").GetBoolean());
    }

    // ── Invalid YAML → exit 1, JSON still on stdout ───────────────

    [Fact]
    public async Task InvalidYaml_ReturnsOneAndStillWritesJson()
    {
        var (stdout, stderr) = NewWriters();
        var code = await ValidateCommand.ExecuteAsync(
            Array.Empty<string>(),
            new StringReader("this is not a model"),
            stdout, stderr);
        Assert.Equal(1, code);
        Assert.Empty(stderr.ToString());

        using var doc = JsonDocument.Parse(stdout.ToString());
        Assert.False(doc.RootElement.GetProperty("isValid").GetBoolean());
        Assert.True(doc.RootElement.GetProperty("errors").GetArrayLength() > 0);
    }

    [Fact]
    public async Task EmptyYaml_ReturnsOneWithErrors()
    {
        var (stdout, stderr) = NewWriters();
        var code = await ValidateCommand.ExecuteAsync(
            Array.Empty<string>(), new StringReader(""), stdout, stderr);
        Assert.Equal(1, code);
        using var doc = JsonDocument.Parse(stdout.ToString());
        Assert.False(doc.RootElement.GetProperty("isValid").GetBoolean());
    }

    // ── Output to file ────────────────────────────────────────────

    [Fact]
    public async Task ValidYaml_OutputToFile_WritesFileNotStdout()
    {
        var tempPath = Path.GetTempFileName();
        try
        {
            var (stdout, stderr) = NewWriters();
            var code = await ValidateCommand.ExecuteAsync(
                ["-o", tempPath], new StringReader(ValidYaml), stdout, stderr);
            Assert.Equal(0, code);

            // Stdout should be empty; file should have the JSON.
            Assert.Empty(stdout.ToString());
            var fileJson = File.ReadAllText(tempPath);
            using var doc = JsonDocument.Parse(fileJson);
            Assert.True(doc.RootElement.GetProperty("isValid").GetBoolean());
        }
        finally { File.Delete(tempPath); }
    }

    // ── Response shape byte-compatibility with API ────────────────

    [Fact]
    public async Task ResponseShape_MatchesApiFields()
    {
        var (stdout, stderr) = NewWriters();
        var code = await ValidateCommand.ExecuteAsync(
            Array.Empty<string>(), new StringReader(ValidYaml), stdout, stderr);
        Assert.Equal(0, code);

        using var doc = JsonDocument.Parse(stdout.ToString());
        var root = doc.RootElement;
        // Every field the /v1/validate response carries must be present:
        Assert.Equal(JsonValueKind.String, root.GetProperty("tier").ValueKind);
        Assert.Equal(JsonValueKind.True, root.GetProperty("isValid").ValueKind);
        Assert.Equal(JsonValueKind.Array, root.GetProperty("errors").ValueKind);
        Assert.Equal(JsonValueKind.Array, root.GetProperty("warnings").ValueKind);
    }
}
