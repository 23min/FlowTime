using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using FlowTime.Sim.Core.Templates.Exceptions;

namespace FlowTime.Sim.Core.Templates;

internal static class TemplateParameterValueConverter
{
    private const double IntegerTolerance = 1e-9;

    public static object? Normalize(TemplateParameter? parameter, object? value)
    {
        if (value is JsonElement element)
        {
            return NormalizeJsonElement(parameter, element);
        }

        if (value is null)
        {
            return null;
        }

        if (IsArrayParameter(parameter))
        {
            if (value is string s && TryParseStringArray(parameter, s, out var parsed))
            {
                return parsed;
            }

            if (value is IEnumerable enumerable && value is not string)
            {
                return ConvertEnumerable(parameter, enumerable);
            }
        }

        return value;
    }

    private static object? NormalizeJsonElement(TemplateParameter? parameter, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Null:
                return null;
            case JsonValueKind.True:
            case JsonValueKind.False:
                return element.GetBoolean();
            case JsonValueKind.Number:
                return element.TryGetInt64(out var integer) ? integer : element.GetDouble();
            case JsonValueKind.String:
                return element.GetString();
            case JsonValueKind.Array:
                if (!IsArrayParameter(parameter))
                {
                    return element.GetRawText();
                }

                return ConvertJsonArray(parameter, element);
            case JsonValueKind.Object:
                return element.GetRawText();
            default:
                return element.GetRawText();
        }
    }

    private static bool TryParseStringArray(TemplateParameter? parameter, string raw, out object? result)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var trimmed = raw.Trim();
        if (!trimmed.StartsWith("[", StringComparison.Ordinal) || !trimmed.EndsWith("]", StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(trimmed);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            result = ConvertJsonArray(parameter, doc.RootElement);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static object ConvertJsonArray(TemplateParameter? parameter, JsonElement arrayElement)
    {
        return ResolveArrayElementKind(parameter) switch
        {
            ArrayElementKind.Int => ConvertJsonArrayToIntArray(parameter, arrayElement),
            _ => ConvertJsonArrayToDoubleArray(parameter, arrayElement)
        };
    }

    private static object ConvertEnumerable(TemplateParameter? parameter, IEnumerable enumerable)
    {
        return ResolveArrayElementKind(parameter) switch
        {
            ArrayElementKind.Int => ConvertEnumerableToIntArray(parameter, enumerable),
            _ => ConvertEnumerableToDoubleArray(parameter, enumerable)
        };
    }

    private static double[] ConvertJsonArrayToDoubleArray(TemplateParameter? parameter, JsonElement arrayElement)
    {
        var values = new List<double>();
        foreach (var item in arrayElement.EnumerateArray())
        {
            values.Add(ConvertJsonElementToDouble(parameter, item));
        }

        return values.ToArray();
    }

    private static int[] ConvertJsonArrayToIntArray(TemplateParameter? parameter, JsonElement arrayElement)
    {
        var values = new List<int>();
        foreach (var item in arrayElement.EnumerateArray())
        {
            values.Add(ConvertJsonElementToInt(parameter, item));
        }

        return values.ToArray();
    }

    private static double[] ConvertEnumerableToDoubleArray(TemplateParameter? parameter, IEnumerable enumerable)
    {
        var values = new List<double>();
        foreach (var item in enumerable)
        {
            values.Add(ConvertToDouble(parameter, item));
        }

        return values.ToArray();
    }

    private static int[] ConvertEnumerableToIntArray(TemplateParameter? parameter, IEnumerable enumerable)
    {
        var values = new List<int>();
        foreach (var item in enumerable)
        {
            values.Add(ConvertToInt(parameter, item));
        }

        return values.ToArray();
    }

    private static double ConvertJsonElementToDouble(TemplateParameter? parameter, JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Number => element.GetDouble(),
            JsonValueKind.String => ParseDouble(parameter, element.GetString()),
            _ => throw CreateConversionException(parameter, "double")
        };
    }

    private static int ConvertJsonElementToInt(TemplateParameter? parameter, JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Number => element.TryGetInt64(out var integer)
                ? checked((int)integer)
                : ConvertDoubleToInt(parameter, element.GetDouble()),
            JsonValueKind.String => ParseInt(parameter, element.GetString()),
            _ => throw CreateConversionException(parameter, "integer")
        };
    }

    private static double ConvertToDouble(TemplateParameter? parameter, object? value)
    {
        return value switch
        {
            null => throw CreateConversionException(parameter, "double"),
            double d => d,
            float f => f,
            decimal m => (double)m,
            long l => l,
            int i => i,
            short s => s,
            byte b => b,
            JsonElement json => ConvertJsonElementToDouble(parameter, json),
            string str => ParseDouble(parameter, str),
            _ => throw CreateConversionException(parameter, "double")
        };
    }

    private static int ConvertToInt(TemplateParameter? parameter, object? value)
    {
        return value switch
        {
            null => throw CreateConversionException(parameter, "integer"),
            int i => i,
            long l => checked((int)l),
            short s => s,
            byte b => b,
            double d => ConvertDoubleToInt(parameter, d),
            float f => ConvertDoubleToInt(parameter, f),
            decimal m => ConvertDoubleToInt(parameter, (double)m),
            JsonElement json => ConvertJsonElementToInt(parameter, json),
            string str => ParseInt(parameter, str),
            _ => throw CreateConversionException(parameter, "integer")
        };
    }

    private static double ParseDouble(TemplateParameter? parameter, string? raw)
    {
        if (raw == null)
        {
            throw CreateConversionException(parameter, "double");
        }

        if (double.TryParse(raw, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        throw CreateConversionException(parameter, "double");
    }

    private static int ParseInt(TemplateParameter? parameter, string? raw)
    {
        if (raw == null)
        {
            throw CreateConversionException(parameter, "integer");
        }

        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        if (double.TryParse(raw, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var doubleValue))
        {
            return ConvertDoubleToInt(parameter, doubleValue);
        }

        throw CreateConversionException(parameter, "integer");
    }

    private static int ConvertDoubleToInt(TemplateParameter? parameter, double value)
    {
        var rounded = Math.Round(value);
        if (Math.Abs(value - rounded) > IntegerTolerance)
        {
            throw CreateConversionException(parameter, "integer");
        }

        return checked((int)rounded);
    }

    private static ArrayElementKind ResolveArrayElementKind(TemplateParameter? parameter)
    {
        if (parameter?.ArrayOf != null &&
            parameter.ArrayOf.Equals("int", StringComparison.OrdinalIgnoreCase))
        {
            return ArrayElementKind.Int;
        }

        return ArrayElementKind.Double;
    }

    private static bool IsArrayParameter(TemplateParameter? parameter)
    {
        return parameter != null &&
               parameter.Type != null &&
               parameter.Type.Equals("array", StringComparison.OrdinalIgnoreCase);
    }

    private static TemplateValidationException CreateConversionException(TemplateParameter? parameter, string expected)
    {
        var name = parameter?.Name ?? "parameter";
        return new TemplateValidationException($"Parameter '{name}' expects {expected} values.");
    }

    private enum ArrayElementKind
    {
        Double,
        Int
    }
}
