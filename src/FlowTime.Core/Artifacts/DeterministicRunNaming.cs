using System.IO;
using System.Text;

namespace FlowTime.Core.Artifacts;

public static class DeterministicRunNaming
{
    public static string BuildRunId(string templateId, string inputHash)
    {
        var safeTemplate = SanitizeTemplateId(templateId);
        var hashValue = inputHash.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)
            ? inputHash[7..]
            : inputHash;
        return $"run_{safeTemplate}_{hashValue}";
    }

    private static string SanitizeTemplateId(string templateId)
    {
        if (string.IsNullOrWhiteSpace(templateId))
        {
            return "template";
        }

        var invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(templateId.Length);
        foreach (var ch in templateId.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch) || ch == '-' || ch == '_')
            {
                builder.Append(ch);
            }
            else if (char.IsWhiteSpace(ch) || Array.IndexOf(invalid, ch) >= 0)
            {
                builder.Append('-');
            }
            else
            {
                builder.Append(ch);
            }
        }

        var sanitized = builder.ToString().Trim('-');
        while (sanitized.Contains("--", StringComparison.Ordinal))
        {
            sanitized = sanitized.Replace("--", "-");
        }

        return string.IsNullOrWhiteSpace(sanitized) ? "template" : sanitized;
    }
}
