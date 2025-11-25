using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace FlowTime.UI.TimeTravel;

public enum ClassSelectionMode
{
    All,
    Single,
    Multi
}

public sealed class ClassSelectionState
{
    public const int MaxSelections = 3;

    private readonly Dictionary<string, string> canonicalClasses;
    private readonly List<string> selected = new(MaxSelections);

    public ClassSelectionMode Mode { get; private set; } = ClassSelectionMode.All;

    public IReadOnlyList<string> SelectedClasses => selected;

    public ClassSelectionState(IReadOnlyList<string> availableClasses)
    {
        ArgumentNullException.ThrowIfNull(availableClasses);

        canonicalClasses = availableClasses
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToDictionary(c => c, c => c, StringComparer.OrdinalIgnoreCase);
    }

    public void ApplyQueryValue(string? value)
    {
        selected.Clear();

        if (string.IsNullOrWhiteSpace(value))
        {
            UpdateMode();
            return;
        }

        var requested = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var requestedClass in requested)
        {
            if (selected.Count >= MaxSelections)
            {
                break;
            }

            if (!TryGetCanonical(requestedClass, out var canonical) ||
                selected.Contains(canonical, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            selected.Add(canonical);
        }

        SortSelection();
        UpdateMode();
    }

    public void Toggle(string classId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(classId);

        if (!TryGetCanonical(classId, out var canonical))
        {
            return;
        }

        var existingIndex = selected.FindIndex(c => string.Equals(c, canonical, StringComparison.OrdinalIgnoreCase));
        if (existingIndex >= 0)
        {
            selected.RemoveAt(existingIndex);
            SortSelection();
            UpdateMode();
            return;
        }

        if (selected.Count >= MaxSelections)
        {
            return;
        }

        selected.Add(canonical);
        SortSelection();
        UpdateMode();
    }

    public string? GetQueryValue()
    {
        return Mode == ClassSelectionMode.All
            ? null
            : string.Join(",", selected);
    }

    private bool TryGetCanonical(string input, [NotNullWhen(true)] out string? canonical)
    {
        canonical = null;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        return canonicalClasses.TryGetValue(input.Trim(), out canonical);
    }

    private void SortSelection()
    {
        selected.Sort(StringComparer.OrdinalIgnoreCase);
    }

    private void UpdateMode()
    {
        Mode = selected.Count switch
        {
            0 => ClassSelectionMode.All,
            1 => ClassSelectionMode.Single,
            _ => ClassSelectionMode.Multi
        };
    }
}
