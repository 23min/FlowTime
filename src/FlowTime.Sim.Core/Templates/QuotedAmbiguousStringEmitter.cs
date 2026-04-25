using System;
using System.Globalization;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.EventEmitters;

namespace FlowTime.Sim.Core.Templates;

/// <summary>
/// Forces <see cref="ScalarStyle.DoubleQuoted"/> on string values whose literal text would
/// otherwise re-resolve as a non-string YAML 1.2 plain scalar (bool / int / float / null).
/// Sibling of <see cref="FlowSequenceEventEmitter"/>; both are wired through
/// <c>SerializerBuilder.WithEventEmitter</c> in
/// <see cref="Services.TemplateService.CreateYamlSerializer"/>.
///
/// <para>Why this exists.</para>
/// YamlDotNet's default string emission writes plain (unquoted) scalars whenever the
/// literal text is "safe" — including text whose surface matches a YAML 1.2 type-resolution
/// pattern. Round-tripping a DTO whose <c>string</c> field carries the literal "0", "true",
/// "3.14", or "null" therefore loses author intent: the emitted line <c>expr: 0</c> is a
/// plain scalar that the YAML 1.2 resolver types as <c>integer</c>, not <c>string</c>.
/// Schema validators that declare <c>type: string</c> reject the resulting wire form (m-E24-04
/// canary, 231 errors across four shapes — <c>nodes[].expr</c>, <c>metadata/graph.hidden</c>,
/// <c>metadata/pmf.expected</c>).
///
/// <para>Why source-type-driven (not value-driven, not attribute-driven).</para>
/// The fix activates on <c>eventInfo.Source.Type == typeof(string)</c>. Anything declared as
/// a C# <c>string</c> — including <c>Dictionary&lt;string, string&gt;</c> entries — is
/// guarded automatically. Numeric / bool / array fields are left alone (their source type
/// drives YamlDotNet's normal scalar emission). A new string field added to any DTO is
/// guarded the moment it ships, without touching this class. That is the future-proofing
/// the milestone tracking calls "no indirection" — the rule lives at the serializer level,
/// once, on the type the rule is about.
///
/// <para>Why double-quoted, not single-quoted.</para>
/// Both styles round-trip as strings under the validator's post-m-E24-04 ParseScalar fix.
/// Double-quoted is chosen because it is already the dominant style in the on-disk
/// templates (e.g. <c>expr: "0"</c>, <c>graph.hidden: "true"</c>), so the wire shape after
/// emission matches the wire shape before parameter substitution. No second style appears
/// in production output.
///
/// <para>YAML 1.2 ambiguity surface.</para>
/// The literal text is treated as ambiguous if it matches:
/// <list type="bullet">
///   <item><description>YAML booleans (<c>true</c>, <c>false</c>, case-insensitive).</description></item>
///   <item><description>YAML nulls (<c>null</c>, <c>~</c>, empty string, case-insensitive).</description></item>
///   <item><description>An integer literal (<see cref="int"/>.<see cref="int.TryParse(string?, NumberStyles, IFormatProvider, out int)"/>
///     under <see cref="NumberStyles.Integer"/> + invariant culture).</description></item>
///   <item><description>A floating-point literal (<see cref="double"/>.<see cref="double.TryParse(string?, NumberStyles, IFormatProvider, out double)"/>
///     under <see cref="NumberStyles.Float"/> + invariant culture).</description></item>
/// </list>
/// All other strings emit with <see cref="ScalarStyle.Any"/> so YamlDotNet picks its
/// existing default (plain when safe, escaped when not). The ambiguity test mirrors the
/// validator's post-fix ParseScalar coercion attempt-order exactly — the round-trip pair
/// (emitter ⇄ validator) is symmetric by construction.
/// </summary>
internal sealed class QuotedAmbiguousStringEmitter : ChainedEventEmitter
{
    public QuotedAmbiguousStringEmitter(IEventEmitter next)
        : base(next)
    {
    }

    public override void Emit(ScalarEventInfo eventInfo, IEmitter emitter)
    {
        if (eventInfo.Source.Type == typeof(string)
            && eventInfo.Source.Value is string text
            && IsYamlTypeAmbiguous(text))
        {
            eventInfo.Style = ScalarStyle.DoubleQuoted;
        }

        base.Emit(eventInfo, emitter);
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="text"/>, emitted as a plain
    /// scalar, would be re-resolved by YAML 1.2 as a non-string value (bool / int / float
    /// / null). Mirror of the validator's plain-scalar coercion attempt-order in
    /// <c>ModelSchemaValidator.ParseScalar</c> — keep the two in lock-step.
    /// </summary>
    private static bool IsYamlTypeAmbiguous(string text)
    {
        // YAML 1.2 null forms surface as plain scalars that would re-resolve to null.
        if (text.Length == 0
            || string.Equals(text, "null", StringComparison.OrdinalIgnoreCase)
            || text == "~")
        {
            return true;
        }

        // Booleans — YamlDotNet recognizes the lowercase forms; bool.TryParse covers both
        // casings, which is the conservative choice (over-quoting "True" is harmless;
        // under-quoting it would corrupt author intent).
        if (bool.TryParse(text, out _))
        {
            return true;
        }

        // Integer / float — same NumberStyles + culture as the validator's ParseScalar so
        // the round-trip pair is symmetric. Float covers integers too, but checking int
        // first keeps the order legible and matches the validator.
        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
        {
            return true;
        }

        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
        {
            return true;
        }

        return false;
    }
}
