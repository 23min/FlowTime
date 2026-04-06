using System.Text.Json;
using System.Text.Json.Serialization;

namespace FlowTime.Core.Models;

public sealed class ParallelismReferenceJsonConverter : JsonConverter<ParallelismReference>
{
    public override ParallelismReference? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.Null => null,
            JsonTokenType.String => ParallelismReference.Parse(reader.GetString()),
            JsonTokenType.Number when reader.TryGetDouble(out var numeric) => ParallelismReference.Literal(numeric),
            _ => throw new JsonException("Parallelism must be a number or series reference.")
        };
    }

    public override void Write(Utf8JsonWriter writer, ParallelismReference value, JsonSerializerOptions options)
    {
        var scalar = value.ToScalar();
        switch (scalar)
        {
            case double numeric:
                writer.WriteNumberValue(numeric);
                return;
            case string reference:
                writer.WriteStringValue(reference);
                return;
            case null:
                writer.WriteNullValue();
                return;
            default:
                throw new JsonException("Parallelism must be a number or series reference.");
        }
    }
}