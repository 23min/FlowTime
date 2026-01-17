using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;

namespace FlowTime.Sim.Core.Hashing;

public sealed record RunHashInput(
    string TemplateId,
    string TemplateVersion,
    string Mode,
    IReadOnlyDictionary<string, object?>? Parameters,
    IReadOnlyDictionary<string, string>? TelemetryBindings,
    string RngKind,
    int RngSeed);

public static class RunHashCalculator
{
    private static readonly IReadOnlyDictionary<string, object?> emptyParameterDictionary = new Dictionary<string, object?>(StringComparer.Ordinal);
    private static readonly IReadOnlyDictionary<string, string> emptyBindingDictionary = new Dictionary<string, string>(StringComparer.Ordinal);

    public static string ComputeHash(RunHashInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var parameters = input.Parameters ?? emptyParameterDictionary;
        var bindings = input.TelemetryBindings ?? emptyBindingDictionary;

        var buffer = new ArrayBufferWriter<byte>(512);
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { SkipValidation = true }))
        {
            writer.WriteStartObject();
            writer.WriteString("templateId", input.TemplateId ?? string.Empty);
            writer.WriteString("templateVersion", input.TemplateVersion ?? string.Empty);
            writer.WriteString("mode", (input.Mode ?? string.Empty).ToLowerInvariant());
            writer.WritePropertyName("parameters");
            WriteSortedDictionary(writer, parameters);
            writer.WritePropertyName("telemetryBindings");
            WriteSortedDictionary(writer, bindings);
            writer.WritePropertyName("rng");
            writer.WriteStartObject();
            writer.WriteString("kind", input.RngKind ?? "pcg32");
            writer.WriteNumber("seed", input.RngSeed);
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        var hash = SHA256.HashData(buffer.WrittenSpan);
        return "sha256:" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static void WriteSortedDictionary(Utf8JsonWriter writer, IReadOnlyDictionary<string, object?>? dictionary)
    {
        if (dictionary is null || dictionary.Count == 0)
        {
            writer.WriteStartObject();
            writer.WriteEndObject();
            return;
        }

        writer.WriteStartObject();
        foreach (var kvp in dictionary.OrderBy(k => k.Key, StringComparer.Ordinal))
        {
            writer.WritePropertyName(kvp.Key ?? string.Empty);
            WriteJsonValue(writer, kvp.Value);
        }

        writer.WriteEndObject();
    }

    private static void WriteSortedDictionary(Utf8JsonWriter writer, IReadOnlyDictionary<string, string>? dictionary)
    {
        if (dictionary is null || dictionary.Count == 0)
        {
            writer.WriteStartObject();
            writer.WriteEndObject();
            return;
        }

        writer.WriteStartObject();
        foreach (var kvp in dictionary.OrderBy(k => k.Key, StringComparer.Ordinal))
        {
            writer.WriteString(kvp.Key ?? string.Empty, kvp.Value ?? string.Empty);
        }

        writer.WriteEndObject();
    }

    private static void WriteJsonValue(Utf8JsonWriter writer, object? value)
    {
        switch (value)
        {
            case null:
                writer.WriteNullValue();
                return;
            case string s:
                writer.WriteStringValue(s);
                return;
            case bool b:
                writer.WriteBooleanValue(b);
                return;
            case sbyte sb:
                writer.WriteNumberValue(sb);
                return;
            case byte b8:
                writer.WriteNumberValue(b8);
                return;
            case short s16:
                writer.WriteNumberValue(s16);
                return;
            case ushort us16:
                writer.WriteNumberValue(us16);
                return;
            case int i32:
                writer.WriteNumberValue(i32);
                return;
            case uint ui32:
                writer.WriteNumberValue(ui32);
                return;
            case long i64:
                writer.WriteNumberValue(i64);
                return;
            case ulong ui64:
                writer.WriteNumberValue(ui64);
                return;
            case float f:
                writer.WriteNumberValue(f);
                return;
            case double d:
                writer.WriteNumberValue(d);
                return;
            case decimal m:
                writer.WriteNumberValue(m);
                return;
            case JsonElement element:
                element.WriteTo(writer);
                return;
            case IReadOnlyDictionary<string, object?> readOnlyDict:
                WriteSortedDictionary(writer, readOnlyDict);
                return;
            case IDictionary<string, object?> dict:
                WriteSortedDictionary(writer, new Dictionary<string, object?>(dict, StringComparer.Ordinal));
                return;
            case IDictionary genericDict:
                WriteSortedDictionary(writer, ToStringDictionary(genericDict));
                return;
            case IEnumerable enumerable when value is not string:
                writer.WriteStartArray();
                foreach (var item in enumerable)
                {
                    WriteJsonValue(writer, item);
                }
                writer.WriteEndArray();
                return;
            default:
                writer.WriteStringValue(Convert.ToString(value, CultureInfo.InvariantCulture));
                return;
        }
    }

    private static IReadOnlyDictionary<string, object?> ToStringDictionary(IDictionary dictionary)
    {
        var map = new Dictionary<string, object?>(dictionary.Count, StringComparer.Ordinal);
        foreach (DictionaryEntry entry in dictionary)
        {
            map[entry.Key?.ToString() ?? string.Empty] = entry.Value;
        }

        return map;
    }
}
