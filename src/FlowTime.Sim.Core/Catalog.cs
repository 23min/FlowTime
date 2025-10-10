using System.ComponentModel.DataAnnotations;

namespace FlowTime.Sim.Core;

/// <summary>
/// Catalog.v1 - Domain-neutral system catalog defining components, connections, and layout hints
/// for both simulation and UI diagram rendering.
/// </summary>
public sealed record Catalog
{
    [Required]
    public int Version { get; init; } = 1;

    [Required]
    public CatalogMetadata Metadata { get; init; } = new();

    [Required]
    public List<CatalogComponent> Components { get; init; } = new();

    public List<CatalogConnection> Connections { get; init; } = new();

    [Required]
    public List<string> Classes { get; init; } = new() { "DEFAULT" };

    public CatalogLayoutHints? LayoutHints { get; init; }

    /// <summary>
    /// Gets components as read-only list for API consumers.
    /// </summary>
    public IReadOnlyList<CatalogComponent> ComponentsReadOnly => Components;

    /// <summary>
    /// Gets connections as read-only list for API consumers.
    /// </summary>
    public IReadOnlyList<CatalogConnection> ConnectionsReadOnly => Connections;

    /// <summary>
    /// Gets classes as read-only list for API consumers.
    /// </summary>
    public IReadOnlyList<string> ClassesReadOnly => Classes;

    /// <summary>
    /// Validates that component IDs are unique and connections reference valid components.
    /// </summary>
    public ValidationResult Validate()
    {
        var errors = new List<string>();

        // Check for duplicate component IDs
        var componentIds = Components.Select(c => c.Id).ToList();
        var duplicates = componentIds.GroupBy(id => id).Where(g => g.Count() > 1).Select(g => g.Key);
        foreach (var duplicate in duplicates)
        {
            errors.Add($"Duplicate component ID: {duplicate}");
        }

        // Check that connections reference valid components
        var validIds = new HashSet<string>(componentIds);
        foreach (var connection in Connections)
        {
            if (!validIds.Contains(connection.From))
            {
                errors.Add($"Connection references unknown component: {connection.From}");
            }
            if (!validIds.Contains(connection.To))
            {
                errors.Add($"Connection references unknown component: {connection.To}");
            }
        }

        // Validate component ID format (must match Gold component_id requirements)
        foreach (var component in Components)
        {
            if (!IsValidComponentId(component.Id))
            {
                errors.Add($"Invalid component ID format: {component.Id}. Must contain only A-Z a-z 0-9 _ - .");
            }
        }

        return new ValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors
        };
    }

    private static bool IsValidComponentId(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;
        
        // Component IDs must match Gold component_id requirements
        // Allowed characters: A-Z a-z 0-9 _ - .
        return id.All(c => char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '.');
    }
}

public sealed record CatalogMetadata
{
    [Required]
    public string Id { get; init; } = string.Empty;

    [Required] 
    public string Title { get; init; } = string.Empty;

    public string? Description { get; init; }
}

public sealed record CatalogComponent
{
    [Required]
    public string Id { get; init; } = string.Empty;

    [Required]
    public string Label { get; init; } = string.Empty;

    public string? Description { get; init; }
}

public sealed record CatalogConnection
{
    [Required]
    public string From { get; init; } = string.Empty;

    [Required]
    public string To { get; init; } = string.Empty;

    public string? Label { get; init; }
}

public sealed record CatalogLayoutHints
{
    public string? RankDir { get; init; } = "LR";
    public int? Spacing { get; init; }
}

public sealed record ValidationResult
{
    public bool IsValid { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
}
