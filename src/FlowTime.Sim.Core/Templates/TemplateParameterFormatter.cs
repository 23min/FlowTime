using System.Collections;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace FlowTime.Sim.Core.Templates;

internal static class TemplateParameterFormatter
{
    public static string FormatForSubstitution(object? value)
    {
        return value switch
        {
            null => "null",
            bool b => b.ToString().ToLowerInvariant(),
            double or float or decimal => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty,
            int or long or short or byte or sbyte => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty,
            JsonElement element => FormatJsonElement(element),
            string s => ShouldQuoteString(s) ? $"\"{s}\"" : s,
            IEnumerable enumerable when value is not string => FormatEnumerable(enumerable),
            _ => value.ToString() ?? string.Empty
        };
    }

    private static string FormatEnumerable(IEnumerable enumerable)
    {
        var items = new System.Collections.Generic.List<string>();
        foreach (var item in enumerable)
        {
            items.Add(FormatForSubstitution(item));
        }

        return $"[{string.Join(", ", items)}]";
    }

    private static bool ShouldQuoteString(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return true;
        }

        if (value.StartsWith("[") || value.StartsWith("{") ||
            (value.StartsWith("\"") && value.EndsWith("\"")))
        {
            return false;
        }

        return value.Any(char.IsWhiteSpace) || value.Contains(':') || value.Contains('#');
    }

    private static string FormatJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Array => element.GetRawText(),
            JsonValueKind.Object => element.GetRawText(),
            JsonValueKind.Null => "null",
            _ => element.GetRawText()
        };
    }
}
