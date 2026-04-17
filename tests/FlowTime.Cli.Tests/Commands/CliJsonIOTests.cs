using System.Text.Json;
using FlowTime.Cli.Commands;

namespace FlowTime.Cli.Tests.Commands;

/// <summary>
/// Unit tests for <see cref="CliJsonIO"/>: stdin/stdout/file JSON I/O with web
/// serialization defaults matching the API.
/// </summary>
public class CliJsonIOTests
{
    private sealed record SampleDto(string Name, int Count);

    // ── ReadJson ────────────────────────────────────────────────────

    [Fact]
    public void ReadJson_FromFile_DeserializesCorrectly()
    {
        var tempPath = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempPath, """{"name":"Alice","count":42}""");
            var reader = new StringReader("");
            var dto = CliJsonIO.ReadJson<SampleDto>(tempPath, reader);
            Assert.Equal("Alice", dto.Name);
            Assert.Equal(42, dto.Count);
        }
        finally { File.Delete(tempPath); }
    }

    [Fact]
    public void ReadJson_FromStdin_WhenPathIsNull()
    {
        var reader = new StringReader("""{"name":"Bob","count":7}""");
        var dto = CliJsonIO.ReadJson<SampleDto>(specPath: null, reader);
        Assert.Equal("Bob", dto.Name);
        Assert.Equal(7, dto.Count);
    }

    [Fact]
    public void ReadJson_FromStdin_WhenPathIsDash()
    {
        // Convention: "-" means stdin (standard UNIX)
        var reader = new StringReader("""{"name":"Carol","count":3}""");
        var dto = CliJsonIO.ReadJson<SampleDto>(specPath: "-", reader);
        Assert.Equal("Carol", dto.Name);
        Assert.Equal(3, dto.Count);
    }

    [Fact]
    public void ReadJson_InvalidJson_ThrowsJsonException()
    {
        var reader = new StringReader("not valid json {{{");
        Assert.Throws<JsonException>(
            () => CliJsonIO.ReadJson<SampleDto>(specPath: null, reader));
    }

    [Fact]
    public void ReadJson_MissingFile_ThrowsFileNotFoundException()
    {
        var reader = new StringReader("");
        Assert.Throws<FileNotFoundException>(
            () => CliJsonIO.ReadJson<SampleDto>(
                specPath: "/nonexistent/path/spec.json", reader));
    }

    [Fact]
    public void ReadJson_NullLiteral_ThrowsJsonException()
    {
        // "null" on the wire deserializes to a null T. ReadJson rejects this with a
        // JsonException (as opposed to returning null which the caller can't handle).
        var reader = new StringReader("null");
        Assert.Throws<JsonException>(
            () => CliJsonIO.ReadJson<SampleDto>(specPath: null, reader));
    }

    [Fact]
    public void ReadJson_UsesCamelCaseMatching()
    {
        // Web defaults accept camelCase on the wire → PascalCase on properties.
        var reader = new StringReader("""{"Name":"PascalOK","count":1}""");
        var dto = CliJsonIO.ReadJson<SampleDto>(specPath: null, reader);
        Assert.Equal("PascalOK", dto.Name);
        Assert.Equal(1, dto.Count);
    }

    // ── ReadYaml ────────────────────────────────────────────────────

    [Fact]
    public void ReadYaml_FromFile_ReturnsContent()
    {
        var tempPath = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempPath, "grid:\n  bins: 4\n");
            var reader = new StringReader("");
            var yaml = CliJsonIO.ReadYaml(tempPath, reader);
            Assert.Equal("grid:\n  bins: 4\n", yaml);
        }
        finally { File.Delete(tempPath); }
    }

    [Fact]
    public void ReadYaml_FromStdin_WhenPathIsNull()
    {
        var reader = new StringReader("grid:\n  bins: 2\n");
        var yaml = CliJsonIO.ReadYaml(modelPath: null, reader);
        Assert.Equal("grid:\n  bins: 2\n", yaml);
    }

    [Fact]
    public void ReadYaml_FromStdin_WhenPathIsDash()
    {
        var reader = new StringReader("nodes: []\n");
        var yaml = CliJsonIO.ReadYaml(modelPath: "-", reader);
        Assert.Equal("nodes: []\n", yaml);
    }

    [Fact]
    public void ReadYaml_MissingFile_ThrowsFileNotFoundException()
    {
        var reader = new StringReader("");
        Assert.Throws<FileNotFoundException>(
            () => CliJsonIO.ReadYaml("/nonexistent/model.yaml", reader));
    }

    // ── WriteJson ───────────────────────────────────────────────────

    [Fact]
    public void WriteJson_ToStdout_WhenPathIsNull()
    {
        var writer = new StringWriter();
        CliJsonIO.WriteJson(outputPath: null, new SampleDto("Eve", 5), writer);
        var output = writer.ToString();
        Assert.Contains("\"name\"", output);  // camelCase on wire
        Assert.Contains("Eve", output);
        Assert.Contains("5", output);
    }

    [Fact]
    public void WriteJson_ToStdout_WhenPathIsDash()
    {
        var writer = new StringWriter();
        CliJsonIO.WriteJson(outputPath: "-", new SampleDto("Fran", 9), writer);
        Assert.Contains("Fran", writer.ToString());
    }

    [Fact]
    public void WriteJson_ToFile_CreatesFileWithJson()
    {
        var tempPath = Path.GetTempFileName();
        try
        {
            var writer = new StringWriter();
            CliJsonIO.WriteJson(tempPath, new SampleDto("Gina", 11), writer);

            // Stdout should be empty when writing to file.
            Assert.Empty(writer.ToString());

            var fileContent = File.ReadAllText(tempPath);
            Assert.Contains("\"name\"", fileContent);
            Assert.Contains("Gina", fileContent);
        }
        finally { File.Delete(tempPath); }
    }

    [Fact]
    public void WriteJson_UsesCamelCase()
    {
        var writer = new StringWriter();
        CliJsonIO.WriteJson(outputPath: null, new SampleDto("X", 1), writer);
        var output = writer.ToString();
        Assert.Contains("\"name\"", output);
        Assert.Contains("\"count\"", output);
        Assert.DoesNotContain("\"Name\"", output);
        Assert.DoesNotContain("\"Count\"", output);
    }
}
