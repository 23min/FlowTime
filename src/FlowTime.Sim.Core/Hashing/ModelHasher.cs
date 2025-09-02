using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("FlowTime.Sim.Tests")]

// Slice 1 (SIM-M2): canonical model hashing & normalization.
// Goal: stable hash independent of YAML key ordering, whitespace, comments.
// Approach: parse YAML -> canonical object graph -> emit minimal JSON with
// sorted object keys and invariant number formatting, then SHA256 over UTF-8 bytes.

namespace FlowTime.Sim.Core;

public static class ModelHasher
{
    private static readonly IDeserializer deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public static string ComputeModelHash(string rawYaml)
    {
        if (string.IsNullOrWhiteSpace(rawYaml)) throw new ArgumentException("YAML empty", nameof(rawYaml));
        // Strip full-line comments & trailing spaces early to reduce parse noise; inline comments kept (removed later by canonicalization implicit structure parsing).
        var stripped = PreStripComments(rawYaml);
        object? yamlObj;
        try
        {
            yamlObj = deserializer.Deserialize<object?>(stripped);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to parse YAML for hashing (ensure syntactically valid).", ex);
        }
        if (yamlObj is null)
        {
            throw new InvalidOperationException("Parsed YAML was null (empty after normalization)");
        }
        var sb = new StringBuilder(512);
        using (var w = new System.IO.StringWriter(sb))
        {
            WriteCanonicalJson(yamlObj, w);
        }
        var canonical = sb.ToString();
        var bytes = Encoding.UTF8.GetBytes(canonical);
        var sha = SHA256.HashData(bytes);
        return "sha256:" + Convert.ToHexString(sha).ToLowerInvariant();
    }

    // Exposed for tests: produces the canonical JSON string used for hashing.
    internal static string ComputeCanonicalRepresentation(string rawYaml)
    {
        if (string.IsNullOrWhiteSpace(rawYaml)) throw new ArgumentException("YAML empty", nameof(rawYaml));
        var stripped = PreStripComments(rawYaml);
    object? yamlObj = deserializer.Deserialize<object?>(stripped);
    if (yamlObj is null) throw new InvalidOperationException("Parsed YAML was null (empty after normalization)");
        var sb = new StringBuilder(512);
        using var w = new System.IO.StringWriter(sb);
        WriteCanonicalJson(yamlObj, w);
        return sb.ToString();
    }

    private static string PreStripComments(string raw)
    {
        var sb = new StringBuilder(raw.Length);
        using var sr = new StringReader(raw.Replace("\r\n", "\n").Replace('\r', '\n'));
        string? line;
        while ((line = sr.ReadLine()) is not null)
        {
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("#")) continue; // whole-line comment
            // Remove inline comments preceded by a space (naive but avoids cutting URLs like http://)
            var idx = line.IndexOf(" #", StringComparison.Ordinal);
            if (idx >= 0)
            {
                line = line.Substring(0, idx);
            }
            // Collapse trailing whitespace
            line = line.TrimEnd();
            if (line.Length == 0)
            {
                // Collapse multiple blank lines to single
                if (sb.Length > 0 && sb[^1] == '\n') continue;
            }
            sb.Append(line).Append('\n');
        }
        return sb.ToString();
    }

    private static void WriteCanonicalJson(object? node, TextWriter w)
    {
        switch (node)
        {
            case null:
                w.Write("null");
                break;
            case string s:
                WriteEscapedString(s, w);
                break;
            case bool b:
                w.Write(b ? "true" : "false");
                break;
            case sbyte or byte or short or ushort or int or uint or long or ulong:
                w.Write(Convert.ToString(node, System.Globalization.CultureInfo.InvariantCulture));
                break;
            case float f:
                w.Write(f.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
                break;
            case double d:
                w.Write(d.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
                break;
            case decimal m:
                w.Write(m.ToString(System.Globalization.CultureInfo.InvariantCulture));
                break;
            case IDictionary<object, object?> map:
                w.Write('{');
                var first = true;
                foreach (var kvp in map.OrderBy(k => k.Key?.ToString(), StringComparer.Ordinal))
                {
                    if (!first) w.Write(',');
                    first = false;
                    WriteEscapedString(kvp.Key?.ToString() ?? string.Empty, w);
                    w.Write(':');
                    WriteCanonicalJson(kvp.Value, w);
                }
                w.Write('}');
                break;
            case IEnumerable<object?> list:
                w.Write('[');
                var i = 0;
                foreach (var item in list)
                {
                    if (i++ > 0) w.Write(',');
                    WriteCanonicalJson(item, w);
                }
                w.Write(']');
                break;
            case System.Collections.IEnumerable genericList:
                w.Write('[');
                var firstElem = true;
                foreach (var item in genericList)
                {
                    if (!firstElem) w.Write(',');
                    firstElem = false;
                    WriteCanonicalJson(item, w);
                }
                w.Write(']');
                break;
            default:
                // Fallback: ToString as string
                WriteEscapedString(node.ToString() ?? string.Empty, w);
                break;
        }
    }

    private static void WriteEscapedString(string value, TextWriter w)
    {
        w.Write('"');
        foreach (var ch in value)
        {
            switch (ch)
            {
                case '"': w.Write("\\\""); break;
                case '\\': w.Write("\\\\"); break;
                case '\n': w.Write("\\n"); break;
                case '\r': w.Write("\\r"); break;
                case '\t': w.Write("\\t"); break;
                default:
                    if (ch < 32)
                    {
                        w.Write("\\u");
                        w.Write(((int)ch).ToString("x4"));
                    }
                    else w.Write(ch);
                    break;
            }
        }
        w.Write('"');
    }
}

// Expose internals to test assembly for direct hashing assertions.