using System.Globalization;
using System.Text.Json.Serialization;
using FlowTime.Core.Compiler;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace FlowTime.Core.Models;

[JsonConverter(typeof(ParallelismReferenceJsonConverter))]
public sealed record ParallelismReference : IYamlConvertible
{
    public double? Constant { get; private set; }
    public CompiledSeriesReference? SeriesReference { get; private set; }

    public static ParallelismReference Literal(double value)
    {
        if (!double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Parallelism constant must be finite.");
        }

        return new ParallelismReference { Constant = value };
    }

    public static ParallelismReference Series(string reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            throw new ArgumentException("Parallelism reference must not be empty.", nameof(reference));
        }

        return new ParallelismReference { SeriesReference = SemanticReferenceResolver.ParseSeriesReference(reference) };
    }

    public static ParallelismReference? Parse(object? value)
    {
        if (value is null)
        {
            return null;
        }

        if (value is ParallelismReference reference)
        {
            return reference;
        }

        if (value is string text)
        {
            var trimmed = text.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                throw new InvalidOperationException("Parallelism must be a number or series reference.");
            }

            return double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var numeric)
                ? Literal(numeric)
                : Series(trimmed);
        }

        if (value is IConvertible)
        {
            return Literal(Convert.ToDouble(value, CultureInfo.InvariantCulture));
        }

        throw new InvalidOperationException("Parallelism must be a number or series reference.");
    }

    public object? ToScalar()
    {
        if (Constant.HasValue)
        {
            return Constant.Value;
        }

        return SeriesReference?.ToAuthoredValue();
    }

    public string? ToAuthoredValue()
    {
        var authored = ToScalar();
        if (authored is double constant)
        {
            return constant.ToString("G", CultureInfo.InvariantCulture);
        }

        return authored as string;
    }

    public void Read(IParser parser, Type expectedType, ObjectDeserializer nestedObjectDeserializer)
    {
        var scalar = parser.Consume<Scalar>();

        if (scalar.Tag == "tag:yaml.org,2002:null" ||
            string.Equals(scalar.Value, "null", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(scalar.Value, "~", StringComparison.Ordinal) ||
            (scalar.Style == ScalarStyle.Plain && string.IsNullOrWhiteSpace(scalar.Value)))
        {
            Constant = null;
            SeriesReference = null;
            return;
        }

        var parsed = Parse(scalar.Value) ?? throw new InvalidOperationException("Parallelism must be a number or series reference.");
        Constant = parsed.Constant;
        SeriesReference = parsed.SeriesReference;
    }

    public void Write(IEmitter emitter, ObjectSerializer nestedObjectSerializer)
    {
        var scalar = ToScalar();
        switch (scalar)
        {
            case double numeric:
                emitter.Emit(new Scalar(numeric.ToString("G", CultureInfo.InvariantCulture)));
                break;
            case string text:
                emitter.Emit(new Scalar(text));
                break;
            default:
                emitter.Emit(new Scalar("null"));
                break;
        }
    }
}