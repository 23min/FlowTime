namespace FlowTime.Core.Models;

public enum ClassEntryKind
{
    Specific,
    Fallback
}

public sealed record ClassEntry<TPayload>
{
    public required ClassEntryKind Kind { get; init; }
    public string? ClassId { get; init; }
    public required string ContractKey { get; init; }
    public required TPayload Payload { get; init; }

    public static ClassEntry<TPayload> Specific(string classId, TPayload payload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(classId);

        var normalized = classId.Trim();
        return new ClassEntry<TPayload>
        {
            Kind = ClassEntryKind.Specific,
            ClassId = normalized,
            ContractKey = normalized,
            Payload = payload
        };
    }

    public static ClassEntry<TPayload> Fallback(TPayload payload)
    {
        return new ClassEntry<TPayload>
        {
            Kind = ClassEntryKind.Fallback,
            ContractKey = "*",
            Payload = payload
        };
    }
}